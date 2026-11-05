using System.ComponentModel;

namespace A2G_Trainer.Model
{
    /// <summary>
    /// A manager's champion bet ("Meistertipp") for one league country: the bet block in the saved
    /// Mitspieler record (Settings.CoachRecordBetOffset), +0x18 stake, +0x1C club, +0x1E odds. At the
    /// season end the game pays stake * odds / 10 if the club leads the 1st league, then clears the stake.
    /// </summary>
    public class ChampionTip : INotifyPropertyChanged
    {
        /// <summary>Raised whenever one of the bet's values changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>The league country the bet is for.</summary>
        public PlayerEnums.Country Country { get; set; }
        /// <summary>Stake ("Einsatz"); 0 means no bet. The game took it from the wealth when the bet was placed.</summary>
        public int Stake { get => this.stake; set { this.stake = value; this.OnPropertyChanged(nameof(this.Stake)); } }
        private int stake;
        /// <summary>ID of the tipped club within <see cref="Country"/>.</summary>
        public ushort ClubId { get => this.clubId; set { this.clubId = value; this.OnPropertyChanged(nameof(this.ClubId)); } }
        private ushort clubId;
        /// <summary>Odds as the payout per 10 of stake ("Quote"); the payout only uses this stored value.</summary>
        public short Odds { get => this.odds; set { this.odds = value; this.OnPropertyChanged(nameof(this.Odds)); } }
        private short odds;
    }
}
