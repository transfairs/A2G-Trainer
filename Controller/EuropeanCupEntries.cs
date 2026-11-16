using A2G_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Champion and cup winner of the European countries without a league table. The game draws them
    /// at every season start (FUN_004200e0, weighted by team index only) and right after puts them
    /// into the pots of the European cups (FUN_0041a310), drawn at half-week 0x0C. Writing a title
    /// before that start does nothing, it's drawn anew; in between, swapping two teams of the country
    /// everywhere the qualification keeps them gives the other team the place - see
    /// docs/savegame-format.md, "Meister der Länder ohne Ligatabelle".
    /// </summary>
    internal static class EuropeanCupEntries
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        /// <summary>A title a country's team can hold.</summary>
        internal enum Title
        {
            /// <summary>National champion (#43): Champions League or UEFA cup.</summary>
            Champion,
            /// <summary>Cup winner (#44): Cup Winners' Cup.</summary>
            CupWinner,
        }

        /// <summary>One team of a European country without league table.</summary>
        internal sealed class Team
        {
            /// <summary>Index within its country, as the pots and titles keep it.</summary>
            public short Index { get; set; }
            /// <summary>Club name.</summary>
            public string Name { get; set; } = "";
            /// <inheritdoc/>
            public override string ToString() => this.Name;
        }

        /// <summary>
        /// The countries without league table and their teams, by country; empty without fixed
        /// addresses. <paramref name="clubRecords"/> replaces the address of the club records, which
        /// lie beyond the module the tests fake.
        /// </summary>
        internal static Dictionary<PlayerEnums.Country, List<Team>> Countries(ProcessMemory memory, FixedAddresses fixedAddresses, uint? clubRecords = null)
        {
            Dictionary<PlayerEnums.Country, List<Team>> countries = new Dictionary<PlayerEnums.Country, List<Team>>();
            if (fixedAddresses == null)
                return countries;
            int countryCount = Enum.GetValues(typeof(PlayerEnums.Country)).Length;
            byte[] counts = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.EuropeanClubCountOffset), countryCount);
            byte[] firsts = memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.EuropeanFirstClubOffset), countryCount);
            if (counts == null || firsts == null)
                return countries;
            uint records = clubRecords ?? fixedAddresses.Runtime(memory, Settings.EuropeanClubBaseOffset);
            for (int country = 0; country < countryCount; country++)
            {
                if (counts[country] == 0 || firsts[country] == 0)
                    continue;
                List<Team> teams = new List<Team>();
                for (short team = 0; team < counts[country]; team++)
                {
                    uint record = records + (uint)(firsts[country] + team) * Settings.EuropeanClubStride;
                    byte[] name = memory.ReadBytesAtAddress(record, 0x20);
                    byte[] owner = memory.ReadBytesAtAddress(record + Settings.EuropeanClubCountryOffset, 1);
                    if (name == null || owner == null || owner[0] != country)
                        continue;
                    teams.Add(new Team { Index = team, Name = Latin1.GetString(name).Split('\0')[0] });
                }
                if (teams.Count > 0)
                    countries[(PlayerEnums.Country)country] = teams;
            }
            return countries;
        }

        private static uint TitleOffset(Title title) => title == Title.Champion ? Settings.CupChampionOffset : Settings.CupWinnerOffset;

        /// <summary>The team index holding <paramref name="title"/> in <paramref name="country"/>, or null if unreadable.</summary>
        internal static short? Holder(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, Title title)
        {
            byte[] value = fixedAddresses == null ? null : memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, TitleOffset(title)) + (uint)country, 1);
            return value?[0];
        }

        /// <summary>Whether a swap reaches the draws now: in the first season too, after the season start and before half-week 0x0C.</summary>
        internal static bool CanSwap(ProcessMemory memory, FixedAddresses fixedAddresses) =>
            LeagueTables.Phase(memory, fixedAddresses) == LeagueTables.SeasonPhase.BeforeCupDraw;

        /// <summary>
        /// Gives <paramref name="team"/> the <paramref name="title"/> of <paramref name="country"/> by
        /// swapping it with the current holder in every pot, the UI cup pairings and the title tables
        /// (champion, cup winner, runner-up, further UEFA cup teams), so nobody appears twice.
        /// Returns the number of entries changed, or -1 outside the window before the draws.
        /// </summary>
        internal static int Assign(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, Title title, short team)
        {
            if (!CanSwap(memory, fixedAddresses))
                return -1;
            short? holder = Holder(memory, fixedAddresses, country, title);
            if (!holder.HasValue)
                return -1;
            if (holder.Value == team)
                return 0;
            int changed = Swap(memory, fixedAddresses, country, holder.Value, team);
            Logger.Info($"Europapokal: {title} von {country} von Team {holder.Value} an Team {team} ({changed} Einträge getauscht).");
            return changed;
        }

        /// <summary>Swaps the two teams of <paramref name="country"/> everywhere the European cup qualification keeps them; returns the entries changed.</summary>
        internal static int Swap(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, short a, short b)
        {
            int changed = 0;
            foreach (KeyValuePair<uint, int> list in Settings.CupPotLists)
                changed += SwapEntries(memory, fixedAddresses.Runtime(memory, list.Key), list.Value, 4, new[] { 0 }, country, a, b);
            changed += SwapEntries(memory, fixedAddresses.Runtime(memory, Settings.UiCupPairingsOffset), Settings.UiCupPairings, (int)Settings.CupPairingStride, new[] { 0, 4 }, country, a, b);

            // The finalist (#45) isn't written for these countries, so it stays as it is.
            foreach (uint offset in new[] { Settings.CupChampionOffset, Settings.CupWinnerOffset, Settings.CupRunnerUpOffset })
                changed += SwapBytes(memory, fixedAddresses.Runtime(memory, offset) + (uint)country, 1, a, b);
            // The further teams are the ones left after champion, cup winner and runner-up; the
            // slots after them are filler and stay.
            byte[] teams = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.EuropeanClubCountOffset) + (uint)country, 1);
            int further = teams == null ? 0 : Math.Min(4, Math.Max(0, teams[0] - 3));
            changed += SwapBytes(memory, fixedAddresses.Runtime(memory, Settings.CupFurtherTeamsOffset) + (uint)country * 4, further, a, b);
            return changed;
        }

        // Entries of byte country, byte, Int16 team at the given places of each element.
        private static int SwapEntries(ProcessMemory memory, uint address, int count, int stride, int[] places, PlayerEnums.Country country, short a, short b)
        {
            byte[] block = memory.ReadBytesAtAddress(address, count * stride);
            if (block == null)
                return 0;
            int changed = 0;
            for (int element = 0; element < count; element++)
            {
                foreach (int place in places)
                {
                    int at = element * stride + place;
                    if (block[at] != (byte)country)
                        continue;
                    short team = BitConverter.ToInt16(block, at + 2);
                    short other = team == a ? b : team == b ? a : team;
                    if (other == team)
                        continue;
                    Array.Copy(BitConverter.GetBytes(other), 0, block, at + 2, 2);
                    changed++;
                }
            }
            if (changed > 0)
                memory.WriteBytesAtAddress(address, block);
            return changed;
        }

        // Team index bytes.
        private static int SwapBytes(ProcessMemory memory, uint address, int count, short a, short b)
        {
            byte[] bytes = count > 0 ? memory.ReadBytesAtAddress(address, count) : null;
            if (bytes == null)
                return 0;
            int changed = 0;
            for (int i = 0; i < count; i++)
            {
                byte other = bytes[i] == a ? (byte)b : bytes[i] == b ? (byte)a : bytes[i];
                if (other == bytes[i])
                    continue;
                bytes[i] = other;
                changed++;
            }
            if (changed > 0)
                memory.WriteBytesAtAddress(address, bytes);
            return changed;
        }
    }
}
