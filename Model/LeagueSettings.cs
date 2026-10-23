using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// One league country other than the main country, as a candidate bonus country (Bonusland):
    /// whether it is active in the savegame, and whether the Trainer may switch it on or off.
    /// </summary>
    public class BonusCountry : INotifyPropertyChanged
    {
        /// <summary>Raised whenever <see cref="IsActive"/> changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>Creates the entry for one country as read from memory.</summary>
        public BonusCountry(PlayerEnums.Country country, bool wasActive, short leagueSystem, string toggleBlockedReason)
        {
            this.Country = country;
            this.WasActive = wasActive;
            this.isActive = wasActive;
            this.LeagueSystem = leagueSystem;
            this.ToggleBlockedReason = toggleBlockedReason;
        }

        /// <summary>The country itself.</summary>
        public PlayerEnums.Country Country { get; }

        /// <summary>Whether the country was a bonus country when the settings were read.</summary>
        public bool WasActive { get; }

        /// <summary>Whether the country is to be a bonus country; written by LeagueController.Save.</summary>
        public bool IsActive { get => this.isActive; set { this.isActive = value; this.OnPropertyChanged(nameof(this.IsActive)); } }
        private bool isActive;

        /// <summary>
        /// The country's league system, which the game keeps next to each bonus country: as stored
        /// for an active country, as looked up for an inactive one (-1 if that wasn't possible).
        /// </summary>
        public short LeagueSystem { get; }

        /// <summary>Why the country can't be switched on/off right now, or null if it can.</summary>
        public string ToggleBlockedReason { get; }

        /// <summary>Whether the Trainer may change <see cref="IsActive"/>.</summary>
        public bool CanToggle => this.ToggleBlockedReason == null;
    }

    /// <summary>
    /// Savegame-wide league/country configuration, shared by every trainer slot in the savegame.
    /// </summary>
    public class LeagueSettings : Entity
    {
        /// <summary>The countries with a league system of their own - the only possible main and bonus countries (table 0x6C1C38).</summary>
        public static readonly PlayerEnums.Country[] LeagueCountries = { PlayerEnums.Country.Deutschland, PlayerEnums.Country.England, PlayerEnums.Country.Frankreich, PlayerEnums.Country.Italien, PlayerEnums.Country.Spanien };

        /// <summary>Number of bonus-country slots the savegame has.</summary>
        public const int MaxBonusCountries = 4;

        /// <summary>The primary country the league is played in. Shown only - changing it mid-game isn't supported.</summary>
        public PlayerEnums.Country MainCountry { get => this.mainCountry; set { this.mainCountry = value; this.OnPropertyChanged(nameof(this.MainCountry)); } }
        private PlayerEnums.Country mainCountry;

        /// <summary>
        /// Every league country except the main country: the active bonus countries first, in the
        /// game's slot order, then the inactive ones.
        /// </summary>
        public List<BonusCountry> BonusCountries { get; } = new List<BonusCountry>();

        /// <summary>The countries currently marked active, in <see cref="BonusCountries"/> order.</summary>
        public IEnumerable<PlayerEnums.Country> ActiveBonusCountries => this.BonusCountries.Where(c => c.IsActive).Select(c => c.Country);

        /// <summary>Resolved memory addresses for this entity's fields.</summary>
        public Addresses Addresses { get => this.addresses; set { this.addresses = value; this.OnPropertyChanged(nameof(this.Addresses)); } }
        private Addresses addresses;
    }
}
