using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// The national teams' squads (Nationalkader): one record per country in club-record layout
    /// (see Settings.NationalTeamRecordTableOffset) with the squad as player IDs, i.e. indices into the
    /// player records. The game fetches a national team into the club cache only briefly (club ID
    /// 200) and then writes the working copy's squad back over the record, so the Trainer only
    /// changes a squad while no working copy of it exists.
    /// </summary>
    internal static class NationalSquads
    {
        /// <summary>One player as listed for a squad.</summary>
        internal sealed class Member
        {
            /// <summary>Player ID (index into the player records).</summary>
            public ushort Id { get; set; }
            /// <summary>First and last name.</summary>
            public string Name { get; set; }
            /// <summary>Main position; the game keeps the squad sorted by it.</summary>
            public PlayerEnums.Position Position { get; set; }
            /// <summary>Strength ("Stärke").</summary>
            public byte Level { get; set; }
            /// <summary>Age in years, or null if the current year isn't known.</summary>
            public int? Age { get; set; }
            /// <summary>The text a list shows (see <see cref="ToString"/>).</summary>
            public string Text => this.ToString();

            /// <inheritdoc />
            public override string ToString() =>
                $"{this.Name} ({this.Position}, {this.Level}" + (this.Age.HasValue ? $", {this.Age} J.)" : ")");
        }

        /// <summary>What <see cref="Write"/> did.</summary>
        internal enum WriteResult
        {
            /// <summary>The squad was written.</summary>
            Written,
            /// <summary>The game has the national team fetched right now; nothing was written.</summary>
            Fetched,
            /// <summary>More players than a squad holds; nothing was written.</summary>
            TooMany,
            /// <summary>The records aren't known for this build; nothing was written.</summary>
            NotSupported
        }

        private const int ScanChunkRecords = 400;

        private static bool IsCountry(PlayerEnums.Country country) =>
            country != PlayerEnums.Country.Sonstige && Enum.IsDefined(typeof(PlayerEnums.Country), country);

        private static uint RecordAddress(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country) =>
            fixedAddresses.Runtime(memory, Settings.NationalTeamRecordTableOffset) + (uint)country * Settings.ClubRecordStride;

        /// <summary>Player IDs of the country's squad, in the game's order; empty if it can't be read.</summary>
        internal static List<ushort> SquadIds(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country)
        {
            List<ushort> ids = new List<ushort>();
            if (fixedAddresses == null || !IsCountry(country))
                return ids;

            uint record = RecordAddress(memory, fixedAddresses, country);
            byte[] count = memory.ReadBytesAtAddress(record + Settings.ClubRecordSquadCountOffset, 2);
            if (count == null)
                return ids;
            int n = Math.Max(0, Math.Min((int)BitConverter.ToInt16(count, 0), Settings.NationalTeamSquadCapacity));
            byte[] list = n > 0 ? memory.ReadBytesAtAddress(record + Settings.ClubRecordSquadOffset, n * 2) : new byte[0];
            if (list == null)
                return ids;
            for (int i = 0; i < n; i++)
                ids.Add(BitConverter.ToUInt16(list, i * 2));
            return ids;
        }

        /// <summary>Whether the game holds a working copy of the country's national team right now.</summary>
        internal static bool IsFetched(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country) =>
            ClubRecords.WorkingCopies(memory, fixedAddresses, country, Settings.NationalTeamClubId).Count > 0;

        /// <summary>
        /// Writes the squad, sorted by position as the game keeps it. Refuses while the game has the
        /// national team fetched, since it would write its own squad back over the record.
        /// </summary>
        internal static WriteResult Write(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, IList<Member> squad)
        {
            if (fixedAddresses == null || !IsCountry(country))
                return WriteResult.NotSupported;
            if (squad.Count > Settings.NationalTeamSquadCapacity)
                return WriteResult.TooMany;
            if (IsFetched(memory, fixedAddresses, country))
                return WriteResult.Fetched;

            byte[] list = new byte[Settings.NationalTeamSquadCapacity * 2];
            int i = 0;
            foreach (Member member in squad.OrderBy(m => (byte)m.Position))
                BitConverter.GetBytes(member.Id).CopyTo(list, 2 * i++);

            uint record = RecordAddress(memory, fixedAddresses, country);
            memory.WriteBytesAtAddress(record + Settings.ClubRecordSquadOffset, list);
            memory.WriteBytesAtAddress(record + Settings.ClubRecordSquadCountOffset, BitConverter.GetBytes((short)squad.Count));
            Logger.Debug($"National squad of {country}: wrote {squad.Count} players.");
            return WriteResult.Written;
        }

        /// <summary>
        /// Every player of the country's nationality, strongest first - the ones that can play for
        /// its national team (the game counts a player as "Nationalspieler" only in the squad of his
        /// own nationality, FUN_00585740).
        /// </summary>
        internal static List<Member> PlayersOfNationality(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country)
        {
            List<Member> players = new List<Member>();
            if (fixedAddresses == null || !IsCountry(country))
                return players;

            uint table = fixedAddresses.Runtime(memory, Settings.PlayerRecordTableOffset);
            int? year = CurrentYear(memory, fixedAddresses);
            NamePoolResolver namePool = new NamePoolResolver(memory);
            // In chunks small enough to stay off the large object heap.
            for (int first = 0; first < Settings.PlayerRecordCapacity; first += ScanChunkRecords)
            {
                int count = Math.Min(ScanChunkRecords, Settings.PlayerRecordCapacity - first);
                byte[] chunk = memory.ReadBytesAtAddress(table + (uint)first * Settings.PlayerRecordStride, count * (int)Settings.PlayerRecordStride);
                if (chunk == null)
                    continue;
                for (int i = 0; i < count; i++)
                {
                    int at = i * (int)Settings.PlayerRecordStride;
                    bool used = BitConverter.ToUInt16(chunk, at) != 0 || BitConverter.ToUInt16(chunk, at + 2) != 0;
                    if (used && (chunk[at + Settings.PlayerRecordNationalityOffset] & 0x7F) == (byte)country)
                        players.Add(ToMember(memory, namePool, (ushort)(first + i), chunk, at, year));
                }
            }
            return players.OrderByDescending(p => p.Level).ThenBy(p => p.Name).ToList();
        }

        /// <summary>The players with the given IDs, in that order; IDs outside the records are left out.</summary>
        internal static List<Member> Members(ProcessMemory memory, FixedAddresses fixedAddresses, IEnumerable<ushort> ids)
        {
            List<Member> members = new List<Member>();
            if (fixedAddresses == null)
                return members;

            int? year = CurrentYear(memory, fixedAddresses);
            NamePoolResolver namePool = new NamePoolResolver(memory);
            uint table = fixedAddresses.Runtime(memory, Settings.PlayerRecordTableOffset);
            foreach (ushort id in ids)
            {
                byte[] record = id < Settings.PlayerRecordCapacity ? memory.ReadBytesAtAddress(table + id * Settings.PlayerRecordStride, (int)Settings.PlayerRecordStride) : null;
                if (record != null)
                    members.Add(ToMember(memory, namePool, id, record, 0, year));
            }
            return members;
        }

        private static Member ToMember(ProcessMemory memory, NamePoolResolver namePool, ushort id, byte[] records, int at, int? year)
        {
            Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
            uint? first = namePool.ResolveFirstnameAddress(id);
            uint? last = namePool.ResolveLastnameAddress(id);
            string name = $"{(first.HasValue ? memory.ReadStringAtAddress(first.Value, 32, latin1) : "")} {(last.HasValue ? memory.ReadStringAtAddress(last.Value, 32, latin1) : "")}".Trim();
            return new Member
            {
                Id = id,
                Name = name.Length > 0 ? name : $"Spieler {id}",
                Position = (PlayerEnums.Position)records[at + Settings.PlayerRecordPositionOffset],
                Level = records[at + Settings.PlayerRecordLevelOffset],
                Age = year.HasValue ? year.Value - BitConverter.ToUInt16(records, at + (int)Settings.PlayerRecordAgeOffset) : (int?)null
            };
        }

        private static int? CurrentYear(ProcessMemory memory, FixedAddresses fixedAddresses)
        {
            byte[] year = memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.AgeReferenceYearOffset), 2);
            return year != null ? BitConverter.ToUInt16(year, 0) : (int?)null;
        }
    }
}
