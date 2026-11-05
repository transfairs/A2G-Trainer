using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// CoachView on its own (no Trainer window, a build without fixed addresses so nothing reads the
    /// national squads): championship tips, the national squad box, friendlies, stock countries, the
    /// career and level tabs without or with a coach set directly.
    /// </summary>
    public class CoachViewCoverageTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private static T Field<T>(object target, string name) => (T)typeof(CoachView).GetField(name, Private).GetValue(target);
        private static void SetField(object target, string name, object value) => typeof(CoachView).GetField(name, Private).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] args) => typeof(CoachView).GetMethod(name, Private).Invoke(target, args);

        private static void Raise(Control control, string onEvent, EventArgs args) =>
            control.GetType().GetMethod(onEvent, Private, null, new[] { typeof(EventArgs) }, null).Invoke(control, new object[] { args });

        // The competency inputs are bound by name to a level's distribution; until a coach is shown
        // their binding source has nothing with those properties, and creating the handles would
        // fail every one of those bindings. Point it at a real (empty) distribution first, as
        // ShowCompetencyLevel does with the coach's.
        private static void CreateHandle(CoachView view)
        {
            Field<BindingSource>(view, "competencyBindingSource").DataSource = new CoachCompetencyLevel();
            IntPtr handle = view.Handle;
        }

        private static void WithView(Action<FakeModule, CoachView> test) => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (CoachView view = new CoachView(fake.Memory, new ProcessController(null)))
            {
                // No fixed addresses: the national squads (a scan of every player record) stay off.
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                CreateHandle(view);
                test(fake, view);
            }
        });

        // RefreshValues + InitClubTabControl as the trainer does it, on the seeded league.
        private static void WithLoadedView(Action<FakeModule, CoachView> test) => WithView((fake, view) =>
        {
            CoachViewTests.SeedLeagueAndStock(fake);
            view.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
            view.InitClubTabControl();
            test(fake, view);
        });

        #region Championship tips
        [Fact]
        public void ShowChampionTips_BeforeTheTabIsSetUp_DoesNothing() => WithView((fake, view) =>
        {
            SetField(view, "coach", new Coach());

            Invoke(view, "ShowChampionTips");

            Assert.Null(Field<ComboBox>(view, "TipCountryInput").DataSource);
        });

        [Fact]
        public void ChampionTips_KeepTheChosenCountry_AndTakeTheClubChosen() => WithLoadedView((fake, view) =>
        {
            Coach coach = Field<Coach>(view, "coach");
            ChampionTip english = new ChampionTip { Country = PlayerEnums.Country.England, ClubId = 1 };
            coach.ChampionTips[PlayerEnums.Country.Deutschland] = new ChampionTip { Country = PlayerEnums.Country.Deutschland };
            coach.ChampionTips[PlayerEnums.Country.England] = english;
            SetField(view, "allClubs", new BindingList<Club>
            {
                new Club { Country = PlayerEnums.Country.England, Id = 1, ClubName = "Arsenal" },
                new Club { Country = PlayerEnums.Country.England, Id = 5, ClubName = "Chelsea" },
            });
            ComboBox country = Field<ComboBox>(view, "TipCountryInput");
            ComboBox club = Field<ComboBox>(view, "TipClubInput");
            Invoke(view, "ShowChampionTips");
            country.SelectedValue = PlayerEnums.Country.England;

            Invoke(view, "ShowChampionTips");

            Assert.Equal(PlayerEnums.Country.England, country.SelectedValue);
            Assert.Equal(2, club.Items.Count);
            club.SelectedValue = (ushort)5;
            Raise(club, "OnSelectionChangeCommitted", EventArgs.Empty);
            Assert.Equal((ushort)5, english.ClubId);

            // Without a tip shown the choice goes nowhere.
            Field<BindingSource>(view, "championTipBindingSource").DataSource = typeof(ChampionTip);
            club.SelectedValue = (ushort)1;
            Raise(club, "OnSelectionChangeCommitted", EventArgs.Empty);
            Assert.Equal((ushort)5, english.ClubId);
        });
        #endregion

        #region National squad
        [Fact]
        public void NationalSquadBox_StartsOnTheCoachsNationalTeam_OrElseDeutschland() => WithView((fake, view) =>
        {
            Invoke(view, "InitNationalSquadBox");
            Assert.Equal(PlayerEnums.Country.Deutschland, Field<ComboBox>(view, "NationalSquadCountryInput").SelectedValue);
            Assert.False(Field<GroupBox>(view, "NationalSquadBox").Enabled);

            using (CoachView second = new CoachView(fake.Memory, new ProcessController(null)))
            {
                CreateHandle(second);
                SetField(second, "coach", new Coach { NationalTeam = PlayerEnums.Country.Italien });

                Invoke(second, "InitNationalSquadBox");

                Assert.Equal(PlayerEnums.Country.Italien, Field<ComboBox>(second, "NationalSquadCountryInput").SelectedValue);
            }
        });

        [Fact]
        public void MoveSelected_WithoutSelectionOrRoom_MovesNothing() => StaThread.Run(() =>
        {
            MethodInfo move = typeof(CoachView).GetMethod("MoveSelected", BindingFlags.NonPublic | BindingFlags.Static);
            NationalSquads.Member member = new NationalSquads.Member { Id = 7, Name = "Sieben" };
            BindingList<NationalSquads.Member> from = new BindingList<NationalSquads.Member> { member };
            BindingList<NationalSquads.Member> to = new BindingList<NationalSquads.Member> { new NationalSquads.Member { Id = 1 } };
            using (Form form = new Form())
            using (ListBox list = new ListBox())
            {
                form.Controls.Add(list);
                IntPtr handle = form.Handle;
                list.DataSource = from;
                list.SelectedIndex = -1;

                move.Invoke(null, new object[] { list, from, to, int.MaxValue });
                Assert.Single(from);

                list.SelectedIndex = 0;
                move.Invoke(null, new object[] { list, from, to, 1 });
                Assert.Single(from);
                Assert.Single(to);

                move.Invoke(null, new object[] { list, from, to, 2 });
                Assert.Empty(from);
                Assert.Same(member, to[1]);
            }
        });

        [Fact]
        public void SaveNationalSquad_WithoutACountry_DoesNothing() => WithView((fake, view) =>
        {
            Label status = Field<Label>(view, "NationalSquadStatus");
            status.Text = "unverändert";

            Invoke(view, "SaveNationalSquad");

            Assert.Equal("unverändert", status.Text);
        });
        #endregion

        #region Friendlies
        [Fact]
        public void Friendlies_ShowAnOpponentOutsideTheChoices_AndTakeTheOneChosen() => WithView((fake, view) =>
        {
            Coach coach = new Coach { NationalTeam = PlayerEnums.Country.Italien };
            // Against the own national team - never offered, but kept as stored.
            NationalFriendly friendly = new NationalFriendly { Index = 0, Opponent = PlayerEnums.Country.Italien };
            coach.Friendlies.Add(friendly);
            SetField(view, "coach", coach);
            ComboBox opponent = Field<ComboBox[]>(view, "friendlyOpponentInputs")[0];

            Invoke(view, "ShowFriendlies");

            Assert.Equal(PlayerEnums.Country.Italien, opponent.SelectedValue);
            Assert.Equal("Land " + (byte)PlayerEnums.Country.Italien, opponent.Text);
            Assert.True(opponent.Enabled);

            opponent.SelectedValue = PlayerEnums.Country.Spanien;
            Raise(opponent, "OnSelectionChangeCommitted", EventArgs.Empty);
            Assert.Equal(PlayerEnums.Country.Spanien, friendly.Opponent);

            // No country chosen: the friendly keeps its opponent.
            opponent.DataSource = null;
            Raise(opponent, "OnSelectionChangeCommitted", EventArgs.Empty);
            Assert.Equal(PlayerEnums.Country.Spanien, friendly.Opponent);

            // The second friendly doesn't exist: its choice goes nowhere.
            ComboBox second = Field<ComboBox[]>(view, "friendlyOpponentInputs")[1];
            second.SelectedValue = PlayerEnums.Country.Spanien;
            Raise(second, "OnSelectionChangeCommitted", EventArgs.Empty);
            Assert.Single(coach.Friendlies);
        });
        #endregion

        #region Stocks and next season
        [Fact]
        public void StockCountry_KeepsAStoredCountryOutsideTheLeague() => WithView((fake, view) =>
        {
            Coach coach = new Coach();
            coach.Stocks[0].Country = PlayerEnums.Country.Italien;
            coach.Stocks[0].Shares = 10;
            SetField(view, "coach", coach);
            SetField(view, "league", new LeagueSettings { MainCountry = PlayerEnums.Country.Deutschland });
            ComboBox combo = Field<ComboBox[]>(view, "stockCountryCombos")[0];

            Invoke(view, "PopulateStockCountryCombo", 0, PlayerEnums.Country.Italien);

            Assert.Equal(3, combo.Items.Count);
            Assert.Equal(PlayerEnums.Country.Italien, combo.SelectedValue);
        });
        #endregion

        #region Career
        [Fact]
        public void ShowCareer_WithoutTheCareerTab_DoesNothing() => WithView((fake, view) =>
        {
            TextBox start = Field<TextBox>(view, "careerStartInput");
            start.Text = "1999";
            SetField(view, "careerTab", null);

            Invoke(view, "ShowCareer");

            Assert.Equal("1999", start.Text);
        });

        [Fact]
        public void ShowCareerEnd_WithoutACoach_ShowsADash() => WithView((fake, view) =>
        {
            Label end = Field<Label>(view, "careerEndValue");
            end.Text = "x";

            Invoke(view, "ShowCareerEnd");

            Assert.Equal("-", end.Text);

            SetField(view, "coach", new Coach());
            Invoke(view, "ShowCareerEnd");
            Assert.Equal("-", end.Text);
            SetField(view, "coach", new Coach { CurrentYear = 2001, CurrentSeason = 1, CareerMode = CoachEnums.CareerMode.OneYear });
            Invoke(view, "ShowCareerEnd");
            Assert.Equal("2001/02", end.Text);
        });

        [Fact]
        public void CareerCounters_ReachTheCoachOnlyWithAchievements() => WithView((fake, view) =>
        {
            TextBox[] inputs = Field<TextBox[]>(view, "achievementInputs");
            TextBox start = Field<TextBox>(view, "careerStartInput");

            // Without a coach, and for an input that isn't a counter.
            inputs[0].Text = "3";
            start.Text = "5";
            Coach coach = new Coach();
            SetField(view, "coach", coach);
            inputs[0].Text = "4";
            Assert.Null(coach.Achievements);

            coach.Achievements = new CoachAchievements();
            inputs[0].Text = "6";
            inputs[1].Text = "";
            Assert.Equal((short)6, coach.Achievements.Counters[0]);
            Assert.Equal((short)0, coach.Achievements.Counters[1]);
        });
        #endregion

        #region Level
        [Fact]
        public void PrepareLevelUp_WithoutACoach_DoesNothing() => WithView((fake, view) =>
        {
            Button prepare = Field<Button>(view, "prepareLevelUpButton");
            Raise(prepare, "OnClick", EventArgs.Empty);

            Coach coach = new Coach { CurrentSeason = 3 };
            SetField(view, "coach", coach);
            Raise(prepare, "OnClick", EventArgs.Empty);
            Assert.Equal((short)2, coach.LevelState.LastChangeSeason);
            Assert.Equal(24, coach.LevelState.WinStreak);
        });

        [Fact]
        public void LevelBitBoxes_SetAndClearTheirBit() => WithView((fake, view) =>
        {
            Coach coach = new Coach();
            SetField(view, "coach", coach);
            CheckBox promotion = Field<List<KeyValuePair<CheckBox, Func<CoachLevelState, bool>>>>(view, "levelChecks")
                .Select(c => c.Key).Single(c => c.Text == "Meister");
            coach.LevelState.SeasonSuccess = 0x01;

            promotion.Checked = true;
            Assert.Equal(0x03, coach.LevelState.SeasonSuccess);
            promotion.Checked = false;
            Assert.Equal(0x01, coach.LevelState.SeasonSuccess);
        });

        [Fact]
        public void ShowLevelState_WithoutTheBindingSource_DoesNothing_AndRefusalsAloneAllowTheReset() => WithView((fake, view) =>
        {
            Coach coach = new Coach();
            coach.LevelState.NaturalisationRefusals = 2;
            SetField(view, "coach", coach);
            CheckBox reset = Field<CheckBox>(view, "resetNaturalisationInput");
            BindingSource levelBindingSource = Field<BindingSource>(view, "levelBindingSource");

            SetField(view, "levelBindingSource", null);
            Invoke(view, "ShowLevelState");
            Assert.Equal("", Field<Label>(view, "naturalisationValue").Text);

            SetField(view, "levelBindingSource", levelBindingSource);
            Invoke(view, "ShowLevelState");
            Assert.True(reset.Enabled);
            Assert.Equal("0 Gespräche, 2 Ablehnungen", Field<Label>(view, "naturalisationValue").Text);

            coach.LevelState.NaturalisationRefusals = 0;
            Invoke(view, "ShowLevelState");
            Assert.False(reset.Enabled);
            coach.LevelState.NaturalisationTalks = 1;
            Invoke(view, "ShowLevelState");
            Assert.True(reset.Enabled);
        });
        #endregion
    }
}
