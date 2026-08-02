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

        /// <summary>Number of trainer slots a savegame can hold.</summary>
        public const int MaxTrainers = 4;

        /// <summary>Number of stock-market (Aktien) slots per coach.</summary>
        public const int MaxStocks = 6;
        /// <summary>The up-to-<see cref="MaxStocks"/> stock holdings for this coach.</summary>
        public Stock[] Stocks { get; } = new Stock[MaxStocks] { new Stock(), new Stock(), new Stock(), new Stock(), new Stock(), new Stock() };

        /// <summary>Coach's personal wealth/cash reserves.</summary>
        public int Wealth { get => this.wealth; set { this.wealth = value; this.OnPropertyChanged(nameof(this.Wealth)); } }
        private int wealth;

        /// <summary>Resolved memory addresses for this coach slot's fields.</summary>
        public Addresses Addresses { get => this.addresses; set { this.addresses = value; this.OnPropertyChanged(nameof(this.Addresses)); } }
        private Addresses addresses;

    }
}
