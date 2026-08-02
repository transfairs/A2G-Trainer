using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// A single bonus-country slot (Bonusland). <see cref="Country"/> is null when the slot is unused.
    /// </summary>
    public class AdditionalCountry : INotifyPropertyChanged
    {
        /// <summary>Raised whenever <see cref="Country"/> changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>Country assigned to this slot, or null if the slot is unused.</summary>
        public PlayerEnums.Country? Country { get => this.country; set { this.country = value; this.OnPropertyChanged(nameof(this.Country)); } }
        private PlayerEnums.Country? country;
    }

    /// <summary>
    /// Savegame-wide league/country configuration, shared by every trainer slot in the savegame.
    /// </summary>
    public class LeagueSettings : Entity
    {
        /// <summary>The primary country the league is played in.</summary>
        public PlayerEnums.Country MainCountry { get => this.mainCountry; set { this.mainCountry = value; this.OnPropertyChanged(nameof(this.MainCountry)); } }
        private PlayerEnums.Country mainCountry;

        /// <summary>Number of additional-country (Bonusland) slots the savegame supports.</summary>
        public const int MaxAdditionalCountries = 4;
        /// <summary>The up-to-<see cref="MaxAdditionalCountries"/> bonus-country slots.</summary>
        public AdditionalCountry[] AdditionalCountries { get; } = new AdditionalCountry[MaxAdditionalCountries] { new AdditionalCountry(), new AdditionalCountry(), new AdditionalCountry(), new AdditionalCountry() };

        /// <summary>Resolved memory addresses for this entity's fields.</summary>
        public Addresses Addresses { get => this.addresses; set { this.addresses = value; this.OnPropertyChanged(nameof(this.Addresses)); } }
        private Addresses addresses;
    }
}
