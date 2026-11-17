using System.Reflection;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// FixedAddresses' code searches where their search windows can't be read, and the CALL target
    /// of a site that is no call or unreadable.
    /// </summary>
    public class FixedAddressesCoverageGapTests
    {
        private static object Private(string name, params object[] args) =>
            typeof(FixedAddresses).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, args);

        [Fact]
        public void DiscoverSpeechList_WithAModuleEndingBeforeItsWindows_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.Memory.mProc.ModuleSize = 0x1000;

                Assert.Null(Private("DiscoverSpeechList", fake.Memory));
            }
        }

        [Fact]
        public void CallTarget_OfNoCallOrAnUnreadableSite_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                const uint site = 0x51D000;
                fake.WriteModuleBytes(site, new byte[] { 0xE8, 0x0B, 0, 0, 0 });
                fake.WriteModuleBytes(site + 0x10, new byte[] { 0x90, 0, 0, 0, 0 });

                Assert.Equal(fake.ModuleBase + site + 0x10, (uint)Private("CallTarget", fake.Memory, site));
                Assert.Null(Private("CallTarget", fake.Memory, site + 0x10));

                fake.MakeModuleRegionUnreadable(0x51E000, 5);
                Assert.Null(Private("CallTarget", fake.Memory, 0x51E000u));
            }
        }

        // The function's window reaches 0x3500 bytes further than its call sites' window: the function is
        // found there while the call sites' window can't be read at all.
        [Fact]
        public void DiscoverMatchInit_WithTheCallSiteWindowUnreadable_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                const uint function = 0x212000;
                fake.WriteModuleBytes(function, Settings.MatchInitPattern);
                uint window = Settings.FixedAddressSearchWindow;
                uint start = Settings.MatchInitCallSiteOffsets[0] - window;
                fake.MakeModuleRegionUnreadable(start, (int)(2 * window));
                Assert.True(function >= ((start + 2 * window + 0xFFF) & ~0xFFFu));

                Assert.Null(Private("DiscoverMatchInit", fake.Memory));
            }
        }

        [Fact]
        public void MatchTeamFactorOffset_IsTheFactorInTheTeamBlock()
        {
            Assert.Equal(0x1Cu, Settings.MatchTeamFactorOffset);
        }
    }
}
