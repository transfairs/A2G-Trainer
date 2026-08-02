using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using A2G_Trainer_XP.Controller;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    // Unlike ProcessMemoryTests, these self-attach to the current test process and drive the
    // real ReadProcessMemory/WriteProcessMemory Win32 calls end-to-end - the one thing the
    // unit tests can't exercise, since without a real handle every read/write there is a no-op.
    //
    // ProcessMemory's address scheme is "*(uint32*)(moduleBase + offsetA) + offsetB": it
    // dereferences a 4-byte pointer field and adds an offset. To drive that for real without a
    // live game, we allocate our own unmanaged "pointer field" and "data" blocks, point the
    // former at the latter, and derive offsetA from the pointer field's real address relative
    // to our own module base - exactly what the game's memory layout looks like from the
    // trainer's point of view, just self-referential.
    //
    // This only works if the test process itself is 32-bit (see the PlatformTarget=x86 note in
    // the .csproj): every address here gets truncated to uint32, so a 64-bit host's module base
    // or heap pointers (which can sit above 4GB) would be silently corrupted by that truncation.
    /// <summary>Integration tests for ProcessMemory against a real (self-attached) process.</summary>
    public class ProcessMemoryIntegrationTests
    {
        private static (ProcessMemory memory, uint moduleBase) AttachToSelf()
        {
            Assert.Equal(4, IntPtr.Size); // guard: only meaningful in a 32-bit process, see class remarks

            ProcessMemory memory = new ProcessMemory();
            Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));

            return (memory, (uint)memory.mProc.ModuleBaseAddress.ToInt64());
        }

        private static string BuildAddress(uint moduleBase, IntPtr pointerField, string offsetBHex = "0")
        {
            uint pointerFieldAddress = (uint)pointerField.ToInt64();
            uint offsetA = pointerFieldAddress - moduleBase; // wraps correctly mod 2^32 either way round
            return $"self+{offsetA:X},{offsetBHex}";
        }

        [Fact]
        public void ReadBytes_ThroughRealPointerIndirection_ReturnsWhatWasActuallyWritten()
        {
            (ProcessMemory memory, uint moduleBase) = AttachToSelf();

            IntPtr dataBlock = Marshal.AllocHGlobal(4);
            IntPtr pointerField = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.WriteInt32(dataBlock, unchecked((int)0x12345678));
                Marshal.WriteInt32(pointerField, unchecked((int)(uint)dataBlock.ToInt64()));

                byte[] bytes = memory.ReadBytes(BuildAddress(moduleBase, pointerField), 4);

                Assert.NotNull(bytes);
                Assert.Equal(0x12345678, BitConverter.ToInt32(bytes, 0));
            }
            finally
            {
                Marshal.FreeHGlobal(dataBlock);
                Marshal.FreeHGlobal(pointerField);
            }
        }

        [Fact]
        public void WriteBytes_ThroughRealPointerIndirection_ActuallyWritesToTargetMemory()
        {
            (ProcessMemory memory, uint moduleBase) = AttachToSelf();

            IntPtr dataBlock = Marshal.AllocHGlobal(4);
            IntPtr pointerField = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.WriteInt32(dataBlock, 0);
                Marshal.WriteInt32(pointerField, unchecked((int)(uint)dataBlock.ToInt64()));

                memory.WriteBytes(BuildAddress(moduleBase, pointerField), BitConverter.GetBytes(0x1A2B3C4D));

                Assert.Equal(0x1A2B3C4D, Marshal.ReadInt32(dataBlock));
            }
            finally
            {
                Marshal.FreeHGlobal(dataBlock);
                Marshal.FreeHGlobal(pointerField);
            }
        }

        [Fact]
        public void ReadInt32_ThroughRealPointerIndirection_ReturnsWhatWasActuallyWritten()
        {
            (ProcessMemory memory, uint moduleBase) = AttachToSelf();

            IntPtr dataBlock = Marshal.AllocHGlobal(4);
            IntPtr pointerField = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.WriteInt32(dataBlock, unchecked((int)0x89ABCDEF));
                Marshal.WriteInt32(pointerField, unchecked((int)(uint)dataBlock.ToInt64()));

                int value = memory.ReadInt32(BuildAddress(moduleBase, pointerField));

                Assert.Equal(unchecked((int)0x89ABCDEF), value);
            }
            finally
            {
                Marshal.FreeHGlobal(dataBlock);
                Marshal.FreeHGlobal(pointerField);
            }
        }

        [Fact]
        public void ReadUInt16_ThroughRealPointerIndirection_ReturnsWhatWasActuallyWritten()
        {
            (ProcessMemory memory, uint moduleBase) = AttachToSelf();

            IntPtr dataBlock = Marshal.AllocHGlobal(2);
            IntPtr pointerField = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.WriteInt16(dataBlock, unchecked((short)0xBEEF));
                Marshal.WriteInt32(pointerField, unchecked((int)(uint)dataBlock.ToInt64()));

                ushort value = memory.ReadUInt16(BuildAddress(moduleBase, pointerField));

                Assert.Equal(unchecked((ushort)0xBEEF), value);
            }
            finally
            {
                Marshal.FreeHGlobal(dataBlock);
                Marshal.FreeHGlobal(pointerField);
            }
        }

        [Fact]
        public void ReadString_ThroughRealPointerIndirection_ReadsBackWrittenText()
        {
            (ProcessMemory memory, uint moduleBase) = AttachToSelf();

            Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
            byte[] expected = latin1.GetBytes("Klose\0\0\0\0");
            IntPtr dataBlock = Marshal.AllocHGlobal(expected.Length);
            IntPtr pointerField = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.Copy(expected, 0, dataBlock, expected.Length);
                Marshal.WriteInt32(pointerField, unchecked((int)(uint)dataBlock.ToInt64()));

                string result = memory.ReadString(BuildAddress(moduleBase, pointerField), expected.Length, latin1);

                Assert.Equal("Klose", result);
            }
            finally
            {
                Marshal.FreeHGlobal(dataBlock);
                Marshal.FreeHGlobal(pointerField);
            }
        }

        [Fact]
        public void ReadBytes_RespectsOffsetB_AfterDereferencingThePointer()
        {
            (ProcessMemory memory, uint moduleBase) = AttachToSelf();

            // 8-byte block: bytes 0-3 are a decoy, bytes 4-7 are the value offsetB should land on.
            IntPtr dataBlock = Marshal.AllocHGlobal(8);
            IntPtr pointerField = Marshal.AllocHGlobal(4);
            try
            {
                Marshal.WriteInt32(dataBlock, unchecked((int)0xDEADBEEF));
                Marshal.WriteInt32(dataBlock + 4, unchecked((int)0x0BADF00D));
                Marshal.WriteInt32(pointerField, unchecked((int)(uint)dataBlock.ToInt64()));

                byte[] bytes = memory.ReadBytes(BuildAddress(moduleBase, pointerField, "4"), 4);

                Assert.NotNull(bytes);
                Assert.Equal(0x0BADF00D, BitConverter.ToInt32(bytes, 0));
            }
            finally
            {
                Marshal.FreeHGlobal(dataBlock);
                Marshal.FreeHGlobal(pointerField);
            }
        }
    }
}
