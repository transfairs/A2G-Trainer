using System;
using System.Linq;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for taking a player off the game's global transfer list (TransferList, FUN_0062c0f0) and
    /// for PlayerController.Save doing it when the box is unticked.
    /// </summary>
    public class TransferListTests
    {
        // The list lies past the default FakeModule; a runtime shift of -0x400000 moves it (and the
        // season day at +42666C) inside.
        private const uint ListAt = 0x4A2280;
        private static readonly FixedAddresses Shifted = new FixedAddresses(unchecked(ListAt - Settings.TransferListOffset), 0, null, null);

        private static uint Entry(int index) => ListAt + Settings.TransferListEntriesOffset + (uint)index * Settings.TransferListEntryStride;

        private static void SeedList(FakeModule fake, params ushort[] ids)
        {
            fake.WriteModuleBytes(ListAt, BitConverter.GetBytes((short)ids.Length));
            for (int i = 0; i < ids.Length; i++)
            {
                byte[] entry = new byte[Settings.TransferListEntryStride];
                Array.Copy(BitConverter.GetBytes(ids[i]), entry, 2);
                entry[0x0B] = (byte)(i + 1);
                fake.WriteModuleBytes(Entry(i), entry);
            }
        }

        private static ushort IdAt(FakeModule fake, int index) => BitConverter.ToUInt16(fake.ReadModuleBytes(Entry(index), 2), 0);

        [Fact]
        public void Remove_MovesTheEntriesBehindUpAndDropsTheCount()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedList(fake, 11, 22, 33);

                Assert.True(TransferList.Remove(fake.Memory, Shifted, 22));

                Assert.Equal((short)2, BitConverter.ToInt16(fake.ReadModuleBytes(ListAt, 2), 0));
                Assert.Equal((ushort)11, IdAt(fake, 0));
                Assert.Equal((ushort)33, IdAt(fake, 1));
                // The whole entry moved, not only the ID.
                Assert.Equal((byte)3, fake.ReadModuleBytes(Entry(1) + 0x0B, 1)[0]);
            }
        }

        [Fact]
        public void Remove_TheLastEntry_OnlyDropsTheCount()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedList(fake, 11, 22);

                Assert.True(TransferList.Remove(fake.Memory, Shifted, 22));

                Assert.Equal((short)1, BitConverter.ToInt16(fake.ReadModuleBytes(ListAt, 2), 0));
                Assert.Equal((ushort)11, IdAt(fake, 0));
            }
        }

        [Fact]
        public void Remove_WithoutTheEntryOrAPlausibleList_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedList(fake, 11);
                Assert.False(TransferList.Remove(fake.Memory, Shifted, 99));
                Assert.Equal((short)1, BitConverter.ToInt16(fake.ReadModuleBytes(ListAt, 2), 0));

                Assert.False(TransferList.Remove(fake.Memory, null, 11));

                fake.WriteModuleBytes(ListAt, BitConverter.GetBytes((short)(Settings.TransferListCapacity + 1)));
                Assert.False(TransferList.Remove(fake.Memory, Shifted, 11));
            }
        }

        [Fact]
        public void Add_AppendsAnEntryLikeTheManagersOwnListing()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedList(fake, 11);
                fake.WriteModuleBytes(unchecked(Settings.GlobalCounterOffset + ListAt - Settings.TransferListOffset), BitConverter.GetBytes((short)123));
                Player player = new Player { NameRecordId = 4711, ClubCountry = PlayerEnums.Country.England, ClubId = 9, Position = PlayerEnums.Position.MD, Level = 5 };

                Assert.True(TransferList.Add(fake.Memory, Shifted, player));

                Assert.Equal((short)2, BitConverter.ToInt16(fake.ReadModuleBytes(ListAt, 2), 0));
                byte[] entry = fake.ReadModuleBytes(Entry(1), (int)Settings.TransferListEntryStride);
                Assert.Equal((ushort)4711, BitConverter.ToUInt16(entry, 0));
                Assert.Equal((byte)PlayerEnums.Country.England, entry[2]);
                Assert.Equal((ushort)9, BitConverter.ToUInt16(entry, 4));
                Assert.Equal((byte)PlayerEnums.Position.MD, entry[6]);
                Assert.Equal((byte)5, entry[7]);
                Assert.Equal((short)123, BitConverter.ToInt16(entry, 8));
                Assert.Equal(new byte[] { 1, 1 }, new[] { entry[0xA], entry[0xB] });

                // Already on it, a generated filler player, no fixed addresses: refused.
                Assert.False(TransferList.Add(fake.Memory, Shifted, player));
                Assert.False(TransferList.Add(fake.Memory, Shifted, new Player { NameRecordId = 15001 }));
                Assert.False(TransferList.Add(fake.Memory, null, new Player { NameRecordId = 5 }));
                // Full.
                fake.WriteModuleBytes(ListAt, BitConverter.GetBytes((short)Settings.TransferListCapacity));
                Assert.False(TransferList.Add(fake.Memory, Shifted, new Player { NameRecordId = 5 }));
            }
        }

        [Fact]
        public void IsTransferListed_TogglesCareerBitFour_AndNationalTeamRetiredBitThree()
        {
            Player player = new Player { Career = PlayerEnums.Career.Retires };

            player.IsTransferListed = true;
            player.IsNationalTeamRetired = true;
            Assert.Equal((PlayerEnums.Career)0x98, player.Career);

            player.IsTransferListed = false;
            player.IsNationalTeamRetired = false;
            Assert.Equal(PlayerEnums.Career.Retires, player.Career);
        }

        [Fact]
        public void Save_WhenTheListCantTakeThePlayer_UnticksTheBox_AndUntickingClearsTheBit()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                // Without known fixed addresses there is no list to read: the transfer list lies past
                // this FakeModule, and reading there would hit whatever the test process has mapped.
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                PlayerController controller = new PlayerController(fake.Memory, new Club { PlayerCount = 1, AmateurPlayerCount = 0 }, isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();
                Assert.False(player.WasTransferListed);

                // Ticked, but the list can't be reached: unticked again.
                player.IsTransferListed = true;
                controller.Save(player);
                Assert.False(player.IsTransferListed);

                // Listed when read and unticked: the career bit is cleared (the list itself is covered
                // by the tests above).
                player.Career = PlayerEnums.Career.TransferListed | PlayerEnums.Career.Retires;
                player.WasTransferListed = true;
                player.IsTransferListed = false;
                controller.Save(player);
                Assert.False(player.WasTransferListed);
                Assert.Equal((byte)PlayerEnums.Career.Retires, fake.ReadDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.CAREER], 1)[0]);
            }
        }
    }
}
