using System.Collections.Generic;
using System.ComponentModel;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for the LeagueSettings/AdditionalCountry models.</summary>
    public class LeagueSettingsTests
    {
        [Fact]
        public void MainCountry_Set_RaisesPropertyChanged()
        {
            LeagueSettings league = new LeagueSettings();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)league).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            league.MainCountry = PlayerEnums.Country.England;

            Assert.Equal(PlayerEnums.Country.England, league.MainCountry);
            Assert.Contains(nameof(LeagueSettings.MainCountry), raised);
        }

        [Fact]
        public void AdditionalCountries_HasFourSlots()
        {
            LeagueSettings league = new LeagueSettings();

            Assert.Equal(4, league.AdditionalCountries.Length);
        }

        [Fact]
        public void AdditionalCountry_Country_DefaultsToNull()
        {
            AdditionalCountry slot = new AdditionalCountry();

            Assert.Null(slot.Country);
        }

        [Fact]
        public void AdditionalCountry_Country_Set_RaisesPropertyChanged()
        {
            AdditionalCountry slot = new AdditionalCountry();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)slot).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            slot.Country = PlayerEnums.Country.Spanien;

            Assert.Equal(PlayerEnums.Country.Spanien, slot.Country);
            Assert.Contains(nameof(AdditionalCountry.Country), raised);
        }
    }
}
