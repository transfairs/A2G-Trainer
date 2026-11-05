using System.ComponentModel;

namespace A2G_Trainer.Model
{
    /// <summary>
    /// One active loan of a club: a slot of 0x14 bytes at club +0x15C (8 slots). +0 amount, +4/+6/+8
    /// the rates of the three terms in basis points, +0xA the chosen term, +0xB bit 0 active, bit 1
    /// repayable early. The game bills interest on the amount every quarter at the chosen term's rate.
    /// A new one is filled like the game's own (FUN_00550c80).
    /// </summary>
    public class ClubLoan : INotifyPropertyChanged
    {
        /// <summary>Raised whenever amount, term, rate or repayability change.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>Years of the three terms (table 0x6BCB28).</summary>
        public static readonly int[] TermYears = { 1, 2, 5 };

        /// <summary>The loan's slot (0..7); -1 until saved for a new one.</summary>
        public int Slot { get; set; } = -1;
        /// <summary>Amount borrowed (+0, Int32).</summary>
        public int Amount { get => this.amount; set { this.amount = value; this.OnPropertyChanged(nameof(this.Amount)); } }
        private int amount;
        /// <summary>The chosen term 0..2 (+0xA).</summary>
        public byte Term { get => this.term; set { this.term = (byte)System.Math.Min((int)value, TermYears.Length - 1); this.OnPropertyChanged(nameof(this.Term)); this.OnPropertyChanged(nameof(this.Rate)); } }
        private byte term;
        /// <summary>The rate of each term in basis points (+4, +6, +8); the game keeps all three for an early repayment.</summary>
        public short[] Rates { get; } = new short[3];
        /// <summary>Interest rate of the chosen term in percent; setting it changes that term's rate.</summary>
        public decimal Rate
        {
            get => this.Rates[this.term] / 100m;
            set { this.Rates[this.term] = (short)System.Math.Round(System.Math.Max(short.MinValue / 100m, System.Math.Min(short.MaxValue / 100m, value)) * 100m); this.OnPropertyChanged(nameof(this.Rate)); }
        }
        /// <summary>Whether the loan can be paid back early (+0xB bit 1).</summary>
        public bool Cancellable { get => this.cancellable; set { this.cancellable = value; this.OnPropertyChanged(nameof(this.Cancellable)); } }
        private bool cancellable;

        /// <summary>
        /// A new loan like the game's offer: base rate R (basis points) for 1/2/5 years is R, R-25,
        /// R-50 when repayable early, else R-150, R-175, R-200.
        /// </summary>
        public static ClubLoan New(int amount, byte term, bool cancellable, short baseRate)
        {
            ClubLoan loan = new ClubLoan { amount = amount, term = term, cancellable = cancellable };
            short discount = (short)(cancellable ? 0 : 150);
            for (int i = 0; i < 3; i++)
                loan.Rates[i] = (short)(baseRate - discount - i * 25);
            return loan;
        }
    }

    /// <summary>
    /// One active investment of a club: a slot of 0x10 bytes at club +0x1FC (6 slots). Its value is
    /// base * current index of its kind / index at purchase (FUN_0058c2e0). A new one is filled like
    /// the game's own (code at 0x4495A0): principal and base the amount, index at purchase the kind's
    /// current index.
    /// </summary>
    public class ClubInvestment : INotifyPropertyChanged
    {
        /// <summary>Raised whenever kind, principal or value change.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>The investment's slot (0..5); -1 until saved for a new one.</summary>
        public int Slot { get; set; } = -1;
        /// <summary>The current index of every kind (shared by the club's investments, see Settings.InvestmentIndexOffset).</summary>
        public int[] KindIndices { get; set; } = new int[KindNames.Length];

        /// <summary>
        /// Kind 0..4 (+0xA): Tagesgeld and the four investment advisers (texts 5558-5562). Changing it
        /// keeps the current value: the investment counts as bought now at the new kind's index.
        /// </summary>
        public byte Kind
        {
            get => this.kind;
            set
            {
                int value0 = this.Value;
                this.kind = value;
                this.IndexAtPurchase = (short)this.CurrentIndex;
                this.Base = value0;
                this.OnPropertyChanged(nameof(this.Kind));
            }
        }
        private byte kind;
        /// <summary>Name of the kind, as the game shows it.</summary>
        public string KindName => this.kind < KindNames.Length ? KindNames[this.kind] : $"Art {this.kind}";
        /// <summary>Amount paid in (+0, Int32); selling books value minus this as gain or loss.</summary>
        public int Principal { get => this.principal; set { this.principal = value; this.OnPropertyChanged(nameof(this.Principal)); } }
        private int principal;
        /// <summary>Base value (+4, Int32) the current value is scaled from.</summary>
        public int Base { get => this.baseValue; set { this.baseValue = value; this.OnPropertyChanged(nameof(this.Value)); } }
        private int baseValue;
        /// <summary>The kind's index when bought (+8, Int16, the game keeps the low word).</summary>
        public short IndexAtPurchase { get; set; }
        /// <summary>The kind's index now.</summary>
        public int CurrentIndex => this.kind < this.KindIndices.Length ? this.KindIndices[this.kind] : 0;

        /// <summary>Current value as the game computes it; setting it adjusts the base value.</summary>
        public int Value
        {
            get => this.IndexAtPurchase == 0 ? this.baseValue : (int)System.Math.Max(int.MinValue, System.Math.Min(int.MaxValue, (long)this.baseValue * this.CurrentIndex / this.IndexAtPurchase));
            set => this.Base = this.IndexAtPurchase == 0 || this.CurrentIndex == 0 ? value : (int)System.Math.Max(int.MinValue, System.Math.Min(int.MaxValue, (long)value * this.IndexAtPurchase / this.CurrentIndex));
        }

        /// <summary>A new investment of the given kind and amount, bought at the kind's current index.</summary>
        public static ClubInvestment New(byte kind, int amount, int[] kindIndices)
        {
            ClubInvestment investment = new ClubInvestment { KindIndices = kindIndices, principal = amount };
            investment.kind = kind;
            investment.IndexAtPurchase = (short)investment.CurrentIndex;
            investment.baseValue = amount;
            return investment;
        }

        /// <summary>The five kinds of investment (texts 5558-5562).</summary>
        public static readonly string[] KindNames = { "Tagesgeld", "Kostol & Any", "Windig & Schnellweg", "Dipl. Kfm. Sorglos", "Mme Véronique" };
    }

    /// <summary>
    /// One law firm's fees per case in DM. The game keeps them in thousands of DM (table 0x6C1F10,
    /// Int16), so a fee is rounded to whole thousands, at most 32,767,000 DM.
    /// </summary>
    public class LawFirmFee : INotifyPropertyChanged
    {
        /// <summary>Raised whenever a fee changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The game's unit of the fees.</summary>
        public const int Unit = 1000;

        /// <summary>The firm's name (from the game's dialogs).</summary>
        [DisplayName("Kanzlei"), ReadOnly(true)]
        public string Name { get; set; }
        /// <summary>Fee per red card case in DM.</summary>
        [DisplayName("Rote Karte (DM)")]
        public int RedCard { get => this.redCard; set { this.redCard = GameCosts.InUnits(value, Unit); this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.RedCard))); } }
        private int redCard;
        /// <summary>Fee per other case in DM.</summary>
        [DisplayName("Sonstiges (DM)")]
        public int Other { get => this.other; set { this.other = GameCosts.InUnits(value, Unit); this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Other))); } }
        private int other;
    }

    /// <summary>
    /// What the game charges every club (static tables in its writable data section, so they apply
    /// until the game is restarted): law firm fees, the statistics and computer departments, and the
    /// current index of each kind of investment. Costs are in DM, rounded to the game's units.
    /// </summary>
    public class GameCosts : INotifyPropertyChanged
    {
        /// <summary>Raised whenever a department cost changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>The game's unit of the statistics department's cost.</summary>
        public const int StatisticsUnit = 10000;
        /// <summary>The game's unit of the computer department's cost.</summary>
        public const int ComputerUnit = 100000;

        /// <summary>
        /// An amount in DM as the game can keep it: a whole number (Int16) of units, rounded to the
        /// nearest, 0 at least and at most what still fits Int16 units and an Int32 in DM.
        /// </summary>
        public static int InUnits(int amount, int unit) =>
            (int)System.Math.Max(0, System.Math.Min(System.Math.Min(short.MaxValue, int.MaxValue / unit), System.Math.Round((double)amount / unit, System.MidpointRounding.AwayFromZero))) * unit;

        /// <summary>The five law firms, cheapest first.</summary>
        public BindingList<LawFirmFee> LawFirms { get; } = new BindingList<LawFirmFee>();
        /// <summary>Season cost of the small statistics department in DM (table 0x6C1EE2, units of 10,000).</summary>
        public int SmallStatistics { get => this.smallStatistics; set { this.smallStatistics = InUnits(value, StatisticsUnit); this.OnPropertyChanged(nameof(this.SmallStatistics)); } }
        private int smallStatistics;
        /// <summary>Season cost of the large statistics department in DM (0x6C1EE4, units of 10,000).</summary>
        public int LargeStatistics { get => this.largeStatistics; set { this.largeStatistics = InUnits(value, StatisticsUnit); this.OnPropertyChanged(nameof(this.LargeStatistics)); } }
        private int largeStatistics;
        /// <summary>Season cost of the computer department in DM (0x6C1EEA, units of 100,000).</summary>
        public int Computer { get => this.computer; set { this.computer = InUnits(value, ComputerUnit); this.OnPropertyChanged(nameof(this.Computer)); } }
        private int computer;
        /// <summary>The current index of each kind of investment (start 1000 each season).</summary>
        public BindingList<InvestmentIndex> Indices { get; } = new BindingList<InvestmentIndex>();
    }

    /// <summary>The current index of one kind of investment (Int32, 0x826710 + kind * 4).</summary>
    public class InvestmentIndex : INotifyPropertyChanged
    {
        /// <summary>Raised whenever the index changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>The kind's name.</summary>
        [DisplayName("Anbieter"), ReadOnly(true)]
        public string Name { get; set; }
        /// <summary>The index; investments are worth base * index / index at purchase.</summary>
        [DisplayName("Index (Start 1000)")]
        public int Value { get => this.value; set { this.value = value; this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(this.Value))); } }
        private int value;
    }

    /// <summary>
    /// The savegame-wide interest rates (one set for every club, in basis points in the game,
    /// shown here in percent) and the corporation tax. The game starts them at the career start and
    /// moves the rates by up to 0.25 points each quarter (FUN_0058bfe0).
    /// </summary>
    public class InterestRates : INotifyPropertyChanged
    {
        /// <summary>Raised whenever a rate changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void Set(ref decimal field, decimal value, string name) { field = value; this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name)); }

        /// <summary>Interest on a positive balance (0x826706).</summary>
        public decimal Deposit { get => this.deposit; set => this.Set(ref this.deposit, value, nameof(this.Deposit)); }
        private decimal deposit;
        /// <summary>Interest on an overdraft within the limit ("Dispo", 0x826708).</summary>
        public decimal Overdraft { get => this.overdraft; set => this.Set(ref this.overdraft, value, nameof(this.Overdraft)); }
        private decimal overdraft;
        /// <summary>Interest beyond the overdraft limit (0x826704).</summary>
        public decimal OverLimit { get => this.overLimit; set => this.Set(ref this.overLimit, value, nameof(this.OverLimit)); }
        private decimal overLimit;
        /// <summary>Base rate of new loans (0x82670A); the offers are below it by term and repayability.</summary>
        public decimal Loan { get => this.loan; set => this.Set(ref this.loan, value, nameof(this.Loan)); }
        private decimal loan;
        /// <summary>Interest on Tagesgeld investments (0x82670E).</summary>
        public decimal CallMoney { get => this.callMoney; set => this.Set(ref this.callMoney, value, nameof(this.CallMoney)); }
        private decimal callMoney;
        /// <summary>Corporation tax in percent (0x827BB1); there is no VAT rate, the game takes a fixed 87 % of ticket income.</summary>
        public decimal CorporationTax { get => this.corporationTax; set => this.Set(ref this.corporationTax, value, nameof(this.CorporationTax)); }
        private decimal corporationTax;
    }
}
