using System;
using System.Collections.Generic;
using System.Linq;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// A human trainer/manager (Mitspieler) in one of the savegame's up-to-<see cref="MaxTrainers"/> slots.
    /// </summary>
    public class Coach : Entity
    {
        /// <summary>True while the coach's fields have not yet been populated from memory.</summary>
        public bool Initilisation { get => this.initilisation; set => this.initilisation = value; }
        private bool initilisation = true;

        /// <summary>Coach's first name, truncated to the 9-byte field width the game allows.</summary>
        public string Firstname
        {
            get => this.firstname; set
            {
                if (Tools.LimitedStringEquals(value, this.firstname, 9))
                {
                    this.firstname = value != null && value.Length > 9 ? value.Substring(0, 9) : value; this.OnPropertyChanged(nameof(this.Firstname));
                }
            }
        }
        private string firstname = string.Empty;
        /// <summary>Coach's last name, truncated to the 15-byte field width the game allows.</summary>
        public string Lastname
        {
            get => this.lastname; set
            {
                if (Tools.LimitedStringEquals(value, this.lastname, 15))
                {
                    this.lastname = value != null && value.Length > 15 ? value.Substring(0, 15) : value; this.OnPropertyChanged(nameof(this.Lastname));
                }
            }
        }
        private string lastname = string.Empty;
        /// <summary>Coach's age in years.</summary>
        public byte Age { get => this.age; set { this.age = value; this.OnPropertyChanged(nameof(this.Age)); } }
        private byte age = 0;
        /// <summary>Coach's trainer level, expected to stay within <see cref="MinLevel"/>..<see cref="MaxLevel"/>.</summary>
        public byte Level { get => this.level; set { this.level = value; this.OnPropertyChanged(nameof(this.Level)); } }
        private byte level = 0;
        /// <summary>In-game difficulty setting associated with this coach.</summary>
        public CoachEnums.Difficulty Difficulty { get => this.difficulty; set { this.difficulty = value; this.OnPropertyChanged(nameof(this.Difficulty)); } }
        private CoachEnums.Difficulty difficulty = CoachEnums.Difficulty.Realistisch;

        /// <summary>Total number of matches this coach has managed.</summary>
        public ushort Games
        {
            get => this.games;
            set { this.games = value; this.OnPropertyChanged(nameof(this.Games)); this.OnPropertyChanged(nameof(this.WinPercentage)); }
        }
        private ushort games = 0;
        /// <summary>Number of matches this coach has won, out of <see cref="Games"/>.</summary>
        public ushort Wins
        {
            get => this.wins;
            set { this.wins = value; this.OnPropertyChanged(nameof(this.Wins)); this.OnPropertyChanged(nameof(this.WinPercentage)); }
        }
        private ushort wins = 0;
        /// <summary>Share of <see cref="Games"/> won, as a percentage (0 if no games have been played yet). Derived, not stored in memory.</summary>
        public double WinPercentage => this.games == 0 ? 0d : (double)this.wins / this.games * 100d;

        /// <summary>
        /// Lowest/highest valid trainer level. The game keeps a separate 6-competency-point
        /// distribution for each of these 16 levels (see <see cref="CompetencyLevels"/>) and
        /// re-applies whichever is stored if the coach is later promoted or demoted - so the UI must
        /// not allow <see cref="Level"/> outside this range even though the underlying byte field
        /// could technically hold 0-255.
        /// </summary>
        public const byte MinLevel = 0;
        /// <summary>Highest valid trainer level (see <see cref="MinLevel"/> for details).</summary>
        public const byte MaxLevel = 15;
        /// <summary>Number of valid trainer levels, i.e. <see cref="MaxLevel"/> - <see cref="MinLevel"/> + 1.</summary>
        public const int LevelCount = MaxLevel - MinLevel + 1;

        /// <summary>One entry per trainer level (index 0..MaxLevel) - see <see cref="CoachCompetencyLevel"/>.</summary>
        public CoachCompetencyLevel[] CompetencyLevels { get; } = Enumerable.Range(0, LevelCount).Select(_ => new CoachCompetencyLevel()).ToArray();

        /// <summary>The manager's image ("Image" in the game, see <see cref="CoachEnums.ManagerImage"/>).</summary>
        public CoachEnums.ManagerImage Image { get => this.image; set { this.image = value; this.OnPropertyChanged(nameof(this.Image)); } }
        private CoachEnums.ManagerImage image = CoachEnums.ManagerImage.KeinImage;
        /// <summary>Whether <see cref="Image"/> was read from the game: only builds with known fixed addresses (see FixedAddresses) have it.</summary>
        public bool HasImage { get => this.hasImage; set { this.hasImage = value; this.OnPropertyChanged(nameof(this.HasImage)); } }
        private bool hasImage;

        /// <summary>Number of trainer slots a savegame can hold.</summary>
        public const int MaxTrainers = 4;

        /// <summary>Number of stock-market (Aktien) slots per coach.</summary>
        public const int MaxStocks = 6;
        /// <summary>The up-to-<see cref="MaxStocks"/> stock holdings for this coach.</summary>
        public Stock[] Stocks { get; } = new Stock[MaxStocks] { new Stock(), new Stock(), new Stock(), new Stock(), new Stock(), new Stock() };

        /// <summary>Coach's personal wealth/cash reserves.</summary>
        public int Wealth { get => this.wealth; set { this.wealth = value; this.OnPropertyChanged(nameof(this.Wealth)); } }
        private int wealth;

        /// <summary>Country of the club this coach trains right now (read-only - see AddressPresets.COACH).</summary>
        public PlayerEnums.Country CurrentCountry { get => this.currentCountry; set { this.currentCountry = value; this.OnPropertyChanged(nameof(this.CurrentCountry)); } }
        private PlayerEnums.Country currentCountry = PlayerEnums.Country.Sonstige;
        /// <summary>ID (within <see cref="CurrentCountry"/>) of the club this coach trains right now (read-only).</summary>
        public byte CurrentClubId { get => this.currentClubId; set { this.currentClubId = value; this.OnPropertyChanged(nameof(this.CurrentClubId)); } }
        private byte currentClubId;

        /// <summary>Annual base salary ("Grundgehalt") of the current contract; the game pays a twelfth each month.</summary>
        public int Salary { get => this.salary; set { this.salary = value; this.OnPropertyChanged(nameof(this.Salary)); } }
        private int salary;
        /// <summary>Bonus per league point ("Punktprämie").</summary>
        public int PointBonus { get => this.pointBonus; set { this.pointBonus = value; this.OnPropertyChanged(nameof(this.PointBonus)); } }
        private int pointBonus;
        /// <summary>Bonus per cup round won ("Pokalrundenprämie").</summary>
        public int CupRoundBonus { get => this.cupRoundBonus; set { this.cupRoundBonus = value; this.OnPropertyChanged(nameof(this.CupRoundBonus)); } }
        private int cupRoundBonus;
        /// <summary>Bonus for winning the title or getting promoted ("Titel-/Aufstiegsprämie").</summary>
        public int TitleBonus { get => this.titleBonus; set { this.titleBonus = value; this.OnPropertyChanged(nameof(this.TitleBonus)); } }
        private int titleBonus;
        /// <summary>Bonus for reaching <see cref="SeasonGoal"/>.</summary>
        public int SeasonGoalBonus { get => this.seasonGoalBonus; set { this.seasonGoalBonus = value; this.OnPropertyChanged(nameof(this.SeasonGoalBonus)); } }
        private int seasonGoalBonus;
        /// <summary>Season goal agreed with the board.</summary>
        public CoachEnums.SeasonGoal SeasonGoal { get => this.seasonGoal; set { this.seasonGoal = value; this.OnPropertyChanged(nameof(this.SeasonGoal)); } }
        private CoachEnums.SeasonGoal seasonGoal;

        /// <summary>Years left on the current contract. Below 2 the contract counts as running out (see <see cref="CanNegotiateOffers"/>).</summary>
        public ushort ContractYearsLeft
        {
            get => this.contractYearsLeft;
            set { this.contractYearsLeft = value; this.OnPropertyChanged(nameof(this.ContractYearsLeft)); this.OnPropertyChanged(nameof(this.CanNegotiateOffers)); }
        }
        private ushort contractYearsLeft;
        /// <summary>The board's release byte as stored (+0x65; a new contract clears it) - see <see cref="HasBoardRelease"/>.</summary>
        public byte BoardRelease
        {
            get => this.boardRelease;
            set
            {
                this.boardRelease = value;
                this.OnPropertyChanged(nameof(this.BoardRelease));
                this.OnPropertyChanged(nameof(this.HasBoardRelease));
                this.OnPropertyChanged(nameof(this.CanNegotiateOffers));
            }
        }
        private byte boardRelease;
        /// <summary>Whether the board has released this coach to negotiate with other clubs. Setting it keeps a stored non-zero value.</summary>
        public bool HasBoardRelease
        {
            get => this.boardRelease != 0;
            set => this.BoardRelease = value ? (this.boardRelease != 0 ? this.boardRelease : (byte)1) : (byte)0;
        }

        /// <summary>
        /// The club's option to extend the contract by a year, as stored (the game's offers use 0-2;
        /// only zero/non-zero matters - see <see cref="HasExtensionOption"/>).
        /// </summary>
        public ushort ExtensionOption
        {
            get => this.extensionOption;
            set { this.extensionOption = value; this.OnPropertyChanged(nameof(this.ExtensionOption)); this.OnPropertyChanged(nameof(this.HasExtensionOption)); }
        }
        private ushort extensionOption;
        /// <summary>
        /// Whether the club holds the option to extend by a year. In the last contract year the club
        /// may draw it, which adds a year and uses the option up. Setting it keeps a stored non-zero value.
        /// </summary>
        public bool HasExtensionOption
        {
            get => this.extensionOption != 0;
            set => this.ExtensionOption = value ? (this.extensionOption != 0 ? this.extensionOption : (ushort)1) : (ushort)0;
        }

        // Even bits of Perks are vehicles, odd bits homes (see CoachEnums.VehiclePerk); the game
        // only uses bits 0-11, anything above is kept as it is.
        private const ushort VehiclePerkMask = 0x555;
        private const ushort HousingPerkMask = 0xAAA;

        /// <summary>Raw contract extras bitmask; edited through <see cref="Vehicle"/> and <see cref="Housing"/>.</summary>
        public ushort Perks
        {
            get => this.perks;
            set
            {
                this.perks = value;
                this.OnPropertyChanged(nameof(this.Perks));
                this.OnPropertyChanged(nameof(this.Vehicle));
                this.OnPropertyChanged(nameof(this.Housing));
            }
        }
        private ushort perks;

        /// <summary>Vehicle from the contract extras (the best one, should several bits be set).</summary>
        public CoachEnums.VehiclePerk Vehicle
        {
            get => (CoachEnums.VehiclePerk)HighestBit(this.perks & VehiclePerkMask);
            set => this.Perks = WithPerk(this.perks, VehiclePerkMask, (int)value);
        }

        /// <summary>Home from the contract extras (the best one, should several bits be set).</summary>
        public CoachEnums.HousingPerk Housing
        {
            get => (CoachEnums.HousingPerk)HighestBit(this.perks & HousingPerkMask);
            set => this.Perks = WithPerk(this.perks, HousingPerkMask, (int)value);
        }

        private static int HighestBit(int value)
        {
            int bit = -1;
            for (int i = 0; value >> i != 0; i++)
            {
                if (((value >> i) & 1) != 0)
                    bit = i;
            }
            return bit;
        }

        private static ushort WithPerk(ushort perks, ushort categoryMask, int bit)
        {
            int cleared = perks & ~categoryMask;
            return (ushort)(bit >= 0 ? cleared | (1 << bit) : cleared);
        }

        /// <summary>
        /// Whether the coach could negotiate an offer from another club right now: a release clause,
        /// the board's release, or a contract that's running out (less than two years left) - the
        /// game's own check (FUN_004a0be0). Without any of these an offer shows up, but can't be accepted.
        /// </summary>
        public bool CanNegotiateOffers => this.HasReleaseClause || this.HasBoardRelease || this.ContractYearsLeft < 2;

        /// <summary>Country of the club this coach will train next season.</summary>
        public PlayerEnums.Country CountryNextSeason { get => this.countryNextSeason; set { this.countryNextSeason = value; this.OnPropertyChanged(nameof(this.CountryNextSeason)); } }
        private PlayerEnums.Country countryNextSeason = PlayerEnums.Country.Sonstige;
        /// <summary>ID (within <see cref="CountryNextSeason"/>) of the club this coach will train next season.</summary>
        public byte ClubIdNextSeason { get => this.clubIdNextSeason; set { this.clubIdNextSeason = value; this.OnPropertyChanged(nameof(this.ClubIdNextSeason)); } }
        private byte clubIdNextSeason;
        /// <summary>Whether this coach's contract with the currently trained club includes a release clause ("Freigabeklausel"), and whether its terms have been made public.</summary>
        public byte ReleaseClause
        {
            get => this.releaseClause;
            set
            {
                this.releaseClause = value;
                this.OnPropertyChanged(nameof(this.ReleaseClause));
                this.OnPropertyChanged(nameof(this.HasReleaseClause));
                this.OnPropertyChanged(nameof(this.IsReleaseClausePublished));
            }
        }
        private byte releaseClause;

        // Bits of ReleaseClause, from the original build's code: bit 0 is the clause itself (lets
        // the trainer negotiate with other clubs despite a running contract, FUN_004a0be0); bit 7
        // means it was made public via "Freigabeklausel bekanntgeben", which makes the game generate
        // job offers about ten times as often (FUN_006262c0). Any other bits are kept as they are.
        private const byte ReleaseClauseBit = 0x01;
        private const byte ReleaseClausePublishedBit = 0x80;

        /// <summary>Whether this coach's current contract has a release clause ("Freigabeklausel"). Clearing it also withdraws the announcement.</summary>
        public bool HasReleaseClause
        {
            get => (this.releaseClause & ReleaseClauseBit) != 0;
            set => this.ReleaseClause = value
                ? (byte)(this.releaseClause | ReleaseClauseBit)
                : (byte)(this.releaseClause & ~(ReleaseClauseBit | ReleaseClausePublishedBit));
        }

        /// <summary>Whether the release clause has been made public ("bekanntgegeben"). Setting it also sets <see cref="HasReleaseClause"/>, since the game only announces an existing clause.</summary>
        public bool IsReleaseClausePublished
        {
            get => (this.releaseClause & ReleaseClausePublishedBit) != 0;
            set => this.ReleaseClause = value
                ? (byte)(this.releaseClause | ReleaseClauseBit | ReleaseClausePublishedBit)
                : (byte)(this.releaseClause & ~ReleaseClausePublishedBit);
        }

        /// <summary>Resolved memory addresses for this coach slot's fields.</summary>
        public Addresses Addresses { get => this.addresses; set { this.addresses = value; this.OnPropertyChanged(nameof(this.Addresses)); } }
        private Addresses addresses;

    }
}
