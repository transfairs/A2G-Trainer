using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    // One of the coach's (up to 6) club stock holdings - not an Entity, since it has no
    // address/offset of its own: CoachController computes each slot's addresses on the fly.
    public class Stock : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public PlayerEnums.Country Country { get => this.country; set { this.country = value; this.OnPropertyChanged(nameof(this.Country)); } }
        private PlayerEnums.Country country;
        public byte ClubId { get => this.clubId; set { this.clubId = value; this.OnPropertyChanged(nameof(this.ClubId)); } }
        private byte clubId;
        public ushort Shares { get => this.shares; set { this.shares = value; this.OnPropertyChanged(nameof(this.Shares)); } }
        private ushort shares;
        public ushort Price { get => this.price; set { this.price = value; this.OnPropertyChanged(nameof(this.Price)); } }
        private ushort price;
    }
}
