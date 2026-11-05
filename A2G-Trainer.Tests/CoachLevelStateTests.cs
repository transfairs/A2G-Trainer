using System;
using System.Collections.Generic;
using System.Linq;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for the level evaluation's values, the manager's flags, sabotage and naturalisation (CoachLevelState, CoachController).</summary>
    public class CoachLevelStateTests
    {
        private static uint WorkingCopy(uint slotOffset) => Settings.CoachSlotTableOffset + 1 * Settings.CoachSlotStride + slotOffset;
        private static uint Record(uint recordOffset) => Settings.CoachRecordTableOffset + 1 * Settings.CoachRecordStride + recordOffset;
        private static uint Naturalisation => Settings.NaturalisationTableOffset + 1 * Settings.NaturalisationTableStride;

        private static CoachController Read(FakeModule fake) => new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

        [Fact]
        public void GetEntity_ReadsTheLevelBlockFlagsSabotageAndNaturalisation()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(WorkingCopy(0x1820), new byte[] { 12, 0, 30, 0, 5, 0, 3, 1, 0x28, 0x02, 7, 0, 3, 1, 0x0A, 1, 2, 0, 1, 2 });
                fake.WriteModuleBytes(WorkingCopy(0xAA), new byte[] { 40, 0, 2, 0 });
                fake.WriteModuleBytes(WorkingCopy(0xB4), BitConverter.GetBytes((short)-6));
                fake.WriteModuleBytes(WorkingCopy(0x1888), new byte[] { 3, 0, 0, 0, 0x0C, 0, 0, 0 });
                // Manager 3 sabotages manager 1; two naturalisation talks, one refused.
                fake.WriteModuleBytes(Record(0x1A60 + 3 * 4), new byte[] { 2, 4, 0, 0 });
                fake.WriteModuleBytes(Naturalisation, new byte[] { 2, 0, 0x10, 0, 4, 0, 0, 0, 0x11, 0, 2, 0, 0, 0 });

                CoachLevelState state = Read(fake).Coach.LevelState;

                Assert.Equal((short)12, state.WinStreak);
                Assert.Equal((short)30, state.WeeksInOffice);
                Assert.Equal((short)5, state.Unbeaten);
                Assert.Equal((byte)3, state.LastPosition);
                Assert.Equal((byte)1, state.PromotionsInARow);
                Assert.Equal((ushort)0x228, state.Marks);
                Assert.True(state.HasMark(0x200));
                Assert.False(state.HasMark(0x80));
                Assert.Equal((short)7, state.BigTitles);
                Assert.Equal((byte)3, state.SeasonSuccess);
                Assert.Equal((byte)1, state.Credit);
                Assert.Equal((byte)0x0A, state.DemotionReasons);
                Assert.Equal((byte)1, state.DismissalsLastSeason);
                Assert.Equal((byte)2, state.DismissalsThisSeason);
                Assert.Equal((byte)0, state.ResignationsLastSeason);
                Assert.Equal((byte)1, state.ResignationsThisSeason);
                Assert.Equal((short)40, state.LastChangeHalfWeek);
                Assert.Equal((short)2, state.LastChangeSeason);
                Assert.Equal((short)-6, state.SeriesPoints);
                Assert.Equal((byte)3, state.RefereeOutbursts);
                Assert.True(state.HasFlag(0x4));
                Assert.True(state.HasFlag(0x8));
                Assert.False(state.HasFlag(0x1));
                Assert.True(state.HasSabotage);
                Assert.Equal((short)2, state.NaturalisationTalks);
                Assert.Equal(1, state.NaturalisationRefusals);
            }
        }

        [Fact]
        public void Save_WritesTheLevelBlockAndFlagsToBothCopies_AndKeepsTheFortbildungResult()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(WorkingCopy(0x1833), new byte[] { 2 });
                CoachController controller = Read(fake);
                CoachLevelState state = controller.Coach.LevelState;
                state.PrepareLevelUp(5);
                state.BigTitles = 15;
                state.SetFlag(0x8, true);
                state.SetFlag(0x8, false);
                state.SetFlag(0x2, true);
                state.RefereeOutbursts = 4;
                state.SeriesPoints = 18;

                controller.Save();

                foreach (Func<uint, int, byte[]> read in new Func<uint, int, byte[]>[]
                {
                    (offset, length) => fake.ReadModuleBytes(WorkingCopy(offset), length),
                    (offset, length) => fake.ReadModuleBytes(Record(offset == 0x1888 || offset == 0x188C ? offset + 0x830 : offset >= 0x1820 && offset < 0x1834 ? offset + 0x2F4 : offset), length)
                })
                {
                    byte[] block = read(0x1820, 0x14);
                    Assert.Equal((short)24, BitConverter.ToInt16(block, 0));
                    Assert.Equal((short)37, BitConverter.ToInt16(block, 4));
                    // The unchanged Fortbildung result (2) doesn't give mark 0x80 again; the level-up mark 0x200.
                    Assert.Equal((ushort)0x200, BitConverter.ToUInt16(block, 8));
                    Assert.Equal((short)15, BitConverter.ToInt16(block, 0xA));
                    Assert.Equal((byte)2, block[0x13]);
                    Assert.Equal((short)4, BitConverter.ToInt16(read(0xAC, 2), 0));
                    Assert.Equal((short)18, BitConverter.ToInt16(read(0xB4, 2), 0));
                    Assert.Equal((byte)4, read(0x1888, 1)[0]);
                    Assert.Equal(2u, BitConverter.ToUInt32(read(0x188C, 4), 0));
                }
            }
        }

        [Fact]
        public void Save_StopsSabotageAndResetsNaturalisation_OnlyWhenAsked()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(Record(0x1A60 + 2 * 4), new byte[] { 3, 2, 0x34, 0x12 });
                fake.WriteModuleBytes(Naturalisation, new byte[] { 11, 0, 0x10, 0, 7, 0, 0, 0 });
                CoachController controller = Read(fake);

                controller.Save();
                Assert.Equal((byte)3, fake.ReadModuleBytes(Record(0x1A60 + 2 * 4), 1)[0]);
                Assert.Equal((byte)11, fake.ReadModuleBytes(Naturalisation, 1)[0]);

                controller.Coach.LevelState.StopSabotage = true;
                controller.Coach.LevelState.ResetNaturalisation = true;
                controller.Save();

                Assert.Equal(new byte[4], fake.ReadModuleBytes(Record(0x1A60 + 2 * 4), 4));
                // The talks counter is 0, the entry stays without its refusal bit.
                Assert.Equal(new byte[] { 0, 0, 0x10, 0, 3, 0 }, fake.ReadModuleBytes(Naturalisation, 6));
                CoachLevelState state = controller.Coach.LevelState;
                Assert.False(state.HasSabotage);
                Assert.False(state.StopSabotage);
                Assert.False(state.ResetNaturalisation);
                Assert.Equal(0, state.NaturalisationRefusals);
            }
        }

        [Fact]
        public void LevelState_RaisesItsProperties()
        {
            CoachLevelState state = new CoachLevelState();
            List<string> raised = new List<string>();
            state.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            state.WeeksInOffice = 1;
            state.LastPosition = 1;
            state.SeasonSuccess = 1;
            state.Credit = 1;
            state.LastChangeHalfWeek = 1;
            state.SetMark(0x08, true);
            state.SetMark(0x08, false);

            Assert.Equal(new[] { "WeeksInOffice", "LastPosition", "SeasonSuccess", "Credit", "LastChangeHalfWeek", "Marks", "Marks" }, raised);
            Assert.Equal((ushort)0, state.Marks);
        }
    }
}
