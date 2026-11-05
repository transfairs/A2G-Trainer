using System;
using System.IO;
using System.Reflection;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using AnstossTrainer.Shared;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// ClubController's less travelled save paths: stadium/department flags switched on, full loan
    /// and investment slots, a calendar that can't be read, a staff name too long for the game and
    /// one that can't go into the name pool, and the editor-setting switch's guard.
    /// </summary>
    public class ClubControllerCoverageTests
    {
        private static ClubController Own(FakeModule fake) => new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);

        private static byte[] Loan(int amount, byte flags)
        {
            byte[] loan = new byte[0x14];
            Array.Copy(BitConverter.GetBytes(amount), loan, 4);
            Array.Copy(BitConverter.GetBytes((short)900), 0, loan, 4, 2);
            loan[0xB] = flags;
            return loan;
        }

        private static string Hex(uint offset) => offset.ToString("X");

        [Fact]
        public void Save_WithRunningTrackAndComputerDepartment_WritesThemSwitchedOn()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                ClubController controller = Own(fake);
                Club club = controller.Club;
                Assert.False(club.HasRunningTrack);
                Assert.False(club.HasComputerDepartment);

                club.HasRunningTrack = true;
                club.HasComputerDepartment = true;
                club.LawFirm = 3;
                club.StatisticsDepartment = 1;
                controller.Save();

                Assert.Equal((byte)1, fake.ReadDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.HasRunningTrack], 1)[0]);
                Assert.Equal(new byte[] { 3, 1, 1, 3, 1, 1 }, fake.ReadDisplayCacheBytes(Hex(Settings.ClubPlannedLawFirmOffset), 6));
                Assert.Equal((byte)1, fake.ReadModuleBytes(ClubEditorSettingsTests.OtherCopy + 0x89A, 1)[0]);

                Club reread = Own(fake).Club;
                Assert.True(reread.HasRunningTrack);
                Assert.True(reread.HasComputerDepartment);
            }
        }

        [Fact]
        public void Save_WithEveryLoanSlotTaken_LeavesANewLoanOut()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                for (int slot = 0; slot < Settings.ClubLoanSlots; slot++)
                    fake.WriteDisplayCacheBytes(Hex(Settings.ClubLoansOffset + (uint)slot * Settings.ClubLoanStride), Loan(100000 * (slot + 1), 1));
                ClubController controller = Own(fake);
                Club club = controller.Club;
                Assert.Equal(Settings.ClubLoanSlots, club.Loans.Count);

                ClubLoan added = ClubLoan.New(999999, 0, false, 800);
                club.Loans.Add(added);
                controller.Save();

                Assert.Equal(-1, added.Slot);
                for (int slot = 0; slot < Settings.ClubLoanSlots; slot++)
                {
                    byte[] stored = fake.ReadDisplayCacheBytes(Hex(Settings.ClubLoansOffset + (uint)slot * Settings.ClubLoanStride), 0x14);
                    Assert.Equal(100000 * (slot + 1), BitConverter.ToInt32(stored, 0));
                }
                Assert.Equal(Settings.ClubLoanSlots, Own(fake).Club.Loans.Count);
            }
        }

        [Fact]
        public void Save_WithEveryInvestmentSlotTaken_LeavesANewInvestmentOut()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                for (int slot = 0; slot < Settings.ClubInvestmentSlots; slot++)
                {
                    byte[] investment = new byte[0x10];
                    Array.Copy(BitConverter.GetBytes(10000 * (slot + 1)), investment, 4);
                    Array.Copy(BitConverter.GetBytes(10000 * (slot + 1)), 0, investment, 4, 4);
                    Array.Copy(BitConverter.GetBytes((short)1000), 0, investment, 8, 2);
                    investment[0xB] = 1;
                    fake.WriteDisplayCacheBytes(Hex(Settings.ClubInvestmentsOffset + (uint)slot * Settings.ClubInvestmentStride), investment);
                }
                ClubController controller = Own(fake);
                Club club = controller.Club;
                Assert.Equal(Settings.ClubInvestmentSlots, club.Investments.Count);

                ClubInvestment added = ClubInvestment.New(2, 77777, club.InvestmentIndices);
                club.Investments.Add(added);
                controller.Save();

                Assert.Equal(-1, added.Slot);
                for (int slot = 0; slot < Settings.ClubInvestmentSlots; slot++)
                {
                    byte[] stored = fake.ReadDisplayCacheBytes(Hex(Settings.ClubInvestmentsOffset + (uint)slot * Settings.ClubInvestmentStride), 0x10);
                    Assert.Equal(10000 * (slot + 1), BitConverter.ToInt32(stored, 0));
                }
            }
        }

        // Day 0 of the season, day 0 of the quarter's last month: -30, maturity years * 1000.
        private static void AssertLoanBilledWithoutCalendar(FakeModule fake)
        {
            byte[] slot0 = fake.ReadDisplayCacheBytes(Hex(Settings.ClubLoansOffset), 0x14);
            Assert.Equal(500000, BitConverter.ToInt32(slot0, 0));
            Assert.Equal((short)-30, BitConverter.ToInt16(slot0, 0xE));
            Assert.Equal((short)2000, BitConverter.ToInt16(slot0, 0x10));
        }

        private static void WriteCalendar(FakeModule fake)
        {
            fake.WriteModuleBytes(Settings.GlobalCounterOffset, BitConverter.GetBytes((short)50));
            fake.WriteModuleBytes(Settings.DayOfMonthOffset, BitConverter.GetBytes((short)17));
            fake.WriteModuleBytes(Settings.MonthOffset, BitConverter.GetBytes((short)3));
        }

        [Fact]
        public void Save_NewLoan_WithAnUnreadableCalendar_CountsFromDayZero()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                WriteCalendar(fake);
                ClubController controller = Own(fake);
                fake.MakeModuleRegionUnreadable(Settings.GlobalCounterOffset, 0x10);

                ClubLoan added = ClubLoan.New(500000, 1, true, 800);
                controller.Club.Loans.Add(added);
                controller.Save();

                Assert.Equal(0, added.Slot);
                AssertLoanBilledWithoutCalendar(fake);
            }
        }

        [Fact]
        public void Save_NewLoan_WithoutFixedAddresses_CountsFromDayZero()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake, withRecord: false);
                WriteCalendar(fake);
                // An unconfirmed layout: no fixed addresses, so the calendar isn't known.
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: false);
                Assert.Null(FixedAddresses.For(fake.Memory));
                ClubController controller = Own(fake);

                ClubLoan added = ClubLoan.New(500000, 1, true, 800);
                controller.Club.Loans.Add(added);
                controller.Save();

                Assert.Equal(0, added.Slot);
                AssertLoanBilledWithoutCalendar(fake);
            }
        }

        private static uint CoTrainerEntry(ushort id) => Settings.CoachPoolTableOffset + id * Settings.StaffEntryStride;

        private static string LogText()
        {
            string path = (string)typeof(Logger).GetField("LogFilePath", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            if (!File.Exists(path))
                return string.Empty;
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        // A co-trainer whose name can't be resolved nor appended: the build's name pool is unknown.
        [Fact]
        public void Save_StaffName_TooLongIsCutToTheGamesLength_AndWithoutANamePoolTheEntryKeepsItsName()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.Memory.Layout = new PersistentLayout(Settings.PlayerRecordTableOffset, null, Settings.AgeReferenceYearOffset, Settings.ActiveTrainerCountOffset,
                    isVerified: true, hasOriginalBuildAnchors: true);
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                Addresses own = AddressPresets.OWN_CLUB;
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 15 });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.CoTrainer], BitConverter.GetBytes((ushort)5));
                fake.WriteModuleBytes(CoTrainerEntry(5), BitConverter.GetBytes((ushort)1));
                fake.WriteModuleBytes(CoTrainerEntry(5) + 2, BitConverter.GetBytes((ushort)2));
                fake.WriteModuleBytes(CoTrainerEntry(5) + Settings.StaffCompetenceOffset, new byte[] { 9 });

                ClubController controller = Own(fake);
                Club club = controller.Club;
                Assert.True(club.HasCoTrainer);
                Assert.Equal("", club.CoTrainerFirstname);

                string marker = Guid.NewGuid().ToString("N").Substring(0, 10);
                string cut = "Co " + marker + "xx";
                Assert.Equal(Club.StaffNameLength, cut.Length);
                club.ClubName = "X";
                club.StadiumName = "Y";
                club.CoTrainerFirstname = cut + "-Überlänge";
                club.CoTrainerCompetence = 11;
                controller.Save();

                Assert.Equal((ushort)1, BitConverter.ToUInt16(fake.ReadModuleBytes(CoTrainerEntry(5), 2), 0));
                Assert.Equal((byte)11, fake.ReadModuleBytes(CoTrainerEntry(5) + Settings.StaffCompetenceOffset, 1)[0]);
                Assert.Contains($"Trainerstab: \"{cut}\" konnte nicht in den Namensbestand aufgenommen werden", LogText());
            }
        }

        [Fact]
        public void EditorSettingBytes_ForAKeyThatIsNoEditorSetting_Throws()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                ClubController controller = Own(fake);
                MethodInfo method = typeof(ClubController).GetMethod("EditorSettingBytes", BindingFlags.NonPublic | BindingFlags.Instance);

                Assert.Equal(new byte[] { 2 }, (byte[])method.Invoke(controller, new object[] { ClubEnums.AddressKey.ArchRival }));
                TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => method.Invoke(controller, new object[] { ClubEnums.AddressKey.NAME }));
                ArgumentOutOfRangeException inner = Assert.IsType<ArgumentOutOfRangeException>(thrown.InnerException);
                Assert.Equal("key", inner.ParamName);
            }
        }
    }
}
