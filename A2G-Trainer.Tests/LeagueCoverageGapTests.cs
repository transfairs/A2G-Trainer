using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// The league tables and the European cup entries where the game's memory can't be read: the
    /// page holding a table, counter or title is made unreadable after seeding, and the readers
    /// must skip it instead of failing.
    /// </summary>
    public class LeagueCoverageGapTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        [Fact]
        public void Countries_WithTheCountryCountersUnreadable_AreTheDefaults()
        {
            using (FakeModule fake = new FakeModule())
            {
                LeagueTablesTests.SeedLeagues(fake);
                fake.MakeModuleRegionUnreadable(Settings.MainCountryOffset, 2);

                PlayerEnums.Country main = LeagueTables.Countries(fake.Memory, FixedAddresses.For(fake.Memory), out List<PlayerEnums.Country> bonus);

                Assert.Equal((PlayerEnums.Country)0, main);
                Assert.Empty(bonus);
            }
        }

        [Fact]
        public void ReadAll_WithTheLeagueRangesUnreadable_IsEmpty()
        {
            using (FakeModule fake = new FakeModule())
            {
                LeagueTablesTests.SeedLeagues(fake);
                fake.MakeModuleRegionUnreadable(Settings.LeagueRangeTableOffset, 12);

                Assert.Empty(LeagueTables.ReadAll(fake.Memory, FixedAddresses.For(fake.Memory)));
            }
        }

        [Fact]
        public void Read_WithTheHeaderOrGridUnreadableOrANegativeCount_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                LeagueTablesTests.SeedLeagues(fake);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                uint league0 = Settings.LeagueTableOffset;
                uint league1 = league0 + Settings.LeagueStride;
                uint league3 = league1 + 2 * Settings.LeagueStride;
                // League 0's header ends on the page before its grid; league 1 starts on the grid's page, league 3 (count -1) on a readable one.
                Assert.NotEqual(league0 / 0x1000, (league0 + Settings.LeagueGridOffset) / 0x1000);
                Assert.Equal((league0 + Settings.LeagueGridOffset) / 0x1000, league1 / 0x1000);
                Assert.NotEqual(league1 / 0x1000, league3 / 0x1000);
                Assert.Equal(league3 / 0x1000, (league3 + Settings.LeagueGridOffset) / 0x1000);
                fake.WriteModuleBytes(league3, BitConverter.GetBytes((short)-1));
                Assert.NotNull(LeagueTables.Read(fake.Memory, fixedAddresses, 0));

                fake.MakeModuleRegionUnreadable(league0 + Settings.LeagueGridOffset, 1);

                Assert.Null(LeagueTables.Read(fake.Memory, fixedAddresses, 0));
                Assert.Null(LeagueTables.Read(fake.Memory, fixedAddresses, 1));
                Assert.Null(LeagueTables.Read(fake.Memory, fixedAddresses, 3));
            }
        }

        [Fact]
        public void ReadLicence_WithAnUnreadableWorkingCopy_ReadsTheRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                LeagueTablesTests.SeedLeagues(fake);
                byte[] slot = new byte[Settings.ClubCacheStride];
                slot[0] = 1;
                slot[1] = (byte)PlayerEnums.Country.England;
                slot[2] = 4;
                Array.Copy(BitConverter.GetBytes(1u), 0, slot, Settings.ClubCachePointerOffset, 4);
                fake.WriteModuleBytes(Settings.ClubCacheTableOffset, slot);
                fake.WriteModuleBytes(Settings.ClubRecordTableOffset + 14u * Settings.ClubRecordStride + 0xF5, new byte[] { 0x03 });

                Assert.Equal((byte)0x03, LeagueTables.ReadLicence(fake.Memory, FixedAddresses.For(fake.Memory), PlayerEnums.Country.England, 4));
            }
        }

        [Fact]
        public void EuropeanCountries_WithTheCountsUnreadable_AreEmpty()
        {
            using (FakeModule fake = new FakeModule())
            {
                LeagueTablesTests.SeedEuropeanCountries(fake);
                fake.MakeModuleRegionUnreadable(Settings.EuropeanClubCountOffset, 76);

                Assert.Empty(EuropeanCupEntries.Countries(fake.Memory, FixedAddresses.For(fake.Memory), fake.ModuleBase + LeagueTablesTests.EuropeanRecords));
            }
        }

        [Fact]
        public void EuropeanCountries_WithTheFirstIndicesUnreadable_AreEmpty()
        {
            using (FakeModule fake = new FakeModule())
            {
                LeagueTablesTests.SeedEuropeanCountries(fake);
                fake.MakeModuleRegionUnreadable(Settings.EuropeanFirstClubOffset, 76);

                Assert.Empty(EuropeanCupEntries.Countries(fake.Memory, FixedAddresses.For(fake.Memory), fake.ModuleBase + LeagueTablesTests.EuropeanRecords));
            }
        }

        [Fact]
        public void EuropeanCountries_WithAnUnreadableNameOrCountry_LeaveTheTeamOut()
        {
            using (FakeModule fake = new FakeModule())
            {
                byte[] counts = new byte[76];
                counts[(int)PlayerEnums.Country.Holland] = 1;
                fake.WriteModuleBytes(Settings.EuropeanClubCountOffset, counts);
                byte[] firsts = new byte[76];
                firsts[(int)PlayerEnums.Country.Holland] = 1;
                fake.WriteModuleBytes(Settings.EuropeanFirstClubOffset, firsts);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);

                // The name lies below the lowest valid address.
                Assert.Empty(EuropeanCupEntries.Countries(fake.Memory, fixedAddresses, 0x100));

                // The name ends on a readable page, the owning country lies on the next, unreadable one.
                const uint record = 0x51DFC0;
                fake.WriteModuleBytes(record, Latin1.GetBytes("Ajax\0"));
                fake.WriteModuleBytes(record + Settings.EuropeanClubCountryOffset, new[] { (byte)PlayerEnums.Country.Holland });
                uint records = fake.ModuleBase + record - Settings.EuropeanClubStride;
                Assert.Equal(new[] { "Ajax" }, EuropeanCupEntries.Countries(fake.Memory, fixedAddresses, records)[PlayerEnums.Country.Holland].Select(t => t.Name));
                fake.MakeModuleRegionUnreadable(record + Settings.EuropeanClubCountryOffset, 1);

                Assert.Empty(EuropeanCupEntries.Countries(fake.Memory, fixedAddresses, records));
            }
        }

        [Fact]
        public void Assign_WithTheTitleUnreadable_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                LeagueTablesTests.SeedEuropeanCountries(fake);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                fake.MakeModuleRegionUnreadable(Settings.CupChampionOffset + (uint)PlayerEnums.Country.Holland, 1);

                Assert.True(EuropeanCupEntries.CanSwap(fake.Memory, fixedAddresses));
                Assert.Equal(-1, EuropeanCupEntries.Assign(fake.Memory, fixedAddresses, PlayerEnums.Country.Holland, EuropeanCupEntries.Title.Champion, 0));
            }
        }

        [Fact]
        public void Swap_WithThePotsTitlesAndCountsUnreadable_ChangesOnlyWhatItCanRead()
        {
            using (FakeModule fake = new FakeModule())
            {
                LeagueTablesTests.SeedEuropeanCountries(fake);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                byte h = (byte)PlayerEnums.Country.Holland;
                // The pots, UI pairings, runner-up and further teams share one page; the team counts lie on another.
                fake.MakeModuleRegionUnreadable(Settings.UiCupPairingsOffset, 1);
                fake.MakeModuleRegionUnreadable(Settings.EuropeanClubCountOffset, 76);

                // Only the champion (4 -> 0) and the cup winner tables (on their own pages) are left.
                Assert.Equal(1, EuropeanCupEntries.Swap(fake.Memory, fixedAddresses, PlayerEnums.Country.Holland, 4, 0));
                Assert.Equal(new byte[] { 0 }, fake.ReadModuleBytes(Settings.CupChampionOffset + h, 1));
            }
        }
    }
}
