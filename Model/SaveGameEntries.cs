using A2G_Trainer.Controller;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using A2G_Trainer.View;
using System.Linq;

namespace A2G_Trainer.Model
{
    /// <summary>
    /// Base of the objects the savegame editor shows: each one reads and writes its fields straight
    /// in an opened <see cref="SaveGameFile"/>, so there is nothing to apply - a change is in the
    /// file image as soon as the property is set, and on disk once the file is saved.
    /// </summary>
    public abstract class SaveGameEntry : INotifyPropertyChanged
    {
        internal SaveGameEntry(SaveGameFile file, uint address)
        {
            this.File = file;
            this.Address = address;
        }

        /// <summary>Raised after a field was written.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        internal SaveGameFile File { get; }
        /// <summary>Original-build address of the record the entry reads.</summary>
        [Browsable(false)]
        public uint Address { get; }

        /// <summary>Raises <see cref="PropertyChanged"/>.</summary>
        protected void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        internal byte B(uint offset) => this.File.ReadByte(this.Address + offset);
        internal ushort U16(uint offset) => this.File.ReadUInt16(this.Address + offset);
        internal short I16(uint offset) => this.File.ReadInt16(this.Address + offset);
        internal int I32(uint offset) => this.File.ReadInt32(this.Address + offset);
        internal uint U32(uint offset) => this.File.ReadUInt32(this.Address + offset);
        internal string Text(uint offset, int length) => this.File.ReadString(this.Address + offset, length);

        internal void SetB(uint offset, byte value, string name) { this.File.WriteByte(this.Address + offset, value); this.OnPropertyChanged(name); }
        internal void SetU16(uint offset, ushort value, string name) { this.File.WriteUInt16(this.Address + offset, value); this.OnPropertyChanged(name); }
        internal void SetI16(uint offset, short value, string name) { this.File.WriteInt16(this.Address + offset, value); this.OnPropertyChanged(name); }
        internal void SetI32(uint offset, int value, string name) { this.File.WriteInt32(this.Address + offset, value); this.OnPropertyChanged(name); }
        internal void SetU32(uint offset, uint value, string name) { this.File.WriteUInt32(this.Address + offset, value); this.OnPropertyChanged(name); }

        // A text field of maxLength characters plus its NUL.
        internal void SetText(uint offset, string value, int maxLength, string name)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length > maxLength)
                throw new ArgumentException($"Höchstens {maxLength} Zeichen.");
            this.File.WriteString(this.Address + offset, value, maxLength + 1);
            this.OnPropertyChanged(name);
        }

        internal static void Check(int value, int min, int max)
        {
            if (value < min || value > max)
                throw new ArgumentException($"Erlaubt sind Werte von {min} bis {max}.");
        }
    }

    /// <summary>
    /// The savegame's calendar (see Settings.StartYearOffset), with the game's conversions for
    /// injury end dates and vulnerability, the same as <see cref="GameCalendar"/> does in memory.
    /// </summary>
    internal sealed class SaveGameCalendar
    {
        private readonly SaveGameFile file;

        /// <summary>Reads the calendar values from the given savegame file.</summary>
        public SaveGameCalendar(SaveGameFile file)
        {
            this.file = file;
            this.Year = file.ReadInt16(SaveGameFile.ModuleBase + Settings.AgeReferenceYearOffset);
            this.StartYear = file.ReadInt16(SaveGameFile.ModuleBase + Settings.StartYearOffset);
            this.DayCounter = file.ReadInt16(SaveGameFile.ModuleBase + Settings.DayCounterOffset);
            this.DaysPerYear = file.ReadInt16(SaveGameFile.ModuleBase + Settings.DaysPerYearOffset);
            this.GlobalCounter = file.ReadInt16(SaveGameFile.ModuleBase + Settings.GlobalCounterOffset);
        }

        /// <summary>The savegame's current in-game year (Settings.AgeReferenceYearOffset), the reference for player ages.</summary>
        public short Year { get; }
        /// <summary>The current calendar year (Settings.StartYearOffset; the game counts it up on 1 January).</summary>
        public short StartYear { get; }
        /// <summary>Days since 1 January of <see cref="StartYear"/> - "today".</summary>
        public short DayCounter { get; }
        /// <summary>Days per year the game uses when turning an end date back into a number of days (365).</summary>
        public short DaysPerYear { get; }
        /// <summary>The counter injury starts and vulnerability end values are stored against.</summary>
        public short GlobalCounter { get; }

        /// <summary>Year and day of year of the given day counter value, or null outside the game's calendar table.</summary>
        public Tuple<short, short> DateOf(int dayCounter)
        {
            uint address = SaveGameFile.ModuleBase + Settings.CalendarTableOffset + (uint)dayCounter * 4;
            if (dayCounter < 0 || !this.file.Contains(address, 4))
                return null;
            short year = this.file.ReadInt16(address);
            return year == 0 ? null : Tuple.Create(year, this.file.ReadInt16(address + 2));
        }

        /// <summary>Today as a date, or null if the calendar table doesn't have it.</summary>
        public DateTime? Today
        {
            get
            {
                Tuple<short, short> date = this.DateOf(this.DayCounter);
                if (date == null || date.Item1 < 1 || date.Item2 < 0 || date.Item2 > 365)
                    return null;
                return new DateTime(date.Item1, 1, 1).AddDays(date.Item2);
            }
        }

        /// <summary>Remaining injury days for a stored end date, exactly as the game unpacks it (never negative).</summary>
        public ushort InjuredDaysFromEndDate(short endYear, short endDayOfYear)
        {
            if (endYear < this.StartYear)
                return 0;
            int days = (endYear - this.StartYear) * this.DaysPerYear + endDayOfYear - this.DayCounter;
            return days < 0 ? (ushort)0 : (ushort)days;
        }

        /// <summary>Remaining vulnerability for a stored end value (0 = not vulnerable), as the game unpacks it into its one-byte field.</summary>
        public byte VulnerableTicksFromEndValue(ushort endValue) => endValue == 0 ? (byte)0 : (byte)(endValue - this.GlobalCounter);
        /// <summary>The stored end value for <paramref name="ticks"/> more vulnerability ticks (0 = not vulnerable).</summary>
        public ushort VulnerableEndValueForTicks(byte ticks) => ticks == 0 ? (ushort)0 : (ushort)(this.GlobalCounter + ticks);
    }

    /// <summary>A player's permanent record (Settings.PlayerRecordTableOffset, 0xC8 bytes) in a savegame file.</summary>
    [DefaultProperty(nameof(Lastname))]
    public sealed class SaveGamePlayer : SaveGameEntry
    {
        /// <summary>Number of player records in a savegame.</summary>
        public const int RecordCount = 15006;
        /// <summary>Longest first name (the game's display copy has room for 9 characters).</summary>
        public const int FirstnameLength = 9;
        /// <summary>Longest last name (the game's display copy has room for 15 characters).</summary>
        public const int LastnameLength = 15;

        private const byte DopingBits = 0x0F;
        private const byte DopingSetValue = 0x0C;

        private readonly SaveGameCalendar calendar;

        internal SaveGamePlayer(SaveGameFile file, SaveGameCalendar calendar, ushort id)
            : base(file, AddressOf(id))
        {
            this.calendar = calendar;
            this.Id = id;
        }

        internal static uint AddressOf(ushort id) => SaveGameFile.ModuleBase + Settings.PlayerRecordTableOffset + id * Settings.PlayerRecordStride;

        /// <summary>The player's ID (record number).</summary>
        [SaveGameCategory("Allgemein"), DisplayName("ID"), ReadOnly(true)]
        public ushort Id { get; }

        /// <summary>True if the record holds a player (any name set).</summary>
        [Browsable(false)]
        public bool IsUsed => this.U16(0) != 0 || this.U16(2) != 0;

        /// <inheritdoc/>
        public override string ToString() => $"{this.Firstname} {this.Lastname}".Trim();

        #region Allgemein
        /// <summary>First name, from the savegame's name pool.</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Vorname")]
        public string Firstname { get => this.File.GetName(this.U16(0)); set => this.SetName(0, value, FirstnameLength, nameof(this.Firstname)); }

        /// <summary>Last name, from the savegame's name pool.</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Nachname")]
        public string Lastname { get => this.File.GetName(this.U16(2)); set => this.SetName(2, value, LastnameLength, nameof(this.Lastname)); }

        // Names are shared pool entries: only this player's index is changed, to an existing entry
        // with the new name or one appended to the pool, so nobody else is renamed.
        private void SetName(uint offset, string value, int maxLength, string name)
        {
            value = (value ?? string.Empty).Trim();
            if (value.Length == 0)
                throw new ArgumentException("Der Name darf nicht leer sein.");
            if (value.Length > maxLength)
                throw new ArgumentException($"Höchstens {maxLength} Zeichen.");
            int index = this.File.AddName(value);
            if (index < 0)
                throw new ArgumentException("Der Namensbestand des Spielstands ist voll.");
            this.SetU16(offset, (ushort)index, name);
        }

        /// <summary>Age; the record keeps the birth year (Settings.PlayerRecordAgeOffset).</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Alter")]
        public byte Age
        {
            get => (byte)(this.calendar.Year - this.U16(Settings.PlayerRecordAgeOffset));
            set { Check(value, 1, 99); this.SetU16(Settings.PlayerRecordAgeOffset, (ushort)(this.calendar.Year - value), nameof(this.Age)); }
        }

        /// <summary>Strength ("Stärke").</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Stärke")]
        public byte Level { get => this.B(Settings.PlayerRecordLevelOffset); set => this.SetB(Settings.PlayerRecordLevelOffset, value, nameof(this.Level)); }

        /// <summary>Main position.</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Position"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Position Position { get => (PlayerEnums.Position)this.B(Settings.PlayerRecordPositionOffset); set => this.SetB(Settings.PlayerRecordPositionOffset, (byte)value, nameof(this.Position)); }

        /// <summary>First secondary position.</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Nebenposition 1"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Position SecondaryPosition1 { get => (PlayerEnums.Position)this.B(Settings.PlayerRecordPositionOffset + 1); set => this.SetB(Settings.PlayerRecordPositionOffset + 1, (byte)value, nameof(this.SecondaryPosition1)); }

        /// <summary>Second secondary position.</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Nebenposition 2"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Position SecondaryPosition2 { get => (PlayerEnums.Position)this.B(Settings.PlayerRecordPositionOffset + 2); set => this.SetB(Settings.PlayerRecordPositionOffset + 2, (byte)value, nameof(this.SecondaryPosition2)); }

        /// <summary>Nationality: the low 7 bits; bit 7 is kept as it is.</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Nationalität"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Country Nationality
        {
            get => (PlayerEnums.Country)(this.B(Settings.PlayerRecordNationalityOffset) & 0x7F);
            set => this.SetB(Settings.PlayerRecordNationalityOffset, (byte)((this.B(Settings.PlayerRecordNationalityOffset) & 0x80) | ((byte)value & 0x7F)), nameof(this.Nationality));
        }

        /// <summary>Shirt number (1-99, 0 = none yet).</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Rückennummer")]
        public byte Jersey { get => this.B(Settings.PlayerRecordJerseyOffset); set { Check(value, 0, 99); this.SetB(Settings.PlayerRecordJerseyOffset, value, nameof(this.Jersey)); } }

        /// <summary>Skin colour.</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Hautfarbe"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.SkinColor SkinColor { get => (PlayerEnums.SkinColor)this.B(Settings.PlayerRecordSkinColorOffset); set => this.SetB(Settings.PlayerRecordSkinColorOffset, (byte)value, nameof(this.SkinColor)); }

        /// <summary>Hair colour.</summary>
        [SaveGameCategory("Allgemein"), DisplayName("Haarfarbe"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.HairColor HairColor { get => (PlayerEnums.HairColor)this.B(Settings.PlayerRecordSkinColorOffset + 1); set => this.SetB(Settings.PlayerRecordSkinColorOffset + 1, (byte)value, nameof(this.HairColor)); }
        #endregion

        #region Fähigkeiten
        /// <summary>Skills (bit mask).</summary>
        [SaveGameCategory("Fähigkeiten"), DisplayName("Fähigkeiten"), TypeConverter(typeof(FlagsEnumConverter)), Editor(typeof(FlagsEnumEditor), typeof(UITypeEditor))]
        public PlayerEnums.Skills Skills { get => (PlayerEnums.Skills)this.U16(Settings.PlayerRecordSkillsOffset); set => this.SetU16(Settings.PlayerRecordSkillsOffset, (ushort)value, nameof(this.Skills)); }

        /// <summary>Negative skills (bit mask).</summary>
        [SaveGameCategory("Fähigkeiten"), DisplayName("Schwächen"), TypeConverter(typeof(FlagsEnumConverter)), Editor(typeof(FlagsEnumEditor), typeof(UITypeEditor))]
        public PlayerEnums.Skills NegativeSkills { get => (PlayerEnums.Skills)this.U16(Settings.PlayerRecordSkillsOffset + 2); set => this.SetU16(Settings.PlayerRecordSkillsOffset + 2, (ushort)value, nameof(this.NegativeSkills)); }

        /// <summary>Personality traits (bit mask).</summary>
        [SaveGameCategory("Fähigkeiten"), DisplayName("Eigenschaften"), TypeConverter(typeof(FlagsEnumConverter)), Editor(typeof(FlagsEnumEditor), typeof(UITypeEditor))]
        public PlayerEnums.Personality Personality { get => (PlayerEnums.Personality)this.U16(Settings.PlayerRecordPersonalityOffset); set => this.SetU16(Settings.PlayerRecordPersonalityOffset, (ushort)value, nameof(this.Personality)); }

        /// <summary>Character.</summary>
        [SaveGameCategory("Fähigkeiten"), DisplayName("Charakter"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Character Character { get => (PlayerEnums.Character)this.B(Settings.PlayerRecordCharacterOffset); set => this.SetB(Settings.PlayerRecordCharacterOffset, (byte)value, nameof(this.Character)); }

        /// <summary>Health ("Gesundheitszustand").</summary>
        [SaveGameCategory("Fähigkeiten"), DisplayName("Gesundheitszustand"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Health Health { get => (PlayerEnums.Health)this.B(Settings.PlayerRecordHealthOffset); set => this.SetB(Settings.PlayerRecordHealthOffset, (byte)value, nameof(this.Health)); }
        #endregion

        #region Verfassung
        /// <summary>Form.</summary>
        [SaveGameCategory("Verfassung"), DisplayName("Form")]
        public byte Form { get => this.B(Settings.PlayerRecordFormOffset); set => this.SetB(Settings.PlayerRecordFormOffset, value, nameof(this.Form)); }

        /// <summary>Condition ("Kondition").</summary>
        [SaveGameCategory("Verfassung"), DisplayName("Kondition")]
        public byte Condition { get => this.B(Settings.PlayerRecordConditionOffset); set => this.SetB(Settings.PlayerRecordConditionOffset, value, nameof(this.Condition)); }

        /// <summary>Freshness ("Frische").</summary>
        [SaveGameCategory("Verfassung"), DisplayName("Frische")]
        public byte Freshness { get => this.B(Settings.PlayerRecordFreshnessOffset); set => this.SetB(Settings.PlayerRecordFreshnessOffset, value, nameof(this.Freshness)); }

        /// <summary>Kind of injury or illness, as the game names it.</summary>
        [SaveGameCategory("Verfassung"), DisplayName("Verletzung"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Injury Injury { get => (PlayerEnums.Injury)this.B(Settings.PlayerRecordInjuryOffset); set => this.SetB(Settings.PlayerRecordInjuryOffset, (byte)value, nameof(this.Injury)); }

        /// <summary>Remaining injury days; stored as the end date, packed as the game does it (FUN_00583120).</summary>
        [SaveGameCategory("Verfassung"), DisplayName("Verletzt (Tage)")]
        public ushort InjuredDays
        {
            get => this.calendar.InjuredDaysFromEndDate(this.I16(Settings.PlayerRecordInjuryEndYearOffset), this.I16(Settings.PlayerRecordInjuryEndYearOffset + 2));
            set
            {
                if (value == 0)
                {
                    this.File.Write(this.Address + Settings.PlayerRecordInjuryStartOffset, new byte[6]);
                    this.File.WriteByte(this.Address + Settings.PlayerRecordInjuryClearedFlagOffset, 0);
                }
                else
                {
                    Tuple<short, short> end = this.calendar.DateOf(this.calendar.DayCounter + value);
                    if (end == null)
                        throw new ArgumentException("So viele Tage reicht der Kalender des Spielstands nicht.");
                    if (this.U16(Settings.PlayerRecordInjuryStartOffset) == 0)
                        this.File.WriteUInt16(this.Address + Settings.PlayerRecordInjuryStartOffset, (ushort)this.calendar.GlobalCounter);
                    this.File.WriteInt16(this.Address + Settings.PlayerRecordInjuryEndYearOffset, end.Item1);
                    this.File.WriteInt16(this.Address + Settings.PlayerRecordInjuryEndYearOffset + 2, end.Item2);
                }
                this.OnPropertyChanged(nameof(this.InjuredDays));
            }
        }

        /// <summary>Remaining vulnerability ticks ("verletzungsanfällig"), stored as the counter value it ends at.</summary>
        [SaveGameCategory("Verfassung"), DisplayName("Verletzungsanfällig")]
        public byte VulnerableTicks
        {
            get => this.calendar.VulnerableTicksFromEndValue(this.U16(Settings.PlayerRecordVulnerableOffset));
            set => this.SetU16(Settings.PlayerRecordVulnerableOffset, this.calendar.VulnerableEndValueForTicks(value), nameof(this.VulnerableTicks));
        }

        /// <summary>Matches still banned for a red card.</summary>
        [SaveGameCategory("Verfassung"), DisplayName("Gesperrt (Spiele)")]
        public byte RedCardBannedMatches { get => this.B(Settings.PlayerRecordBannedOffset); set => this.SetB(Settings.PlayerRecordBannedOffset, value, nameof(this.RedCardBannedMatches)); }

        /// <summary>Doped: bits 0-3 of the doping byte (the game sets 0x0C and counts down), the other bits stay.</summary>
        [SaveGameCategory("Verfassung"), DisplayName("Gedopt"), TypeConverter(typeof(YesNoConverter))]
        public bool Doped
        {
            get => (this.B(Settings.PlayerRecordBannedOffset + 1) & DopingBits) != 0;
            set
            {
                byte state = this.B(Settings.PlayerRecordBannedOffset + 1);
                byte written = !value ? (byte)(state & ~DopingBits) : (state & DopingBits) != 0 ? state : (byte)(state | DopingSetValue);
                this.SetB(Settings.PlayerRecordBannedOffset + 1, written, nameof(this.Doped));
            }
        }

        /// <summary>Yellow cards this season.</summary>
        [SaveGameCategory("Verfassung"), DisplayName("Gelbe Karten")]
        public byte YellowCardsSeason { get => this.B(Settings.PlayerRecordBannedOffset + 2); set => this.SetB(Settings.PlayerRecordBannedOffset + 2, value, nameof(this.YellowCardsSeason)); }
        #endregion

        #region Stimmung
        /// <summary>Reasons for being unhappy (bit mask).</summary>
        [SaveGameCategory("Stimmung"), DisplayName("Unzufrieden"), TypeConverter(typeof(FlagsEnumConverter)), Editor(typeof(FlagsEnumEditor), typeof(UITypeEditor))]
        public PlayerEnums.Unhappy Unhappy { get => (PlayerEnums.Unhappy)this.U16(Settings.PlayerRecordUnhappyOffset); set => this.SetU16(Settings.PlayerRecordUnhappyOffset, (ushort)value, nameof(this.Unhappy)); }

        /// <summary>Reason for being happy.</summary>
        [SaveGameCategory("Stimmung"), DisplayName("Zufrieden"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Happy Happy { get => (PlayerEnums.Happy)this.B(Settings.PlayerRecordUnhappyOffset + 2); set => this.SetB(Settings.PlayerRecordUnhappyOffset + 2, (byte)value, nameof(this.Happy)); }
        #endregion

        #region Vertrag
        /// <summary>Salary.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Gehalt")]
        public ushort Salary { get => this.U16(Settings.PlayerRecordSalaryOffset); set => this.SetU16(Settings.PlayerRecordSalaryOffset, value, nameof(this.Salary)); }

        /// <summary>Appearance bonus ("Auflaufprämie").</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Auflaufprämie")]
        public ushort ShowUpBonus { get => this.U16(Settings.PlayerRecordSalaryOffset + 2); set => this.SetU16(Settings.PlayerRecordSalaryOffset + 2, value, nameof(this.ShowUpBonus)); }

        /// <summary>Goal bonus ("Torprämie").</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Torprämie")]
        public ushort GoalsBonus { get => this.U16(Settings.PlayerRecordGoalsBonusOffset); set => this.SetU16(Settings.PlayerRecordGoalsBonusOffset, value, nameof(this.GoalsBonus)); }

        /// <summary>Transfer fee ("Ablöse").</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Ablöse")]
        public ushort TransferFee { get => this.U16(Settings.PlayerRecordTransferFeeOffset); set => this.SetU16(Settings.PlayerRecordTransferFeeOffset, value, nameof(this.TransferFee)); }

        /// <summary>Price last paid / buy option price, in the game's money factor x 100.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Kaufpreis")]
        public uint BuyPrice { get => this.U32(Settings.PlayerRecordBuyPriceOffset); set => this.SetU32(Settings.PlayerRecordBuyPriceOffset, value, nameof(this.BuyPrice)); }

        /// <summary>Years left on the contract; stored as the end year.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Vertragslaufzeit (Jahre)")]
        public byte ContractDuration
        {
            get => (byte)Math.Max(0, this.U16(Settings.PlayerRecordContractEndYearOffset) - this.calendar.Year);
            set { Check(value, 0, 20); this.SetU16(Settings.PlayerRecordContractEndYearOffset, (ushort)(this.calendar.Year + value), nameof(this.ContractDuration)); }
        }

        /// <summary>Years at the club; stored as the year the player joined.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Jahre im Verein")]
        public byte YearsInClub
        {
            get => (byte)(this.calendar.Year - this.U16(Settings.PlayerRecordContractEndYearOffset + 2));
            set { Check(value, 0, 50); this.SetU16(Settings.PlayerRecordContractEndYearOffset + 2, (ushort)(this.calendar.Year - value), nameof(this.YearsInClub)); }
        }

        /// <summary>Contract details.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Vertragsdetails"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Contract ContractDetails { get => (PlayerEnums.Contract)this.B(Settings.PlayerRecordContractDetailsOffset); set => this.SetB(Settings.PlayerRecordContractDetailsOffset, (byte)value, nameof(this.ContractDetails)); }

        /// <summary>Career plan.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Karriere"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Career Career { get => (PlayerEnums.Career)this.B(Settings.PlayerRecordCareerOffset); set => this.SetB(Settings.PlayerRecordCareerOffset, (byte)value, nameof(this.Career)); }

        /// <summary>Country of the lending club of a loaned player.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Stammverein (Land)"), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Country HomeCountry { get => (PlayerEnums.Country)this.B(Settings.PlayerRecordHomeCountryOffset); set => this.SetB(Settings.PlayerRecordHomeCountryOffset, (byte)value, nameof(this.HomeCountry)); }

        /// <summary>Club ID of the lending club of a loaned player.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Stammverein (ID)")]
        public ushort HomeClubId { get => this.U16(Settings.PlayerRecordHomeClubOffset); set => this.SetU16(Settings.PlayerRecordHomeClubOffset, value, nameof(this.HomeClubId)); }
        #endregion

        #region Statistik
        /// <summary>Red cards this season.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Rote Karten")]
        public byte RedCards { get => this.B(Settings.PlayerRecordRedCardsOffset); set => this.SetB(Settings.PlayerRecordRedCardsOffset, value, nameof(this.RedCards)); }
        /// <summary>Yellow-red cards this season.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Gelb-Rote Karten")]
        public byte YellowRedCards { get => this.B(Settings.PlayerRecordYellowRedCardsOffset); set => this.SetB(Settings.PlayerRecordYellowRedCardsOffset, value, nameof(this.YellowRedCards)); }
        /// <summary>Goals this season.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Tore")]
        public byte Goals { get => this.B(Settings.PlayerRecordGoalsOffset); set => this.SetB(Settings.PlayerRecordGoalsOffset, value, nameof(this.Goals)); }
        /// <summary>Own goals this season.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Eigentore")]
        public byte OwnGoals { get => this.B(Settings.PlayerRecordOwnGoalsOffset); set => this.SetB(Settings.PlayerRecordOwnGoalsOffset, value, nameof(this.OwnGoals)); }
        /// <summary>Assists this season.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Vorlagen")]
        public byte Assists { get => this.B(Settings.PlayerRecordAssistsOffset); set => this.SetB(Settings.PlayerRecordAssistsOffset, value, nameof(this.Assists)); }
        /// <summary>Goals as a substitute this season.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Joker-Tore")]
        public byte JokerGoals { get => this.B(Settings.PlayerRecordJokerGoalsOffset); set => this.SetB(Settings.PlayerRecordJokerGoalsOffset, value, nameof(this.JokerGoals)); }
        /// <summary>Appearances this season.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Einsätze")]
        public byte Appearances { get => this.B(Settings.PlayerRecordAppearancesOffset); set => this.SetB(Settings.PlayerRecordAppearancesOffset, value, nameof(this.Appearances)); }
        /// <summary>Times subbed in this season.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Eingewechselt")]
        public byte SubbedIn { get => this.B(Settings.PlayerRecordSubbedInOffset); set => this.SetB(Settings.PlayerRecordSubbedInOffset, value, nameof(this.SubbedIn)); }
        /// <summary>Times subbed out this season.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Ausgewechselt")]
        public byte SubbedOut { get => this.B(Settings.PlayerRecordSubbedOutOffset); set => this.SetB(Settings.PlayerRecordSubbedOutOffset, value, nameof(this.SubbedOut)); }
        /// <summary>League goals counted towards the season-end strength upgrade.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Tore für Aufwertung")]
        public byte UpgradeGoals { get => this.B(Settings.PlayerRecordUpgradeGoalsOffset); set => this.SetB(Settings.PlayerRecordUpgradeGoalsOffset, value, nameof(this.UpgradeGoals)); }
        /// <summary>National team caps; only players without any can be naturalised.</summary>
        [SaveGameCategory("Statistik"), DisplayName("Länderspiele")]
        public byte Caps { get => this.B(Settings.PlayerRecordCapsOffset); set => this.SetB(Settings.PlayerRecordCapsOffset, value, nameof(this.Caps)); }
        /// <summary>Retired from the national team (career bit 3).</summary>
        [SaveGameCategory("Statistik"), DisplayName("Aus Nationalteam zurückgetreten"), TypeConverter(typeof(YesNoConverter))]
        public bool NationalTeamRetired
        {
            get => (this.B(Settings.PlayerRecordCareerOffset) & (byte)PlayerEnums.Career.NationalTeamRetired) != 0;
            set
            {
                byte career = this.B(Settings.PlayerRecordCareerOffset);
                career = value ? (byte)(career | (byte)PlayerEnums.Career.NationalTeamRetired) : (byte)(career & ~(byte)PlayerEnums.Career.NationalTeamRetired);
                this.SetB(Settings.PlayerRecordCareerOffset, career, nameof(this.NationalTeamRetired));
            }
        }
        #endregion
    }

    /// <summary>
    /// A club's record (Settings.ClubRecordTableOffset, 0x770 bytes) in a savegame file. The game
    /// copies it into a working copy when it fetches the club (FUN_00585ff0); the record offsets
    /// here are the working-copy offsets of AddressPresets.OWN_CLUB mapped back through that copy.
    /// </summary>
    public sealed class SaveGameClub : SaveGameEntry
    {
        /// <summary>Number of club records in a savegame.</summary>
        public const int RecordCount = 300;
        /// <summary>Most players in a squad (working copy +0x990, room for 30).</summary>
        public const int SquadCapacity = 30;

        // Working copy -> record: the stadium block (+0x81C -> +0x4EC) and the finances (+0x154 -> +0x1A4).
        private const uint StadiumShift = 0x81C - 0x4EC;
        private const uint FinanceShift = 0x1A4 - 0x154;

        private const uint ShortNameOffset = 0x14;
        private const uint ChantOffset = 0x18;
        private const uint FanNameOffset = 0x4A;
        private const uint SquadCountOffset = 0xDE;
        private const uint SquadOffset = 0x570;
        private const uint AmateurCountOffset = 0x5BD;
        private const uint AmateurOffset = 0x5BE;
        private const uint IdOffset = 0xF4;
        private const uint CountryOffset = 0xF8;
        private const uint RespectOffset = 0x94;
        private const uint Will2WinOffset = 0xA0;
        private const uint SpiritOffset = 0xA2;
        private const uint PressingOffset = 0xA3;
        private const uint OffsideTrapOffset = 0xA4;
        private const uint AntiOffsideTrapOffset = 0xA5;
        private const uint TeamSkillsOffset = 0xA6;
        private const uint TeamCohesionOffset = 0x198;
        private const uint FreeTicketsOffset = 0x5D4;
        private const uint RoadGameSupportOffset = 0x5D6;

        private readonly SaveGameEditor editor;

        internal SaveGameClub(SaveGameEditor editor, int recordIndex)
            : base(editor.File, SaveGameFile.ModuleBase + Settings.ClubRecordTableOffset + (uint)recordIndex * Settings.ClubRecordStride)
        {
            this.editor = editor;
            this.RecordIndex = recordIndex;
        }

        /// <summary>Index of the record in the club table.</summary>
        [Browsable(false)]
        public int RecordIndex { get; }

        /// <inheritdoc/>
        public override string ToString() => this.Name;

        /// <summary>Clubs of the same country, by ID, for picking a rival or friend.</summary>
        internal IDictionary<byte, string> CountryClubNames => this.editor.ClubNames(this.Country);

        #region Verein
        /// <summary>Club ID within its country.</summary>
        [SaveGameCategory("Verein"), DisplayName("ID"), ReadOnly(true)]
        public byte Id => this.B(IdOffset);

        /// <summary>The club's country.</summary>
        [SaveGameCategory("Verein"), DisplayName("Land"), ReadOnly(true), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Country Country => (PlayerEnums.Country)this.B(CountryOffset);

        /// <summary>Club name.</summary>
        [SaveGameCategory("Verein"), DisplayName("Name")]
        public string Name { get => this.Text(0, 20); set => this.SetText(0, value, 19, nameof(this.Name)); }

        /// <summary>Short name ("---" = none).</summary>
        [SaveGameCategory("Verein"), DisplayName("Kürzel")]
        public string ShortName { get => this.Text(ShortNameOffset, 4); set => this.SetText(ShortNameOffset, value, Club.ShortNameLength, nameof(this.ShortName)); }

        /// <summary>Article of the name.</summary>
        [SaveGameCategory("Verein"), DisplayName("Artikel"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.Article Article { get => (ClubEnums.Article)this.B(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.Article)); set => this.SetB(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.Article), (byte)value, nameof(this.Article)); }

        /// <summary>Preposition of the name.</summary>
        [SaveGameCategory("Verein"), DisplayName("Präposition"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.Preposition Preposition { get => (ClubEnums.Preposition)this.B(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.Preposition)); set => this.SetB(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.Preposition), (byte)value, nameof(this.Preposition)); }

        /// <summary>Home town.</summary>
        [SaveGameCategory("Verein"), DisplayName("Ort")]
        public string Town { get => this.Text(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.Town), Club.TownLength + 1); set => this.SetText(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.Town), value, Club.TownLength, nameof(this.Town)); }

        /// <summary>Board ("Vorstand").</summary>
        [SaveGameCategory("Verein"), DisplayName("Vorstand"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.Board Board { get => (ClubEnums.Board)this.B(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.Board)); set => this.SetB(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.Board), (byte)value, nameof(this.Board)); }

        /// <summary>The goal the board measures the trainer against.</summary>
        [SaveGameCategory("Verein"), DisplayName("Saisonziel"), TypeConverter(typeof(DescriptionEnumConverter))]
        public CoachEnums.SeasonGoal SeasonGoal { get => (CoachEnums.SeasonGoal)this.B(Settings.ClubRecordSeasonGoalOffset); set => this.SetB(Settings.ClubRecordSeasonGoalOffset, (byte)value, nameof(this.SeasonGoal)); }

        /// <summary>Arch rival (club ID in the same country).</summary>
        [SaveGameCategory("Verein"), DisplayName("Erzrivale"), TypeConverter(typeof(ClubIdConverter))]
        public byte ArchRivalId { get => this.B(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.ArchRival)); set => this.SetB(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.ArchRival), value, nameof(this.ArchRivalId)); }

        /// <summary>Fan friendship (club ID in the same country).</summary>
        [SaveGameCategory("Verein"), DisplayName("Fanfreundschaft"), TypeConverter(typeof(ClubIdConverter))]
        public byte FanFriendshipId { get => this.B(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.FanFriendship)); set => this.SetB(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.FanFriendship), value, nameof(this.FanFriendshipId)); }

        /// <summary>Position on the editor's map.</summary>
        [SaveGameCategory("Verein"), DisplayName("Karte X")]
        public short MapX { get => this.I16(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.MapX)); set => this.SetI16(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.MapX), value, nameof(this.MapX)); }

        /// <summary>Position on the editor's map.</summary>
        [SaveGameCategory("Verein"), DisplayName("Karte Y")]
        public short MapY { get => this.I16(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.MapY)); set => this.SetI16(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.MapY), value, nameof(this.MapY)); }
        #endregion

        #region Fans
        /// <summary>Chant ("Anfeuerung").</summary>
        [SaveGameCategory("Fans"), DisplayName("Anfeuerung")]
        public string Chant { get => this.Text(ChantOffset, Club.FanTextLength + 1); set => this.SetText(ChantOffset, value, Club.FanTextLength, nameof(this.Chant)); }

        /// <summary>What the fans are called.</summary>
        [SaveGameCategory("Fans"), DisplayName("Fanbezeichnung")]
        public string FanName { get => this.Text(FanNameOffset, Club.FanTextLength + 1); set => this.SetText(FanNameOffset, value, Club.FanTextLength, nameof(this.FanName)); }

        /// <summary>Fan turnout.</summary>
        [SaveGameCategory("Fans"), DisplayName("Fanaufkommen"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.FanTurnout FanTurnout { get => (ClubEnums.FanTurnout)this.B(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.FanTurnout)); set => this.SetB(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.FanTurnout), (byte)value, nameof(this.FanTurnout)); }

        /// <summary>Kind of fans.</summary>
        [SaveGameCategory("Fans"), DisplayName("Fantyp"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.FanType FanType { get => (ClubEnums.FanType)this.B(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.FanType)); set => this.SetB(Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.FanType), (byte)value, nameof(this.FanType)); }

        /// <summary>Free tickets.</summary>
        [SaveGameCategory("Fans"), DisplayName("Freikarten")]
        public ushort FreeTickets { get => this.U16(FreeTicketsOffset); set => this.SetU16(FreeTicketsOffset, value, nameof(this.FreeTickets)); }

        /// <summary>Support for away games.</summary>
        [SaveGameCategory("Fans"), DisplayName("Auswärtsfahrten")]
        public ushort RoadGameSupport { get => this.U16(RoadGameSupportOffset); set => this.SetU16(RoadGameSupportOffset, value, nameof(this.RoadGameSupport)); }
        #endregion

        #region Trikots
        private uint JerseyOffset => Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.Jersey);
        private ClubEnums.JerseyColor ShirtColor(int set) => (ClubEnums.JerseyColor)(this.B(this.JerseyOffset + (uint)set * 3) & 0x0F);
        private ClubEnums.ShirtPattern ShirtPattern(int set) => (ClubEnums.ShirtPattern)(this.B(this.JerseyOffset + (uint)set * 3) >> 4);
        private void SetShirt(int set, ClubEnums.JerseyColor color, ClubEnums.ShirtPattern pattern, string name) =>
            this.SetB(this.JerseyOffset + (uint)set * 3, (byte)(((byte)pattern << 4) | ((byte)color & 0x0F)), name);

        /// <summary>Shirt colour of the first set.</summary>
        [SaveGameCategory("Trikots"), DisplayName("1. Hemd Farbe"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.JerseyColor Shirt1Color { get => this.ShirtColor(0); set => this.SetShirt(0, value, this.ShirtPattern(0), nameof(this.Shirt1Color)); }
        /// <summary>Shirt pattern of the first set.</summary>
        [SaveGameCategory("Trikots"), DisplayName("1. Hemd Muster"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.ShirtPattern Shirt1Pattern { get => this.ShirtPattern(0); set => this.SetShirt(0, this.ShirtColor(0), value, nameof(this.Shirt1Pattern)); }
        /// <summary>Shorts colour of the first set.</summary>
        [SaveGameCategory("Trikots"), DisplayName("1. Hose"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.JerseyColor Shorts1 { get => (ClubEnums.JerseyColor)this.B(this.JerseyOffset + 1); set => this.SetB(this.JerseyOffset + 1, (byte)value, nameof(this.Shorts1)); }
        /// <summary>Socks colour of the first set.</summary>
        [SaveGameCategory("Trikots"), DisplayName("1. Stutzen"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.JerseyColor Socks1 { get => (ClubEnums.JerseyColor)this.B(this.JerseyOffset + 2); set => this.SetB(this.JerseyOffset + 2, (byte)value, nameof(this.Socks1)); }
        /// <summary>Shirt colour of the second set.</summary>
        [SaveGameCategory("Trikots"), DisplayName("2. Hemd Farbe"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.JerseyColor Shirt2Color { get => this.ShirtColor(1); set => this.SetShirt(1, value, this.ShirtPattern(1), nameof(this.Shirt2Color)); }
        /// <summary>Shirt pattern of the second set.</summary>
        [SaveGameCategory("Trikots"), DisplayName("2. Hemd Muster"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.ShirtPattern Shirt2Pattern { get => this.ShirtPattern(1); set => this.SetShirt(1, this.ShirtColor(1), value, nameof(this.Shirt2Pattern)); }
        /// <summary>Shorts colour of the second set.</summary>
        [SaveGameCategory("Trikots"), DisplayName("2. Hose"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.JerseyColor Shorts2 { get => (ClubEnums.JerseyColor)this.B(this.JerseyOffset + 4); set => this.SetB(this.JerseyOffset + 4, (byte)value, nameof(this.Shorts2)); }
        /// <summary>Socks colour of the second set.</summary>
        [SaveGameCategory("Trikots"), DisplayName("2. Stutzen"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.JerseyColor Socks2 { get => (ClubEnums.JerseyColor)this.B(this.JerseyOffset + 5); set => this.SetB(this.JerseyOffset + 5, (byte)value, nameof(this.Socks2)); }
        #endregion

        #region Mannschaft
        /// <summary>Respect.</summary>
        [SaveGameCategory("Mannschaft"), DisplayName("Respekt")]
        public byte Respect { get => this.B(RespectOffset); set => this.SetB(RespectOffset, value, nameof(this.Respect)); }
        /// <summary>Will to win.</summary>
        [SaveGameCategory("Mannschaft"), DisplayName("Siegeswille")]
        public byte Will2Win { get => this.B(Will2WinOffset); set => this.SetB(Will2WinOffset, value, nameof(this.Will2Win)); }
        /// <summary>Team spirit.</summary>
        [SaveGameCategory("Mannschaft"), DisplayName("Moral")]
        public byte Spirit { get => this.B(SpiritOffset); set => this.SetB(SpiritOffset, value, nameof(this.Spirit)); }
        /// <summary>Team cohesion.</summary>
        [SaveGameCategory("Mannschaft"), DisplayName("Mannschaftsgefüge")]
        public byte TeamCohesion { get => this.B(TeamCohesionOffset); set => this.SetB(TeamCohesionOffset, value, nameof(this.TeamCohesion)); }

        /// <summary>IDs of the professional squad (record +0xDE count, +0x570 IDs).</summary>
        [Browsable(false)]
        public IList<ushort> SquadIds => this.Ids(this.I16(SquadCountOffset), SquadOffset);

        /// <summary>IDs of the amateurs (record +0x5BD count, +0x5BE IDs).</summary>
        [Browsable(false)]
        public IList<ushort> AmateurIds => this.Ids(this.B(AmateurCountOffset), AmateurOffset);

        private IList<ushort> Ids(int count, uint offset)
        {
            List<ushort> ids = new List<ushort>();
            for (int i = 0; i < Math.Min(count, SquadCapacity); i++)
            {
                ushort id = this.U16(offset + (uint)i * 2);
                if (id < SaveGamePlayer.RecordCount)
                    ids.Add(id);
            }
            return ids;
        }
        #endregion

        #region Taktik
        /// <summary>Offside trap (x10 scale).</summary>
        [SaveGameCategory("Taktik"), DisplayName("Abseitsfalle")]
        public byte OffsideTrap { get => this.B(OffsideTrapOffset); set => this.SetB(OffsideTrapOffset, value, nameof(this.OffsideTrap)); }
        /// <summary>Anti-offside trap (0-5).</summary>
        [SaveGameCategory("Taktik"), DisplayName("Anti-Abseitsfalle")]
        public byte AntiOffsideTrap { get => this.B(AntiOffsideTrapOffset); set { Check(value, 0, Club.MaxTeamSkill); this.SetB(AntiOffsideTrapOffset, value, nameof(this.AntiOffsideTrap)); } }
        /// <summary>Pressing (0-5).</summary>
        [SaveGameCategory("Taktik"), DisplayName("Pressing")]
        public byte Pressing { get => this.B(PressingOffset); set { Check(value, 0, Club.MaxTeamSkill); this.SetB(PressingOffset, value, nameof(this.Pressing)); } }
        /// <summary>Throw-ins (0-5).</summary>
        [SaveGameCategory("Taktik"), DisplayName("Einwürfe")]
        public byte ThrowIns { get => this.Skill(6); set => this.SetSkill(6, value, nameof(this.ThrowIns)); }
        /// <summary>Free kicks (0-5).</summary>
        [SaveGameCategory("Taktik"), DisplayName("Freistöße")]
        public byte FreeKicks { get => this.Skill(0); set => this.SetSkill(0, value, nameof(this.FreeKicks)); }
        /// <summary>Corners (0-5).</summary>
        [SaveGameCategory("Taktik"), DisplayName("Ecken")]
        public byte Corners { get => this.Skill(3); set => this.SetSkill(3, value, nameof(this.Corners)); }
        /// <summary>Dirty tricks (0-5).</summary>
        [SaveGameCategory("Taktik"), DisplayName("Miese Tricks")]
        public byte DirtyTricks { get => this.Skill(12); set => this.SetSkill(12, value, nameof(this.DirtyTricks)); }

        // The packed skills (see Club.TeamSkills): 3 bits each.
        private byte Skill(int shift) => (byte)((this.U16(TeamSkillsOffset) >> shift) & 7);

        private void SetSkill(int shift, byte value, string name)
        {
            Check(value, 0, Club.MaxTeamSkill);
            this.SetU16(TeamSkillsOffset, (ushort)((this.U16(TeamSkillsOffset) & ~(7 << shift)) | (value << shift)), name);
        }
        #endregion

        #region Finanzen
        private int Money(string key) => this.I32(WorkingToRecord(key));
        private void SetMoney(string key, int value, string name) => this.SetI32(WorkingToRecord(key), value, name);
        private static uint WorkingToRecord(string workingHex) => Convert.ToUInt32(workingHex, 16) + FinanceShift;

        /// <summary>Cash.</summary>
        [SaveGameCategory("Finanzen"), DisplayName("Vermögen")]
        public int Wealth { get => this.Money("154"); set => this.SetMoney("154", value, nameof(this.Wealth)); }
        /// <summary>Earnings from league games.</summary>
        [SaveGameCategory("Finanzen"), DisplayName("Einnahmen Ligaspiele")]
        public int EarningsLeagueGames { get => this.Money("290"); set => this.SetMoney("290", value, nameof(this.EarningsLeagueGames)); }
        /// <summary>Earnings from friendlies.</summary>
        [SaveGameCategory("Finanzen"), DisplayName("Einnahmen Freundschaftsspiele")]
        public int EarningsFriendlyGames { get => this.Money("2B8"); set => this.SetMoney("2B8", value, nameof(this.EarningsFriendlyGames)); }
        /// <summary>Earnings from advertising.</summary>
        [SaveGameCategory("Finanzen"), DisplayName("Einnahmen Werbung")]
        public int EarningsAds { get => this.Money("470"); set => this.SetMoney("470", value, nameof(this.EarningsAds)); }
        /// <summary>Money from the current sponsor.</summary>
        [SaveGameCategory("Finanzen"), DisplayName("Sponsorgeld")]
        public int SponsorCash { get => this.Money("3C4"); set => this.SetMoney("3C4", value, nameof(this.SponsorCash)); }
        /// <summary>Sponsor contract period.</summary>
        [SaveGameCategory("Finanzen"), DisplayName("Sponsorlaufzeit")]
        public byte SponsorPeriod { get => this.B(WorkingToRecord("3BD")); set => this.SetB(WorkingToRecord("3BD"), value, nameof(this.SponsorPeriod)); }
        /// <summary>Grant pot ("Zuschusstopf").</summary>
        [SaveGameCategory("Finanzen"), DisplayName("Zuschusstopf")]
        public int GrantPot { get => this.Money("3B8"); set => this.SetMoney("3B8", value, nameof(this.GrantPot)); }
        /// <summary>Grant rate.</summary>
        [SaveGameCategory("Finanzen"), DisplayName("Zuschussrate")]
        public short GrantRate { get => this.I16(WorkingToRecord("3B4")); set => this.SetI16(WorkingToRecord("3B4"), value, nameof(this.GrantRate)); }
        #endregion

        #region Stadion
        private static uint Stadium(string workingHex) => Convert.ToUInt32(workingHex, 16) - StadiumShift;

        /// <summary>Stadium name.</summary>
        [SaveGameCategory("Stadion"), DisplayName("Stadionname")]
        public string StadiumName { get => this.Text(Stadium("81C"), 29); set => this.SetText(Stadium("81C"), value, 28, nameof(this.StadiumName)); }

        /// <summary>Capacity as the game counts it: every block not under construction (FUN_00607920).</summary>
        [SaveGameCategory("Stadion"), DisplayName("Kapazität"), ReadOnly(true)]
        public int Capacity => this.I32(Stadium("850"));

        /// <summary>Stadium blocks A-L.</summary>
        [SaveGameCategory("Stadion"), DisplayName("Blöcke"), TypeConverter(typeof(ExpandableObjectConverter))]
        public StadiumBlocks Blocks => new StadiumBlocks(this);

        /// <summary>Video wall / scoreboard.</summary>
        [SaveGameCategory("Stadion"), DisplayName("Anzeigetafel"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.DisplayUnit DisplayUnit { get => (ClubEnums.DisplayUnit)this.B(Stadium("896")); set => this.SetB(Stadium("896"), (byte)value, nameof(this.DisplayUnit)); }
        /// <summary>Pitch condition.</summary>
        [SaveGameCategory("Stadion"), DisplayName("Rasen"), TypeConverter(typeof(DescriptionEnumConverter))]
        public ClubEnums.FieldCondition FieldCondition { get => (ClubEnums.FieldCondition)this.B(Stadium("89B")); set => this.SetB(Stadium("89B"), (byte)value, nameof(this.FieldCondition)); }
        /// <summary>Floodlight.</summary>
        [SaveGameCategory("Stadion"), DisplayName("Flutlicht"), TypeConverter(typeof(YesNoConverter))]
        public bool HasFloodLight { get => this.B(Stadium("897")) != 0; set => this.SetB(Stadium("897"), (byte)(value ? 1 : 0), nameof(this.HasFloodLight)); }
        /// <summary>Undersoil heating.</summary>
        [SaveGameCategory("Stadion"), DisplayName("Rasenheizung"), TypeConverter(typeof(YesNoConverter))]
        public bool HasGrassHeating { get => this.B(Stadium("898")) != 0; set => this.SetB(Stadium("898"), (byte)(value ? 1 : 0), nameof(this.HasGrassHeating)); }
        /// <summary>Upper tier.</summary>
        [SaveGameCategory("Stadion"), DisplayName("Oberrang"), TypeConverter(typeof(YesNoConverter))]
        public bool HasUpperSeats { get => this.B(Stadium("899")) != 0; set => this.SetB(Stadium("899"), (byte)(value ? 1 : 0), nameof(this.HasUpperSeats)); }
        /// <summary>Running track.</summary>
        [SaveGameCategory("Stadion"), DisplayName("Laufbahn"), TypeConverter(typeof(YesNoConverter))]
        public bool HasRunningTrack { get => this.B(Stadium("89A")) != 0; set => this.SetB(Stadium("89A"), (byte)(value ? 1 : 0), nameof(this.HasRunningTrack)); }

        internal short BlockStandings(int block) => this.I16(Stadium("854") + (uint)block * 2);
        internal short BlockSeats(int block) => this.I16(Stadium("86C") + (uint)block * 2);
        internal byte BlockWeeks(int block) => this.B(Stadium("884") + (uint)block);
        internal bool BlockRoof(int block) => (this.U16(Stadium("890")) & (1 << block)) != 0;

        internal void SetBlock(int block, short? standings, short? seats, byte? weeks, bool? roof)
        {
            if (standings.HasValue)
                this.File.WriteInt16(this.Address + Stadium("854") + (uint)block * 2, standings.Value);
            if (seats.HasValue)
                this.File.WriteInt16(this.Address + Stadium("86C") + (uint)block * 2, seats.Value);
            if (weeks.HasValue)
                this.File.WriteByte(this.Address + Stadium("884") + (uint)block, weeks.Value);
            if (roof.HasValue)
            {
                ushort roofs = this.U16(Stadium("890"));
                roofs = roof.Value ? (ushort)(roofs | (1 << block)) : (ushort)(roofs & ~(1 << block));
                this.File.WriteUInt16(this.Address + Stadium("890"), roofs);
            }
            // The game keeps the capacity next to the blocks and doesn't recount it on load.
            int capacity = 0;
            for (int i = 0; i < Club.BlockCount; i++)
            {
                if (this.BlockWeeks(i) == 0)
                    capacity += this.BlockStandings(i) + this.BlockSeats(i);
            }
            this.File.WriteInt32(this.Address + Stadium("850"), capacity);
            this.OnPropertyChanged(nameof(this.Blocks));
            this.OnPropertyChanged(nameof(this.Capacity));
        }
        #endregion
    }

    /// <summary>The twelve stadium blocks of a <see cref="SaveGameClub"/>, for the property grid.</summary>
    public sealed class StadiumBlocks
    {
        private readonly SaveGameClub club;

        internal StadiumBlocks(SaveGameClub club)
        {
            this.club = club;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Club.BlockCount} Blöcke";

        private StadiumBlock Get(int block) => new StadiumBlock(this.club, block);

        /// <summary>Block A.</summary>
        [DisplayName("Block A"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock A => this.Get(0);
        /// <summary>Block B.</summary>
        [DisplayName("Block B"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock B => this.Get(1);
        /// <summary>Block C.</summary>
        [DisplayName("Block C"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock C => this.Get(2);
        /// <summary>Block D.</summary>
        [DisplayName("Block D"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock D => this.Get(3);
        /// <summary>Block E.</summary>
        [DisplayName("Block E"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock E => this.Get(4);
        /// <summary>Block F.</summary>
        [DisplayName("Block F"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock F => this.Get(5);
        /// <summary>Block G.</summary>
        [DisplayName("Block G"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock G => this.Get(6);
        /// <summary>Block H.</summary>
        [DisplayName("Block H"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock H => this.Get(7);
        /// <summary>Block I.</summary>
        [DisplayName("Block I"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock I => this.Get(8);
        /// <summary>Block J.</summary>
        [DisplayName("Block J"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock J => this.Get(9);
        /// <summary>Block K (boxes).</summary>
        [DisplayName("Block K"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock K => this.Get(10);
        /// <summary>Block L.</summary>
        [DisplayName("Block L"), TypeConverter(typeof(ExpandableObjectConverter))] public StadiumBlock L => this.Get(11);
    }

    /// <summary>One stadium block of a <see cref="SaveGameClub"/>.</summary>
    public sealed class StadiumBlock
    {
        private readonly SaveGameClub club;
        private readonly int block;

        internal StadiumBlock(SaveGameClub club, int block)
        {
            this.club = club;
            this.block = block;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{this.Standings + this.Seats} Plätze";

        /// <summary>Standing places (block K: boxes).</summary>
        [DisplayName("Stehplätze")]
        public short Standings { get => this.club.BlockStandings(this.block); set { SaveGameEntry.Check(value, 0, short.MaxValue); this.club.SetBlock(this.block, value, null, null, null); } }
        /// <summary>Seats.</summary>
        [DisplayName("Sitzplätze")]
        public short Seats { get => this.club.BlockSeats(this.block); set { SaveGameEntry.Check(value, 0, short.MaxValue); this.club.SetBlock(this.block, null, value, null, null); } }
        /// <summary>Remaining construction weeks.</summary>
        [DisplayName("Bauzeit (Wochen)")]
        public byte Weeks { get => this.club.BlockWeeks(this.block); set => this.club.SetBlock(this.block, null, null, value, null); }
        /// <summary>Roofed.</summary>
        [DisplayName("Überdacht"), TypeConverter(typeof(YesNoConverter))]
        public bool Roof { get => this.club.BlockRoof(this.block); set => this.club.SetBlock(this.block, null, null, null, value); }
    }

    /// <summary>
    /// A human trainer ("Mitspieler") in a savegame file: the saved record
    /// (Settings.CoachRecordTableOffset, same field offsets as the working copy the live Trainer
    /// edits, see AddressPresets.COACH), plus competencies and image from their per-ID table.
    /// </summary>
    public sealed class SaveGameCoach : SaveGameEntry
    {
        // AddressPresets.COACH offsets are relative to 0x822D00, where Mitspieler ID 1 starts at +34B00.
        private const uint PresetBase = 0x34B00;
        private const int StockStride = 0x10;
        private static readonly CoachEnums.CompetencyKey[] CompetencyOrder = (CoachEnums.CompetencyKey[])Enum.GetValues(typeof(CoachEnums.CompetencyKey));

        private readonly SaveGameEditor editor;
        private readonly uint competencyAddress;
        private readonly uint imageAddress;

        internal SaveGameCoach(SaveGameEditor editor, int id)
            : base(editor.File, SaveGameFile.ModuleBase + Settings.CoachRecordTableOffset + (uint)id * Settings.CoachRecordStride)
        {
            this.editor = editor;
            this.MitspielerId = id;
            this.competencyAddress = SaveGameFile.ModuleBase + Settings.CoachCompetencyRecordOffset + (uint)id * Settings.CoachCompetencyRecordStride;
            this.imageAddress = SaveGameFile.ModuleBase + Settings.CoachImageRecordOffset + (uint)id * Settings.CoachCompetencyRecordStride;
        }

        /// <summary>Mitspieler ID (1-based).</summary>
        [Browsable(false)]
        public int MitspielerId { get; }

        private static uint Field(CoachEnums.AddressKey key) => Convert.ToUInt32(AddressPresets.COACH[key], 16) - PresetBase;

        /// <inheritdoc/>
        public override string ToString()
        {
            string name = $"{this.Firstname} {this.Lastname}".Trim();
            return name.Length > 0 ? name : $"Manager {this.MitspielerId}";
        }

        #region Person
        /// <summary>First name.</summary>
        [SaveGameCategory("Person"), DisplayName("Vorname")]
        public string Firstname { get => this.Text(Field(CoachEnums.AddressKey.FIRSTNAME), 10); set => this.SetText(Field(CoachEnums.AddressKey.FIRSTNAME), value, 9, nameof(this.Firstname)); }
        /// <summary>Last name.</summary>
        [SaveGameCategory("Person"), DisplayName("Nachname")]
        public string Lastname { get => this.Text(Field(CoachEnums.AddressKey.LAST_NAME), 16); set => this.SetText(Field(CoachEnums.AddressKey.LAST_NAME), value, 15, nameof(this.Lastname)); }
        /// <summary>Age.</summary>
        [SaveGameCategory("Person"), DisplayName("Alter")]
        public byte Age { get => this.B(Field(CoachEnums.AddressKey.AGE)); set => this.SetB(Field(CoachEnums.AddressKey.AGE), value, nameof(this.Age)); }
        /// <summary>Trainer level (0-15).</summary>
        [SaveGameCategory("Person"), DisplayName("Trainerstufe")]
        public byte Level { get => this.B(Field(CoachEnums.AddressKey.LEVEL)); set { Check(value, Coach.MinLevel, Coach.MaxLevel); this.SetB(Field(CoachEnums.AddressKey.LEVEL), value, nameof(this.Level)); } }
        /// <summary>Difficulty level.</summary>
        [SaveGameCategory("Person"), DisplayName("Schwierigkeitsgrad"), TypeConverter(typeof(DescriptionEnumConverter))]
        public CoachEnums.Difficulty Difficulty { get => (CoachEnums.Difficulty)this.B(Field(CoachEnums.AddressKey.DIFFICULTY)); set => this.SetB(Field(CoachEnums.AddressKey.DIFFICULTY), (byte)value, nameof(this.Difficulty)); }
        /// <summary>Manager image.</summary>
        [SaveGameCategory("Person"), DisplayName("Image"), TypeConverter(typeof(DescriptionEnumConverter))]
        public CoachEnums.ManagerImage Image { get => (CoachEnums.ManagerImage)this.File.ReadUInt16(this.imageAddress); set { this.File.WriteUInt16(this.imageAddress, (ushort)value); this.OnPropertyChanged(nameof(this.Image)); } }
        /// <summary>Private wealth.</summary>
        [SaveGameCategory("Person"), DisplayName("Privatvermögen")]
        public int Wealth { get => this.I32(Field(CoachEnums.AddressKey.WEALTH)); set => this.SetI32(Field(CoachEnums.AddressKey.WEALTH), value, nameof(this.Wealth)); }
        /// <summary>Matches managed.</summary>
        [SaveGameCategory("Person"), DisplayName("Spiele")]
        public ushort Games { get => this.U16(Field(CoachEnums.AddressKey.GAMES)); set => this.SetU16(Field(CoachEnums.AddressKey.GAMES), value, nameof(this.Games)); }
        /// <summary>Matches won.</summary>
        [SaveGameCategory("Person"), DisplayName("Siege")]
        public ushort Wins { get => this.U16(Field(CoachEnums.AddressKey.WINS)); set => this.SetU16(Field(CoachEnums.AddressKey.WINS), value, nameof(this.Wins)); }
        #endregion

        #region Verein
        /// <summary>Current club, read-only like in the live Trainer.</summary>
        [SaveGameCategory("Verein"), DisplayName("Aktueller Verein"), ReadOnly(true)]
        public string CurrentClub => this.editor.ClubName((PlayerEnums.Country)this.B(Field(CoachEnums.AddressKey.COUNTRY)), this.B(Field(CoachEnums.AddressKey.CLUB)));
        /// <summary>Club for next season, read-only.</summary>
        [SaveGameCategory("Verein"), DisplayName("Verein nächste Saison"), ReadOnly(true)]
        public string NextClub => this.editor.ClubName((PlayerEnums.Country)this.B(Field(CoachEnums.AddressKey.COUNTRY_NEXT_SEASON)), this.B(Field(CoachEnums.AddressKey.CLUB_NEXT_SEASON)));
        #endregion

        #region Vertrag
        /// <summary>Salary per year.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Gehalt")]
        public int Salary { get => this.I32(Field(CoachEnums.AddressKey.SALARY)); set => this.SetI32(Field(CoachEnums.AddressKey.SALARY), value, nameof(this.Salary)); }
        /// <summary>Bonus per league point.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Punktprämie")]
        public int PointBonus { get => this.I32(Field(CoachEnums.AddressKey.POINT_BONUS)); set => this.SetI32(Field(CoachEnums.AddressKey.POINT_BONUS), value, nameof(this.PointBonus)); }
        /// <summary>Bonus per cup round won.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Prämie je Pokalrunde")]
        public int CupRoundBonus { get => this.I32(Field(CoachEnums.AddressKey.PER_TROPHY_ROUND)); set => this.SetI32(Field(CoachEnums.AddressKey.PER_TROPHY_ROUND), value, nameof(this.CupRoundBonus)); }
        /// <summary>Bonus for a title or promotion.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Titelprämie")]
        public int TitleBonus { get => this.I32(Field(CoachEnums.AddressKey.PER_TITLE)); set => this.SetI32(Field(CoachEnums.AddressKey.PER_TITLE), value, nameof(this.TitleBonus)); }
        /// <summary>Bonus for reaching the season goal.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Prämie Saisonziel")]
        public int SeasonGoalBonus { get => this.I32(Field(CoachEnums.AddressKey.ACHIEVED_BONUS)); set => this.SetI32(Field(CoachEnums.AddressKey.ACHIEVED_BONUS), value, nameof(this.SeasonGoalBonus)); }
        /// <summary>Season goal of the contract (the board judges by the club's goal, see <see cref="SaveGameClub.SeasonGoal"/>).</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Saisonziel"), TypeConverter(typeof(DescriptionEnumConverter))]
        public CoachEnums.SeasonGoal SeasonGoal { get => (CoachEnums.SeasonGoal)this.U16(Field(CoachEnums.AddressKey.SEASON_GOAL)); set => this.SetU16(Field(CoachEnums.AddressKey.SEASON_GOAL), (ushort)value, nameof(this.SeasonGoal)); }
        /// <summary>Years left on the contract.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Restlaufzeit (Jahre)")]
        public ushort ContractYearsLeft { get => this.U16(Field(CoachEnums.AddressKey.CONTRACT_DURATION)); set => this.SetU16(Field(CoachEnums.AddressKey.CONTRACT_DURATION), value, nameof(this.ContractYearsLeft)); }
        /// <summary>The club's option to extend by a year.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Verlängerungsoption"), TypeConverter(typeof(YesNoConverter))]
        public bool HasExtensionOption
        {
            get => this.U16(Field(CoachEnums.AddressKey.EXTEND_OPTION)) != 0;
            set => this.SetU16(Field(CoachEnums.AddressKey.EXTEND_OPTION), value ? (this.U16(Field(CoachEnums.AddressKey.EXTEND_OPTION)) != 0 ? this.U16(Field(CoachEnums.AddressKey.EXTEND_OPTION)) : (ushort)1) : (ushort)0, nameof(this.HasExtensionOption));
        }
        /// <summary>The board's trust (0-100); like the live Trainer, setting it adjusts only the base value.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Vertrauen des Vorstandes")]
        public byte BoardTrust
        {
            get => Coach.EffectiveBoardTrust(this.I16(Field(CoachEnums.AddressKey.TRUST)), this.BoardTrustBonus);
            set { Check(value, 0, Coach.MaxBoardTrust); this.SetI16(Field(CoachEnums.AddressKey.TRUST), Coach.BoardTrustBaseFor(value, this.BoardTrustBonus), nameof(this.BoardTrust)); }
        }
        private int BoardTrustBonus
        {
            get
            {
                uint bonus = Field(CoachEnums.AddressKey.TRUST_BONUS);
                return this.B(bonus) + this.B(bonus + 1) + (sbyte)this.B(bonus + 2) + (sbyte)this.B(bonus + 3);
            }
        }
        /// <summary>Release clause.</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Ausstiegsklausel"), TypeConverter(typeof(YesNoConverter))]
        public bool HasReleaseClause { get => this.B(Field(CoachEnums.AddressKey.RELEASE_CLAUSE)) != 0; set => this.SetB(Field(CoachEnums.AddressKey.RELEASE_CLAUSE), (byte)(value ? 1 : 0), nameof(this.HasReleaseClause)); }
        /// <summary>Company car (contract extras, even bits).</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Fahrzeug"), TypeConverter(typeof(DescriptionEnumConverter))]
        public CoachEnums.VehiclePerk Vehicle
        {
            get => new Coach { Perks = this.Perks }.Vehicle;
            set { Coach coach = new Coach { Perks = this.Perks }; coach.Vehicle = value; this.SetU16(Field(CoachEnums.AddressKey.PERKS), coach.Perks, nameof(this.Vehicle)); }
        }
        /// <summary>Housing (contract extras, odd bits).</summary>
        [SaveGameCategory("Vertrag"), DisplayName("Wohnung"), TypeConverter(typeof(DescriptionEnumConverter))]
        public CoachEnums.HousingPerk Housing
        {
            get => new Coach { Perks = this.Perks }.Housing;
            set { Coach coach = new Coach { Perks = this.Perks }; coach.Housing = value; this.SetU16(Field(CoachEnums.AddressKey.PERKS), coach.Perks, nameof(this.Housing)); }
        }
        private ushort Perks => this.U16(Field(CoachEnums.AddressKey.PERKS));
        #endregion

        #region Kompetenzen
        private uint CompetencyAt(int index) => this.competencyAddress + (uint)((this.Level * CompetencyOrder.Length + index) * 2);
        private ushort GetCompetency(int index) => this.File.ReadUInt16(this.CompetencyAt(index));
        private void SetCompetency(int index, ushort value, string name) { this.File.WriteUInt16(this.CompetencyAt(index), value); this.OnPropertyChanged(name); }

        /// <summary>Negotiating skill at the current level.</summary>
        [SaveGameCategory("Kompetenzen (aktuelle Stufe)"), DisplayName("Verhandlungsgeschick")]
        public ushort Negotiation { get => this.GetCompetency(0); set => this.SetCompetency(0, value, nameof(this.Negotiation)); }
        /// <summary>Motivation at the current level.</summary>
        [SaveGameCategory("Kompetenzen (aktuelle Stufe)"), DisplayName("Motivationsfähigkeit")]
        public ushort Motivation { get => this.GetCompetency(1); set => this.SetCompetency(1, value, nameof(this.Motivation)); }
        /// <summary>Training at the current level.</summary>
        [SaveGameCategory("Kompetenzen (aktuelle Stufe)"), DisplayName("Trainingsgestaltung")]
        public ushort Training { get => this.GetCompetency(2); set => this.SetCompetency(2, value, nameof(this.Training)); }
        /// <summary>Authority at the current level.</summary>
        [SaveGameCategory("Kompetenzen (aktuelle Stufe)"), DisplayName("Autorität")]
        public ushort Authority { get => this.GetCompetency(3); set => this.SetCompetency(3, value, nameof(this.Authority)); }
        /// <summary>Languages at the current level.</summary>
        [SaveGameCategory("Kompetenzen (aktuelle Stufe)"), DisplayName("Fremdsprachenkenntnisse")]
        public ushort Languages { get => this.GetCompetency(4); set => this.SetCompetency(4, value, nameof(this.Languages)); }
        /// <summary>Charisma at the current level.</summary>
        [SaveGameCategory("Kompetenzen (aktuelle Stufe)"), DisplayName("Ausstrahlung")]
        public ushort Charisma { get => this.GetCompetency(5); set => this.SetCompetency(5, value, nameof(this.Charisma)); }
        #endregion

        #region Aktien
        /// <summary>The trainer's stock slots.</summary>
        [SaveGameCategory("Aktien"), DisplayName("Aktien"), TypeConverter(typeof(ExpandableObjectConverter))]
        public SaveGameStocks Stocks => new SaveGameStocks(this);

        internal uint StockField(CoachEnums.AddressKey key, int slot) => Field(key) + (uint)(slot * StockStride);
        internal string StockClub(int slot) => this.editor.ClubName((PlayerEnums.Country)this.B(this.StockField(CoachEnums.AddressKey.STOCK_COUNTRY, slot)), this.B(this.StockField(CoachEnums.AddressKey.STOCK_CLUB, slot)));
        #endregion
    }

    /// <summary>The six stock slots of a <see cref="SaveGameCoach"/>.</summary>
    public sealed class SaveGameStocks
    {
        private readonly SaveGameCoach coach;

        internal SaveGameStocks(SaveGameCoach coach)
        {
            this.coach = coach;
        }

        /// <inheritdoc/>
        public override string ToString() => $"{Coach.MaxStocks} Plätze";

        private SaveGameStock Get(int slot) => new SaveGameStock(this.coach, slot);

        /// <summary>Stock slot 1.</summary>
        [DisplayName("Aktie 1"), TypeConverter(typeof(ExpandableObjectConverter))] public SaveGameStock Stock1 => this.Get(0);
        /// <summary>Stock slot 2.</summary>
        [DisplayName("Aktie 2"), TypeConverter(typeof(ExpandableObjectConverter))] public SaveGameStock Stock2 => this.Get(1);
        /// <summary>Stock slot 3.</summary>
        [DisplayName("Aktie 3"), TypeConverter(typeof(ExpandableObjectConverter))] public SaveGameStock Stock3 => this.Get(2);
        /// <summary>Stock slot 4.</summary>
        [DisplayName("Aktie 4"), TypeConverter(typeof(ExpandableObjectConverter))] public SaveGameStock Stock4 => this.Get(3);
        /// <summary>Stock slot 5.</summary>
        [DisplayName("Aktie 5"), TypeConverter(typeof(ExpandableObjectConverter))] public SaveGameStock Stock5 => this.Get(4);
        /// <summary>Stock slot 6.</summary>
        [DisplayName("Aktie 6"), TypeConverter(typeof(ExpandableObjectConverter))] public SaveGameStock Stock6 => this.Get(5);
    }

    /// <summary>One stock slot of a <see cref="SaveGameCoach"/>: club read-only, shares and price editable.</summary>
    public sealed class SaveGameStock
    {
        private readonly SaveGameCoach coach;
        private readonly int slot;

        internal SaveGameStock(SaveGameCoach coach, int slot)
        {
            this.coach = coach;
            this.slot = slot;
        }

        /// <inheritdoc/>
        public override string ToString() => this.Shares == 0 ? "-" : $"{this.Shares} x {this.Club}";

        /// <summary>Club of the stock.</summary>
        [DisplayName("Verein"), ReadOnly(true)]
        public string Club => this.coach.StockClub(this.slot);
        /// <summary>Number of shares.</summary>
        [DisplayName("Anteile")]
        public int Shares { get => this.coach.I32(this.coach.StockField(CoachEnums.AddressKey.STOCK_SHARES, this.slot)); set => this.coach.SetI32(this.coach.StockField(CoachEnums.AddressKey.STOCK_SHARES, this.slot), value, nameof(SaveGameCoach.Stocks)); }
        /// <summary>Purchase price.</summary>
        [DisplayName("Kaufkurs")]
        public ushort Price { get => this.coach.U16(this.coach.StockField(CoachEnums.AddressKey.STOCK_PRICE, this.slot)); set => this.coach.SetU16(this.coach.StockField(CoachEnums.AddressKey.STOCK_PRICE, this.slot), value, nameof(SaveGameCoach.Stocks)); }
    }

    /// <summary>Savegame-wide values: date, main country, number of trainers.</summary>
    public sealed class SaveGameOverview : SaveGameEntry
    {
        private readonly SaveGameCalendar calendar;

        internal SaveGameOverview(SaveGameEditor editor)
            : base(editor.File, SaveGameFile.ModuleBase)
        {
            this.calendar = editor.Calendar;
            this.MainCountry = editor.MainCountry;
            this.FilePath = editor.File.Path;
        }

        /// <inheritdoc/>
        public override string ToString() => "Spielstand";

        /// <summary>The file.</summary>
        [SaveGameCategory("Spielstand"), DisplayName("Datei"), ReadOnly(true)]
        public string FilePath { get; }
        /// <summary>Current in-game date.</summary>
        [SaveGameCategory("Spielstand"), DisplayName("Datum"), ReadOnly(true)]
        public string Date => this.calendar.Today?.ToString("dd.MM.yyyy") ?? this.calendar.Year.ToString();
        /// <summary>Current calendar year (counts up on 1 January, see Settings.StartYearOffset).</summary>
        [SaveGameCategory("Spielstand"), DisplayName("Kalenderjahr"), ReadOnly(true)]
        public short CalendarYear => this.calendar.StartYear;
        /// <summary>Main country.</summary>
        [SaveGameCategory("Spielstand"), DisplayName("Hauptland"), ReadOnly(true), TypeConverter(typeof(DescriptionEnumConverter))]
        public PlayerEnums.Country MainCountry { get; }
        /// <summary>Number of human trainers.</summary>
        [SaveGameCategory("Spielstand"), DisplayName("Mitspieler"), ReadOnly(true)]
        public short Trainers => this.File.ReadInt16(SaveGameFile.ModuleBase + Settings.ActiveTrainerCountOffset);
        /// <summary>Names in the savegame's name pool.</summary>
        [SaveGameCategory("Spielstand"), DisplayName("Namen im Namensbestand"), ReadOnly(true)]
        public int Names => this.File.NameCount;
    }

    /// <summary>Shows a club ID as the club's name and offers the clubs of the same country to pick from.</summary>
    public sealed class ClubIdConverter : TypeConverter
    {
        private static IDictionary<byte, string> Names(ITypeDescriptorContext context) =>
            (context?.Instance as SaveGameClub)?.CountryClubNames ?? new Dictionary<byte, string>();

        /// <inheritdoc/>
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => true;
        /// <inheritdoc/>
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;
        /// <inheritdoc/>
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) =>
            new StandardValuesCollection(Names(context).OrderBy(c => c.Value, StringComparer.CurrentCulture).Select(c => c.Key).ToList());
        /// <inheritdoc/>
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) => sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        /// <inheritdoc/>
        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType) => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

        /// <inheritdoc/>
        public override object ConvertTo(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is byte id)
                return Names(context).TryGetValue(id, out string name) ? name : id.ToString();
            return base.ConvertTo(context, culture, value, destinationType);
        }

        /// <inheritdoc/>
        public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
        {
            if (value is string text)
            {
                foreach (KeyValuePair<byte, string> club in Names(context))
                {
                    if (club.Value == text)
                        return club.Key;
                }
                if (byte.TryParse(text, out byte id))
                    return id;
                throw new ArgumentException($"Kein Verein \"{text}\".");
            }
            return base.ConvertFrom(context, culture, value);
        }
    }
}
