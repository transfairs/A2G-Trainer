using System;
using System.Text;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for CoachController.GetEntity's read path against a FakeModule - the counterpart to
    /// CoachControllerSaveTests (which only verifies the write path). Every value written here is
    /// deliberately distinct from both the type's default and from any other field's value, so a
    /// field left unread (still at its Coach default) or two fields accidentally swapped would
    /// both fail an assertion instead of passing by coincidence.
    /// </summary>
    public class CoachControllerFakeModuleTests
    {
        [Fact]
        public void GetEntity_ReadsIdentityStocksWealthDifficultyGamesWinsNextSeasonReleaseClauseAndCompetenciesFromMemory()
        {
            using (FakeModule fake = new FakeModule())
            {
                // The club pointer is deliberately left null (the game clears it whenever team slot 0
                // is freed): on the original build, trainers are read from their fixed working copy.
                Addresses coachAddresses = AddressPresets.COACH;
                Encoding latin1 = Encoding.GetEncoding("iso-8859-1");

                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.FIRSTNAME], latin1.GetBytes("Helmut\0"));
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.LAST_NAME], latin1.GetBytes("Schoen\0"));
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.LEVEL], new byte[] { 11 });
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.AGE], new byte[] { 55 });
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.DIFFICULTY], new byte[] { (byte)CoachEnums.Difficulty.NurFuerDieBesten });
                // Beyond byte range (0-255): both fields are 2 bytes wide in memory, so this also
                // catches a regression back to a single-byte read.
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.GAMES], BitConverter.GetBytes((ushort)355));
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.WINS], BitConverter.GetBytes((ushort)333));
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.WEALTH], BitConverter.GetBytes(987654));

                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.COUNTRY_NEXT_SEASON], new byte[] { (byte)PlayerEnums.Country.Spanien });
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.CLUB_NEXT_SEASON], new byte[] { 21 });
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.RELEASE_CLAUSE], new byte[] { 0x81 });
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.CLUB], new byte[] { 55 });

                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.STOCK_COUNTRY], new byte[] { (byte)PlayerEnums.Country.Frankreich });
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.STOCK_CLUB], new byte[] { 3 });
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.STOCK_SHARES], BitConverter.GetBytes((ushort)12));
                CoachMemory.WriteWorkingCopy(fake, coachAddresses[CoachEnums.AddressKey.STOCK_PRICE], BitConverter.GetBytes((ushort)34));

                const int stockSlotStride = 0x10;
                string lastStockCountryOffset = Tools.SumHex(new[] { coachAddresses[CoachEnums.AddressKey.STOCK_COUNTRY], (5 * stockSlotStride).ToString("X") });
                string lastStockClubOffset = Tools.SumHex(new[] { coachAddresses[CoachEnums.AddressKey.STOCK_CLUB], (5 * stockSlotStride).ToString("X") });
                string lastStockSharesOffset = Tools.SumHex(new[] { coachAddresses[CoachEnums.AddressKey.STOCK_SHARES], (5 * stockSlotStride).ToString("X") });
                string lastStockPriceOffset = Tools.SumHex(new[] { coachAddresses[CoachEnums.AddressKey.STOCK_PRICE], (5 * stockSlotStride).ToString("X") });
                CoachMemory.WriteWorkingCopy(fake, lastStockCountryOffset, new byte[] { (byte)PlayerEnums.Country.Italien });
                CoachMemory.WriteWorkingCopy(fake, lastStockClubOffset, new byte[] { 9 });
                CoachMemory.WriteWorkingCopy(fake, lastStockSharesOffset, BitConverter.GetBytes((ushort)56));
                CoachMemory.WriteWorkingCopy(fake, lastStockPriceOffset, BitConverter.GetBytes((ushort)78));

                // Level 0: all six competencies, distinct values, to catch both a field never being
                // read (stuck at the CoachCompetencyLevel default of 1) and two competencies being
                // read into the wrong slot (CompetencyOrder mismatched against the in-memory layout).
                const int competencyTableOffset = 0x36398;
                const int competencyEntryStride = 0x2;
                int competencyCount = Enum.GetValues(typeof(CoachEnums.CompetencyKey)).Length;
                for (int competencyIndex = 0; competencyIndex < competencyCount; competencyIndex++)
                {
                    string offset = (competencyTableOffset + competencyIndex * competencyEntryStride).ToString("X");
                    CoachMemory.WriteWorkingCopy(fake, offset, BitConverter.GetBytes((ushort)(2 + competencyIndex)));
                }
                // Level 15 (Coach.MaxLevel): a single field, to verify the per-level stride
                // separately from the per-competency stride exercised above.
                string level15Offset = (competencyTableOffset + Coach.MaxLevel * competencyCount * competencyEntryStride).ToString("X");
                CoachMemory.WriteWorkingCopy(fake, level15Offset, BitConverter.GetBytes((ushort)44));

                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                Coach coach = controller.Coach;

                Assert.Equal("Helmut", coach.Firstname);
                Assert.Equal("Schoen", coach.Lastname);
                Assert.Equal((byte)11, coach.Level);
                Assert.Equal((byte)55, coach.Age);
                Assert.Equal(CoachEnums.Difficulty.NurFuerDieBesten, coach.Difficulty);
                Assert.Equal((ushort)355, coach.Games);
                Assert.Equal((ushort)333, coach.Wins);
                Assert.Equal(987654, coach.Wealth);

                Assert.Equal(PlayerEnums.Country.Spanien, coach.CountryNextSeason);
                Assert.Equal((byte)21, coach.ClubIdNextSeason);
                Assert.True(coach.HasReleaseClause);
                Assert.True(coach.IsReleaseClausePublished);
                Assert.Equal(PlayerEnums.Country.Deutschland, coach.CurrentCountry);
                Assert.Equal((byte)55, coach.CurrentClubId);

                Assert.Equal(PlayerEnums.Country.Frankreich, coach.Stocks[0].Country);
                Assert.Equal((byte)3, coach.Stocks[0].ClubId);
                Assert.Equal((ushort)12, coach.Stocks[0].Shares);
                Assert.Equal((ushort)34, coach.Stocks[0].Price);

                Assert.Equal(PlayerEnums.Country.Italien, coach.Stocks[5].Country);
                Assert.Equal((byte)9, coach.Stocks[5].ClubId);
                Assert.Equal((ushort)56, coach.Stocks[5].Shares);
                Assert.Equal((ushort)78, coach.Stocks[5].Price);

                CoachCompetencyLevel level0 = coach.CompetencyLevels[Coach.MinLevel];
                Assert.Equal((ushort)2, level0.Verhandlungsgeschick);
                Assert.Equal((ushort)3, level0.Motivationsfaehigkeit);
                Assert.Equal((ushort)4, level0.Trainingsgestaltung);
                Assert.Equal((ushort)5, level0.Autoritaet);
                Assert.Equal((ushort)6, level0.Fremdsprachenkenntnisse);
                Assert.Equal((ushort)7, level0.Ausstrahlung);

                Assert.Equal((ushort)44, coach.CompetencyLevels[Coach.MaxLevel].Verhandlungsgeschick);
            }
        }

        [Fact]
        public void GetEntity_ForSecondSlot_ReadsTheNextMitspielerWorkingCopy()
        {
            using (FakeModule fake = new FakeModule())
            {
                Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.FIRSTNAME], latin1.GetBytes("Erster\0"), trainerSlot: 0);
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.FIRSTNAME], latin1.GetBytes("Zweiter\0"), trainerSlot: 1);

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 1).Coach;

                Assert.Equal("Zweiter", coach.Firstname);
            }
        }

        [Fact]
        public void GetEntity_WithoutCoachAnchors_ReadsThroughTheClubPointer()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
                fake.WriteDisplayCacheBytes(AddressPresets.COACH[CoachEnums.AddressKey.FIRSTNAME], latin1.GetBytes("Zeiger\0"));
                fake.WriteDisplayCacheBytes(AddressPresets.COACH[CoachEnums.AddressKey.WEALTH], BitConverter.GetBytes(4711));
                // A value at the fixed working copy must be ignored when the build's anchors aren't known.
                CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.FIRSTNAME], latin1.GetBytes("Fest\0"));

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN).Coach;

                Assert.Equal("Zeiger", coach.Firstname);
                Assert.Equal(4711, coach.Wealth);
            }
        }
    }
}
