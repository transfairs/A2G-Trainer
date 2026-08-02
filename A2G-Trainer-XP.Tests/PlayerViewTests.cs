using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer_XP;
using A2G_Trainer_XP.Model;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for PlayerView's edit-enablement rules across roster contexts (incl. TRAINEE).</summary>
    public class PlayerViewTests
    {
        private static Control Find(Control root, string name) => root.Controls.Find(name, searchAllChildren: true).Single();

        private static MethodInfo DisableEditsMethod => typeof(PlayerView).GetMethod("DisableEdits", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Controller.ProcessMemory AttachTrainer(Trainer trainer)
        {
            trainer.Memory.OpenProcess(Process.GetCurrentProcess().Id);
            return trainer.Memory;
        }

        [Fact]
        public void RefreshPlayerListView_ForTrainee_WithNoPlayersLoaded_DoesNotThrow()
        {
            StaThread.Run(() =>
            {
                using (Trainer trainer = new Trainer())
                using (FakeModule fake = new FakeModule(memory: AttachTrainer(trainer)))
                {
                    Exception thrown = Record.Exception(() => trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.TRAINEE));

                    Assert.Null(thrown);
                    Assert.Empty(trainer.PlayerView.PlayerController.EntityList);
                    Assert.Equal(string.Empty, trainer.PlayerView.ClubController.Club.ClubName);
                }
            });
        }

        [Fact]
        public void DisableEdits_ForTrainee_EnablesSaveButton_ButKeepsUnconfirmedContractFieldsDisabled()
        {
            StaThread.Run(() =>
            {
                using (Trainer trainer = new Trainer())
                using (FakeModule fake = new FakeModule(memory: AttachTrainer(trainer)))
                {
                    trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.TRAINEE);
                    DisableEditsMethod.Invoke(trainer.PlayerView, new object[] { true });

                    Assert.True(Find(trainer.PlayerView, "SaveBtn").Enabled);
                    Assert.False(Find(trainer.PlayerView, "OtherTab").Enabled);
                    Assert.False(Find(trainer.PlayerView, "GoalBonusInput").Enabled);
                    Assert.False(Find(trainer.PlayerView, "ContractDurationInput").Enabled);
                    Assert.False(Find(trainer.PlayerView, "Retires").Enabled);
                    Assert.True(Find(trainer.PlayerView, "LevelInput").Enabled);
                }
            });
        }

        [Fact]
        public void DisableEdits_ForOwnType_DoesNotForceOffTraineeOnlyRestrictions()
        {
            StaThread.Run(() =>
            {
                using (Trainer trainer = new Trainer())
                using (FakeModule fake = new FakeModule(memory: AttachTrainer(trainer)))
                {
                    trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);
                    DisableEditsMethod.Invoke(trainer.PlayerView, new object[] { true });

                    Assert.True(Find(trainer.PlayerView, "SaveBtn").Enabled);
                    Assert.True(Find(trainer.PlayerView, "OtherTab").Enabled);
                    Assert.True(Find(trainer.PlayerView, "GoalBonusInput").Enabled);
                }
            });
        }

        // No Designer-built tab currently has an actual VScrollBar/HScrollBar as a direct child
        // control (WinForms' own AutoScroll bars aren't exposed through Controls), so DisableEdits'
        // skip-scrollbars guard is otherwise never exercised - inject one directly to hit it.
        [Fact]
        public void DisableEdits_SkipsScrollBarControls_LeavingTheirEnabledStateUntouched()
        {
            StaThread.Run(() =>
            {
                using (Trainer trainer = new Trainer())
                using (FakeModule fake = new FakeModule(memory: AttachTrainer(trainer)))
                {
                    trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);
                    TabPage personalTab = (TabPage)Find(trainer.PlayerView, "PersonalTab");
                    VScrollBar scrollBar = new VScrollBar { Name = "InjectedScrollBar", Enabled = false };
                    personalTab.Controls.Add(scrollBar);

                    DisableEditsMethod.Invoke(trainer.PlayerView, new object[] { true });

                    Assert.False(scrollBar.Enabled);
                }
            });
        }
    }
}
