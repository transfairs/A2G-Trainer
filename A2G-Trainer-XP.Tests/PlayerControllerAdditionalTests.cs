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

            object result = InvokePrivate(controller, "TryWritePersistentName", (uint?)0x20000u, null, Encoding.GetEncoding("iso-8859-1"), "Vorname", player);

            Assert.Null(result);
        }

        [Fact]
        public void TryWritePersistentName_WhenAddressUnresolved_ReturnsAddressWarning()
        {
            PlayerController controller = NewMinimalController();
            Player player = controller.EntityList.Single();

            string result = (string)InvokePrivate(controller, "TryWritePersistentName", (uint?)null, "Max", Encoding.GetEncoding("iso-8859-1"), "Vorname", player);

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

                string result = (string)InvokePrivate(controller, "TryWritePersistentName", (uint?)unchecked((uint)page.ToInt64()), "Max", Encoding.GetEncoding("iso-8859-1"), "Vorname", player);

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

                string result = (string)InvokePrivate(controller, "TryWritePersistentName", (uint?)unchecked((uint)buffer.ToInt64()), "Max", Encoding.GetEncoding("iso-8859-1"), "Vorname", player);

                Assert.Contains("Stringende nicht gefunden", result);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [Fact]
        public void TryWritePersistentName_WhenReplacementLengthDiffers_ReturnsLengthWarningAndDoesNotWrite()
        {
            IntPtr buffer = Marshal.AllocHGlobal(16);
            try
            {
                byte[] current = Encoding.GetEncoding("iso-8859-1").GetBytes("AB\0");
                Marshal.Copy(current, 0, buffer, current.Length);

                PlayerController controller = NewMinimalController();
                Player player = controller.EntityList.Single();

                string result = (string)InvokePrivate(controller, "TryWritePersistentName", (uint?)unchecked((uint)buffer.ToInt64()), "LongerName", Encoding.GetEncoding("iso-8859-1"), "Vorname", player);

                Assert.Contains("Länge falsch", result);
                byte[] stillOriginal = new byte[3];
                Marshal.Copy(buffer, stillOriginal, 0, 3);
                Assert.Equal(current, stillOriginal);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [Fact]
        public void TryWritePersistentName_WhenReplacementLengthMatches_WritesTheNewValueAndReturnsNull()
        {
            IntPtr buffer = Marshal.AllocHGlobal(16);
            try
            {
                byte[] current = Encoding.GetEncoding("iso-8859-1").GetBytes("AB\0");
                Marshal.Copy(current, 0, buffer, current.Length);

                PlayerController controller = NewMinimalController();
                Player player = controller.EntityList.Single();

                object result = InvokePrivate(controller, "TryWritePersistentName", (uint?)unchecked((uint)buffer.ToInt64()), "CD", Encoding.GetEncoding("iso-8859-1"), "Vorname", player);

                Assert.Null(result);
                byte[] written = new byte[3];
                Marshal.Copy(buffer, written, 0, 3);
                Assert.Equal(Encoding.GetEncoding("iso-8859-1").GetBytes("CD\0"), written);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}
