using A2G_Trainer.Controller;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for ProcessController.IsGameCandidate, the pre-filter before TryAttach. Under Wine
    /// Mono every window title is null and a game in another Wine desktop has no main window, so
    /// the game's process names must count on their own.
    /// </summary>
    public class ProcessCandidateTests
    {
        [Theory]
        [InlineData("anstoss2", "ANSTOSS 2 - Mannschaftsaufstellung")]
        [InlineData("anstoss2", null)]          // Wine Mono: no title
        [InlineData("anstoss2", "")]            // game in another desktop: no main window
        [InlineData("ANSTOSS2", null)]
        [InlineData("run", null)]               // GOG / 2007 CD release
        [InlineData("verl", "ANSTOSS 2 - Verlängerung")]  // title still counts; TryAttach rejects verl.exe
        public void AcceptsGameProcesses(string name, string title)
        {
            Assert.True(ProcessController.IsGameCandidate(name, title));
        }

        [Theory]
        [InlineData("Editor", "ANSTOSS 2 Dateneditor")]
        [InlineData("anstoss2", "ANSTOSS 2 Dateneditor")]
        [InlineData("explorer", null)]
        [InlineData("a2start", null)]
        [InlineData("A2G-Trainer", "A2G-Trainer")]
        [InlineData(null, null)]                // process exited while being queried
        public void RejectsOtherProcesses(string name, string title)
        {
            Assert.False(ProcessController.IsGameCandidate(name, title));
        }

        [Fact]
        public void SafeGet_GivesNull_ForAProcessThatCantBeQueried()
        {
            // A Process object without a process behind it throws, like one that exited meanwhile.
            Assert.Null(ProcessController.SafeGet(() => new System.Diagnostics.Process().ProcessName));
            Assert.Equal(System.Diagnostics.Process.GetCurrentProcess().ProcessName,
                ProcessController.SafeGet(() => System.Diagnostics.Process.GetCurrentProcess().ProcessName));
        }
    }
}
