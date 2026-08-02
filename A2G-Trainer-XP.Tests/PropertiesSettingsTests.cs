using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for the generated application-settings singleton.</summary>
    public class PropertiesSettingsTests
    {
        [Fact]
        public void Default_ReturnsSameSynchronizedInstanceEachTime()
        {
            Properties.Settings first = Properties.Settings.Default;
            Properties.Settings second = Properties.Settings.Default;

            Assert.NotNull(first);
            Assert.Same(first, second);
        }
    }
}
