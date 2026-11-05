using System;
using System.Linq;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// ClubSquads and ClubRecords with parts of the game's tables unreadable, and the squad read
    /// from the record although the club is fetched.
    /// </summary>
    public class ClubSquadsAdditionalTests
    {
        private const PlayerEnums.Country Country = PlayerEnums.Country.Deutschland;
        private static FixedAddresses Original => FixedAddresses.Original;

        private static uint Record(int index) => Settings.ClubRecordTableOffset + (uint)index * Settings.ClubRecordStride;

        private static ClubSquads.Entry Club(int index, byte id) => new ClubSquads.Entry { Index = index, Country = Country, Id = id, Name = "Eintracht" };

        private static void WriteRecordSquad(FakeModule fake, int index, params ushort[] squad)
        {
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordSquadCountOffset, BitConverter.GetBytes((short)squad.Length));
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordSquadOffset, squad.SelectMany(BitConverter.GetBytes).ToArray());
        }

        // Cache slot 1 holds a working copy of the club at module offset copy.
        private static void CacheClub(FakeModule fake, byte id, uint copy)
        {
            byte[] slot = new byte[Settings.ClubCacheStride];
            slot[0] = 1;
            slot[1] = (byte)Country;
            slot[2] = id;
            BitConverter.GetBytes(fake.ModuleBase + copy).CopyTo(slot, (int)Settings.ClubCachePointerOffset);
            fake.WriteModuleBytes(Settings.ClubCacheTableOffset + Settings.ClubCacheStride, slot);
        }

        [Fact]
        public void SquadIds_FromRecord_IgnoresTheWorkingCopy()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteRecordSquad(fake, 1, 20, 21);
                const uint copy = 0x100000;
                uint player = fake.ModuleBase + 0x101000;
                fake.WriteModuleBytes(0x101000, BitConverter.GetBytes((ushort)25));
                fake.WriteModuleBytes(copy + Settings.ClubSquadCountOffset, BitConverter.GetBytes((short)1));
                fake.WriteModuleBytes(copy + Settings.ClubSquadOffset, BitConverter.GetBytes(player));
                CacheClub(fake, 1, copy);

                Assert.Equal(new ushort[] { 25 }, ClubSquads.SquadIds(fake.Memory, Original, Club(1, 1)));
                Assert.Equal(new ushort[] { 20, 21 }, ClubSquads.SquadIds(fake.Memory, Original, Club(1, 1), fromRecord: true));
            }
        }

        [Fact]
        public void SquadIds_OfAnUnreadableWorkingCopy_IsEmpty()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteRecordSquad(fake, 1, 20, 21);
                const uint copy = 0x51E000;
                CacheClub(fake, 1, copy);
                fake.MakeModuleRegionUnreadable(copy, 0x1000);

                Assert.Empty(ClubSquads.SquadIds(fake.Memory, Original, Club(1, 1)));
                Assert.Equal(new ushort[] { 20, 21 }, ClubSquads.SquadIds(fake.Memory, Original, Club(1, 1), fromRecord: true));
            }
        }

        [Fact]
        public void Youth_WithAnUnreadableYouthTable_IsEmptyAndNotFound()
        {
            using (FakeModule fake = new FakeModule())
            {
                byte[] entry = new byte[Settings.YouthStride];
                entry[1] = 1;
                BitConverter.GetBytes((ushort)33).CopyTo(entry, (int)Settings.YouthIdOffsets[0]);
                fake.WriteModuleBytes(Settings.YouthTableOffset, entry);
                Assert.Equal(new ushort[] { 33 }, ClubSquads.YouthIds(fake.Memory, Original, Club(0, 0)));

                fake.MakeModuleRegionUnreadable(Settings.YouthTableOffset, (int)Settings.YouthStride);

                Assert.Empty(ClubSquads.YouthIds(fake.Memory, Original, Club(0, 0)));
                Assert.Equal(ClubSquads.YouthMove.NotFound,
                    ClubSquads.MoveYouthToAgeGroup(fake.Memory, Original, Club(0, 0), 33, 18, true, out int from, out int to));
                Assert.Equal(0, from);
                Assert.Equal(0, to);
            }
        }

        [Fact]
        public void PlayerWorkingCopies_WithAnUnreadablePlayerCache_AreNone()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(Settings.PlayerCacheTableOffset, new byte[] { 1, 1, 0, 0x80 });
                fake.WriteModuleBytes(Settings.PlayerCacheTableOffset + Settings.PlayerCacheCopyOffset, BitConverter.GetBytes((ushort)7));
                Assert.NotNull(ClubSquads.PlayerWorkingCopy(fake.Memory, Original, 7));

                fake.MakeModuleRegionUnreadable(Settings.PlayerCacheTableOffset, 1);

                Assert.Empty(ClubSquads.PlayerWorkingCopies(fake.Memory, Original));
                Assert.Null(ClubSquads.PlayerWorkingCopy(fake.Memory, Original, 7));
            }
        }

        [Fact]
        public void IsListed_WithAnUnreadableWorkingCopy_FallsBackToTheRecord_AndWithItUnreadableIsUnknown()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                // Club 2's working copy (cache slot 4) says not listed, its record says listed.
                fake.WriteModuleBytes(ClubEditorSettingsTests.Record(2) + Settings.ClubRecordListedOffset, new byte[] { 1 });
                Assert.False(ClubRecords.IsListed(fake.Memory, Original, ClubEditorSettingsTests.Country, 2));

                fake.MakeModuleRegionUnreadable(ClubEditorSettingsTests.OtherCopy + 0x2000 + Settings.ClubListedOffset, 1);
                Assert.True(ClubRecords.IsListed(fake.Memory, Original, ClubEditorSettingsTests.Country, 2));

                fake.MakeModuleRegionUnreadable(ClubEditorSettingsTests.Record(2) + Settings.ClubRecordListedOffset, 1);
                Assert.Null(ClubRecords.IsListed(fake.Memory, Original, ClubEditorSettingsTests.Country, 2));
            }
        }
    }
}
