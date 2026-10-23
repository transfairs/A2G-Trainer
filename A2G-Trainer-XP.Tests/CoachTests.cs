using System.Collections.Generic;
using System.ComponentModel;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for the Coach model's field truncation and change-notification behavior.</summary>
    public class CoachTests
    {
        [Fact]
        public void Firstname_LongerThanLimit_IsTruncated()
        {
            Coach coach = new Coach { Firstname = "Maximilian1234" };

            Assert.Equal("Maximilia", coach.Firstname);
        }

        [Fact]
        public void Lastname_LongerThanLimit_IsTruncated()
        {
            Coach coach = new Coach { Lastname = "AVeryLongLastnameIndeed" };

            Assert.Equal(15, coach.Lastname.Length);
        }

        [Fact]
        public void Age_Set_RaisesPropertyChanged()
        {
            Coach coach = new Coach();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            coach.Age = 42;

            Assert.Equal((byte)42, coach.Age);
            Assert.Contains(nameof(Coach.Age), raised);
        }

        [Fact]
        public void Level_Set_RaisesLevelChangeNotification()
        {
            Coach coach = new Coach();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            coach.Level = 7;

            Assert.Equal((byte)7, coach.Level);
            Assert.Contains(nameof(Coach.Level), raised);
        }

        [Fact]
        public void Difficulty_Set_RaisesPropertyChanged()
        {
            Coach coach = new Coach();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            coach.Difficulty = CoachEnums.Difficulty.UltraViolence;

            Assert.Equal(CoachEnums.Difficulty.UltraViolence, coach.Difficulty);
            Assert.Contains(nameof(Coach.Difficulty), raised);
        }

        [Fact]
        public void Games_Set_RaisesPropertyChangedForGamesAndWinPercentage()
        {
            Coach coach = new Coach();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            // A value beyond byte range (0-255) to prove Games actually holds a ushort, since the
            // in-game field is 2 bytes wide - a regression back to byte would silently truncate this.
            coach.Games = 300;

            Assert.Equal((ushort)300, coach.Games);
            Assert.Contains(nameof(Coach.Games), raised);
            Assert.Contains(nameof(Coach.WinPercentage), raised);
        }

        [Fact]
        public void Wins_Set_RaisesPropertyChangedForWinsAndWinPercentage()
        {
            Coach coach = new Coach();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            // Beyond byte range, same rationale as Games above.
            coach.Wins = 260;

            Assert.Equal((ushort)260, coach.Wins);
            Assert.Contains(nameof(Coach.Wins), raised);
            Assert.Contains(nameof(Coach.WinPercentage), raised);
        }

        [Fact]
        public void WinPercentage_WithNoGamesPlayed_IsZero()
        {
            Coach coach = new Coach { Wins = 0, Games = 0 };

            Assert.Equal(0d, coach.WinPercentage);
        }

        [Fact]
        public void WinPercentage_ComputesShareOfGamesWon()
        {
            Coach coach = new Coach { Games = 400, Wins = 100 };

            Assert.Equal(25d, coach.WinPercentage);
        }

        [Fact]
        public void CountryNextSeason_Set_RaisesPropertyChanged()
        {
            Coach coach = new Coach();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            coach.CountryNextSeason = PlayerEnums.Country.Italien;

            Assert.Equal(PlayerEnums.Country.Italien, coach.CountryNextSeason);
            Assert.Contains(nameof(Coach.CountryNextSeason), raised);
        }

        [Fact]
        public void ClubIdNextSeason_Set_RaisesPropertyChanged()
        {
            Coach coach = new Coach();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            coach.ClubIdNextSeason = 42;

            Assert.Equal((byte)42, coach.ClubIdNextSeason);
            Assert.Contains(nameof(Coach.ClubIdNextSeason), raised);
        }

        [Fact]
        public void ReleaseClause_Set_RaisesPropertyChangedForTheByteAndBothFlags()
        {
            Coach coach = new Coach();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            coach.ReleaseClause = 0x81;

            Assert.True(coach.HasReleaseClause);
            Assert.True(coach.IsReleaseClausePublished);
            Assert.Contains(nameof(Coach.ReleaseClause), raised);
            Assert.Contains(nameof(Coach.HasReleaseClause), raised);
            Assert.Contains(nameof(Coach.IsReleaseClausePublished), raised);
        }

        [Fact]
        public void IsReleaseClausePublished_SetTrue_AlsoSetsTheClause()
        {
            Coach coach = new Coach();

            coach.IsReleaseClausePublished = true;

            Assert.Equal((byte)0x81, coach.ReleaseClause);
        }

        [Fact]
        public void IsReleaseClausePublished_SetFalse_KeepsTheClause()
        {
            Coach coach = new Coach { ReleaseClause = 0x81 };

            coach.IsReleaseClausePublished = false;

            Assert.Equal((byte)0x01, coach.ReleaseClause);
        }

        [Fact]
        public void HasReleaseClause_SetFalse_AlsoWithdrawsTheAnnouncement_ButKeepsUnrelatedBits()
        {
            Coach coach = new Coach { ReleaseClause = 0x85 };

            coach.HasReleaseClause = false;

            Assert.Equal((byte)0x04, coach.ReleaseClause);
            Assert.False(coach.IsReleaseClausePublished);
        }

        [Fact]
        public void HasReleaseClause_SetTrue_DoesNotPublish()
        {
            Coach coach = new Coach();

            coach.HasReleaseClause = true;

            Assert.Equal((byte)0x01, coach.ReleaseClause);
        }

        [Theory]
        [InlineData((byte)0x00, false, (ushort)3, false)]  // long contract, nothing else: offer can't be accepted
        [InlineData((byte)0x01, false, (ushort)3, true)]   // release clause
        [InlineData((byte)0x00, true, (ushort)3, true)]    // board release
        [InlineData((byte)0x00, false, (ushort)1, true)]   // contract running out
        public void CanNegotiateOffers_FollowsTheGamesCheck(byte clause, bool boardRelease, ushort yearsLeft, bool expected)
        {
            Coach coach = new Coach { ReleaseClause = clause, HasBoardRelease = boardRelease, ContractYearsLeft = yearsLeft };

            Assert.Equal(expected, coach.CanNegotiateOffers);
        }

        [Fact]
        public void CurrentCountryAndClubId_Set_RaisePropertyChanged()
        {
            Coach coach = new Coach();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            coach.CurrentCountry = PlayerEnums.Country.Deutschland;
            coach.CurrentClubId = 55;

            Assert.Equal(PlayerEnums.Country.Deutschland, coach.CurrentCountry);
            Assert.Equal((byte)55, coach.CurrentClubId);
            Assert.Contains(nameof(Coach.CurrentCountry), raised);
            Assert.Contains(nameof(Coach.CurrentClubId), raised);
        }
    }
}
