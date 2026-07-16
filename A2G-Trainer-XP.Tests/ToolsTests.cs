using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    public class ToolsTests
    {
        [Fact]
        public void SumHex_AddsPositiveTokens()
        {
            Assert.Equal("15", Tools.SumHex(new[] { "A", "B" }));
        }

        [Fact]
        public void SumHex_HonoursExplicitPlusSign()
        {
            Assert.Equal("30", Tools.SumHex(new[] { "+10", "+20" }));
        }

        [Fact]
        public void SumHex_HonoursMinusSign_AndFormatsNegativeResultAsTwosComplement()
        {
            // -0xA + 0x5 = -5, and int.ToString("X") renders negative numbers as
            // their 32-bit two's complement, not a leading "-".
            Assert.Equal("FFFFFFFB", Tools.SumHex(new[] { "-A", "5" }));
        }

        [Fact]
        public void SumHex_SkipsNullEmptyAndWhitespaceEntries()
        {
            Assert.Equal("10", Tools.SumHex(new[] { null, "", "   ", "10" }));
        }

        [Fact]
        public void SumHex_TrimsWhitespaceAroundTokens()
        {
            Assert.Equal("10", Tools.SumHex(new[] { " 10 " }));
        }

        [Fact]
        public void SumHex_EmptyArray_ReturnsZero()
        {
            Assert.Equal("0", Tools.SumHex(new string[0]));
        }

        [Fact]
        public void SumHex_RepeatedCalls_WithSameTokens_AreStable()
        {
            // ParseToken caches per-token results; confirm the cache doesn't corrupt
            // later lookups of the same or overlapping token sets.
            Assert.Equal("15", Tools.SumHex(new[] { "A", "B" }));
            Assert.Equal("1F", Tools.SumHex(new[] { "A", "B", "A" }));
            Assert.Equal("15", Tools.SumHex(new[] { "A", "B" }));
        }

        [Theory]
        [InlineData(null, null, false)]
        [InlineData(null, "Max", true)]
        [InlineData("Max", null, true)]
        [InlineData("Max", "Max", false)]
        [InlineData("Ann", "Bob", true)]
        public void LimitedStringEquals_BasicCases(string subject, string benchmark, bool expected)
        {
            Assert.Equal(expected, Tools.LimitedStringEquals(subject, benchmark, 9));
        }

        [Fact]
        public void LimitedStringEquals_WhenPrefixMatches_ReturnsFalse()
        {
            // subject differs from benchmark only beyond the (limitation-1)-char prefix.
            Assert.False(Tools.LimitedStringEquals("Abcdefghij", "Abcdefgh", 9));
        }

        [Fact]
        public void LimitedStringEquals_WhenPrefixDiffers_ReturnsTrue()
        {
            Assert.True(Tools.LimitedStringEquals("Abcdefghij", "Abcdefgz", 9));
        }

        [Fact]
        public void LimitedStringEquals_ShortSubject_DoesNotThrow_AndReturnsTrue()
        {
            // subject.Length < limitation short-circuits before the Substring call,
            // which would otherwise throw for a subject shorter than limitation-1.
            Assert.True(Tools.LimitedStringEquals("Al", "Bo", 9));
        }

        [Theory]
        [InlineData("test", "Test")]
        [InlineData("Test", "Test")]
        [InlineData("t", "T")]
        [InlineData("", "")]
        public void ToMemberName_CapitalisesFirstLetter(string input, string expected)
        {
            Assert.Equal(expected, Tools.ToMemberName(input));
        }

        [Fact]
        public void ToMemberName_Null_ReturnsNull()
        {
            Assert.Null(Tools.ToMemberName(null));
        }
    }
}
