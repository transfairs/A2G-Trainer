using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Who plays for a club, read the way the game itself finds out (see Settings): the squad and
    /// amateurs from the club record or, while the club is fetched, from its working copy; the youth
    /// players from the youth table; and the working copy the game may hold of a player. With this the
    /// Trainer lists any club's players by their IDs, without the game having to show the club first.
    /// </summary>
    internal static class ClubSquads
    {
        /// <summary>A club with a record, as offered by the club selection.</summary>
        internal sealed class Entry
        {
            /// <summary>Index of the club record (league base + club ID), also the youth table's index.</summary>
            public int Index { get; set; }
            /// <summary>Country of the club.</summary>
            public PlayerEnums.Country Country { get; set; }
            /// <summary>Club ID within its country.</summary>
            public byte Id { get; set; }
            /// <summary>Club name from the record.</summary>
            public string Name { get; set; }

            /// <summary>True if this is the club <paramref name="country"/>/<paramref name="id"/>.</summary>
            public bool Is(PlayerEnums.Country country, ushort id) => this.Country == country && this.Id == id;

            /// <inheritdoc />
            public override string ToString() => this.Name;
        }

        /// <summary>Every club with a record, in record order; empty if the records can't be used.</summary>
        internal static List<Entry> Clubs(ProcessMemory memory, FixedAddresses fixedAddresses)
        {
            List<Entry> clubs = new List<Entry>();
            if (fixedAddresses == null)
                return clubs;

            Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
            uint table = fixedAddresses.Runtime(memory, Settings.ClubRecordTableOffset);
            for (int index = 0; index < Settings.ClubRecordCount; index++)
            {
                uint record = table + (uint)index * Settings.ClubRecordStride;
                byte[] name = memory.ReadBytesAtAddress(record, 19);
                byte[] id = memory.ReadBytesAtAddress(record + Settings.ClubRecordIdOffset, 1);
                byte[] country = memory.ReadBytesAtAddress(record + Settings.ClubRecordCountryOffset, 1);
                if (name == null || id == null || country == null)
                    continue;

                // Records of countries not chosen at the career start stay empty.
                string clubName = latin1.GetString(name).Split('\0')[0];
                PlayerEnums.Country clubCountry = (PlayerEnums.Country)country[0];
                if (clubName.Length == 0 || clubCountry == PlayerEnums.Country.Sonstige || !Enum.IsDefined(typeof(PlayerEnums.Country), clubCountry))
                    continue;
                clubs.Add(new Entry { Index = index, Country = clubCountry, Id = id[0], Name = clubName });
            }
            return clubs;
        }

        /// <summary>
        /// Player IDs of the club's squad followed by its amateurs; the working copy's if the club is
        /// fetched, unless <paramref name="fromRecord"/>. During a match the engine fetches and releases
        /// the clubs all the time, and a released working copy's player pointers can already point at
        /// other players; the record's IDs stay right.
        /// </summary>
        internal static List<ushort> SquadIds(ProcessMemory memory, FixedAddresses fixedAddresses, Entry club, bool fromRecord = false)
        {
            if (fixedAddresses == null)
                return new List<ushort>();

            uint workingCopy = fromRecord ? 0 : ClubRecords.WorkingCopies(memory, fixedAddresses, club.Country, club.Id).FirstOrDefault();
            if (workingCopy != 0)
            {
                List<ushort> ids = PointedIds(memory, workingCopy + Settings.ClubSquadOffset, Count(memory, workingCopy + Settings.ClubSquadCountOffset, 2, Settings.ClubSquadCapacity));
                ids.AddRange(PointedIds(memory, workingCopy + Settings.ClubAmateursOffset, Count(memory, workingCopy + Settings.ClubAmateurCountOffset, 1, Settings.ClubAmateurCapacity)));
                return ids;
            }

            uint record = fixedAddresses.Runtime(memory, Settings.ClubRecordTableOffset) + (uint)club.Index * Settings.ClubRecordStride;
            List<ushort> squad = Ids(memory, record + Settings.ClubRecordSquadOffset, Count(memory, record + Settings.ClubRecordSquadCountOffset, 2, Settings.ClubSquadCapacity));
            squad.AddRange(Ids(memory, record + Settings.ClubRecordAmateursOffset, Count(memory, record + Settings.ClubRecordAmateurCountOffset, 1, Settings.ClubAmateurCapacity)));
            return squad;
        }

        /// <summary>Player IDs of the club's A, B and C youth.</summary>
        internal static List<ushort> YouthIds(ProcessMemory memory, FixedAddresses fixedAddresses, Entry club)
        {
            List<ushort> ids = new List<ushort>();
            if (fixedAddresses == null)
                return ids;

            byte[] entry = memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.YouthTableOffset) + (uint)club.Index * Settings.YouthStride, (int)Settings.YouthStride);
            if (entry == null)
                return ids;

            uint[] offsets = Settings.YouthIdOffsets;
            int[] capacities = Settings.YouthCapacities;
            for (int group = 0; group < offsets.Length; group++)
            {
                int count = Math.Min((int)entry[1 + group], capacities[group]);
                for (int i = 0; i < count; i++)
                    ids.Add(BitConverter.ToUInt16(entry, (int)offsets[group] + i * 2));
            }
            return ids;
        }

        /// <summary>What <see cref="MoveYouthToAgeGroup"/> did.</summary>
        internal enum YouthMove
        {
            /// <summary>The player already was in the group of his age.</summary>
            Unchanged,
            /// <summary>The player was moved to the group of his age.</summary>
            Moved,
            /// <summary>The group of his age is full; the player stays where he was.</summary>
            GroupFull,
            /// <summary>The player isn't among the club's youth (or the table can't be read).</summary>
            NotFound
        }

        /// <summary>Names of the youth groups in table order.</summary>
        internal static readonly string[] YouthGroupNames = { "A-Jugend", "B-Jugend", "C-Jugend" };

        /// <summary>
        /// The youth group the game files a player of <paramref name="age"/> under (FUN_0059fb20), as
        /// an index in table order: 0 = A (17 and older), 1 = B (15, 16), 2 = C (under 15).
        /// </summary>
        internal static int YouthGroup(int age) => age >= 17 ? 0 : age >= 15 ? 1 : 2;

        /// <summary>
        /// Moves a youth player to the group of his age, as the game expects him there: it takes a
        /// youth player out of the group of his age (e.g. FUN_005d4b60), and an appointment to sign
        /// one only takes place for a player in the A youth (FUN_00638c00). Unless his age was just
        /// changed, a player only moves up: the season change (FUN_005cb860) already puts 16-year-olds
        /// in the A youth, which must stay as it is.
        /// </summary>
        internal static YouthMove MoveYouthToAgeGroup(ProcessMemory memory, FixedAddresses fixedAddresses, Entry club, ushort playerId, int age, bool ageChanged, out int from, out int to)
        {
            from = to = YouthGroup(age);
            if (fixedAddresses == null)
                return YouthMove.NotFound;

            uint address = fixedAddresses.Runtime(memory, Settings.YouthTableOffset) + (uint)club.Index * Settings.YouthStride;
            byte[] entry = memory.ReadBytesAtAddress(address, (int)Settings.YouthStride);
            if (entry == null)
                return YouthMove.NotFound;

            uint[] offsets = Settings.YouthIdOffsets;
            int[] capacities = Settings.YouthCapacities;
            int slot = -1;
            for (int group = 0; group < offsets.Length && slot < 0; group++)
            {
                int count = Math.Min((int)entry[1 + group], capacities[group]);
                for (int i = 0; i < count && slot < 0; i++)
                {
                    if (BitConverter.ToUInt16(entry, (int)offsets[group] + i * 2) == playerId)
                    {
                        from = group;
                        slot = i;
                    }
                }
            }
            if (slot < 0)
                return YouthMove.NotFound;
            if (from == to || (!ageChanged && to > from))
                return YouthMove.Unchanged;
            if (entry[1 + to] >= capacities[to])
                return YouthMove.GroupFull;

            // Out of the old group (the ones behind move up, as FUN_0059f670 does), onto the end of the new one.
            int oldCount = Math.Min((int)entry[1 + from], capacities[from]);
            for (int i = slot; i < oldCount - 1; i++)
                Array.Copy(entry, (int)offsets[from] + (i + 1) * 2, entry, (int)offsets[from] + i * 2, 2);
            Array.Clear(entry, (int)offsets[from] + (oldCount - 1) * 2, 2);
            entry[1 + from] = (byte)(oldCount - 1);
            BitConverter.GetBytes(playerId).CopyTo(entry, (int)offsets[to] + entry[1 + to] * 2);
            entry[1 + to]++;

            memory.WriteBytesAtAddress(address, entry);
            return YouthMove.Moved;
        }

        /// <summary>Address of every working copy in the game's player cache, by player ID.</summary>
        internal static Dictionary<ushort, uint> PlayerWorkingCopies(ProcessMemory memory, FixedAddresses fixedAddresses)
        {
            Dictionary<ushort, uint> copies = new Dictionary<ushort, uint>();
            if (fixedAddresses == null)
                return copies;

            uint table = fixedAddresses.Runtime(memory, Settings.PlayerCacheTableOffset);
            byte[] cache = memory.ReadBytesAtAddress(table, Settings.PlayerCacheSlots * (int)Settings.PlayerCacheStride);
            if (cache == null)
                return copies;

            for (int slot = 0; slot < Settings.PlayerCacheSlots; slot++)
            {
                int at = slot * (int)Settings.PlayerCacheStride;
                if (cache[at] == 0)
                    continue;
                ushort id = BitConverter.ToUInt16(cache, at + (int)Settings.PlayerCacheCopyOffset);
                if (!copies.ContainsKey(id))
                    copies.Add(id, table + (uint)at + Settings.PlayerCacheCopyOffset);
            }
            return copies;
        }

        /// <summary>Address of the player's working copy, or null if the game holds none.</summary>
        internal static uint? PlayerWorkingCopy(ProcessMemory memory, FixedAddresses fixedAddresses, ushort playerId) =>
            PlayerWorkingCopies(memory, fixedAddresses).TryGetValue(playerId, out uint copy) ? copy : (uint?)null;

        // A count of 1 or 2 bytes, capped at the room the game has for it; 0 if unreadable.
        private static int Count(ProcessMemory memory, uint address, int size, int capacity)
        {
            byte[] bytes = memory.ReadBytesAtAddress(address, size);
            if (bytes == null)
                return 0;
            int count = size == 2 ? BitConverter.ToInt16(bytes, 0) : bytes[0];
            return Math.Max(0, Math.Min(count, capacity));
        }

        private static List<ushort> Ids(ProcessMemory memory, uint address, int count)
        {
            List<ushort> ids = new List<ushort>();
            byte[] bytes = count > 0 ? memory.ReadBytesAtAddress(address, count * 2) : null;
            if (bytes != null)
                for (int i = 0; i < count; i++)
                    ids.Add(BitConverter.ToUInt16(bytes, i * 2));
            return ids;
        }

        // The IDs at the start of the working copies the pointers lead to.
        private static List<ushort> PointedIds(ProcessMemory memory, uint address, int count)
        {
            List<ushort> ids = new List<ushort>();
            byte[] pointers = count > 0 ? memory.ReadBytesAtAddress(address, count * 4) : null;
            if (pointers == null)
                return ids;
            for (int i = 0; i < count; i++)
            {
                byte[] id = memory.ReadBytesAtAddress(BitConverter.ToUInt32(pointers, i * 4), 2);
                if (id != null)
                    ids.Add(BitConverter.ToUInt16(id, 0));
            }
            return ids;
        }
    }
}
