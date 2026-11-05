using A2G_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// A club's match while the game plays it (original build: match table 0x82E460, 0x26C per match,
    /// count and minute in the match clock 0xC24E10; FUN_005f0120, FUN_005f2040). The engine reads the
    /// line-up and the bench every minute from the club's working copy (+0x8FC/+0x912), but keeps
    /// attitude, hardness, effort and the substitutions made in the match itself, copied from the club
    /// only at kick-off (FUN_005f4620). The game's live tactics dialog takes bench and substitutions
    /// when it opens and writes them back when it closes, so writes only count while it is closed.
    /// </summary>
    internal sealed class LiveMatch
    {
        // Within a match: home team block +0x54, away +0x13C (0xE8 apart).
        private const uint AwayShift = 0xE8;
        private const uint AttitudeOffset = 0x84;
        private const uint HardnessOffset = 0x85;
        private const uint EffortOffset = 0x86;
        private const uint SubstitutionsOffset = 0x88;
        private const uint HumanMaskOffset = 0x240;
        private const int MaxMatches = 150;

        /// <summary>Attitude names (0..6, texts 1517-1522 with "Normal" in the middle).</summary>
        internal static readonly string[] AttitudeNames = { "Abwehrriegel", "Auf Konter", "Vorsichtig", "Normal", "Kontr. offensiv", "Offensiv", "Brechstange" };
        /// <summary>Hardness names (0..4, texts 1610-1614).</summary>
        internal static readonly string[] HardnessNames = { "Gewalttätig", "Rauh", "Normal", "Rücksichtsvoll", "Nonnenhockey" };
        /// <summary>Effort names (0..6; 4 is normal, the game's factor runs from 110 % to 90 %).</summary>
        internal static readonly string[] EffortNames = { "110 %", "107 %", "105 %", "102 %", "Normal", "95 %", "90 %" };
        /// <summary>Substitutions a team may make.</summary>
        internal const byte MaxSubstitutions = 3;
        /// <summary>Places on the bench (club +0x912).</summary>
        internal const int BenchSize = 7;

        private readonly ProcessMemory memory;
        private readonly FixedAddresses fixedAddresses;
        private readonly uint match;
        private readonly uint clock;

        private LiveMatch(ProcessMemory memory, FixedAddresses fixedAddresses, uint match, uint clock, bool isHome, PlayerEnums.Country country, ushort clubId)
        {
            this.memory = memory;
            this.fixedAddresses = fixedAddresses;
            this.match = match;
            this.clock = clock;
            this.IsHome = isHome;
            this.Country = country;
            this.ClubId = clubId;
        }

        /// <summary>Whether the club plays at home.</summary>
        public bool IsHome { get; }
        /// <summary>Country of the club.</summary>
        public PlayerEnums.Country Country { get; }
        /// <summary>ID of the club.</summary>
        public ushort ClubId { get; }

        private uint Team => this.match + (this.IsHome ? 0 : AwayShift);

        /// <summary>The running match of <paramref name="country"/>/<paramref name="clubId"/> with a human team in it, or null.</summary>
        internal static LiveMatch Find(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, ushort clubId) =>
            fixedAddresses == null ? null
                : Find(memory, fixedAddresses, country, clubId, fixedAddresses.Runtime(memory, Settings.MatchTableOffset), fixedAddresses.Runtime(memory, Settings.MatchClockOffset));

        /// <summary>As <see cref="Find(ProcessMemory, FixedAddresses, PlayerEnums.Country, ushort)"/>, with the match table and clock at the given addresses.</summary>
        internal static LiveMatch Find(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, ushort clubId, uint table, uint clockAddress)
        {
            byte[] clock = memory.ReadBytesAtAddress(clockAddress, 0xA);
            if (clock == null)
                return null;
            int count = Math.Min((int)BitConverter.ToInt16(clock, 2), MaxMatches);
            for (int i = 0; i < count; i++)
            {
                uint match = table + (uint)i * Settings.MatchStride;
                byte[] head = memory.ReadBytesAtAddress(match, 8);
                byte[] mask = memory.ReadBytesAtAddress(match + HumanMaskOffset, 2);
                if (head == null || mask == null || BitConverter.ToInt16(mask, 0) == 0)
                    continue;
                if (head[0] == (byte)country && BitConverter.ToUInt16(head, 2) == clubId)
                    return new LiveMatch(memory, fixedAddresses, match, clockAddress, true, country, clubId);
                if (head[4] == (byte)country && BitConverter.ToUInt16(head, 6) == clubId)
                    return new LiveMatch(memory, fixedAddresses, match, clockAddress, false, country, clubId);
            }
            return null;
        }

        /// <summary>The current minute.</summary>
        public short Minute => ReadInt16(this.memory.ReadBytesAtAddress(this.clock + 8, 2));
        /// <summary>Goals of the home team.</summary>
        public short HomeGoals => ReadInt16(this.memory.ReadBytesAtAddress(this.match + 0xE, 2));
        /// <summary>Goals of the away team.</summary>
        public short AwayGoals => ReadInt16(this.memory.ReadBytesAtAddress(this.match + 0x10, 2));
        /// <summary>Country and ID of the opponent.</summary>
        public KeyValuePair<PlayerEnums.Country, ushort> Opponent
        {
            get
            {
                byte[] head = this.memory.ReadBytesAtAddress(this.match, 8) ?? new byte[8];
                int at = this.IsHome ? 4 : 0;
                return new KeyValuePair<PlayerEnums.Country, ushort>((PlayerEnums.Country)head[at], BitConverter.ToUInt16(head, at + 2));
            }
        }

        /// <summary>Substitutions made so far (the game allows 3).</summary>
        public byte Substitutions
        {
            get => this.ReadByte(SubstitutionsOffset);
            set => this.memory.WriteBytesAtAddress(this.Team + SubstitutionsOffset, new[] { Math.Min(value, MaxSubstitutions) });
        }
        /// <summary>Attitude 0..6, see <see cref="AttitudeNames"/>.</summary>
        public byte Attitude { get => this.ReadByte(AttitudeOffset); set => this.WriteTactic(AttitudeOffset, value, AttitudeNames.Length); }
        /// <summary>Hardness 0..4, see <see cref="HardnessNames"/>.</summary>
        public byte Hardness { get => this.ReadByte(HardnessOffset); set => this.WriteTactic(HardnessOffset, value, HardnessNames.Length); }
        /// <summary>Effort 0..6, see <see cref="EffortNames"/>.</summary>
        public byte Effort { get => this.ReadByte(EffortOffset); set => this.WriteTactic(EffortOffset, value, EffortNames.Length); }

        private byte ReadByte(uint offset) => this.memory.ReadBytesAtAddress(this.Team + offset, 1)?[0] ?? 0;

        // A changed tactic also marks the team as one the computer must not change it for (bit 4
        // home / 8 away, FUN_00414c50), as the game's own dialog does.
        private void WriteTactic(uint offset, byte value, int count)
        {
            if (value >= count)
                return;
            this.memory.WriteBytesAtAddress(this.Team + offset, new[] { value });
            byte[] mask = this.memory.ReadBytesAtAddress(this.match + HumanMaskOffset, 2);
            if (mask != null)
                this.memory.WriteBytesAtAddress(this.match + HumanMaskOffset, BitConverter.GetBytes((ushort)(BitConverter.ToUInt16(mask, 0) | (this.IsHome ? 4 : 8))));
        }

        private List<uint> ClubCopies() => ClubRecords.WorkingCopies(this.memory, this.fixedAddresses, this.Country, this.ClubId);

        // Before kick-off (and between the engine's moves) the game holds no working copy of the club,
        // so line-up and bench then come from the record.
        private ushort[] ReadIds(uint offset, int count)
        {
            uint copy = this.ClubCopies().FirstOrDefault();
            uint? record = copy == 0 ? ClubRecords.RecordAddress(this.memory, this.fixedAddresses, this.Country, this.ClubId) : null;
            uint? field = copy != 0 ? copy + offset : record.HasValue ? ClubRecords.RecordField(record.Value, offset, count * 2) : null;
            byte[] ids = field.HasValue ? this.memory.ReadBytesAtAddress(field.Value, count * 2) : null;
            return Enumerable.Range(0, count).Select(i => ids != null ? BitConverter.ToUInt16(ids, i * 2) : (ushort)0).ToArray();
        }

        /// <summary>The starting eleven as it stands now (club +0x8FC; 0 for an empty place).</summary>
        public ushort[] LineUp => this.ReadIds(Settings.ClubLineUpOffset, 11);
        /// <summary>The bench (club +0x912; 0 for an empty place).</summary>
        public ushort[] Bench => this.ReadIds(Settings.ClubBenchOffset, BenchSize);

        // Player working copy +0x84 in a match: 1 played, 2 came on, 4 went off (minute in +0x85,
        // FUN_00415aa0/FUN_00415a60), 8 booked, 0x10/0x20 sent off (colours in the report, FUN_004f13b0).
        private const byte WentOff = 0x04;
        private const byte SentOff = 0x30;

        /// <summary>
        /// Players that may sit on the bench: in the squad, not in the line-up, and not sent off; of
        /// those who played only the ones substituted (the game's substitution doesn't check, and
        /// bringing one back on just marks him as come on again).
        /// </summary>
        public List<ushort> BenchCandidates(IEnumerable<ushort> squad)
        {
            HashSet<ushort> lineUp = new HashSet<ushort>(this.LineUp);
            Dictionary<ushort, uint> copies = ClubSquads.PlayerWorkingCopies(this.memory, this.fixedAddresses);
            return squad.Where(id => id != 0 && !lineUp.Contains(id) && this.MayComeOn(copies, id)).ToList();
        }

        private bool MayComeOn(Dictionary<ushort, uint> copies, ushort id)
        {
            byte flags = copies.TryGetValue(id, out uint copy) ? this.memory.ReadBytesAtAddress(copy + Settings.PlayerMatchFlagsOffset, 1)?[0] ?? 0 : (byte)0;
            return flags == 0 || ((flags & WentOff) != 0 && (flags & SentOff) == 0);
        }

        /// <summary>Writes the bench into every working copy of the club and its record.</summary>
        public void WriteBench(ushort[] bench)
        {
            byte[] data = new byte[BenchSize * 2];
            for (int i = 0; i < BenchSize && i < bench.Length; i++)
                Array.Copy(BitConverter.GetBytes(bench[i]), 0, data, i * 2, 2);
            foreach (uint copy in this.ClubCopies())
                this.memory.WriteBytesAtAddress(copy + Settings.ClubBenchOffset, data);
            uint? record = ClubRecords.RecordAddress(this.memory, this.fixedAddresses, this.Country, this.ClubId);
            uint? field = record.HasValue ? ClubRecords.RecordField(record.Value, Settings.ClubBenchOffset, data.Length) : null;
            if (field.HasValue)
                this.memory.WriteBytesAtAddress(field.Value, data);
            Logger.Debug($"LiveMatch: bench of {this.Country}/{this.ClubId} set to {string.Join(", ", bench)}.");
        }

        private static short ReadInt16(byte[] bytes) => bytes != null ? BitConverter.ToInt16(bytes, 0) : (short)0;
    }
}
