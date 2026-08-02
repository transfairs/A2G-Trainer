using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for the Player model's trait-flag helper properties and position/skill setters.</summary>
    public class PlayerTests
    {
        private static List<string> TrackChanges(Player player)
        {
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)player).PropertyChanged += (s, e) => raised.Add(e.PropertyName);
            return raised;
        }

        [Fact]
        public void Constructor_InitialisesTwoEmptySecondaryPositions()
        {
            Player player = new Player();

            Assert.Equal(new[] { PlayerEnums.Position.None, PlayerEnums.Position.None }, player.SecondaryPositions);
        }

        [Fact]
        public void ToString_FormatsAsLastnameCommaFirstname()
        {
            Player player = new Player { Lastname = "Klose", Firstname = "Miroslav" };

            Assert.Equal("Klose, Miroslav", player.ToString());
        }

        [Theory]
        [InlineData(20, "Talent")]
        [InlineData(25, "ewiges Talent")]
        [InlineData(30, "ewiges Talent")]
        public void TalentLabel_DependsOnAge(byte age, string expected)
        {
            Player player = new Player { Age = age };

            Assert.Equal(expected, player.TalentLabel);
        }

        [Fact]
        public void Position_SettingOneHelper_ClearsTheOthers()
        {
            Player player = new Player { IsL = true };

            Assert.True(player.IsL);
            Assert.False(player.IsTO);

            player.IsMD = true;

            Assert.True(player.IsMD);
            Assert.False(player.IsL);
        }

        [Fact]
        public void IsNotTO_TracksPosition()
        {
            Player player = new Player { IsTO = true };

            Assert.False(player.IsNotTO);

            player.IsL = true;

            Assert.True(player.IsNotTO);
        }

        [Fact]
        public void SecondaryPositions_AreClampedToTwo_KeepingTheMostRecent()
        {
            Player player = new Player
            {
                IsSecondaryTO = true,
                IsSecondaryL = true,
                IsSecondaryMD = true
            };

            Assert.Equal(2, player.SecondaryPositions.Count);
            Assert.False(player.IsSecondaryTO);
            Assert.True(player.IsSecondaryL);
            Assert.True(player.IsSecondaryMD);
        }

        [Fact]
        public void SecondaryPositions_SettingFalse_RemovesIt()
        {
            Player player = new Player { IsSecondaryL = true };
            Assert.True(player.IsSecondaryL);

            player.IsSecondaryL = false;

            Assert.False(player.IsSecondaryL);
        }

        [Fact]
        public void HasKopfball_IsMaskedByGoalkeeperPosition()
        {
            Player player = new Player { HasKopfball = true };

            Assert.True(player.HasKopfball);
            Assert.True(player.Skills.HasFlag(PlayerEnums.Skills.Kopfball));

            player.IsTO = true;

            // The flag is still set internally, but the helper hides it for goalkeepers.
            Assert.False(player.HasKopfball);
            Assert.True(player.Skills.HasFlag(PlayerEnums.Skills.Kopfball));
        }

        [Fact]
        public void HasElfmetertoeter_OnlyAppliesToGoalkeepers()
        {
            Player player = new Player { HasElfmetertoeter = true };

            // Not a goalkeeper yet, so the keeper-only skill does not show up.
            Assert.False(player.HasElfmetertoeter);

            player.IsTO = true;

            Assert.True(player.HasElfmetertoeter);
        }

        [Fact]
        public void PositiveAndNegativeSkills_AreIndependentBitfields()
        {
            Player player = new Player
            {
                HasZweikampf = true,
                HasNegZweikampf = true
            };

            Assert.True(player.HasZweikampf);
            Assert.True(player.HasNegZweikampf);

            player.HasZweikampf = false;

            Assert.False(player.HasZweikampf);
            Assert.True(player.HasNegZweikampf);
        }

        [Fact]
        public void Character_IsExclusive()
        {
            Player player = new Player { IsHitzkopf = true };

            Assert.True(player.IsHitzkopf);
            Assert.False(player.IsNormalChar);

            player.IsFrohnatur = true;

            Assert.True(player.IsFrohnatur);
            Assert.False(player.IsHitzkopf);
        }

        [Fact]
        public void ContractDetails_AreIndependentFlags()
        {
            Player player = new Player
            {
                IsLeased = true,
                HasBuyOption = true
            };

            Assert.True(player.IsLeased);
            Assert.True(player.HasBuyOption);

            player.IsLeased = false;

            Assert.False(player.IsLeased);
            Assert.True(player.HasBuyOption);
        }

        [Fact]
        public void Happy_And_Unhappy_AreIndependentFlagSets()
        {
            Player player = new Player
            {
                IsTollerVertrag = true,
                IsWillMehrGeld = true
            };

            Assert.True(player.IsTollerVertrag);
            Assert.True(player.IsWillMehrGeld);

            player.IsTollerVertrag = false;

            Assert.False(player.IsTollerVertrag);
            Assert.True(player.IsWillMehrGeld);
        }

        [Fact]
        public void HasDarkSkin_ReflectsSkinColor()
        {
            Player player = new Player { SkinColor = PlayerEnums.SkinColor.Dark };

            Assert.True(player.HasDarkSkin);
            Assert.False(player.HasBlackSkin);
        }

        [Fact]
        public void HasFairSkin_ReflectsSkinColor()
        {
            Player player = new Player { SkinColor = PlayerEnums.SkinColor.Black };

            Assert.False(player.HasFairSkin);

            player.SkinColor = PlayerEnums.SkinColor.Fair;

            Assert.True(player.HasFairSkin);
        }

        [Fact]
        public void HasLightBlondHair_ReflectsHairColor()
        {
            Player player = new Player { HairColor = PlayerEnums.HairColor.Schwarz };

            Assert.False(player.HasLightBlondHair);

            player.HairColor = PlayerEnums.HairColor.Hellblond;

            Assert.True(player.HasLightBlondHair);
        }

        [Fact]
        public void HasBlondHair_Set_ReplacesHairColor()
        {
            Player player = new Player { HairColor = PlayerEnums.HairColor.Schwarz };

            player.HasBlondHair = true;

            Assert.Equal(PlayerEnums.HairColor.Blond, player.HairColor);
        }

        [Fact]
        public void HasBlondHair_SetFalse_ClearsHairColorToNeutralDefault()
        {
            Player player = new Player { HairColor = PlayerEnums.HairColor.Blond };

            player.HasBlondHair = false;

            Assert.Equal(PlayerEnums.HairColor.Hellblond, player.HairColor);
            Assert.False(player.HasBlondHair);
        }

        [Fact]
        public void HasBlondHair_SetFalse_WhenNotCurrentlySelected_DoesNotDisturbOtherColor()
        {
            // "false" only clears the color if it is the one currently selected - it must not
            // clobber a sibling option in the same group (e.g. Schwarz) that happens to be active.
            Player player = new Player { HairColor = PlayerEnums.HairColor.Schwarz };

            player.HasBlondHair = false;

            Assert.Equal(PlayerEnums.HairColor.Schwarz, player.HairColor);
            Assert.True(player.HasBlackHair);
        }

        [Fact]
        public void HasDarkSkin_SetFalse_ClearsSkinColorToNeutralDefault()
        {
            Player player = new Player { SkinColor = PlayerEnums.SkinColor.Dark };

            player.HasDarkSkin = false;

            Assert.Equal(PlayerEnums.SkinColor.Fair, player.SkinColor);
            Assert.True(player.HasFairSkin);
        }

        [Fact]
        public void IsHitzkopf_SetFalse_ClearsCharacterToNormal()
        {
            Player player = new Player { IsHitzkopf = true };

            player.IsHitzkopf = false;

            Assert.True(player.IsNormalChar);
        }

        [Fact]
        public void IsRobust_SetFalse_ClearsHealthToNormal()
        {
            Player player = new Player { IsRobust = true };

            player.IsRobust = false;

            Assert.True(player.HasNormalHealth);
        }

        [Fact]
        public void IsTalent_SetFalse_ClearsPersonalityToNone()
        {
            Player player = new Player { IsTalent = true };

            player.IsTalent = false;

            Assert.True(player.IsNoone);
        }

        [Fact]
        public void IsL_SetFalse_ClearsPositionToNone()
        {
            Player player = new Player { IsL = true };

            player.IsL = false;

            Assert.Equal(PlayerEnums.Position.None, player.Position);
        }

        [Fact]
        public void Firstname_LongerThanLimit_IsTruncated()
        {
            Player player = new Player { Firstname = "Maximilian1234" };

            Assert.Equal("Maximilia", player.Firstname);
        }

        [Fact]
        public void Lastname_LongerThanLimit_IsTruncated()
        {
            Player player = new Player { Lastname = "AVeryLongLastnameIndeed" };

            Assert.Equal(15, player.Lastname.Length);
            Assert.Equal("AVeryLongLastna", player.Lastname);
        }

        [Fact]
        public void Firstname_SettingSameTruncatedValueAgain_DoesNotRaiseChange()
        {
            Player player = new Player { Firstname = "Maximilian1234" };
            Assert.Equal("Maximilia", player.Firstname);

            List<string> raised = TrackChanges(player);
            player.Firstname = "Maximilia";

            Assert.DoesNotContain(nameof(Player.Firstname), raised);
        }

        [Fact]
        public void Firstname_SettingDifferentValue_RaisesChange()
        {
            Player player = new Player();
            List<string> raised = TrackChanges(player);

            player.Firstname = "Max";

            Assert.Equal("Max", player.Firstname);
            Assert.Contains(nameof(Player.Firstname), raised);
        }
    }
}
