using System.Linq;
using System.Windows.Forms;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for the parts of the shared window and view bases (Anstoss-Trainer-Shared) the A2G
    /// trainer itself doesn't use: the placeholder screen and the defaults of TrainerView.
    /// </summary>
    public class SharedBaseTests
    {
        private sealed class TestShell : TrainerShell
        {
            internal readonly Panel Host = new Panel();

            internal TestShell()
            {
                this.Text = "Shell";
                this.Controls.Add(this.Host);
                this.InitShell(this.Host, () => { });
            }

            internal void Placeholder(Control placeholder, string title = null) => this.ShowPlaceholder(placeholder, title);
            internal void Screen(UserControl view, string title = null) => this.ShowScreen(view, title);
        }

        private sealed class TestView : TrainerView
        {
            internal string Game => this.GameDisplayName;
        }

        [Fact]
        public void ShowPlaceholder_ReplacesTheViewAndTitle_AndTheNextScreenComesBack() => StaThread.Run(() =>
        {
            using (TestShell shell = new TestShell())
            using (UserControl view = new UserControl())
            using (Label waiting = new Label())
            {
                shell.Screen(view, "Kader");
                Assert.Equal("Shell :: Kader", shell.Text);

                shell.Placeholder(waiting, "Warte");
                Assert.Same(waiting, Assert.Single(shell.Host.Controls.Cast<Control>()));
                Assert.Equal("Shell :: Warte", shell.Text);

                shell.Placeholder(waiting);
                Assert.Equal("Shell", shell.Text);

                // The placeholder cleared the current view, so showing the same view again puts it back.
                shell.Screen(view, "Kader");
                Assert.Same(view, Assert.Single(shell.Host.Controls.Cast<Control>()));
            }
        });

        [Fact]
        public void TrainerView_WithoutMemory_NamesTheGameGenerically() => StaThread.Run(() =>
        {
            using (TestView view = new TestView())
            {
                Assert.Equal("Anstoss", view.Game);
            }
        });
    }
}
