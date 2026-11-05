using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for the kind of injury (player byte +0x46): the names from the game's table at
    /// 0x6C78F8, 1-61 injuries and 65-81 illnesses, and Player.InjuryKind over the stored byte.
    /// </summary>
    public class InjuryKindTests
    {
        [Theory]
        [InlineData(1, "Achillessehnenanriß")]
        [InlineData(15, "Innenbanddehnung")]
        [InlineData(21, "Kapselriß im Knöchel")]
        [InlineData(39, "Muskelfaserriß")]
        [InlineData(61, "Rückenprellung")]
        [InlineData(65, "Angina")]
        [InlineData(81, "Sonnenstich")]
        public void Injury_UsesTheGamesNames(byte value, string expected)
        {
            Assert.Equal(expected, PlayerEnums.GetDescription((PlayerEnums.Injury)value));
        }

        [Fact]
        public void Injury_HasNoValuesTheGameLeavesUnused()
        {
            byte[] values = Enum.GetValues(typeof(PlayerEnums.Injury)).Cast<byte>().ToArray();

            Assert.Equal(Enumerable.Range(0, 62).Concat(Enumerable.Range(65, 17)).Select(v => (byte)v), values);
        }

        [Fact]
        public void InjuryKind_MapsOntoTheStoredByteAndRaisesBoth()
        {
            Player player = new Player();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)player).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            player.InjuryKind = PlayerEnums.Injury.Kreuzbandriss;

            Assert.Equal((byte)30, player.Injury);
            Assert.Contains(nameof(Player.Injury), raised);
            Assert.Contains(nameof(Player.InjuryKind), raised);
            player.Injury = 70;
            Assert.Equal(PlayerEnums.Injury.Hexenschuss, player.InjuryKind);
        }
    }
}
