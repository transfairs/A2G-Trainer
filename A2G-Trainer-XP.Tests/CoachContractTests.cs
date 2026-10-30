using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for the coach's contract terms (working copy +0x48..+0x67): the model's extras/option/
    /// release flags, reading and saving them through CoachController, and the "Vertrag" tab's bindings.
    /// </summary>
    public class CoachContractTests
    {
        // Trainer 1's current contract as read from a running game (anstoss2.exe 0x857848, 32 bytes):
        // 705375 salary, 12825 per point, 10260 per cup round, 76950 for title/promotion, 38475 for
        // the season goal "Meisterschaft", 1 year left, no option, Dienstente + 2-Zimmer Wohnung.
        private static readonly byte[] LiveContract =
        {
            0x5F, 0xC3, 0x0A, 0x00, 0x19, 0x32, 0x00, 0x00, 0x14, 0x28, 0x00, 0x00, 0x96, 0x2C, 0x01, 0x00,
            0x4B, 0x96, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x0C, 0x00, 0x00, 0x00, 0x00, 0x00
        };

        private static uint WorkingCopy(uint slotOffset) => Settings.CoachSlotTableOffset + 1 * Settings.CoachSlotStride + slotOffset;

        private static List<string> RecordChanges(Coach coach)
        {
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);
            return raised;
        }

        [Fact]
        public void AddressPresets_MapTheContractBlock()
        {
            Addresses coach = AddressPresets.COACH;

            Assert.Equal("34B48", coach[CoachEnums.AddressKey.SALARY]);
            Assert.Equal("34B4C", coach[CoachEnums.AddressKey.POINT_BONUS]);
            Assert.Equal("34B50", coach[CoachEnums.AddressKey.PER_TROPHY_ROUND]);
            Assert.Equal("34B54", coach[CoachEnums.AddressKey.PER_TITLE]);
            Assert.Equal("34B58", coach[CoachEnums.AddressKey.ACHIEVED_BONUS]);
            Assert.Equal("34B5C", coach[CoachEnums.AddressKey.SEASON_GOAL]);
            Assert.Equal("34B60", coach[CoachEnums.AddressKey.EXTEND_OPTION]);
            Assert.Equal("34B62", coach[CoachEnums.AddressKey.PERKS]);
        }

        [Fact]
        public void Perks_SplitIntoVehicleAndHousing()
        {
            Coach coach = new Coach { Perks = 0x000C };

            Assert.Equal(CoachEnums.VehiclePerk.Dienstente, coach.Vehicle);
            Assert.Equal(CoachEnums.HousingPerk.ZweiZimmerWohnung, coach.Housing);
        }

        [Fact]
        public void Perks_WithoutAnyBit_AreNone()
        {
            Coach coach = new Coach();

            Assert.Equal(CoachEnums.VehiclePerk.Keines, coach.Vehicle);
            Assert.Equal(CoachEnums.HousingPerk.Keine, coach.Housing);
        }

        [Fact]
        public void Perks_WithSeveralBitsOfOneKind_ShowTheBestOne()
        {
            Coach coach = new Coach { Perks = (1 << 2) | (1 << 10) | (1 << 3) | (1 << 9) };

            Assert.Equal(CoachEnums.VehiclePerk.RrMitChauffeur, coach.Vehicle);
            Assert.Equal(CoachEnums.HousingPerk.Luxusvilla, coach.Housing);
        }

        [Fact]
        public void Vehicle_Set_ReplacesOnlyTheVehicleBits_AndKeepsUnknownBits()
        {
            Coach coach = new Coach { Perks = 0x8000 | (1 << 2) | (1 << 4) | (1 << 3) };

            coach.Vehicle = CoachEnums.VehiclePerk.Bahncard;

            Assert.Equal((ushort)(0x8000 | (1 << 0) | (1 << 3)), coach.Perks);
        }

        [Fact]
        public void Housing_SetToNone_ClearsOnlyTheHousingBits()
        {
            Coach coach = new Coach { Perks = (1 << 6) | (1 << 11) };

            coach.Housing = CoachEnums.HousingPerk.Keine;

            Assert.Equal((ushort)(1 << 6), coach.Perks);
        }

        [Fact]
        public void Perks_Set_RaisesVehicleAndHousing()
        {
            Coach coach = new Coach();
            List<string> raised = RecordChanges(coach);

            coach.Housing = CoachEnums.HousingPerk.Palast;

            Assert.Contains(nameof(Coach.Perks), raised);
            Assert.Contains(nameof(Coach.Vehicle), raised);
            Assert.Contains(nameof(Coach.Housing), raised);
        }

        [Theory]
        [InlineData((ushort)0, true, (ushort)1)]
        [InlineData((ushort)2, true, (ushort)2)]   // a stored non-zero value is kept
        [InlineData((ushort)2, false, (ushort)0)]
        public void HasExtensionOption_Set_MapsOntoTheStoredValue(ushort stored, bool set, ushort expected)
        {
            Coach coach = new Coach { ExtensionOption = stored };
            List<string> raised = RecordChanges(coach);

            coach.HasExtensionOption = set;

            Assert.Equal(expected, coach.ExtensionOption);
            Assert.Equal(set, coach.HasExtensionOption);
            Assert.Contains(nameof(Coach.HasExtensionOption), raised);
        }

        [Theory]
        [InlineData((byte)0, true, (byte)1)]
        [InlineData((byte)5, true, (byte)5)]
        [InlineData((byte)5, false, (byte)0)]
        public void HasBoardRelease_Set_MapsOntoTheStoredByte_AndRaisesCanNegotiateOffers(byte stored, bool set, byte expected)
        {
            Coach coach = new Coach { BoardRelease = stored };
            List<string> raised = RecordChanges(coach);

            coach.HasBoardRelease = set;

            Assert.Equal(expected, coach.BoardRelease);
            Assert.Contains(nameof(Coach.HasBoardRelease), raised);
            Assert.Contains(nameof(Coach.CanNegotiateOffers), raised);
        }

        [Fact]
        public void ContractYearsLeft_Set_RaisesCanNegotiateOffers()
        {
            Coach coach = new Coach { ContractYearsLeft = 3 };
            List<string> raised = RecordChanges(coach);

            coach.ContractYearsLeft = 1;

            Assert.True(coach.CanNegotiateOffers);
            Assert.Contains(nameof(Coach.ContractYearsLeft), raised);
            Assert.Contains(nameof(Coach.CanNegotiateOffers), raised);
        }

        [Fact]
        public void AmountsAndSeasonGoal_Set_RaisePropertyChanged()
        {
            Coach coach = new Coach();
            List<string> raised = RecordChanges(coach);

            coach.Salary = 1;
            coach.PointBonus = 2;
            coach.CupRoundBonus = 3;
            coach.TitleBonus = 4;
            coach.SeasonGoalBonus = 5;
            coach.SeasonGoal = CoachEnums.SeasonGoal.Klassenerhalt;

            Assert.Equal(new[] { nameof(Coach.Salary), nameof(Coach.PointBonus), nameof(Coach.CupRoundBonus), nameof(Coach.TitleBonus), nameof(Coach.SeasonGoalBonus), nameof(Coach.SeasonGoal) }, raised);
        }

        [Fact]
        public void GetEntity_ReadsTheContractTerms()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(WorkingCopy(0x48), LiveContract);
                fake.WriteModuleBytes(WorkingCopy(0x65), new byte[] { 1 });

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal(705375, coach.Salary);
                Assert.Equal(12825, coach.PointBonus);
                Assert.Equal(10260, coach.CupRoundBonus);
                Assert.Equal(76950, coach.TitleBonus);
                Assert.Equal(38475, coach.SeasonGoalBonus);
                Assert.Equal(CoachEnums.SeasonGoal.Meisterschaft, coach.SeasonGoal);
                Assert.Equal((ushort)1, coach.ContractYearsLeft);
                Assert.False(coach.HasExtensionOption);
                Assert.Equal(CoachEnums.VehiclePerk.Dienstente, coach.Vehicle);
                Assert.Equal(CoachEnums.HousingPerk.ZweiZimmerWohnung, coach.Housing);
                Assert.True(coach.HasBoardRelease);
            }
        }

        [Fact]
        public void Save_WritesTheContractTermsToWorkingCopyAndRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                Coach coach = controller.Coach;
                coach.Salary = 2000000;
                coach.PointBonus = 20000;
                coach.CupRoundBonus = 30000;
                coach.TitleBonus = 400000;
                coach.SeasonGoalBonus = 50000;
                coach.SeasonGoal = CoachEnums.SeasonGoal.UefaPokalplatz;
                coach.ContractYearsLeft = 4;
                coach.HasExtensionOption = true;
                coach.Vehicle = CoachEnums.VehiclePerk.RrMitChauffeur;
                coach.Housing = CoachEnums.HousingPerk.Palast;
                coach.HasBoardRelease = true;

                controller.Save();

                Addresses a = AddressPresets.COACH;
                foreach (Func<string, int, byte[]> read in new Func<string, int, byte[]>[]
                {
                    (offset, length) => CoachMemory.ReadWorkingCopy(fake, offset, length),
                    (offset, length) => CoachMemory.ReadRecord(fake, offset, length)
                })
                {
                    Assert.Equal(2000000, BitConverter.ToInt32(read(a[CoachEnums.AddressKey.SALARY], 4), 0));
                    Assert.Equal(20000, BitConverter.ToInt32(read(a[CoachEnums.AddressKey.POINT_BONUS], 4), 0));
                    Assert.Equal(30000, BitConverter.ToInt32(read(a[CoachEnums.AddressKey.PER_TROPHY_ROUND], 4), 0));
                    Assert.Equal(400000, BitConverter.ToInt32(read(a[CoachEnums.AddressKey.PER_TITLE], 4), 0));
                    Assert.Equal(50000, BitConverter.ToInt32(read(a[CoachEnums.AddressKey.ACHIEVED_BONUS], 4), 0));
                    Assert.Equal((ushort)3, BitConverter.ToUInt16(read(a[CoachEnums.AddressKey.SEASON_GOAL], 2), 0));
                    Assert.Equal((ushort)4, BitConverter.ToUInt16(read(a[CoachEnums.AddressKey.CONTRACT_DURATION], 2), 0));
                    Assert.Equal((ushort)1, BitConverter.ToUInt16(read(a[CoachEnums.AddressKey.EXTEND_OPTION], 2), 0));
                    Assert.Equal((ushort)((1 << 10) | (1 << 11)), BitConverter.ToUInt16(read(a[CoachEnums.AddressKey.PERKS], 2), 0));
                    Assert.Equal((byte)1, read(a[CoachEnums.AddressKey.BOARD_RELEASE], 1)[0]);
                }
            }
        }

        [Fact]
        public void Save_WithUnchangedContract_WritesTheBlockBackUnchanged()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(WorkingCopy(0x48), LiveContract);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

                controller.Save();

                Assert.Equal(LiveContract, fake.ReadModuleBytes(WorkingCopy(0x48), LiveContract.Length));
            }
        }

        private static T Field<T>(CoachView view, string name) =>
            (T)typeof(CoachView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        [Fact]
        public void InitClubTabControl_BindsTheContractTab() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteModuleBytes(WorkingCopy(0x48), LiveContract);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);

                trainer.CoachView.InitClubTabControl();

                CoachView view = trainer.CoachView;
                Assert.Equal(nameof(Coach.Salary), Field<TextBox>(view, "SalaryInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.PointBonus), Field<TextBox>(view, "PointBonusInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.CupRoundBonus), Field<TextBox>(view, "CupRoundBonusInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.TitleBonus), Field<TextBox>(view, "TitleBonusInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.SeasonGoalBonus), Field<TextBox>(view, "SeasonGoalBonusInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.ContractYearsLeft), Field<TextBox>(view, "ContractYearsInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.SeasonGoal), Field<ComboBox>(view, "SeasonGoalInput").DataBindings["SelectedValue"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.Vehicle), Field<ComboBox>(view, "VehiclePerkInput").DataBindings["SelectedValue"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.Housing), Field<ComboBox>(view, "HousingPerkInput").DataBindings["SelectedValue"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.HasExtensionOption), Field<CheckBox>(view, "ExtensionOptionInput").DataBindings["Checked"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.HasBoardRelease), Field<CheckBox>(view, "BoardReleaseInput").DataBindings["Checked"].BindingMemberInfo.BindingField);

                Assert.Equal(12, Field<ComboBox>(view, "SeasonGoalInput").Items.Count);
                Assert.Equal(7, Field<ComboBox>(view, "VehiclePerkInput").Items.Count);
                Assert.Equal(7, Field<ComboBox>(view, "HousingPerkInput").Items.Count);
            }
        });

        // Shows the view in host and opens its "Vertrag" tab. An exception WinForms raises while doing
        // so (e.g. a binding without a data source) fails the test here, instead of reaching WinForms'
        // default handler, which would pop up an error dialog on the desktop and wait for a click.
        private static TabPage ShowContractTab(CoachView view, Form host)
        {
            Exception uiError = null;
            System.Threading.ThreadExceptionEventHandler handler = (s, e) => uiError = uiError ?? e.Exception;
            Application.ThreadException += handler;
            try
            {
                host.Controls.Add(view);
                host.Show();
                TabPage contractTab = Field<TabPage>(view, "ContractTab");
                Field<TabControl>(view, "ClubTabControl").SelectedTab = contractTab;
                Application.DoEvents();
                if (uiError != null)
                    throw new Xunit.Sdk.XunitException("Fehler in der Oberfläche: " + uiError);
                return contractTab;
            }
            finally
            {
                Application.ThreadException -= handler;
            }
        }

        // The tab is taller than its page, so it scrolls vertically - and that scrollbar's width must
        // still leave room for both columns, or a horizontal scrollbar appears as well.
        [Fact]
        public void ContractTab_ScrollsOnlyVertically() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            using (Form host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-3000, -3000) })
            {
                // Loaded and wired first, as in the running app: shown without a coach, the Allgemein
                // tab's competency bindings have no data source and WinForms pops up an error dialog.
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();
                host.ClientSize = trainer.CoachView.Size;
                TabPage contractTab = ShowContractTab(trainer.CoachView, host);

                Assert.True(contractTab.VerticalScroll.Visible);
                Assert.False(contractTab.HorizontalScroll.Visible);
            }
        });

        // "Speichern" reloads through RefreshValues, which first blanks every combo. That blank
        // selection used to reach the SeasonGoal enum and throw, leaving the whole form empty.
        [Fact]
        public void RefreshValues_AfterInit_ReloadsTheContractCombosWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            using (Form host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-3000, -3000) })
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteModuleBytes(WorkingCopy(0x48), LiveContract);
                fake.WriteModuleBytes(WorkingCopy(0x5C), BitConverter.GetBytes((ushort)CoachEnums.SeasonGoal.Klassenerhalt));
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                // The crash needs live controls on an opened "Vertrag" tab, as in the running app:
                // only then do the blanked combos push their empty selection into the binding.
                ShowContractTab(trainer.CoachView, host);

                Exception thrown = Record.Exception(() => trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN));

                Assert.Null(thrown);
                // Check the freshly read coach the bindings now point at.
                Coach coach = Field<Coach>(trainer.CoachView, "coach");
                Assert.Equal(CoachEnums.SeasonGoal.Klassenerhalt, coach.SeasonGoal);
                Assert.Equal(CoachEnums.VehiclePerk.Dienstente, coach.Vehicle);
                Assert.Equal(705375, coach.Salary);
            }
        });
    }
}
