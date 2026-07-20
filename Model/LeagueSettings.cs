using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    // One of up to LeagueSettings.MaxAdditionalCountries additional countries whose clubs can be
    // traded on the stock market - not an Entity, computed on the fly by LeagueController.
    public class AdditionalCountry : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        // Null means the slot is unused - the game writes FF there, and Country has no "none" member.
        public PlayerEnums.Country? Country { get => this.country; set { this.country = value; this.OnPropertyChanged(nameof(this.Country)); } }
        private PlayerEnums.Country? country;
    }

    // Which countries' leagues/clubs are available for the currently open savegame - fixed when the
    // career was created. One MainCountry (the coach's home league) plus up to four AdditionalCountries
    // that clubs can be bought stock in. Global to the savegame, not per-trainer like Coach.
    public class LeagueSettings : Entity
    {
        public PlayerEnums.Country MainCountry { get => this.mainCountry; set { this.mainCountry = value; this.OnPropertyChanged(nameof(this.MainCountry)); } }
        private PlayerEnums.Country mainCountry;

        // Game caps additional-country selection at 4 slots.
        public const int MaxAdditionalCountries = 4;
        public AdditionalCountry[] AdditionalCountries { get; } = new AdditionalCountry[MaxAdditionalCountries] { new AdditionalCountry(), new AdditionalCountry(), new AdditionalCountry(), new AdditionalCountry() };

        public Addresses Addresses { get => this.addresses; set { this.addresses = value; this.OnPropertyChanged(nameof(this.Addresses)); } }
        private Addresses addresses;
    }
}
