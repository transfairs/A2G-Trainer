using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for Trainer's private menu-click handlers and ShowScreen's title branches.</summary>
    public class TrainerAdditionalTests
    {
        private static void InvokeHandler(Trainer trainer, string methodName, object sender) =>
            typeof(Trainer).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(trainer, new[] { sender, EventArgs.Empty });

        // Every handler below except Help/About ends up calling a view's RefreshValues/
        // RefreshPlayerListView, which starts with a non-silent EntityView.IsGameRunning() check -
        // on unattached memory that pops a real modal MessageBox and hangs the test run waiting for
        // a user who isn't there. A real (self-attach) OpenProcess is enough to satisfy that check
        // without needing a FakeModule - see the many other self-attach-only tests doing the same.
        private static void AttachSelf(Trainer trainer) => trainer.Memory.OpenProcess(Process.GetCurrentProcess().Id);

        [Theory]
        [InlineData("OwnClubToolStripMenuItem_Click")]
        [InlineData("AllClubsToolStripMenuItem_Click")]
        [InlineData("OwnPlayersToolStripMenuItem_Click")]
        [InlineData("OpponentToolStripMenuItem_Click")]
        [InlineData("TraineeToolStripMenuItem_Click")]
        [InlineData("DynamicTeamToolStripMenuItem_Click")]
        [InlineData("HelpToolStripMenuItem_Click")]
        [InlineData("AboutToolStripMenuItem_Click")]
        public void MenuClickHandler_DoesNotThrow(string methodName) => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                AttachSelf(trainer);

                Exception thrown = Record.Exception(() => InvokeHandler(trainer, methodName, null));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void TrainerToolStripMenuItem_Click_SwitchesToTheGivenTrainerSlot() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                AttachSelf(trainer);
                ToolStripMenuItem item = new ToolStripMenuItem("Robin Trauer") { Tag = 2 };

                Exception thrown = Record.Exception(() => InvokeHandler(trainer, "TrainerToolStripMenuItem_Click", item));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void ShowScreen_WithNoTitle_LeavesWindowTitleAsTheBaseTitle() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                string baseTitle = trainer.Text;

                typeof(Trainer).GetMethod("ShowScreen", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(trainer, new object[] { trainer.ClubView, null });

                Assert.Equal(baseTitle, trainer.Text);
            }
        });

        [Fact]
        public void ShowScreen_CalledTwiceWithTheSameControl_SkipsSwappingItAgain() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                MethodInfo showScreen = typeof(Trainer).GetMethod("ShowScreen", BindingFlags.NonPublic | BindingFlags.Instance);

                showScreen.Invoke(trainer, new object[] { trainer.ClubView, "Erster Aufruf" });
                Exception thrown = Record.Exception(() => showScreen.Invoke(trainer, new object[] { trainer.ClubView, "Zweiter Aufruf" }));

                Assert.Null(thrown);
                Assert.EndsWith("Zweiter Aufruf", trainer.Text);
            }
        });

        [Fact]
        public void RefreshTrainerMenu_WithBlankAndNamedTrainers_BuildsBothLabelStyles() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                List<KeyValuePair<int, Coach>> trainers = new List<KeyValuePair<int, Coach>>
                {
                    new KeyValuePair<int, Coach>(0, new Coach { Firstname = "", Lastname = "" }),
                    new KeyValuePair<int, Coach>(1, new Coach { Firstname = "Robin", Lastname = "Trauer" }),
                };

                trainer.RefreshTrainerMenu(trainers);

                FieldInfo field = typeof(Trainer).GetField("trainerToolStripMenuItem", BindingFlags.NonPublic | BindingFlags.Instance);
                ToolStripMenuItem menu = (ToolStripMenuItem)field.GetValue(trainer);

                Assert.Equal("Trainer 1", menu.DropDownItems[0].Text);
                Assert.Equal("Robin Trauer", menu.DropDownItems[1].Text);
            }
        });
    }
}
