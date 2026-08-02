using System;
using System.Runtime.InteropServices;
using System.Text;
using A2G_Trainer_XP.Controller;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for NamePoolResolver's persistent name-pool scan, including every early-exit and
    /// buffer-growth path. The module block is sized well past NamePoolPointerOffset/
    /// PlayerRecordTableOffset (the FakeModule default is deliberately smaller, so other tests can
    /// rely on those reads failing) so the pool-pointer/record fields are actually writable here.
    /// </summary>
    public class NamePoolResolverTests
    {
        private const uint LargeModuleBlockSize = 0x800000;

        private const uint MEM_COMMIT = 0x1000;
        private const uint MEM_RESERVE = 0x2000;
        private const uint MEM_RELEASE = 0x8000;
        private const uint PAGE_READWRITE = 0x04;
        private const uint PAGE_NOACCESS = 0x01;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr lpAddress, UIntPtr dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtect(IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualFree(IntPtr lpAddress, UIntPtr dwSize, uint dwFreeType);

        [Fact]
        public void ResolveFirstnameAddress_WithReadablePool_ReturnsAddressOfTheTargetString()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
                byte[] pool = Concat(latin1.GetBytes("Alice\0"), latin1.GetBytes("Bob\0"));
                fake.WriteDisplayCacheBytes("0", pool);
                uint poolAddress = (uint)fake.DisplayCacheBlock.ToInt64();

                fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset, BitConverter.GetBytes((ushort)0));
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset + 2, BitConverter.GetBytes((ushort)1));

                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                Assert.Equal(poolAddress, resolver.ResolveFirstnameAddress(playerId: 0));
                Assert.Equal(poolAddress + 6, resolver.ResolveLastnameAddress(playerId: 0));
            }
        }

        [Fact]
        public void Resolve_WhenPoolPointerIsZero_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                // NamePoolPointerOffset is left at the fake module block's zero-initialized default.
                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                Assert.Null(resolver.ResolveFirstnameAddress(playerId: 0));
            }
        }

        [Fact]
        public void Resolve_WhenPoolPointerTargetsUnreadableMemory_ReturnsNull()
        {
            // A fixed "plausible-looking but probably unmapped" address (e.g. 0x20000) isn't
            // reliably unreadable - as this test assembly grew, that address occasionally landed in
            // real committed memory and made the test flaky. A page explicitly committed then
            // switched to PAGE_NOACCESS is guaranteed unreadable regardless of what else is mapped.
            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x1000, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            Assert.NotEqual(IntPtr.Zero, page);
            try
            {
                Assert.True(VirtualProtect(page, (UIntPtr)0x1000, PAGE_NOACCESS, out _));

                using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
                {
                    fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(unchecked((uint)page.ToInt64())));

                    NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                    Assert.Null(resolver.ResolveFirstnameAddress(playerId: 0));
                }
            }
            finally
            {
                VirtualFree(page, UIntPtr.Zero, MEM_RELEASE);
            }
        }

        [Fact]
        public void Resolve_WhenTargetStringIsPastTheFirstScanBuffer_GrowsTheBufferAndSucceeds()
        {
            const int initialScanBufferSize = 0x10000;
            const int fillerLength = initialScanBufferSize + 5000;
            // Must cover the full second (doubled) scan-buffer read, not just up to the terminator -
            // ReadProcessMemory fails the whole call if any part of the requested range is unbacked.
            const int secondScanBufferSize = initialScanBufferSize * 2;
            int poolLength = secondScanBufferSize;

            IntPtr pool = Marshal.AllocHGlobal(poolLength);
            try
            {
                byte[] filler = new byte[poolLength];
                for (int i = 0; i < filler.Length; i++)
                    filler[i] = 0xAA;
                filler[fillerLength] = 0; // the only null terminator, past the first (64KB) scan buffer
                Marshal.Copy(filler, 0, pool, filler.Length);

                using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
                {
                    uint poolAddress = unchecked((uint)pool.ToInt64());
                    fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                    fake.WriteModuleBytes(Settings.PlayerRecordTableOffset, BitConverter.GetBytes((ushort)1));

                    NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                    Assert.Equal((uint)(poolAddress + fillerLength + 1), resolver.ResolveFirstnameAddress(playerId: 0));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pool);
            }
        }

        [Fact]
        public void Resolve_WhenTargetIndexIsNeverFoundEvenAtMaxScanBufferSize_ReturnsNull()
        {
            const int maxScanBufferSize = 0x800000;

            IntPtr pool = Marshal.AllocHGlobal(maxScanBufferSize);
            try
            {
                byte[] filler = new byte[maxScanBufferSize];
                for (int i = 0; i < filler.Length; i++)
                    filler[i] = 0xAA; // never a null terminator anywhere in the whole buffer
                Marshal.Copy(filler, 0, pool, filler.Length);

                using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
                {
                    uint poolAddress = unchecked((uint)pool.ToInt64());
                    fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                    fake.WriteModuleBytes(Settings.PlayerRecordTableOffset, BitConverter.GetBytes((ushort)1));

                    NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                    Assert.Null(resolver.ResolveFirstnameAddress(playerId: 0));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pool);
            }
        }

        private static byte[] Concat(byte[] a, byte[] b)
        {
            byte[] result = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, result, 0, a.Length);
            Buffer.BlockCopy(b, 0, result, a.Length, b.Length);
            return result;
        }
    }
}
