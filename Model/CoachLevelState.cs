using System;
using System.ComponentModel;

namespace A2G_Trainer.Model
{
    /// <summary>
    /// What the game's weekly level evaluation (FUN_00628420) looks at, and the manager's event and
    /// lock flags: the level block (working copy +0x1820.., record +0x1B14..), the series and the
    /// last level change (+0xAA/+0xAC/+0xB4), +0x1888/+0x188C (record +0x20B8/+0x20BC), the sabotage
    /// against him (record +0x1A60 + perpetrator * 4) and his naturalisation table (0x7DCF56 + ID * 0x7A).
    /// </summary>
    public class CoachLevelState : INotifyPropertyChanged
    {
        /// <summary>Raised whenever a value changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void Changed(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>Win streak, 2 per win (+0x1820); levels 0-8 need 6..24.</summary>
        public short WinStreak { get => this.winStreak; set { this.winStreak = value; this.Changed(nameof(this.WinStreak)); } }
        private short winStreak;
        /// <summary>Weeks in office (+0x1822).</summary>
        public short WeeksInOffice { get => this.weeksInOffice; set { this.weeksInOffice = value; this.Changed(nameof(this.WeeksInOffice)); } }
        private short weeksInOffice;
        /// <summary>Matches without defeat (+0x1824); levels 9 and 10 need 37.</summary>
        public short Unbeaten { get => this.unbeaten; set { this.unbeaten = value; this.Changed(nameof(this.Unbeaten)); } }
        private short unbeaten;
        /// <summary>League position at the end of the last season (+0x1826).</summary>
        public byte LastPosition { get => this.lastPosition; set { this.lastPosition = value; this.Changed(nameof(this.LastPosition)); } }
        private byte lastPosition;
        /// <summary>Promotions in a row (+0x1827).</summary>
        public byte PromotionsInARow { get => this.promotionsInARow; set { this.promotionsInARow = value; this.Changed(nameof(this.PromotionsInARow)); } }
        private byte promotionsInARow;
        /// <summary>Level-up marks (+0x1828): 0x08 cup quarter-final, 0x20 finances restored, 0x40 that mark already given, 0x80 Fortbildung passed, 0x100 semi-final, 0x200 tournament won.</summary>
        public ushort Marks { get => this.marks; set { this.marks = value; this.Changed(nameof(this.Marks)); } }
        private ushort marks;
        /// <summary>Big titles of the career (+0x182A); levels 11-14 need 15..50.</summary>
        public short BigTitles { get => this.bigTitles; set { this.bigTitles = value; this.Changed(nameof(this.BigTitles)); } }
        private short bigTitles;
        /// <summary>Season success (+0x182C): bit 0 promoted, bit 1 champion of the 1st league.</summary>
        public byte SeasonSuccess { get => this.seasonSuccess; set { this.seasonSuccess = value; this.Changed(nameof(this.SeasonSuccess)); } }
        private byte seasonSuccess;
        /// <summary>Credit (+0x182D), only lowered on a dismissal or resignation; counts for level 9.</summary>
        public byte Credit { get => this.credit; set { this.credit = value; this.Changed(nameof(this.Credit)); } }
        private byte credit;
        /// <summary>Pending reasons to lose a level (+0x182E): bit 0 season goal missed, 1 relegation place, 2 licence penalty, 3 debts.</summary>
        public byte DemotionReasons { get => this.demotionReasons; set { this.demotionReasons = value; this.Changed(nameof(this.DemotionReasons)); } }
        private byte demotionReasons;
        /// <summary>Dismissals last season (+0x182F).</summary>
        public byte DismissalsLastSeason { get => this.dismissalsLastSeason; set { this.dismissalsLastSeason = value; this.Changed(nameof(this.DismissalsLastSeason)); } }
        private byte dismissalsLastSeason;
        /// <summary>Dismissals this season (+0x1830).</summary>
        public byte DismissalsThisSeason { get => this.dismissalsThisSeason; set { this.dismissalsThisSeason = value; this.Changed(nameof(this.DismissalsThisSeason)); } }
        private byte dismissalsThisSeason;
        /// <summary>Resignations last season (+0x1831).</summary>
        public byte ResignationsLastSeason { get => this.resignationsLastSeason; set { this.resignationsLastSeason = value; this.Changed(nameof(this.ResignationsLastSeason)); } }
        private byte resignationsLastSeason;
        /// <summary>Resignations this season (+0x1832).</summary>
        public byte ResignationsThisSeason { get => this.resignationsThisSeason; set { this.resignationsThisSeason = value; this.Changed(nameof(this.ResignationsThisSeason)); } }
        private byte resignationsThisSeason;
        /// <summary>Series in points (+0xB4): win +2, draw +1, a defeat starts or extends a negative series by 2.</summary>
        public short SeriesPoints { get => this.seriesPoints; set { this.seriesPoints = value; this.Changed(nameof(this.SeriesPoints)); } }
        private short seriesPoints;
        /// <summary>Half-week of the last level change (+0xAA).</summary>
        public short LastChangeHalfWeek { get => this.lastChangeHalfWeek; set { this.lastChangeHalfWeek = value; this.Changed(nameof(this.LastChangeHalfWeek)); } }
        private short lastChangeHalfWeek;
        /// <summary>Season index of the last level change (+0xAC); a level-up needs 8 weeks since.</summary>
        public short LastChangeSeason { get => this.lastChangeSeason; set { this.lastChangeSeason = value; this.Changed(nameof(this.LastChangeSeason)); } }
        private short lastChangeSeason;

        /// <summary>Referee outbursts (+0x1888); more than 2 bring a reprimand once a season (no game code raises it).</summary>
        public byte RefereeOutbursts { get => this.refereeOutbursts; set { this.refereeOutbursts = value; this.Changed(nameof(this.RefereeOutbursts)); } }
        private byte refereeOutbursts;
        /// <summary>The flag bits (+0x188C): 1 reprimand this season, 2 state order received, 4 stadium message pending, 8 money taken this month.</summary>
        public uint Flags { get => this.flags; set { this.flags = value; this.Changed(nameof(this.Flags)); } }
        private uint flags;

        /// <summary>Whether another manager's sabotage against this one is running.</summary>
        public bool HasSabotage { get; set; }
        /// <summary>Whether the next save stops every sabotage against this manager.</summary>
        public bool StopSabotage { get => this.stopSabotage; set { this.stopSabotage = value; this.Changed(nameof(this.StopSabotage)); } }
        private bool stopSabotage;

        /// <summary>Naturalisation talks this season (more than 10 and the association refuses further ones).</summary>
        public short NaturalisationTalks { get; set; }
        /// <summary>Players who refused to change their nationality (bit 2 of their entry; it lasts about 10 seasons).</summary>
        public int NaturalisationRefusals { get; set; }
        /// <summary>Whether the next save clears the talks counter and every refusal.</summary>
        public bool ResetNaturalisation { get => this.resetNaturalisation; set { this.resetNaturalisation = value; this.Changed(nameof(this.ResetNaturalisation)); } }
        private bool resetNaturalisation;

        /// <summary>Whether bit <paramref name="mask"/> of <see cref="Marks"/> is set.</summary>
        public bool HasMark(ushort mask) => (this.marks & mask) != 0;
        /// <summary>Sets or clears bit <paramref name="mask"/> of <see cref="Marks"/>.</summary>
        public void SetMark(ushort mask, bool on) => this.Marks = (ushort)(on ? this.marks | mask : this.marks & ~mask);
        /// <summary>Whether bit <paramref name="mask"/> of <see cref="Flags"/> is set.</summary>
        public bool HasFlag(uint mask) => (this.flags & mask) != 0;
        /// <summary>Sets or clears bit <paramref name="mask"/> of <see cref="Flags"/>.</summary>
        public void SetFlag(uint mask, bool on) => this.Flags = on ? this.flags | mask : this.flags & ~mask;

        /// <summary>
        /// Sets what makes the next weekly evaluation raise the level, as far as the manager's values
        /// decide it: no reasons to lose one, the 8-week wait over, the mark 0x200 (counts on almost
        /// every level), a full win streak and unbeaten run. From level 4 the club must also be on target.
        /// </summary>
        public void PrepareLevelUp(short currentSeason)
        {
            this.DemotionReasons = 0;
            this.DismissalsLastSeason = this.DismissalsThisSeason = 0;
            this.ResignationsLastSeason = this.ResignationsThisSeason = 0;
            this.LastChangeSeason = (short)(currentSeason - 1);
            this.PromotionsInARow = 0;
            this.SetMark(0x200, true);
            this.WinStreak = 24;
            this.Unbeaten = Math.Max(this.Unbeaten, (short)37);
        }
    }
}
