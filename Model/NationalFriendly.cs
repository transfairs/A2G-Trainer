using System.ComponentModel;

namespace A2G_Trainer.Model
{
    /// <summary>
    /// One of the national coach's three friendlies: the chosen opponent record at +0x36 + n * 6 in
    /// the coach's friendly block (Settings.NationalFriendlyBlockOffset): byte country, byte 0,
    /// UInt16 200 (the national teams' club ID), UInt16 0 = at home. On the match day the game plays
    /// exactly this record (FUN_005aaaa0); there is no separate fixture.
    /// </summary>
    public class NationalFriendly : INotifyPropertyChanged
    {
        /// <summary>Raised whenever opponent or venue change.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name) => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        /// <summary>Season days of the three friendlies (the game's fixed calendar).</summary>
        public static readonly short[] Days = { 101, 164, 220 };

        /// <summary>Which friendly (0..2).</summary>
        public int Index { get; set; }
        /// <summary>Season day it's played on.</summary>
        public short Day => Days[this.Index];
        /// <summary>The opponent; <see cref="PlayerEnums.Country.Sonstige"/> means no match.</summary>
        public PlayerEnums.Country Opponent { get => this.opponent; set { this.opponent = value; this.OnPropertyChanged(nameof(this.Opponent)); } }
        private PlayerEnums.Country opponent;
        /// <summary>Whether the coach's team plays at home.</summary>
        public bool AtHome { get => this.atHome; set { this.atHome = value; this.OnPropertyChanged(nameof(this.AtHome)); } }
        private bool atHome;
        /// <summary>
        /// Whether it can't be changed any more: already played, or the squads are named (the game
        /// nominates them 3 days before and locks its own choice 4 days before).
        /// </summary>
        public bool IsLocked { get; set; }
    }
}
