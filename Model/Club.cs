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

        #region Tactics
        /// <summary>Highest level of the trained team skills (all but <see cref="OffsideTrap"/>).</summary>
        public const byte MaxTeamSkill = 5;

        /// <summary>Offside trap (x10 scale; in a match the game springs it only above 100).</summary>
        public byte OffsideTrap { get => this.offsideTrap; set { this.offsideTrap = value; this.OnPropertyChanged(nameof(this.OffsideTrap)); } }
        private byte offsideTrap;
        /// <summary>Anti-offside trap (0-5).</summary>
        public byte AntiOffsideTrap { get => this.antiOffsideTrap; set { this.antiOffsideTrap = value; this.OnPropertyChanged(nameof(this.AntiOffsideTrap)); } }
        private byte antiOffsideTrap;
        /// <summary>Pressing (0-5).</summary>
        public byte Pressing { get => this.pressing; set { this.pressing = value; this.OnPropertyChanged(nameof(this.Pressing)); } }
        private byte pressing;

        /// <summary>
        /// The packed skills (working copy +0xA0), 3 bits each: free kicks (bits 0-2), corners (3-5),
        /// throw-ins (6-8), a value the game doesn't show (9-11) and dirty tricks (12-14).
        /// </summary>
        public ushort TeamSkills { get => this.teamSkills; set { this.teamSkills = value; this.OnPropertyChanged(nameof(this.TeamSkills)); } }
        private ushort teamSkills;
        /// <summary>Free kicks (0-5), bits 0-2 of <see cref="TeamSkills"/>.</summary>
        public byte FreeKicks { get => this.Skill(0); set => this.SetSkill(0, value, nameof(this.FreeKicks)); }
        /// <summary>Corners (0-5), bits 3-5 of <see cref="TeamSkills"/>.</summary>
        public byte Corners { get => this.Skill(3); set => this.SetSkill(3, value, nameof(this.Corners)); }
        /// <summary>Throw-ins (0-5), bits 6-8 of <see cref="TeamSkills"/>.</summary>
        public byte ThrowIns { get => this.Skill(6); set => this.SetSkill(6, value, nameof(this.ThrowIns)); }
        /// <summary>Dirty tricks (0-5), bits 12-14 of <see cref="TeamSkills"/>.</summary>
        public byte DirtyTricks { get => this.Skill(12); set => this.SetSkill(12, value, nameof(this.DirtyTricks)); }

        private byte Skill(int shift) => (byte)((this.teamSkills >> shift) & 7);

        private void SetSkill(int shift, byte value, string name)
        {
            this.teamSkills = (ushort)((this.teamSkills & ~(7 << shift)) | (Math.Min(value, MaxTeamSkill) << shift));
            this.OnPropertyChanged(name);
        }
        #endregion

        #region Coaching staff
        // The co-trainer (trainer pool) and goalkeeper trainer (own table) the club employs; ID 0 = none.
        // Competence, contract and salary belong to the person's entry, not to the club.

        /// <summary>The co-trainer's ID in the trainer pool, 0 if the club has none.</summary>
        public ushort CoTrainerId { get => this.coTrainerId; set { this.coTrainerId = value; this.OnPropertyChanged(nameof(this.CoTrainerId)); this.OnPropertyChanged(nameof(this.HasCoTrainer)); } }
        private ushort coTrainerId;
        /// <summary>Whether the club employs a co-trainer.</summary>
        public bool HasCoTrainer => this.coTrainerId != 0;
        /// <summary>Longest first or last name of a co-/goalkeeper trainer the trainer accepts (as for players).</summary>
        public const int StaffNameLength = 15;

        /// <summary>The co-trainer's first name ("keiner" without one).</summary>
        public string CoTrainerFirstname { get => this.coTrainerFirstname; set { this.coTrainerFirstname = value; this.OnPropertyChanged(nameof(this.CoTrainerFirstname)); } }
        private string coTrainerFirstname = "";
        /// <summary>The co-trainer's last name.</summary>
        public string CoTrainerLastname { get => this.coTrainerLastname; set { this.coTrainerLastname = value; this.OnPropertyChanged(nameof(this.CoTrainerLastname)); } }
        private string coTrainerLastname = "";
        /// <summary>The co-trainer's competence (1-15).</summary>
        public byte CoTrainerCompetence { get => this.coTrainerCompetence; set { this.coTrainerCompetence = value; this.OnPropertyChanged(nameof(this.CoTrainerCompetence)); } }
        private byte coTrainerCompetence;
        /// <summary>The co-trainer's remaining contract years; at 0 the game ends the contract at the season change.</summary>
        public byte CoTrainerContract { get => this.coTrainerContract; set { this.coTrainerContract = value; this.OnPropertyChanged(nameof(this.CoTrainerContract)); } }
        private byte coTrainerContract;
        /// <summary>The co-trainer's salary per year (DM).</summary>
        public int CoTrainerSalary { get => this.coTrainerSalary; set { this.coTrainerSalary = value; this.OnPropertyChanged(nameof(this.CoTrainerSalary)); } }
        private int coTrainerSalary;

        /// <summary>The goalkeeper trainer's ID in the goalkeeper trainer table, 0 if the club has none.</summary>
        public ushort GoalkeeperCoachId { get => this.goalkeeperCoachId; set { this.goalkeeperCoachId = value; this.OnPropertyChanged(nameof(this.GoalkeeperCoachId)); this.OnPropertyChanged(nameof(this.HasGoalkeeperCoach)); } }
        private ushort goalkeeperCoachId;
        /// <summary>Whether the club employs a goalkeeper trainer.</summary>
        public bool HasGoalkeeperCoach => this.goalkeeperCoachId != 0;
        /// <summary>The goalkeeper trainer's first name ("keiner" without one).</summary>
        public string GoalkeeperCoachFirstname { get => this.goalkeeperCoachFirstname; set { this.goalkeeperCoachFirstname = value; this.OnPropertyChanged(nameof(this.GoalkeeperCoachFirstname)); } }
        private string goalkeeperCoachFirstname = "";
        /// <summary>The goalkeeper trainer's last name.</summary>
        public string GoalkeeperCoachLastname { get => this.goalkeeperCoachLastname; set { this.goalkeeperCoachLastname = value; this.OnPropertyChanged(nameof(this.GoalkeeperCoachLastname)); } }
        private string goalkeeperCoachLastname = "";
        /// <summary>The goalkeeper trainer's competence (1-15).</summary>
        public byte GoalkeeperCoachCompetence { get => this.goalkeeperCoachCompetence; set { this.goalkeeperCoachCompetence = value; this.OnPropertyChanged(nameof(this.GoalkeeperCoachCompetence)); } }
        private byte goalkeeperCoachCompetence;
        /// <summary>The goalkeeper trainer's remaining contract years.</summary>
        public byte GoalkeeperCoachContract { get => this.goalkeeperCoachContract; set { this.goalkeeperCoachContract = value; this.OnPropertyChanged(nameof(this.GoalkeeperCoachContract)); } }
        private byte goalkeeperCoachContract;
        /// <summary>The goalkeeper trainer's salary per year (DM).</summary>
        public int GoalkeeperCoachSalary { get => this.goalkeeperCoachSalary; set { this.goalkeeperCoachSalary = value; this.OnPropertyChanged(nameof(this.GoalkeeperCoachSalary)); } }
        private int goalkeeperCoachSalary;
        #endregion

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

        #region Kalkulation, slush fund, loans and investments (club finance block)
        /// <summary>Standing ticket price in DM (+0x272, Int16).</summary>
        public short StandingPrice { get => this.standingPrice; set { this.standingPrice = value; this.OnPropertyChanged(nameof(this.StandingPrice)); } }
        private short standingPrice;
        /// <summary>Seat ticket price in DM (+0x274, Int16); boxes and covered seats are priced from it.</summary>
        public short SeatPrice { get => this.seatPrice; set { this.seatPrice = value; this.OnPropertyChanged(nameof(this.SeatPrice)); } }
        private short seatPrice;
        /// <summary>Season ticket discount in percent (+0x270, 0..50).</summary>
        public byte SeasonTicketDiscount { get => this.seasonTicketDiscount; set { this.seasonTicketDiscount = value; this.OnPropertyChanged(nameof(this.SeasonTicketDiscount)); } }
        private byte seasonTicketDiscount;
        /// <summary>Season ticket quota in percent of the capacity (+0x271).</summary>
        public byte SeasonTicketQuota { get => this.seasonTicketQuota; set { this.seasonTicketQuota = value; this.OnPropertyChanged(nameof(this.SeasonTicketQuota)); } }
        private byte seasonTicketQuota;
        /// <summary>Spending on security in percent (+0x2AC, Int16, 1..5).</summary>
        public short Security { get => this.security; set { this.security = value; this.OnPropertyChanged(nameof(this.Security)); } }
        private short security;
        /// <summary>The law firm, 0 (cheapest) .. 4 (+0x373 current, +0x370 planned).</summary>
        public byte LawFirm { get => this.lawFirm; set { this.lawFirm = value; this.OnPropertyChanged(nameof(this.LawFirm)); } }
        private byte lawFirm;
        /// <summary>Statistics department 0..2 (+0x374 in effect, +0x371 planned): none, 20,000 or 200,000 DM.</summary>
        public byte StatisticsDepartment { get => this.statisticsDepartment; set { this.statisticsDepartment = value; this.OnPropertyChanged(nameof(this.StatisticsDepartment)); } }
        private byte statisticsDepartment;
        /// <summary>Computer department (+0x375 in effect, +0x372 planned), 200,000 DM.</summary>
        public bool HasComputerDepartment { get => this.hasComputerDepartment; set { this.hasComputerDepartment = value; this.OnPropertyChanged(nameof(this.HasComputerDepartment)); } }
        private bool hasComputerDepartment;
        /// <summary>Slush fund ("Schwarze Kasse", +0x144, Int32).</summary>
        public int SlushFund { get => this.slushFund; set { this.slushFund = value; this.OnPropertyChanged(nameof(this.SlushFund)); } }
        private int slushFund;
        /// <summary>Share of each home match's income going to the slush fund, in per mille (+0x148, 0..30).</summary>
        public short SlushFundRate { get => this.slushFundRate; set { this.slushFundRate = value; this.OnPropertyChanged(nameof(this.SlushFundRate)); } }
        private short slushFundRate;
        /// <summary>Legal fees booked this season (+0x384, Int32); 0 refunds them.</summary>
        public int LegalCosts { get => this.legalCosts; set { this.legalCosts = value; this.OnPropertyChanged(nameof(this.LegalCosts)); } }
        private int legalCosts;
        /// <summary>Overdraft limit ("Dispo", +0x39C, Int16) in units of 100,000 DM; the game sets it at the season change.</summary>
        public short OverdraftLimit { get => this.overdraftLimit; set { this.overdraftLimit = value; this.OnPropertyChanged(nameof(this.OverdraftLimit)); } }
        private short overdraftLimit;
        /// <summary>Whether the club is listed on the stock market (only then it has a share price).</summary>
        public bool IsListed { get => this.isListed; set { this.isListed = value; this.OnPropertyChanged(nameof(this.IsListed)); } }
        private bool isListed;
        /// <summary>Share price (record stock block +4, Int16); the game moves it towards its target every week.</summary>
        public short SharePrice { get => this.sharePrice; set { this.sharePrice = value; this.OnPropertyChanged(nameof(this.SharePrice)); } }
        private short sharePrice;
        /// <summary>Shares issued (record stock block +0, Int32). Read-only.</summary>
        public int IssuedShares { get => this.issuedShares; set { this.issuedShares = value; this.OnPropertyChanged(nameof(this.IssuedShares)); } }
        private int issuedShares;

        /// <summary>The club's active loans.</summary>
        public System.ComponentModel.BindingList<ClubLoan> Loans { get; } = new System.ComponentModel.BindingList<ClubLoan>();
        /// <summary>The current index of each kind of investment, shared by <see cref="Investments"/>.</summary>
        public int[] InvestmentIndices { get; set; } = new int[ClubInvestment.KindNames.Length];
        /// <summary>The club's active investments.</summary>
        public System.ComponentModel.BindingList<ClubInvestment> Investments { get; } = new System.ComponentModel.BindingList<ClubInvestment>();
        #endregion

        /// <summary>
        /// The association byte (+0x101) as stored: bits 0-4 count offences, bits 5/6 cost one stage,
        /// bit 7 belongs to the licence procedure - see <see cref="AssociationImage"/>.
        /// </summary>
        public byte Association
        {
            get => this.association;
            set { this.association = value; this.OnPropertyChanged(nameof(this.Association)); this.OnPropertyChanged(nameof(this.AssociationImage)); }
        }
        private byte association;

        /// <summary>The licence byte (+0xFF) as stored; bit 7 means the licence for the new season was refused.</summary>
        public byte Licence
        {
            get => this.licence;
            set { this.licence = value; this.OnPropertyChanged(nameof(this.Licence)); this.OnPropertyChanged(nameof(this.LicenceRefused)); }
        }
        private byte licence;

        /// <summary>Whether the licence for the new season was refused (bit 7 of <see cref="Licence"/>).</summary>
        public bool LicenceRefused
        {
            get => (this.licence & 0x80) != 0;
            set => this.Licence = (byte)(value ? this.licence | 0x80 : this.licence & 0x7F);
        }

        /// <summary>
        /// The association's requirements (+0x100), one bit each (FUN_005b5820, texts 2445-2450, 5565):
        /// see <see cref="ClubEnums.Requirement"/>. The season end sets them anew.
        /// </summary>
        public byte Requirements
        {
            get => this.requirements;
            set { this.requirements = value; this.OnPropertyChanged(nameof(this.Requirements)); }
        }
        private byte requirements;

        /// <summary>Whether the association imposes <paramref name="requirement"/>.</summary>
        public bool HasRequirement(ClubEnums.Requirement requirement) => (this.requirements & (byte)requirement) != 0;

        /// <summary>Imposes or lifts <paramref name="requirement"/>; the two transfer requirements exclude each other.</summary>
        public void SetRequirement(ClubEnums.Requirement requirement, bool on)
        {
            byte value = (byte)(on ? this.requirements | (byte)requirement : this.requirements & ~(byte)requirement);
            if (on && requirement == ClubEnums.Requirement.TransferSurplus)
                value &= unchecked((byte)~(byte)ClubEnums.Requirement.TransferLimit);
            if (on && requirement == ClubEnums.Requirement.TransferLimit)
                value &= unchecked((byte)~(byte)ClubEnums.Requirement.TransferSurplus);
            this.Requirements = value;
        }

        /// <summary>Maximum debt under the requirement (+0x494, x 1,000,000 DM).</summary>
        public byte DebtLimit { get => this.debtLimit; set { this.debtLimit = value; this.OnPropertyChanged(nameof(this.DebtLimit)); } }
        private byte debtLimit;
        /// <summary>Maximum calculated spectators under the requirement (+0x495, x 1,000).</summary>
        public byte SpectatorLimit { get => this.spectatorLimit; set { this.spectatorLimit = value; this.OnPropertyChanged(nameof(this.SpectatorLimit)); } }
        private byte spectatorLimit;
        /// <summary>Maximum salaries under the requirement (+0x496, x 100,000 DM); the game imposes it whenever it isn't 0.</summary>
        public ushort SalaryLimit { get => this.salaryLimit; set { this.salaryLimit = value; this.OnPropertyChanged(nameof(this.SalaryLimit)); } }
        private ushort salaryLimit;
        /// <summary>Transfer surplus to reach or transfer spending allowed (+0x498, x 100,000 DM).</summary>
        public short TransferLimit { get => this.transferLimit; set { this.transferLimit = value; this.OnPropertyChanged(nameof(this.TransferLimit)); } }
        private short transferLimit;

        /// <summary>The bribery byte (+0x14A): bit 7 referee bribe tried, bit 5 opponent asked, bit 4 opponent paid; the game clears them after every league match.</summary>
        public byte Bribery { get => this.bribery; set { this.bribery = value; this.OnPropertyChanged(nameof(this.Bribery)); } }
        private byte bribery;
        /// <summary>The short training camp (+0x1074): bit 0x800 booked for the next match, bits 8-9 camps this season.</summary>
        public uint ShortCamp { get => this.shortCamp; set { this.shortCamp = value; this.OnPropertyChanged(nameof(this.ShortCamp)); this.OnPropertyChanged(nameof(this.ShortCampsThisSeason)); } }
        private uint shortCamp;
        /// <summary>Short camps this season (0..2): the first adds 1 to every player, the second with a 50 % chance, from the third on a malus.</summary>
        public byte ShortCampsThisSeason
        {
            get => (byte)((this.shortCamp >> 8) & 3);
            set => this.ShortCamp = (this.shortCamp & ~0x300u) | ((uint)Math.Min(value, (byte)2) << 8);
        }
        /// <summary>Requests to the sponsor for money (+0xFC); only while it is 0 does he pay.</summary>
        public byte SponsorRequests { get => this.sponsorRequests; set { this.sponsorRequests = value; this.OnPropertyChanged(nameof(this.SponsorRequests)); } }
        private byte sponsorRequests;
        /// <summary>Spent on scouts this season (+0x2FC); from 2 Mio. DM the game refuses more.</summary>
        public int ScoutSpending { get => this.scoutSpending; set { this.scoutSpending = value; this.OnPropertyChanged(nameof(this.ScoutSpending)); } }
        private int scoutSpending;

        /// <summary>The club's image with the public (+0x10E, 10..50, FUN_005b7950 clamps) - see <see cref="PublicImageStage"/>.</summary>
        public byte PublicImage
        {
            get => this.publicImage;
            set { this.publicImage = value; this.OnPropertyChanged(nameof(this.PublicImage)); this.OnPropertyChanged(nameof(this.PublicImageStage)); }
        }
        private byte publicImage;

        // A value inside each stage (FUN_00469d90: above 44/34/24/14).
        private static readonly byte[] PublicImageStageValues = { 0, 10, 20, 30, 40, 50 };

        /// <summary>The club's image with the public as the game names it; setting it stores a value of that stage.</summary>
        public ClubEnums.PublicImage PublicImageStage
        {
            get => this.publicImage > 44 ? ClubEnums.PublicImage.SehrBeliebt : this.publicImage > 34 ? ClubEnums.PublicImage.RechtAngesehen
                : this.publicImage > 24 ? ClubEnums.PublicImage.Neutral : this.publicImage > 14 ? ClubEnums.PublicImage.WenigAngesehen : ClubEnums.PublicImage.VollkommenVerhasst;
            set => this.PublicImage = PublicImageStageValues[(int)value];
        }

        // Lowest offence count of each stage (FUN_005b5c30: below 2 -> 5, below 5 -> 4, below 9 -> 3, below 23 -> 2).
        private static readonly byte[] AssociationStageCounts = { 0, 23, 9, 5, 2, 0 };

        /// <summary>
        /// The club's standing with the association, as the game shows it (FUN_005b5c30). Setting it
        /// stores that stage's lowest offence count and clears bits 5/6; bit 7 stays.
        /// </summary>
        public ClubEnums.AssociationImage AssociationImage
        {
            get
            {
                int count = this.association & 0x1F;
                int stage = count < 2 ? 5 : count < 5 ? 4 : count < 9 ? 3 : count < 23 ? 2 : 1;
                if ((this.association & 0x60) != 0 && stage > 1)
                    stage--;
                return (ClubEnums.AssociationImage)stage;
            }
            set => this.Association = (byte)((this.association & 0x80) | AssociationStageCounts[(int)value]);
        }

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
