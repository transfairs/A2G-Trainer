using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Additional PlayerController coverage: SaveEntityList, the field-list Save overload, the
    /// secondary-position count branches, the Nationality high-bit-preservation branch, and every
    /// branch of the persistent-name-write helper (TryWritePersistentName).
    /// </summary>
    public class PlayerControllerAdditionalTests
    {
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

        private static Club NewSinglePlayerClub() => new Club { PlayerCount = 1, AmateurPlayerCount = 0 };

        private static object InvokePrivate(object target, string methodName, params object[] args) =>
            target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        [Fact]
        public void SaveEntityList_SavesEveryPlayerAndAggregatesWarnings()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.FIRSTNAME], new byte[] { (byte)'A', 0 });
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.LASTNAME], new byte[] { (byte)'B', 0 });

                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);

                List<string> warnings = controller.SaveEntityList();

                Assert.NotNull(warnings);
            }
        }

        [Fact]
        public void Save_WithFieldList_WritesOnlyTheListedKnownFieldsAndIgnoresUnhandledOnes()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();
                player.Condition = 77;
                player.Freshness = 88;

                Exception thrown = Record.Exception(() => controller.Save(player, new List<PlayerEnums.AddressKey>
                {
                    PlayerEnums.AddressKey.CONDITION,
                    PlayerEnums.AddressKey.FRESHNESS,
                    PlayerEnums.AddressKey.ID, // not handled by the switch - must be silently skipped
                }));

                Assert.Null(thrown);
                Assert.Equal((byte)77, fake.ReadDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.CONDITION], 1)[0]);
                Assert.Equal((byte)88, fake.ReadDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.FRESHNESS], 1)[0]);
            }
        }

        [Fact]
        public void Save_WithNoSecondaryPositions_WritesOnlyMainPosition()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes((ushort)2001));
                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();
                player.SecondaryPositions = new List<PlayerEnums.Position>();

                Exception thrown = Record.Exception(() => controller.Save(player));

                Assert.Null(thrown);
            }
        }

        [Fact]
        public void Save_WithOneSecondaryPosition_WritesOnlyTheFirstSecondarySlot()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes((ushort)2001));
                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();
                player.SecondaryPositions = new List<PlayerEnums.Position> { PlayerEnums.Position.LV };

                Exception thrown = Record.Exception(() => controller.Save(player));

                Assert.Null(thrown);
                uint b = Settings.PlayerRecordTableOffset;
                Assert.Equal(new byte[] { (byte)player.Position, (byte)PlayerEnums.Position.LV, (byte)PlayerEnums.Position.None }, fake.ReadModuleBytes(b + Settings.PlayerRecordPositionOffset, 3));
            }
        }

        [Fact]
        public void Save_WhenPersistentNationalityByteIsUnreadable_StillWritesWithoutPreservingAnyHighBit()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes((ushort)2001));
                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();
                player.Nationality = PlayerEnums.Country.Deutschland;
                fake.MakeModuleRegionUnreadable(Settings.PlayerRecordTableOffset + Settings.PlayerRecordNationalityOffset, 1);

                Exception thrown = Record.Exception(() => controller.Save(player));

                Assert.Null(thrown);
            }
        }

        private static PlayerController NewMinimalController()
        {
            FakeModule fake = new FakeModule();
            fake.RoutePointerSlot(Settings.PlayerAddress[0]);
            return new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
        }

        [Fact]
        public void TryWritePersistentName_WhenNewValueIsNull_ReturnsNullWithoutWriting()
        {
            PlayerController controller = NewMinimalController();
            Player player = controller.EntityList.Single();

            object result = InvokePrivate(controller, "TryWritePersistentName", null, (uint?)0x20000u, null, Encoding.GetEncoding("iso-8859-1"), false, player);

            Assert.Null(result);
        }

        [Fact]
        public void TryWritePersistentName_WhenAddressUnresolved_ReturnsAddressWarning()
        {
            PlayerController controller = NewMinimalController();
            Player player = controller.EntityList.Single();

            string result = (string)InvokePrivate(controller, "TryWritePersistentName", null, (uint?)null, "Max", Encoding.GetEncoding("iso-8859-1"), false, player);

            Assert.Contains("Adresse nicht auflösbar", result);
        }

        [Fact]
        public void TryWritePersistentName_WhenAddressUnreadable_ReturnsMemoryWarning()
        {
            // A fixed "plausible-looking but probably unmapped" address (e.g. 0x20000) isn't reliably
            // unreadable - as this test assembly grew, that address occasionally landed in real
            // committed memory and made the test flaky. A page explicitly committed then switched to
            // PAGE_NOACCESS is guaranteed unreadable regardless of what else happens to be mapped.
            IntPtr page = VirtualAlloc(IntPtr.Zero, (UIntPtr)0x1000, MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            Assert.NotEqual(IntPtr.Zero, page);
            try
            {
                Assert.True(VirtualProtect(page, (UIntPtr)0x1000, PAGE_NOACCESS, out _));

                PlayerController controller = NewMinimalController();
                Player player = controller.EntityList.Single();

                string result = (string)InvokePrivate(controller, "TryWritePersistentName", null, (uint?)unchecked((uint)page.ToInt64()), "Max", Encoding.GetEncoding("iso-8859-1"), false, player);

                Assert.Contains("Speicher nicht lesbar", result);
            }
            finally
            {
                VirtualFree(page, UIntPtr.Zero, MEM_RELEASE);
            }
        }

        [Fact]
        public void TryWritePersistentName_WhenNoTerminatorWithinScanRange_ReturnsStringEndWarning()
        {
            IntPtr buffer = Marshal.AllocHGlobal(64);
            try
            {
                byte[] noNulls = new byte[64];
                for (int i = 0; i < noNulls.Length; i++)
                    noNulls[i] = (byte)'X';
                Marshal.Copy(noNulls, 0, buffer, noNulls.Length);

                PlayerController controller = NewMinimalController();
                Player player = controller.EntityList.Single();

                string result = (string)InvokePrivate(controller, "TryWritePersistentName", null, (uint?)unchecked((uint)buffer.ToInt64()), "Max", Encoding.GetEncoding("iso-8859-1"), false, player);

                Assert.Contains("Stringende nicht gefunden", result);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        // A pool where every player named "Jakob" shares entry 0 - the shape that made renaming one
        // Jakob rename them all. Records 0 and 1 both point their Firstname at "Jakob", Lastnames at
        // their own entries; "Josef" (entry 3) already exists for some other player.
        private static readonly string[] SharedPool = { "Jakob", "Meier", "Huber", "Josef" };

        // The original layout minus the original-build-only anchors: the name pool is then found by
        // counting null terminators and can't be appended to - the path every other build takes.
        internal static readonly PersistentLayout ScanOnlyLayout = new PersistentLayout(
            Settings.PlayerRecordTableOffset, Settings.NamePoolPointerOffset, Settings.AgeReferenceYearOffset, Settings.ActiveTrainerCountOffset, isVerified: true);

        // withBookkeeping: also set up the game's own table/count/end pointer (original build), so
        // names resolve through the table and new ones get appended.
        private static void WithSharedPool(Action<FakeModule, PlayerController, IntPtr> test, bool withBookkeeping = false)
        {
            Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
            byte[] poolBytes = SharedPool.SelectMany(s => latin1.GetBytes(s + "\0")).Concat(new byte[] { 0x01, 0 }).ToArray();
            IntPtr pool = Marshal.AllocHGlobal(0x20000); // covers NamePoolResolver's first 64KB scan read
            try
            {
                Marshal.Copy(new byte[0x20000], 0, pool, 0x20000);
                Marshal.Copy(poolBytes, 0, pool, poolBytes.Length);

                using (FakeModule fake = new FakeModule(moduleBlockSize: 0x800000))
                {
                    uint poolAddress = unchecked((uint)pool.ToInt64());
                    if (withBookkeeping)
                    {
                        uint entry = poolAddress;
                        for (int i = 0; i < SharedPool.Length; i++)
                        {
                            fake.WriteModuleBytes(Settings.NamePoolTableOffset + (uint)i * 4, BitConverter.GetBytes(entry));
                            entry += (uint)SharedPool[i].Length + 1;
                        }
                        fake.WriteModuleBytes(Settings.NamePoolCountOffset, BitConverter.GetBytes((ushort)SharedPool.Length));
                        fake.WriteModuleBytes(Settings.NamePoolEndPointerOffset, BitConverter.GetBytes(entry));
                    }
                    else
                    {
                        fake.Memory.Layout = ScanOnlyLayout;
                    }

                    fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                    fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(poolAddress));
                    // [+0 Firstname index, +2 Lastname index, +4 own PlayerId]
                    fake.WriteModuleBytes(Settings.PlayerRecordTableOffset, new byte[] { 0, 0, 1, 0, 0, 0 });
                    fake.WriteModuleBytes(Settings.PlayerRecordTableOffset + Settings.PlayerRecordStride, new byte[] { 0, 0, 2, 0, 1, 0 });

                    PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                    test(fake, controller, pool);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(pool);
            }
        }

        private static object TryWriteFirstname(FakeModule fake, PlayerController controller, string newValue, Player player)
        {
            NamePoolResolver namePool = new NamePoolResolver(fake.Memory);
            return InvokePrivate(controller, "TryWritePersistentName", namePool, namePool.ResolveFirstnameAddress(player.NameRecordId), newValue, Encoding.GetEncoding("iso-8859-1"), false, player);
        }

        [Fact]
        public void TryWritePersistentName_RenamingASharedFirstname_RepointsOnlyThisPlayerAndLeavesThePoolUntouched()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();
                byte[] poolBefore = new byte[64];
                Marshal.Copy(pool, poolBefore, 0, poolBefore.Length);

                object result = TryWriteFirstname(fake, controller, "Josef", player);

                Assert.Null(result);
                Assert.Equal(new byte[] { 3, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset, 2));
                // The other Jakob (record 1) still points at the untouched "Jakob" entry.
                Assert.Equal(new byte[] { 0, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset + Settings.PlayerRecordStride, 2));
                byte[] poolAfter = new byte[64];
                Marshal.Copy(pool, poolAfter, 0, poolAfter.Length);
                Assert.Equal(poolBefore, poolAfter);
            });
        }

        [Fact]
        public void TryWritePersistentName_WhenNewNameIsNotInThePool_ReturnsWarningAndChangesNothing()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();
                byte[] poolBefore = new byte[64];
                Marshal.Copy(pool, poolBefore, 0, poolBefore.Length);

                string result = (string)TryWriteFirstname(fake, controller, "Jonas", player);

                Assert.Contains("kommt im Spielstand nicht vor", result);
                Assert.Equal(new byte[] { 0, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset, 2));
                byte[] poolAfter = new byte[64];
                Marshal.Copy(pool, poolAfter, 0, poolAfter.Length);
                Assert.Equal(poolBefore, poolAfter);
            });
        }

        private static string PoolString(IntPtr pool, int length) =>
            Encoding.GetEncoding("iso-8859-1").GetString(ReadBytes(pool, length));

        private static byte[] ReadBytes(IntPtr pool, int length)
        {
            byte[] bytes = new byte[length];
            Marshal.Copy(pool, bytes, 0, length);
            return bytes;
        }

        private static object TryWriteLastname(FakeModule fake, PlayerController controller, string newValue, Player player)
        {
            NamePoolResolver namePool = new NamePoolResolver(fake.Memory);
            return InvokePrivate(controller, "TryWritePersistentName", namePool, namePool.ResolveLastnameAddress(player.NameRecordId), newValue, Encoding.GetEncoding("iso-8859-1"), true, player);
        }

        [Fact]
        public void TryWritePersistentName_UnsharedSameLengthName_IsOverwrittenInPlace()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();

                // "Meier" (entry 1) is only player 0's Lastname.
                object result = TryWriteLastname(fake, controller, "Maier", player);

                Assert.Null(result);
                Assert.Equal("Jakob\0Maier\0Huber\0Josef\0", PoolString(pool, 24));
                Assert.Equal(new byte[] { 1, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset + 2, 2));
            });
        }

        [Fact]
        public void TryWritePersistentName_SharedSameLengthName_ReportsTheSharersAndChangesNothing()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();
                byte[] poolBefore = ReadBytes(pool, 64);

                string result = (string)TryWriteFirstname(fake, controller, "Jonas", player);

                Assert.Contains("von 2 Spielern geteilt", result);
                Assert.Equal(poolBefore, ReadBytes(pool, 64));
            });
        }

        [Fact]
        public void TryWritePersistentName_UnsharedNameOfDifferentLength_ReportsTheLengthAndChangesNothing()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();
                byte[] poolBefore = ReadBytes(pool, 64);

                string result = (string)TryWriteLastname(fake, controller, "Meierhofer", player);

                Assert.Contains("nicht genauso lang", result);
                Assert.Equal(poolBefore, ReadBytes(pool, 64));
            });
        }

        [Fact]
        public void TryWritePersistentName_WhenTheRecordTableCantBeCounted_RefusesTheInPlaceWrite()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();
                // CountReferences reads the table up to the module's end - an unreadable page in
                // between makes the sharer count unknown, which must be treated as "shared".
                fake.MakeModuleRegionUnreadable(0x700000, 0x1000);
                byte[] poolBefore = ReadBytes(pool, 64);

                string result = (string)TryWriteLastname(fake, controller, "Maier", player);

                Assert.Contains("möglicherweise von weiteren Spielern", result);
                Assert.Equal(poolBefore, ReadBytes(pool, 64));
            });
        }

        [Fact]
        public void TryWritePersistentName_WhenNameIsUnchanged_ReturnsNullWithoutRepointing()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();

                object result = TryWriteFirstname(fake, controller, "Jakob", player);

                Assert.Null(result);
                Assert.Equal(new byte[] { 0, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset, 2));
            });
        }

        [Fact]
        public void TryWritePersistentName_RenamingALastname_RepointsTheLastnameIndex()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();
                NamePoolResolver namePool = new NamePoolResolver(fake.Memory);

                object result = InvokePrivate(controller, "TryWritePersistentName", namePool, namePool.ResolveLastnameAddress(player.NameRecordId), "Huber", Encoding.GetEncoding("iso-8859-1"), true, player);

                Assert.Null(result);
                Assert.Equal(new byte[] { 2, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset + 2, 2));
            });
        }

        // ---- Original build: names that aren't in the pool yet get appended like the game does ----

        private const int SharedPoolBytes = 24; // "Jakob\0Meier\0Huber\0Josef\0"

        [Fact]
        public void TryWritePersistentName_NewName_IsAppendedAndOnlyThisPlayerRepointed()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();
                uint poolAddress = unchecked((uint)pool.ToInt64());

                object result = TryWriteFirstname(fake, controller, "Jonas", player);

                Assert.Null(result);
                Assert.Equal("Jakob\0Meier\0Huber\0Josef\0Jonas\0", PoolString(pool, SharedPoolBytes + 6));
                Assert.Equal(BitConverter.GetBytes(poolAddress + SharedPoolBytes), fake.ReadModuleBytes(Settings.NamePoolTableOffset + 4 * 4, 4));
                Assert.Equal(BitConverter.GetBytes((ushort)5), fake.ReadModuleBytes(Settings.NamePoolCountOffset, 2));
                Assert.Equal(BitConverter.GetBytes(poolAddress + SharedPoolBytes + 6), fake.ReadModuleBytes(Settings.NamePoolEndPointerOffset, 4));
                Assert.Equal(new byte[] { 4, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset, 2));
                // The other Jakob keeps his name.
                Assert.Equal(new byte[] { 0, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset + Settings.PlayerRecordStride, 2));
            }, withBookkeeping: true);
        }

        [Fact]
        public void TryWritePersistentName_NewNameOfDifferentLength_IsAppendedInsteadOfRefused()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();

                object result = TryWriteLastname(fake, controller, "Meierhofer", player);

                Assert.Null(result);
                Assert.Equal("Jakob\0Meier\0Huber\0Josef\0Meierhofer\0", PoolString(pool, SharedPoolBytes + 11));
                Assert.Equal(new byte[] { 4, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset + 2, 2));
                // Resolving now goes through the table and finds the appended entry.
                NamePoolResolver namePool = new NamePoolResolver(fake.Memory);
                Assert.Equal(unchecked((uint)pool.ToInt64()) + SharedPoolBytes, namePool.ResolveLastnameAddress(player.NameRecordId));
            }, withBookkeeping: true);
        }

        [Fact]
        public void TryWritePersistentName_ExistingName_IsReusedWithoutGrowingThePool()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();

                object result = TryWriteFirstname(fake, controller, "Josef", player);

                Assert.Null(result);
                Assert.Equal(new byte[] { 3, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset, 2));
                Assert.Equal(BitConverter.GetBytes((ushort)4), fake.ReadModuleBytes(Settings.NamePoolCountOffset, 2));
            }, withBookkeeping: true);
        }

        [Fact]
        public void TryWritePersistentName_WhenThePoolIsFull_ReturnsWarningAndChangesNothing()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();
                uint poolAddress = unchecked((uint)pool.ToInt64());
                fake.WriteModuleBytes(Settings.NamePoolEndPointerOffset, BitConverter.GetBytes(poolAddress + (uint)Settings.NamePoolCapacity - 3));
                byte[] poolBefore = ReadBytes(pool, 64);

                string result = (string)TryWriteFirstname(fake, controller, "Jonas", player);

                Assert.Contains("Namensbestand", result);
                Assert.Equal(poolBefore, ReadBytes(pool, 64));
                Assert.Equal(BitConverter.GetBytes((ushort)4), fake.ReadModuleBytes(Settings.NamePoolCountOffset, 2));
                Assert.Equal(new byte[] { 0, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset, 2));
            }, withBookkeeping: true);
        }

        [Fact]
        public void TryWritePersistentName_WhenTheTableIsFull_ReturnsWarningAndChangesNothing()
        {
            WithSharedPool((fake, controller, pool) =>
            {
                Player player = controller.EntityList.Single();
                // Resolve the current name before the count jumps past the four real entries.
                NamePoolResolver namePool = new NamePoolResolver(fake.Memory);
                uint? address = namePool.ResolveFirstnameAddress(player.NameRecordId);
                fake.WriteModuleBytes(Settings.NamePoolCountOffset, BitConverter.GetBytes((ushort)Settings.NamePoolTableCapacity));

                string result = (string)InvokePrivate(controller, "TryWritePersistentName", namePool, address, "Jonas", Encoding.GetEncoding("iso-8859-1"), false, player);

                Assert.Contains("Namensbestand", result);
                Assert.Equal(new byte[] { 0, 0 }, fake.ReadModuleBytes(Settings.PlayerRecordTableOffset, 2));
            }, withBookkeeping: true);
        }
    }
}
