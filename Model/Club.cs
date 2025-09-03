using System;
using System.Collections.Generic;

namespace A2G_Trainer_XP.Model
{
    public class Club : Entity
    {
        public bool Initilisation { get => this.initilisation; set => this.initilisation = value; }
        private bool initilisation = true;

        public byte Id { get => this.id; set { this.id = value; this.OnPropertyChanged(nameof(this.Id)); } }
        private byte id;

        public PlayerEnums.Country Country { get => this.country; set { this.country = value; this.OnPropertyChanged(nameof(this.Country)); } }
        private PlayerEnums.Country country;

        public Addresses Addresses { get => this.addresses; set { this.addresses = value; this.OnPropertyChanged(nameof(this.Addresses)); } }
        private Addresses addresses;

        #region Helper
        public bool IsBlockARoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockA); set => this.Multiplex(ClubEnums.Roof.BlockA, value, nameof(this.IsBlockARoof)); }
        public bool IsBlockBRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockB); set => this.Multiplex(ClubEnums.Roof.BlockB, value, nameof(this.IsBlockBRoof)); }
        public bool IsBlockCRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockC); set => this.Multiplex(ClubEnums.Roof.BlockC, value, nameof(this.IsBlockCRoof)); }
        public bool IsBlockDRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockD); set => this.Multiplex(ClubEnums.Roof.BlockD, value, nameof(this.IsBlockDRoof)); }
        public bool IsBlockERoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockE); set => this.Multiplex(ClubEnums.Roof.BlockE, value, nameof(this.IsBlockERoof)); }
        public bool IsBlockFRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockF); set => this.Multiplex(ClubEnums.Roof.BlockF, value, nameof(this.IsBlockFRoof)); }
        public bool IsBlockGRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockG); set => this.Multiplex(ClubEnums.Roof.BlockG, value, nameof(this.IsBlockGRoof)); }
        public bool IsBlockHRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockH); set => this.Multiplex(ClubEnums.Roof.BlockH, value, nameof(this.IsBlockHRoof)); }
        public bool IsBlockIRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockI); set => this.Multiplex(ClubEnums.Roof.BlockI, value, nameof(this.IsBlockIRoof)); }
        public bool IsBlockJRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockJ); set => this.Multiplex(ClubEnums.Roof.BlockJ, value, nameof(this.IsBlockJRoof)); }
        public bool IsBlockKRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockK); set => this.Multiplex(ClubEnums.Roof.BlockK, value, nameof(this.IsBlockKRoof)); }
        public bool IsBlockLRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockL); set => this.Multiplex(ClubEnums.Roof.BlockL, value, nameof(this.IsBlockLRoof)); }
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
        public ClubEnums.DisplayUnit DisplayUnit { get => this.displayUnit; set => this.Setter(value); }
        private ClubEnums.DisplayUnit displayUnit = ClubEnums.DisplayUnit.None;
        public ClubEnums.FieldCondition FieldCondition { get => this.fieldCondition; set => this.Setter(value); }
        private ClubEnums.FieldCondition fieldCondition = ClubEnums.FieldCondition.Clean;
        public bool HasGrassHeating { get => this.grassHeating; set => this.grassHeating = value; }
        private bool grassHeating = false;
        public bool HasFloodLight { get => this.floodLight; set => this.floodLight = value; }
        private bool floodLight = false;

        public ClubEnums.Roof Roof { get => this.roof; set => this.Setter(value); }
        private ClubEnums.Roof roof = ClubEnums.Roof.None;
        public byte BlockAWeeks { get => this.blockAWeeks; set { this.blockAWeeks = value; this.OnPropertyChanged(nameof(this.BlockAWeeks)); } }
        private byte blockAWeeks = 0;
        public byte BlockBWeeks { get => this.blockBWeeks; set { this.blockBWeeks = value; this.OnPropertyChanged(nameof(this.BlockBWeeks)); } }
        private byte blockBWeeks = 0;
        public byte BlockCWeeks { get => this.blockCWeeks; set { this.blockCWeeks = value; this.OnPropertyChanged(nameof(this.BlockCWeeks)); } }
        private byte blockCWeeks = 0;
        public byte BlockDWeeks { get => this.blockDWeeks; set { this.blockDWeeks = value; this.OnPropertyChanged(nameof(this.BlockDWeeks)); } }
        private byte blockDWeeks = 0;
        public byte BlockEWeeks { get => this.blockEWeeks; set { this.blockEWeeks = value; this.OnPropertyChanged(nameof(this.BlockEWeeks)); } }
        private byte blockEWeeks = 0;
        public byte BlockFWeeks { get => this.blockFWeeks; set { this.blockFWeeks = value; this.OnPropertyChanged(nameof(this.BlockFWeeks)); } }
        private byte blockFWeeks = 0;
        public byte BlockGWeeks { get => this.blockGWeeks; set { this.blockGWeeks = value; this.OnPropertyChanged(nameof(this.BlockGWeeks)); } }
        private byte blockGWeeks = 0;
        public byte BlockHWeeks { get => this.blockHWeeks; set { this.blockHWeeks = value; this.OnPropertyChanged(nameof(this.BlockHWeeks)); } }
        private byte blockHWeeks = 0;
        public byte BlockIWeeks { get => this.blockIWeeks; set { this.blockIWeeks = value; this.OnPropertyChanged(nameof(this.BlockIWeeks)); } }
        private byte blockIWeeks = 0;
        public byte BlockJWeeks { get => this.blockJWeeks; set { this.blockJWeeks = value; this.OnPropertyChanged(nameof(this.BlockJWeeks)); } }
        private byte blockJWeeks = 0;
        public byte BlockKWeeks { get => this.blockKWeeks; set { this.blockKWeeks = value; this.OnPropertyChanged(nameof(this.BlockKWeeks)); } }
        private byte blockKWeeks = 0;
        public byte BlockLWeeks { get => this.blockLWeeks; set { this.blockLWeeks = value; this.OnPropertyChanged(nameof(this.BlockLWeeks)); } }
        private byte blockLWeeks = 0;

        public ushort BlockAStandings { get => this.blockAStandings; set { this.blockAStandings = this.BlockSetter(this.blockAStandings, value); this.OnPropertyChanged(nameof(this.BlockAStandings)); } }
        private ushort blockAStandings = 0;
        public ushort BlockBStandings { get => this.blockBStandings; set { this.blockBStandings = this.BlockSetter(this.blockBStandings, value); this.OnPropertyChanged(nameof(this.BlockBStandings)); } }
        private ushort blockBStandings = 0;
        public ushort BlockCStandings { get => this.blockCStandings; set { this.blockCStandings = this.BlockSetter(this.blockCStandings, value); this.OnPropertyChanged(nameof(this.BlockCStandings)); } }
        private ushort blockCStandings = 0;
        public ushort BlockDStandings { get => this.blockDStandings; set { this.blockDStandings = this.BlockSetter(this.blockDStandings, value); this.OnPropertyChanged(nameof(this.BlockDStandings)); } }
        private ushort blockDStandings = 0;
        public ushort BlockEStandings { get => this.blockEStandings; set { this.blockEStandings = this.BlockSetter(this.blockEStandings, value); this.OnPropertyChanged(nameof(this.BlockEStandings)); } }
        private ushort blockEStandings = 0;
        public ushort BlockFStandings { get => this.blockFStandings; set { this.blockFStandings = this.BlockSetter(this.blockFStandings, value); this.OnPropertyChanged(nameof(this.BlockFStandings)); } }
        private ushort blockFStandings = 0;
        public ushort BlockGStandings { get => this.blockGStandings; set { this.blockGStandings = this.BlockSetter(this.blockGStandings, value); this.OnPropertyChanged(nameof(this.BlockGStandings)); } }
        private ushort blockGStandings = 0;
        public ushort BlockHStandings { get => this.blockHStandings; set { this.blockHStandings = this.BlockSetter(this.blockHStandings, value); this.OnPropertyChanged(nameof(this.BlockHStandings)); } }
        private ushort blockHStandings = 0;
        public ushort BlockIStandings { get => this.blockIStandings; set { this.blockIStandings = this.BlockSetter(this.blockIStandings, value); this.OnPropertyChanged(nameof(this.BlockIStandings)); } }
        private ushort blockIStandings = 0;
        public ushort BlockJStandings { get => this.blockJStandings; set { this.blockJStandings = this.BlockSetter(this.blockJStandings, value); this.OnPropertyChanged(nameof(this.BlockJStandings)); } }
        private ushort blockJStandings = 0;
        public ushort BlockKStandings { get => this.blockKStandings; set { this.blockKStandings = this.BlockSetter(this.blockKStandings, value); this.OnPropertyChanged(nameof(this.BlockKStandings)); } }
        private ushort blockKStandings = 0;
        public ushort BlockLStandings { get => this.blockLStandings; set { this.blockLStandings = this.BlockSetter(this.blockLStandings, value); this.OnPropertyChanged(nameof(this.BlockLStandings)); } }
        private ushort blockLStandings = 0;

        public ushort BlockASeats { get => this.blockASeats; set { this.blockASeats = this.BlockSetter(this.blockASeats, value); this.OnPropertyChanged(nameof(this.BlockASeats)); } }
        private ushort blockASeats = 0;
        public ushort BlockBSeats { get => this.blockBSeats; set { this.blockBSeats = this.BlockSetter(this.blockBSeats, value); this.OnPropertyChanged(nameof(this.BlockBSeats)); } }
        private ushort blockBSeats = 0;
        public ushort BlockCSeats { get => this.blockCSeats; set { this.blockCSeats = this.BlockSetter(this.blockCSeats, value); this.OnPropertyChanged(nameof(this.BlockCSeats)); } }
        private ushort blockCSeats = 0;
        public ushort BlockDSeats { get => this.blockDSeats; set { this.blockDSeats = this.BlockSetter(this.blockDSeats, value); this.OnPropertyChanged(nameof(this.BlockDSeats)); } }
        private ushort blockDSeats = 0;
        public ushort BlockESeats { get => this.blockESeats; set { this.blockESeats = this.BlockSetter(this.blockESeats, value); this.OnPropertyChanged(nameof(this.BlockESeats)); } }
        private ushort blockESeats = 0;
        public ushort BlockFSeats { get => this.blockFSeats; set { this.blockFSeats = this.BlockSetter(this.blockFSeats, value); this.OnPropertyChanged(nameof(this.BlockFSeats)); } }
        private ushort blockFSeats = 0;
        public ushort BlockGSeats { get => this.blockGSeats; set { this.blockGSeats = this.BlockSetter(this.blockGSeats, value); this.OnPropertyChanged(nameof(this.BlockGSeats)); } }
        private ushort blockGSeats = 0;
        public ushort BlockHSeats { get => this.blockHSeats; set { this.blockHSeats = this.BlockSetter(this.blockHSeats, value); this.OnPropertyChanged(nameof(this.BlockHSeats)); } }
        private ushort blockHSeats = 0;
        public ushort BlockISeats { get => this.blockISeats; set { this.blockISeats = this.BlockSetter(this.blockISeats, value); this.OnPropertyChanged(nameof(this.BlockISeats)); } }
        private ushort blockISeats = 0;
        public ushort BlockJSeats { get => this.blockJSeats; set { this.blockJSeats = this.BlockSetter(this.blockJSeats, value); this.OnPropertyChanged(nameof(this.BlockJSeats)); } }
        private ushort blockJSeats = 0;
        public ushort BlockKSeats { get => this.blockKSeats; set { this.blockKSeats = this.BlockSetter(this.blockKSeats, value); this.OnPropertyChanged(nameof(this.BlockKSeats)); } }
        private ushort blockKSeats = 0;
        public ushort BlockLSeats { get => this.blockLSeats; set { this.blockLSeats = this.BlockSetter(this.blockLSeats, value); this.OnPropertyChanged(nameof(this.BlockLSeats)); } }
        private ushort blockLSeats = 0;
        #endregion

        #region Finance
        public int EarningsAds { get => this.earningsAds; set { this.earningsAds = value; this.OnPropertyChanged(nameof(this.EarningsAds)); } }
        private int earningsAds = 0;
        public int EarningsFriendlyGames { get => this.earningsFriendlyGames; set { this.earningsFriendlyGames = value; this.OnPropertyChanged(nameof(this.EarningsFriendlyGames)); } }
        private int earningsFriendlyGames = 0;
        public int EarningsLeagueGames { get => this.earningsLeagueGames; set { this.earningsLeagueGames = value; this.OnPropertyChanged(nameof(this.EarningsLeagueGames)); } }
        private int earningsLeagueGames = 0;
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

        public byte AmateurPlayerCount { get => this.amateurPlayerCount; set { this.amateurPlayerCount = value; this.OnPropertyChanged(nameof(this.AmateurPlayerCount)); } }
        private byte amateurPlayerCount;
        public byte TraineeACount { get => this.traineeACount; set { this.traineeACount = value; this.OnPropertyChanged(nameof(this.TraineeACount)); } }
        private byte traineeACount;
        public byte TraineeBCount { get => this.traineeBCount; set { this.traineeBCount = value; this.OnPropertyChanged(nameof(this.TraineeBCount)); } }
        private byte traineeBCount;
        public byte TraineeCCount { get => this.traineeCCount; set { this.traineeCCount = value; this.OnPropertyChanged(nameof(this.TraineeCCount)); } }
        private byte traineeCCount;
        #endregion

        public String SquadAddress { get => this.squadAddress; set { this.squadAddress = value; this.OnPropertyChanged(nameof(this.SquadAddress)); } }

        private String squadAddress;

        #region Setter
        private void Setter(ClubEnums.DisplayUnit displayUnit, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.displayUnit = displayUnit;
                this.OnPropertyChanged(nameof(this.DisplayUnit));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Setter(ClubEnums.FieldCondition fieldCondition, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.fieldCondition = fieldCondition;
                this.OnPropertyChanged(nameof(this.DisplayUnit));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Setter(ClubEnums.Roof roof, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.roof = roof;
                this.OnPropertyChanged(nameof(this.Roof));
                if (!this.Initilisation)
                {
                    this.blockAWeeks = this.blockAWeeks < (byte)1 ? (byte)1 : this.blockAWeeks;
                    this.OnPropertyChanged(nameof(this.BlockAWeeks));
                }
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private ushort BlockSetter(ushort blocksize, ushort value)
        {
            ushort output = blocksize;
            if (value != output)
            {
                output = value;
                if (!this.Initilisation)
                {
                    this.blockAWeeks = this.blockAWeeks < (byte)1 ? (byte)1 : this.blockAWeeks;
                    this.OnPropertyChanged(nameof(this.BlockAWeeks));
                }
            }
            return output;
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

        public bool IsClubMember(Player player)
        {
            if (player != null)
            {
                return player.ClubCountry == this.Country && player.ClubId == this.Id;
            }
            return false;
        }

    }
}
