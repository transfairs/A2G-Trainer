using System.Linq;
using System.Reflection;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for Player's large block of checkbox-style boolean convenience properties (HasX/IsX),
    /// each a thin get/set wrapper around a shared enum bitmask via the private Setter/Multiplex
    /// helpers. Rather than one hand-written test per property (~80 of them, all following the
    /// same two patterns), this drives every one generically through reflection - toggling true
    /// then false exercises both the "set/add the flag" and "clear the flag" branches of whichever
    /// helper it forwards to.
    /// </summary>
    public class PlayerBooleanPropertiesTests
    {
        [Fact]
        public void EveryBooleanConvenienceProperty_ToggleRoundTrips_WithoutThrowing()
        {
            Player player = new Player();
            PropertyInfo[] boolProperties = typeof(Player).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(bool) && p.CanRead && p.CanWrite)
                .ToArray();

            Assert.True(boolProperties.Length > 50, "Expected the full set of Player's boolean convenience properties to be found via reflection.");

            foreach (PropertyInfo property in boolProperties)
            {
                property.SetValue(player, true);
                property.GetValue(player);
                property.SetValue(player, false);
                property.GetValue(player);
            }
        }

        [Fact]
        public void TalentLabel_BelowTwentyFive_ReadsPlainTalent()
        {
            Player player = new Player { Age = 20 };

            Assert.Equal("Talent", player.TalentLabel);
        }

        [Fact]
        public void TalentLabel_TwentyFiveOrOlder_ReadsEwigesTalent()
        {
            Player player = new Player { Age = 25 };

            Assert.Equal("ewiges Talent", player.TalentLabel);
        }

        [Fact]
        public void SecondaryPositions_SetWithMoreThanTwo_TrimsDownToTheLastTwo()
        {
            Player player = new Player
            {
                SecondaryPositions = new System.Collections.Generic.List<PlayerEnums.Position>
                {
                    PlayerEnums.Position.LV, PlayerEnums.Position.RV, PlayerEnums.Position.DM
                }
            };

            Assert.Equal(new[] { PlayerEnums.Position.RV, PlayerEnums.Position.DM }, player.SecondaryPositions);
        }

        [Fact]
        public void IsSecondaryTO_WhenSecondaryPositionsWasNull_InitialisesTheListBeforeAdding()
        {
            Player player = new Player { SecondaryPositions = null };

            player.IsSecondaryTO = true;

            Assert.Equal(new[] { PlayerEnums.Position.TO }, player.SecondaryPositions);
        }

        [Fact]
        public void IsSecondaryTO_SetFalse_WhenSecondaryPositionsWasNull_DoesNotThrow()
        {
            Player player = new Player { SecondaryPositions = null };

            System.Exception thrown = Record.Exception(() => player.IsSecondaryTO = false);

            Assert.Null(thrown);
        }

        // The ContractDetails/Career Setter(..., bool enabled, string helper) overloads are only
        // ever called through their plain property setters, which always pass the default
        // (enabled: true, helper: null) - invoked directly here to also cover the "named helper"
        // branch no current caller reaches (same rationale as ClubAdditionalTests' DisplayUnit/
        // FieldCondition/Roof Setter coverage).
        [Fact]
        public void ContractDetailsSetter_WithHelperName_RaisesTheHelperPropertyToo()
        {
            Player player = new Player();
            var raised = new System.Collections.Generic.List<string>();
            ((System.ComponentModel.INotifyPropertyChanged)player).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            InvokeSetter(player, PlayerEnums.Contract.Leased, true, "SomeHelper");

            Assert.Contains("SomeHelper", raised);
        }

        [Fact]
        public void CareerSetter_WithHelperName_RaisesTheHelperPropertyToo()
        {
            Player player = new Player();
            var raised = new System.Collections.Generic.List<string>();
            ((System.ComponentModel.INotifyPropertyChanged)player).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            InvokeSetter(player, PlayerEnums.Career.Retires, true, "SomeHelper");

            Assert.Contains("SomeHelper", raised);
        }

        private static void InvokeSetter<TEnum>(Player player, TEnum value, bool enabled, string helper)
        {
            MethodInfo method = typeof(Player).GetMethod("Setter", BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(TEnum), typeof(bool), typeof(string) }, null);
            method.Invoke(player, new object[] { value, enabled, helper });
        }
    }
}
