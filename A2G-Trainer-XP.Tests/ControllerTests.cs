using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    // These exercise the controllers with a ProcessMemory that was never attached to a real
    // process. Club/CoachController guard their memory reads behind "MainModule != null", so
    // construction succeeds and yields default-valued entities - which is enough to verify the
    // offset arithmetic and preset wiring without a live game process.
    public class ControllerTests
    {
        [Fact]
        public void ClubController_WithoutLiveProcess_ReturnsDefaultClub_UsingOwnClubPreset()
        {
            ProcessMemory memory = new ProcessMemory();

            ClubController controller = new ClubController(memory, isGog: false, PlayerEnums.AddressType.OWN);

            Assert.NotNull(controller.Club);
            Assert.Equal(0, controller.Club.PlayerCount);
            Assert.Equal(string.Empty, controller.Club.ClubName);
            Assert.Same(AddressPresets.OWN_CLUB, controller.Club.Addresses);
        }

        [Fact]
        public void ClubController_OpponentType_UsesOpponentClubPreset()
        {
            ProcessMemory memory = new ProcessMemory();

            ClubController controller = new ClubController(memory, isGog: false, PlayerEnums.AddressType.OPPONENT);

            Assert.Same(AddressPresets.OPPONENT_CLUB, controller.Club.Addresses);
        }

        [Fact]
        public void CoachController_WithoutLiveProcess_ReturnsDefaultCoach()
        {
            ProcessMemory memory = new ProcessMemory();

            CoachController controller = new CoachController(memory, isGog: false, PlayerEnums.AddressType.OWN);

            Assert.NotNull(controller.Coach);
            Assert.Equal(string.Empty, controller.Coach.Firstname);
            Assert.Equal(0, controller.Coach.Age);
            Assert.Same(AddressPresets.COACH, controller.Coach.Addresses);
        }

        [Fact]
        public void PlayerController_WithEmptyClub_ProducesEmptyListWithoutTouchingMemory()
        {
            ProcessMemory memory = new ProcessMemory();
            Club emptyClub = new Club(); // PlayerCount == AmateurPlayerCount == 0 -> refresh loop never runs

            PlayerController controller = new PlayerController(memory, emptyClub, isGog: false, PlayerEnums.AddressType.OWN);

            Assert.Empty(controller.EntityList);
        }

        [Fact]
        public void PlayerController_OwnType_WithEmptyClub_InitialisesOpponentAndDynamicPresets()
        {
            ProcessMemory memory = new ProcessMemory();
            Club emptyClub = new Club();

            PlayerController controller = new PlayerController(memory, emptyClub, isGog: false, PlayerEnums.AddressType.OWN);

            Assert.Equal(string.Empty, controller.OpponentOffset);
            Assert.Equal(string.Empty, controller.OtherOffset);

            // playerOffset ("178") + "" == "178"; OWN_PLAYERS shifted by that offset.
            Addresses expected = AddressPresets.InitPreset(string.Empty);
            Assert.Equal(expected[PlayerEnums.AddressKey.ID], AddressPresets.OPPONENT_PLAYERS[PlayerEnums.AddressKey.ID]);
            Assert.Equal(expected[PlayerEnums.AddressKey.ID], AddressPresets.DYNAMIC_PLAYERS[PlayerEnums.AddressKey.ID]);
        }

        [Fact]
        public void PlayerController_OpponentType_DoesNotTouchOpponentPreset()
        {
            ProcessMemory memory = new ProcessMemory();
            Club emptyClub = new Club();

            // Sanity baseline, then confirm the OPPONENT path leaves AddressPresets.OPPONENT_PLAYERS alone.
            AddressPresets.InitOpponent("5");
            Addresses before = AddressPresets.OPPONENT_PLAYERS;

            new PlayerController(memory, emptyClub, isGog: false, PlayerEnums.AddressType.OPPONENT);

            Assert.Same(before, AddressPresets.OPPONENT_PLAYERS);
        }
    }
}
