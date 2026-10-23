using System;
using System.Diagnostics;
using System.Text;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for CoachController.Save and the unresolved-address fallback in GetEntity.</summary>
    public class CoachControllerSaveTests
    {
        private static Coach FillCoach(CoachController controller)
        {
            Coach coach = controller.Coach;
            coach.Firstname = "Robin";
            coach.Lastname = "Trauer";
            coach.Level = 9;
            coach.Age = 42;
            coach.Difficulty = CoachEnums.Difficulty.Realistisch;
            coach.Wealth = 123456;
            coach.CountryNextSeason = PlayerEnums.Country.Holland;
            coach.ClubIdNextSeason = 17;
            coach.HasReleaseClause = true;
            // Beyond byte range (0-255) to prove Save writes the full 2-byte field rather than
            // truncating - the in-game "Anzahl Spiele"/"Anzahl Siege" fields are both 2 bytes wide.
            coach.Games = 400;
            coach.Wins = 260;
            coach.Stocks[0].Country = PlayerEnums.Country.Deutschland;
            coach.Stocks[0].ClubId = 7;
            coach.Stocks[0].Shares = 100;
            coach.Stocks[0].Price = 250;
            coach.Stocks[5].Country = PlayerEnums.Country.England;
            coach.Stocks[5].ClubId = 9;
            coach.Stocks[5].Shares = 50;
            coach.Stocks[5].Price = 75;
            coach.CompetencyLevels[Coach.MinLevel].Set(CoachEnums.CompetencyKey.Verhandlungsgeschick, 11);
            coach.CompetencyLevels[Coach.MaxLevel].Set(CoachEnums.CompetencyKey.Verhandlungsgeschick, 22);
            return coach;
        }

        // Asserts every field FillCoach set, reading each one through read(presetOffset, length).
        private static void AssertSaved(Func<string, int, byte[]> read)
        {
            Addresses coachAddresses = AddressPresets.COACH;
            Encoding latin1 = Encoding.GetEncoding("iso-8859-1");

            Assert.Equal("Robin", latin1.GetString(read(coachAddresses[CoachEnums.AddressKey.FIRSTNAME], 9)).Split('\0')[0]);
            Assert.Equal("Trauer", latin1.GetString(read(coachAddresses[CoachEnums.AddressKey.LAST_NAME], 15)).Split('\0')[0]);
            Assert.Equal((byte)9, read(coachAddresses[CoachEnums.AddressKey.LEVEL], 1)[0]);
            Assert.Equal((byte)42, read(coachAddresses[CoachEnums.AddressKey.AGE], 1)[0]);
            Assert.Equal((byte)CoachEnums.Difficulty.Realistisch, read(coachAddresses[CoachEnums.AddressKey.DIFFICULTY], 1)[0]);
            Assert.Equal(123456, BitConverter.ToInt32(read(coachAddresses[CoachEnums.AddressKey.WEALTH], 4), 0));
            Assert.Equal((byte)PlayerEnums.Country.Holland, read(coachAddresses[CoachEnums.AddressKey.COUNTRY_NEXT_SEASON], 1)[0]);
            Assert.Equal((byte)17, read(coachAddresses[CoachEnums.AddressKey.CLUB_NEXT_SEASON], 1)[0]);
            Assert.Equal((byte)0x01, read(coachAddresses[CoachEnums.AddressKey.RELEASE_CLAUSE], 1)[0]);
            Assert.Equal((ushort)400, BitConverter.ToUInt16(read(coachAddresses[CoachEnums.AddressKey.GAMES], 2), 0));
            Assert.Equal((ushort)260, BitConverter.ToUInt16(read(coachAddresses[CoachEnums.AddressKey.WINS], 2), 0));

            Assert.Equal((byte)PlayerEnums.Country.Deutschland, read(coachAddresses[CoachEnums.AddressKey.STOCK_COUNTRY], 1)[0]);
            Assert.Equal((byte)7, read(coachAddresses[CoachEnums.AddressKey.STOCK_CLUB], 1)[0]);
            Assert.Equal((ushort)100, BitConverter.ToUInt16(read(coachAddresses[CoachEnums.AddressKey.STOCK_SHARES], 2), 0));
            Assert.Equal((ushort)250, BitConverter.ToUInt16(read(coachAddresses[CoachEnums.AddressKey.STOCK_PRICE], 2), 0));

            const int stockSlotStride = 0x10;
            string lastStockCountryOffset = Tools.SumHex(new[] { coachAddresses[CoachEnums.AddressKey.STOCK_COUNTRY], (5 * stockSlotStride).ToString("X") });
            string lastStockPriceOffset = Tools.SumHex(new[] { coachAddresses[CoachEnums.AddressKey.STOCK_PRICE], (5 * stockSlotStride).ToString("X") });
            Assert.Equal((byte)PlayerEnums.Country.England, read(lastStockCountryOffset, 1)[0]);
            Assert.Equal((ushort)75, BitConverter.ToUInt16(read(lastStockPriceOffset, 2), 0));

            const int competencyTableOffset = 0x36398;
            const int competencyEntryStride = 0x2;
            int competencyCount = Enum.GetValues(typeof(CoachEnums.CompetencyKey)).Length;
            string level0Offset = (competencyTableOffset).ToString("X");
            string level15Offset = (competencyTableOffset + Coach.MaxLevel * competencyCount * competencyEntryStride).ToString("X");
            Assert.Equal((ushort)11, BitConverter.ToUInt16(read(level0Offset, 2), 0));
            Assert.Equal((ushort)22, BitConverter.ToUInt16(read(level15Offset, 2), 0));
        }

        [Fact]
        public void Save_WritesWorkingCopyAndSavedRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                FillCoach(controller);

                controller.Save();

                // The working copy is what the game shows right now; the saved record is what the
                // game re-fetches the working copy from and what ends up in the savegame file.
                AssertSaved((offset, length) => CoachMemory.ReadWorkingCopy(fake, offset, length));
                AssertSaved((offset, length) => CoachMemory.ReadRecord(fake, offset, length));
            }
        }

        [Fact]
        public void Save_ForSecondSlot_WritesTheNextMitspielersRecordOnly()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 1);
                controller.Coach.Wealth = 2468;

                controller.Save();

                string wealth = AddressPresets.COACH[CoachEnums.AddressKey.WEALTH];
                Assert.Equal(2468, BitConverter.ToInt32(CoachMemory.ReadRecord(fake, wealth, 4, trainerSlot: 1), 0));
                Assert.Equal(0, BitConverter.ToInt32(CoachMemory.ReadRecord(fake, wealth, 4, trainerSlot: 0), 0));
            }
        }

        [Fact]
        public void Save_WithoutCoachAnchors_WritesThroughTheClubPointerOnly()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                FillCoach(controller);

                controller.Save();

                AssertSaved(fake.ReadDisplayCacheBytes);
                // Unconfirmed build: the fixed record must stay untouched.
                Assert.Equal(0, BitConverter.ToInt32(CoachMemory.ReadRecord(fake, AddressPresets.COACH[CoachEnums.AddressKey.WEALTH], 4), 0));
            }
        }

        [Fact]
        public void GetEntity_WhenAddressIsUnresolvable_FallsBackToZeroDefaultsWithoutThrowing()
        {
            // Deliberately doesn't call RoutePointerSlot and uses a layout without coach anchors: a
            // fresh FakeModule's module block is guaranteed zero-initialized by VirtualAlloc, so the
            // pointer field ResolveAddress dereferences is reliably 0 - unlike a bare self-attach
            // reading real, uncontrolled process memory at a fixed offset, which isn't guaranteed to
            // be zero (and isn't: this used to read as 0 reliably until the test assembly grew large
            // enough to shift what's actually mapped at that offset in the test host process).
            using (FakeModule fake = new FakeModule())
            {
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN).Coach;

                Assert.Equal((ushort)0, coach.Stocks[0].Shares);
                Assert.Equal(0, coach.Wealth);
            }
        }
    }
}
