using System;
using System.Diagnostics;
using System.Text;
using A2G_Trainer.Controller;
using Xunit;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Tests
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
        public void OpenProcess_ZeroOrUnknownNegativePid_ReturnsFalse()
        {
            ProcessMemory memory = new ProcessMemory();

            Assert.False(memory.OpenProcess(0));
            // Negative IDs are valid on Windows 9x; on NT there is no such process.
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

        // Windows 9x path: a page-file mapping plus a copy of its handle in the game. Here the "game"
        // is the test process itself, where the mapped view is ordinary memory.
        private static uint? AllocateShared(ProcessMemory memory, int size)
        {
            bool before = ProcessMemory.UseSharedArena;
            ProcessMemory.UseSharedArena = true;
            try
            {
                return memory.AllocateExecutable(size);
            }
            finally
            {
                ProcessMemory.UseSharedArena = before;
            }
        }

        [Fact]
        public void AllocateExecutable_SharedArena_GivesWritableMemory()
        {
            ProcessMemory memory = new ProcessMemory();
            Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));

            uint? block = AllocateShared(memory, 0x100);

            Assert.True(block.HasValue);
            byte[] data = { 0xC3, 0x90, 0x90, 0xCC };
            memory.WriteBytesAtAddress(block.Value, data);
            Assert.Equal(data, memory.ReadBytesAtAddress(block.Value, data.Length));
        }

        [Fact]
        public void AllocateExecutable_SharedArena_WithoutHandleInGame_ReturnsNull()
        {
            // No process attached: the mapping can't be handed to the game, so nothing is allocated.
            Assert.Null(AllocateShared(new ProcessMemory(), 0x100));
        }

        [Fact]
        public void AllocateExecutable_SharedArena_EmptySize_ReturnsNull()
        {
            ProcessMemory memory = new ProcessMemory();
            Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));

            // A page-file mapping of size zero is refused.
            Assert.Null(AllocateShared(memory, 0));
        }
    }
}
