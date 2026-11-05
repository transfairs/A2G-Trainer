using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for the shared code's remaining paths: ProcessMemory's string write and its refusal of
    /// a hung process, TrainerShell's keys without (or besides) the navigation arrows, and
    /// ViewScaler's handling of repeated or parentless control notifications.
    /// </summary>
    public class SharedCoverageTests
    {
        [Fact]
        public void WriteMemory_String_WritesTheEncodedTextThroughThePointer()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot("100");

                fake.Memory.WriteMemory("Module+100,20", "string", "Jörg", Encoding.GetEncoding(1252));

                Assert.Equal(new byte[] { 0x4A, 0xF6, 0x72, 0x67 }, fake.ReadDisplayCacheBytes("20", 4));
                Assert.Equal("Jörg", fake.Memory.ReadString("Module+100,20", 8, Encoding.GetEncoding(1252)));
            }
        }

        [Fact]
        public void OpenProcess_OnAProcessWhoseWindowHangs_RefusesIt()
        {
            // A child whose form is shown but never pumped: Thread.Sleep (unlike Start-Sleep's wait, which
            // pumps sent messages on an STA thread) leaves its window not answering messages.
            ProcessStartInfo start = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -Command \"Add-Type -AssemblyName System.Windows.Forms; " +
                "$f = New-Object System.Windows.Forms.Form; $f.StartPosition = 'Manual'; " +
                "$f.Location = New-Object System.Drawing.Point(-32000, -32000); $f.Show(); [System.Threading.Thread]::Sleep(60000)\"")
            {
                CreateNoWindow = true,
                UseShellExecute = false,
            };
            using (Process hung = Process.Start(start))
            {
                try
                {
                    Stopwatch waited = Stopwatch.StartNew();
                    while (waited.Elapsed < TimeSpan.FromSeconds(30))
                    {
                        hung.Refresh();
                        if (hung.MainWindowHandle != IntPtr.Zero)
                            break;
                        System.Threading.Thread.Sleep(100);
                    }
                    Assert.NotEqual(IntPtr.Zero, hung.MainWindowHandle);

                    ProcessMemory memory = new ProcessMemory();

                    Assert.False(memory.OpenProcess(hung.Id));
                    Assert.Null(memory.mProc.Process);
                    Assert.Equal(IntPtr.Zero, memory.mProc.Handle);
                }
                finally
                {
                    if (!hung.HasExited)
                        hung.Kill();
                }
            }
        }

        private sealed class KeyShell : TrainerShell
        {
            internal readonly Panel Host = new Panel();
            internal readonly MenuStrip Strip = new MenuStrip();

            internal KeyShell(bool arrows)
            {
                this.Text = "Shell";
                this.Controls.Add(this.Host);
                this.Controls.Add(this.Strip);
                this.InitShell(this.Host, () => { });
                if (arrows)
                    this.AddNavigationButtons(this.Strip);
            }

            internal void Go(UserControl view, string title) => this.Navigate(title, () => this.ShowScreen(view, title));

            internal bool Key(Keys keys)
            {
                Message msg = new Message();
                return (bool)typeof(TrainerShell).GetMethod("ProcessCmdKey", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(this, new object[] { msg, keys });
            }
        }

        [Fact]
        public void ProcessCmdKey_WithoutArrows_LeavesAltLeftAndRightToTheWindow() => StaThread.Run(() =>
        {
            using (KeyShell shell = new KeyShell(arrows: false))
            using (UserControl first = new UserControl())
            using (UserControl second = new UserControl())
            {
                shell.Go(first, "Erste");
                shell.Go(second, "Zweite");
                Assert.True(shell.CanGoBack);

                Assert.False(shell.Key(Keys.Alt | Keys.Left));
                Assert.Equal("Shell :: Zweite", shell.Text);
                Assert.False(shell.Key(Keys.Alt | Keys.Right));
                Assert.Equal("Shell :: Zweite", shell.Text);

                // The history itself still works.
                Assert.True(shell.GoBack());
                Assert.Equal("Shell :: Erste", shell.Text);
            }
        });

        [Fact]
        public void ProcessCmdKey_WithArrows_HandlesOnlyAltLeftAndRight() => StaThread.Run(() =>
        {
            using (KeyShell shell = new KeyShell(arrows: true))
            using (UserControl first = new UserControl())
            using (UserControl second = new UserControl())
            {
                shell.Go(first, "Erste");
                shell.Go(second, "Zweite");

                Assert.False(shell.Key(Keys.Alt | Keys.Up));
                Assert.False(shell.Key(Keys.Left));
                Assert.Equal("Shell :: Zweite", shell.Text);

                Assert.True(shell.Key(Keys.Alt | Keys.Left));
                Assert.Equal("Shell :: Erste", shell.Text);
                Assert.True(shell.Key(Keys.Alt | Keys.Right));
                Assert.Equal("Shell :: Zweite", shell.Text);
            }
        });

        // A panel that can repeat the ControlAdded notification, as a re-entrant layout might.
        private sealed class NotifyingPanel : Panel
        {
            internal void Announce(Control control) => this.OnControlAdded(new ControlEventArgs(control));
        }

        [Fact]
        public void Track_ARepeatedNotification_KeepsTheDesignBounds() => StaThread.Run(() =>
        {
            using (UserControl root = new UserControl { Size = new Size(200, 100), Font = new Font("Microsoft Sans Serif", 8f) })
            {
                NotifyingPanel panel = new NotifyingPanel { Bounds = new Rectangle(0, 0, 100, 50) };
                Button button = new Button { Bounds = new Rectangle(10, 10, 40, 20) };
                panel.Controls.Add(button);
                root.Controls.Add(panel);

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(400, 200));
                    Assert.Equal(new Rectangle(20, 20, 80, 40), button.Bounds);

                    // Announced again while scaled: still measured from its design, not from now.
                    panel.Announce(button);
                    Assert.Equal(new Rectangle(20, 20, 80, 40), button.Bounds);

                    scaler.ScaleTo(new Size(200, 100));
                    Assert.Equal(new Rectangle(10, 10, 40, 20), button.Bounds);
                }
            }
        });

        [Fact]
        public void Track_AControlWithoutParent_ScalesItsBoundsButLeavesItsFont() => StaThread.Run(() =>
        {
            using (UserControl root = new UserControl { Size = new Size(200, 100), Font = new Font("Microsoft Sans Serif", 8f) })
            using (Font own = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold))
            using (Label loose = new Label { Bounds = new Rectangle(10, 10, 40, 20), Font = own })
            {
                NotifyingPanel panel = new NotifyingPanel { Bounds = new Rectangle(0, 0, 100, 50) };
                root.Controls.Add(panel);

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(400, 200));

                    panel.Announce(loose);

                    Assert.Null(loose.Parent);
                    Assert.Equal(new Rectangle(20, 20, 80, 40), loose.Bounds);
                    Assert.Same(own, loose.Font);
                }
            }
        });
    }
}
