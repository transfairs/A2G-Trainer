using System.Collections.Generic;
using System.ComponentModel;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for Stock's change-notification setters.</summary>
    public class StockTests
    {
        [Fact]
        public void Shares_Set_WithSubscriber_RaisesPropertyChanged()
        {
            Stock stock = new Stock();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)stock).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            stock.Shares = 42;

            Assert.Equal(42, stock.Shares);
            Assert.Contains(nameof(Stock.Shares), raised);
        }

        [Theory]
        [InlineData(PlayerEnums.Country.Deutschland, 1, true)]
        [InlineData(PlayerEnums.Country.Deutschland, 0, false)]
        [InlineData(PlayerEnums.Country.Deutschland, -5, false)]
        [InlineData(PlayerEnums.Country.Sonstige, 10, false)]
        public void HasPosition_NeedsACountryAndShares(PlayerEnums.Country country, int shares, bool expected)
        {
            Assert.Equal(expected, new Stock { Country = country, Shares = shares }.HasPosition);
        }
    }
}
