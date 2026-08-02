using System.ComponentModel;
using System.Diagnostics;
using System.Collections.Generic;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for the Game model's Process/IsRunning tracking.</summary>
    public class GameTests
    {
        [Fact]
        public void Process_Set_ReturnsSameProcess()
        {
            Game game = new Game();
            Process current = Process.GetCurrentProcess();

            game.Process = current;

            Assert.Same(current, game.Process);
        }

        [Fact]
        public void IsRunning_Set_RaisesPropertyChanged()
        {
            Game game = new Game();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)game).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            game.IsRunning = true;

            Assert.True(game.IsRunning);
            Assert.Contains(nameof(Game.IsRunning), raised);
        }
    }
}
