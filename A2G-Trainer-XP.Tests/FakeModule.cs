using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using A2G_Trainer_XP.Controller;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Stands in for the game's process module in tests: allocates a real block of this test
    /// process's own memory to act as "module base", plus a second block to act as the transient
    /// display-cache region, and lets tests wire pointer slots/bytes into either without needing
    /// a real Anstoss 2 Gold process attached.
    /// </summary>
    internal sealed class FakeModule : IDisposable
    {
        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_NOACCESS = 0x01;
        private const int PageSize = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

        public const int DefaultModuleBlockSize = 0x520000;
        private const int DisplayCacheBlockSize = 0x40000;

        public ProcessMemory Memory { get; }
        public uint ModuleBase { get; }
        private readonly IntPtr moduleBlock;
        private readonly int moduleBlockSize;
        public IntPtr DisplayCacheBlock { get; }

        public FakeModule(int moduleBlockSize = DefaultModuleBlockSize, ProcessMemory memory = null)
        {
            this.Memory = memory ?? new ProcessMemory();
            if (this.Memory.mProc.Process == null && !this.Memory.OpenProcess(Process.GetCurrentProcess().Id))
                throw new InvalidOperationException("Self-attach failed - required for FakeModule.");

            this.moduleBlockSize = moduleBlockSize;
            this.moduleBlock = VirtualAlloc(IntPtr.Zero, (UIntPtr)moduleBlockSize, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (this.moduleBlock == IntPtr.Zero)
                throw new InvalidOperationException("VirtualAlloc failed for FakeModule's module block.");

            this.Memory.mProc.ModuleBaseAddress = this.moduleBlock;
            this.ModuleBase = (uint)this.moduleBlock.ToInt64();

            this.DisplayCacheBlock = Marshal.AllocHGlobal(DisplayCacheBlockSize);
            byte[] zeros = new byte[DisplayCacheBlockSize];
            Marshal.Copy(zeros, 0, this.DisplayCacheBlock, DisplayCacheBlockSize);
        }

        /// <summary>Writes a pointer to <see cref="DisplayCacheBlock"/> at the given module-base offset, so ResolveAddress's pointer chase lands in the fake display cache.</summary>
        public void RoutePointerSlot(string hexOffsetFromModuleBase)
        {
            uint offset = ParseHex(hexOffsetFromModuleBase);
            Marshal.WriteInt32(this.moduleBlock + (int)offset, unchecked((int)(uint)this.DisplayCacheBlock.ToInt64()));
        }

        public void WriteDisplayCacheBytes(string hexOffset, byte[] data) => Marshal.Copy(data, 0, this.DisplayCacheBlock + (int)ParseHex(hexOffset), data.Length);

        public byte[] ReadDisplayCacheBytes(string hexOffset, int length)
        {
            byte[] buffer = new byte[length];
            Marshal.Copy(this.DisplayCacheBlock + (int)ParseHex(hexOffset), buffer, 0, length);
            return buffer;
        }

        public void WriteModuleBytes(uint offset, byte[] data) => Marshal.Copy(data, 0, this.moduleBlock + (int)offset, data.Length);

        public byte[] ReadModuleBytes(uint offset, int length)
        {
            byte[] buffer = new byte[length];
            Marshal.Copy(this.moduleBlock + (int)offset, buffer, 0, length);
            return buffer;
        }

        /// <summary>Revokes read access to the page(s) covering the given module-relative range, to test unreadable-address fallback paths.</summary>
        public void MakeModuleRegionUnreadable(uint offset, int length)
        {
            uint pageStart = offset & ~(uint)(PageSize - 1);
            uint pageEnd = (offset + (uint)length + (uint)PageSize - 1) & ~(uint)(PageSize - 1);
            if (!VirtualProtect(this.moduleBlock + (int)pageStart, (UIntPtr)(pageEnd - pageStart), PAGE_NOACCESS, out _))
                throw new InvalidOperationException("VirtualProtect failed while isolating a FakeModule region.");
        }

        public void Dispose()
        {
            VirtualFree(this.moduleBlock, UIntPtr.Zero, MEM_RELEASE);
            Marshal.FreeHGlobal(this.DisplayCacheBlock);
        }

        private static uint ParseHex(string token)
        {
            token = token.Trim();
            return token.Length == 0 ? 0u : Convert.ToUInt32(token, 16);
        }
    }
}
