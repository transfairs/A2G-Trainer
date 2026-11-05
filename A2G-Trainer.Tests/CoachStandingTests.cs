using System;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for the manager's nerves (+0x28, with the season's sum and weeks), the standings with
    /// public, fans and media (+0x30..+0x32, media derived by FUN_005bb0b0) and the champion bets of
    /// the Wettbüro (record +0x1A74 + league system * 0x20).
    /// </summary>
    public class CoachStandingTests
    {
        private static string Key(CoachEnums.AddressKey key) => AddressPresets.COACH[key];

        // Germany's league system 2: the bet block of Mitspieler 1 in the saved record.
        private const byte GermanySystem = 2;
        private static uint BetAt => Settings.CoachRecordTableOffset + 1 * Settings.CoachRecordStride + Settings.CoachRecordBetOffset + GermanySystem * Settings.CoachRecordBetStride + Settings.CoachBetChampionOffset;

        private static void SeedLeagueSystem(FakeModule fake) =>
            fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + (uint)PlayerEnums.Country.Deutschland, new[] { GermanySystem });

        [Fact]
        public void GetEntity_ReadsNervesStandingsAndTheChampionBet()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachMemory.WriteWorkingCopy(fake, Key(CoachEnums.AddressKey.NERVES), BitConverter.GetBytes((short)73));
                CoachMemory.WriteWorkingCopy(fake, Key(CoachEnums.AddressKey.NERVES_WEEKS), BitConverter.GetBytes((short)10));
                CoachMemory.WriteWorkingCopy(fake, Key(CoachEnums.AddressKey.PUBLIC_STANDING), new byte[] { 61, 0xFB, 50 });
                SeedLeagueSystem(fake);
                byte[] bet = new byte[8];
                Array.Copy(BitConverter.GetBytes(250000), 0, bet, 0, 4);
                Array.Copy(BitConverter.GetBytes((ushort)4), 0, bet, 4, 2);
                Array.Copy(BitConverter.GetBytes((short)35), 0, bet, 6, 2);
                fake.WriteModuleBytes(BetAt, bet);

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal((byte)73, coach.Nerves);
                Assert.Equal((short)10, coach.NervesWeeks);
                Assert.Equal((byte)61, coach.PublicStanding);
                // A negative byte counts as 0, like the game's clamp.
                Assert.Equal((byte)0, coach.FanStanding);
                Assert.Equal((byte)50, coach.MediaBase);
                ChampionTip tip = coach.ChampionTips[PlayerEnums.Country.Deutschland];
                Assert.Equal(250000, tip.Stake);
                Assert.Equal((ushort)4, tip.ClubId);
                Assert.Equal((short)35, tip.Odds);
            }
        }

        [Fact]
        public void Save_WritesTheValues_AndTheNervesSumOnlyWhenTheNervesChanged()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachMemory.WriteWorkingCopy(fake, Key(CoachEnums.AddressKey.NERVES), BitConverter.GetBytes((short)60));
                CoachMemory.WriteWorkingCopy(fake, Key(CoachEnums.AddressKey.NERVES_SUM), BitConverter.GetBytes((short)555));
                CoachMemory.WriteWorkingCopy(fake, Key(CoachEnums.AddressKey.NERVES_WEEKS), BitConverter.GetBytes((short)8));
                SeedLeagueSystem(fake);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

                controller.Save();
                Assert.Equal((short)555, BitConverter.ToInt16(CoachMemory.ReadWorkingCopy(fake, Key(CoachEnums.AddressKey.NERVES_SUM), 2), 0));

                Coach coach = controller.Coach;
                coach.Nerves = 90;
                coach.PublicStanding = 80;
                coach.FanStanding = 70;
                coach.MediaBase = 40;
                ChampionTip tip = coach.ChampionTips[PlayerEnums.Country.Deutschland];
                tip.ClubId = 7;
                tip.Odds = 120;
                tip.Stake = 50000;
                controller.Save();

                foreach (Func<string, int, byte[]> read in new Func<string, int, byte[]>[]
                {
                    (offset, length) => CoachMemory.ReadWorkingCopy(fake, offset, length),
                    (offset, length) => CoachMemory.ReadRecord(fake, offset, length)
                })
                {
                    Assert.Equal((short)90, BitConverter.ToInt16(read(Key(CoachEnums.AddressKey.NERVES), 2), 0));
                    Assert.Equal((short)720, BitConverter.ToInt16(read(Key(CoachEnums.AddressKey.NERVES_SUM), 2), 0));
                    Assert.Equal(new byte[] { 80, 70, 40 }, read(Key(CoachEnums.AddressKey.PUBLIC_STANDING), 3));
                }
                byte[] bet = fake.ReadModuleBytes(BetAt, 8);
                Assert.Equal(50000, BitConverter.ToInt32(bet, 0));
                Assert.Equal((ushort)7, BitConverter.ToUInt16(bet, 4));
                Assert.Equal((short)120, BitConverter.ToInt16(bet, 6));
            }
        }

        [Fact]
        public void MediaStanding_ScalesTheBaseByTheCharismaOfTheCurrentLevel()
        {
            Coach coach = new Coach { Level = 3 };
            // Charisma 5: factor (5 + 20) * 4 = 100, so the base is shown as it is.
            coach.CompetencyLevels[3].Set(CoachEnums.CompetencyKey.Ausstrahlung, 5);
            coach.MediaBase = 64;
            Assert.Equal((byte)64, coach.MediaStanding);

            // Charisma 30: factor 200, capped at 100.
            coach.CompetencyLevels[3].Set(CoachEnums.CompetencyKey.Ausstrahlung, 30);
            Assert.Equal((byte)100, coach.MediaStanding);

            // Setting it stores the smallest base reaching the value: 75 needs 38 (38 * 200 / 100 = 76).
            coach.MediaStanding = 75;
            Assert.Equal((byte)38, coach.MediaBase);
            Assert.Equal((byte)76, coach.MediaStanding);

            // Charisma 0: factor 80; 100 would need 125, within the signed byte.
            coach.CompetencyLevels[3].Set(CoachEnums.CompetencyKey.Ausstrahlung, 0);
            coach.MediaStanding = 100;
            Assert.Equal((byte)125, coach.MediaBase);
        }

        [Fact]
        public void ChampionTip_RaisesPropertyChanged()
        {
            ChampionTip tip = new ChampionTip();
            var raised = new System.Collections.Generic.List<string>();
            tip.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            tip.Stake = 1;
            tip.ClubId = 2;
            tip.Odds = 3;

            Assert.Equal(new[] { nameof(ChampionTip.Stake), nameof(ChampionTip.ClubId), nameof(ChampionTip.Odds) }, raised);
        }

        [Theory]
        [InlineData(0x00, ClubEnums.AssociationImage.Ausgezeichnet)]
        [InlineData(0x02, ClubEnums.AssociationImage.Angesehen)]
        [InlineData(0x05, ClubEnums.AssociationImage.Normal)]
        [InlineData(0x09, ClubEnums.AssociationImage.Bescheiden)]
        [InlineData(0x17, ClubEnums.AssociationImage.Verzockt)]
        [InlineData(0x20, ClubEnums.AssociationImage.Angesehen)]
        [InlineData(0x57, ClubEnums.AssociationImage.Verzockt)]
        public void AssociationImage_FollowsTheGamesStages(byte stored, ClubEnums.AssociationImage expected)
        {
            Assert.Equal(expected, new Club { Association = stored }.AssociationImage);
        }

        [Fact]
        public void AssociationImage_Set_StoresTheStagesLowestCountAndKeepsTheLicenceBit()
        {
            Club club = new Club { Association = 0xE7 };

            club.AssociationImage = ClubEnums.AssociationImage.Ausgezeichnet;
            Assert.Equal((byte)0x80, club.Association);

            club.AssociationImage = ClubEnums.AssociationImage.Bescheiden;
            Assert.Equal((byte)0x89, club.Association);
            Assert.Equal(ClubEnums.AssociationImage.Bescheiden, club.AssociationImage);
        }
    }
}
