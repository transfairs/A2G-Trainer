using System.ComponentModel;

namespace A2G_Trainer.Model
{
    /// <summary>
    /// A manager's titles and setbacks as the game's "Stationen und Erfolge" screen shows them
    /// (FUN_00512780): Int16 counters at the start of the per-ID block 0x7DB180 + ID * 0x610 (saved
    /// record only, FUN_0057d390/FUN_0057d3f0), raised by FUN_005cc190.
    /// </summary>
    public class CoachAchievements : INotifyPropertyChanged
    {
        /// <summary>Raised whenever one of the counters changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        /// <summary>Block offset of each counter, in the order of <see cref="Counters"/>.</summary>
        internal static readonly uint[] Offsets = { 0x00, 0x10, 0x0C, 0x0E, 0x04, 0x06, 0x02, 0x08, 0x0A, 0x60C, 0x60E, 0x1A6 };

        /// <summary>The game's label of each counter (texts 1124-1133, 6359/6360), in the order of <see cref="Offsets"/>.</summary>
        public static readonly string[] Names =
        {
            "Meister", "UEFA-Cup erreicht", "Aufstiege", "Abstiege", "Pokalsiege", "Pokalfinale verloren",
            "Europapokale", "Supercups", "Europ. Supercup/Weltcup", "Weltmeister", "Europameister", "Entlassungen"
        };

        /// <summary>The counters, indexed like <see cref="Names"/>.</summary>
        public short[] Counters { get; } = new short[Offsets.Length];

        /// <summary>Sets one counter and notifies the binding.</summary>
        public void Set(int index, short value)
        {
            this.Counters[index] = value;
            this.OnPropertyChanged(Names[index]);
        }
    }
}
