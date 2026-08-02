using System.Globalization;
using A2G_Trainer_XP.Properties;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for the generated strongly-typed resource accessor.</summary>
    public class ResourcesDesignerTests
    {
        [Fact]
        public void Constructor_DoesNotThrow()
        {
            var resources = new Resources();

            Assert.NotNull(resources);
        }

        [Fact]
        public void Culture_SetThenGet_RoundTrips()
        {
            CultureInfo original = Resources.Culture;
            try
            {
                Resources.Culture = CultureInfo.InvariantCulture;

                Assert.Equal(CultureInfo.InvariantCulture, Resources.Culture);
            }
            finally
            {
                Resources.Culture = original;
            }
        }

        [Fact]
        public void TrainerLogoIco_DoesNotThrow()
        {
            var icon = Resources.Trainer_Logo_ico;

            Assert.NotNull(icon);
        }
    }
}
