using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    public class ClubEnumsTests
    {
        [Theory]
        [InlineData(0, ClubEnums.FieldCondition.Clean)]
        [InlineData(1, ClubEnums.FieldCondition.Optimal)]
        [InlineData(2, ClubEnums.FieldCondition.Ordentlich)]
        [InlineData(4, ClubEnums.FieldCondition.Ordentlich)]
        [InlineData(5, ClubEnums.FieldCondition.Mittelmaessig)]
        [InlineData(9, ClubEnums.FieldCondition.Mittelmaessig)]
        [InlineData(10, ClubEnums.FieldCondition.Holprig)]
        [InlineData(19, ClubEnums.FieldCondition.Holprig)]
        [InlineData(20, ClubEnums.FieldCondition.Kartoffelacker)]
        [InlineData(29, ClubEnums.FieldCondition.Kartoffelacker)]
        [InlineData(30, ClubEnums.FieldCondition.SpielfeldFraglich)]
        [InlineData(255, ClubEnums.FieldCondition.SpielfeldFraglich)]
        public void MapToCondition_RespectsThresholds(int value, ClubEnums.FieldCondition expected)
        {
            Assert.Equal(expected, ClubEnums.MapToCondition((byte)value));
        }

        [Fact]
        public void GetDescription_ReturnsAttributeValue_WhenPresent()
        {
            Assert.Equal("keine Anzeigetafel", ClubEnums.GetDescription(ClubEnums.DisplayUnit.None));
            Assert.Equal("Mittelmäßig", ClubEnums.GetDescription(ClubEnums.FieldCondition.Mittelmaessig));
        }

        [Fact]
        public void GetDescription_FallsBackToEnumName_WhenAttributeMissing()
        {
            Assert.Equal("Videowand", ClubEnums.GetDescription(ClubEnums.DisplayUnit.Videowand));
        }
    }
}
