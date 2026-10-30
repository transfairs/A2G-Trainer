using System;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Resolves the true, save-persistent address of a player's Firstname/Lastname.
    /// </summary>
    /// <remarks>
    /// The address the rest of this project uses for names (Settings.PlayerAddress via
    /// AddressPresets.*[FIRSTNAME]/[LASTNAME]) is a transient *display cache*. Writing there updates
    /// what's shown on screen, but the value is rebuilt from scratch (at a new heap address) every
    /// time a savegame loads, so it doesn't actually persist a rename. The real, permanent string
    /// lives in a single pool of tightly packed, null-terminated strings that's rebuilt fresh (at a
    /// new heap address) on every load. Two things about that pool ARE stable:
    /// <list type="number">
    /// <item>anstoss2.exe+7FDD58 always holds a pointer to the current pool's start, no matter when
    /// you read it (confirmed stable across restarts and across different savegames).</item>
    /// <item>Every player has a small fixed-size record at anstoss2.exe+516678 + PlayerId*0xC8 whose
    /// first 4 bytes are two 16-bit indices: [+0] = which string in the pool is their Firstname,
    /// [+2] = which string is their Lastname. PlayerId here is the existing 2-byte value at
    /// offset 0 of the player's normal (display-cache) struct - the same slot PlayerController
    /// already reads as a 1-byte "Id" today; NameRecordId on Player is the same field read wide.</item>
    /// </list>
    /// Both offsets above are the original build's; the attached build's actual anchors come from
    /// <see cref="ProcessMemory.Layout"/> (see PersistentLayout/PersistentLayoutCalibrator for GOG).
    /// The pool has no fixed stride - names are variable length, so "the Nth string" only turns into
    /// a byte offset by counting null terminators from the start; there's no arithmetic shortcut.
    /// That also means overwriting one entry with a replacement of a DIFFERENT byte length would shift
    /// every later name in the pool.
    /// <para>
    /// The pool is also deduplicated: every "Jakob" in the game points at the SAME pool entry, so
    /// overwriting an entry renames every player sharing it, not just the one being edited. A
    /// rename therefore repoints that one player's index at an existing entry holding the new name
    /// (<see cref="FindIndexOf"/> + <see cref="RepointFirstname"/>/<see cref="RepointLastname"/>),
    /// and only overwrites an entry's bytes when <see cref="CountReferences"/> shows that no other
    /// player uses it.
    /// </para>
    /// </remarks>
    public class NamePoolResolver
    {
        private const int FirstnameIndexOffset = 0;
        private const int LastnameIndexOffset = 2;
        private const int SelfReferenceOffset = 4; // each record's own PlayerId

        // Starting size for the scan buffer, doubled until the target index is found or MaxScanBufferSize
        // is hit. A fixed 64KB buffer turned out too small in practice - Lastname indices for some
        // players run well past that (observed a failure to resolve at 64KB that a bigger buffer
        // fixed), so we grow instead of guessing one size.
        private const int InitialScanBufferSize = 0x10000;
        private const int MaxScanBufferSize = 0x800000; // 8 MB - comfortably above anything observed

        private readonly ProcessMemory memory;
        private readonly PlayerRecordResolver records;
        private readonly uint? poolPointerOffset;
        // Table/count/end-pointer anchors (Settings.NamePoolTableOffset etc.), moved like all runtime
        // data in other builds (see FixedAddresses.RuntimeShiftFor). Without them names are found by
        // counting null terminators and can't be appended.
        private readonly bool hasPoolBookkeeping;
        private readonly uint poolBookkeepingShift;

        /// <summary>Creates a resolver that reads the persistent name pool through the given process memory accessor and its current <see cref="ProcessMemory.Layout"/>.</summary>
        public NamePoolResolver(ProcessMemory memory) : this(memory, memory.Layout)
        {
        }

        /// <summary>Creates a resolver for an explicit layout (e.g. a calibration candidate) instead of the memory's current one.</summary>
        public NamePoolResolver(ProcessMemory memory, PersistentLayout layout)
        {
            this.memory = memory;
            this.records = new PlayerRecordResolver(memory, layout);
            this.poolPointerOffset = layout.NamePoolPointerOffset;
            uint? shift = FixedAddresses.RuntimeShiftFor(memory, layout);
            this.hasPoolBookkeeping = shift.HasValue && this.poolPointerOffset.HasValue;
            this.poolBookkeepingShift = shift ?? 0;
        }

        /// <summary>Whether <see cref="AppendName"/> is available for the attached build.</summary>
        public bool CanAppend => this.hasPoolBookkeeping;

        /// <summary>
        /// Appends <paramref name="value"/> as a new pool entry the way the game's own add-a-name
        /// function does (FUN_005b3380) and returns its index, or null if that isn't possible here
        /// (unknown build, empty name, pool or table full, memory unreadable). The caller then points
        /// a player's Firstname/Lastname index at it (<see cref="RepointFirstname"/>/<see cref="RepointLastname"/>);
        /// nothing else in the pool moves, so no other player is affected.
        /// </summary>
        /// <remarks>
        /// The name is trimmed first, like the game does (FUN_005b3540). Callers should try
        /// <see cref="FindIndexOf"/> first; this only re-checks when trimming changed the name.
        /// </remarks>
        public ushort? AppendName(string value, Encoding encoding)
        {
            if (!this.hasPoolBookkeeping)
                return null;

            string name = NormalizeLikeGame(value);
            if (name.Length == 0)
                return null;

            // Clean-up can turn the value into a name the pool already has - reuse that, like the game.
            if (name != value)
            {
                ushort? existing = this.FindIndexOf(name, encoding);
                if (existing.HasValue)
                    return existing;
            }

            uint? poolBase = this.ReadPoolBase();
            uint? poolEnd = this.ReadUInt32(this.EndPointerOffset);
            ushort? count = this.ReadUInt16(this.CountOffset);
            if (!poolBase.HasValue || !poolEnd.HasValue || !count.HasValue || poolEnd.Value < poolBase.Value)
                return null;

            byte[] bytes = encoding.GetBytes(name + "\0");
            if (poolEnd.Value - poolBase.Value + (uint)bytes.Length > Settings.NamePoolCapacity)
            {
                Logger.Warn($"AppendName: name pool full (0x{poolEnd.Value - poolBase.Value:X} of 0x{Settings.NamePoolCapacity:X} bytes used) - \"{name}\" not added.");
                return null;
            }
            if (count.Value >= Settings.NamePoolTableCapacity)
            {
                Logger.Warn($"AppendName: name table full ({count.Value} of {Settings.NamePoolTableCapacity} entries) - \"{name}\" not added.");
                return null;
            }

            // Same order as the game: string, table entry, then the two counters - so the table
            // never points at an entry that isn't written yet.
            this.memory.WriteBytesAtAddress(poolEnd.Value, bytes);
            this.memory.WriteBytesAtAddress(this.TableEntryAddress(count.Value), BitConverter.GetBytes(poolEnd.Value));
            this.memory.WriteBytesAtAddress(this.memory.ModuleBase + this.CountOffset, BitConverter.GetBytes((ushort)(count.Value + 1)));
            this.memory.WriteBytesAtAddress(this.memory.ModuleBase + this.EndPointerOffset, BitConverter.GetBytes(poolEnd.Value + (uint)bytes.Length));
            Logger.Debug($"AppendName: added \"{name}\" as pool index {count.Value} at 0x{poolEnd.Value:X}.");
            return count.Value;
        }

        /// <summary>The game's own clean-up before adding a name (FUN_005b3540): cut at the first control character, then trim surrounding spaces.</summary>
        internal static string NormalizeLikeGame(string value)
        {
            if (value == null)
                return string.Empty;

            int start = 0;
            while (start < value.Length && value[start] <= ' ')
                start++;
            int end = start;
            while (end < value.Length && value[end] >= ' ')
                end++;
            return value.Substring(start, end - start).TrimEnd(' ');
        }

        private uint TableEntryAddress(ushort index) => this.memory.ModuleBase + Settings.NamePoolTableOffset + this.poolBookkeepingShift + (uint)index * 4;
        private uint CountOffset => Settings.NamePoolCountOffset + this.poolBookkeepingShift;
        private uint EndPointerOffset => Settings.NamePoolEndPointerOffset + this.poolBookkeepingShift;

        private uint? ReadUInt32(uint moduleOffset)
        {
            byte[] bytes = this.memory.ReadBytesAtAddress(this.memory.ModuleBase + moduleOffset, 4);
            return bytes == null ? (uint?)null : BitConverter.ToUInt32(bytes, 0);
        }

        private ushort? ReadUInt16(uint moduleOffset)
        {
            byte[] bytes = this.memory.ReadBytesAtAddress(this.memory.ModuleBase + moduleOffset, 2);
            return bytes == null ? (ushort?)null : BitConverter.ToUInt16(bytes, 0);
        }

        // The game's own lookup: table[index], valid only below the entry count.
        private uint? ResolveViaTable(int index)
        {
            ushort? count = this.ReadUInt16(this.CountOffset);
            if (!count.HasValue || index >= count.Value)
                return null;

            byte[] entry = this.memory.ReadBytesAtAddress(this.TableEntryAddress((ushort)index), 4);
            if (entry == null)
                return null;
            uint address = BitConverter.ToUInt32(entry, 0);
            return address < 0x10000 ? (uint?)null : address;
        }

        /// <summary>Resolves the persistent address of the given player's Firstname string, or null if it can't be resolved.</summary>
        public uint? ResolveFirstnameAddress(ushort playerId) => this.Resolve(playerId, FirstnameIndexOffset);

        /// <summary>Resolves the persistent address of the given player's Lastname string, or null if it can't be resolved.</summary>
        public uint? ResolveLastnameAddress(ushort playerId) => this.Resolve(playerId, LastnameIndexOffset);

        /// <summary>Points the given player's Firstname at pool entry <paramref name="index"/>, leaving the pool itself (and every other player sharing the old entry) untouched.</summary>
        public void RepointFirstname(ushort playerId, ushort index) => this.Repoint(playerId, FirstnameIndexOffset, index);

        /// <summary>Points the given player's Lastname at pool entry <paramref name="index"/>, leaving the pool itself (and every other player sharing the old entry) untouched.</summary>
        public void RepointLastname(ushort playerId, ushort index) => this.Repoint(playerId, LastnameIndexOffset, index);

        /// <summary>
        /// Returns the index of the first pool entry exactly equal to <paramref name="value"/>, or
        /// null if the pool has no such entry (or can't be read).
        /// </summary>
        /// <remarks>
        /// The pool's entry count isn't stored anywhere we know of, so the scan stops at the first
        /// entry that can't be a name (contains a control byte) - past the pool's end lies unrelated
        /// heap data, and a "match" there would hand the game an index it can't resolve.
        /// </remarks>
        public ushort? FindIndexOf(string value, Encoding encoding)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            uint? poolBase = this.ReadPoolBase();
            if (!poolBase.HasValue)
                return null;

            byte[] target = encoding.GetBytes(value);
            for (int scanBufferSize = InitialScanBufferSize; scanBufferSize <= MaxScanBufferSize; scanBufferSize *= 2)
            {
                byte[] buffer = this.memory.ReadBytesAtAddress(poolBase.Value, scanBufferSize);
                if (buffer == null)
                    return null;

                int pos = 0;
                bool ranOffEndOfBuffer = false;
                for (int index = 0; index <= ushort.MaxValue; index++)
                {
                    int nullIndex = Array.IndexOf(buffer, (byte)0, pos);
                    if (nullIndex < 0)
                    {
                        ranOffEndOfBuffer = true;
                        break; // this buffer size wasn't enough - retry with a bigger one
                    }

                    for (int i = pos; i < nullIndex; i++)
                    {
                        if (buffer[i] < 0x20)
                            return null; // ran past the end of the pool without a match
                    }

                    if (nullIndex - pos == target.Length && SequenceEqualAt(buffer, pos, target))
                        return (ushort)index;

                    pos = nullIndex + 1;
                }

                if (!ranOffEndOfBuffer)
                    return null; // every addressable (UInt16) index checked without a match
            }

            return null;
        }

        /// <summary>Reads the pool index the given player's Firstname points at, or null if the record can't be read.</summary>
        public ushort? ReadFirstnameIndex(ushort playerId) => this.ReadIndex(playerId, FirstnameIndexOffset);

        /// <summary>Reads the pool index the given player's Lastname points at, or null if the record can't be read.</summary>
        public ushort? ReadLastnameIndex(ushort playerId) => this.ReadIndex(playerId, LastnameIndexOffset);

        /// <summary>
        /// Counts how many Firstname/Lastname fields across all player records point at pool entry
        /// <paramref name="poolIndex"/>, or null if the record table can't be read.
        /// </summary>
        /// <remarks>
        /// The table's length isn't known, so it's read up to the end of the module (or the last
        /// UInt16-addressable record) and only records whose +4 self-reference equals their own
        /// position count - whatever static data follows the table almost never satisfies that,
        /// so it can't produce phantom "sharers". A stray match only ever over-counts, which makes
        /// callers refuse an in-place write rather than risk renaming someone else.
        /// </remarks>
        public int? CountReferences(ushort poolIndex)
        {
            if (!this.records.IsAvailable)
                return null;

            uint tableStart = this.records.GetFieldAddress(0, 0);
            long moduleEnd = (long)this.memory.ModuleBase + this.memory.ModuleSize;
            long tableLength = Math.Min(moduleEnd - tableStart, (long)(ushort.MaxValue + 1) * Settings.PlayerRecordStride);
            if (tableLength < Settings.PlayerRecordStride)
                return null;

            byte[] table = this.memory.ReadBytesAtAddress(tableStart, (int)tableLength);
            if (table == null)
                return null;

            int count = 0;
            for (int id = 0; (long)(id + 1) * Settings.PlayerRecordStride <= table.Length; id++)
            {
                int record = id * (int)Settings.PlayerRecordStride;
                if (BitConverter.ToUInt16(table, record + SelfReferenceOffset) != id)
                    continue;
                if (BitConverter.ToUInt16(table, record + FirstnameIndexOffset) == poolIndex)
                    count++;
                if (BitConverter.ToUInt16(table, record + LastnameIndexOffset) == poolIndex)
                    count++;
            }
            return count;
        }

        private ushort? ReadIndex(ushort playerId, int indexFieldOffset)
        {
            if (!this.records.IsAvailable)
                return null;

            byte[] indexBytes = this.memory.ReadBytesAtAddress(this.records.GetFieldAddress(playerId, (uint)indexFieldOffset), 2);
            return indexBytes == null ? (ushort?)null : BitConverter.ToUInt16(indexBytes, 0);
        }

        private static bool SequenceEqualAt(byte[] buffer, int offset, byte[] target)
        {
            for (int i = 0; i < target.Length; i++)
            {
                if (buffer[offset + i] != target[i])
                    return false;
            }
            return true;
        }

        private void Repoint(ushort playerId, int indexFieldOffset, ushort index)
        {
            this.memory.WriteBytesAtAddress(this.records.GetFieldAddress(playerId, (uint)indexFieldOffset), BitConverter.GetBytes(index));
        }

        private uint? ReadPoolBase()
        {
            if (!this.poolPointerOffset.HasValue)
                return null;

            byte[] poolBaseBytes = this.memory.ReadBytesAtAddress(this.memory.ModuleBase + this.poolPointerOffset.Value, 4);
            if (poolBaseBytes == null)
                return null;
            uint poolBase = BitConverter.ToUInt32(poolBaseBytes, 0);
            return poolBase < 0x10000 ? (uint?)null : poolBase;
        }

        private uint? Resolve(ushort playerId, int indexFieldOffset)
        {
            // Unknown/unconfirmed anchors for this build (see PersistentLayout) - callers fall back
            // to the display cache, same as for any other unresolvable name.
            if (!this.records.IsAvailable || !this.poolPointerOffset.HasValue)
                return null;

            byte[] indexBytes = this.memory.ReadBytesAtAddress(this.records.GetFieldAddress(playerId, (uint)indexFieldOffset), 2);
            if (indexBytes == null)
                return null;
            int targetIndex = BitConverter.ToUInt16(indexBytes, 0);

            // Where the game's own table is known, use it like the game does: direct, and correct
            // even for appended names regardless of how the pool bytes are laid out.
            if (this.hasPoolBookkeeping)
                return this.ResolveViaTable(targetIndex);

            uint? poolBase = this.ReadPoolBase();
            if (!poolBase.HasValue)
                return null;

            for (int scanBufferSize = InitialScanBufferSize; scanBufferSize <= MaxScanBufferSize; scanBufferSize *= 2)
            {
                byte[] buffer = this.memory.ReadBytesAtAddress(poolBase.Value, scanBufferSize);
                if (buffer == null)
                    return null;

                int pos = 0;
                bool ranOffEndOfBuffer = false;
                for (int i = 0; i < targetIndex; i++)
                {
                    int nullIndex = Array.IndexOf(buffer, (byte)0, pos);
                    if (nullIndex < 0)
                    {
                        ranOffEndOfBuffer = true;
                        break; // this buffer size wasn't enough - retry with a bigger one
                    }
                    pos = nullIndex + 1;
                }

                if (!ranOffEndOfBuffer)
                    return poolBase.Value + (uint)pos;
            }

            return null; // target index still not found even at MaxScanBufferSize - fail safely rather than guess
        }
    }
}
