using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// The failure paths of AppointmentPlan and AppointmentLocks: plans, lists and tables the game
    /// memory doesn't let be read, code that can't be written, list entries that only match in part,
    /// and the rarer appointment types (a moved training week, a match only in the trainer's plan,
    /// a test match with the own club as B or an open opponent).
    /// </summary>
    public class AppointmentCoverageGapTests
    {
        private const PlayerEnums.Country Country = AppointmentPlanTests.Country;
        private const byte OwnId = AppointmentPlanTests.OwnId;
        private const byte OpponentId = AppointmentPlanTests.OpponentId;

        [DllImport("kernel32.dll", EntryPoint = "OpenProcess")]
        private static extern IntPtr OpenProcessNative(uint access, bool inherit, int processId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        // PROCESS_VM_READ | PROCESS_QUERY_INFORMATION: reads only.
        private const uint ReadOnlyAccess = 0x0410;
        // ... plus PROCESS_VM_OPERATION: memory can be allocated and protected, but not written.
        private const uint NoWriteAccess = 0x0418;

        private static byte[] Le(short value) => BitConverter.GetBytes(value);

        private static short Count(FakeModule fake, uint offset) => BitConverter.ToInt16(fake.ReadModuleBytes(offset, 2), 0);

        private static void WriteList(FakeModule fake, uint offset, int head, params byte[][] entries)
        {
            fake.WriteModuleBytes(offset, Le((short)entries.Length));
            fake.WriteModuleBytes(offset + (uint)head, entries.SelectMany(e => e).ToArray());
        }

        // A #62 test match on `day` between the clubs a and b of Deutschland.
        private static void TestMatch(FakeModule fake, int index, short day, byte a = OwnId, byte b = OpponentId, uint shift = 0)
        {
            byte[] entry = new byte[0x14];
            entry[1] = 1;
            Le(day).CopyTo(entry, 2);
            entry[4] = (byte)Country;
            entry[6] = a;
            entry[8] = (byte)Country;
            entry[0xA] = b;
            fake.WriteModuleBytes(Settings.TestMatchTableOffset + shift + (uint)(index * 0x14), entry);
        }

        private static byte[] Pair(short day) =>
            Le(day).Concat(Le((short)Country)).Concat(Le(OwnId)).Concat(Le((short)Country)).Concat(Le(OpponentId)).ToArray();

        // Points the club cache's working copy of the own club at the module offset `copy`.
        private static void PointCopyAt(FakeModule fake, uint copy)
        {
            fake.WriteModuleBytes(Settings.ClubCacheTableOffset + Settings.ClubCachePointerOffset, BitConverter.GetBytes(fake.ModuleBase + copy));
        }

        // Runs `test` with the fake's process handle replaced by one with only `access`.
        private static void WithHandle(FakeModule fake, uint access, Action test)
        {
            IntPtr full = fake.Memory.mProc.Handle;
            IntPtr limited = OpenProcessNative(access, false, Process.GetCurrentProcess().Id);
            Assert.NotEqual(IntPtr.Zero, limited);
            try
            {
                fake.Memory.mProc.Handle = limited;
                test();
            }
            finally
            {
                fake.Memory.mProc.Handle = full;
                CloseHandle(limited);
            }
        }

        #region AppointmentLocks
        [Fact]
        public void SetWeeklyMode_WithoutMemoryForTheStub_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                AppointmentLocksTests.WriteOriginalCode(fake);
                WithHandle(fake, ReadOnlyAccess, () =>
                {
                    AppointmentLocks locks = new AppointmentLocks(fake.Memory);
                    Assert.Null(locks.EnsureInstalled());
                    Assert.False(locks.SetWeeklyMode(1));
                });
                Assert.Equal(Settings.WeeklyLimitEntry, fake.ReadModuleBytes(Settings.WeeklyLimitFunctionOffset, Settings.WeeklyLimitEntry.Length));
            }
        }

        [Fact]
        public void SetWeeklyMode_WhenTheJumpCannotBeWritten_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                AppointmentLocksTests.WriteOriginalCode(fake);
                WithHandle(fake, NoWriteAccess, () =>
                {
                    AppointmentLocks locks = new AppointmentLocks(fake.Memory);
                    Assert.Null(locks.EnsureInstalled());
                    Assert.False(locks.SetWeeklyMode(2));
                });
                Assert.Equal(Settings.WeeklyLimitEntry, fake.ReadModuleBytes(Settings.WeeklyLimitFunctionOffset, Settings.WeeklyLimitEntry.Length));
            }
        }

        [Fact]
        public void SetClubLimitLifted_WhenTheCodeCannotBeWritten_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                AppointmentLocksTests.WriteOriginalCode(fake);
                WithHandle(fake, ReadOnlyAccess, () =>
                {
                    AppointmentLocks locks = new AppointmentLocks(fake.Memory);
                    Assert.False(locks.SetClubLimitLifted(true));
                    Assert.False(locks.IsClubLimitLifted);
                    // Restoring what is already the game's needs no write.
                    Assert.True(locks.SetClubLimitLifted(false));
                });
                Assert.Equal(AppointmentLocks.ClubLimit, fake.ReadModuleBytes(Settings.ClubLimitSiteOffset + (uint)Settings.ClubLimitPattern.Length, 1)[0]);
            }
        }
        #endregion

        #region Reading
        [Fact]
        public void Read_WhenAPlanOfTheWorkingCopyCannotBeRead_IsNull()
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                // The trainer's plan, the manager's plan (the trainer's readable) and the training
                // units (both plans readable) each end up on a page that can't be read.
                fake.MakeModuleRegionUnreadable(0x502000, 1);
                fake.MakeModuleRegionUnreadable(0x504000, 1);
                fake.MakeModuleRegionUnreadable(0x506000, 1);

                PointCopyAt(fake, 0x502000);
                Assert.Null(AppointmentPlanTests.Read(fake));
                PointCopyAt(fake, 0x504000 - Settings.ClubManagerPlanOffset);
                Assert.Null(AppointmentPlanTests.Read(fake));
                PointCopyAt(fake, 0x506000 - Settings.ClubTrainingUnitsOffset);
                Assert.Null(AppointmentPlanTests.Read(fake));

                PointCopyAt(fake, AppointmentPlanTests.Copy);
                Assert.NotNull(AppointmentPlanTests.Read(fake));
            }
        }

        [Fact]
        public void Settings_PlanOffsets()
        {
            Assert.Equal(Settings.GlobalCounterOffset + 2, Settings.PlanReferenceDayOffset);
            Assert.Equal(Settings.ClubHumanOffset + 1, Settings.ClubManagerOffset);
        }
        #endregion

        #region Lists
        [Fact]
        public void Cancel_Negotiation_WithAnUnreadableList_Fails()
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                AppointmentPlanTests.Manager(fake, 4, 0, 0x0C, 0x70, 0x0A, (byte)Country, 0, OpponentId, 0);

                // The count can't be read.
                AppointmentPlan plan = AppointmentPlan.Read(fake.Memory, FixedAddresses.For(fake.Memory), Country, OwnId, 0x1000, fake.ModuleBase + AppointmentPlanTests.Extensions);
                Assert.False(plan.Cancel(plan.Appointments().Single()));

                // The count can, its entries can't.
                const uint list = 0x507FFC;
                fake.WriteModuleBytes(list, Le(1));
                fake.MakeModuleRegionUnreadable(list + 4, 1);
                plan = AppointmentPlan.Read(fake.Memory, FixedAddresses.For(fake.Memory), Country, OwnId, fake.ModuleBase + list, fake.ModuleBase + AppointmentPlanTests.Extensions);
                Assert.False(plan.Cancel(plan.Appointments().Single()));
                Assert.Equal(0x0C, AppointmentPlanTests.Read(fake).Appointments().Single().Kind.Code);
            }
        }

        [Fact]
        public void Cancel_Extension_SkipsTheEntriesOfOtherDaysAndPlayers()
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                byte[] earlier = Le(124).Concat(Le(0x1234)).ToArray();
                byte[] other = Le(125).Concat(Le(0x1235)).ToArray();
                byte[] mine = Le(125).Concat(Le(0x1234)).ToArray();
                WriteList(fake, AppointmentPlanTests.Extensions, 2, earlier, other, mine);
                AppointmentPlanTests.Manager(fake, 6, 0, 0x0A, 0x34, 0x12);
                AppointmentPlan plan = AppointmentPlanTests.Read(fake);

                Assert.True(plan.Cancel(plan.Appointments().Single()));
                Assert.Equal(2, Count(fake, AppointmentPlanTests.Extensions));
                Assert.Equal(earlier.Concat(other).ToArray(), fake.ReadModuleBytes(AppointmentPlanTests.Extensions + 2, 8));
            }
        }

        [Fact]
        public void Move_YouthTalk_SkipsTheEntriesOfOtherDaysAndPlayers()
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                byte[] Talk(ushort player, short day) => BitConverter.GetBytes(player).Concat(new byte[] { (byte)Country, 0 }).Concat(Le(OwnId)).Concat(Le(day)).ToArray();
                byte[] earlier = Talk(0x1234, 124), other = Talk(0x1235, 125);
                WriteList(fake, Settings.YouthTalkListOffset, 2, earlier, other, Talk(0x1234, 125));
                AppointmentPlanTests.Manager(fake, 6, 0, 0x0D, 0x34, 0x12);
                AppointmentPlan plan = AppointmentPlanTests.Read(fake);

                Assert.True(plan.Move(plan.Appointments().Single(), 8));
                Assert.Equal(earlier.Concat(other).Concat(Talk(0x1234, 127)).ToArray(), fake.ReadModuleBytes(Settings.YouthTalkListOffset + 2, 24));
            }
        }
        #endregion

        #region Test matches
        [Fact]
        public void TestMatch_WithAnUnreadableTable_CanNeitherBeAddedNorCancelled()
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                TestMatch(fake, 0, 150);
                AppointmentPlanTests.Trainer(fake, 31, 0, 0x14, 1, 0, (byte)Country, 0, OpponentId, 0);
                AppointmentPlan plan = AppointmentPlanTests.Read(fake);
                fake.MakeModuleRegionUnreadable(Settings.TestMatchTableOffset, 300 * 0x14);

                Assert.False(plan.AddTestMatch(10, Country, OpponentId, false, 0));
                Assert.False(plan.Cancel(plan.Appointments().Single()));
                Assert.Equal(0x14, AppointmentPlanTests.Read(fake).Appointments().Single().Kind.Code);
            }
        }

        [Fact]
        public void TestMatch_WithAnUnreadablePairList_IsStillAddedAndMoved()
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                AppointmentPlan plan = AppointmentPlanTests.Read(fake);
                fake.MakeModuleRegionUnreadable(Settings.FriendlyPairListOffset, 2);

                Assert.True(plan.AddTestMatch(10, Country, OpponentId, false, 0));
                Assert.Equal(1, fake.ReadModuleBytes(Settings.TestMatchTableOffset + 1, 1)[0]);

                plan = AppointmentPlanTests.Read(fake);
                Assert.True(plan.Move(plan.Appointments().Single(), 12));
                Assert.Equal(131, BitConverter.ToInt16(fake.ReadModuleBytes(Settings.TestMatchTableOffset + 2, 2), 0));
            }
        }

        [Fact]
        public void AddTestMatch_WithABrokenPairCount_AndAForeignOpponent_ArrangesTheMatchOnly()
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                fake.WriteModuleBytes(Settings.FriendlyPairListOffset, Le(-1));
                Assert.True(AppointmentPlanTests.Read(fake).AddTestMatch(10, PlayerEnums.Country.England, OwnId, false, 0));

                Assert.Equal(-1, Count(fake, Settings.FriendlyPairListOffset));
                byte[] entry = fake.ReadModuleBytes(Settings.TestMatchTableOffset, 0x14);
                Assert.Equal((byte)PlayerEnums.Country.England, entry[8]);
            }
        }

        [Fact]
        public void Move_TestMatch_WithTheOwnClubAsB_AndWithAnOpenOpponent()
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                // Lübeck invited: the own club is B.
                TestMatch(fake, 0, 150, a: OpponentId, b: OwnId);
                AppointmentPlanTests.Trainer(fake, 31, 0, 0x14, 1, 0, (byte)Country, 0, OpponentId, 0);
                AppointmentPlan plan = AppointmentPlanTests.Read(fake);
                Assert.True(plan.Move(plan.Appointments().Single(), 32));
                Assert.Equal(151, BitConverter.ToInt16(fake.ReadModuleBytes(Settings.TestMatchTableOffset + 2, 2), 0));

                // The plan doesn't name the opponent: any match of the own club that day.
                AppointmentPlanTests.Trainer(fake, 32, 0, 0x14, 1, 0);
                plan = AppointmentPlanTests.Read(fake);
                AppointmentPlan.Appointment open = plan.Appointments().Single();
                Assert.Null(open.Club);
                Assert.True(plan.Cancel(open));
                Assert.Equal(0, fake.ReadModuleBytes(Settings.TestMatchTableOffset + 1, 1)[0]);
            }
        }

        [Fact]
        public void Move_TestMatch_WhosePairEntriesCannotBeRead_StillMoves()
        {
            // Runtime data moved so that the pair list's count is the last word of a page, the
            // entries on the next page, which can't be read.
            uint shift = 0x304000 - 2 - Settings.FriendlyPairListOffset;
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                byte[] records = fake.ReadModuleBytes(Settings.ClubRecordTableOffset, 2 * (int)Settings.ClubRecordStride);
                fake.WriteModuleBytes(Settings.ClubRecordTableOffset + shift, records);
                fake.WriteModuleBytes(Settings.GlobalCounterOffset + shift, fake.ReadModuleBytes(Settings.GlobalCounterOffset, 4));
                fake.WriteModuleBytes(Settings.ClubCacheTableOffset + shift, fake.ReadModuleBytes(Settings.ClubCacheTableOffset, (int)Settings.ClubCacheStride));
                TestMatch(fake, 0, 150, shift: shift);
                fake.WriteModuleBytes(Settings.FriendlyPairListOffset + shift, Le(1));
                fake.MakeModuleRegionUnreadable(0x304000, 1);
                AppointmentPlanTests.Trainer(fake, 31, 0, 0x14, 1, 0, (byte)Country, 0, OpponentId, 0);
                FixedAddresses shifted = new FixedAddresses(shift, 0, null, null);

                AppointmentPlan plan = AppointmentPlan.Read(fake.Memory, shifted, Country, OwnId,
                    fake.ModuleBase + AppointmentPlanTests.Negotiations, fake.ModuleBase + AppointmentPlanTests.Extensions);
                Assert.True(plan.Move(plan.Appointments().Single(), 33));

                Assert.Equal(152, BitConverter.ToInt16(fake.ReadModuleBytes(Settings.TestMatchTableOffset + shift + 2, 2), 0));
                Assert.Equal(1, Count(fake, Settings.FriendlyPairListOffset + shift));
            }
        }
        #endregion

        #region Plan only
        [Fact]
        public void Move_TrainingWeek_TakesItsUnits()
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                for (int i = 0; i < 5; i++)
                {
                    AppointmentPlanTests.Trainer(fake, 8 + i, 0, 0x01, (byte)(i + 1), 0);
                    fake.WriteModuleBytes(AppointmentPlanTests.Copy + Settings.ClubTrainingUnitsOffset + (uint)((8 + i) * 3), new byte[] { (byte)(i + 1), 2, 3 });
                }
                AppointmentPlan plan = AppointmentPlanTests.Read(fake);
                AppointmentPlan.Appointment week = plan.Appointments().Single();
                Assert.Equal(5, week.Length);

                Assert.True(plan.Move(week, 22));

                for (int i = 0; i < 5; i++)
                {
                    Assert.Equal(new byte[] { (byte)(i + 1), 2, 3 }, fake.ReadModuleBytes(AppointmentPlanTests.Copy + Settings.ClubTrainingUnitsOffset + (uint)((22 + i) * 3), 3));
                    Assert.Equal(0x01, fake.ReadModuleBytes(AppointmentPlanTests.Copy + Settings.ClubTrainerPlanOffset + (uint)((22 + i) * 12) + 1, 1)[0]);
                }
            }
        }

        [Fact]
        public void Overview_MatchOnlyInTheTrainersPlan_CopiesItsOpponent()
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            {
                AppointmentPlanTests.Trainer(fake, 4, 0, 0x15, 0, 0, 14, 0, 0x2F, 0, 1, 0, 9, 9);
                Assert.Equal(new byte[] { 0, 0x15, 0, 0, 14, 0, 0x2F, 0, 1, 0, 0, 0 }, AppointmentPlanTests.Read(fake).Overview(4));
            }
        }
        #endregion
    }
}
