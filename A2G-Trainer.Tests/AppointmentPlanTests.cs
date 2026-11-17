using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for AppointmentPlan: reading both plans into appointments, cancelling and moving them in
    /// the list they live in (#70, #71, #90, #91, #62/#113) and in the plans, adding camps, test matches
    /// and the club's own events, and the week overview as the game builds it.
    /// </summary>
    public class AppointmentPlanTests
    {
        internal const PlayerEnums.Country Country = PlayerEnums.Country.Deutschland;
        internal const byte OwnId = 0;
        internal const byte OpponentId = 1;
        internal const short ReferenceDay = 119;
        // The club's working copy in the club cache, and the negotiations (#70) and contract extensions
        // (#71), which the game keeps past the default fake module, in its free end.
        internal const uint Copy = 0x500000;
        internal const uint Negotiations = 0x51A000;
        internal const uint Extensions = 0x51E000;
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        private static uint Record(int index) => Settings.ClubRecordTableOffset + (uint)index * Settings.ClubRecordStride;
        internal static uint DataBlock => Settings.ClubPlanDataOffset;
        private static uint WeekSlot => Settings.CoachSlotTableOffset + Settings.CoachSlotStride + Settings.CoachSlotWeekOffset;
        private static uint WeekRecord => Settings.CoachRecordTableOffset + Settings.CoachRecordStride + Settings.CoachRecordWeekOffset;

        /// <summary>Kiel (human, data slot 1, manager 1) and Lübeck in Deutschland, today day 121 of the week from day 119.</summary>
        internal static FakeModule NewFake(short today = ReferenceDay + 2, bool cached = true)
        {
            FakeModule fake = new FakeModule();
            fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + (uint)Country, new byte[] { 0 });
            fake.WriteModuleBytes(Settings.LeagueClubCountTableOffset, BitConverter.GetBytes((short)2));
            fake.WriteModuleBytes(Settings.LeagueBaseTableOffset, BitConverter.GetBytes((short)0));
            WriteClub(fake, 0, "Kiel", OwnId);
            WriteClub(fake, 1, "Lübeck", OpponentId);
            fake.WriteModuleBytes(Record(0) + Settings.ClubRecordDataSlotOffset, new byte[] { 1 });
            fake.WriteModuleBytes(Settings.GlobalCounterOffset, BitConverter.GetBytes(today).Concat(BitConverter.GetBytes(ReferenceDay)).ToArray());
            if (cached)
            {
                byte[] entry = new byte[Settings.ClubCacheStride];
                entry[0] = 1;
                entry[1] = (byte)Country;
                entry[2] = OwnId;
                BitConverter.GetBytes(fake.ModuleBase + Copy).CopyTo(entry, (int)Settings.ClubCachePointerOffset);
                fake.WriteModuleBytes(Settings.ClubCacheTableOffset, entry);
                fake.WriteModuleBytes(Copy + Settings.ClubHumanOffset, new byte[] { 1, 1 });
            }
            return fake;
        }

        private static void WriteClub(FakeModule fake, int index, string name, byte id)
        {
            fake.WriteModuleBytes(Record(index), Latin1.GetBytes(name + "\0"));
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordIdOffset, new[] { id });
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordCountryOffset, new[] { (byte)Country });
        }

        internal static void Trainer(FakeModule fake, int day, params byte[] entry)
        {
            byte[] full = new byte[AppointmentPlan.TrainerEntrySize];
            entry.CopyTo(full, 0);
            fake.WriteModuleBytes(Copy + Settings.ClubTrainerPlanOffset + (uint)(day * AppointmentPlan.TrainerEntrySize), full);
        }

        internal static void Manager(FakeModule fake, int day, params byte[] entry)
        {
            byte[] full = new byte[AppointmentPlan.ManagerEntrySize];
            entry.CopyTo(full, 0);
            fake.WriteModuleBytes(Copy + Settings.ClubManagerPlanOffset + (uint)(day * AppointmentPlan.ManagerEntrySize), full);
        }

        private static byte[] TrainerAt(FakeModule fake, int day, uint basis = Copy, uint offset = 0xA30) =>
            fake.ReadModuleBytes(basis + offset + (uint)(day * AppointmentPlan.TrainerEntrySize), AppointmentPlan.TrainerEntrySize);

        private static byte[] ManagerAt(FakeModule fake, int day) =>
            fake.ReadModuleBytes(Copy + Settings.ClubManagerPlanOffset + (uint)(day * AppointmentPlan.ManagerEntrySize), AppointmentPlan.ManagerEntrySize);

        private static byte[] Units(FakeModule fake, int day) =>
            fake.ReadModuleBytes(Copy + Settings.ClubTrainingUnitsOffset + (uint)(day * AppointmentPlan.UnitsSize), AppointmentPlan.UnitsSize);

        internal static AppointmentPlan Read(FakeModule fake) =>
            AppointmentPlan.Read(fake.Memory, FixedAddresses.For(fake.Memory), Country, OwnId, fake.ModuleBase + Negotiations, fake.ModuleBase + Extensions);

        private static byte[] Le(short value) => BitConverter.GetBytes(value);

        // A #70 entry: type 3, day, 2 parties, player, selling club Lübeck.
        private static byte[] Negotiation(short day, ushort player)
        {
            byte[] entry = new byte[0x48];
            new byte[] { 3, 0 }.CopyTo(entry, 0);
            Le(day).CopyTo(entry, 2);
            Le(2).CopyTo(entry, 4);
            BitConverter.GetBytes(player).CopyTo(entry, 6);
            entry[8] = (byte)Country;
            entry[0xA] = OpponentId;
            return entry;
        }

        private static void WriteList(FakeModule fake, uint offset, int head, params byte[][] entries)
        {
            fake.WriteModuleBytes(offset, Le((short)entries.Length));
            fake.WriteModuleBytes(offset + (uint)head, entries.SelectMany(e => e).ToArray());
        }

        private static short Count(FakeModule fake, uint offset) => BitConverter.ToInt16(fake.ReadModuleBytes(offset, 2), 0);

        // A #62 test match on `day`: status, the own club as A, Lübeck as B.
        private static void TestMatch(FakeModule fake, int index, short day, byte status = 1, byte homeOfB = 0, byte fee = 0)
        {
            byte[] entry = new byte[0x14];
            entry[0] = 0x8C;
            entry[1] = status;
            Le(day).CopyTo(entry, 2);
            entry[4] = (byte)Country;
            entry[6] = OwnId;
            entry[8] = (byte)Country;
            entry[0xA] = OpponentId;
            entry[0x12] = homeOfB;
            entry[0x13] = fee;
            fake.WriteModuleBytes(Settings.TestMatchTableOffset + (uint)(index * 0x14), entry);
        }

        private static byte[] Pair(short day) =>
            Le(day).Concat(Le((short)Country)).Concat(Le(OwnId)).Concat(Le((short)Country)).Concat(Le(OpponentId)).ToArray();

        #region Reading
        [Fact]
        public void Read_ClubWithoutAHumanManager_OrWithoutAddresses_IsNull()
        {
            using (FakeModule fake = NewFake())
            {
                Assert.Null(AppointmentPlan.Read(fake.Memory, FixedAddresses.For(fake.Memory), Country, OpponentId));
                Assert.NotNull(AppointmentPlan.Read(fake.Memory, FixedAddresses.For(fake.Memory), Country, OwnId));
                Assert.Null(AppointmentPlan.Read(fake.Memory, null, Country, OwnId));
                // A club without a record.
                Assert.Null(AppointmentPlan.Read(fake.Memory, FixedAddresses.For(fake.Memory), Country, 7));
                // A slot the game doesn't have.
                fake.WriteModuleBytes(Record(0) + Settings.ClubRecordDataSlotOffset, new byte[] { 5 });
                Assert.Null(Read(fake));
            }
        }

        [Fact]
        public void Read_KnowsTodayAndTheReferenceDay()
        {
            using (FakeModule fake = NewFake())
            {
                AppointmentPlan plan = Read(fake);
                Assert.Equal(ReferenceDay, plan.ReferenceDay);
                Assert.Equal(ReferenceDay + 2, plan.Today);
                Assert.Equal(2, plan.TodayIndex);
                Assert.Equal(Country, plan.Country);
                Assert.Equal(OwnId, plan.ClubId);
                Assert.Empty(plan.Appointments());
            }
        }

        [Fact]
        public void Appointments_GroupDaysAndPlans()
        {
            using (FakeModule fake = NewFake())
            {
                // The second half of a camp (days 4-7 of it), a whole camp from day 7, a test match,
                // a match in both plans, an open day in both, a negotiation and a court date.
                for (int i = 0; i < 4; i++)
                    Trainer(fake, i, 0, 3, (byte)(4 + i), 0, 1, 0, 0, 0, 0, 0, 4, 0);
                for (int i = 0; i < 7; i++)
                    Trainer(fake, 7 + i, 0, 3, (byte)(1 + i), 0, 0, 0, 0, 0, 0, 0, 2, 0);
                Trainer(fake, 15, 0, 0x14, 1, 0, (byte)Country, 0, OpponentId, 0, 1, 0, 2, 0);
                Trainer(fake, 20, 0, 0x15, 0, 0, (byte)Country, 0, OpponentId, 0, 0, 0);
                Manager(fake, 20, 0, 0x15);
                Trainer(fake, 21, 0, 0x19, 1, 0);
                Manager(fake, 21, 0, 0x19);
                Manager(fake, 4, 0, 0x0C, 0x70, 0x0A, (byte)Country, 0, OpponentId, 0);
                Manager(fake, 5, 0, 0x1B, 1);

                List<AppointmentPlan.Appointment> list = Read(fake).Appointments();

                Assert.Equal(new[] { 0x03, 0x0C, 0x1B, 0x03, 0x14, 0x15, 0x19 }, list.Select(a => (int)a.Kind.Code));
                AppointmentPlan.Appointment first = list[0];
                Assert.True(first.BeganBefore);
                Assert.Equal(4, first.Length);
                Assert.Equal(1, first.CampKind);
                Assert.Equal(4, first.CampLocation);
                AppointmentPlan.Appointment camp = list[3];
                Assert.Equal(7, camp.Day);
                Assert.Equal(7, camp.Length);
                Assert.False(camp.BeganBefore);
                Assert.Equal(2, camp.CampLocation);
                Assert.Equal((ushort)0x0A70, list[1].PlayerId);
                Assert.Equal(new KeyValuePair<PlayerEnums.Country, ushort>(Country, OpponentId), list[1].Club);
                Assert.True(list[1].InManagerPlan);
                Assert.False(list[1].InTrainerPlan);
                Assert.Equal(new KeyValuePair<PlayerEnums.Country, ushort>(Country, OpponentId), list[4].Club);
                Assert.True(list[5].InTrainerPlan && list[5].InManagerPlan);
                Assert.True(list[6].InTrainerPlan && list[6].InManagerPlan);
                Assert.Null(list[6].Club);
                Assert.Equal((ushort)0, list[6].PlayerId);
            }
        }

        [Fact]
        public void Appointments_TwoCampsInARow_AreTwo_AndAnOpenOpponentIsNoClub()
        {
            using (FakeModule fake = NewFake())
            {
                for (int i = 0; i < 14; i++)
                    Trainer(fake, 7 + i, 0, 3, (byte)(1 + i % 7), 0);
                Trainer(fake, 22, 0, 0x14, 1, 0);
                // A sick visit only in the trainer's plan names its player there.
                Trainer(fake, 23, 0, 0x04, 0, 0, 0x34, 0x12);

                List<AppointmentPlan.Appointment> list = Read(fake).Appointments();

                Assert.Equal(new[] { 7, 14, 22, 23 }, list.Select(a => a.Day));
                Assert.Null(list[2].Club);
                Assert.Equal((ushort)0x1234, list[3].PlayerId);
            }
        }

        [Fact]
        public void Read_WithoutAWorkingCopy_TakesTheDataBlock_AndWritesOnlyThere()
        {
            using (FakeModule fake = NewFake(cached: false))
            {
                byte[] entry = new byte[AppointmentPlan.ManagerEntrySize];
                entry[1] = 0x12;
                fake.WriteModuleBytes(DataBlock + Settings.ClubPlanDataManagerOffset + 10 * 8, entry);

                AppointmentPlan plan = Read(fake);
                AppointmentPlan.Appointment fanClub = Assert.Single(plan.Appointments());
                Assert.Equal(10, fanClub.Day);

                Assert.True(plan.Cancel(fanClub));
                Assert.Equal(0, fake.ReadModuleBytes(DataBlock + Settings.ClubPlanDataManagerOffset + 10 * 8 + 1, 1)[0]);
            }
        }

        [Fact]
        public void KindOf_AnUnknownType_IsReadOnly()
        {
            AppointmentPlan.Kind kind = AppointmentPlan.KindOf(0x30);
            Assert.Equal("Termin (Art 48)", kind.Name);
            Assert.Equal(AppointmentPlan.Source.Game, kind.Source);
            Assert.Equal(7, AppointmentPlan.KindOf(0x03).Length);
            Assert.All(AppointmentPlan.AddableKinds, k => Assert.NotEqual(AppointmentPlan.Source.Game, AppointmentPlan.KindOf(k).Source));
            Assert.Equal(23, AppointmentPlan.CampLocationNames.Length);
            Assert.All(AppointmentPlan.CampUnits, u => Assert.Equal(21, u.Length));
        }
        #endregion

        #region Cancelling
        [Fact]
        public void Cancel_Negotiation_TakesItOutOfItsList_ThePlanAndTheWeekOverview()
        {
            using (FakeModule fake = NewFake())
            {
                WriteList(fake, Negotiations, 4, Negotiation(123, 0x0A70), Negotiation(124, 0x0A71), Negotiation(130, 0x0B00));
                Manager(fake, 4, 0, 0x0C, 0x70, 0x0A, (byte)Country, 0, OpponentId, 0);
                AppointmentPlan plan = Read(fake);

                Assert.True(plan.Cancel(plan.Appointments().Single()));

                Assert.Equal(2, Count(fake, Negotiations));
                Assert.Equal(Negotiation(124, 0x0A71), fake.ReadModuleBytes(Negotiations + 4, 0x48));
                Assert.Equal(Negotiation(130, 0x0B00), fake.ReadModuleBytes(Negotiations + 4 + 0x48, 0x48));
                Assert.Equal(0, ManagerAt(fake, 4)[1]);
                Assert.Equal(0, fake.ReadModuleBytes(DataBlock + Settings.ClubPlanDataManagerOffset + 4 * 8 + 1, 1)[0]);
                Assert.Equal(new byte[12], fake.ReadModuleBytes(WeekSlot + 4 * 12, 12));
                Assert.Equal(new byte[12], fake.ReadModuleBytes(WeekRecord + 4 * 12, 12));
                Assert.Empty(Read(fake).Appointments());
            }
        }

        [Fact]
        public void Cancel_WithoutItsListEntry_ChangesNothing()
        {
            using (FakeModule fake = NewFake())
            {
                WriteList(fake, Negotiations, 4, Negotiation(124, 0x0A70));
                Manager(fake, 4, 0, 0x0C, 0x70, 0x0A);
                AppointmentPlan plan = Read(fake);

                Assert.False(plan.Cancel(plan.Appointments().Single()));
                Assert.Equal(0x0C, ManagerAt(fake, 4)[1]);
                Assert.Equal(1, Count(fake, Negotiations));

                // An empty or broken list has nothing to take out either.
                fake.WriteModuleBytes(Negotiations, Le(0));
                Assert.False(Read(fake).Cancel(plan.Appointments().Single()));
                fake.WriteModuleBytes(Negotiations, Le(201));
                Assert.False(Read(fake).Cancel(plan.Appointments().Single()));
            }
        }

        [Fact]
        public void Cancel_LastNegotiation_OnlyCountsDown()
        {
            using (FakeModule fake = NewFake())
            {
                WriteList(fake, Negotiations, 4, Negotiation(130, 0x0B00), Negotiation(123, 0x0A70));
                Manager(fake, 4, 0, 0x0B, 0x70, 0x0A, (byte)Country, 0, OpponentId, 0);
                AppointmentPlan plan = Read(fake);

                Assert.True(plan.Cancel(plan.Appointments().Single()));
                Assert.Equal(1, Count(fake, Negotiations));
                Assert.Equal(Negotiation(130, 0x0B00), fake.ReadModuleBytes(Negotiations + 4, 0x48));
            }
        }

        [Theory]
        [InlineData(0x0A)]
        [InlineData(0x0D)]
        [InlineData(0x0E)]
        public void Cancel_ExtensionAndTalks_TakeThemOutOfTheirLists(byte type)
        {
            using (FakeModule fake = NewFake())
            {
                uint list;
                byte[] mine, other;
                if (type == 0x0A)
                {
                    list = Extensions;
                    mine = Le(125).Concat(Le(0x1234)).ToArray();
                    other = Le(126).Concat(Le(0x1235)).ToArray();
                }
                else
                {
                    list = type == 0x0D ? Settings.YouthTalkListOffset : Settings.AmateurTalkListOffset;
                    mine = Le(0x1234).Concat(new byte[] { (byte)Country, 0 }).Concat(Le(OwnId)).Concat(Le(125)).ToArray();
                    other = Le(0x1235).Concat(new byte[] { (byte)Country, 0 }).Concat(Le(OwnId)).Concat(Le(126)).ToArray();
                }
                WriteList(fake, list, 2, mine, other);
                Manager(fake, 6, 0, type, 0x34, 0x12);
                AppointmentPlan plan = Read(fake);

                Assert.True(plan.Cancel(plan.Appointments().Single()));
                Assert.Equal(1, Count(fake, list));
                Assert.Equal(other, fake.ReadModuleBytes(list + 2, other.Length));
                Assert.Equal(0, ManagerAt(fake, 6)[1]);
            }
        }

        [Fact]
        public void Cancel_TestMatch_FreesItsEntry_AndKeepsThePair()
        {
            using (FakeModule fake = NewFake())
            {
                // Another club's match that day and a played one of the own club come first.
                byte[] foreign = new byte[0x14];
                foreign[1] = 1;
                Le(150).CopyTo(foreign, 2);
                foreign[4] = (byte)PlayerEnums.Country.England;
                fake.WriteModuleBytes(Settings.TestMatchTableOffset, foreign);
                TestMatch(fake, 1, 150, status: 2);
                TestMatch(fake, 2, 150);
                WriteList(fake, Settings.FriendlyPairListOffset, 2, Pair(150));
                Trainer(fake, 31, 0, 0x14, 1, 0, (byte)Country, 0, OpponentId, 0, 0, 0, 2, 0);
                AppointmentPlan plan = Read(fake);

                Assert.True(plan.Cancel(plan.Appointments().Single()));

                Assert.Equal(1, fake.ReadModuleBytes(Settings.TestMatchTableOffset + 1, 1)[0]);
                Assert.Equal(2, fake.ReadModuleBytes(Settings.TestMatchTableOffset + 0x14 + 1, 1)[0]);
                Assert.Equal(0, fake.ReadModuleBytes(Settings.TestMatchTableOffset + 2 * 0x14 + 1, 1)[0]);
                Assert.Equal(new byte[] { 0, 0, 0, 0 }, TrainerAt(fake, 31).Skip(0).Take(4).ToArray());
                Assert.Equal(1, Count(fake, Settings.FriendlyPairListOffset));
            }
        }

        [Fact]
        public void Cancel_TestMatchAgainstAnotherOpponent_IsNotFound()
        {
            using (FakeModule fake = NewFake())
            {
                TestMatch(fake, 0, 150);
                Trainer(fake, 31, 0, 0x14, 1, 0, (byte)Country, 0, 9, 0);
                AppointmentPlan plan = Read(fake);

                Assert.False(plan.Cancel(plan.Appointments().Single()));
                Assert.Equal(1, fake.ReadModuleBytes(Settings.TestMatchTableOffset + 1, 1)[0]);
            }
        }

        [Fact]
        public void Cancel_PlanOnly_TheGamesOwn_AndPast_Appointments()
        {
            using (FakeModule fake = NewFake())
            {
                Trainer(fake, 1, 0, 0x02, 1, 0);
                Trainer(fake, 2, 0, 0x02, 1, 0);
                Trainer(fake, 6, 0, 0x15, 0, 0, (byte)Country, 0, OpponentId, 0);
                Manager(fake, 9, 0, 0x0F, 0x34, 0x12);
                Trainer(fake, 10, 0, 0x01, 1, 0, 3, 0);
                AppointmentPlan plan = Read(fake);
                List<AppointmentPlan.Appointment> list = plan.Appointments();

                // Yesterday's training is over, today's can still go.
                Assert.False(plan.CanCancel(list[0]));
                Assert.False(plan.Cancel(list[0]));
                Assert.True(plan.CanCancel(list[1]));
                Assert.False(plan.CanMove(list[1]));
                // The league match and the pre-contract talk are the game's.
                Assert.False(plan.CanCancel(list[2]));
                Assert.False(plan.CanCancel(list[3]));
                // A training week: only its plan days go, as in the game's dialog.
                Assert.True(plan.Cancel(list[4]));
                Assert.Equal(new byte[] { 0, 0, 0, 0, 3, 0 }, TrainerAt(fake, 10).Take(6).ToArray());
                Assert.True(plan.Cancel(list[1]));
                Assert.Equal(0, TrainerAt(fake, 2)[1]);
            }
        }
        #endregion

        #region Moving
        [Fact]
        public void Move_Negotiation_ChangesItsDay_AndThePlan()
        {
            using (FakeModule fake = NewFake())
            {
                WriteList(fake, Negotiations, 4, Negotiation(123, 0x0A70));
                Manager(fake, 4, 0, 0x0C, 0x70, 0x0A, (byte)Country, 0, OpponentId, 0);
                AppointmentPlan plan = Read(fake);
                AppointmentPlan.Appointment negotiation = plan.Appointments().Single();

                Assert.True(plan.CanMove(negotiation));
                Assert.True(plan.Move(negotiation, 5));

                Assert.Equal(124, BitConverter.ToInt16(fake.ReadModuleBytes(Negotiations + 4 + 2, 2), 0));
                Assert.Equal(0, ManagerAt(fake, 4)[1]);
                Assert.Equal(new byte[] { 0, 0x0C, 0x70, 0x0A, (byte)Country, 0, OpponentId, 0 }, ManagerAt(fake, 5));
                // The week overview shows it on its new day.
                Assert.Equal(new byte[] { 0, 0x0C, 0, 0, 0x70, 0x0A, (byte)Country, 0, OpponentId, 0, 0, 0 }, fake.ReadModuleBytes(WeekSlot + 5 * 12, 12));
            }
        }

        [Fact]
        public void Move_TestMatch_MovesItsEntry_ItsPair_AndThePlan()
        {
            using (FakeModule fake = NewFake())
            {
                TestMatch(fake, 0, 150, homeOfB: 1, fee: 0x3C);
                WriteList(fake, Settings.FriendlyPairListOffset, 2, Pair(149), Pair(150));
                Trainer(fake, 31, 0, 0x14, 1, 0, (byte)Country, 0, OpponentId, 0, 1, 0, 2, 0x3C);
                AppointmentPlan plan = Read(fake);

                Assert.True(plan.Move(plan.Appointments().Single(), 33));

                Assert.Equal(152, BitConverter.ToInt16(fake.ReadModuleBytes(Settings.TestMatchTableOffset + 2, 2), 0));
                Assert.Equal(Pair(149), fake.ReadModuleBytes(Settings.FriendlyPairListOffset + 2, 10));
                Assert.Equal(Pair(152), fake.ReadModuleBytes(Settings.FriendlyPairListOffset + 12, 10));
                Assert.Equal(0x14, TrainerAt(fake, 33)[1]);
                Assert.Equal(0, TrainerAt(fake, 31)[1]);
            }
        }

        [Fact]
        public void Move_TestMatch_WithoutItsPair_OrPairList_StillMoves()
        {
            using (FakeModule fake = NewFake())
            {
                TestMatch(fake, 0, 150);
                Trainer(fake, 31, 0, 0x14, 1, 0, (byte)Country, 0, OpponentId, 0);
                Assert.True(Read(fake).Move(Read(fake).Appointments().Single(), 32));
                WriteList(fake, Settings.FriendlyPairListOffset, 2, Pair(140));
                Assert.True(Read(fake).Move(Read(fake).Appointments().Single(), 33));
                Assert.Equal(Pair(140), fake.ReadModuleBytes(Settings.FriendlyPairListOffset + 2, 10));
            }
        }

        [Fact]
        public void Move_Camp_TakesItsUnits_ToAnotherSunday_Only()
        {
            using (FakeModule fake = NewFake())
            {
                for (int i = 0; i < 7; i++)
                {
                    Trainer(fake, 14 + i, 0, 3, (byte)(1 + i), 0, 0, 0, 0, 0, 0, 0, 4, 0);
                    fake.WriteModuleBytes(Copy + Settings.ClubTrainingUnitsOffset + (uint)((14 + i) * 3), new byte[] { (byte)(i + 1), 0x0C, 0 });
                }
                Trainer(fake, 30, 0, 0x15, 0, 0);
                AppointmentPlan plan = Read(fake);
                AppointmentPlan.Appointment camp = plan.Appointments()[0];

                Assert.False(plan.Fits(camp.Kind, 15, camp));
                // Its own days don't count as taken; a week with a match does.
                Assert.True(plan.Fits(camp.Kind, 14, camp));
                Assert.False(plan.Fits(camp.Kind, 28, camp));
                Assert.False(plan.Move(camp, 28));
                Assert.True(plan.Move(camp, 21));

                Assert.Equal(new byte[] { 0, 3, 1, 0, 0, 0, 0, 0, 0, 0, 4, 0 }, TrainerAt(fake, 21));
                Assert.Equal(new byte[] { 7, 0x0C, 0 }, Units(fake, 27));
                Assert.Equal(0, TrainerAt(fake, 14)[1]);
                Assert.Equal(new byte[] { 0, 3, 7, 0, 0, 0, 0, 0, 0, 0, 4, 0 }, TrainerAt(fake, 27, DataBlock, Settings.ClubPlanDataTrainerOffset));
            }
        }

        [Fact]
        public void Move_ToToday_PastThePlans_OrToATakenDay_Fails()
        {
            using (FakeModule fake = NewFake())
            {
                Manager(fake, 10, 0, 0x12);
                Manager(fake, 11, 0, 0x13);
                AppointmentPlan plan = Read(fake);
                AppointmentPlan.Appointment fanClub = plan.Appointments()[0];

                Assert.False(plan.Move(fanClub, 2));
                Assert.False(plan.Move(fanClub, 11));
                Assert.False(plan.Move(fanClub, 42));
                Assert.True(plan.Move(fanClub, 41));
                Assert.Equal(0x12, ManagerAt(fake, 41)[1]);
            }
        }
        #endregion

        #region Adding
        [Fact]
        public void AddCamp_WritesSevenDaysAndTheTemplatesUnits()
        {
            using (FakeModule fake = NewFake())
            {
                AppointmentPlan plan = Read(fake);

                Assert.False(plan.AddCamp(7, 2, 4));
                Assert.False(plan.AddCamp(7, 0, 0));
                Assert.False(plan.AddCamp(7, 0, 23));
                Assert.False(plan.AddCamp(8, 0, 4));
                Assert.False(plan.AddCamp(0, 0, 4));
                Assert.False(plan.AddCamp(42 - 6, 0, 4));
                Assert.True(plan.AddCamp(7, 1, 4));

                for (int i = 0; i < 7; i++)
                {
                    Assert.Equal(new byte[] { 0, 3, (byte)(i + 1), 0, 1, 0, 0, 0, 0, 0, 4, 0 }, TrainerAt(fake, 7 + i));
                    Assert.Equal(AppointmentPlan.CampUnits[1].Skip(i * 3).Take(3).ToArray(), Units(fake, 7 + i));
                }
                AppointmentPlan.Appointment camp = Read(fake).Appointments().Single();
                Assert.Equal(7, camp.Length);
                // The week is taken now.
                Assert.False(Read(fake).AddCamp(7, 0, 4));
            }
        }

        [Theory]
        [InlineData(0x12, false, true)]
        [InlineData(0x13, false, true)]
        [InlineData(0x19, true, true)]
        public void AddSimple_WritesThePlansItBelongsTo(byte code, bool trainer, bool manager)
        {
            using (FakeModule fake = NewFake())
            {
                AppointmentPlan plan = Read(fake);
                Assert.True(plan.AddSimple(code, 3));

                Assert.Equal(trainer ? new byte[] { 0, code, 1, 0 } : new byte[4], TrainerAt(fake, 3).Take(4).ToArray());
                Assert.Equal(manager ? code : 0, ManagerAt(fake, 3)[1]);
                Assert.Equal(code, fake.ReadModuleBytes(WeekRecord + 3 * 12 + 1, 1)[0]);
                Assert.False(Read(fake).AddSimple(code, 3));
            }
        }

        [Fact]
        public void AddSimple_OnlyForTheClubsOwnEvents()
        {
            using (FakeModule fake = NewFake())
            {
                AppointmentPlan plan = Read(fake);
                Assert.False(plan.AddSimple(0x03, 7));
                Assert.False(plan.AddSimple(0x14, 7));
                Assert.False(plan.AddSimple(0x18, 7));
                Assert.False(plan.AddSimple(0x12, 2));
            }
        }

        [Fact]
        public void NewAndMovedAppointments_NeedTheDayFreeInBothPlans()
        {
            using (FakeModule fake = NewFake())
            {
                // A test match in the trainer's plan, a fan club meeting in the manager's.
                Trainer(fake, 8, 0, 0x14, 1, 0);
                Manager(fake, 9, 0, 0x12);
                AppointmentPlan plan = Read(fake);

                Assert.False(plan.AddSimple(0x12, 8));
                Assert.False(plan.AddCamp(7, 0, 4));
                Assert.False(plan.Move(plan.Appointments()[0], 9));
                Assert.True(plan.AddSimple(0x13, 10));
            }
        }

        [Fact]
        public void AddTestMatch_TakesAFreeEntry_AddsThePair_AndThePlanDay()
        {
            using (FakeModule fake = NewFake())
            {
                TestMatch(fake, 0, 140, status: 2);
                TestMatch(fake, 1, 140, status: 0);
                WriteList(fake, Settings.FriendlyPairListOffset, 2, Pair(140));
                AppointmentPlan plan = Read(fake);

                Assert.True(plan.AddTestMatch(10, Country, OpponentId, away: true, fee: 0x3C));

                byte[] entry = fake.ReadModuleBytes(Settings.TestMatchTableOffset + 0x14, 0x14);
                Assert.Equal(new byte[] { 0x8C, 1, 129, 0, (byte)Country, 0, OwnId, 0, (byte)Country, 0, OpponentId, 0, 0, 0, 0, 0, 0, 0, 1, 0x3C }, entry);
                Assert.Equal(2, Count(fake, Settings.FriendlyPairListOffset));
                Assert.Equal(Pair(129), fake.ReadModuleBytes(Settings.FriendlyPairListOffset + 12, 10));
                Assert.Equal(new byte[] { 0, 0x14, 1, 0, (byte)Country, 0, OpponentId, 0, 1, 0, 0, 0x3C }, TrainerAt(fake, 10));
                // The opponent plays the other one that day: no second match.
                Assert.False(Read(fake).AddTestMatch(10, Country, OpponentId, false, 0));
            }
        }

        [Fact]
        public void AddTestMatch_Refuses_TheOwnClub_ABusyOpponent_AndAFullTable()
        {
            using (FakeModule fake = NewFake())
            {
                AppointmentPlan plan = Read(fake);
                Assert.False(plan.AddTestMatch(10, Country, OwnId, false, 0));

                // Lübeck plays a test match as B that day.
                TestMatch(fake, 5, 129);
                Assert.False(plan.AddTestMatch(10, Country, OpponentId, false, 0));
                // As A against someone else.
                byte[] other = new byte[0x14];
                other[1] = 1;
                Le(129).CopyTo(other, 2);
                other[4] = (byte)Country;
                other[6] = OpponentId;
                other[8] = (byte)PlayerEnums.Country.England;
                fake.WriteModuleBytes(Settings.TestMatchTableOffset + 5 * 0x14, other);
                Assert.False(plan.AddTestMatch(10, Country, OpponentId, false, 0));

                // No free entry left.
                for (int i = 0; i < 300; i++)
                    TestMatch(fake, i, 100, status: 2);
                Assert.False(plan.AddTestMatch(10, Country, OpponentId, false, 0));
            }
        }

        [Fact]
        public void AddTestMatch_WithAFullPairList_StillArrangesTheMatch()
        {
            using (FakeModule fake = NewFake())
            {
                fake.WriteModuleBytes(Settings.FriendlyPairListOffset, Le(50));
                Assert.True(Read(fake).AddTestMatch(10, Country, OpponentId, false, 0));
                Assert.Equal(50, Count(fake, Settings.FriendlyPairListOffset));
                Assert.Equal(1, fake.ReadModuleBytes(Settings.TestMatchTableOffset + 1, 1)[0]);
            }
        }
        #endregion

        #region Week overview
        [Fact]
        public void Overview_FollowsTheGamesRules()
        {
            using (FakeModule fake = NewFake())
            {
                Manager(fake, 0, 0, 0x04, 0x34, 0x12, 9, 9, 9, 9);
                Manager(fake, 1, 0, 0x10, 1, 0, 0x22, 0, 9, 9);
                Trainer(fake, 2, 0, 0x15, 0, 0, 14, 0, 0x2F, 0, 1, 0, 9, 9);
                Manager(fake, 2, 0, 0x15);
                Trainer(fake, 3, 0, 0x03, 2, 0, 1, 0, 0, 0, 0, 0, 4, 0);
                Trainer(fake, 4, 0, 0x04, 0, 0, 0x34, 0x12, 9, 9);
                Trainer(fake, 5, 0, 0x16, 1, 0, 14, 0, 0x30, 0, 9, 9);
                Trainer(fake, 6, 0, 0x02, 1, 0, 9, 9);
                AppointmentPlan plan = Read(fake);

                Assert.Equal(new byte[] { 0, 4, 0, 0, 0x34, 0x12, 0, 0, 0, 0, 0, 0 }, plan.Overview(0));
                Assert.Equal(new byte[] { 0, 0x10, 0, 0, 1, 0, 0x22, 0, 0, 0, 0, 0 }, plan.Overview(1));
                Assert.Equal(new byte[] { 0, 0x15, 0, 0, 14, 0, 0x2F, 0, 1, 0, 0, 0 }, plan.Overview(2));
                Assert.Equal(new byte[] { 0, 3, 2, 0, 1, 0, 0, 0, 0, 0, 4, 0 }, plan.Overview(3));
                Assert.Equal(new byte[] { 0, 4, 0, 0, 0x34, 0x12, 0, 0, 0, 0, 0, 0 }, plan.Overview(4));
                Assert.Equal(new byte[] { 0, 0x16, 1, 0, 14, 0, 0x30, 0, 0, 0, 0, 0 }, plan.Overview(5));
                Assert.Equal(new byte[] { 0, 2, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, plan.Overview(6));
            }
        }

        [Theory]
        [InlineData(0x0B, 6)]
        [InlineData(0x15, 6)]
        [InlineData(0x12, 0)]
        public void Overview_ManagerTypes_CopyTheirData(byte type, int length)
        {
            using (FakeModule fake = NewFake())
            {
                Manager(fake, 3, 0, type, 1, 2, 3, 4, 5, 6);
                Trainer(fake, 3, 0, 0, 0, 0, 7, 8, 9, 10, 11, 12);
                byte[] overview = Read(fake).Overview(3);
                byte[] data = type == 0x15 ? new byte[] { 7, 8, 9, 10, 11, 12 } : new byte[] { 1, 2, 3, 4, 5, 6 };
                Assert.Equal(type, overview[1]);
                Assert.Equal(data.Take(length).Concat(new byte[6 - length]).ToArray(), overview.Skip(4).Take(6).ToArray());
            }
        }

        [Fact]
        public void Write_KeepsTheOverviewOfUnchangedDays()
        {
            using (FakeModule fake = NewFake())
            {
                // The game shows a national match on day 5 that isn't in the club's plans.
                byte[] national = { 0, 0x15, 0, 0, 14, 0, 200, 0, 0, 0, 0, 0 };
                fake.WriteModuleBytes(WeekSlot + 5 * 12, national);
                Manager(fake, 4, 0, 0x12);
                AppointmentPlan plan = Read(fake);

                Assert.True(plan.Cancel(plan.Appointments().Single()));

                Assert.Equal(national, fake.ReadModuleBytes(WeekSlot + 5 * 12, 12));
                Assert.Equal(new byte[12], fake.ReadModuleBytes(WeekSlot + 4 * 12, 12));
            }
        }
        #endregion

        [Fact]
        public void Calendar_Date_CountsFromToday()
        {
            using (FakeModule fake = NewFake())
            {
                fake.WriteModuleBytes(Settings.StartYearOffset, Le(1998));
                fake.WriteModuleBytes(Settings.DayCounterOffset, Le(10));
                fake.WriteModuleBytes(Settings.DaysPerYearOffset, Le(365));
                fake.WriteModuleBytes(Settings.CalendarTableOffset + 8 * 4, Le(1998).Concat(Le(7)).ToArray());
                fake.WriteModuleBytes(Settings.CalendarTableOffset + 12 * 4, Le(1998).Concat(Le(11)).ToArray());
                GameCalendar calendar = GameCalendar.Read(fake.Memory);

                Assert.Equal(new DateTime(1998, 1, 8), calendar.Date(-2));
                Assert.Equal(new DateTime(1998, 1, 12), calendar.Date(2));
                Assert.Null(calendar.Date(5));
                Assert.Null(calendar.Date(-11));
            }
        }
    }
}
