namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Resolves absolute memory addresses within the permanent per-player record table (see
    /// Settings.PlayerRecordTableOffset), used for the handful of fields confirmed to persist
    /// there instead of only in the transient display cache.
    /// </summary>
    public class PlayerRecordResolver
    {
        private readonly ProcessMemory memory;

        /// <summary>Creates a resolver that computes addresses through the given process memory accessor.</summary>
        public PlayerRecordResolver(ProcessMemory memory)
        {
            this.memory = memory;
        }

        /// <summary>Absolute address of <paramref name="fieldOffset"/> within the given player's persistent record.</summary>
        public uint GetFieldAddress(ushort playerId, uint fieldOffset)
        {
            return this.memory.ModuleBase + Settings.PlayerRecordTableOffset + (uint)playerId * Settings.PlayerRecordStride + fieldOffset;
        }
    }
}
