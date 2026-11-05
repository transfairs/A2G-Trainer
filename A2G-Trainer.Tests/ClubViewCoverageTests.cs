using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// ClubView on its own (no main window: each one costs the x86 test host ~20 MB): the tabs
    /// without a club or a game, the bank buttons, the lock boxes, the club choice and the live
    /// match tab's home match, hardness/effort and an emptied bench place.
    /// </summary>
    public class ClubViewCoverageTests
    {
        private const PlayerEnums.Country Country = ClubEditorSettingsTests.Country;
        private const byte OwnId = ClubEditorSettingsTests.OwnId;
        private const uint Table = 0x51A000;
        private const uint Clock = 0x51F000;

        private static T Field<T>(object target, string name) =>
            (T)typeof(ClubView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void SetField(object target, string name, object value) =>
            typeof(ClubView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static object Invoke(object target, string name, params object[] args) =>
            typeof(ClubView).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, args.Select(a => a.GetType()).ToArray(), null).Invoke(target, args);

        private static void Commit(ComboBox combo) =>
            typeof(ComboBox).GetMethod("OnSelectionChangeCommitted", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(combo, new object[] { EventArgs.Empty });

        // A view whose memory isn't attached to any process: no game, no club.
        private static void WithoutGame(Action<ClubView> test) => StaThread.Run(() =>
        {
            using (ClubView view = new ClubView(new ProcessMemory(), new ProcessController(null)))
            {
                view.InitClubTabControl();
                test(view);
            }
        });

        // A view showing the own club of a fake game (ClubEditorSettingsTests.SeedOwnClub).
        private static void WithOwnClub(Action<FakeModule, ClubView> test, Action<FakeModule> seed = null, bool withRecord = true) => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (ClubView view = new ClubView(fake.Memory, new ProcessController(null)))
            {
                ClubEditorSettingsTests.SeedOwnClub(fake, withRecord);
                seed?.Invoke(fake);
                view.RefreshValues(PlayerEnums.AddressType.OWN);
                view.InitClubTabControl();
                test(fake, view);
            }
        });

        [Fact]
        public void WithoutAClub_EveryTabIsEmpty() => WithoutGame(view =>
        {
            Assert.Null(Field<Club>(view, "club"));
            Assert.Equal("Block A", Field<GroupBox>(view, "BlockBox").Text);
            Assert.Equal("", Field<TextBox>(view, "BlockStandingsInput").Text);
            Assert.Equal("", Field<TextBox>(view, "BlockSeatsInput").Text);
            Assert.Equal("", Field<TextBox>(view, "BlockWeeksInput").Text);
            Assert.False(Field<CheckBox>(view, "BlockRoofInput").Checked);
            Assert.Equal("0", Field<Label>(view, "LegendStandingOpenValue").Text);
            Assert.Equal("", Field<Label>(view, "CapacityValue").Text);

            // Only "–" to choose, no texts of a record, no map position.
            Assert.Equal(1, Field<ComboBox>(view, "ArchRivalInput").Items.Count);
            Assert.Equal(1, Field<ComboBox>(view, "FanFriendshipInput").Items.Count);
            Assert.False(Field<TextBox>(view, "ShortNameInput").Enabled);
            Assert.Equal("", Field<TextBox>(view, "MapXInput").Text);
            Assert.Equal("", Field<TextBox>(view, "MapYInput").Text);
            Assert.Null(Field<ClubMap>(view, "ClubMapView").Club);
            Assert.Empty(Field<ClubMap>(view, "ClubMapView").Clubs);

            // Bank and costs: nothing to show without a game.
            Assert.Null(Field<DataGridView>(view, "loansGrid").DataSource);
            Assert.Null(Field<DataGridView>(view, "investmentsGrid").DataSource);
            Assert.Equal("-", Field<Label>(view, "issuedSharesValue").Text);
            Assert.False(Field<GroupBox>(view, "ratesBox").Enabled);
            Assert.False(Field<GroupBox>(view, "costsBox").Enabled);

            // Association: nothing imposed, every limit locked.
            Assert.All(Field<Dictionary<ClubEnums.Requirement, CheckBox>>(view, "requirementInputs").Values, c => Assert.False(c.Checked));
            Assert.False(Field<CheckBox>(view, "licenceRefusedInput").Checked);
            Assert.All(Field<List<KeyValuePair<CheckBox, Func<Club, bool>>>>(view, "lockInputs"), l => Assert.False(l.Key.Checked));
            Assert.False(Field<TextBox>(view, "debtLimitInput").Enabled);

            // Board and live match.
            Invoke(view, "ShowBoard");
            Assert.Equal("–", Field<Label>(view, "ManagerValue").Text);
            Assert.False(Field<TextBox>(view, "BoardTrustInput").Enabled);
            Invoke(view, "ShowLiveMatch");
            Assert.Equal("Kein laufendes Spiel", Field<Label>(view, "liveStatusValue").Text);
            Assert.False(Field<GroupBox>(view, "liveTacticsBox").Enabled);
        });

        [Fact]
        public void WithoutAClub_EditsAndButtonsDoNothing() => WithoutGame(view =>
        {
            Field<TextBox>(view, "BlockSeatsInput").Text = "100";
            Field<TextBox>(view, "MapXInput").Text = "12";
            Field<List<KeyValuePair<CheckBox, Func<Club, bool>>>>(view, "lockInputs")[0].Key.Checked = true;
            Field<Dictionary<ClubEnums.Requirement, CheckBox>>(view, "requirementInputs")[ClubEnums.Requirement.DebtLimit].Checked = true;
            Field<CheckBox>(view, "licenceRefusedInput").Checked = true;
            ComboBox rival = Field<ComboBox>(view, "ArchRivalInput");
            rival.SelectedIndex = 0;
            Commit(rival);

            Invoke(view, "AddLoan");
            Invoke(view, "RemoveLoan");
            Invoke(view, "AddInvestment");
            Invoke(view, "RemoveInvestment");

            Assert.Null(Field<Club>(view, "club"));
            // The requirement wasn't taken, so its limit stays locked.
            Assert.False(Field<TextBox>(view, "debtLimitInput").Enabled);
            Assert.Equal("", Field<Label>(view, "CapacityValue").Text);
        });

        [Fact]
        public void LiveTab_WithAClubButNoGame_ShowsNoMatch() => WithoutGame(view =>
        {
            SetField(view, "club", new Club { Country = Country, Id = OwnId });

            Invoke(view, "ShowLiveMatch");

            Assert.Equal("Kein laufendes Spiel", Field<Label>(view, "liveStatusValue").Text);
            Assert.Null(Field<object>(view, "liveMatch"));
        });

        [Fact]
        public void AddLoan_TakesTheGamesLoanRate_OrElse8Percent_UpToTheSlots() => WithoutGame(view =>
        {
            Club club = new Club();
            SetField(view, "club", club);

            Invoke(view, "AddLoan");
            Assert.Equal(1000000, club.Loans[0].Amount);
            Assert.Equal(8m, club.Loans[0].Rate);
            Assert.True(club.Loans[0].Cancellable);

            SetField(view, "interestRates", new InterestRates { Loan = 9.5m });
            Invoke(view, "AddLoan");
            Assert.Equal(9.5m, club.Loans[1].Rate);

            for (int i = 0; i < Settings.ClubLoanSlots + 2; i++)
                Invoke(view, "AddLoan");
            Assert.Equal(Settings.ClubLoanSlots, club.Loans.Count);

            for (int i = 0; i < Settings.ClubInvestmentSlots + 2; i++)
                Invoke(view, "AddInvestment");
            Assert.Equal(Settings.ClubInvestmentSlots, club.Investments.Count);
            Assert.Equal(100000, club.Investments[0].Principal);
        });

        [Fact]
        public void RemoveButtons_TakeTheSelectedLoanAndInvestment() => StaThread.Run(() =>
        {
            using (Form host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000), Size = new Size(900, 700) })
            using (ClubView view = new ClubView(new ProcessMemory(), new ProcessController(null)))
            {
                Club club = new Club();
                club.Loans.Add(ClubLoan.New(1000, 0, true, 800));
                club.Loans.Add(ClubLoan.New(2000, 0, true, 800));
                club.Investments.Add(ClubInvestment.New(0, 3000, club.InvestmentIndices));
                club.Investments.Add(ClubInvestment.New(0, 4000, club.InvestmentIndices));
                SetField(view, "club", club);
                view.InitClubTabControl();
                host.Controls.Add(view);
                host.Show();
                Field<TabControl>(view, "ClubTabControl").SelectedTab = Field<TabPage>(view, "bankTab");
                Application.DoEvents();

                DataGridView loans = Field<DataGridView>(view, "loansGrid");
                DataGridView investments = Field<DataGridView>(view, "investmentsGrid");
                loans.CurrentCell = loans.Rows[1].Cells[0];
                investments.CurrentCell = investments.Rows[0].Cells[1];

                Invoke(view, "RemoveLoan");
                Invoke(view, "RemoveInvestment");

                Assert.Equal(1000, Assert.Single(club.Loans).Amount);
                Assert.Equal(4000, Assert.Single(club.Investments).Principal);

                // Nothing selected: nothing removed.
                loans.CurrentCell = null;
                investments.CurrentCell = null;
                Invoke(view, "RemoveLoan");
                Invoke(view, "RemoveInvestment");
                Assert.Single(club.Loans);
                Assert.Single(club.Investments);
            }
        });

        [Fact]
        public void BudgetGrid_DataErrorOutsideAnEdit_SetsTheLimitOfAnOverflow() => StaThread.Run(() =>
        {
            using (Form host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000), Size = new Size(900, 700) })
            using (ClubView view = new ClubView(new ProcessMemory(), new ProcessController(null)))
            {
                Club club = new Club();
                club.Loans.Add(ClubLoan.New(1000, 0, true, 800));
                SetField(view, "club", club);
                view.InitClubTabControl();
                host.Controls.Add(view);
                host.Show();
                Field<TabControl>(view, "ClubTabControl").SelectedTab = Field<TabPage>(view, "bankTab");
                Application.DoEvents();
                DataGridView grid = Field<DataGridView>(view, "loansGrid");

                // Not raised by an edit, so there's no editing control: the empty text counts as positive.
                DataGridViewDataErrorEventArgs e = new DataGridViewDataErrorEventArgs(new FormatException("x", new OverflowException()), 0, 0, DataGridViewDataErrorContexts.Commit);
                typeof(DataGridView).GetMethod("OnDataError", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(grid, new object[] { false, e });

                Assert.False(e.ThrowException);
                Assert.Equal(int.MaxValue, club.Loans[0].Amount);

                // Typed into the cell: a negative number too small becomes the minimum.
                grid.CurrentCell = grid.Rows[0].Cells[0];
                grid.BeginEdit(true);
                grid.EditingControl.Text = "-99999999999";
                grid.EndEdit();
                Assert.Equal(int.MinValue, club.Loans[0].Amount);
            }
        });

        [Theory]
        [InlineData(typeof(int), false, int.MaxValue)]
        [InlineData(typeof(int), true, int.MinValue)]
        [InlineData(typeof(short), false, short.MaxValue)]
        [InlineData(typeof(short), true, short.MinValue)]
        [InlineData(typeof(byte), false, byte.MaxValue)]
        [InlineData(typeof(byte), true, byte.MinValue)]
        public void OverflowLimit_CoversBothSigns(Type type, bool negative, object expected)
        {
            Assert.Equal(Convert.ChangeType(expected, type), ClubView.OverflowLimit(type, negative));
        }

        [Fact]
        public void OverflowLimit_ForDecimals_IsTheirMaximumOrMinimum()
        {
            Assert.Equal(decimal.MaxValue, ClubView.OverflowLimit(typeof(decimal), false));
            Assert.Equal(decimal.MinValue, ClubView.OverflowLimit(typeof(decimal), true));
            Assert.Null(ClubView.OverflowLimit(typeof(string), true));
        }

        [Fact]
        public void LockBoxes_SetAndClearTheirBriberyBits() => WithOwnClub((fake, view) =>
        {
            Club club = Field<Club>(view, "club");
            List<KeyValuePair<CheckBox, Func<Club, bool>>> locks = Field<List<KeyValuePair<CheckBox, Func<Club, bool>>>>(view, "lockInputs");
            club.Bribery = 0;

            locks[1].Key.Checked = true;
            Assert.Equal((byte)0x20, club.Bribery);
            locks[2].Key.Checked = true;
            Assert.Equal((byte)0x30, club.Bribery);
            Assert.True(locks[2].Value(club));
            locks[1].Key.Checked = false;
            Assert.Equal((byte)0x10, club.Bribery);
            locks[2].Key.Checked = false;
            Assert.Equal((byte)0, club.Bribery);
            Assert.False(locks[1].Value(club));

            // Reloading clears the boxes first; that must not reach the club shown so far.
            locks[1].Key.Checked = true;
            view.RefreshValues(PlayerEnums.AddressType.OWN);
            Assert.Equal((byte)0x20, club.Bribery);
            Assert.NotSame(club, Field<Club>(view, "club"));
        });

        [Fact]
        public void ClubChoice_WithoutASelection_KeepsTheClubsRival() => WithOwnClub((fake, view) =>
        {
            Club club = Field<Club>(view, "club");
            ComboBox rival = Field<ComboBox>(view, "ArchRivalInput");
            Assert.Equal((byte)2, club.ArchRivalId);

            rival.SelectedIndex = -1;
            Commit(rival);
            Assert.Equal((byte)2, club.ArchRivalId);

            rival.SelectedIndex = 1;   // VfL Osnabrück
            Commit(rival);
            Assert.Equal((byte)0, club.ArchRivalId);
        });

        [Fact]
        public void ClubChoice_WithoutAClubShown_OffersEveryClubOfTheCountry() => WithOwnClub((fake, view) =>
        {
            SetField(view, "club", null);

            Invoke(view, "ShowEditorSettings");

            ComboBox rival = Field<ComboBox>(view, "ArchRivalInput");
            Assert.Equal(new[] { "–", "VfL Osnabrück", "Eintr. Braunschweig", "FC Hannover 66" }, Enumerable.Range(0, rival.Items.Count).Select(i => rival.GetItemText(rival.Items[i])));
            Assert.Equal("", Field<TextBox>(view, "MapXInput").Text);
        });

        // Gives the three records of SeedOwnClub their ID and country, so the club choice lists them.
        private static void ListRecords(FakeModule fake)
        {
            for (byte id = 0; id < 3; id++)
            {
                fake.WriteModuleBytes(ClubEditorSettingsTests.Record(id) + Settings.ClubRecordIdOffset, new[] { id });
                fake.WriteModuleBytes(ClubEditorSettingsTests.Record(id) + Settings.ClubRecordCountryOffset, new[] { (byte)Country });
            }
        }

        [Fact]
        public void PickingAClub_ShowsIt_WithOrWithoutAListener() => WithOwnClub((fake, view) =>
        {
            ClubSquads.Entry hannover = new ClubSquads.Entry { Country = Country, Id = 2, Name = "FC Hannover 66" };
            MethodInfo picked = typeof(ClubView).GetMethod("ClubPicked", BindingFlags.NonPublic | BindingFlags.Instance);

            picked.Invoke(view, new object[] { hannover });
            Assert.Same(hannover, view.ChosenClub);
            Assert.Equal((byte)2, Field<Club>(view, "club").Id);

            ClubSquads.Entry reported = null;
            view.ClubChosen += c => reported = c;
            ClubSquads.Entry osnabrueck = new ClubSquads.Entry { Country = Country, Id = 0, Name = "VfL Osnabrück" };
            picked.Invoke(view, new object[] { osnabrueck });
            Assert.Same(osnabrueck, reported);
            Assert.Equal((byte)0, Field<Club>(view, "club").Id);
            Assert.Equal((byte)0, view.ClubController.RecordClub.Id);
        }, ListRecords);

        [Fact]
        public void PickedClubWithoutARecord_FallsBackToTheOwnClub() => WithOwnClub((fake, view) =>
        {
            view.ShowClub(new ClubSquads.Entry { Country = Country, Id = 9, Name = "Unbekannt" });

            // No human manager elsewhere either: the own club, from its record.
            Assert.Equal(OwnId, Field<Club>(view, "club").Id);
            Assert.Equal(OwnId, view.ClubController.RecordClub.Id);
        }, ListRecords);

        [Fact]
        public void OtherClubWithoutRecords_ShowsTheOwnClub() => WithOwnClub((fake, view) =>
        {
            view.RefreshValues(PlayerEnums.AddressType.CLUB);

            Assert.Equal(OwnId, Field<Club>(view, "club").Id);
            Assert.Null(view.ClubController.RecordClub);
            Assert.Equal(PlayerEnums.AddressType.OWN, view.ClubController.Type);
        }, withRecord: false);

        #region Live match
        // The own club's home match in minute 70, 2:0 against a club without a record (ID 77).
        private static uint SeedHomeMatch(FakeModule fake)
        {
            LiveMatchTests.SeedMatch(fake);
            uint match = Table + Settings.MatchStride;
            fake.WriteModuleBytes(Clock + 8, new byte[] { 70, 0 });
            fake.WriteModuleBytes(match, new byte[] { (byte)Country, 0, OwnId, 0, (byte)Country, 0, 77, 0 });
            fake.WriteModuleBytes(match + 0xE, new byte[] { 2, 0, 0, 0 });
            fake.WriteModuleBytes(match + 0x84, new byte[] { 3, 2, 4, 0, 1 });
            return match;
        }

        private static LiveMatch FindMatch(FakeModule fake) =>
            LiveMatch.Find(fake.Memory, FixedAddresses.For(fake.Memory), Country, OwnId, fake.ModuleBase + Table, fake.ModuleBase + Clock);

        private static void ShowMatch(ClubView view, LiveMatch match) =>
            typeof(ClubView).GetMethod("ShowLiveMatch", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(LiveMatch) }, null).Invoke(view, new object[] { match });

        [Fact]
        public void LiveTab_HomeMatch_NamesAnUnknownOpponent_AndWritesHardnessAndEffort() => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (ClubView view = new ClubView(fake.Memory, null))
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                uint match = SeedHomeMatch(fake);
                SetField(view, "club", new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN).Club);
                ShowMatch(view, FindMatch(fake));

                Assert.Equal($"70. Minute, 2:0 gegen {Country} 77", Field<Label>(view, "liveStatusValue").Text);

                // A known opponent is named, and the own club's squad comes from its record.
                ListRecords(fake);
                fake.WriteModuleBytes(match + 6, new byte[] { 2, 0 });
                ShowMatch(view, FindMatch(fake));
                Assert.Equal("70. Minute, 2:0 gegen FC Hannover 66", Field<Label>(view, "liveStatusValue").Text);
                Assert.Equal(2, Field<ComboBox>(view, "liveHardnessInput").SelectedIndex);

                Field<ComboBox>(view, "liveHardnessInput").SelectedIndex = 4;
                Field<ComboBox>(view, "liveEffortInput").SelectedIndex = 0;
                Invoke(view, "ApplyLiveMatch");

                Assert.Equal(new byte[] { 3, 4, 0 }, fake.ReadModuleBytes(match + 0x84, 3));
                // The computer must leave the home team's tactics alone now.
                Assert.Equal((byte)(2 | 4), fake.ReadModuleBytes(match + 0x240, 1)[0]);
            }
        });

        [Fact]
        public void LiveTab_ClubWithoutARecord_OffersOnlyTheBench_AndAnEmptiedPlaceIsWritten() => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (ClubView view = new ClubView(fake.Memory, null))
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                SeedHomeMatch(fake);
                // A club the records don't know: no squad, the bench players only.
                SetField(view, "club", new Club { Country = Country, Id = 99 });
                ShowMatch(view, FindMatch(fake));

                ComboBox[] places = Field<ComboBox[]>(view, "liveBenchInputs");
                Assert.Equal((ushort)120, places[0].SelectedValue);
                Assert.Equal(new ushort[] { 0, 120 }, places[0].Items.Cast<NationalSquads.Member>().Select(m => m.Id).ToArray());

                // Emptying a place without picking "–" (no selection) frees the player for the others.
                places[0].SelectedIndex = -1;
                Assert.Contains((ushort)120, places[1].Items.Cast<NationalSquads.Member>().Select(m => m.Id));
                Invoke(view, "ApplyLiveMatch");

                Assert.Equal(new byte[] { 0, 0, 121, 0 }, fake.ReadModuleBytes(ClubEditorSettingsTests.OtherCopy + Settings.ClubBenchOffset, 4));
            }
        });
        #endregion
    }
}
