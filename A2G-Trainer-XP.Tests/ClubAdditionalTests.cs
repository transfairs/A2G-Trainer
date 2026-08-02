using System;
using System.Linq;
using System.Reflection;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for Club's remaining gaps: the twelve IsBlockXRoof boolean convenience properties
    /// (driven generically via reflection, same rationale as PlayerBooleanPropertiesTests),
    /// SquadAddress, IsClubMember, and the Setter overloads' helper-name/Initilisation branches
    /// that no current production caller happens to exercise with a non-default argument.
    /// </summary>
    public class ClubAdditionalTests
    {
        [Fact]
        public void EveryBooleanConvenienceProperty_ToggleRoundTrips_WithoutThrowing()
        {
            Club club = new Club();
            PropertyInfo[] boolProperties = typeof(Club).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.PropertyType == typeof(bool) && p.CanRead && p.CanWrite)
                .ToArray();

            Assert.True(boolProperties.Length >= 12, "Expected all twelve IsBlockXRoof properties to be found via reflection.");

            foreach (PropertyInfo property in boolProperties)
            {
                property.SetValue(club, true);
                property.GetValue(club);
                property.SetValue(club, false);
                property.GetValue(club);
            }
        }

        [Fact]
        public void SquadAddress_Set_RaisesPropertyChanged()
        {
            Club club = new Club();
            var raised = new System.Collections.Generic.List<string>();
            ((System.ComponentModel.INotifyPropertyChanged)club).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            club.SquadAddress = "1234";

            Assert.Equal("1234", club.SquadAddress);
            Assert.Contains(nameof(Club.SquadAddress), raised);
        }

        [Fact]
        public void IsClubMember_WithNullPlayer_ReturnsFalse()
        {
            Club club = new Club();

            Assert.False(club.IsClubMember(null));
        }

        [Fact]
        public void IsClubMember_WithMatchingCountryAndId_ReturnsTrue()
        {
            Club club = new Club { Id = 5, Country = PlayerEnums.Country.Deutschland };
            Player player = new Player { ClubId = 5, ClubCountry = PlayerEnums.Country.Deutschland };

            Assert.True(club.IsClubMember(player));
        }

        [Fact]
        public void IsClubMember_WithDifferentClubId_ReturnsFalse()
        {
            Club club = new Club { Id = 5, Country = PlayerEnums.Country.Deutschland };
            Player player = new Player { ClubId = 6, ClubCountry = PlayerEnums.Country.Deutschland };

            Assert.False(club.IsClubMember(player));
        }

        [Fact]
        public void IsClubMember_WithDifferentCountry_ReturnsFalse()
        {
            Club club = new Club { Id = 5, Country = PlayerEnums.Country.Deutschland };
            Player player = new Player { ClubId = 5, ClubCountry = PlayerEnums.Country.England };

            Assert.False(club.IsClubMember(player));
        }

        [Fact]
        public void Roof_SetAfterInitilisation_BumpsBlockAWeeksToAtLeastOne()
        {
            Club club = new Club { Initilisation = false, BlockAWeeks = 0 };

            club.Roof = ClubEnums.Roof.BlockC;

            Assert.Equal((byte)1, club.BlockAWeeks);
        }

        [Fact]
        public void Roof_SetAfterInitilisation_LeavesAnAlreadyPositiveBlockAWeeksUnchanged()
        {
            Club club = new Club { Initilisation = false, BlockAWeeks = 5 };

            club.Roof = ClubEnums.Roof.BlockC;

            Assert.Equal((byte)5, club.BlockAWeeks);
        }

        [Fact]
        public void Roof_SetDuringInitilisation_DoesNotTouchBlockAWeeks()
        {
            Club club = new Club { Initilisation = true, BlockAWeeks = 0 };

            club.Roof = ClubEnums.Roof.BlockC;

            Assert.Equal((byte)0, club.BlockAWeeks);
        }

        [Fact]
        public void StadiumName_LongerThanTwentyEight_IsTruncated()
        {
            Club club = new Club();

            club.StadiumName = new string('A', 40);

            Assert.Equal(28, club.StadiumName.Length);
        }

        // DisplayUnit/FieldCondition/Roof's private Setter(..., bool enabled, string helper) overloads
        // are only ever called through their plain property setters, which always pass the default
        // (enabled: true, helper: null) - these are invoked directly to also cover the "disabled" and
        // "named helper" branches that no current caller reaches.
        [Fact]
        public void DisplayUnitSetter_WithHelperName_RaisesTheHelperPropertyToo()
        {
            Club club = new Club();
            var raised = new System.Collections.Generic.List<string>();
            ((System.ComponentModel.INotifyPropertyChanged)club).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            InvokeSetter(club, ClubEnums.DisplayUnit.GrosseLED, true, "SomeHelper");

            Assert.Contains("SomeHelper", raised);
        }

        [Fact]
        public void FieldConditionSetter_WithHelperName_RaisesTheHelperPropertyToo()
        {
            Club club = new Club();
            var raised = new System.Collections.Generic.List<string>();
            ((System.ComponentModel.INotifyPropertyChanged)club).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            InvokeSetter(club, ClubEnums.FieldCondition.Clean, true, "SomeHelper");

            Assert.Contains("SomeHelper", raised);
        }

        [Fact]
        public void RoofSetter_WithHelperName_RaisesTheHelperPropertyToo()
        {
            Club club = new Club { Initilisation = true };
            var raised = new System.Collections.Generic.List<string>();
            ((System.ComponentModel.INotifyPropertyChanged)club).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            InvokeSetter(club, ClubEnums.Roof.BlockA, true, "SomeHelper");

            Assert.Contains("SomeHelper", raised);
        }

        private static void InvokeSetter<TEnum>(Club club, TEnum value, bool enabled, string helper)
        {
            MethodInfo method = typeof(Club).GetMethod("Setter", BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(TEnum), typeof(bool), typeof(string) }, null);
            method.Invoke(club, new object[] { value, enabled, helper });
        }
    }
}
