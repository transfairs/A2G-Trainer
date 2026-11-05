using System;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>TransferList with a list, entries or season day that can't be read, and implausible counts.</summary>
    public class TransferListAdditionalTests
    {
        // As in TransferListTests: a runtime shift of -0x400000 moves the list (and the season day at
        // +2666C) inside the default FakeModule.
        private const uint ListAt = 0x4A2280;
        private const uint DayAt = 0x2666C;
        private static readonly FixedAddresses Shifted = new FixedAddresses(unchecked(ListAt - Settings.TransferListOffset), 0, null, null);

        private static void WriteCount(FakeModule fake, short count) => fake.WriteModuleBytes(ListAt, BitConverter.GetBytes(count));

        private static short Count(FakeModule fake) => BitConverter.ToInt16(fake.ReadModuleBytes(ListAt, 2), 0);

        private static Player Player(ushort id) => new Player { NameRecordId = id, ClubCountry = PlayerEnums.Country.England, ClubId = 3 };

        [Fact]
        public void Add_WithAnUnreadableList_Refuses()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.MakeModuleRegionUnreadable(ListAt, 2);

                Assert.False(TransferList.Add(fake.Memory, Shifted, Player(5)));
                Assert.False(TransferList.Remove(fake.Memory, Shifted, 5));
            }
        }

        [Fact]
        public void Add_WithAnUnreadableSeasonDay_Refuses()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteCount(fake, 0);
                fake.MakeModuleRegionUnreadable(DayAt, 2);

                Assert.False(TransferList.Add(fake.Memory, Shifted, Player(5)));
                Assert.Equal(0, Count(fake));
            }
        }

        [Fact]
        public void Add_ToAnEmptyList_WritesTheFirstEntry_AndANegativeCountIsRefused()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteCount(fake, 0);
                Assert.True(TransferList.Add(fake.Memory, Shifted, Player(5)));
                Assert.Equal(1, Count(fake));
                Assert.Equal((ushort)5, BitConverter.ToUInt16(fake.ReadModuleBytes(ListAt + Settings.TransferListEntriesOffset, 2), 0));

                WriteCount(fake, -1);
                Assert.False(TransferList.Add(fake.Memory, Shifted, Player(6)));
                Assert.False(TransferList.Remove(fake.Memory, Shifted, 5));
                Assert.Equal(-1, Count(fake));
            }
        }

        [Fact]
        public void AddAndRemove_WithEntriesRunningIntoUnreadableMemory_Refuse()
        {
            using (FakeModule fake = new FakeModule())
            {
                // 200 entries reach past the list's page into the next one.
                WriteCount(fake, 200);
                uint nextPage = (ListAt & ~0xFFFu) + 0x1000;
                Assert.True(ListAt + Settings.TransferListEntriesOffset + 200 * Settings.TransferListEntryStride > nextPage);
                fake.MakeModuleRegionUnreadable(nextPage, 1);

                Assert.False(TransferList.Add(fake.Memory, Shifted, Player(5)));
                Assert.False(TransferList.Remove(fake.Memory, Shifted, 0));
                Assert.Equal(200, Count(fake));
            }
        }
    }
}
