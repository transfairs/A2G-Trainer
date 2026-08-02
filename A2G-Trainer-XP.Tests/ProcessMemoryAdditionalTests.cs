using System.Text;
using A2G_Trainer_XP.Controller;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for ProcessMemory's absolute-address helpers and OpenProcess's access-denied path.</summary>
    public class ProcessMemoryAdditionalTests
    {
        // OpenProcess's "!process.Responding" branch was attempted here via a real window on an STA
        // thread that's deliberately never pumped (no Application.Run/DoEvents) - the theory being
        // that Windows' hang detection would eventually flag it. In practice, even after an 8-second
        // wait, Process.Responding still read true: .NET's Responding getter goes through
        // IsHungAppWindow, which Windows only sets once its own ghosting/input-stall heuristics
        // trigger - not from a window merely sitting unpumped with no input ever sent to it. Making
        // that fire deterministically would need to actually inject input at the window and is not
        // attempted here. See the final report.
        [Fact]
        public void WriteBytes_WithoutOpenProcess_ReturnsWithoutThrowing()
        {
            ProcessMemory memory = new ProcessMemory();

            System.Exception thrown = Record.Exception(() => memory.WriteBytes("Module+0,0", new byte[] { 1 }));

            Assert.Null(thrown);
        }

        [Fact]
        public void ReadBytesAtAddress_BelowMinValidAddress_ReturnsNull()
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.Null(memory.ReadBytesAtAddress(0, 4));
        }

        [Fact]
        public void WriteBytesAtAddress_BelowMinValidAddress_ReturnsWithoutThrowing()
        {
            ProcessMemory memory = new ProcessMemory();

            System.Exception thrown = Record.Exception(() => memory.WriteBytesAtAddress(0, new byte[] { 1 }));

            Assert.Null(thrown);
        }

        [Fact]
        public void ReadStringAtAddress_BelowMinValidAddress_ReturnsEmptyString()
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.Equal(string.Empty, memory.ReadStringAtAddress(0, 4, Encoding.ASCII));
        }

        [Fact]
        public void ReadBytes_AddressWithEmptyHexToken_TreatsItAsZero()
        {
            ProcessMemory memory = new ProcessMemory();

            // "Module+,0" has an empty token between '+' and ',' - ParseHex must treat that as 0
            // rather than throwing, same as a genuinely-empty offset elsewhere in the address chain.
            System.Exception thrown = Record.Exception(() => memory.ReadBytes("Module+,0", 1));

            Assert.Null(thrown);
        }

        [Fact]
        public void OpenProcess_ProtectedSystemProcess_FailsToOpenAndReturnsFalse()
        {
            ProcessMemory memory = new ProcessMemory();

            // PID 4 is always the "System" process on Windows - PROCESS_ALL_ACCESS against it is
            // denied for a non-elevated caller, exercising the OpenProcessNative-failure path
            // distinctly from the "process doesn't exist" one covered elsewhere.
            bool opened = memory.OpenProcess(4);

            Assert.False(opened);
        }
    }
}
