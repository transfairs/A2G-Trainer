using AnstossTrainer.Shared;
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
    /// Tests for the "Spielmechaniken" view: the quiz and interview helps, the goal limit patch and
    /// the requested job offer, and its menu entry. Each Trainer window costs the x86 test host
    /// ~20 MB, so the checks share as few windows as they can.
    /// </summary>
    public class MechanicsViewTests
    {
        private static T Field<T>(MechanicsView view, string name) =>
            (T)typeof(MechanicsView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        private static void SetField(MechanicsView view, string name, object value) =>
            typeof(MechanicsView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(view, value);

        private static void InvokePrivate(object target, string methodName) =>
            target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

        private static List<KeyValuePair<int, Coach>> OneTrainer() =>
            new List<KeyValuePair<int, Coach>> { new KeyValuePair<int, Coach>(0, new Coach { Firstname = "Robin", Lastname = "Trauer" }) };

        [Theory]
        [InlineData(JobOfferInjector.RequestResult.Requested, "Tagesabschluss")]
        [InlineData(JobOfferInjector.RequestResult.NotEmployed, "vereinslos")]
        [InlineData(JobOfferInjector.RequestResult.ChangePending, "Kein Wechsel")]
        [InlineData(JobOfferInjector.RequestResult.SameClub, "aktuelle Verein")]
        [InlineData(JobOfferInjector.RequestResult.NotSupported, "Spielversion")]
        [InlineData(JobOfferInjector.RequestResult.HookFailed, "Logdatei")]
        public void JobOfferMessage_ExplainsEveryResult(JobOfferInjector.RequestResult result, string expected)
        {
            Assert.Contains(expected, MechanicsView.JobOfferMessage(result, "Testverein"));
        }

        [Fact]
        public void JobOfferMessage_MentionsTheClauseOnlyWhenItWasSet()
        {
            Assert.Contains("Freigabeklausel", MechanicsView.JobOfferMessage(JobOfferInjector.RequestResult.Requested, "Testverein", clauseSet: true));
            Assert.DoesNotContain("Freigabeklausel", MechanicsView.JobOfferMessage(JobOfferInjector.RequestResult.Requested, "Testverein"));
        }

        [Fact]
        public void MenuEntry_ShowsTheViewWithTheTrainersOfTheMenu() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                CoachViewTests.SeedLeagueAndStock(fake);
                trainer.RefreshTrainerMenu(OneTrainer());
                MenuStrip menu = (MenuStrip)typeof(Trainer).GetField("menuStrip", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer);
                ToolStripItem entry = menu.Items["spielmechanikenToolStripMenuItem"];
                Assert.Equal(menu.Items.IndexOf(menu.Items["spielstandToolStripMenuItem"]) - 1, menu.Items.IndexOf(entry));

                entry.PerformClick();

                MechanicsView view = trainer.MechanicsView;
                Panel content = (Panel)typeof(Trainer).GetField("ContentPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer);
                Assert.Contains(view, content.Controls.Cast<Control>());
                Assert.EndsWith("Spielmechaniken", trainer.Text);
                Assert.Equal("Diese Funktionen wirken nur, solange A2G-Trainer und Spiel laufen.", Field<Label>(view, "noticeLabel").Text);
                ComboBox trainers = Field<ComboBox>(view, "jobOfferTrainerInput");
                Assert.Equal("Robin Trauer", trainers.Text);
                Assert.True(Field<GroupBox>(view, "jobOfferBox").Enabled);
                ComboBox countries = Field<ComboBox>(view, "jobOfferCountryInput");
                Assert.NotEmpty(((System.Collections.IEnumerable)countries.DataSource).Cast<object>());

                // A country chosen before stays chosen; a slot that's gone falls back to the first one.
                countries.SelectedValue = PlayerEnums.Country.England;
                typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Field<Button>(view, "reloadButton"), new object[] { EventArgs.Empty });
                Assert.Equal(PlayerEnums.Country.England, countries.SelectedValue);
                view.SetTrainers(new List<KeyValuePair<int, Coach>> { new KeyValuePair<int, Coach>(1, new Coach()) });
                Assert.Equal("Trainer 2", trainers.Text);

                // Without the game the box is off.
                using (MechanicsView detached = new MechanicsView(new ProcessMemory(), null))
                {
                    detached.RefreshValues();
                    Assert.False(Field<GroupBox>(detached, "jobOfferBox").Enabled);
                    Assert.False(Field<CheckBox>(detached, "goalLimitInput").Checked);

                    // Where the game's code can't be patched, the boxes don't stay ticked.
                    foreach (string name in new[] { "interviewFixInput", "goalLimitInput" })
                    {
                        CheckBox box = Field<CheckBox>(detached, name);
                        box.Checked = true;
                        Assert.False(box.Checked);
                    }
                }
            }
        });

        [Fact]
        public void RequestJobOffer_ShowsTheOutcomeInTheStatusLine() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                MechanicsView view = trainer.MechanicsView;
                Label status = Field<Label>(view, "jobOfferStatus");

                // Without a coach nothing is requested.
                InvokePrivate(view, "RequestJobOffer");
                Assert.Equal("", status.Text);

                // A build without the hook says so.
                CoachViewTests.SeedLeagueAndStock(fake);
                fake.Memory.Layout = CoachViewTests.UnconfirmedLayout;
                view.SetTrainers(OneTrainer());
                view.RefreshValues();
                SetField(view, "allClubs", CoachViewTests.TwoGermanClubs());
                InvokePrivate(view, "PopulateJobOfferCountryCombo");
                InvokePrivate(view, "RequestJobOffer");
                Assert.Equal(MechanicsView.JobOfferMessage(JobOfferInjector.RequestResult.NotSupported, "Aachen"), status.Text);

                // Without a coach every club of the country is offered (unconfirmed build: no eligibility filter).
                SetField(view, "coach", null);
                InvokePrivate(view, "PopulateJobOfferClubCombo");
                Assert.Equal(2, ((List<Club>)Field<ComboBox>(view, "jobOfferClubInput").DataSource).Count);
            }
        });

        [Fact]
        public void RequestJobOffer_WhenTheGameTakesTheRequest_SaysWhenTheOfferComes() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                CoachViewTests.SeedLeagueAndStock(fake);
                CoachViewTests.SeedGermanLeagueWithAachenOnly(fake);
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.CLUB], new byte[] { 55 });
                // The game's unpatched CALL to its offer presentation.
                uint callSite = fake.ModuleBase + Settings.JobOfferPresentCallSiteOffset;
                int rel = unchecked((int)(fake.ModuleBase + Settings.JobOfferPresentFunctionOffset - (callSite + 5)));
                fake.WriteModuleBytes(Settings.JobOfferPresentCallSiteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
                MechanicsView view = trainer.MechanicsView;
                view.SetTrainers(OneTrainer());
                view.RefreshValues();
                SetField(view, "allClubs", CoachViewTests.TwoGermanClubs());
                InvokePrivate(view, "PopulateJobOfferCountryCombo");

                InvokePrivate(view, "RequestJobOffer");

                Assert.StartsWith("Aachen bietet beim nächsten Tagesabschluss", Field<Label>(view, "jobOfferStatus").Text);
            }
        });

        [Fact]
        public void Switches_WorkInTheGameAndAreTakenBackWithTheView() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                // The interview calls InterviewEffectsFix redirects and the goal limits, as in the unpatched game.
                InterviewEffectsFixTests.WriteOriginalCalls(fake);
                GoalLimitPatchTests.PlantGoalLimitCode(fake, Settings.GoalLimitSiteOffset, Settings.GoalHomeBrakeSiteOffset, Settings.GoalAwayBrakeSiteOffset, GoalLimitPatchTests.OriginalLimit);
                CoachViewTests.SeedLeagueAndStock(fake);
                MechanicsView view = trainer.MechanicsView;

                CheckBox quiz = Field<CheckBox>(view, "quizHelpInput");
                quiz.Checked = true;
                Assert.True(Field<QuizHelper>(view, "quizHelper").Enabled);
                view.RefreshValues();
                Assert.True(quiz.Checked);
                quiz.Checked = false;
                Assert.False(Field<QuizHelper>(view, "quizHelper").Enabled);

                CheckBox display = Field<CheckBox>(view, "interviewDisplayInput");
                CheckBox fix = Field<CheckBox>(view, "interviewFixInput");
                display.Checked = true;
                Assert.True(Field<InterviewEffectsDisplay>(view, "interviewDisplay").Enabled);
                fix.Checked = true;
                Assert.True(fix.Checked);
                uint callSite = view.Memory.ModuleBase + Settings.InterviewApplyCallSiteOffset;
                uint stub = unchecked((uint)(callSite + 5 + BitConverter.ToInt32(view.Memory.ReadBytesAtAddress(callSite + 1, 4), 0))) - InterviewEffectsFix.CodeOffset;
                Assert.Equal(1, view.Memory.ReadBytesAtAddress(stub + InterviewEffectsFix.FlagOffset, 1)[0]);
                view.RefreshValues();
                Assert.True(display.Checked && fix.Checked);
                display.Checked = false;
                Assert.False(Field<InterviewEffectsDisplay>(view, "interviewDisplay").Enabled);
                fix.Checked = false;
                Assert.Equal(0, view.Memory.ReadBytesAtAddress(stub + InterviewEffectsFix.FlagOffset, 1)[0]);

                // The goal limit box follows the patch in the game, also over a refresh.
                CheckBox goals = Field<CheckBox>(view, "goalLimitInput");
                goals.Checked = true;
                Assert.True(new GoalLimitPatch(fake.Memory).IsEnabled);
                view.RefreshValues();
                Assert.True(goals.Checked);
                Assert.True(new GoalLimitPatch(fake.Memory).IsEnabled);
                goals.Checked = false;
                Assert.False(new GoalLimitPatch(fake.Memory).IsEnabled);

                // Closing the trainer takes the patches back.
                fix.Checked = true;
                goals.Checked = true;
                view.Dispose();
                Assert.Equal(0, view.Memory.ReadBytesAtAddress(stub + InterviewEffectsFix.FlagOffset, 1)[0]);
                Assert.False(new GoalLimitPatch(fake.Memory).IsEnabled);
            }
        });

        // Lifted by A2G-Patch (exe or msvfw32.dll): the box shows it, closing leaves it alone.
        [Fact]
        public void GoalLimits_LiftedOutsideTheTrainer_StayLiftedWhenItCloses() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                GoalLimitPatchTests.PlantGoalLimitCode(fake, Settings.GoalLimitSiteOffset, Settings.GoalHomeBrakeSiteOffset, Settings.GoalAwayBrakeSiteOffset, GoalLimitPatchTests.OriginalLimit);
                CoachViewTests.SeedLeagueAndStock(fake);
                Assert.True(new GoalLimitPatch(fake.Memory).SetEnabled(true));
                MechanicsView view = trainer.MechanicsView;

                view.RefreshValues();
                Assert.True(Field<CheckBox>(view, "goalLimitInput").Checked);
                view.Dispose();

                Assert.True(new GoalLimitPatch(fake.Memory).IsEnabled);
            }
        });
    }
}
