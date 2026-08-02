using A2G_Trainer_XP.Model;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Reads/writes the savegame-wide country selection (LeagueSettings). Unlike CoachController
    /// this isn't per-trainer-slot - every trainer in the savegame shares the same league configuration.
    /// </summary>
    public class LeagueController : EntityController<LeagueSettings>
    {
        // Distance between one bonus-country slot and the next (Bonusland 1 -> Bonusland 2 is
        // 426644 -> 426646). Each slot is really a little-endian UInt16: FF FF is the "unused"
        // sentinel, and a set slot holds the country ID in the low byte with the high byte at 00
        // (every Country value fits in a byte, so the high byte is always 0 when a slot is in use).
        private const int AdditionalCountrySlotStride = 0x2;
        private const byte EmptySlot = 0xFF;
        private const byte UnusedHighByte = 0x00;

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

        /// <summary>Reads the current main country and all additional-country slots from memory.</summary>
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

                for (int slot = 0; slot < LeagueSettings.MaxAdditionalCountries; slot++)
                {
                    byte value = (byte)this.memory.ReadByte(this.GetAdditionalCountryAddress(league, slot));
                    league.AdditionalCountries[slot].Country = value == EmptySlot ? (PlayerEnums.Country?)null : (PlayerEnums.Country)value;
                }
            }
            return league;
        }

        // Additional-country slots aren't separate dictionary entries - each one is the slot-0
        // offset (from AddressPresets.LEAGUE_SETTINGS) plus slotIndex * AdditionalCountrySlotStride.
        private string GetAdditionalCountryAddress(LeagueSettings league, int slotIndex)
        {
            string slotOffset = Tools.SumHex(new[] { league.Addresses[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], (slotIndex * AdditionalCountrySlotStride).ToString("X") });
            return GetAddress(this.memory, league, slotOffset);
        }

        /// <summary>Writes the current main country and all additional-country slots back to memory.</summary>
        public void Save()
        {
            Logger.Debug($"Saving league settings: MainCountry={this.League.MainCountry}");

            this.memory.WriteBytes(GetAddress(this.memory, this.League, this.League.Addresses[LeagueEnums.AddressKey.MAIN_COUNTRY]), new byte[] { (byte)this.League.MainCountry });

            for (int slot = 0; slot < LeagueSettings.MaxAdditionalCountries; slot++)
            {
                PlayerEnums.Country? country = this.League.AdditionalCountries[slot].Country;
                byte[] slotBytes = country.HasValue ? new byte[] { (byte)country.Value, UnusedHighByte } : new byte[] { EmptySlot, EmptySlot };
                this.memory.WriteBytes(this.GetAdditionalCountryAddress(this.League, slot), slotBytes);
            }
        }
    }
}
