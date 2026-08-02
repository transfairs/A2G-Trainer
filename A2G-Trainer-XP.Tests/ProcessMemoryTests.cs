using System;
using System.Diagnostics;
using System.Text;
using A2G_Trainer_XP.Controller;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for ProcessMemory's address parsing and null-safe read fallbacks.</summary>
    public class ProcessMemoryTests
    {
        [Theory]
        [InlineData("NoDelimiters")]
        [InlineData("Module+OnlyPlus")]
        [InlineData("Module,OnlyComma")]
        [InlineData("Module,178+1")] // comma before plus
        public void ReadBytes_MalformedAddress_ThrowsFormatException(string address)
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.Throws<FormatException>(() => memory.ReadBytes(address, 1));
        }

        [Fact]
        public void ReadBytes_WellFormedAddress_WithoutOpenProcess_ReturnsNull()
        {
            // No process attached -> module base is zero and the handle is invalid, so the
            // resolved address collapses to below MinValidAddress and the read is skipped.
            ProcessMemory memory = new ProcessMemory();

            Assert.Null(memory.ReadBytes("Module+0,0", 4));
        }

        [Fact]
        public void ReadByte_WellFormedAddress_WithoutOpenProcess_ReturnsZero()
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.Equal(0, memory.ReadByte("Module+0,0"));
        }

        [Fact]
        public void ReadInt32_WellFormedAddress_WithoutOpenProcess_ReturnsZero()
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.Equal(0, memory.ReadInt32("Module+0,0"));
        }

        [Fact]
        public void ReadUInt16_WellFormedAddress_WithoutOpenProcess_ReturnsZero()
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.Equal((ushort)0, memory.ReadUInt16("Module+0,0"));
        }

        [Theory]
        [InlineData("NoDelimiters")]
        [InlineData("Module+OnlyPlus")]
        [InlineData("Module,OnlyComma")]
        public void ReadInt32_MalformedAddress_ThrowsFormatException(string address)
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.Throws<FormatException>(() => memory.ReadInt32(address));
        }

        [Theory]
        [InlineData("NoDelimiters")]
        [InlineData("Module+OnlyPlus")]
        [InlineData("Module,OnlyComma")]
        public void ReadUInt16_MalformedAddress_ThrowsFormatException(string address)
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.Throws<FormatException>(() => memory.ReadUInt16(address));
        }

        [Fact]
        public void ReadString_WellFormedAddress_WithoutOpenProcess_ReturnsEmptyString()
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.Equal(string.Empty, memory.ReadString("Module+0,0", 4, Encoding.ASCII));
        }

        [Fact]
        public void WriteMemory_NonStringType_Throws()
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.Throws<ArgumentException>(
                () => memory.WriteMemory("Module+0,0", "int", "1", Encoding.ASCII));
        }

        [Fact]
        public void OpenProcess_NonPositivePid_ReturnsFalse()
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.False(memory.OpenProcess(0));
            Assert.False(memory.OpenProcess(-1));
        }

        [Fact]
        public void OpenProcess_UnknownPid_ReturnsFalse()
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.False(memory.OpenProcess(int.MaxValue));
        }

        [Fact]
        public void OpenProcess_CurrentProcess_SucceedsAndIsIdempotent()
        {
            ProcessMemory memory = new ProcessMemory();
            int pid = Process.GetCurrentProcess().Id;

            Assert.True(memory.OpenProcess(pid));
            Assert.Equal(pid, memory.mProc.Process.Id);
            Assert.NotEqual(IntPtr.Zero, memory.mProc.ModuleBaseAddress);

            // Re-attaching to the same, already-open process is a cheap no-op that still succeeds.
            Assert.True(memory.OpenProcess(pid));
            Assert.Equal(pid, memory.mProc.Process.Id);
        }
    }
}
