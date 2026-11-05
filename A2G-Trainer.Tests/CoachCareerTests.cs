using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for the manager's career data: both level bytes, the difficulty lock, career duration,
    /// player-coach state, Managerfortbildung and titles (CoachController) and the
    /// "Karriere" tab (CoachView.Career).
    /// </summary>
    public class CoachCareerTests
    {
        private static Addresses A => AddressPresets.COACH;

        private static uint Achievements => Settings.CoachAchievementsOffset + 1 * Settings.CoachAchievementsStride;

        private static CoachController Read(FakeModule fake) => new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

        // The values of trainer 1 read from a running game (2007 CD, 03.11.2026): season 1 of 1998,
        // career until retirement at 80, no titles, one station (Deutschland club 1 from 06.07.1998, both posts).
        private static void SeedCareer(FakeModule fake)
        {
            fake.WriteModuleBytes(Settings.SeasonIndexOffset, BitConverter.GetBytes((short)1));
            fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes((short)1998));
            CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.RETIREMENT_AGE], BitConverter.GetBytes((short)80));
            CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.CAREER_MODE], BitConverter.GetBytes((short)9));
            CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.CAREER_START], BitConverter.GetBytes((short)1));
            fake.WriteModuleBytes(Achievements + 0x1A4, new byte[] { 1, 0, 0, 0, 1, 0, 0, 0 });
            fake.WriteModuleBytes(Achievements + 0x1AC, new byte[] { 0x0E, 0, 1, 0, 6, 7, 0, 0, 0xCE, 7, 0, 0, 0, 3 });
        }

        [Fact]
        public void AddressPresets_MapTheCareerFields()
        {
            Assert.Equal("34B21", A[CoachEnums.AddressKey.LEVEL_BASE]);
            Assert.Equal("34B26", A[CoachEnums.AddressKey.RETIREMENT_AGE]);
            Assert.Equal("34B36", A[CoachEnums.AddressKey.CAREER_MODE]);
            Assert.Equal("34B38", A[CoachEnums.AddressKey.CAREER_START]);
            Assert.Equal("36328", A[CoachEnums.AddressKey.TRAINING_FLAGS]);
            Assert.Equal("36333", A[CoachEnums.AddressKey.TRAINING_RESULT]);
            Assert.Equal(Settings.CoachRecordTableOffset + Settings.CoachRecordStride + 0x1B27, CoachMemory.RecordOffset(0, A[CoachEnums.AddressKey.TRAINING_RESULT]));
        }

        [Fact]
        public void Difficulty_ReadsTheLevelAndTheLockSeparately()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.DIFFICULTY], new byte[] { 0x84 });

                Coach coach = Read(fake).Coach;

                Assert.Equal(CoachEnums.Difficulty.NurFuerDieBesten, coach.Difficulty);
                Assert.True(coach.DifficultyLocked);
            }
        }

        [Fact]
        public void Save_KeepsTheDifficultyLock_UnlessItIsLifted()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.DIFFICULTY], new byte[] { 0x83 });
                CoachController controller = Read(fake);

                controller.Coach.Difficulty = CoachEnums.Difficulty.UltraViolence;
                controller.Save();
                Assert.Equal((byte)0x85, CoachMemory.ReadWorkingCopy(fake, A[CoachEnums.AddressKey.DIFFICULTY], 1)[0]);
                Assert.Equal((byte)0x85, CoachMemory.ReadRecord(fake, A[CoachEnums.AddressKey.DIFFICULTY], 1)[0]);

                controller.Coach.UnlockDifficulty = true;
                controller.Save();
                Assert.Equal((byte)0x05, CoachMemory.ReadRecord(fake, A[CoachEnums.AddressKey.DIFFICULTY], 1)[0]);
                Assert.False(controller.Coach.DifficultyLocked);
                Assert.False(controller.Coach.UnlockDifficulty);
            }
        }

        [Fact]
        public void Save_WritesBothLevelBytes()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.LEVEL], new byte[] { 9 });
                CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.LEVEL_BASE], new byte[] { 7 });
                CoachController controller = Read(fake);
                Assert.Equal((byte)7, controller.Coach.LevelBase);

                controller.Coach.Level = 14;
                controller.Save();

                Assert.Equal((byte)14, CoachMemory.ReadWorkingCopy(fake, A[CoachEnums.AddressKey.LEVEL_BASE], 1)[0]);
                Assert.Equal((byte)14, CoachMemory.ReadRecord(fake, A[CoachEnums.AddressKey.LEVEL_BASE], 1)[0]);
                Assert.Equal((byte)14, CoachMemory.ReadRecord(fake, A[CoachEnums.AddressKey.LEVEL], 1)[0]);
            }
        }

        [Fact]
        public void GetEntity_ReadsCareerAndTitles()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedCareer(fake);
                fake.WriteModuleBytes(Achievements, BitConverter.GetBytes((short)2));
                fake.WriteModuleBytes(Achievements + 0x60E, BitConverter.GetBytes((short)1));

                Coach coach = Read(fake).Coach;

                Assert.Equal((short)80, coach.RetirementAge);
                Assert.Equal(CoachEnums.CareerMode.Career, coach.CareerMode);
                Assert.Equal(1998, coach.CareerStartYear);
                Assert.Null(coach.CareerEndYear);
                Assert.Equal((short)2, coach.Achievements.Counters[0]);
                Assert.Equal((short)1, coach.Achievements.Counters[Array.IndexOf(CoachAchievements.Names, "Europameister")]);
            }
        }

        [Fact]
        public void CareerYears_FollowTheModeAndTheStart()
        {
            Coach coach = new Coach { CurrentSeason = 3, CurrentYear = 2000, CareerStartSeason = 1, CareerMode = CoachEnums.CareerMode.FiveYears };

            Assert.Equal(1998, coach.CareerStartYear);
            Assert.Equal(2002, coach.CareerEndYear);

            coach.CareerStartYear = 2001;
            Assert.Equal((short)4, coach.CareerStartSeason);
            Assert.Equal(2005, coach.CareerEndYear);

            coach.CareerMode = CoachEnums.CareerMode.Endless;
            Assert.Null(coach.CareerEndYear);
        }

        [Fact]
        public void Save_WritesTheCareerAndTheTitles()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedCareer(fake);
                CoachController controller = Read(fake);
                Coach coach = controller.Coach;
                coach.RetirementAge = 99;
                coach.CareerMode = CoachEnums.CareerMode.Endless;
                coach.CareerStartYear = 2000;
                coach.Achievements.Set(0, 5);
                coach.Achievements.Set(11, 0);

                controller.Save();

                Assert.Equal((short)99, BitConverter.ToInt16(CoachMemory.ReadRecord(fake, A[CoachEnums.AddressKey.RETIREMENT_AGE], 2), 0));
                Assert.Equal((short)10, BitConverter.ToInt16(CoachMemory.ReadRecord(fake, A[CoachEnums.AddressKey.CAREER_MODE], 2), 0));
                Assert.Equal((short)3, BitConverter.ToInt16(CoachMemory.ReadWorkingCopy(fake, A[CoachEnums.AddressKey.CAREER_START], 2), 0));
                Assert.Equal((short)5, BitConverter.ToInt16(fake.ReadModuleBytes(Achievements, 2), 0));
                // The station count and the station itself stay as they were.
                Assert.Equal((byte)1, fake.ReadModuleBytes(Achievements + 0x1A8, 1)[0]);
                Assert.Equal((byte)0x0E, fake.ReadModuleBytes(Achievements + 0x1AC, 1)[0]);
            }
        }

        [Fact]
        public void Save_PassingTheFortbildung_SetsTheResultAndTheFlag()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.TRAINING_FLAGS], new byte[] { 0x01 });
                CoachController controller = Read(fake);
                Assert.Equal(CoachEnums.TrainingResult.Failed, controller.Coach.TrainingResult);

                controller.Coach.TrainingResult = CoachEnums.TrainingResult.Perfect;
                controller.Save();

                Assert.Equal((byte)2, CoachMemory.ReadRecord(fake, A[CoachEnums.AddressKey.TRAINING_RESULT], 1)[0]);
                Assert.Equal((byte)0x81, CoachMemory.ReadRecord(fake, A[CoachEnums.AddressKey.TRAINING_FLAGS], 1)[0]);
                Assert.Equal((byte)0x81, CoachMemory.ReadWorkingCopy(fake, A[CoachEnums.AddressKey.TRAINING_FLAGS], 1)[0]);
            }
        }

        [Fact]
        public void Save_FailingTheFortbildung_ClearsTheFlag_AnUnchangedResultLeavesIt()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.TRAINING_RESULT], new byte[] { 1 });
                CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.TRAINING_FLAGS], new byte[] { 0x81 });
                CoachController controller = Read(fake);

                controller.Coach.TrainingResult = CoachEnums.TrainingResult.Failed;
                controller.Save();
                Assert.Equal((byte)0x01, CoachMemory.ReadWorkingCopy(fake, A[CoachEnums.AddressKey.TRAINING_FLAGS], 1)[0]);

                // Used up by the level evaluation (here: set by hand) and saved again unchanged: stays as it is.
                controller.Coach.LevelState.SetMark(0x80, true);
                controller.Save();
                Assert.Equal((byte)0x81, CoachMemory.ReadWorkingCopy(fake, A[CoachEnums.AddressKey.TRAINING_FLAGS], 1)[0]);
            }
        }

        [Fact]
        public void Achievements_Set_RaisesTheCounter()
        {
            CoachAchievements achievements = new CoachAchievements();
            List<string> raised = new List<string>();
            achievements.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            achievements.Set(1, 3);

            Assert.Equal((short)3, achievements.Counters[1]);
            Assert.Equal(new[] { CoachAchievements.Names[1] }, raised);
        }

        [Fact]
        public void LeagueController_ReadsAndWritesWithDismissals()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.CountrySelectionAddress[0]);
                string offset = AddressPresets.LEAGUE_SETTINGS[LeagueEnums.AddressKey.WITH_DISMISSALS];
                fake.WriteDisplayCacheBytes(offset, new byte[] { 1 });
                LeagueController controller = new LeagueController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Assert.True(controller.League.WithDismissals);

                controller.League.WithDismissals = false;
                controller.Save();

                Assert.Equal((byte)0, fake.ReadDisplayCacheBytes(offset, 1)[0]);
            }
        }

        private static T Field<T>(CoachView view, string name) =>
            (T)typeof(CoachView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        // Seeds the career for the "Karriere" tab check: locked difficulty.
        internal static void SeedCareerTab(FakeModule fake)
        {
            SeedCareer(fake);
            CoachMemory.WriteWorkingCopy(fake, A[CoachEnums.AddressKey.DIFFICULTY], new byte[] { 0x83 });
        }

        // The "Karriere" tab, checked inside CoachContractTests' window test (each Trainer window costs
        // the x86 test host ~20 MB); needs SeedCareerTab before the view's refresh.
        internal static void CheckCareerTab(CoachView view)
        {
            {
                Assert.Contains(Field<TabControl>(view, "ClubTabControl").TabPages.Cast<TabPage>(), t => t.Text == "Karriere");
                CheckBox unlock = Field<CheckBox>(view, "unlockDifficultyInput");
                Assert.Equal(nameof(Coach.UnlockDifficulty), unlock.DataBindings["Checked"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(LeagueSettings.WithDismissals), Field<CheckBox>(view, "withDismissalsInput").DataBindings["Checked"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.CareerMode), Field<ComboBox>(view, "careerModeInput").DataBindings["SelectedValue"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Coach.TrainingResult), Field<ComboBox>(view, "trainingResultInput").DataBindings["SelectedValue"].BindingMemberInfo.BindingField);

                TextBox start = Field<TextBox>(view, "careerStartInput");
                Assert.Equal("1998", start.Text);
                Assert.Equal("-", Field<Label>(view, "careerEndValue").Text);
                Field<ComboBox>(view, "careerModeInput").SelectedValue = CoachEnums.CareerMode.TenYears;
                start.Text = "1999";
                Assert.Equal((short)2, view.CoachController.Coach.CareerStartSeason);
                Assert.Equal("2008/09", Field<Label>(view, "careerEndValue").Text);

                TextBox retirement = Field<TextBox>(view, "retirementAgeInput");
                retirement.Text = "70";
                Assert.Equal((short)70, view.CoachController.Coach.RetirementAge);
                retirement.Text = "99999";
                Assert.Equal(short.MaxValue.ToString(), retirement.Text);

                TextBox[] counters = Field<TextBox[]>(view, "achievementInputs");
                counters[0].Text = "4";
                Assert.Equal((short)4, view.CoachController.Coach.Achievements.Counters[0]);

                // Without titles (no fixed addresses) the box is disabled.
                view.CoachController.Coach.Achievements = null;
                MethodInfo show = typeof(CoachView).GetMethod("ShowCareer", BindingFlags.NonPublic | BindingFlags.Instance);
                show.Invoke(view, null);
                Assert.False(Field<GroupBox>(view, "achievementsBox").Enabled);

                // "Levelwertung" tab and the flags box.
                Assert.Contains(Field<TabControl>(view, "ClubTabControl").TabPages.Cast<TabPage>(), t => t.Text == "Levelwertung");
                CoachLevelState state = view.CoachController.Coach.LevelState;
                var checks = Field<List<KeyValuePair<CheckBox, Func<CoachLevelState, bool>>>>(view, "levelChecks");
                CheckBox kassengriff = checks.Single(c => c.Key.Text == "Kassengriff diesen Monat").Key;
                kassengriff.Checked = true;
                Assert.True(state.HasFlag(0x8));
                checks.Single(c => c.Key.Text == "Turniersieg").Key.Checked = true;
                Assert.True(state.HasMark(0x200));
                checks.Single(c => c.Key.Text == "Schulden").Key.Checked = true;
                Assert.Equal((byte)0x08, state.DemotionReasons);
                typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(Field<Button>(view, "prepareLevelUpButton"), new object[] { EventArgs.Empty });
                Assert.Equal((short)24, state.WinStreak);
                Assert.False(checks.Single(c => c.Key.Text == "Schulden").Key.Checked);
                Assert.False(Field<CheckBox>(view, "stopSabotageInput").Enabled);
                Assert.Equal("0 Gespräche, 0 Ablehnungen", Field<Label>(view, "naturalisationValue").Text);
            }
        }
    }
}
