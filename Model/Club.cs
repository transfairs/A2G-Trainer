using System.Collections.Generic;

namespace A2G_Trainer_XP.Model
{
    public class Club : Entity
    {
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
    }
}
