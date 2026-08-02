using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for LeagueController's field reads/writes against a FakeModule.</summary>
    public class LeagueControllerFakeModuleTests
    {
        [Fact]
        public void Constructor_ReadsMainCountryAndAdditionalCountrySlots()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.CountrySelectionAddress[0]);
                Addresses addresses = AddressPresets.LEAGUE_SETTINGS;

                fake.WriteDisplayCacheBytes(addresses[LeagueEnums.AddressKey.MAIN_COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
                fake.WriteDisplayCacheBytes(addresses[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], new byte[] { (byte)PlayerEnums.Country.England, 0x00 });
                fake.WriteDisplayCacheBytes(Tools.SumHex(new[] { addresses[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], (2).ToString("X") }), new byte[] { 0xFF, 0xFF });
                fake.WriteDisplayCacheBytes(Tools.SumHex(new[] { addresses[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], (4).ToString("X") }), new byte[] { 0xFF, 0xFF });
                fake.WriteDisplayCacheBytes(Tools.SumHex(new[] { addresses[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], (6).ToString("X") }), new byte[] { 0xFF, 0xFF });

                LeagueController controller = new LeagueController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);

                Assert.Equal(PlayerEnums.Country.Deutschland, controller.League.MainCountry);
                Assert.Equal(PlayerEnums.Country.England, controller.League.AdditionalCountries[0].Country);
                Assert.Null(controller.League.AdditionalCountries[1].Country);
                Assert.Null(controller.League.AdditionalCountries[2].Country);
                Assert.Null(controller.League.AdditionalCountries[3].Country);
            }
        }

        [Fact]
        public void Save_WritesMainCountryAndBothEmptyAndFilledAdditionalSlots()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.CountrySelectionAddress[0]);
                Addresses addresses = AddressPresets.LEAGUE_SETTINGS;

                LeagueController controller = new LeagueController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                controller.League.MainCountry = PlayerEnums.Country.Spanien;
                controller.League.AdditionalCountries[0].Country = PlayerEnums.Country.Frankreich;
                controller.League.AdditionalCountries[1].Country = null;

                controller.Save();

                byte[] mainCountry = fake.ReadDisplayCacheBytes(addresses[LeagueEnums.AddressKey.MAIN_COUNTRY], 1);
                byte[] slot0 = fake.ReadDisplayCacheBytes(addresses[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], 2);
                byte[] slot1 = fake.ReadDisplayCacheBytes(Tools.SumHex(new[] { addresses[LeagueEnums.AddressKey.ADDITIONAL_COUNTRY], (2).ToString("X") }), 2);

                Assert.Equal((byte)PlayerEnums.Country.Spanien, mainCountry[0]);
                Assert.Equal((byte)PlayerEnums.Country.Frankreich, slot0[0]);
                Assert.Equal(0x00, slot0[1]);
                Assert.Equal(0xFF, slot1[0]);
                Assert.Equal(0xFF, slot1[1]);
            }
        }
    }
}
