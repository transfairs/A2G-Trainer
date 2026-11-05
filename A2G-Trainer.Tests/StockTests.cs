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

            Assert.Equal((ushort)42, stock.Shares);
            Assert.Contains(nameof(Stock.Shares), raised);
        }
    }
}
