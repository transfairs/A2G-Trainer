using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for the Club model's roof-bitmask helpers and stadium-block setters.</summary>
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

        // Stadium edits used to put block A under construction for a week (so the game's weekly run
        // recounted the capacity); the Trainer now recounts the capacity itself on save.
        [Fact]
        public void StadiumChanges_AfterInitialisation_LeaveTheConstructionWeeksAlone()
        {
            Club club = new Club { Initilisation = false };

            club.Roof = ClubEnums.Roof.BlockA;
            club.IsBlockCRoof = true;
            club.BlockAStandings = 100;
            club.BlockFSeats = 200;

            Assert.Equal(0, club.BlockAWeeks);
            Assert.Equal(0, club.BlockFWeeks);
        }

        [Fact]
        public void OpenCapacity_CountsStandingAndSeatsOfBlocksNotUnderConstruction()
        {
            Club club = new Club
            {
                BlockAStandings = 1000, BlockASeats = 500,
                BlockBStandings = 2000, BlockBSeats = 700, BlockBWeeks = 3,
                BlockLStandings = 10, BlockLSeats = 20,
            };

            Assert.Equal(1530, club.OpenCapacity());

            club.BlockBWeeks = 0;
            Assert.Equal(4230, club.OpenCapacity());

            // The game counts the places as Int16 - the "negative places" trick.
            club.BlockLSeats = 65516;
            Assert.Equal(4190, club.OpenCapacity());
        }

        [Fact]
        public void FieldCondition_RaisesItsOwnPropertyChanged()
        {
            Club club = new Club();
            var changed = new System.Collections.Generic.List<string>();
            club.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            club.FieldCondition = ClubEnums.FieldCondition.Holprig;

            Assert.Contains(nameof(Club.FieldCondition), changed);
            Assert.DoesNotContain(nameof(Club.DisplayUnit), changed);
        }
    }
}
