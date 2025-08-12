using System.Collections.Generic;

namespace A2G_Trainer_XP.Model
{
    public class Club : Entity
    {
        #region Helper
        public bool IsBlockARoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockA); set => this.Multiplex(ClubEnums.Roof.BlockA, value, nameof(this.IsBlockARoof)); }
        #endregion
        public string ClubName
        {
            get => this.clubName; set
            {
                if (Tools.LimitedStringEquals(value, this.clubName, 19))
                {
                    this.clubName = value != null && value.Length > 19 ? value.Substring(0, 19) : value; this.OnPropertyChanged(nameof(this.ClubName));
                }
            }
        }
        private string clubName = string.Empty;

        #region Stadium
        public string StadiumName
        {
            get => this.stadiumName; set
            {
                if (Tools.LimitedStringEquals(value, this.stadiumName, 28))
                {
                    this.stadiumName = value != null && value.Length > 28 ? value.Substring(0, 28) : value; this.OnPropertyChanged(nameof(this.StadiumName));
                }
            }
        }
        private string stadiumName = string.Empty;
        public ClubEnums.Roof Roof { get => this.roof; set => this.Setter(value); }
        private ClubEnums.Roof roof = ClubEnums.Roof.None;
        public ushort BlockAStandings { get => this.blockAStandings; set { this.blockAStandings = value; this.OnPropertyChanged(nameof(this.BlockAStandings)); } }
        private ushort blockAStandings = 0;
        public ushort BlockASeats { get => this.blockASeats; set { this.blockASeats = value; this.OnPropertyChanged(nameof(this.BlockASeats)); } }
        private ushort blockASeats = 0;
        #endregion

        #region PlayerCount
        public ushort PlayerCount
        {
            get => playerCount;
            set
            {
                if (value != playerCount)
                {
                    playerCount = value;
                    this.OnPropertyChanged(nameof(PlayerCount));
                }
            }
        }
        private ushort playerCount = 0;
        #endregion

        #region Setter
        private void Setter(ClubEnums.Roof roof, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.roof = roof;
                this.OnPropertyChanged(nameof(this.Roof));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        #endregion

        #region Modifier
        private void Multiplex(ClubEnums.Roof roof, bool enabled, string helper)
        {
            if (enabled)
            {
                this.roof |= roof;
            }
            else this.roof &= ~roof;
            this.OnPropertyChanged(nameof(this.Roof));
            if (helper != null) this.OnPropertyChanged(helper);
        }
        #endregion
    }
}
