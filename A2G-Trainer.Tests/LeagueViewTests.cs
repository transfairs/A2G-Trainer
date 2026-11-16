using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
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
    /// The "Ligen" view on its own (no main window: each one costs the x86 test host ~20 MB): the
    /// tables, a rolled league's order, licence and deduction, and the European cup titles.
    /// </summary>
    public class LeagueViewTests
    {
        private static T Field<T>(LeagueView view, string name) =>
            (T)typeof(LeagueView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        private static void Invoke(LeagueView view, string name, params object[] args) =>
            typeof(LeagueView).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, args);

        private static void Click(Button button) =>
            typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(button, new object[] { EventArgs.Empty });

        private static void WithGame(Action<FakeModule, LeagueView> test, short halfWeek = 5, bool englandBonus = true) => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (LeagueView view = new LeagueView(fake.Memory, new ProcessController(null)))
            {
                LeagueTablesTests.SeedLeagues(fake, halfWeek, englandBonus);
                LeagueTablesTests.SeedEuropeanCountries(fake, halfWeek);
                view.CupClubRecords = fake.ModuleBase + LeagueTablesTests.EuropeanRecords;
                // The table reports a selection only once it has a window.
                Assert.NotEqual(IntPtr.Zero, Field<ListView>(view, "tableList").Handle);
                view.RefreshValues();
                test(fake, view);
            }
        });

        private static string[] Names(ListView list) => list.Items.Cast<ListViewItem>().Select(i => i.SubItems[1].Text).ToArray();

        [Fact]
        public void Tables_ShowEveryLeagueWithItsMode() => WithGame((fake, view) =>
        {
            ComboBox leagues = Field<ComboBox>(view, "leagueInput");
            ListView table = Field<ListView>(view, "tableList");
            Label status = Field<Label>(view, "leagueStatus");

            Assert.Equal(new[] { "Deutschland – Bundesliga", "England – Premier League", "England – Division One" }, leagues.Items.Cast<string>());
            Assert.Equal("gespielt, 1 von 6 Spieltagen", status.Text);
            Assert.Equal("D0", table.Items[0].SubItems[1].Text);
            Assert.False(Field<Button>(view, "applyOrderButton").Enabled);

            leagues.SelectedIndex = 1;
            Assert.Equal("ohne Spiele, Reihenfolge der Vereinsliste", status.Text);
            Assert.Equal(new[] { "E2", "E0", "E1" }, Names(table));
            table.Items[0].Selected = true;
            Assert.False(Field<CheckBox>(view, "licenceRefusedInput").Enabled);

            leagues.SelectedIndex = 2;
            Assert.Equal("ausgewürfelt, ohne Auf- und Abstieg", status.Text);
            Assert.Equal(new[] { "E3", "E5", "E6", "E4" }, Names(table));
            Assert.All(table.Items.Cast<ListViewItem>(), i => Assert.Equal(LeagueView.BracketBackColor, i.BackColor));
            Assert.Equal("3", table.Items[0].SubItems[7].Text);
            // England isn't chosen: neither the order nor the licence would change anything.
            table.Items[0].Selected = true;
            Assert.False(Field<Button>(view, "applyOrderButton").Enabled);
            Assert.False(Field<Button>(view, "downButton").Enabled);
            Assert.False(Field<CheckBox>(view, "licenceRefusedInput").Enabled);
        }, englandBonus: false);

        [Fact]
        public void Order_IsMovedAndWrittenAsResults() => WithGame((fake, view) =>
        {
            Field<ComboBox>(view, "leagueInput").SelectedIndex = 2;
            Assert.Equal("Ergebnisse beim Saisonstart ausgewürfelt", Field<Label>(view, "leagueStatus").Text);
            ListView table = Field<ListView>(view, "tableList");
            Button up = Field<Button>(view, "upButton"), down = Field<Button>(view, "downButton");
            Assert.False(up.Enabled);

            table.Items[3].Selected = true;
            Assert.True(up.Enabled);
            Assert.False(down.Enabled);
            Click(up);
            Click(up);
            Click(up);
            Click(up);   // already on top: nothing
            Invoke(view, "MoveSelection", 1);
            Invoke(view, "MoveSelection", -1);
            Assert.Equal(new[] { 1, 0, 2, 3 }, view.ShownOrder());
            Assert.Equal("1", table.Items[0].Text);

            Click(Field<Button>(view, "applyOrderButton"));

            Assert.Equal(new[] { "E4", "E3", "E5", "E6" }, Names(table));
            Assert.Equal("E4", table.SelectedItems[0].SubItems[1].Text);
            Assert.Equal(new[] { 1, 0, 2, 3 }, LeagueTables.ReadAll(fake.Memory, FixedAddresses.For(fake.Memory))[2].Standings().Select(r => r.ListPosition));

            // Without a selection the buttons do nothing.
            table.SelectedItems.Clear();
            Invoke(view, "MoveSelection", 1);
            Assert.Equal(new[] { 1, 0, 2, 3 }, view.ShownOrder());
        });

        [Fact]
        public void Order_AfterTheSeasonEnd_CantBeChosen() => WithGame((fake, view) =>
        {
            Field<ComboBox>(view, "leagueInput").SelectedIndex = 2;
            Field<ListView>(view, "tableList").Items[1].Selected = true;
            Assert.False(Field<Button>(view, "upButton").Enabled);
            Assert.False(Field<Button>(view, "applyOrderButton").Enabled);
            Invoke(view, "ApplyOrder");
            Assert.Equal(new[] { "E3", "E5", "E6", "E4" }, Names(Field<ListView>(view, "tableList")));
        }, halfWeek: 999);

        [Fact]
        public void Licence_IsWrittenRightAwayAndReordersTheTable() => WithGame((fake, view) =>
        {
            Field<ComboBox>(view, "leagueInput").SelectedIndex = 2;
            ListView table = Field<ListView>(view, "tableList");
            CheckBox refused = Field<CheckBox>(view, "licenceRefusedInput");
            ComboBox deduction = Field<ComboBox>(view, "deductionInput");
            Assert.Equal(new[] { "keiner", "3 Punkte", "6 Punkte", "9 Punkte" }, deduction.Items.Cast<string>());

            table.Items[0].Selected = true;   // E3, 3 points
            Assert.True(refused.Enabled);
            Assert.Equal(0, deduction.SelectedIndex);

            refused.Checked = true;

            uint record = Settings.ClubRecordTableOffset + 13u * Settings.ClubRecordStride + 0xF5;
            Assert.Equal(new byte[] { 0x80 }, fake.ReadModuleBytes(record, 1));
            Assert.Equal("E3", Names(table).Last());
            Assert.Equal(System.Drawing.Color.Gray, table.SelectedItems[0].ForeColor);

            deduction.SelectedIndex = 2;
            Assert.Equal(new byte[] { 0x82 }, fake.ReadModuleBytes(record, 1));
            Assert.Equal("0", table.SelectedItems[0].SubItems[7].Text);

            // The same value again writes nothing.
            deduction.SelectedIndex = 2;
            table.SelectedItems.Clear();
            Assert.False(refused.Enabled);
            Assert.Equal(-1, deduction.SelectedIndex);
        });

        [Fact]
        public void CupTitles_AreAssignedBeforeTheDraw() => WithGame((fake, view) =>
        {
            ComboBox countries = Field<ComboBox>(view, "cupCountryInput");
            ComboBox champion = Field<ComboBox>(view, "championInput");
            ComboBox cupWinner = Field<ComboBox>(view, "cupWinnerInput");
            Label status = Field<Label>(view, "cupStatus");

            Assert.Equal(new[] { "Holland", "Island" }, countries.Items.Cast<object>().Select(c => c.ToString()));
            Assert.Equal("Europapokal noch nicht ausgelost, die Auslosung ist in etwa 4 Wochen.", Field<Label>(view, "seasonStatus").Text);
            Assert.Equal("Twente", champion.Text);
            Assert.Equal("PSV", cupWinner.Text);
            Assert.True(Field<Button>(view, "cupApplyButton").Enabled);

            cupWinner.SelectedIndex = 4;
            Click(Field<Button>(view, "cupApplyButton"));
            Assert.Equal("Meister und Pokalsieger müssen verschiedene Vereine sein.", status.Text);

            champion.SelectedIndex = 0;
            cupWinner.SelectedIndex = 2;
            Click(Field<Button>(view, "cupApplyButton"));
            Assert.Equal("Übernommen: Ajax ist Meister, Feyenoord Pokalsieger.", status.Text);
            Assert.Equal("Ajax", champion.Text);
            Assert.Equal("Feyenoord", cupWinner.Text);

            // A country chosen before stays chosen on reloading.
            countries.SelectedIndex = 1;
            Click(Field<Button>(view, "reloadButton"));
            Assert.Equal("Island", countries.Text);

            // The draw came meanwhile: nothing is taken.
            countries.SelectedIndex = 0;
            fake.WriteModuleBytes(Settings.HalfWeekOffset, BitConverter.GetBytes((short)12));
            champion.SelectedIndex = 3;
            Click(Field<Button>(view, "cupApplyButton"));
            Assert.Equal("Nicht übernommen: Der Europapokal ist schon ausgelost.", status.Text);
            Assert.Equal("Ajax", champion.Text);

            // Without a choice nothing happens.
            champion.SelectedIndex = -1;
            Invoke(view, "ApplyCupTitles");
            Assert.Equal("Ajax", Field<ComboBox>(view, "championInput").Items.Cast<object>().First().ToString());
        });

        [Fact]
        public void CupTitles_AfterTheDraw_AreLocked() => WithGame((fake, view) =>
        {
            Assert.StartsWith("Europapokal ist ausgelost.", Field<Label>(view, "seasonStatus").Text);
            Assert.False(Field<ComboBox>(view, "championInput").Enabled);
            Assert.False(Field<Button>(view, "cupApplyButton").Enabled);
        }, halfWeek: 40);

        [Fact]
        public void WithoutGame_EverythingIsEmpty() => StaThread.Run(() =>
        {
            using (LeagueView view = new LeagueView(new ProcessMemory(), null))
            {
                view.RefreshValues();
                Assert.Empty(Field<ComboBox>(view, "leagueInput").Items);
                Assert.Empty(Field<ListView>(view, "tableList").Items);
                Assert.Empty(Field<ComboBox>(view, "cupCountryInput").Items);
                Assert.Equal("Kein Spiel verbunden.", Field<Label>(view, "seasonStatus").Text);
                Assert.Equal("", Field<Label>(view, "leagueStatus").Text);
                Assert.False(Field<Button>(view, "cupApplyButton").Enabled);
                Invoke(view, "ApplyOrder");
                Invoke(view, "WriteLicence");
                Invoke(view, "ApplyCupTitles");
            }
        });

        [Fact]
        public void SeasonText_NamesThePhase()
        {
            Assert.Equal("Saison ist beendet. Der nächste Saisonstart lost Meister und Pokalsieger neu aus.", LeagueView.SeasonText(LeagueTables.SeasonPhase.SeasonOver, 999));
            // Two half-weeks a week: right after the season start six weeks to go, in the last one "this week".
            Assert.Equal("Europapokal noch nicht ausgelost, die Auslosung ist in etwa 6 Wochen.", LeagueView.SeasonText(LeagueTables.SeasonPhase.BeforeCupDraw, 0));
            Assert.Equal("Europapokal noch nicht ausgelost, die Auslosung ist in dieser Woche.", LeagueView.SeasonText(LeagueTables.SeasonPhase.BeforeCupDraw, 11));
            Assert.Equal("Europapokal noch nicht ausgelost, die Auslosung ist in etwa 6 Wochen.", LeagueView.SeasonText(LeagueTables.SeasonPhase.BeforeCupDraw, null));
            Assert.Equal("Kein Spiel verbunden.", LeagueView.SeasonText(LeagueTables.SeasonPhase.Unknown, null));
        }

        [Fact]
        public void ModeText_WithoutLeague_IsEmpty()
        {
            Assert.Equal("", LeagueView.ModeText(null));
        }
    }
}
