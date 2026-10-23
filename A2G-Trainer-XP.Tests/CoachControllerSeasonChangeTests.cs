using System;
using System.Linq;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for what CoachController does so a next-season club set in the Trainer actually goes
    /// through at the game's season change: the eligibility check that mirrors the game's own, and
    /// pre-filling an empty next-season contract.
    /// </summary>
    public class CoachControllerSeasonChangeTests
    {
        private const PlayerEnums.Country Main = PlayerEnums.Country.Deutschland;
        private const PlayerEnums.Country Bonus = PlayerEnums.Country.England;

        private static void WriteInt16s(FakeModule fake, uint offset, params short[] values) =>
            fake.WriteModuleBytes(offset, values.SelectMany(v => BitConverter.GetBytes(v)).ToArray());

        // Main country: league system 0 with leagues 0 (clubs 5, 9) and 1 (club 12).
        // Bonus country: league system 1 with leagues 2 (club 7) and 3 (club 8).
        private static void SeedLeagues(FakeModule fake)
        {
            fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + (uint)Main, new byte[] { 0 });
            fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + (uint)Bonus, new byte[] { 1 });
            WriteInt16s(fake, Settings.LeagueRangeTableOffset, 0, 2, 4);

            short[][] leagues = { new short[] { 5, 9 }, new short[] { 12 }, new short[] { 7 }, new short[] { 8 } };
            for (int league = 0; league < leagues.Length; league++)
            {
                uint at = Settings.LeagueTableOffset + (uint)league * Settings.LeagueStride;
                WriteInt16s(fake, at, (short)leagues[league].Length);
                WriteInt16s(fake, at + Settings.LeagueClubIdsOffset, leagues[league]);
            }

            WriteInt16s(fake, Settings.BonusCountryCountOffset, 1);
            WriteInt16s(fake, Settings.BonusCountryListOffset, (short)Bonus);
        }

        [Theory]
        [InlineData(Main, 5, true)]    // main country, top league
        [InlineData(Main, 12, true)]   // main country, second league - any league counts
        [InlineData(Main, 99, false)]  // not in any of its leagues
        [InlineData(Bonus, 7, true)]   // bonus country, top league
        [InlineData(Bonus, 8, false)]  // bonus country, second league - only the top league counts
        public void IsEligibleNextSeasonClub_MirrorsTheGamesSeasonChangeCheck(PlayerEnums.Country country, byte clubId, bool expected)
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);

                Assert.Equal(expected, CoachController.IsEligibleNextSeasonClub(fake.Memory, country, clubId));
            }
        }

        [Fact]
        public void IsEligibleNextSeasonClub_WithoutOriginalBuildAnchors_IsUnknown()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);

                Assert.Null(CoachController.IsEligibleNextSeasonClub(fake.Memory, Main, 5));
            }
        }

        [Fact]
        public void IsEligibleNextSeasonClub_WithUnreadableLeagueSystemTable_IsUnknown()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                fake.MakeModuleRegionUnreadable(Settings.LeagueSystemByCountryOffset, 1);

                Assert.Null(CoachController.IsEligibleNextSeasonClub(fake.Memory, Main, 5));
            }
        }

        // The league range comes from game data, so a corrupt one can walk the league table into
        // unreadable memory - that's "unknown", not "not eligible".
        [Fact]
        public void IsEligibleNextSeasonClub_WithALeagueRangeRunningIntoUnreadableMemory_IsUnknown()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                WriteInt16s(fake, Settings.LeagueRangeTableOffset, 0, short.MaxValue);
                fake.MakeModuleRegionUnreadable(Settings.LeagueTableOffset + 10 * Settings.LeagueStride, 2);

                Assert.Null(CoachController.IsEligibleNextSeasonClub(fake.Memory, Main, 99));
            }
        }

        [Fact]
        public void IsEligibleNextSeasonClub_WithAClubListRunningIntoUnreadableMemory_IsUnknown()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                // League 4 claims 0x2000 clubs; their IDs run on into a page that can't be read.
                uint league = Settings.LeagueTableOffset + 4 * Settings.LeagueStride;
                WriteInt16s(fake, Settings.LeagueRangeTableOffset, 4, 5);
                WriteInt16s(fake, league, 0x2000);
                fake.MakeModuleRegionUnreadable(league + 0x2000, 1);

                Assert.Null(CoachController.IsEligibleNextSeasonClub(fake.Memory, Main, 5));
            }
        }

        // Without a readable bonus-country list the country counts as the main country, where any
        // league is accepted.
        [Fact]
        public void IsEligibleNextSeasonClub_WithUnreadableBonusCountryCount_TreatsTheCountryAsMain()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                fake.MakeModuleRegionUnreadable(Settings.BonusCountryCountOffset, 2);

                Assert.True(CoachController.IsEligibleNextSeasonClub(fake.Memory, Bonus, 8));
            }
        }

        [Fact]
        public void IsEligibleNextSeasonClub_WithoutBonusCountries_AcceptsAnyLeague()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                WriteInt16s(fake, Settings.BonusCountryCountOffset, 0);

                Assert.True(CoachController.IsEligibleNextSeasonClub(fake.Memory, Bonus, 8));
            }
        }

        private static uint WorkingCopy(uint slotOffset) => Settings.CoachSlotTableOffset + 1 * Settings.CoachSlotStride + slotOffset;
        private static uint Record(uint slotOffset) => Settings.CoachRecordTableOffset + 1 * Settings.CoachRecordStride + slotOffset;

        private static readonly byte[] CurrentContract = Enumerable.Range(1, 0x20).Select(i => (byte)i).ToArray();

        [Fact]
        public void Save_WithNextSeasonClubAndEmptyNextContract_CopiesTheCurrentContract()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(WorkingCopy(0x48), CurrentContract);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                controller.Coach.CountryNextSeason = Main;
                controller.Coach.ClubIdNextSeason = 5;

                controller.Save();

                Assert.Equal(CurrentContract, fake.ReadModuleBytes(WorkingCopy(0x68), 0x20));
                Assert.Equal(CurrentContract, fake.ReadModuleBytes(Record(0x68), 0x20));
            }
        }

        [Fact]
        public void Save_WithAlreadyNegotiatedNextContract_LeavesItAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                byte[] negotiated = Enumerable.Repeat((byte)0x77, 0x20).ToArray();
                fake.WriteModuleBytes(WorkingCopy(0x48), CurrentContract);
                fake.WriteModuleBytes(WorkingCopy(0x68), negotiated);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                controller.Coach.CountryNextSeason = Main;

                controller.Save();

                Assert.Equal(negotiated, fake.ReadModuleBytes(WorkingCopy(0x68), 0x20));
            }
        }

        [Fact]
        public void EnsureCanNegotiateOffers_WithLongContractAndNoRelease_SetsAndWritesOnlyTheClause()
        {
            using (FakeModule fake = new FakeModule())
            {
                string years = AddressPresets.COACH[CoachEnums.AddressKey.CONTRACT_DURATION];
                string clause = AddressPresets.COACH[CoachEnums.AddressKey.RELEASE_CLAUSE];
                CoachMemory.WriteWorkingCopy(fake, years, BitConverter.GetBytes((ushort)3));
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                // An unsaved edit in the form must not be written along with the clause.
                controller.Coach.Wealth = 999;

                Assert.Equal((ushort)3, controller.Coach.ContractYearsLeft);
                Assert.True(controller.EnsureCanNegotiateOffers());

                Assert.Equal((byte)0x01, CoachMemory.ReadWorkingCopy(fake, clause, 1)[0]);
                Assert.Equal((byte)0x01, CoachMemory.ReadRecord(fake, clause, 1)[0]);
                Assert.Equal(0, BitConverter.ToInt32(CoachMemory.ReadWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.WEALTH], 4), 0));
            }
        }

        [Theory]
        [InlineData((ushort)1, (byte)0)]   // contract running out
        [InlineData((ushort)3, (byte)1)]   // board release
        public void EnsureCanNegotiateOffers_WhenTheGameAlreadyAllowsIt_ChangesNothing(ushort yearsLeft, byte boardRelease)
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.CONTRACT_DURATION], BitConverter.GetBytes(yearsLeft));
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.BOARD_RELEASE], new byte[] { boardRelease });
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

                Assert.False(controller.EnsureCanNegotiateOffers());
                Assert.Equal((byte)0, CoachMemory.ReadWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.RELEASE_CLAUSE], 1)[0]);
            }
        }

        [Fact]
        public void Save_WithUnreadableNextContract_DoesNotCopyAnything()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(WorkingCopy(0x48), CurrentContract);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                controller.Coach.CountryNextSeason = Main;
                fake.MakeModuleRegionUnreadable(WorkingCopy(0x68), 0x20);

                controller.Save();

                Assert.Equal(new byte[0x20], fake.ReadModuleBytes(Record(0x68), 0x20));
            }
        }

        [Fact]
        public void Save_WithoutNextSeasonClub_LeavesTheNextContractEmpty()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(WorkingCopy(0x48), CurrentContract);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

                controller.Save();

                Assert.Equal(new byte[0x20], fake.ReadModuleBytes(WorkingCopy(0x68), 0x20));
            }
        }
    }
}
