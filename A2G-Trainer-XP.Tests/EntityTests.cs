using System.Collections.Generic;
using System.ComponentModel;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    public class EntityTests
    {
        [Fact]
        public void Offset_DefaultsToEmptyString()
        {
            Player player = new Player();

            Assert.Equal(string.Empty, player.Offset);
        }

        [Fact]
        public void Offset_Set_RaisesPropertyChanged()
        {
            Player player = new Player();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)player).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            player.Offset = "1A2B";

            Assert.Equal("1A2B", player.Offset);
            Assert.Contains(nameof(Player.Offset), raised);
        }
    }
}
