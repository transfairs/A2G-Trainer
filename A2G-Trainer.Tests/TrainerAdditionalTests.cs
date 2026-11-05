using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
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
        [InlineData("OtherClubDataToolStripMenuItem_Click")]
        [InlineData("OwnPlayersToolStripMenuItem_Click")]
        [InlineData("OpponentToolStripMenuItem_Click")]
        [InlineData("TraineeToolStripMenuItem_Click")]
        [InlineData("OtherClubToolStripMenuItem_Click")]
        [InlineData("HelpToolStripMenuItem_Click")]
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
        public void SaveGameMenu_IsHiddenAndHasNoShortcuts() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                FieldInfo Field(string name) => typeof(Trainer).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                ToolStripMenuItem menu = (ToolStripMenuItem)Field("spielstandToolStripMenuItem").GetValue(trainer);
                Assert.False(menu.Available);
                Assert.Equal(Keys.None, ((ToolStripMenuItem)Field("spielstandOeffnenToolStripMenuItem").GetValue(trainer)).ShortcutKeys);
                Assert.Equal(Keys.None, ((ToolStripMenuItem)Field("spielstandSpeichernToolStripMenuItem").GetValue(trainer)).ShortcutKeys);
            }
        });

        [Fact]
        public void AboutToolStripMenuItem_Click_ShowsTheAboutDialog() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                Form shown = null;
                trainer.ShowDialogBox = form => { shown = form; return DialogResult.OK; };

                InvokeHandler(trainer, "AboutToolStripMenuItem_Click", null);

                Assert.IsType<A2G_Trainer.View.AboutDialog>(shown);
                Assert.True(shown.IsDisposed);
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
                Assert.EndsWith("Robin Trauer", trainer.Text);

                // Back/forward history (same window: each one costs the x86 test host ~20 MB).
                MenuStrip menu = (MenuStrip)typeof(Trainer).GetField("menuStrip", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer);
                ToolStripItem back = menu.Items["backToolStripMenuItem"];
                ToolStripItem forward = menu.Items["forwardToolStripMenuItem"];
                Assert.Equal(0, menu.Items.IndexOf(back));
                Assert.Equal(1, menu.Items.IndexOf(forward));
                Assert.True(back.Enabled);
                Assert.False(forward.Enabled);

                InvokeHandler(trainer, "HelpToolStripMenuItem_Click", null);
                InvokeHandler(trainer, "HelpToolStripMenuItem_Click", null);
                Assert.True(trainer.GoBack());
                Assert.EndsWith("Robin Trauer", trainer.Text);
                Assert.True(trainer.GoBack());
                Assert.EndsWith("Eigene Mannschaft", trainer.Text);
                Assert.False(trainer.GoBack());
                Assert.False(back.Enabled);
                Assert.True(forward.Enabled);

                bool ProcessKey(Keys keys)
                {
                    Message msg = new Message();
                    return (bool)typeof(Form).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)
                        .Invoke(trainer, new object[] { msg, keys });
                }
                Assert.True(ProcessKey(Keys.Alt | Keys.Right));
                Assert.EndsWith("Robin Trauer", trainer.Text);
                Assert.True(ProcessKey(Keys.Alt | Keys.Right));
                Assert.EndsWith("Hilfe", trainer.Text);
                Assert.False(trainer.GoForward());
                Assert.True(ProcessKey(Keys.Alt | Keys.Left));
                Assert.EndsWith("Robin Trauer", trainer.Text);

                // A club picked inside a view after going back drops what lay ahead and comes back with its club.
                ClubSquads.Entry club = new ClubSquads.Entry { Name = "Testverein" };
                typeof(Trainer).GetMethod("PlayerView_ClubChosen", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(trainer, new object[] { club, PlayerEnums.AddressType.CLUB });
                Assert.EndsWith("Verein: Testverein", trainer.Text);
                Assert.False(trainer.CanGoForward);
                Assert.True(trainer.GoBack());
                Assert.True(trainer.GoForward());
                Assert.EndsWith("Verein: Testverein", trainer.Text);
                Assert.Same(club, trainer.PlayerView.ChosenClub);
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

                // Highscores (same window: each one costs the x86 test host ~20 MB).
                string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "a2g-highscores-" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(folder);
                try
                {
                    trainer.ShowMessage = (text, caption, buttons, icon) => DialogResult.OK;
                    Assert.True(trainer.OpenHighscores(folder));
                    Assert.EndsWith("Highscores", trainer.Text);
                    Assert.Same(folder, trainer.HighscoreView.Files.Folder);
                    Assert.False(trainer.OpenHighscores("C:\\gibt\0es\\nicht"));
                }
                finally
                {
                    System.IO.Directory.Delete(folder, true);
                }
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
        public void Window_IsResizable() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                Assert.Equal(FormBorderStyle.Sizable, trainer.FormBorderStyle);

                // Savegame commands without a savegame (same window: each one costs the x86 test host ~20 MB).
                string title = trainer.Text;
                trainer.ShowMessage = (text, caption, buttons, icon) => throw new InvalidOperationException("asked");
                InvokeHandler(trainer, "SaveSaveGameAsToolStripMenuItem_Click", null);
                typeof(Trainer).GetMethod("ShowSaveGameScreen", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trainer, null);
                Assert.Equal(title, trainer.Text);
                FormClosingEventArgs closing = new FormClosingEventArgs(CloseReason.UserClosing, false);
                typeof(Trainer).GetMethod("Trainer_FormClosing", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trainer, new object[] { trainer, closing });
                Assert.False(closing.Cancel);
                // Already cancelled by someone else: stays cancelled without asking.
                closing = new FormClosingEventArgs(CloseReason.UserClosing, true);
                typeof(Trainer).GetMethod("Trainer_FormClosing", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trainer, new object[] { trainer, closing });
                Assert.True(closing.Cancel);

                // The game folder comes from the game's exe; a process that can't tell gives none.
                MethodInfo saveGameFolder = typeof(Trainer).GetMethod("SaveGameFolder", BindingFlags.NonPublic | BindingFlags.Instance);
                trainer.Anstoss.Process = new Process();
                Assert.Null(saveGameFolder.Invoke(trainer, null));

                // Highscores open straight from the running game's folder.
                string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "a2g-gamefolder-" + Guid.NewGuid().ToString("N"));
                System.IO.Directory.CreateDirectory(System.IO.Path.Combine(folder, "texte"));
                string exe = System.IO.Path.Combine(folder, "ping.exe");
                System.IO.File.Copy(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.SystemX86), "PING.EXE"), exe);
                using (Process game = Process.Start(new ProcessStartInfo(exe, "-n 60 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false }))
                {
                    try
                    {
                        trainer.Anstoss.Process = game;
                        Assert.Equal(folder, saveGameFolder.Invoke(trainer, null));

                        InvokeHandler(trainer, "HighscoresToolStripMenuItem_Click", null);

                        Assert.EndsWith("Highscores", trainer.Text);
                        Assert.Equal(folder, trainer.HighscoreView.Files.Folder);
                    }
                    finally
                    {
                        trainer.Anstoss.Process = null;
                        game.Kill();
                        game.WaitForExit();
                        System.IO.Directory.Delete(folder, true);
                    }
                }
            }
        });

        [Fact]
        public void EnableScaling_PinsTheMinimumSizeAndZoomsEveryViewWithTheWindow() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                MethodInfo enableScaling = typeof(Trainer).GetMethod("EnableScaling", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo showScreen = typeof(Trainer).GetMethod("ShowScreen", BindingFlags.NonPublic | BindingFlags.Instance);
                Panel content = (Panel)typeof(Trainer).GetField("ContentPanel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(trainer);
                Size designSize = trainer.Size;

                enableScaling.Invoke(trainer, null);
                trainer.Size = new Size(designSize.Width * 2, designSize.Height * 2);

                Assert.Equal(designSize, trainer.MinimumSize);
                Assert.Equal(content.ClientSize, trainer.PlayerView.Size);

                showScreen.Invoke(trainer, new object[] { trainer.HelpView, "Hilfe" });

                Assert.Equal(content.ClientSize, trainer.HelpView.Size);
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
