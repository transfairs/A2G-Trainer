using System.Collections.Generic;
using System.ComponentModel;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for the LeagueSettings/BonusCountry models.</summary>
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
        public void LeagueCountries_AreTheFiveCountriesWithALeagueSystem()
        {
            Assert.Equal(new[] { PlayerEnums.Country.Deutschland, PlayerEnums.Country.England, PlayerEnums.Country.Frankreich, PlayerEnums.Country.Italien, PlayerEnums.Country.Spanien },
                LeagueSettings.LeagueCountries);
            Assert.Equal(4, LeagueSettings.MaxBonusCountries);
        }

        [Fact]
        public void BonusCountry_StartsAsReadAndRaisesPropertyChangedWhenToggled()
        {
            BonusCountry entry = new BonusCountry(PlayerEnums.Country.Spanien, wasActive: true, leagueSystem: 4, toggleBlockedReason: null);
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)entry).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            Assert.True(entry.IsActive);
            Assert.True(entry.CanToggle);

            entry.IsActive = false;

            Assert.False(entry.IsActive);
            Assert.True(entry.WasActive);
            Assert.Equal((short)4, entry.LeagueSystem);
            Assert.Contains(nameof(BonusCountry.IsActive), raised);
        }

        [Fact]
        public void BonusCountry_WithReason_CannotBeToggled()
        {
            BonusCountry entry = new BonusCountry(PlayerEnums.Country.Italien, wasActive: false, leagueSystem: 3, toggleBlockedReason: "keine Daten");

            Assert.False(entry.CanToggle);
            Assert.Equal("keine Daten", entry.ToggleBlockedReason);
        }

        [Fact]
        public void ActiveBonusCountries_ListsOnlyActiveEntriesInOrder()
        {
            LeagueSettings league = new LeagueSettings();
            league.BonusCountries.Add(new BonusCountry(PlayerEnums.Country.Spanien, true, 4, null));
            league.BonusCountries.Add(new BonusCountry(PlayerEnums.Country.England, false, 1, null));
            league.BonusCountries.Add(new BonusCountry(PlayerEnums.Country.Italien, true, 3, null));

            Assert.Equal(new[] { PlayerEnums.Country.Spanien, PlayerEnums.Country.Italien }, league.ActiveBonusCountries);
        }
    }
}
