using System;
using System.Collections.Generic;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for the finance models: investment value, kind names and change notifications.</summary>
    public class ClubFinanceTests
    {
        [Fact]
        public void InvestmentValue_WithoutAnIndex_IsTheBaseItself()
        {
            ClubInvestment investment = new ClubInvestment { Base = 5000 };

            Assert.Equal(5000, investment.Value);
            investment.Value = 7000;
            Assert.Equal(7000, investment.Base);
        }

        [Theory]
        [InlineData(500, 1000, 1000)]
        [InlineData(499, 1000, 0)]
        [InlineData(12345678, 1000, 12346000)]
        [InlineData(int.MaxValue, 1000, 32767000)]
        [InlineData(-5000, 1000, 0)]
        [InlineData(int.MaxValue, 100000, 2147400000)]
        public void InUnits_RoundsToTheGamesUnit(int amount, int unit, int expected)
        {
            Assert.Equal(expected, GameCosts.InUnits(amount, unit));
        }

        [Fact]
        public void Costs_AreKeptInDmRoundedToTheGamesUnits()
        {
            LawFirmFee fee = new LawFirmFee { RedCard = 1499, Other = 1500 };
            GameCosts costs = new GameCosts { SmallStatistics = 25000, LargeStatistics = 4000, Computer = 150000 };

            Assert.Equal(1000, fee.RedCard);
            Assert.Equal(2000, fee.Other);
            Assert.Equal(30000, costs.SmallStatistics);
            Assert.Equal(0, costs.LargeStatistics);
            Assert.Equal(200000, costs.Computer);
        }

        [Fact]
        public void LoanRateAndInvestmentValue_ClampInsteadOfOverflowing()
        {
            ClubLoan loan = new ClubLoan();
            loan.Rate = decimal.MaxValue;
            Assert.Equal(327.67m, loan.Rate);
            loan.Rate = decimal.MinValue;
            Assert.Equal(-327.68m, loan.Rate);

            ClubInvestment investment = ClubInvestment.New(1, 1000, new[] { 1000, 1000, 1000, 1000, 1000 });
            investment.KindIndices[1] = 500;
            investment.Value = int.MaxValue;
            Assert.Equal(int.MaxValue, investment.Base);
            investment.KindIndices[1] = 4000;
            Assert.Equal(int.MaxValue, investment.Value);
        }

        [Theory]
        [InlineData(typeof(int), false, int.MaxValue)]
        [InlineData(typeof(int), true, int.MinValue)]
        [InlineData(typeof(short), false, short.MaxValue)]
        [InlineData(typeof(byte), true, byte.MinValue)]
        public void OverflowLimit_IsTheTypesLimit(Type type, bool negative, object expected)
        {
            Assert.Equal(Convert.ChangeType(expected, type), A2G_Trainer.View.ClubView.OverflowLimit(type, negative));
        }

        // A number too big for the cell ends up as the field's maximum, without WinForms' error dialog.
        [Theory]
        [InlineData("99999999999", int.MaxValue)]
        [InlineData("-99999999999", int.MinValue)]
        [InlineData("abc", 1000000)]
        public void BudgetGrid_TooBigANumber_BecomesTheMaximum(string typed, int expected) => AnstossTrainer.Shared.Tests.StaThread.Run(() =>
        {
            using (System.Windows.Forms.Form host = new System.Windows.Forms.Form { ShowInTaskbar = false, StartPosition = System.Windows.Forms.FormStartPosition.Manual, Location = new System.Drawing.Point(-2000, -2000) })
            {
                System.Windows.Forms.Panel panel = new System.Windows.Forms.Panel { Size = new System.Drawing.Size(300, 150) };
                host.Controls.Add(panel);
                var grid = (System.Windows.Forms.DataGridView)typeof(A2G_Trainer.View.ClubView)
                    .GetMethod("AddGrid", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
                    .Invoke(null, new object[] { panel, false });
                grid.Columns.Add(new System.Windows.Forms.DataGridViewTextBoxColumn { DataPropertyName = nameof(ClubLoan.Amount) });
                System.ComponentModel.BindingList<ClubLoan> loans = new System.ComponentModel.BindingList<ClubLoan> { ClubLoan.New(1000000, 0, true, 800) };
                grid.DataSource = loans;
                host.Show();

                grid.CurrentCell = grid.Rows[0].Cells[0];
                grid.BeginEdit(true);
                grid.EditingControl.Text = typed;
                grid.EndEdit();

                Assert.Equal(expected, loans[0].Amount);
            }
        });

        [Fact]
        public void OverflowLimit_ForDecimalAndOtherTypes()
        {
            Assert.Equal(decimal.MaxValue, A2G_Trainer.View.ClubView.OverflowLimit(typeof(decimal), false));
            Assert.Null(A2G_Trainer.View.ClubView.OverflowLimit(typeof(string), false));
        }

        [Fact]
        public void InvestmentKindName_ForAnUnknownKind_ShowsTheNumber()
        {
            Assert.Equal("Tagesgeld", new ClubInvestment { Kind = 0 }.KindName);
            Assert.Equal("Art 9", new ClubInvestment { Kind = 9 }.KindName);
        }

        [Fact]
        public void LoanInvestmentAndRates_RaisePropertyChanged()
        {
            List<string> raised = new List<string>();
            ClubLoan loan = new ClubLoan();
            ClubInvestment investment = new ClubInvestment();
            InterestRates rates = new InterestRates();
            loan.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
            investment.PropertyChanged += (s, e) => raised.Add(e.PropertyName);
            rates.PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            loan.Amount = 1;
            investment.Base = 1;
            rates.Deposit = 1;
            rates.Overdraft = 1;
            rates.OverLimit = 1;
            rates.Loan = 1;
            rates.CallMoney = 1;
            rates.CorporationTax = 1;

            Assert.Equal(new[]
            {
                nameof(ClubLoan.Amount), nameof(ClubInvestment.Value), nameof(InterestRates.Deposit), nameof(InterestRates.Overdraft),
                nameof(InterestRates.OverLimit), nameof(InterestRates.Loan), nameof(InterestRates.CallMoney), nameof(InterestRates.CorporationTax)
            }, raised);
        }
    }
}
