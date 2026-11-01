using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for the shirt-number check before saving (PlayerController.JerseyProblem): the rules
    /// the game's own "Rückennummern" dialog applies, 1-99 and each number once per squad.
    /// </summary>
    public class PlayerJerseyTests
    {
        private static Player P(string lastname, byte jersey) => new Player { Lastname = lastname, Firstname = "A", Jersey = jersey };

        [Fact]
        public void JerseyProblem_WithDistinctNumbers_IsNull()
        {
            Assert.Null(PlayerController.JerseyProblem(new[] { P("Kahn", 1), P("Basler", 14), P("Elber", 99) }, PlayerEnums.AddressType.OWN));
        }

        [Fact]
        public void JerseyProblem_WithANumberTwice_NamesBothPlayers()
        {
            string problem = PlayerController.JerseyProblem(new[] { P("Kahn", 1), P("Basler", 14), P("Elber", 14) }, PlayerEnums.AddressType.OWN);

            Assert.Equal("Bitte jede Rückennummer nur einmal vergeben! (Nr. 14: Basler, A, Elber, A)", problem);
        }

        [Fact]
        public void JerseyProblem_AboveNinetyNine_IsRefused()
        {
            string problem = PlayerController.JerseyProblem(new[] { P("Kahn", 1), P("Basler", 100) }, PlayerEnums.AddressType.OPPONENT);

            Assert.Equal("Rückennummern gehen von 1 bis 99 (Basler, A: 100).", problem);
        }

        // 0 = no number yet (e.g. right after a transfer); the game fills it in, several may have it.
        [Fact]
        public void JerseyProblem_WithPlayersWithoutNumber_IsNull()
        {
            Assert.Null(PlayerController.JerseyProblem(new[] { P("Kahn", 0), P("Basler", 0), P("Elber", 9) }, PlayerEnums.AddressType.OWN));
        }

        [Fact]
        public void JerseyProblem_ForYouthPlayers_IsNotChecked()
        {
            Assert.Null(PlayerController.JerseyProblem(new[] { P("Kahn", 7), P("Basler", 7) }, PlayerEnums.AddressType.TRAINEE));
        }
    }
}
