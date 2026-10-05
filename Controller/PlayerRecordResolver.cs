using System;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Resolves absolute memory addresses within the permanent per-player record table (see
    /// PersistentLayout.PlayerRecordTableOffset), used for the handful of fields confirmed to
    /// persist there instead of only in the transient display cache.
    /// </summary>
    public class PlayerRecordResolver
    {
        private readonly ProcessMemory memory;
        private readonly uint? tableOffset;

        /// <summary>Creates a resolver that computes addresses through the given process memory accessor and its current <see cref="ProcessMemory.Layout"/>.</summary>
        public PlayerRecordResolver(ProcessMemory memory) : this(memory, memory.Layout)
        {
        }

        /// <summary>Creates a resolver for an explicit layout (e.g. a calibration candidate) instead of the memory's current one.</summary>
        public PlayerRecordResolver(ProcessMemory memory, PersistentLayout layout)
        {
            this.memory = memory;
            this.tableOffset = layout.PlayerRecordTableOffset;
        }

        /// <summary>False when the record table isn't known/confirmed for the attached build - callers must then stick to the display cache.</summary>
        public bool IsAvailable => this.tableOffset.HasValue;

        /// <summary>Absolute address of <paramref name="fieldOffset"/> within the given player's persistent record.</summary>
        public uint GetFieldAddress(ushort playerId, uint fieldOffset)
        {
            if (!this.tableOffset.HasValue)
                throw new InvalidOperationException("The persistent player record table isn't available for the attached game build.");

            return this.memory.ModuleBase + this.tableOffset.Value + (uint)playerId * Settings.PlayerRecordStride + fieldOffset;
        }
    }
}
