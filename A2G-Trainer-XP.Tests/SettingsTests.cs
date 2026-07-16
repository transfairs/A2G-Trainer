using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    public class SettingsTests
    {
        [Fact]
        public void PlayerAddress_SecondEntry_IsBaseAddressPlusGogOffset()
        {
            Assert.Equal("0x423690", Settings.PlayerAddress[0]);
            Assert.Equal(Tools.SumHex(new[] { "0x423690", "3140" }), Settings.PlayerAddress[1]);
        }

        [Fact]
        public void ClubAddress_SecondEntry_IsBaseAddressPlusGogOffset()
        {
            Assert.Equal("0x400710", Settings.ClubAddress[0]);
            Assert.Equal(Tools.SumHex(new[] { "0x400710", "3140" }), Settings.ClubAddress[1]);
        }

        [Fact]
        public void AllClubInitialOffset_TargetsAllAddressType()
        {
            Assert.Equal(PlayerEnums.AddressType.ALL, Settings.AllClubInitialOffset.Value.Item2);
            Assert.Equal((ushort)294, Settings.AllClubInitialOffset.Value.Item1);
        }

        [Fact]
        public void NonPlayableInitialOffset_TargetsNonPlayableAddressType()
        {
            Assert.Equal(PlayerEnums.AddressType.NON_PLAYABLE, Settings.NonPlayableInitialOffset.Value.Item2);
            Assert.Equal((ushort)100, Settings.NonPlayableInitialOffset.Value.Item1);
        }
    }
}
