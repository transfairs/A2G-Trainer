using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
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
    /// The "Ligen" view on its own with parts of the game's memory unreadable, a played league's
    /// selection, a club without record and the order written without a selection.
    /// </summary>
    public class LeagueViewCoverageGapTests
    {
        private static T Field<T>(LeagueView view, string name) =>
            (T)typeof(LeagueView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        private static void Invoke(LeagueView view, string name, params object[] args) =>
            typeof(LeagueView).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, args);

        private static void Click(Button button) =>
            typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(button, new object[] { EventArgs.Empty });

        // As LeagueViewTests seeds the game; prepare runs after the seeding, before the first read.
        private static void WithGame(Action<FakeModule> prepare, Action<FakeModule, LeagueView> test) => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (LeagueView view = new LeagueView(fake.Memory, new ProcessController(null)))
            {
                LeagueTablesTests.SeedLeagues(fake);
                LeagueTablesTests.SeedEuropeanCountries(fake, 5);
                view.CupClubRecords = fake.ModuleBase + LeagueTablesTests.EuropeanRecords;
                prepare(fake);
                Assert.NotEqual(IntPtr.Zero, Field<ListView>(view, "tableList").Handle);
                view.RefreshValues();
                test(fake, view);
            }
        });

        private static string[] Names(ListView list) => list.Items.Cast<ListViewItem>().Select(i => i.SubItems[1].Text).ToArray();

        private static uint LicenceByte(int record) => Settings.ClubRecordTableOffset + (uint)record * Settings.ClubRecordStride + 0xF5;

        [Fact]
        public void WithTheHalfWeekUnreadable_TheSeasonIsUnknown() => WithGame(
            fake => fake.MakeModuleRegionUnreadable(Settings.HalfWeekOffset, 2),
            (fake, view) =>
            {
                Assert.Equal("Kein Spiel verbunden.", Field<Label>(view, "seasonStatus").Text);
                Assert.Empty(Field<ComboBox>(view, "cupCountryInput").Items);
                Assert.False(Field<Button>(view, "cupApplyButton").Enabled);
            });

        [Fact]
        public void WithTheChampionUnreadable_NoChampionIsShown() => WithGame(
            fake => fake.MakeModuleRegionUnreadable(Settings.CupChampionOffset + (uint)PlayerEnums.Country.Holland, 1),
            (fake, view) =>
            {
                Assert.Equal("Holland", Field<ComboBox>(view, "cupCountryInput").Text);
                Assert.Null(Field<ComboBox>(view, "championInput").SelectedItem);
                Assert.Equal("PSV", Field<ComboBox>(view, "cupWinnerInput").Text);
            });

        [Fact]
        public void Licence_OfAPlayedLeagueIsOpen_AndWithoutLeagueClosed() => WithGame(_ => { }, (fake, view) =>
        {
            ListView table = Field<ListView>(view, "tableList");
            CheckBox refused = Field<CheckBox>(view, "licenceRefusedInput");
            Assert.Equal(0, Field<ComboBox>(view, "leagueInput").SelectedIndex);

            table.Items[0].Selected = true;
            Assert.True(refused.Enabled);

            // The inputs still show the club's licence: nothing to write, the table stays as it is.
            ListViewItem selected = table.SelectedItems[0];
            Invoke(view, "WriteLicence");
            Assert.Same(selected, table.SelectedItems[0]);
            Assert.Equal(new byte[] { 0 }, fake.ReadModuleBytes(LicenceByte(0), 1));

            // The leagues are gone while the table still shows a selection.
            typeof(LeagueView).GetField("leagues", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, new List<LeagueTable>());
            Invoke(view, "UpdateButtons");
            Assert.False(refused.Enabled);
            Assert.False(Field<ComboBox>(view, "deductionInput").Enabled);
        });

        [Fact]
        public void Deduction_WithTheLicenceGranted_IsWrittenAlone() => WithGame(_ => { }, (fake, view) =>
        {
            Field<ComboBox>(view, "leagueInput").SelectedIndex = 2;
            ListView table = Field<ListView>(view, "tableList");
            table.Items.Cast<ListViewItem>().First(i => i.SubItems[1].Text == "E3").Selected = true;
            Assert.False(Field<CheckBox>(view, "licenceRefusedInput").Checked);

            Field<ComboBox>(view, "deductionInput").SelectedIndex = 1;

            Assert.Equal(new byte[] { 0x01 }, fake.ReadModuleBytes(LicenceByte(13), 1));
        });

        [Fact]
        public void Licence_OfAClubWithoutRecord_IsNotWritten() => WithGame(
            fake => fake.WriteModuleBytes(Settings.LeagueTableOffset + 2 * Settings.LeagueStride + Settings.LeagueClubIdsOffset + 3 * 2, BitConverter.GetBytes((short)12)),
            (fake, view) =>
            {
                Field<ComboBox>(view, "leagueInput").SelectedIndex = 2;
                ListView table = Field<ListView>(view, "tableList");
                ListViewItem club = table.Items.Cast<ListViewItem>().First(i => i.SubItems[1].Text == "Verein 12");
                club.Selected = true;
                ComboBox deduction = Field<ComboBox>(view, "deductionInput");
                Assert.True(deduction.Enabled);

                deduction.SelectedIndex = 1;

                // Nothing changed: the table isn't rebuilt and still shows the club without deduction.
                Assert.Same(club, table.SelectedItems[0]);
                Assert.Equal(0, ((LeagueTableRow)club.Tag).Licence);
            });

        [Fact]
        public void Order_WithoutSelection_IsWrittenAsShown() => WithGame(_ => { }, (fake, view) =>
        {
            Field<ComboBox>(view, "leagueInput").SelectedIndex = 2;
            ListView table = Field<ListView>(view, "tableList");
            Assert.Empty(table.SelectedItems);
            List<int> shown = view.ShownOrder();
            Button apply = Field<Button>(view, "applyOrderButton");
            Assert.True(apply.Enabled);

            Click(apply);

            Assert.Equal(shown, view.ShownOrder());
            Assert.Empty(table.SelectedItems);
            Assert.Equal(shown, LeagueTables.ReadAll(fake.Memory, FixedAddresses.For(fake.Memory))[2].Standings().Select(r => r.ListPosition));
        });
    }
}
