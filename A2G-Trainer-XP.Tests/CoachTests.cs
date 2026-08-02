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
    }
}
