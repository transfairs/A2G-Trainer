using System.Collections.Generic;
using System.ComponentModel;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
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
    }
}
