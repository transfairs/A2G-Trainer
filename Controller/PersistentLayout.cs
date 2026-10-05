namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// The module-relative anchors of the save-persistent data (per-player record table, name-pool
    /// pointer, savegame year, active-trainer count) for the attached game build, and whether
    /// they may be used yet.
    /// </summary>
    /// <remarks>
    /// The original-build anchors were confirmed live and are trusted as-is. Every other build (GOG, and the
    /// 2007 CD release, which shares GOG's addressing) starts from an unconfirmed guess - the original
    /// anchors shifted by the same <see cref="Settings.GogOffset"/> the display-cache addresses
    /// use - which is never read from or written to until <see cref="PersistentLayoutCalibrator"/>
    /// has checked it (or found the real anchors) against the display cache. Until then the
    /// anchor properties return null and every caller falls back to the display cache, so a wrong
    /// guess can't show garbage values or write into unrelated game memory.
    /// </remarks>
    public sealed class PersistentLayout
    {
        /// <summary>The live-confirmed original-build anchors.</summary>
        public static PersistentLayout Original { get; } = new PersistentLayout(
            Settings.PlayerRecordTableOffset,
            Settings.NamePoolPointerOffset,
            Settings.AgeReferenceYearOffset,
            Settings.ActiveTrainerCountOffset,
            isVerified: true);

        /// <summary>A fresh, unverified guess for the GOG build: the original anchors shifted by <see cref="Settings.GogOffset"/>.</summary>
        public static PersistentLayout CreateGogGuess() => new PersistentLayout(
            Settings.PlayerRecordTableOffset + Settings.GogOffset,
            Settings.NamePoolPointerOffset + Settings.GogOffset,
            Settings.AgeReferenceYearOffset + Settings.GogOffset,
            Settings.ActiveTrainerCountOffset + Settings.GogOffset,
            isVerified: false);

        /// <summary>The starting layout for a freshly attached process of the given build.</summary>
        public static PersistentLayout For(bool isGog) => isGog ? CreateGogGuess() : Original;

        private readonly uint? playerRecordTableOffset;
        private readonly uint? namePoolPointerOffset;
        private readonly uint? ageReferenceYearOffset;
        private readonly uint? activeTrainerCountOffset;

        /// <summary>Creates a layout; null anchors are unknown for this build and never used.</summary>
        public PersistentLayout(uint? playerRecordTableOffset, uint? namePoolPointerOffset, uint? ageReferenceYearOffset, uint? activeTrainerCountOffset, bool isVerified)
        {
            this.playerRecordTableOffset = playerRecordTableOffset;
            this.namePoolPointerOffset = namePoolPointerOffset;
            this.ageReferenceYearOffset = ageReferenceYearOffset;
            this.activeTrainerCountOffset = activeTrainerCountOffset;
            this.IsVerified = isVerified;
        }

        /// <summary>True once the anchors are confirmed for the attached build (always for the original build).</summary>
        public bool IsVerified { get; }

        /// <summary>Module-relative start of the per-player record table, or null if not (yet) usable.</summary>
        public uint? PlayerRecordTableOffset => this.IsVerified ? this.playerRecordTableOffset : null;

        /// <summary>Module-relative offset of the name-pool base pointer, or null if not (yet) usable.</summary>
        public uint? NamePoolPointerOffset => this.IsVerified ? this.namePoolPointerOffset : null;

        /// <summary>Module-relative offset of the savegame's current in-game year, or null if not (yet) usable.</summary>
        public uint? AgeReferenceYearOffset => this.IsVerified ? this.ageReferenceYearOffset : null;

        /// <summary>Module-relative offset of the active human trainer count, or null if not (yet) usable.</summary>
        public uint? ActiveTrainerCountOffset => this.IsVerified ? this.activeTrainerCountOffset : null;

        // Fingerprint of the own-roster sample a calibration attempt last failed on (see
        // PlayerController.TryCalibrateLayout), so an unchanged roster doesn't trigger another
        // full module scan on every refresh.
        internal string FailedCalibrationSignature { get; set; }

        /// <inheritdoc/>
        public override string ToString() =>
            $"PlayerRecordTable={Format(this.playerRecordTableOffset)}, NamePoolPointer={Format(this.namePoolPointerOffset)}, " +
            $"AgeReferenceYear={Format(this.ageReferenceYearOffset)}, ActiveTrainerCount={Format(this.activeTrainerCountOffset)}, Verified={this.IsVerified}";

        private static string Format(uint? offset) => offset.HasValue ? "0x" + offset.Value.ToString("X") : "unbekannt";
    }
}
