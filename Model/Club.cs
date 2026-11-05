using System;
using System.Collections.Generic;
using System.Reflection;

namespace A2G_Trainer.Model
{
    /// <summary>
    /// A club, including its stadium (block-level roof/standings/seats/weeks-open-since data
    /// for blocks A-L) and finances. Used for both the user's own club and any club looked up
    /// by <see cref="ClubController"/>.
    /// </summary>
    public class Club : Entity
    {
        /// <summary>Whether the club is still being populated from raw memory (suppresses week-tracking side effects while true).</summary>
        public bool Initilisation { get => this.initilisation; set => this.initilisation = value; }
        private bool initilisation = true;

        /// <summary>Club's numeric ID, used to match it against player records and address presets.</summary>
        public byte Id { get => this.id; set { this.id = value; this.OnPropertyChanged(nameof(this.Id)); } }
        private byte id;

        /// <summary>Country the club is registered in.</summary>
        public PlayerEnums.Country Country { get => this.country; set { this.country = value; this.OnPropertyChanged(nameof(this.Country)); } }
        private PlayerEnums.Country country;

        /// <summary>Memory address preset currently bound to this club instance.</summary>
        public Addresses Addresses { get => this.addresses; set { this.addresses = value; this.OnPropertyChanged(nameof(this.Addresses)); } }
        private Addresses addresses;

        #region Helper
        /// <summary>Whether stadium block A has a roof.</summary>
        public bool IsBlockARoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockA); set => this.Multiplex(ClubEnums.Roof.BlockA, value, nameof(this.IsBlockARoof)); }
        /// <summary>Whether stadium block B has a roof.</summary>
        public bool IsBlockBRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockB); set => this.Multiplex(ClubEnums.Roof.BlockB, value, nameof(this.IsBlockBRoof)); }
        /// <summary>Whether stadium block C has a roof.</summary>
        public bool IsBlockCRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockC); set => this.Multiplex(ClubEnums.Roof.BlockC, value, nameof(this.IsBlockCRoof)); }
        /// <summary>Whether stadium block D has a roof.</summary>
        public bool IsBlockDRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockD); set => this.Multiplex(ClubEnums.Roof.BlockD, value, nameof(this.IsBlockDRoof)); }
        /// <summary>Whether stadium block E has a roof.</summary>
        public bool IsBlockERoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockE); set => this.Multiplex(ClubEnums.Roof.BlockE, value, nameof(this.IsBlockERoof)); }
        /// <summary>Whether stadium block F has a roof.</summary>
        public bool IsBlockFRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockF); set => this.Multiplex(ClubEnums.Roof.BlockF, value, nameof(this.IsBlockFRoof)); }
        /// <summary>Whether stadium block G has a roof.</summary>
        public bool IsBlockGRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockG); set => this.Multiplex(ClubEnums.Roof.BlockG, value, nameof(this.IsBlockGRoof)); }
        /// <summary>Whether stadium block H has a roof.</summary>
        public bool IsBlockHRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockH); set => this.Multiplex(ClubEnums.Roof.BlockH, value, nameof(this.IsBlockHRoof)); }
        /// <summary>Whether stadium block I has a roof.</summary>
        public bool IsBlockIRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockI); set => this.Multiplex(ClubEnums.Roof.BlockI, value, nameof(this.IsBlockIRoof)); }
        /// <summary>Whether stadium block J has a roof.</summary>
        public bool IsBlockJRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockJ); set => this.Multiplex(ClubEnums.Roof.BlockJ, value, nameof(this.IsBlockJRoof)); }
        /// <summary>Whether stadium block K has a roof.</summary>
        public bool IsBlockKRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockK); set => this.Multiplex(ClubEnums.Roof.BlockK, value, nameof(this.IsBlockKRoof)); }
        /// <summary>Whether stadium block L has a roof.</summary>
        public bool IsBlockLRoof { get => this.Roof.HasFlag(ClubEnums.Roof.BlockL); set => this.Multiplex(ClubEnums.Roof.BlockL, value, nameof(this.IsBlockLRoof)); }
        #endregion
        /// <summary>Club's display name (max 19 characters).</summary>
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
        /// <summary>Stadium's display name (max 28 characters).</summary>
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
        /// <summary>Type of scoreboard installed at the stadium.</summary>
        public ClubEnums.DisplayUnit DisplayUnit { get => this.displayUnit; set => this.Setter(value); }
        private ClubEnums.DisplayUnit displayUnit = ClubEnums.DisplayUnit.None;
        /// <summary>Current pitch condition bucket.</summary>
        public ClubEnums.FieldCondition FieldCondition { get => this.fieldCondition; set => this.Setter(value); }
        private ClubEnums.FieldCondition fieldCondition = ClubEnums.FieldCondition.Clean;
        /// <summary>Whether the pitch has grass heating installed.</summary>
        public bool HasGrassHeating { get => this.grassHeating; set => this.grassHeating = value; }
        private bool grassHeating = false;
        /// <summary>Whether the stadium has floodlights installed.</summary>
        public bool HasFloodLight { get => this.floodLight; set => this.floodLight = value; }
        private bool floodLight = false;

        /// <summary>Whether the stadium has a running track (+0x89A): the game's own per-block limits are then 18,000 everywhere, and the "ball net" event (EreigGld.saq 0xD3) can only occur with a track. The match-day mood byte (+0xEF) it seeds is read identically for both values.</summary>
        public bool HasRunningTrack { get => this.runningTrack; set { this.runningTrack = value; this.OnPropertyChanged(nameof(this.HasRunningTrack)); } }
        private bool runningTrack = false;
        /// <summary>"Sitzplätze oben" (+0x899): seats are built above the standing places, so converting one into the other costs less.</summary>
        public bool HasUpperSeats { get => this.upperSeats; set { this.upperSeats = value; this.OnPropertyChanged(nameof(this.HasUpperSeats)); } }
        private bool upperSeats = false;
        /// <summary>The town's construction grant pot (+0x3B8): pays part of stadium costs and some stadium events.</summary>
        public int GrantPot { get => this.grantPot; set { this.grantPot = value; this.OnPropertyChanged(nameof(this.GrantPot)); } }
        private int grantPot = 0;
        /// <summary>The town's grant rate (+0x3B4, the game sets 0-25 each season): chance in % that it pays, and then rate + 5 % of the costs.</summary>
        public short GrantRate { get => this.grantRate; set { this.grantRate = value; this.OnPropertyChanged(nameof(this.GrantRate)); } }
        private short grantRate = 0;

        /// <summary>Bitmask of which stadium blocks (A-L) currently have a roof.</summary>
        public ClubEnums.Roof Roof { get => this.roof; set => this.Setter(value); }
        private ClubEnums.Roof roof = ClubEnums.Roof.None;
        /// <summary>Weeks stadium block A is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockAWeeks { get => this.blockAWeeks; set { this.blockAWeeks = value; this.OnPropertyChanged(nameof(this.BlockAWeeks)); } }
        private byte blockAWeeks = 0;
        /// <summary>Weeks stadium block B is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockBWeeks { get => this.blockBWeeks; set { this.blockBWeeks = value; this.OnPropertyChanged(nameof(this.BlockBWeeks)); } }
        private byte blockBWeeks = 0;
        /// <summary>Weeks stadium block C is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockCWeeks { get => this.blockCWeeks; set { this.blockCWeeks = value; this.OnPropertyChanged(nameof(this.BlockCWeeks)); } }
        private byte blockCWeeks = 0;
        /// <summary>Weeks stadium block D is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockDWeeks { get => this.blockDWeeks; set { this.blockDWeeks = value; this.OnPropertyChanged(nameof(this.BlockDWeeks)); } }
        private byte blockDWeeks = 0;
        /// <summary>Weeks stadium block E is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockEWeeks { get => this.blockEWeeks; set { this.blockEWeeks = value; this.OnPropertyChanged(nameof(this.BlockEWeeks)); } }
        private byte blockEWeeks = 0;
        /// <summary>Weeks stadium block F is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockFWeeks { get => this.blockFWeeks; set { this.blockFWeeks = value; this.OnPropertyChanged(nameof(this.BlockFWeeks)); } }
        private byte blockFWeeks = 0;
        /// <summary>Weeks stadium block G is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockGWeeks { get => this.blockGWeeks; set { this.blockGWeeks = value; this.OnPropertyChanged(nameof(this.BlockGWeeks)); } }
        private byte blockGWeeks = 0;
        /// <summary>Weeks stadium block H is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockHWeeks { get => this.blockHWeeks; set { this.blockHWeeks = value; this.OnPropertyChanged(nameof(this.BlockHWeeks)); } }
        private byte blockHWeeks = 0;
        /// <summary>Weeks stadium block I is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockIWeeks { get => this.blockIWeeks; set { this.blockIWeeks = value; this.OnPropertyChanged(nameof(this.BlockIWeeks)); } }
        private byte blockIWeeks = 0;
        /// <summary>Weeks stadium block J is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockJWeeks { get => this.blockJWeeks; set { this.blockJWeeks = value; this.OnPropertyChanged(nameof(this.BlockJWeeks)); } }
        private byte blockJWeeks = 0;
        /// <summary>Weeks stadium block K is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockKWeeks { get => this.blockKWeeks; set { this.blockKWeeks = value; this.OnPropertyChanged(nameof(this.BlockKWeeks)); } }
        private byte blockKWeeks = 0;
        /// <summary>Weeks stadium block L is still under construction; while above 0 the block is closed and its places don't count.</summary>
        public byte BlockLWeeks { get => this.blockLWeeks; set { this.blockLWeeks = value; this.OnPropertyChanged(nameof(this.BlockLWeeks)); } }
        private byte blockLWeeks = 0;

        /// <summary>Number of standing-room places in stadium block A.</summary>
        public short BlockAStandings { get => this.blockAStandings; set { this.blockAStandings = value; this.OnPropertyChanged(nameof(this.BlockAStandings)); } }
        private short blockAStandings = 0;
        /// <summary>Number of standing-room places in stadium block B.</summary>
        public short BlockBStandings { get => this.blockBStandings; set { this.blockBStandings = value; this.OnPropertyChanged(nameof(this.BlockBStandings)); } }
        private short blockBStandings = 0;
        /// <summary>Number of standing-room places in stadium block C.</summary>
        public short BlockCStandings { get => this.blockCStandings; set { this.blockCStandings = value; this.OnPropertyChanged(nameof(this.BlockCStandings)); } }
        private short blockCStandings = 0;
        /// <summary>Number of standing-room places in stadium block D.</summary>
        public short BlockDStandings { get => this.blockDStandings; set { this.blockDStandings = value; this.OnPropertyChanged(nameof(this.BlockDStandings)); } }
        private short blockDStandings = 0;
        /// <summary>Number of standing-room places in stadium block E.</summary>
        public short BlockEStandings { get => this.blockEStandings; set { this.blockEStandings = value; this.OnPropertyChanged(nameof(this.BlockEStandings)); } }
        private short blockEStandings = 0;
        /// <summary>Number of standing-room places in stadium block F.</summary>
        public short BlockFStandings { get => this.blockFStandings; set { this.blockFStandings = value; this.OnPropertyChanged(nameof(this.BlockFStandings)); } }
        private short blockFStandings = 0;
        /// <summary>Number of standing-room places in stadium block G.</summary>
        public short BlockGStandings { get => this.blockGStandings; set { this.blockGStandings = value; this.OnPropertyChanged(nameof(this.BlockGStandings)); } }
        private short blockGStandings = 0;
        /// <summary>Number of standing-room places in stadium block H.</summary>
        public short BlockHStandings { get => this.blockHStandings; set { this.blockHStandings = value; this.OnPropertyChanged(nameof(this.BlockHStandings)); } }
        private short blockHStandings = 0;
        /// <summary>Number of standing-room places in stadium block I.</summary>
        public short BlockIStandings { get => this.blockIStandings; set { this.blockIStandings = value; this.OnPropertyChanged(nameof(this.BlockIStandings)); } }
        private short blockIStandings = 0;
        /// <summary>Number of standing-room places in stadium block J.</summary>
        public short BlockJStandings { get => this.blockJStandings; set { this.blockJStandings = value; this.OnPropertyChanged(nameof(this.BlockJStandings)); } }
        private short blockJStandings = 0;
        /// <summary>Number of standing-room places in stadium block K.</summary>
        public short BlockKStandings { get => this.blockKStandings; set { this.blockKStandings = value; this.OnPropertyChanged(nameof(this.BlockKStandings)); } }
        private short blockKStandings = 0;
        /// <summary>Number of standing-room places in stadium block L.</summary>
        public short BlockLStandings { get => this.blockLStandings; set { this.blockLStandings = value; this.OnPropertyChanged(nameof(this.BlockLStandings)); } }
        private short blockLStandings = 0;

        /// <summary>Number of seats in stadium block A.</summary>
        public short BlockASeats { get => this.blockASeats; set { this.blockASeats = value; this.OnPropertyChanged(nameof(this.BlockASeats)); } }
        private short blockASeats = 0;
        /// <summary>Number of seats in stadium block B.</summary>
        public short BlockBSeats { get => this.blockBSeats; set { this.blockBSeats = value; this.OnPropertyChanged(nameof(this.BlockBSeats)); } }
        private short blockBSeats = 0;
        /// <summary>Number of seats in stadium block C.</summary>
        public short BlockCSeats { get => this.blockCSeats; set { this.blockCSeats = value; this.OnPropertyChanged(nameof(this.BlockCSeats)); } }
        private short blockCSeats = 0;
        /// <summary>Number of seats in stadium block D.</summary>
        public short BlockDSeats { get => this.blockDSeats; set { this.blockDSeats = value; this.OnPropertyChanged(nameof(this.BlockDSeats)); } }
        private short blockDSeats = 0;
        /// <summary>Number of seats in stadium block E.</summary>
        public short BlockESeats { get => this.blockESeats; set { this.blockESeats = value; this.OnPropertyChanged(nameof(this.BlockESeats)); } }
        private short blockESeats = 0;
        /// <summary>Number of seats in stadium block F.</summary>
        public short BlockFSeats { get => this.blockFSeats; set { this.blockFSeats = value; this.OnPropertyChanged(nameof(this.BlockFSeats)); } }
        private short blockFSeats = 0;
        /// <summary>Number of seats in stadium block G.</summary>
        public short BlockGSeats { get => this.blockGSeats; set { this.blockGSeats = value; this.OnPropertyChanged(nameof(this.BlockGSeats)); } }
        private short blockGSeats = 0;
        /// <summary>Number of seats in stadium block H.</summary>
        public short BlockHSeats { get => this.blockHSeats; set { this.blockHSeats = value; this.OnPropertyChanged(nameof(this.BlockHSeats)); } }
        private short blockHSeats = 0;
        /// <summary>Number of seats in stadium block I.</summary>
        public short BlockISeats { get => this.blockISeats; set { this.blockISeats = value; this.OnPropertyChanged(nameof(this.BlockISeats)); } }
        private short blockISeats = 0;
        /// <summary>Number of seats in stadium block J.</summary>
        public short BlockJSeats { get => this.blockJSeats; set { this.blockJSeats = value; this.OnPropertyChanged(nameof(this.BlockJSeats)); } }
        private short blockJSeats = 0;
        /// <summary>Number of seats in stadium block K.</summary>
        public short BlockKSeats { get => this.blockKSeats; set { this.blockKSeats = value; this.OnPropertyChanged(nameof(this.BlockKSeats)); } }
        private short blockKSeats = 0;
        /// <summary>Number of seats in stadium block L.</summary>
        public short BlockLSeats { get => this.blockLSeats; set { this.blockLSeats = value; this.OnPropertyChanged(nameof(this.BlockLSeats)); } }
        private short blockLSeats = 0;

        /// <summary>
        /// The stadium capacity the game sells tickets against (club +0x850). The game only recounts it
        /// when a construction finishes or at the season change - see <see cref="OpenCapacity"/>.
        /// </summary>
        public int Capacity { get => this.capacity; set { this.capacity = value; this.OnPropertyChanged(nameof(this.Capacity)); } }
        private int capacity = 0;

        /// <summary>Capacity counted the way the game does it (FUN_00607920): standing places (block K: boxes) and seats of every block not under construction.</summary>
        public int OpenCapacity()
        {
            int total = 0;
            for (int block = 0; block < BlockCount; block++)
            {
                if (this.GetWeeks(block) == 0)
                    total += this.GetStandings(block) + this.GetSeats(block);
            }
            return total;
        }

        /// <summary>The twelve stadium blocks A-L, in the game's order (index = roof bit).</summary>
        public const string BlockLetters = "ABCDEFGHIJKL";
        /// <summary>Number of stadium blocks.</summary>
        public const int BlockCount = 12;
        /// <summary>Block K: the game keeps boxes ("Logen") in its standing places (block type 3, table +2BAC00).</summary>
        public const int BoxBlock = 10;

        private static PropertyInfo BlockProperty(int block, string field) => typeof(Club).GetProperty($"Block{BlockLetters[block]}{field}");

        /// <summary>Standing places of a block (block K: boxes).</summary>
        public short GetStandings(int block) => (short)BlockProperty(block, "Standings").GetValue(this, null);
        /// <summary>Sets the standing places of a block (block K: boxes).</summary>
        public void SetStandings(int block, short value) => BlockProperty(block, "Standings").SetValue(this, value, null);
        /// <summary>Seats of a block.</summary>
        public short GetSeats(int block) => (short)BlockProperty(block, "Seats").GetValue(this, null);
        /// <summary>Sets the seats of a block.</summary>
        public void SetSeats(int block, short value) => BlockProperty(block, "Seats").SetValue(this, value, null);
        /// <summary>Remaining construction weeks of a block.</summary>
        public byte GetWeeks(int block) => (byte)BlockProperty(block, "Weeks").GetValue(this, null);
        /// <summary>Sets the remaining construction weeks of a block.</summary>
        public void SetWeeks(int block, byte value) => BlockProperty(block, "Weeks").SetValue(this, value, null);
        /// <summary>Whether a block has a roof.</summary>
        public bool HasRoof(int block) => ((int)this.Roof & (1 << block)) != 0;
        /// <summary>Puts a roof on a block or takes it off.</summary>
        public void SetRoof(int block, bool value) => typeof(Club).GetProperty($"IsBlock{BlockLetters[block]}Roof").SetValue(this, value, null);
        #endregion

        #region Finance
        /// <summary>Club's total wealth/cash reserves.</summary>
        public int Wealth{ get => this.wealth; set { this.wealth = value; this.OnPropertyChanged(nameof(this.Wealth)); } }
        private int wealth = 0;
        /// <summary>Cash amount received from the club's current sponsorship deal.</summary>
        public int SponsorCash { get => this.sponsorCash; set { this.sponsorCash = value; this.OnPropertyChanged(nameof(this.SponsorCash)); } }
        private int sponsorCash = 0;
        /// <summary>Remaining duration (in periods) of the club's current sponsorship deal.</summary>
        public byte SponsorPeriod { get => this.sponsorPeriod; set { this.sponsorPeriod = value; this.OnPropertyChanged(nameof(this.SponsorPeriod)); } }
        private byte sponsorPeriod = 0;
        /// <summary>Club's earnings from advertising.</summary>
        public int EarningsAds { get => this.earningsAds; set { this.earningsAds = value; this.OnPropertyChanged(nameof(this.EarningsAds)); } }
        private int earningsAds = 0;
        /// <summary>Club's earnings from friendly games.</summary>
        public int EarningsFriendlyGames { get => this.earningsFriendlyGames; set { this.earningsFriendlyGames = value; this.OnPropertyChanged(nameof(this.EarningsFriendlyGames)); } }
        private int earningsFriendlyGames = 0;
        /// <summary>Club's earnings from league games.</summary>
        public int EarningsLeagueGames { get => this.earningsLeagueGames; set { this.earningsLeagueGames = value; this.OnPropertyChanged(nameof(this.EarningsLeagueGames)); } }
        private int earningsLeagueGames = 0;
        #endregion

        #region PlayerCount
        /// <summary>Number of players currently on the club's roster.</summary>
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

        /// <summary>Number of amateur players on the club's roster.</summary>
        public byte AmateurPlayerCount { get => this.amateurPlayerCount; set { this.amateurPlayerCount = value; this.OnPropertyChanged(nameof(this.AmateurPlayerCount)); } }
        private byte amateurPlayerCount;
        /// <summary>Number of trainees in trainee group A.</summary>
        public byte TraineeACount { get => this.traineeACount; set { this.traineeACount = value; this.OnPropertyChanged(nameof(this.TraineeACount)); } }
        private byte traineeACount;
        /// <summary>Number of trainees in trainee group B.</summary>
        public byte TraineeBCount { get => this.traineeBCount; set { this.traineeBCount = value; this.OnPropertyChanged(nameof(this.TraineeBCount)); } }
        private byte traineeBCount;
        /// <summary>Number of trainees in trainee group C.</summary>
        public byte TraineeCCount { get => this.traineeCCount; set { this.traineeCCount = value; this.OnPropertyChanged(nameof(this.TraineeCCount)); } }
        private byte traineeCCount;
        #endregion

        /// <summary>Number of free tickets the club issues per home match.</summary>
        public ushort FreeTickets { get => this.freeTickets; set { this.freeTickets = value; this.OnPropertyChanged(nameof(this.FreeTickets)); } }
        private ushort freeTickets;
        /// <summary>Number of away-game supporters the club attracts.</summary>
        public ushort RoadGameSupport { get => this.roadGameSupport; set { this.roadGameSupport = value; this.OnPropertyChanged(nameof(this.RoadGameSupport)); } }
        private ushort roadGameSupport;

        /// <summary>Club's respect rating (x10 scale).</summary>
        public byte Respect { get => this.respect; set { this.respect = value; this.OnPropertyChanged(nameof(this.Respect)); } }
        private byte respect;
        /// <summary>Club's mood/morale rating (x10 scale).</summary>
        public byte Spirit { get => this.spirit; set { this.spirit = value; this.OnPropertyChanged(nameof(this.Spirit)); } }
        private byte spirit;
        /// <summary>Club's will-to-win rating (x10 scale).</summary>
        public byte Will2Win { get => this.will2Win; set { this.will2Win = value; this.OnPropertyChanged(nameof(this.Will2Win)); } }
        private byte will2Win;
        /// <summary>Club's team cohesion rating (x10 scale).</summary>
        public byte TeamCohesion { get => this.teamCohesion; set { this.teamCohesion = value; this.OnPropertyChanged(nameof(this.TeamCohesion)); } }
        private byte teamCohesion;

        #region Editor settings
        /// <summary>Max length of <see cref="Chant"/> and <see cref="FanName"/>: the game copies them unchecked into its texts (the original data uses up to 18).</summary>
        public const int FanTextLength = 20;
        /// <summary>Max length of <see cref="Town"/> (the editor's limit).</summary>
        public const int TownLength = 16;
        /// <summary>Length of <see cref="ShortName"/>; "---" means none.</summary>
        public const int ShortNameLength = 3;
        /// <summary><see cref="ArchRivalId"/>/<see cref="FanFriendshipId"/> value for "none".</summary>
        public const byte NoClub = 0xFF;

        /// <summary>Whether the club's record was found, so <see cref="ShortName"/>, <see cref="Chant"/> and <see cref="FanName"/> are known.</summary>
        public bool HasRecord { get => this.hasRecord; set { this.hasRecord = value; this.OnPropertyChanged(nameof(this.HasRecord)); } }
        private bool hasRecord;

        /// <summary>"Kürzel" (record +0x14), three characters.</summary>
        public string ShortName { get => this.shortName; set => this.SetText(ref this.shortName, value, ShortNameLength, nameof(this.ShortName)); }
        private string shortName = string.Empty;

        /// <summary>"Anfeuerung" (record +0x18): what the fans chant, e.g. "Hertha".</summary>
        public string Chant { get => this.chant; set => this.SetText(ref this.chant, value, FanTextLength, nameof(this.Chant)); }
        private string chant = string.Empty;

        /// <summary>"Fanbezeichnung" (record +0x4A): what the fans are called, e.g. "Herthaner".</summary>
        public string FanName { get => this.fanName; set => this.SetText(ref this.fanName, value, FanTextLength, nameof(this.FanName)); }
        private string fanName = string.Empty;

        /// <summary>"Ort" of the stadium (+0x839).</summary>
        public string Town { get => this.town; set => this.SetText(ref this.town, value, TownLength, nameof(this.Town)); }
        private string town = string.Empty;

        /// <summary>Article the game's texts put in front of the club name (+0xF2).</summary>
        public ClubEnums.Article Article { get => this.article; set { this.article = value; this.OnPropertyChanged(nameof(this.Article)); } }
        private ClubEnums.Article article;

        /// <summary>Preposition for "...vom Verein kam..." (+0xF8).</summary>
        public ClubEnums.Preposition Preposition { get => this.preposition; set { this.preposition = value; this.OnPropertyChanged(nameof(this.Preposition)); } }
        private ClubEnums.Preposition preposition;

        /// <summary>"Fanaufkommen" (+0xEE); the game moves it during the season.</summary>
        public ClubEnums.FanTurnout FanTurnout { get => this.fanTurnout; set { this.fanTurnout = value; this.OnPropertyChanged(nameof(this.FanTurnout)); } }
        private ClubEnums.FanTurnout fanTurnout;

        /// <summary>"Art der Fans" (+0xEF); the game sets it anew after every match.</summary>
        public ClubEnums.FanType FanType { get => this.fanType; set { this.fanType = value; this.OnPropertyChanged(nameof(this.FanType)); } }
        private ClubEnums.FanType fanType;

        /// <summary>"Vorstand" (+0xF5); the game moves it during the season.</summary>
        public ClubEnums.Board Board { get => this.board; set { this.board = value; this.OnPropertyChanged(nameof(this.Board)); } }
        private ClubEnums.Board board;

        /// <summary>"Erzrivale" (+0xF4): ID of a club of the same country, <see cref="NoClub"/> for none.</summary>
        public byte ArchRivalId { get => this.archRivalId; set { this.archRivalId = value; this.OnPropertyChanged(nameof(this.ArchRivalId)); } }
        private byte archRivalId = NoClub;

        /// <summary>"Fanfreundschaft mit" (+0xF3): ID of a club of the same country, <see cref="NoClub"/> for none.</summary>
        public byte FanFriendshipId { get => this.fanFriendshipId; set { this.fanFriendshipId = value; this.OnPropertyChanged(nameof(this.FanFriendshipId)); } }
        private byte fanFriendshipId = NoClub;

        /// <summary>Position on the editor's map (+0x1438); the game uses the distance between two clubs.</summary>
        public short MapX { get => this.mapX; set { this.mapX = value; this.OnPropertyChanged(nameof(this.MapX)); } }
        private short mapX;

        /// <summary>Position on the editor's map (+0x143A).</summary>
        public short MapY { get => this.mapY; set { this.mapY = value; this.OnPropertyChanged(nameof(this.MapY)); } }
        private short mapY;

        /// <summary>
        /// Both jersey sets as stored (+0xE8): shirt, shorts, socks of the first set, then of the
        /// second. Shirt = colour + 16 * pattern, shorts and socks = colour.
        /// </summary>
        public byte[] Jersey
        {
            get => (byte[])this.jersey.Clone();
            set
            {
                if (value == null || value.Length != JerseyLength)
                    throw new ArgumentException($"A jersey has {JerseyLength} bytes.", nameof(value));
                this.jersey = (byte[])value.Clone();
                this.OnPropertyChanged(nameof(this.Jersey));
            }
        }
        private byte[] jersey = new byte[JerseyLength];
        /// <summary>Bytes of <see cref="Jersey"/>: two sets of shirt, shorts and socks.</summary>
        public const int JerseyLength = 6;

        /// <summary>Shirt colour of set 0 (first) or 1 (second).</summary>
        public ClubEnums.JerseyColor GetShirtColor(int set) => (ClubEnums.JerseyColor)(this.jersey[set * 3] & 0x0F);
        /// <summary>Shirt pattern of set 0 (first) or 1 (second).</summary>
        public ClubEnums.ShirtPattern GetShirtPattern(int set) => (ClubEnums.ShirtPattern)(this.jersey[set * 3] >> 4);
        /// <summary>Shorts colour of set 0 (first) or 1 (second).</summary>
        public ClubEnums.JerseyColor GetShortsColor(int set) => (ClubEnums.JerseyColor)this.jersey[set * 3 + 1];
        /// <summary>Socks colour of set 0 (first) or 1 (second).</summary>
        public ClubEnums.JerseyColor GetSocksColor(int set) => (ClubEnums.JerseyColor)this.jersey[set * 3 + 2];

        /// <summary>Sets the shirt colour of a set, keeping its pattern.</summary>
        public void SetShirtColor(int set, ClubEnums.JerseyColor color) => this.SetJerseyByte(set * 3, (byte)((this.jersey[set * 3] & 0xF0) | ((byte)color & 0x0F)));
        /// <summary>Sets the shirt pattern of a set, keeping its colour.</summary>
        public void SetShirtPattern(int set, ClubEnums.ShirtPattern pattern) => this.SetJerseyByte(set * 3, (byte)((this.jersey[set * 3] & 0x0F) | ((byte)pattern << 4)));
        /// <summary>Sets the shorts colour of a set.</summary>
        public void SetShortsColor(int set, ClubEnums.JerseyColor color) => this.SetJerseyByte(set * 3 + 1, (byte)color);
        /// <summary>Sets the socks colour of a set.</summary>
        public void SetSocksColor(int set, ClubEnums.JerseyColor color) => this.SetJerseyByte(set * 3 + 2, (byte)color);

        private void SetJerseyByte(int index, byte value)
        {
            this.jersey[index] = value;
            this.OnPropertyChanged(nameof(this.Jersey));
        }

        private void SetText(ref string field, string value, int maxLength, string name)
        {
            string limited = value == null ? string.Empty : value.Length > maxLength ? value.Substring(0, maxLength) : value;
            if (limited == field)
                return;
            field = limited;
            this.OnPropertyChanged(name);
        }
        #endregion

        /// <summary>Memory address string of the club's squad/roster.</summary>
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
                this.OnPropertyChanged(nameof(this.FieldCondition));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
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
        /// <summary>Sets or clears one bit of the <see cref="Roof"/> bitmask (backing the per-block IsBlockXRoof properties).</summary>
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

        /// <summary>Whether the given player belongs to this club (same country and club ID).</summary>
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
