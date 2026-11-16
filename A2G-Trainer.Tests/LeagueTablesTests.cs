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
    /// The league tables: computed from the result grid like the game does, results written for a
    /// chosen order, the licence byte of any league club, the season phase, and the champion and cup
    /// winner of the European countries without league table.
    /// </summary>
    public class LeagueTablesTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        // Where the tests put the European clubs' records: the free end of the default fake module.
        internal const uint EuropeanRecords = 0x51C000;

        private static LeagueTable Table(int clubs, LeagueMode mode = LeagueMode.Simulated)
        {
            LeagueTable table = new LeagueTable
            {
                Mode = mode,
                ClubIds = Enumerable.Range(0, clubs).Select(i => (short)i).ToArray(),
                ClubNames = Enumerable.Range(0, clubs).Select(i => "Verein " + i).ToArray(),
                Licences = new byte[clubs],
                Grid = Enumerable.Repeat(LeagueTable.NotPlayed, LeagueTable.GridSize * LeagueTable.GridSize * 2).ToArray(),
            };
            return table;
        }

        private static void Result(LeagueTable table, int home, int away, int homeGoals, int awayGoals) =>
            table.Grid[LeagueTable.Cell(home, away)] = (byte)(homeGoals << 4 | awayGoals);

        [Fact]
        public void Standings_CountLikeTheGame()
        {
            LeagueTable table = Table(3);
            Result(table, 0, 1, 2, 1);   // 0 wins at home
            Result(table, 1, 0, 1, 1);   // draw
            Result(table, 2, 0, 0, 3);   // 0 wins away
            Result(table, 1, 2, 4, 0);

            List<LeagueTableRow> rows = table.Standings();

            Assert.Equal(new[] { 0, 1, 2 }, rows.Select(r => r.ListPosition));
            LeagueTableRow first = rows[0];
            Assert.Equal(3, first.Games);
            Assert.Equal(7, first.Points);
            Assert.Equal(2, first.Wins);
            Assert.Equal(1, first.Draws);
            Assert.Equal(0, first.Losses);
            Assert.Equal(6, first.GoalsFor);
            Assert.Equal(2, first.GoalsAgainst);
            Assert.Equal(4, first.GoalDifference);
            Assert.Equal("Verein 0", first.Name);
        }

        [Fact]
        public void Standings_DeductPointsAndPutAClubWithoutLicenceLast()
        {
            LeagueTable table = Table(3);
            Result(table, 0, 1, 1, 0);
            Result(table, 0, 2, 1, 0);
            Result(table, 1, 2, 1, 0);
            table.Licences[0] = 0x80;         // refused: last despite 6 points
            table.Licences[1] = 0x01;         // 3 points off its 3: 0
            table.Licences[2] = 0x03;         // 9 off nothing stays 0

            List<LeagueTableRow> rows = table.Standings();

            Assert.Equal(new[] { 1, 2, 0 }, rows.Select(r => r.ListPosition));
            Assert.Equal(new[] { 0, 0, 6 }, rows.Select(r => r.Points));
            Assert.Equal(3, rows[0].Deduction);
            Assert.True(rows[2].LicenceRefused);
        }

        [Fact]
        public void Standings_BreakTiesByGoalDifferenceThenGoals()
        {
            LeagueTable table = Table(3);
            Result(table, 0, 2, 1, 0);
            Result(table, 1, 2, 3, 2);
            Result(table, 2, 0, 2, 1);
            Result(table, 2, 1, 1, 0);
            // 0: 3 pts, 2:2; 1: 3 pts, 3:3; 2: 6 pts.
            Assert.Equal(new[] { 2, 1, 0 }, table.Standings().Select(r => r.ListPosition));
        }

        [Fact]
        public void Standings_WithoutResults_KeepTheListOrder()
        {
            LeagueTable table = Table(3, LeagueMode.NoResults);
            table.Licences[0] = 0x80;
            Assert.Equal(new[] { 0, 1, 2 }, table.Standings().Select(r => r.ListPosition));
        }

        [Fact]
        public void Standings_WithoutNamesOrLicences_UseDefaults()
        {
            LeagueTable table = new LeagueTable { ClubIds = new short[] { 7 } };
            LeagueTableRow row = Assert.Single(table.Standings());
            Assert.Equal("", row.Name);
            Assert.Equal(0, row.Licence);
            Assert.Equal(0, table.Index);
        }

        [Fact]
        public void SetResults_PutsTheTableInTheChosenOrderThreePointsApart()
        {
            LeagueTable table = Table(24);
            int[] order = Enumerable.Range(0, 24).Reverse().ToArray();

            table.SetResults(order);

            List<LeagueTableRow> rows = table.Standings();
            Assert.Equal(order, rows.Select(r => r.ListPosition));
            for (int rank = 1; rank < rows.Count; rank++)
                Assert.Equal(3, rows[rank - 1].Points - rows[rank].Points);
            Assert.All(rows, r => Assert.Equal(46, r.Games));
            Assert.All(rows, r => Assert.True(r.GoalsFor <= 15 * 46));
            Assert.All(Enumerable.Range(0, 24), i => Assert.Equal(LeagueTable.NotPlayed, table.Grid[LeagueTable.Cell(i, i)]));
        }

        [Theory]
        [InlineData(new int[] { 0, 1 })]
        [InlineData(new int[] { 0, 1, 1 })]
        [InlineData(new int[] { 0, 1, 3 })]
        [InlineData(new int[] { -1, 0, 1 })]
        public void SetResults_RefusesAnOrderThatIsntEveryClubOnce(int[] order)
        {
            Assert.Throws<ArgumentException>(() => Table(3).SetResults(order));
            Assert.Throws<ArgumentException>(() => Table(3).SetResults(null));
        }

        [Fact]
        public void PhaseOf_FollowsTheHalfWeekCounter()
        {
            Assert.Equal(LeagueTables.SeasonPhase.Unknown, LeagueTables.PhaseOf(-1));
            Assert.Equal(LeagueTables.SeasonPhase.BeforeCupDraw, LeagueTables.PhaseOf(0));
            Assert.Equal(LeagueTables.SeasonPhase.BeforeCupDraw, LeagueTables.PhaseOf(11));
            Assert.Equal(LeagueTables.SeasonPhase.Running, LeagueTables.PhaseOf(12));
            Assert.Equal(LeagueTables.SeasonPhase.Running, LeagueTables.PhaseOf(0x61));
            Assert.Equal(LeagueTables.SeasonPhase.SeasonOver, LeagueTables.PhaseOf(0x62));
            Assert.Equal(LeagueTables.SeasonPhase.SeasonOver, LeagueTables.PhaseOf(999));
        }

        [Fact]
        public void ModeOf_FollowsMainAndBonusCountries()
        {
            PlayerEnums.Country[] bonus = { PlayerEnums.Country.England };
            Assert.Equal(LeagueMode.Played, LeagueTables.ModeOf(PlayerEnums.Country.Deutschland, 3, PlayerEnums.Country.Deutschland, bonus));
            Assert.Equal(LeagueMode.Played, LeagueTables.ModeOf(PlayerEnums.Country.England, 0, PlayerEnums.Country.Deutschland, bonus));
            Assert.Equal(LeagueMode.Simulated, LeagueTables.ModeOf(PlayerEnums.Country.England, 1, PlayerEnums.Country.Deutschland, bonus));
            Assert.Equal(LeagueMode.NoResults, LeagueTables.ModeOf(PlayerEnums.Country.Italien, 0, PlayerEnums.Country.Deutschland, bonus));
            Assert.Equal(LeagueMode.SimulatedInactive, LeagueTables.ModeOf(PlayerEnums.Country.Frankreich, 1, PlayerEnums.Country.Deutschland, bonus));
        }

        // Deutschland (main country, one league of 4 clubs, partly played), England (top league of 3,
        // second of 4 rolled; a bonus country unless said otherwise), half-week 5.
        internal static void SeedLeagues(FakeModule fake, short halfWeek = 5, bool englandBonus = true)
        {
            fake.WriteModuleBytes(Settings.LeagueRangeTableOffset, new short[] { 0, 1, 3, 3, 3, 3 }.SelectMany(BitConverter.GetBytes).ToArray());
            fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + (uint)PlayerEnums.Country.Deutschland, new byte[] { 0 });
            fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + (uint)PlayerEnums.Country.England, new byte[] { 1 });
            fake.WriteModuleBytes(Settings.LeagueBaseTableOffset, new short[] { 0, 10 }.SelectMany(BitConverter.GetBytes).ToArray());
            fake.WriteModuleBytes(Settings.LeagueClubCountTableOffset, new short[] { 10, 10 }.SelectMany(BitConverter.GetBytes).ToArray());
            for (int record = 0; record < 20; record++)
                fake.WriteModuleBytes(Settings.ClubRecordTableOffset + (uint)record * Settings.ClubRecordStride, Latin1.GetBytes((record < 10 ? "D" : "E") + (record % 10) + "\0"));
            fake.WriteModuleBytes(Settings.MainCountryOffset, BitConverter.GetBytes((short)PlayerEnums.Country.Deutschland));
            fake.WriteModuleBytes(Settings.BonusCountryCountOffset, BitConverter.GetBytes((short)(englandBonus ? 1 : 0)));
            fake.WriteModuleBytes(Settings.BonusCountryListOffset, BitConverter.GetBytes((short)PlayerEnums.Country.England));
            fake.WriteModuleBytes(Settings.HalfWeekOffset, BitConverter.GetBytes(halfWeek));

            WriteLeague(fake, 0, PlayerEnums.Country.Deutschland, 6, 1, new short[] { 0, 1, 2, 3 }, (0, 1, 2, 0));
            WriteLeague(fake, 1, PlayerEnums.Country.England, 4, 0, new short[] { 2, 0, 1 });
            WriteLeague(fake, 2, PlayerEnums.Country.England, 6, 0, new short[] { 3, 4, 5, 6 }, (0, 1, 2, 0), (2, 3, 1, 1));
        }

        private static void WriteLeague(FakeModule fake, int index, PlayerEnums.Country country, short matchdays, short played, short[] clubs, params (int Home, int Away, int HomeGoals, int AwayGoals)[] results)
        {
            uint at = Settings.LeagueTableOffset + (uint)index * Settings.LeagueStride;
            fake.WriteModuleBytes(at, BitConverter.GetBytes((short)clubs.Length));
            fake.WriteModuleBytes(at + Settings.LeagueMatchdaysOffset, BitConverter.GetBytes(matchdays));
            fake.WriteModuleBytes(at + Settings.LeagueCountryOffset, BitConverter.GetBytes((short)country));
            fake.WriteModuleBytes(at + Settings.LeaguePlayedOffset, BitConverter.GetBytes(played));
            fake.WriteModuleBytes(at + Settings.LeagueClubIdsOffset, clubs.SelectMany(BitConverter.GetBytes).ToArray());
            byte[] grid = Enumerable.Repeat(LeagueTable.NotPlayed, LeagueTable.GridSize * LeagueTable.GridSize * 2).ToArray();
            foreach (var r in results)
                grid[LeagueTable.Cell(r.Home, r.Away)] = (byte)(r.HomeGoals << 4 | r.AwayGoals);
            fake.WriteModuleBytes(at + Settings.LeagueGridOffset, grid);
        }

        private static uint Record(int index) => Settings.ClubRecordTableOffset + (uint)index * Settings.ClubRecordStride;

        [Fact]
        public void ReadAll_ReadsEveryLeagueWithItsModeNamesAndLicences()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                fake.WriteModuleBytes(Record(13) + 0xF5, new byte[] { 0x82 });

                List<LeagueTable> leagues = LeagueTables.ReadAll(fake.Memory, FixedAddresses.For(fake.Memory));

                Assert.Equal(new[] { "Bundesliga", "Premier League", "Division One" }, leagues.Select(l => l.Name));
                Assert.Equal(new[] { LeagueMode.Played, LeagueMode.Played, LeagueMode.Simulated }, leagues.Select(l => l.Mode));
                Assert.Equal(new[] { 0, 0, 1 }, leagues.Select(l => l.Level));
                LeagueTable rolled = leagues[2];
                Assert.Equal(new[] { "E3", "E4", "E5", "E6" }, rolled.ClubNames);
                Assert.Equal(new byte[] { 0x82, 0, 0, 0 }, rolled.Licences);
                Assert.Equal(6, rolled.Matchdays);
                Assert.Equal(1, leagues[0].Played);
                // E3 won but has no licence; E5 and E6 drew, level on everything: list order.
                Assert.Equal(new[] { 2, 3, 1, 0 }, rolled.Standings().Select(r => r.ListPosition));
            }
        }

        [Fact]
        public void ReadAll_InACountryNotChosen_HasNoResultsAtTheTopAndNoSeasonChangeBelow()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake, englandBonus: false);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                List<LeagueTable> leagues = LeagueTables.ReadAll(fake.Memory, fixedAddresses);

                Assert.Equal(new[] { LeagueMode.Played, LeagueMode.NoResults, LeagueMode.SimulatedInactive }, leagues.Select(l => l.Mode));
                Assert.False(LeagueTables.WriteOrder(fake.Memory, fixedAddresses, leagues[2], new[] { 3, 2, 1, 0 }));
            }
        }

        [Fact]
        public void ReadAll_SkipsLeaguesThatDontFit()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                // Bundesliga with a broken club count, Premier League claiming another country, a fourth system past the 13 leagues.
                fake.WriteModuleBytes(Settings.LeagueTableOffset, BitConverter.GetBytes((short)30));
                fake.WriteModuleBytes(Settings.LeagueTableOffset + Settings.LeagueStride + Settings.LeagueCountryOffset, BitConverter.GetBytes((short)PlayerEnums.Country.Italien));
                fake.WriteModuleBytes(Settings.LeagueRangeTableOffset + 6, new short[] { 13, 14 }.SelectMany(BitConverter.GetBytes).ToArray());

                List<LeagueTable> leagues = LeagueTables.ReadAll(fake.Memory, FixedAddresses.For(fake.Memory));

                Assert.Equal(new[] { "Division One" }, leagues.Select(l => l.Name));
                Assert.Empty(LeagueTables.ReadAll(fake.Memory, null));
            }
        }

        [Fact]
        public void ReadAll_NamesALeagueBeyondTheKnownOnesByCountryAndUnknownClubsById()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                // Italy with two leagues (the game has one) and a club past its count.
                fake.WriteModuleBytes(Settings.LeagueRangeTableOffset + 6, new short[] { 3, 5 }.SelectMany(BitConverter.GetBytes).ToArray());
                fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + (uint)PlayerEnums.Country.Italien, new byte[] { 3 });
                WriteLeague(fake, 3, PlayerEnums.Country.Italien, 2, 0, new short[] { 0 });
                WriteLeague(fake, 4, PlayerEnums.Country.Italien, 2, 0, new short[] { 1 });
                fake.WriteModuleBytes(Settings.BonusCountryCountOffset, BitConverter.GetBytes((short)1));
                fake.WriteModuleBytes(Settings.BonusCountryListOffset, BitConverter.GetBytes((short)PlayerEnums.Country.Italien));

                List<LeagueTable> leagues = LeagueTables.ReadAll(fake.Memory, FixedAddresses.For(fake.Memory));

                Assert.Equal(new[] { "Serie A", "Italien 2" }, leagues.Skip(3).Select(l => l.Name));
                Assert.Equal(LeagueMode.Played, leagues[3].Mode);
                Assert.Equal(new[] { "Verein 1" }, leagues[4].ClubNames);
            }
        }

        [Fact]
        public void WriteOrder_WritesTheGridOfARolledLeagueOnly()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                List<LeagueTable> leagues = LeagueTables.ReadAll(fake.Memory, fixedAddresses);

                Assert.False(LeagueTables.WriteOrder(fake.Memory, fixedAddresses, leagues[0], new[] { 3, 2, 1, 0 }));
                Assert.False(LeagueTables.WriteOrder(fake.Memory, null, leagues[2], new[] { 3, 2, 1, 0 }));
                Assert.True(LeagueTables.WriteOrder(fake.Memory, fixedAddresses, leagues[2], new[] { 3, 2, 1, 0 }));

                LeagueTable again = LeagueTables.ReadAll(fake.Memory, fixedAddresses)[2];
                Assert.Equal(new[] { 3, 2, 1, 0 }, again.Standings().Select(r => r.ListPosition));
                Assert.Equal(1, LeagueTables.ReadAll(fake.Memory, fixedAddresses)[0].Standings().Sum(r => r.Games) / 2);
            }
        }

        [Fact]
        public void Licence_IsReadFromAWorkingCopyAndWrittenToRecordAndCopies()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                const uint copy = 0x510000;
                byte[] slot = new byte[Settings.ClubCacheStride];
                slot[0] = 1;
                slot[1] = (byte)PlayerEnums.Country.England;
                slot[2] = 4;
                Array.Copy(BitConverter.GetBytes(fake.ModuleBase + copy), 0, slot, Settings.ClubCachePointerOffset, 4);
                fake.WriteModuleBytes(Settings.ClubCacheTableOffset, slot);
                fake.WriteModuleBytes(copy + Settings.ClubLicenceOffset, new byte[] { 0x02 });
                fake.WriteModuleBytes(Record(14) + 0xF5, new byte[] { 0x01 });

                Assert.Equal((byte)0x02, LeagueTables.ReadLicence(fake.Memory, fixedAddresses, PlayerEnums.Country.England, 4));
                Assert.Equal((byte)0x00, LeagueTables.ReadLicence(fake.Memory, fixedAddresses, PlayerEnums.Country.England, 5));
                Assert.Null(LeagueTables.ReadLicence(fake.Memory, fixedAddresses, PlayerEnums.Country.England, 12));

                Assert.True(LeagueTables.WriteLicence(fake.Memory, fixedAddresses, PlayerEnums.Country.England, 4, 0x83));
                Assert.Equal(new byte[] { 0x83 }, fake.ReadModuleBytes(Record(14) + 0xF5, 1));
                Assert.Equal(new byte[] { 0x83 }, fake.ReadModuleBytes(copy + Settings.ClubLicenceOffset, 1));
                Assert.False(LeagueTables.WriteLicence(fake.Memory, fixedAddresses, PlayerEnums.Country.England, 12, 0x80));
            }
        }

        [Fact]
        public void Phase_ReadsTheHalfWeekCounter()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedLeagues(fake, halfWeek: 40);
                Assert.Equal(LeagueTables.SeasonPhase.Running, LeagueTables.Phase(fake.Memory, FixedAddresses.For(fake.Memory)));
                Assert.Equal(LeagueTables.SeasonPhase.Unknown, LeagueTables.Phase(fake.Memory, null));
            }
        }

        // Holland (6 teams from index 1 of the records) and Island (3 teams), the pots as the season
        // start leaves them: Holland's champion 4 in the CL pot and a UI cup pairing, cup winner 1 in
        // the PdP pot, 2 in the UEFA pot; runner-up 5, further teams 2, 3.
        internal static void SeedEuropeanCountries(FakeModule fake, short halfWeek = 3)
        {
            byte[] counts = new byte[76];
            counts[(int)PlayerEnums.Country.Holland] = 6;
            counts[(int)PlayerEnums.Country.Island] = 3;
            counts[(int)PlayerEnums.Country.Belgien] = 4;   // has no first index: skipped
            fake.WriteModuleBytes(Settings.EuropeanClubCountOffset, counts);
            byte[] firsts = new byte[76];
            firsts[(int)PlayerEnums.Country.Holland] = 1;
            firsts[(int)PlayerEnums.Country.Island] = 7;
            fake.WriteModuleBytes(Settings.EuropeanFirstClubOffset, firsts);
            string[] names = { "", "Ajax", "PSV", "Feyenoord", "Vitesse", "Twente", "Utrecht", "KR", "Fram", "IA" };
            for (int i = 1; i < names.Length; i++)
            {
                uint record = EuropeanRecords + (uint)i * Settings.EuropeanClubStride;
                fake.WriteModuleBytes(record, Latin1.GetBytes(names[i] + "\0"));
                // The last Iceland team claims another country: left out.
                fake.WriteModuleBytes(record + Settings.EuropeanClubCountryOffset, new[] { (byte)(i < 7 ? PlayerEnums.Country.Holland : i < 9 ? PlayerEnums.Country.Island : PlayerEnums.Country.Malta) });
            }
            fake.WriteModuleBytes(Settings.HalfWeekOffset, BitConverter.GetBytes(halfWeek));
            byte h = (byte)PlayerEnums.Country.Holland;
            fake.WriteModuleBytes(Settings.CupChampionOffset + h, new byte[] { 4 });
            fake.WriteModuleBytes(Settings.CupWinnerOffset + h, new byte[] { 1 });
            fake.WriteModuleBytes(Settings.CupRunnerUpOffset + h, new byte[] { 5 });
            fake.WriteModuleBytes(Settings.CupFurtherTeamsOffset + h * 4u, new byte[] { 2, 3, 0, 0 });
            uint[] pots = Settings.CupPotLists.Select(p => p.Key).ToArray();
            fake.WriteModuleBytes(pots[0], new byte[] { (byte)PlayerEnums.Country.Deutschland, 0, 4, 0, h, 0, 4, 0 });
            fake.WriteModuleBytes(pots[2] + 4, new byte[] { h, 0, 1, 0 });
            fake.WriteModuleBytes(pots[4], new byte[] { h, 0, 2, 0, h, 0, 3, 0 });
            fake.WriteModuleBytes(Settings.UiCupPairingsOffset + Settings.CupPairingStride, new byte[] { (byte)PlayerEnums.Country.Island, 0, 1, 0, h, 0, 4, 0 });
        }

        private static byte[] Pot(FakeModule fake, int list, int entries) => fake.ReadModuleBytes(Settings.CupPotLists.ElementAt(list).Key, entries * 4);

        [Fact]
        public void EuropeanCountries_ListTheTeamsOfEveryCountryWithoutLeagueTable()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedEuropeanCountries(fake);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);

                var countries = EuropeanCupEntries.Countries(fake.Memory, fixedAddresses, fake.ModuleBase + EuropeanRecords);

                Assert.Equal(new[] { PlayerEnums.Country.Holland, PlayerEnums.Country.Island }, countries.Keys.OrderBy(c => c));
                Assert.Equal(new[] { "Ajax", "PSV", "Feyenoord", "Vitesse", "Twente", "Utrecht" }, countries[PlayerEnums.Country.Holland].Select(t => t.ToString()));
                Assert.Equal(new short[] { 0, 1 }, countries[PlayerEnums.Country.Island].Select(t => t.Index));
                Assert.Equal((short)4, EuropeanCupEntries.Holder(fake.Memory, fixedAddresses, PlayerEnums.Country.Holland, EuropeanCupEntries.Title.Champion));
                Assert.Equal((short)1, EuropeanCupEntries.Holder(fake.Memory, fixedAddresses, PlayerEnums.Country.Holland, EuropeanCupEntries.Title.CupWinner));
                Assert.Null(EuropeanCupEntries.Holder(fake.Memory, null, PlayerEnums.Country.Holland, EuropeanCupEntries.Title.Champion));
                Assert.Empty(EuropeanCupEntries.Countries(fake.Memory, null));
            }
        }

        [Fact]
        public void Assign_SwapsTheTwoTeamsEverywhereBeforeTheDraw()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedEuropeanCountries(fake);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                byte h = (byte)PlayerEnums.Country.Holland;

                // Ajax (0) champion instead of Twente (4): the CL entry, the UI cup pairing, the title and Ajax's place among the further teams.
                Assert.Equal(4, EuropeanCupEntries.Assign(fake.Memory, fixedAddresses, PlayerEnums.Country.Holland, EuropeanCupEntries.Title.Champion, 0));
                Assert.Equal(new byte[] { (byte)PlayerEnums.Country.Deutschland, 0, 4, 0, h, 0, 0, 0 }, Pot(fake, 0, 2));
                Assert.Equal(new byte[] { 0 }, fake.ReadModuleBytes(Settings.CupChampionOffset + h, 1));
                Assert.Equal(new byte[] { (byte)PlayerEnums.Country.Island, 0, 1, 0, h, 0, 0, 0 }, fake.ReadModuleBytes(Settings.UiCupPairingsOffset + Settings.CupPairingStride, 8));

                // Feyenoord (2) cup winner instead of PSV (1): PdP pot, UEFA pot, title and further teams.
                Assert.Equal(4, EuropeanCupEntries.Assign(fake.Memory, fixedAddresses, PlayerEnums.Country.Holland, EuropeanCupEntries.Title.CupWinner, 2));
                Assert.Equal(new byte[] { h, 0, 2, 0 }, Pot(fake, 2, 2).Skip(4));
                Assert.Equal(new byte[] { h, 0, 1, 0, h, 0, 3, 0 }, Pot(fake, 4, 2));
                // Only the three further teams of six change, not the filler slot after them.
                Assert.Equal(new byte[] { 1, 3, 4, 0 }, fake.ReadModuleBytes(Settings.CupFurtherTeamsOffset + h * 4u, 4));
                Assert.Equal(new byte[] { 5 }, fake.ReadModuleBytes(Settings.CupRunnerUpOffset + h, 1));

                // The holder already: nothing to do.
                Assert.Equal(0, EuropeanCupEntries.Assign(fake.Memory, fixedAddresses, PlayerEnums.Country.Holland, EuropeanCupEntries.Title.CupWinner, 2));
            }
        }

        [Fact]
        public void Assign_AfterTheDraw_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedEuropeanCountries(fake, halfWeek: 12);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                Assert.False(EuropeanCupEntries.CanSwap(fake.Memory, fixedAddresses));
                Assert.Equal(-1, EuropeanCupEntries.Assign(fake.Memory, fixedAddresses, PlayerEnums.Country.Holland, EuropeanCupEntries.Title.Champion, 0));
                Assert.Equal(new byte[] { 4 }, fake.ReadModuleBytes(Settings.CupChampionOffset + (byte)PlayerEnums.Country.Holland, 1));
            }
        }
    }
}
