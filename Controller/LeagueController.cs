using System;
using System.Collections.Generic;
using System.Linq;
using A2G_Trainer_XP.Model;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Reads/writes the savegame-wide country selection (LeagueSettings). Unlike CoachController
    /// this isn't per-trainer-slot - every trainer in the savegame shares the same league configuration.
    /// </summary>
    /// <remarks>
    /// The game keeps three things per bonus country (FUN_006441e0): the count, the country list
    /// and, parallel to it, each country's league system, which the matchday and table code
    /// (FUN_00597ba0, FUN_0063aeb0, FUN_006414f0) uses instead of looking it up again. All three
    /// are written together. A country's clubs and leagues are only loaded at the start of a new
    /// game, and only for the countries chosen then (FUN_005b04d0 loads ttra/Prom*.sav per active
    /// country). So switching a country off is safe - its league data stays where it is, since
    /// every league system has a fixed place - but switching one on is only allowed while its top
    /// league still has clubs in memory, i.e. it was chosen at the start (and switched off later).
    /// </remarks>
    public class LeagueController : EntityController<LeagueSettings>
    {
        // Distance between one bonus-country slot and the next, in both parallel lists: each slot
        // is a little-endian Int16 (country: FFFF = unused; league system: 0 when unused).
        private const int SlotStride = 0x2;
        private static readonly byte[] EmptyCountrySlot = { 0xFF, 0xFF };

        internal LeagueSettings League { get { return this.league; } set { this.league = value; } }
        private LeagueSettings league;

        /// <summary>Attaches to the game process and loads the savegame-wide league/country settings.</summary>
        public LeagueController(ProcessMemory memory, bool isGog, PlayerEnums.AddressType type) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.CountrySelectionAddress;
            this.UpdateBaseAddress(type);
            this.League = this.GetEntity("0", type);
        }

        /// <summary>Reads the main country, the active bonus countries and whether each league country may be switched on/off.</summary>
        internal override LeagueSettings GetEntity(string offset, PlayerEnums.AddressType type)
        {
            LeagueSettings league = new LeagueSettings()
            {
                Offset = offset,
                Addresses = AddressPresets.LEAGUE_SETTINGS
            };

            if (this.memory.mProc.MainModule != null)
            {
                league.MainCountry = (PlayerEnums.Country)this.memory.ReadByte(GetAddress(this.memory, league, league.Addresses[LeagueEnums.AddressKey.MAIN_COUNTRY]));

                List<Coach> trainers = this.ReadTrainers(type);
                int count = Math.Max(0, Math.Min(LeagueSettings.MaxBonusCountries, (int)(short)this.memory.ReadUInt16(GetAddress(this.memory, league, league.Addresses[LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT]))));
                for (int slot = 0; slot < count; slot++)
                {
                    PlayerEnums.Country country = (PlayerEnums.Country)(byte)this.memory.ReadUInt16(this.GetSlotAddress(league, LeagueEnums.AddressKey.ADDITIONAL_COUNTRY, slot));
                    short system = (short)this.memory.ReadUInt16(this.GetSlotAddress(league, LeagueEnums.AddressKey.BONUS_LEAGUE_SYSTEM, slot));
                    league.BonusCountries.Add(new BonusCountry(country, true, system, DeactivationBlockedReason(country, trainers)));
                }

                foreach (PlayerEnums.Country country in LeagueSettings.LeagueCountries)
                {
                    if (country == league.MainCountry || league.BonusCountries.Any(c => c.Country == country))
                        continue;
                    short? system = LookupLeagueSystem(this.memory, country);
                    league.BonusCountries.Add(new BonusCountry(country, false, system ?? -1, ActivationBlockedReason(this.memory, system)));
                }
            }
            return league;
        }

        /// <summary>
        /// Writes the bonus-country selection: count, country list and league-system list. A switch-off
        /// that became blocked since the read (e.g. a next-season change to that country saved in
        /// the meantime) is skipped. Returns the countries whose change was skipped.
        /// </summary>
        public List<PlayerEnums.Country> Save()
        {
            List<PlayerEnums.Country> skipped = new List<PlayerEnums.Country>();
            if (this.League.BonusCountries.All(c => c.IsActive == c.WasActive))
                return skipped;

            List<Coach> trainers = this.ReadTrainers(this.Type);
            List<BonusCountry> active = new List<BonusCountry>();
            foreach (BonusCountry entry in this.League.BonusCountries)
            {
                string reason = entry.IsActive == entry.WasActive ? null
                    : entry.WasActive ? DeactivationBlockedReason(entry.Country, trainers) ?? entry.ToggleBlockedReason
                    : entry.ToggleBlockedReason;
                if (reason != null)
                {
                    skipped.Add(entry.Country);
                    Logger.Warn($"Bonus country {entry.Country} not switched {(entry.IsActive ? "on" : "off")}: {reason}");
                }

                bool keep = reason != null ? entry.WasActive : entry.IsActive;
                if (keep)
                    active.Add(entry);
            }

            int oldCount = this.League.BonusCountries.Count(c => c.WasActive);
            if (active.Count == oldCount && active.All(c => c.WasActive))
                return skipped;

            Logger.Debug($"Saving bonus countries: {string.Join(", ", active.Select(c => c.Country))}");

            // The game reads the count first and then that many slots - so a shrinking list gets its
            // new count before the slots are rewritten, a growing one only after they're in place.
            if (active.Count < oldCount)
                this.WriteCount(active.Count);

            for (int slot = 0; slot < LeagueSettings.MaxBonusCountries; slot++)
            {
                BonusCountry entry = slot < active.Count ? active[slot] : null;
                this.memory.WriteBytes(this.GetSlotAddress(this.League, LeagueEnums.AddressKey.ADDITIONAL_COUNTRY, slot),
                    entry != null ? BitConverter.GetBytes((short)entry.Country) : EmptyCountrySlot);
                this.memory.WriteBytes(this.GetSlotAddress(this.League, LeagueEnums.AddressKey.BONUS_LEAGUE_SYSTEM, slot),
                    BitConverter.GetBytes(entry != null ? entry.LeagueSystem : (short)0));
            }

            if (active.Count >= oldCount)
                this.WriteCount(active.Count);

            return skipped;
        }

        private void WriteCount(int count) =>
            this.memory.WriteBytes(GetAddress(this.memory, this.League, this.League.Addresses[LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT]), BitConverter.GetBytes((short)count));

        // Each slot is the slot-0 offset (from AddressPresets.LEAGUE_SETTINGS) plus slotIndex * SlotStride.
        private string GetSlotAddress(LeagueSettings league, LeagueEnums.AddressKey key, int slotIndex)
        {
            string slotOffset = Tools.SumHex(new[] { league.Addresses[key], (slotIndex * SlotStride).ToString("X") });
            return GetAddress(this.memory, league, slotOffset);
        }

        private List<Coach> ReadTrainers(PlayerEnums.AddressType type) =>
            CoachController.GetActiveTrainers(this.memory, this.isGog, type).Select(t => t.Value).ToList();

        // A country can't be switched off while a human trainer coaches there or has a club there
        // lined up for next season - the game would no longer process that country.
        internal static string DeactivationBlockedReason(PlayerEnums.Country country, IEnumerable<Coach> trainers)
        {
            foreach (Coach trainer in trainers)
            {
                string name = $"{trainer.Firstname} {trainer.Lastname}".Trim();
                if (name.Length == 0)
                    name = "Ein Trainer";
                if (trainer.CurrentCountry == country)
                    return $"{name} trainiert dort einen Verein.";
                if (trainer.CountryNextSeason == country)
                    return $"{name} wechselt zur nächsten Saison dorthin.";
            }
            return null;
        }

        // A country can only be switched on while its top league still has clubs in memory.
        internal static string ActivationBlockedReason(ProcessMemory memory, short? leagueSystem)
        {
            if (FixedAddresses.For(memory) == null)
                return "Zuschalten ist für diese Spielversion nicht möglich.";
            if (!leagueSystem.HasValue)
                return "Ligadaten nicht lesbar.";
            bool? hasClubs = HasClubsInTopLeague(memory, leagueSystem.Value);
            if (hasClubs == null)
                return "Ligadaten nicht lesbar.";
            return hasClubs.Value ? null : "Keine Vereine im Speicher – das Land wurde zu Spielbeginn nicht gewählt.";
        }

        // The fixed country -> league system table (FUN_006441e0 reads it for every bonus country).
        internal static short? LookupLeagueSystem(ProcessMemory memory, PlayerEnums.Country country)
        {
            FixedAddresses fixedAddresses = FixedAddresses.For(memory);
            if (fixedAddresses == null)
                return null;
            byte[] system = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.LeagueSystemByCountryOffset) + (uint)country, 1);
            return system != null ? system[0] : (short?)null;
        }

        internal static bool? HasClubsInTopLeague(ProcessMemory memory, short leagueSystem)
        {
            FixedAddresses fixedAddresses = FixedAddresses.For(memory);
            if (fixedAddresses == null)
                return null;
            byte[] range = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.LeagueRangeTableOffset) + (uint)leagueSystem * 2, 2);
            if (range == null)
                return null;
            uint topLeague = fixedAddresses.Runtime(memory, Settings.LeagueTableOffset) + (uint)BitConverter.ToInt16(range, 0) * Settings.LeagueStride;
            byte[] count = memory.ReadBytesAtAddress(topLeague, 2);
            if (count == null)
                return null;
            return BitConverter.ToInt16(count, 0) > 0;
        }
    }
}
