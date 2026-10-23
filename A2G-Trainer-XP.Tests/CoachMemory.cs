using System;
using A2G_Trainer_XP.Controller;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Maps AddressPresets.COACH offsets (relative to the club pointer's target) onto a FakeModule's
    /// fixed Mitspieler working copy and saved record, mirroring CoachController's original-build
    /// addressing (see Settings.CoachSlotTableOffset).
    /// </summary>
    internal static class CoachMemory
    {
        private const uint PointerRelativeSlotBase = 0x34B00;
        private const uint CompetencyTableOffset = 0x36398;

        private static uint SlotOffset(string presetOffset) => Convert.ToUInt32(presetOffset, 16) - PointerRelativeSlotBase;

        private static uint Id(int trainerSlot) => (uint)trainerSlot + 1;

        public static uint WorkingCopyOffset(int trainerSlot, string presetOffset) =>
            Settings.CoachSlotTableOffset + Id(trainerSlot) * Settings.CoachSlotStride + SlotOffset(presetOffset);

        public static uint RecordOffset(int trainerSlot, string presetOffset)
        {
            uint slotOffset = SlotOffset(presetOffset);
            uint competencyStart = CompetencyTableOffset - PointerRelativeSlotBase;
            if (slotOffset >= competencyStart)
                return Settings.CoachCompetencyRecordOffset + Id(trainerSlot) * Settings.CoachCompetencyRecordStride + (slotOffset - competencyStart);

            return Settings.CoachRecordTableOffset + Id(trainerSlot) * Settings.CoachRecordStride + slotOffset;
        }

        public static void WriteWorkingCopy(FakeModule fake, string presetOffset, byte[] data, int trainerSlot = 0) =>
            fake.WriteModuleBytes(WorkingCopyOffset(trainerSlot, presetOffset), data);

        public static byte[] ReadWorkingCopy(FakeModule fake, string presetOffset, int length, int trainerSlot = 0) =>
            fake.ReadModuleBytes(WorkingCopyOffset(trainerSlot, presetOffset), length);

        public static byte[] ReadRecord(FakeModule fake, string presetOffset, int length, int trainerSlot = 0) =>
            fake.ReadModuleBytes(RecordOffset(trainerSlot, presetOffset), length);
    }
}
