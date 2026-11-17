using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using AnstossTrainer.Shared;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Two adjacent 64 KB blocks of this process: a writable one and a read-only view of a file
    /// mapping, whose protection can't be lifted - so code placed there can be read but not patched.
    /// <see cref="ReadOnlyFirst"/> says which of the two lies lower. Used to make one particular call
    /// site of a stub installer fail to be redirected while another one works.
    /// </summary>
    internal sealed class SplitCodeMemory : IDisposable
    {
        internal const int Half = 0x10000;
        private const uint PageReadWrite = 0x04;
        private const uint PageNoAccess = 0x01;
        private const uint MemCommit = 0x1000;
        private const uint MemReserve = 0x2000;
        private const uint MemRelease = 0x8000;
        private const uint FileMapWrite = 0x02;
        private const uint FileMapRead = 0x04;

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
        [DllImport("kernel32.dll")]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFileMapping(IntPtr file, IntPtr attributes, uint protect, uint sizeHigh, uint sizeLow, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFileEx(IntPtr mapping, uint access, uint offsetHigh, uint offsetLow, UIntPtr size, IntPtr address);
        [DllImport("kernel32.dll")]
        private static extern bool UnmapViewOfFile(IntPtr address);

        /// <summary>Whether the read-only block lies below the writable one.</summary>
        public bool ReadOnlyFirst { get; }
        /// <summary>Start of the writable block.</summary>
        public uint Writable { get; }
        /// <summary>Start of the read-only block.</summary>
        public uint ReadOnly { get; }
        /// <summary>This process, attached through a fresh ProcessMemory.</summary>
        public ProcessMemory Memory { get; }

        private readonly IntPtr mapping;
        private readonly IntPtr writableView;
        private IntPtr readOnlyView;
        private IntPtr block;

        public SplitCodeMemory(bool readOnlyFirst)
        {
            this.ReadOnlyFirst = readOnlyFirst;
            this.mapping = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, PageReadWrite, 0, Half, null);
            Assert.NotEqual(IntPtr.Zero, this.mapping);
            this.writableView = MapViewOfFileEx(this.mapping, FileMapWrite, 0, 0, (UIntPtr)Half, IntPtr.Zero);
            Assert.NotEqual(IntPtr.Zero, this.writableView);
            for (int attempt = 0; attempt < 10 && this.readOnlyView == IntPtr.Zero; attempt++)
            {
                IntPtr reserved = VirtualAlloc(IntPtr.Zero, (UIntPtr)(2 * Half), MemReserve, PageNoAccess);
                VirtualFree(reserved, UIntPtr.Zero, MemRelease);
                IntPtr blockAt = readOnlyFirst ? reserved + Half : reserved;
                IntPtr viewAt = readOnlyFirst ? reserved : reserved + Half;
                this.block = VirtualAlloc(blockAt, (UIntPtr)Half, MemCommit | MemReserve, PageReadWrite);
                if (this.block == IntPtr.Zero)
                    continue;
                this.readOnlyView = MapViewOfFileEx(this.mapping, FileMapRead, 0, 0, (UIntPtr)Half, viewAt);
                if (this.readOnlyView == IntPtr.Zero)
                {
                    VirtualFree(this.block, UIntPtr.Zero, MemRelease);
                    this.block = IntPtr.Zero;
                }
            }
            Assert.NotEqual(IntPtr.Zero, this.readOnlyView);
            this.Writable = (uint)this.block.ToInt64();
            this.ReadOnly = (uint)this.readOnlyView.ToInt64();

            this.Memory = new ProcessMemory();
            Assert.True(this.Memory.OpenProcess(Process.GetCurrentProcess().Id));
        }

        /// <summary>Puts the attached module's base at <paramref name="moduleBase"/>.</summary>
        public void SetModuleBase(uint moduleBase) => this.Memory.mProc.ModuleBaseAddress = new IntPtr(unchecked((int)moduleBase));

        /// <summary>Writes <paramref name="data"/> at <paramref name="address"/> in either block.</summary>
        public void Write(uint address, byte[] data)
        {
            if (address >= this.ReadOnly && address < this.ReadOnly + Half)
                Marshal.Copy(data, 0, this.writableView + (int)(address - this.ReadOnly), data.Length);
            else
                Marshal.Copy(data, 0, new IntPtr(unchecked((int)address)), data.Length);
        }

        /// <summary>Writes a CALL rel32 at <paramref name="site"/> to <paramref name="target"/>.</summary>
        public void WriteCall(uint site, uint target) =>
            this.Write(site, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(unchecked((int)(target - (site + 5))))).ToArray());

        /// <summary>Where the CALL rel32 at <paramref name="site"/> goes now.</summary>
        public uint CallTarget(uint site)
        {
            byte[] call = this.Memory.ReadBytesAtAddress(site, 5);
            return unchecked((uint)(site + 5 + BitConverter.ToInt32(call, 1)));
        }

        public void Dispose()
        {
            UnmapViewOfFile(this.readOnlyView);
            UnmapViewOfFile(this.writableView);
            CloseHandle(this.mapping);
            VirtualFree(this.block, UIntPtr.Zero, MemRelease);
            CloseHandle(this.Memory.mProc.Handle);
        }
    }
}
