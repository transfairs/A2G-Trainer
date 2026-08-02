using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// A single stock-market holding (Aktie) for one of a coach's up-to-<see cref="Coach.MaxStocks"/> slots.
    /// </summary>
    public class Stock : INotifyPropertyChanged
    {
        /// <summary>Raised whenever one of this stock holding's properties changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>Country of the club whose stock this slot holds.</summary>
        public PlayerEnums.Country Country { get => this.country; set { this.country = value; this.OnPropertyChanged(nameof(this.Country)); } }
        private PlayerEnums.Country country;
        /// <summary>Club identifier the shares belong to.</summary>
        public byte ClubId { get => this.clubId; set { this.clubId = value; this.OnPropertyChanged(nameof(this.ClubId)); } }
        private byte clubId;
        /// <summary>Number of shares held.</summary>
        public ushort Shares { get => this.shares; set { this.shares = value; this.OnPropertyChanged(nameof(this.Shares)); } }
        private ushort shares;
        /// <summary>Current share price.</summary>
        public ushort Price { get => this.price; set { this.price = value; this.OnPropertyChanged(nameof(this.Price)); } }
        private ushort price;
    }
}
