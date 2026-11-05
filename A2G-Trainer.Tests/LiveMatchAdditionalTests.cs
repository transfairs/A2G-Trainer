using System;
using System.Linq;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>LiveMatch beyond LiveMatchTests: a home match, unreadable matches and a club without copy or record.</summary>
    public class LiveMatchAdditionalTests
    {
        private const PlayerEnums.Country Country = ClubEditorSettingsTests.Country;
        private const byte OwnId = ClubEditorSettingsTests.OwnId;
        // In the free end of the default module block, like LiveMatchTests.
        private const uint Table = 0x51A000;
        private const uint Clock = 0x51F000;

        private static LiveMatch Find(FakeModule fake, uint table = Table) =>
            LiveMatch.Find(fake.Memory, FixedAddresses.For(fake.Memory), Country, OwnId, fake.ModuleBase + table, fake.ModuleBase + Clock);

        [Fact]
        public void Find_TakesAHomeMatch_AndItsTacticsAreTheHomeTeams()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                LiveMatchTests.SeedMatch(fake);
                // Match 0: two foreign clubs with a human; match 1: the own club at home against club 2.
                fake.WriteModuleBytes(Table, new byte[] { 0xEE, 0, OwnId, 0, 0xEE, 0, OwnId, 0 });
                fake.WriteModuleBytes(Table + 0x240, new byte[] { 1, 0 });
                uint match = Table + Settings.MatchStride;
                fake.WriteModuleBytes(match, new byte[] { (byte)Country, 0, OwnId, 0, (byte)Country, 0, 2, 0 });
                fake.WriteModuleBytes(match + 0x84, new byte[] { 5, 1, 6, 0, 2 });

                LiveMatch live = Find(fake);

                Assert.True(live.IsHome);
                Assert.Equal(Country, live.Opponent.Key);
                Assert.Equal((ushort)2, live.Opponent.Value);
                Assert.Equal((byte)5, live.Attitude);
                Assert.Equal((byte)1, live.Hardness);
                Assert.Equal((byte)6, live.Effort);
                Assert.Equal((byte)2, live.Substitutions);

                live.Hardness = 3;
                Assert.Equal((byte)3, fake.ReadModuleBytes(match + 0x85, 1)[0]);
                // The computer must leave the home team's tactics alone now.
                Assert.Equal((byte)(2 | 4), fake.ReadModuleBytes(match + 0x240, 1)[0]);
            }
        }

        [Fact]
        public void Find_SkipsMatchesItCannotRead()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                LiveMatchTests.SeedMatch(fake);
                // Match 0 ends just before an unreadable page: its head is readable, its mask isn't;
                // match 1 lies wholly in that page.
                const uint table = 0x51C000 - 8;
                fake.WriteModuleBytes(table, new byte[] { (byte)Country, 0, OwnId, 0, 0, 0, 0, 0 });
                fake.MakeModuleRegionUnreadable(0x51C000, 1);

                Assert.Null(Find(fake, table));
            }
        }

        [Fact]
        public void Reads_FallBackToZero_OnceTheMatchIsGone()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                LiveMatchTests.SeedMatch(fake);
                LiveMatch live = Find(fake);
                Assert.Equal((short)63, live.Minute);

                fake.MakeModuleRegionUnreadable(Table, (int)Settings.MatchStride * 2);
                fake.MakeModuleRegionUnreadable(Clock, 0xA);

                Assert.Equal((short)0, live.Minute);
                Assert.Equal((short)0, live.HomeGoals);
                Assert.Equal((short)0, live.AwayGoals);
                Assert.Equal((PlayerEnums.Country)0, live.Opponent.Key);
                Assert.Equal((ushort)0, live.Opponent.Value);
                Assert.Equal((byte)0, live.Attitude);
                Assert.Equal((byte)0, live.Substitutions);
            }
        }

        [Fact]
        public void WithoutACopyOrRecord_TheBenchIsEmpty_AndWritingItReachesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake, withRecord: false);
                LiveMatchTests.SeedMatch(fake);
                fake.WriteModuleBytes(Settings.ClubCacheTableOffset, new byte[Settings.ClubCacheSlots * Settings.ClubCacheStride]);
                LiveMatch live = Find(fake);

                Assert.All(live.Bench, id => Assert.Equal((ushort)0, id));
                Assert.All(live.LineUp, id => Assert.Equal((ushort)0, id));

                live.WriteBench(new ushort[] { 130, 131 });

                Assert.All(live.Bench, id => Assert.Equal((ushort)0, id));
                Assert.Equal(new byte[14], fake.ReadModuleBytes(ClubEditorSettingsTests.Record(OwnId) + 0x15E, 14));
                // The old copy the cache no longer lists keeps its bench.
                Assert.Equal(new byte[] { 120, 0, 121, 0 }, fake.ReadModuleBytes(ClubEditorSettingsTests.OtherCopy + Settings.ClubBenchOffset, 4));
            }
        }
    }
}
