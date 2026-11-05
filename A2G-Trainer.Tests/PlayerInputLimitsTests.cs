using AnstossTrainer.Shared.Tests;
using System;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for the game's limits on condition, freshness (0..100) and red-card bans (0..31).</summary>
    public class PlayerInputLimitsTests
    {
        private static void InvokePrivate(object target, string methodName, params object[] args) =>
            target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        private static TextBox Typed(string text) => new TextBox { Text = text, Modified = true };

        [Theory]
        [InlineData("100", 100, "100")]
        [InlineData("101", 100, "100")]
        [InlineData("255", 100, "100")]
        [InlineData("99999999999", 100, "100")]
        [InlineData("31", 31, "31")]
        [InlineData("32", 31, "31")]
        [InlineData("0", 31, "0")]
        [InlineData("", 31, "")]
        public void CapTypedValue_CapsTypedTextAtMax(string input, int max, string expected)
        {
            using (TextBox textBox = Typed(input))
            {
                PlayerView.CapTypedValue(textBox, max);

                Assert.Equal(expected, textBox.Text);
            }
        }

        [Fact]
        public void CapTypedValue_LeavesLoadedValueAlone()
        {
            using (TextBox textBox = new TextBox { Text = "131", Modified = false })
            {
                PlayerView.CapTypedValue(textBox, PlayerView.MaxBannedMatches);

                Assert.Equal("131", textBox.Text);
            }
        }

        [Fact]
        public void Limits_MatchTheGame()
        {
            Assert.Equal(100, PlayerView.MaxConditionFreshness);
            Assert.Equal(31, PlayerView.MaxBannedMatches);
        }

        [Theory]
        [InlineData("ConditionFreshness_TextChanged", "150", "100")]
        [InlineData("BannedMatches_TextChanged", "40", "31")]
        public void Handlers_CapAtTheirLimit(string handler, string input, string expected) => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (TextBox textBox = Typed(input))
            {
                InvokePrivate(trainer.PlayerView, handler, textBox, EventArgs.Empty);

                Assert.Equal(expected, textBox.Text);
            }
        });
    }
}
