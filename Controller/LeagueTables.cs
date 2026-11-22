using A2G_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// The game's 13 leagues as tables: read from the league structures (Settings.LeagueTableOffset),
    /// results written back for a chosen order, and the licence byte of any league club. The game
    /// computes every table from the result grid, so the grid is all there is to change.
    /// </summary>
    internal static class LeagueTables
    {
        /// <summary>League names by league system and level, as the game's texts 1019-1031 have them.</summary>
        private static readonly string[][] Names =
        {
            new[] { "Bundesliga", "2. Liga", "Regionalliga Nord", "Regionalliga Nordost", "Regionalliga Süd", "Regionalliga West/Südwest" },
            new[] { "Premier League", "Division One", "Division Two" },
            new[] { "Division 1", "Division 2" },
            new[] { "Serie A" },
            new[] { "Primera Division" },
        };

        /// <summary>Where the season stands, from the half-week counter (Settings.HalfWeekOffset).</summary>
        internal enum SeasonPhase
        {
            /// <summary>Not readable.</summary>
            Unknown,
            /// <summary>Between the season start and the European cup draws: the qualified teams are still in the pots.</summary>
            BeforeCupDraw,
            /// <summary>The draws are done, the season runs.</summary>
            Running,
            /// <summary>The season end is done; the next season's start rolls and draws everything anew.</summary>
            SeasonOver,
        }

        /// <summary>The current season phase of the running game.</summary>
        internal static SeasonPhase Phase(ProcessMemory memory, FixedAddresses fixedAddresses)
        {
            byte[] bytes = fixedAddresses == null ? null : memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.HalfWeekOffset), 2);
            return bytes == null ? SeasonPhase.Unknown : PhaseOf(BitConverter.ToInt16(bytes, 0));
        }

        /// <summary>The season phase for a half-week counter value.</summary>
        internal static SeasonPhase PhaseOf(short halfWeek)
        {
            if (halfWeek < 0)
                return SeasonPhase.Unknown;
            if (halfWeek < Settings.HalfWeekCupDraw)
                return SeasonPhase.BeforeCupDraw;
            return halfWeek < Settings.HalfWeekSeasonEnd ? SeasonPhase.Running : SeasonPhase.SeasonOver;
        }

        // Int16 at a runtime offset, or null.
        private static short? RuntimeShort(ProcessMemory memory, FixedAddresses fixedAddresses, uint offset)
        {
            byte[] bytes = memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, offset), 2);
            return bytes == null ? (short?)null : BitConverter.ToInt16(bytes, 0);
        }

        /// <summary>The main country and the active bonus countries of the running game.</summary>
        internal static PlayerEnums.Country Countries(ProcessMemory memory, FixedAddresses fixedAddresses, out List<PlayerEnums.Country> bonus)
        {
            short main = RuntimeShort(memory, fixedAddresses, Settings.MainCountryOffset) ?? 0;
            int count = Math.Max(0, Math.Min(LeagueSettings.MaxBonusCountries, (int)(RuntimeShort(memory, fixedAddresses, Settings.BonusCountryCountOffset) ?? 0)));
            byte[] list = count > 0 ? memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.BonusCountryListOffset), count * 2) : null;
            bonus = new List<PlayerEnums.Country>();
            for (int slot = 0; list != null && slot < count; slot++)
                bonus.Add((PlayerEnums.Country)(byte)BitConverter.ToInt16(list, slot * 2));
            return (PlayerEnums.Country)(byte)main;
        }

        /// <summary>How the game decides the season of a league of <paramref name="country"/> at <paramref name="level"/>.</summary>
        internal static LeagueMode ModeOf(PlayerEnums.Country country, int level, PlayerEnums.Country main, ICollection<PlayerEnums.Country> bonus)
        {
            if (country == main)
                return LeagueMode.Played;
            bool active = bonus.Contains(country);
            if (level > 0)
                return active ? LeagueMode.Simulated : LeagueMode.SimulatedInactive;
            return active ? LeagueMode.Played : LeagueMode.NoResults;
        }

        /// <summary>All 13 leagues of the running game, in the game's order; empty without fixed addresses.</summary>
        internal static List<LeagueTable> ReadAll(ProcessMemory memory, FixedAddresses fixedAddresses)
        {
            List<LeagueTable> leagues = new List<LeagueTable>();
            if (fixedAddresses == null)
                return leagues;
            PlayerEnums.Country main = Countries(memory, fixedAddresses, out List<PlayerEnums.Country> bonus);
            Dictionary<PlayerEnums.Country, List<ClubRecords.Entry>> clubs = new Dictionary<PlayerEnums.Country, List<ClubRecords.Entry>>();
            for (int system = 0; system < LeagueSettings.LeagueCountries.Length; system++)
            {
                PlayerEnums.Country country = LeagueSettings.LeagueCountries[system];
                byte[] range = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.LeagueRangeTableOffset) + (uint)system * 2, 4);
                if (range == null)
                    continue;
                int first = BitConverter.ToInt16(range, 0), end = BitConverter.ToInt16(range, 2);
                for (int index = first; index < end && index < Settings.LeagueCount; index++)
                {
                    LeagueTable table = Read(memory, fixedAddresses, index);
                    if (table == null || table.Country != country)
                        continue;
                    table.Level = index - first;
                    table.Name = system < Names.Length && table.Level < Names[system].Length ? Names[system][table.Level] : $"{PlayerEnums.GetDescription(country)} {table.Level + 1}";
                    table.Mode = ModeOf(country, table.Level, main, bonus);
                    if (!clubs.TryGetValue(country, out List<ClubRecords.Entry> entries))
                        clubs[country] = entries = ClubRecords.CountryClubs(memory, fixedAddresses, country);
                    table.ClubNames = table.ClubIds.Select(id => entries.FirstOrDefault(e => e.Id == id)?.Name ?? $"Verein {id}").ToArray();
                    table.Licences = table.ClubIds.Select(id => ReadLicence(memory, fixedAddresses, country, id) ?? 0).ToArray();
                    leagues.Add(table);
                }
            }
            return leagues;
        }

        /// <summary>The league structure at <paramref name="index"/>: country, matchdays, clubs and grid; null if unreadable.</summary>
        internal static LeagueTable Read(ProcessMemory memory, FixedAddresses fixedAddresses, int index)
        {
            uint address = LeagueAddress(memory, fixedAddresses, index);
            byte[] header = memory.ReadBytesAtAddress(address, (int)Settings.LeagueClubIdsOffset + Settings.LeagueMaxClubs * 2);
            byte[] grid = memory.ReadBytesAtAddress(address + Settings.LeagueGridOffset, LeagueTable.GridSize * LeagueTable.GridSize * 2);
            if (header == null || grid == null)
                return null;
            int count = BitConverter.ToInt16(header, 0);
            if (count < 0 || count > Settings.LeagueMaxClubs)
                return null;
            return new LeagueTable
            {
                Index = index,
                Country = (PlayerEnums.Country)(byte)BitConverter.ToInt16(header, (int)Settings.LeagueCountryOffset),
                Matchdays = BitConverter.ToInt16(header, (int)Settings.LeagueMatchdaysOffset),
                Played = BitConverter.ToInt16(header, (int)Settings.LeaguePlayedOffset),
                ClubIds = Enumerable.Range(0, count).Select(i => BitConverter.ToInt16(header, (int)Settings.LeagueClubIdsOffset + i * 2)).ToArray(),
                Grid = grid,
            };
        }

        private static uint LeagueAddress(ProcessMemory memory, FixedAddresses fixedAddresses, int index) =>
            fixedAddresses.Runtime(memory, Settings.LeagueTableOffset) + (uint)index * Settings.LeagueStride;

        /// <summary>
        /// Writes the results that put the league in <paramref name="order"/> (list positions, best
        /// first). Only for a rolled league of a bonus country: there the game reads the grid again at
        /// the season end, while a played league's matchdays would overwrite what isn't played yet and
        /// the season change of a country not chosen moves nobody.
        /// </summary>
        internal static bool WriteOrder(ProcessMemory memory, FixedAddresses fixedAddresses, LeagueTable table, IList<int> order)
        {
            if (fixedAddresses == null || table.Mode != LeagueMode.Simulated)
                return false;
            table.SetResults(order);
            memory.WriteBytesAtAddress(LeagueAddress(memory, fixedAddresses, table.Index) + Settings.LeagueGridOffset, table.Grid);
            Logger.Info($"Ligatabelle {table.Name}: Ergebnisse für die Reihenfolge {string.Join(",", order.Select(n => n.ToString()).ToArray())} geschrieben.");
            return true;
        }

        /// <summary>The club's licence byte: from a working copy in the game's cache if there is one (it may be newer), else from the record.</summary>
        internal static byte? ReadLicence(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, int clubId)
        {
            foreach (uint copy in ClubRecords.WorkingCopies(memory, fixedAddresses, country, clubId))
            {
                byte[] working = memory.ReadBytesAtAddress(copy + Settings.ClubLicenceOffset, 1);
                if (working != null)
                    return working[0];
            }
            uint? record = ClubRecords.RecordAddress(memory, fixedAddresses, country, clubId);
            byte[] stored = record.HasValue ? memory.ReadBytesAtAddress(ClubRecords.RecordField(record.Value, Settings.ClubLicenceOffset, 1).Value, 1) : null;
            return stored?[0];
        }

        /// <summary>Writes the club's licence byte to its record and every cached working copy; false if the club has no record.</summary>
        internal static bool WriteLicence(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, int clubId, byte licence)
        {
            uint? record = ClubRecords.RecordAddress(memory, fixedAddresses, country, clubId);
            if (!record.HasValue)
                return false;
            memory.WriteBytesAtAddress(ClubRecords.RecordField(record.Value, Settings.ClubLicenceOffset, 1).Value, new[] { licence });
            foreach (uint copy in ClubRecords.WorkingCopies(memory, fixedAddresses, country, clubId))
                memory.WriteBytesAtAddress(copy + Settings.ClubLicenceOffset, new[] { licence });
            return true;
        }
    }
}
