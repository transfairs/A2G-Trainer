using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for AboutDialog: version label, links and their targets, without opening a browser.</summary>
    public class AboutDialogTests
    {
        // Runs body with Open/ShowError recording instead of opening anything.
        private static void WithRecordedOpen(Action<List<string>, List<string>> body)
        {
            Action<string> open = AboutDialog.Open;
            Action<IWin32Window, string, string> showError = AboutDialog.ShowError;
            List<string> opened = new List<string>(), errors = new List<string>();
            AboutDialog.Open = opened.Add;
            AboutDialog.ShowError = (owner, text, caption) => errors.Add(text);
            try
            {
                body(opened, errors);
            }
            finally
            {
                AboutDialog.Open = open;
                AboutDialog.ShowError = showError;
            }
        }

        private static IEnumerable<LinkLabel> Links(Form dialog) => dialog.Controls.OfType<LinkLabel>();

        // The label must reflect the assembly's AssemblyInformationalVersion (the value the release
        // pipeline stamps into Properties/VersionInfo.cs from the git tag), not a hardcoded string.
        [Fact]
        public void VersionLabel_ShowsTheAssemblyInformationalVersion() => StaThread.Run(() =>
        {
            string expected = typeof(AboutDialog).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

            using (AboutDialog dialog = new AboutDialog())
            {
                Assert.Equal($"v{expected}", dialog.Controls.Find("VersionLabel", false).Single().Text);
                Assert.Single(dialog.Controls.Find("Logo", false));
                Assert.Same(dialog.AcceptButton, dialog.CancelButton);
            }
        });

        [Fact]
        public void Links_CoverExactlyTheirWords_AndPointToTheirTargets() => StaThread.Run(() =>
        {
            using (AboutDialog dialog = new AboutDialog())
            {
                Dictionary<string, string> links = Links(dialog)
                    .SelectMany(l => l.Links.Cast<LinkLabel.Link>().Select(k => new { Word = l.Text.Substring(k.Start, k.Length), Target = (string)k.LinkData }))
                    .ToDictionary(k => k.Word, k => k.Target);

                Assert.Equal(AboutDialog.GithubUrl, links[AboutDialog.GithubUrl]);
                Assert.Equal(AboutDialog.ForumUrl, links["anstoss-juenger.de"]);
                Assert.Equal(AboutDialog.StrajkUrl, links["strajk-"]);
                Assert.Equal(AboutDialog.TwentyTwoUrl, links["22"]);
                Assert.Equal(AboutDialog.LogFolder, links[AboutDialog.LogFolder]);
                Assert.Equal(5, links.Count);
            }
        });

        [Fact]
        public void LogFolder_IsTheLoggersFolderUnderLocalAppData()
        {
            Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "A2G-Trainer"), AboutDialog.LogFolder);
        }

        [Fact]
        public void ClickingALink_OpensItsTarget() => StaThread.Run(() => WithRecordedOpen((opened, errors) =>
        {
            using (AboutDialog dialog = new AboutDialog())
            {
                LinkLabel github = Links(dialog).First(l => l.Text.StartsWith("GitHub"));
                typeof(LinkLabel).GetMethod("OnLinkClicked", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(github, new object[] { new LinkLabelLinkClickedEventArgs(github.Links[0]) });

                Assert.Equal(new[] { AboutDialog.GithubUrl }, opened);
                Assert.Empty(errors);
            }
        }));

        [Fact]
        public void OpenLink_CreatesTheLogFolder_AndIgnoresAnEmptyTarget() => StaThread.Run(() => WithRecordedOpen((opened, errors) =>
        {
            using (AboutDialog dialog = new AboutDialog())
            {
                dialog.OpenLink(null);
                dialog.OpenLink("");
                dialog.OpenLink(AboutDialog.LogFolder);

                Assert.Equal(new[] { AboutDialog.LogFolder }, opened);
                Assert.True(Directory.Exists(AboutDialog.LogFolder));
            }
        }));

        [Fact]
        public void OpenLink_ReportsATargetThatCantBeOpened() => StaThread.Run(() => WithRecordedOpen((opened, errors) =>
        {
            AboutDialog.Open = target => throw new InvalidOperationException("kaputt");
            using (AboutDialog dialog = new AboutDialog())
            {
                dialog.OpenLink(AboutDialog.GithubUrl);

                Assert.Equal(new[] { "kaputt" }, errors);
            }
        }));

        // The real Open: a nonexistent local file fails fast with a Win32Exception instead of
        // opening an application (a bogus URL scheme is silently swallowed by the shell).
        [Fact]
        public void Open_Real_WithNonexistentTarget_Throws()
        {
            Assert.ThrowsAny<Exception>(() => AboutDialog.Open("a2g-trainer-test-nonexistent-target.invalid"));
        }

        [Fact]
        public void Dispose_WithDisposingFalse_KeepsTheTooltip() => StaThread.Run(() =>
        {
            using (AboutDialog dialog = new AboutDialog())
            {
                Exception thrown = Record.Exception(() => typeof(AboutDialog)
                    .GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(bool) }, null)
                    .Invoke(dialog, new object[] { false }));

                Assert.Null(thrown);
            }
        });
    }
}
