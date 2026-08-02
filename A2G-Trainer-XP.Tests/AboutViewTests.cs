using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    // Overrides the real Process.Start call (StartProcess) so LinkLabel_LinkClicked's own
    // logic - the null/empty-url guard and the actual delegation to StartProcess - is still
    // exercised for real, without popping open a real browser.
    internal class TestableAboutView : AboutView
    {
        public string LastStartedUrl { get; private set; }
        internal override void StartProcess(string url) => this.LastStartedUrl = url;
    }

    /// <summary>Tests for AboutView's container constructor and link-click handlers.</summary>
    public class AboutViewTests
    {
        // Private members aren't visible via reflection on a derived type even with NonPublic set -
        // GetMethod has to walk up to the declaring type (AboutView here) explicitly.
        private static object InvokePrivate(object target, string methodName, params object[] args) =>
            typeof(AboutView).GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        [Fact]
        public void ContainerConstructor_AddsItselfToTheContainer() => StaThread.Run(() =>
        {
            using (Container container = new Container())
            using (AboutView view = new AboutView(container))
            {
                Assert.Contains(view, container.Components.Cast<IComponent>());
            }
        });

        private static readonly LinkLabelLinkClickedEventArgs NonStringLinkArgs =
            new LinkLabelLinkClickedEventArgs(new LinkLabel.Link { LinkData = 123 });

        [Theory]
        [InlineData("GithubLinkLabel_LinkClicked")]
        [InlineData("AnstossJuengerLinkLabel_LinkClicked")]
        [InlineData("StrajkLinkLabel_LinkClicked")]
        [InlineData("LinkLabel_LinkClicked")]
        public void LinkClickedHandler_WithNonStringLinkData_DoesNothing(string methodName) => StaThread.Run(() =>
        {
            using (AboutView view = new AboutView())
            {
                System.Exception thrown = Record.Exception(() => InvokePrivate(view, methodName, null, NonStringLinkArgs));

                Assert.Null(thrown);
            }
        });

        [Theory]
        [InlineData("GithubLinkLabel_LinkClicked")]
        [InlineData("AnstossJuengerLinkLabel_LinkClicked")]
        [InlineData("StrajkLinkLabel_LinkClicked")]
        public void LinkClickedHandler_WithAUrl_StartsThatUrl(string methodName) => StaThread.Run(() =>
        {
            using (TestableAboutView view = new TestableAboutView())
            {
                LinkLabelLinkClickedEventArgs e = new LinkLabelLinkClickedEventArgs(new LinkLabel.Link { LinkData = "https://example.invalid" });

                InvokePrivate(view, methodName, null, e);

                Assert.Equal("https://example.invalid", view.LastStartedUrl);
            }
        });

        // Exercises the real (non-overridden) StartProcess body. A bogus URL scheme turned out to
        // be silently swallowed by Windows' shell (no exception, no visible app) rather than
        // reliably throwing - a nonexistent local file path is what actually and deterministically
        // fails fast with a Win32Exception, so that's used here instead to safely cover the real
        // Process.Start call without risking a real application (e.g. a browser) opening.
        [Fact]
        public void StartProcess_Real_WithNonexistentTarget_ThrowsWithoutOpeningAnApplication() => StaThread.Run(() =>
        {
            using (AboutView view = new AboutView())
            {
                System.Exception thrown = Record.Exception(() => view.StartProcess("a2g-trainer-xp-test-nonexistent-target.invalid"));

                Assert.NotNull(thrown);
            }
        });
    }
}
