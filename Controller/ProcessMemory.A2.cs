using A2G_Trainer_XP.Controller;

namespace AnstossTrainer.Shared
{
    // Anstoss 2 Gold's part of the shared ProcessMemory: which build is attached and where its
    // persistent data lives. The shared part (Shared/ProcessMemory.cs) knows nothing about either.
    public partial class ProcessMemory
    {
        // Which build's persistent-data anchors apply to the attached process. Defaults to the
        // trusted original-build layout; ProcessController replaces it on every attach (see PersistentLayout).
        /// <summary>The persistent-data anchors for the attached game build.</summary>
        public PersistentLayout Layout { get; set; } = PersistentLayout.Original;

        // Set by ProcessController on attach; null until then (and in tests), see FixedAddresses.For.
        /// <summary>The identified build of the attached process, or null if not (yet) known.</summary>
        internal GameBuild Build { get; set; }
    }
}
