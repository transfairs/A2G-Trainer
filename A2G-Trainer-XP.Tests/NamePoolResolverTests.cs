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
                fake.Memory.Layout = PlayerControllerAdditionalTests.ScanOnlyLayout;

                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                Assert.Equal(poolAddress, resolver.ResolveFirstnameAddress(playerId: 0));
                Assert.Equal(poolAddress + 6, resolver.ResolveLastnameAddress(playerId: 0));
            }
        }

        [Fact]
        public void Resolve_WithTheGamesTable_UsesTheTableEntryInsteadOfCountingTerminators()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                // The table may point anywhere - here entry 1 deliberately points somewhere a
                // terminator count would never reach.
                uint poolAddress = (uint)fake.DisplayCacheBlock.ToInt64();
                fake.WriteDisplayCacheBytes("0", Encoding.GetEncoding("iso-8859-1").GetBytes("Alice\0Bob\0"));
                fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolTableOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolTableOffset + 4, BitConverter.GetBytes(poolAddress + 0x100));
                fake.WriteModuleBytes(Settings.NamePoolCountOffset, BitConverter.GetBytes((ushort)2));
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset, new byte[] { 0, 0, 1, 0 });

                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                Assert.Equal(poolAddress, resolver.ResolveFirstnameAddress(playerId: 0));
                Assert.Equal(poolAddress + 0x100, resolver.ResolveLastnameAddress(playerId: 0));
            }
        }

        [Fact]
        public void Resolve_WithTheGamesTable_IndexAtOrPastTheCount_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                uint poolAddress = (uint)fake.DisplayCacheBlock.ToInt64();
                fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolTableOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolCountOffset, BitConverter.GetBytes((ushort)1));
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset, new byte[] { 0, 0, 1, 0 });

                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                Assert.Null(resolver.ResolveLastnameAddress(playerId: 0));
            }
        }

        [Theory]
        [InlineData("Jonas", "Jonas")]
        [InlineData("  Jonas  ", "Jonas")]
        [InlineData("De Jong", "De Jong")]
        [InlineData("Jonas\tX", "Jonas")]
        [InlineData("\t Jonas", "Jonas")]
        [InlineData("   ", "")]
        [InlineData(null, "")]
        public void NormalizeLikeGame_TrimsAndCutsAtControlCharacters(string input, string expected)
        {
            Assert.Equal(expected, NamePoolResolver.NormalizeLikeGame(input));
        }

        [Fact]
        public void AppendName_WhenTrimmingYieldsAnExistingName_ReusesIt()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                uint poolAddress = (uint)fake.DisplayCacheBlock.ToInt64();
                fake.WriteDisplayCacheBytes("0", Encoding.GetEncoding("iso-8859-1").GetBytes("Alice\0\x01\0"));
                fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolTableOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolCountOffset, BitConverter.GetBytes((ushort)1));
                fake.WriteModuleBytes(Settings.NamePoolEndPointerOffset, BitConverter.GetBytes(poolAddress + 6));

                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                Assert.Equal((ushort?)0, resolver.AppendName(" Alice ", Encoding.GetEncoding("iso-8859-1")));
                Assert.Equal(BitConverter.GetBytes((ushort)1), fake.ReadModuleBytes(Settings.NamePoolCountOffset, 2));
            }
        }

        [Fact]
        public void AppendName_WithoutOriginalBuildAnchorsOrWithAnEmptyName_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                uint poolAddress = (uint)fake.DisplayCacheBlock.ToInt64();
                fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolEndPointerOffset, BitConverter.GetBytes(poolAddress));

                Assert.True(new NamePoolResolver(fake.Memory).CanAppend);
                Assert.Null(new NamePoolResolver(fake.Memory).AppendName("   ", Encoding.GetEncoding("iso-8859-1")));

                fake.Memory.Layout = PlayerControllerAdditionalTests.ScanOnlyLayout;
                Assert.False(new NamePoolResolver(fake.Memory).CanAppend);
                Assert.Null(new NamePoolResolver(fake.Memory).AppendName("Jonas", Encoding.GetEncoding("iso-8859-1")));
            }
        }

        [Fact]
        public void AppendName_WhenTrimmingYieldsANewName_AppendsTheTrimmedName()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                uint poolAddress = (uint)fake.DisplayCacheBlock.ToInt64();
                fake.WriteDisplayCacheBytes("0", Latin1.GetBytes("Alice\0"));
                fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolTableOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolCountOffset, BitConverter.GetBytes((ushort)1));
                fake.WriteModuleBytes(Settings.NamePoolEndPointerOffset, BitConverter.GetBytes(poolAddress + 6));

                Assert.Equal((ushort?)1, new NamePoolResolver(fake.Memory).AppendName(" Bob ", Latin1));
                Assert.Equal(Latin1.GetBytes("Bob\0"), fake.ReadDisplayCacheBytes("6", 4));
            }
        }

        [Fact]
        public void AppendName_WithoutPoolOrWithTheEndBeforeThePool_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                uint poolAddress = (uint)fake.DisplayCacheBlock.ToInt64();
                fake.WriteModuleBytes(Settings.NamePoolEndPointerOffset, BitConverter.GetBytes(poolAddress));
                // Pool pointer still zero.
                Assert.Null(new NamePoolResolver(fake.Memory).AppendName("Jonas", Latin1));

                fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress + 0x100));
                Assert.Null(new NamePoolResolver(fake.Memory).AppendName("Jonas", Latin1));
            }
        }

        [Fact]
        public void AppendName_WithTheBookkeepingUnreadable_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                uint poolAddress = (uint)fake.DisplayCacheBlock.ToInt64();
                fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolEndPointerOffset, BitConverter.GetBytes(poolAddress));
                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);
                Assert.True(resolver.CanAppend);

                // Pool pointer, end pointer and count share a page.
                fake.MakeModuleRegionUnreadable(Settings.NamePoolEndPointerOffset, 4);

                Assert.Null(resolver.AppendName("Jonas", Latin1));
            }
        }

        [Fact]
        public void Resolve_WithTheGamesTable_UnreadableOrInvalidEntry_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                uint poolAddress = (uint)fake.DisplayCacheBlock.ToInt64();
                fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                fake.WriteModuleBytes(Settings.NamePoolCountOffset, BitConverter.GetBytes((ushort)2));
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset, new byte[] { 0, 0, 1, 0 });
                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                // Entry 0 still zero - not a pool address.
                Assert.Null(resolver.ResolveFirstnameAddress(playerId: 0));

                fake.MakeModuleRegionUnreadable(Settings.NamePoolTableOffset, 8);
                Assert.Null(resolver.ResolveLastnameAddress(playerId: 0));
            }
        }

        [Fact]
        public void Resolve_WhenThePoolPointerItselfIsUnreadable_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                fake.Memory.Layout = PlayerControllerAdditionalTests.ScanOnlyLayout;
                fake.MakeModuleRegionUnreadable(Settings.NamePoolPointerOffset, 4);

                Assert.Null(new NamePoolResolver(fake.Memory).ResolveFirstnameAddress(playerId: 0));
            }
        }

        [Fact]
        public void Resolve_WhenPoolPointerIsZero_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                // NamePoolPointerOffset is left at the fake module block's zero-initialized default.
                fake.Memory.Layout = PlayerControllerAdditionalTests.ScanOnlyLayout;
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
                    fake.Memory.Layout = PlayerControllerAdditionalTests.ScanOnlyLayout;

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
                    fake.Memory.Layout = PlayerControllerAdditionalTests.ScanOnlyLayout;

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
                    fake.Memory.Layout = PlayerControllerAdditionalTests.ScanOnlyLayout;

                    NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                    Assert.Null(resolver.ResolveFirstnameAddress(playerId: 0));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pool);
            }
        }

        // Runs test against a pool of exactly poolBytes, zero-padded to poolLength (which must cover
        // every scan-buffer read the test triggers - ReadProcessMemory fails a partially unbacked range).
        private static void WithPool(byte[] poolBytes, int poolLength, Action<NamePoolResolver> test)
        {
            IntPtr pool = Marshal.AllocHGlobal(poolLength);
            try
            {
                Marshal.Copy(new byte[poolLength], 0, pool, poolLength);
                Marshal.Copy(poolBytes, 0, pool, poolBytes.Length);

                using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
                {
                    fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(unchecked((uint)pool.ToInt64())));
                    test(new NamePoolResolver(fake.Memory));
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pool);
            }
        }

        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        [Fact]
        public void FindIndexOf_ReturnsTheFirstExactMatch()
        {
            WithPool(Latin1.GetBytes("Josefine\0Jakob\0Josef\0Josef\0"), 0x10000, resolver =>
                Assert.Equal((ushort?)2, resolver.FindIndexOf("Josef", Latin1)));
        }

        [Fact]
        public void FindIndexOf_StopsAtTheFirstNonNameEntryInsteadOfMatchingGarbagePastThePool()
        {
            WithPool(Latin1.GetBytes("Jakob\0\u0001\u0002\0Josef\0"), 0x10000, resolver =>
                Assert.Null(resolver.FindIndexOf("Josef", Latin1)));
        }

        [Fact]
        public void FindIndexOf_WhenEntryIsPastTheFirstScanBuffer_GrowsTheBufferAndFindsIt()
        {
            byte[] filler = new byte[0x10000 + 10];
            for (int i = 0; i < filler.Length; i++)
                filler[i] = (byte)'A';
            filler[filler.Length - 1] = 0;

            WithPool(Concat(filler, Latin1.GetBytes("Josef\0")), 0x20000, resolver =>
                Assert.Equal((ushort?)1, resolver.FindIndexOf("Josef", Latin1)));
        }

        [Fact]
        public void FindIndexOf_WhenAllAddressableIndicesAreChecked_ReturnsNull()
        {
            // 65536 one-character entries ("A\0") fill every UInt16 index; "Josef" sits just past them.
            byte[] entries = new byte[(ushort.MaxValue + 1) * 2];
            for (int i = 0; i < entries.Length; i += 2)
                entries[i] = (byte)'A';

            WithPool(Concat(entries, Latin1.GetBytes("Josef\0")), 0x40000, resolver =>
                Assert.Null(resolver.FindIndexOf("Josef", Latin1)));
        }

        [Fact]
        public void FindIndexOf_WhenTheGrownScanBufferIsUnreadable_ReturnsNull()
        {
            byte[] noTerminator = new byte[0x10000];
            for (int i = 0; i < noTerminator.Length; i++)
                noTerminator[i] = (byte)'A';

            // Only the first 64KB are backed, so the doubled read fails.
            WithPool(noTerminator, 0x10000, resolver =>
                Assert.Null(resolver.FindIndexOf("Josef", Latin1)));
        }

        [Fact]
        public void FindIndexOf_WhenNoTerminatorFitsEvenTheLargestScanBuffer_ReturnsNull()
        {
            const int maxScanBufferSize = 0x800000;
            byte[] noTerminator = new byte[maxScanBufferSize];
            for (int i = 0; i < noTerminator.Length; i++)
                noTerminator[i] = (byte)'A';

            WithPool(noTerminator, maxScanBufferSize, resolver =>
                Assert.Null(resolver.FindIndexOf("Josef", Latin1)));
        }

        [Fact]
        public void FindIndexOf_WhenThePoolIsUnreadable_ReturnsNull()
        {
            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x1000, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            Assert.NotEqual(IntPtr.Zero, page);
            try
            {
                Assert.True(VirtualProtect(page, (UIntPtr)0x1000, PAGE_NOACCESS, out _));

                using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
                {
                    fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(unchecked((uint)page.ToInt64())));

                    Assert.Null(new NamePoolResolver(fake.Memory).FindIndexOf("Josef", Latin1));
                }
            }
            finally
            {
                VirtualFree(page, UIntPtr.Zero, MEM_RELEASE);
            }
        }

        [Fact]
        public void FindIndexOf_WithEmptyValueOrWithoutPool_ReturnsNull()
        {
            WithPool(Latin1.GetBytes("Jakob\0"), 0x10000, resolver =>
            {
                Assert.Null(resolver.FindIndexOf("", Latin1));
                Assert.Null(resolver.FindIndexOf(null, Latin1));
            });

            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                // Pool pointer left at zero.
                Assert.Null(new NamePoolResolver(fake.Memory).FindIndexOf("Josef", Latin1));
                // No pool pointer known for this build at all.
                PersistentLayout noPool = new PersistentLayout(Settings.PlayerRecordTableOffset, null, null, null, isVerified: true);
                Assert.Null(new NamePoolResolver(fake.Memory, noPool).FindIndexOf("Josef", Latin1));
            }
        }

        [Fact]
        public void CountReferences_CountsBothNameFieldsOfSelfReferencingRecordsOnly()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                uint stride = Settings.PlayerRecordStride;
                // [+0 Firstname, +2 Lastname, +4 own PlayerId]
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset + 1 * stride, new byte[] { 7, 0, 8, 0, 1, 0 });
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset + 2 * stride, new byte[] { 9, 0, 7, 0, 2, 0 });
                // Not a real record (self-reference doesn't match) - must not count.
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset + 3 * stride, new byte[] { 7, 0, 7, 0, 5, 0 });

                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                Assert.Equal(2, resolver.CountReferences(7));
                Assert.Equal(1, resolver.CountReferences(8));
                Assert.Equal((ushort?)9, resolver.ReadFirstnameIndex(2));
                Assert.Equal((ushort?)7, resolver.ReadLastnameIndex(2));
            }
        }

        [Fact]
        public void CountReferences_AndReadIndex_WithoutARecordTable_ReturnNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                PersistentLayout noTable = new PersistentLayout(null, Settings.NamePoolPointerOffset, null, null, isVerified: true);
                NamePoolResolver resolver = new NamePoolResolver(fake.Memory, noTable);

                Assert.Null(resolver.CountReferences(0));
                Assert.Null(resolver.ReadFirstnameIndex(0));
            }
        }

        [Fact]
        public void CountReferences_WhenTheTableStartsTooCloseToTheModuleEnd_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                PersistentLayout tableAtEnd = new PersistentLayout(LargeModuleBlockSize - 0x10, Settings.NamePoolPointerOffset, null, null, isVerified: true);

                Assert.Null(new NamePoolResolver(fake.Memory, tableAtEnd).CountReferences(0));
            }
        }

        [Fact]
        public void CountReferences_AndReadIndex_WhenTheTableIsUnreadable_ReturnNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: (int)LargeModuleBlockSize))
            {
                fake.MakeModuleRegionUnreadable(Settings.PlayerRecordTableOffset, 0x10);
                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                Assert.Null(resolver.CountReferences(0));
                Assert.Null(resolver.ReadLastnameIndex(0));
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
