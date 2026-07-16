using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    public class PlayerEnumsTests
    {
        [Fact]
        public void GetDescription_ReturnsAttributeValue_WhenPresent()
        {
            Assert.Equal("Ägypten", PlayerEnums.GetDescription(PlayerEnums.Country.Aegypten));
            Assert.Equal("Costa Rica", PlayerEnums.GetDescription(PlayerEnums.Country.CostaRica));
        }

        [Fact]
        public void GetDescription_FallsBackToEnumName_WhenAttributeMissing()
        {
            Assert.Equal("Sonstige", PlayerEnums.GetDescription(PlayerEnums.Country.Sonstige));
            Assert.Equal("Deutschland", PlayerEnums.GetDescription(PlayerEnums.Country.Deutschland));
        }
    }
}
