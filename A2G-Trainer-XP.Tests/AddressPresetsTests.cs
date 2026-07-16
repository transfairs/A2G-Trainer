using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    public class AddressPresetsTests
    {
        [Fact]
        public void From_Club_OwnAndOpponent_ReturnDistinctPresets()
        {
            Addresses own = AddressPresets.From(PlayerEnums.AddressType.OWN, isClub: true);
            Addresses opponent = AddressPresets.From(PlayerEnums.AddressType.OPPONENT, isClub: true);

            Assert.Equal("0", own[ClubEnums.AddressKey.NAME]);
            Assert.Equal("14E0", opponent[ClubEnums.AddressKey.NAME]);
        }

        [Fact]
        public void From_Club_TraineeAndDynamic_BothResolveToOpponentPreset()
        {
            Addresses opponent = AddressPresets.From(PlayerEnums.AddressType.OPPONENT, isClub: true);
            Addresses trainee = AddressPresets.From(PlayerEnums.AddressType.TRAINEE, isClub: true);
            Addresses dynamic = AddressPresets.From(PlayerEnums.AddressType.DYNAMIC, isClub: true);

            Assert.Same(opponent, trainee);
            Assert.Same(opponent, dynamic);
        }

        [Fact]
        public void From_Club_AllAndNonPlayable_ReturnExpectedPresets()
        {
            Addresses all = AddressPresets.From(PlayerEnums.AddressType.ALL, isClub: true);
            Addresses nonPlayable = AddressPresets.From(PlayerEnums.AddressType.NON_PLAYABLE, isClub: true);

            Assert.Equal("DE", all[ClubEnums.AddressKey.PLAYER_COUNT]);
            Assert.Equal("D8", nonPlayable[ClubEnums.AddressKey.PLAYER_COUNT]);
        }

        [Fact]
        public void From_Club_UnknownType_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => AddressPresets.From((PlayerEnums.AddressType)999, isClub: true));
        }

        [Fact]
        public void From_Players_UnknownType_Throws()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => AddressPresets.From((PlayerEnums.AddressType)999, isClub: false));
        }

        [Fact]
        public void From_Players_Own_ReturnsOwnPlayerPreset()
        {
            Addresses own = AddressPresets.From(PlayerEnums.AddressType.OWN, isClub: false);

            Assert.Equal("0", own[PlayerEnums.AddressKey.ID]);
            Assert.Equal("1E", own[PlayerEnums.AddressKey.AGE]);
        }

        [Fact]
        public void InitPreset_OffsetsEveryOwnPlayerAddress_ByPlayerOffsetPlusLastPlayer()
        {
            // playerOffset ("178") + lastPlayer ("A") = "182"; every OWN_PLAYERS value shifts by that.
            Addresses preset = AddressPresets.InitPreset("A");

            Assert.Equal("182", preset[PlayerEnums.AddressKey.ID]);
            Assert.Equal("1A0", preset[PlayerEnums.AddressKey.AGE]); // 0x1E + 0x182 = 0x1A0
        }

        [Fact]
        public void InitOpponent_PopulatesOpponentPlayersFromInitPreset()
        {
            AddressPresets.InitOpponent("A");

            Addresses expected = AddressPresets.InitPreset("A");
            Addresses opponent = AddressPresets.From(PlayerEnums.AddressType.OPPONENT, isClub: false);

            Assert.Equal(expected[PlayerEnums.AddressKey.ID], opponent[PlayerEnums.AddressKey.ID]);
        }

        [Fact]
        public void InitDynamicTeam_PopulatesDynamicPlayersFromInitPreset()
        {
            AddressPresets.InitDynamicTeam("A");

            Addresses expected = AddressPresets.InitPreset("A");
            Addresses dynamic = AddressPresets.From(PlayerEnums.AddressType.DYNAMIC, isClub: false);

            Assert.Equal(expected[PlayerEnums.AddressKey.ID], dynamic[PlayerEnums.AddressKey.ID]);
        }
    }
}
