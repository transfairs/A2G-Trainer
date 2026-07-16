using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    public class ClubTests
    {
        [Fact]
        public void IsClubMember_MatchesOnIdAndCountry()
        {
            Club club = new Club { Id = 5, Country = PlayerEnums.Country.Deutschland };
            Player member = new Player { ClubId = 5, ClubCountry = PlayerEnums.Country.Deutschland };
            Player other = new Player { ClubId = 6, ClubCountry = PlayerEnums.Country.Deutschland };

            Assert.True(club.IsClubMember(member));
            Assert.False(club.IsClubMember(other));
        }

        [Fact]
        public void IsClubMember_NullPlayer_ReturnsFalse()
        {
            Club club = new Club { Id = 5, Country = PlayerEnums.Country.Deutschland };

            Assert.False(club.IsClubMember(null));
        }

        [Fact]
        public void ClubName_LongerThanLimit_IsTruncated()
        {
            Club club = new Club { ClubName = "ThisClubNameIsDefinitelyTooLong" };

            Assert.Equal(19, club.ClubName.Length);
        }

        [Fact]
        public void IsBlockARoof_TogglesRoofFlag_IndependentlyOfOtherBlocks()
        {
            Club club = new Club
            {
                IsBlockARoof = true,
                IsBlockCRoof = true
            };

            Assert.True(club.IsBlockARoof);
            Assert.True(club.IsBlockCRoof);
            Assert.False(club.IsBlockBRoof);

            club.IsBlockARoof = false;

            Assert.False(club.IsBlockARoof);
            Assert.True(club.IsBlockCRoof);
        }

        [Fact]
        public void Roof_ChangedAfterInitialisation_ForcesBlockAWeeksToAtLeastOne()
        {
            Club club = new Club { Initilisation = false };

            Assert.Equal(0, club.BlockAWeeks);

            club.Roof = ClubEnums.Roof.BlockA;

            Assert.True(club.BlockAWeeks >= 1);
        }

        [Fact]
        public void Roof_ChangedDuringInitialisation_DoesNotForceBlockAWeeks()
        {
            Club club = new Club(); // Initilisation defaults to true

            club.Roof = ClubEnums.Roof.BlockA;

            Assert.Equal(0, club.BlockAWeeks);
        }

        [Fact]
        public void BlockAStandings_ChangedAfterInitialisation_ForcesBlockAWeeksToAtLeastOne()
        {
            Club club = new Club { Initilisation = false };

            club.BlockAStandings = 100;

            Assert.True(club.BlockAWeeks >= 1);
        }
    }
}
