using System;
using System.Linq;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for LeagueController's bonus-country reads/writes against a FakeModule.</summary>
    public class LeagueControllerFakeModuleTests
    {
        private static readonly Addresses League = AddressPresets.LEAGUE_SETTINGS;

        private static string Slot(LeagueEnums.AddressKey key, int slot) => Tools.SumHex(new[] { League[key], (slot * 2).ToString("X") });

        private static void WriteInt16(FakeModule fake, string offset, short value) => fake.WriteDisplayCacheBytes(offset, BitConverter.GetBytes(value));
        private static short ReadInt16(FakeModule fake, string offset) => BitConverter.ToInt16(fake.ReadDisplayCacheBytes(offset, 2), 0);

        // Main country Deutschland, the given bonus countries active (with their real league systems),
        // and the game's league tables: system = index in LeagueCountries, top leagues 0/6/9/11/12.
        // Only the countries in loaded have clubs in their top league.
        private static void Seed(FakeModule fake, PlayerEnums.Country[] active, PlayerEnums.Country[] loaded)
        {
            fake.RoutePointerSlot(Settings.CountrySelectionAddress[0]);
            fake.WriteDisplayCacheBytes(League[LeagueEnums.AddressKey.MAIN_COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland, 0 });
            WriteInt16(fake, League[LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT], (short)active.Length);
            for (int slot = 0; slot < LeagueSettings.MaxBonusCountries; slot++)
            {
                WriteInt16(fake, Slot(LeagueEnums.AddressKey.ADDITIONAL_COUNTRY, slot), slot < active.Length ? (short)active[slot] : (short)-1);
                WriteInt16(fake, Slot(LeagueEnums.AddressKey.BONUS_LEAGUE_SYSTEM, slot), slot < active.Length ? (short)Array.IndexOf(LeagueSettings.LeagueCountries, active[slot]) : (short)0);
            }

            short[] firstLeague = { 0, 6, 9, 11, 12, 13 };
            fake.WriteModuleBytes(Settings.LeagueRangeTableOffset, firstLeague.SelectMany(BitConverter.GetBytes).ToArray());
            for (int system = 0; system < LeagueSettings.LeagueCountries.Length; system++)
            {
                PlayerEnums.Country country = LeagueSettings.LeagueCountries[system];
                fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + (uint)country, new byte[] { (byte)system });
                short clubs = loaded.Contains(country) ? (short)18 : (short)0;
                fake.WriteModuleBytes(Settings.LeagueTableOffset + (uint)firstLeague[system] * Settings.LeagueStride, BitConverter.GetBytes(clubs));
            }
        }

        private static void SeedTrainer(FakeModule fake, PlayerEnums.Country current, PlayerEnums.Country nextSeason)
        {
            fake.WriteModuleBytes(Settings.ActiveTrainerCountOffset, new byte[] { 1 });
            CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.COUNTRY], new byte[] { (byte)current });
            CoachMemory.WriteWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.COUNTRY_NEXT_SEASON], new byte[] { (byte)nextSeason });
        }

        private static LeagueController Read(FakeModule fake) => new LeagueController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);

        private static BonusCountry Entry(LeagueController controller, PlayerEnums.Country country) => controller.League.BonusCountries.Single(c => c.Country == country);

        [Fact]
        public void Constructor_ListsActiveCountriesInSlotOrderThenTheInactiveOnes()
        {
            using (FakeModule fake = new FakeModule())
            {
                Seed(fake, new[] { PlayerEnums.Country.Spanien, PlayerEnums.Country.England }, new[] { PlayerEnums.Country.Spanien, PlayerEnums.Country.England });

                LeagueController controller = Read(fake);

                Assert.Equal(PlayerEnums.Country.Deutschland, controller.League.MainCountry);
                Assert.Equal(new[] { PlayerEnums.Country.Spanien, PlayerEnums.Country.England, PlayerEnums.Country.Frankreich, PlayerEnums.Country.Italien },
                    controller.League.BonusCountries.Select(c => c.Country));
                Assert.Equal(new[] { PlayerEnums.Country.Spanien, PlayerEnums.Country.England }, controller.League.ActiveBonusCountries);
                Assert.Equal((short)4, Entry(controller, PlayerEnums.Country.Spanien).LeagueSystem);
                Assert.Equal((short)2, Entry(controller, PlayerEnums.Country.Frankreich).LeagueSystem);
            }
        }

        [Fact]
        public void Constructor_OnlyOffersCountriesWithLeagueDataInMemoryForSwitchingOn()
        {
            using (FakeModule fake = new FakeModule())
            {
                // Frankreich was chosen at the start and switched off later; Italien never was.
                Seed(fake, new[] { PlayerEnums.Country.England }, new[] { PlayerEnums.Country.England, PlayerEnums.Country.Frankreich });

                LeagueController controller = Read(fake);

                Assert.True(Entry(controller, PlayerEnums.Country.England).CanToggle);
                Assert.True(Entry(controller, PlayerEnums.Country.Frankreich).CanToggle);
                Assert.Contains("Keine Vereine", Entry(controller, PlayerEnums.Country.Italien).ToggleBlockedReason);
            }
        }

        [Fact]
        public void Constructor_WithAnImplausibleCount_ClampsItToTheSlotCount()
        {
            using (FakeModule fake = new FakeModule())
            {
                Seed(fake, new PlayerEnums.Country[0], new PlayerEnums.Country[0]);
                WriteInt16(fake, League[LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT], -3);

                Assert.Empty(Read(fake).League.ActiveBonusCountries);
            }
        }

        [Theory]
        [InlineData(PlayerEnums.Country.England, PlayerEnums.Country.Sonstige, "trainiert dort")]
        [InlineData(PlayerEnums.Country.Deutschland, PlayerEnums.Country.England, "nächsten Saison")]
        public void Constructor_BlocksSwitchingOffACountryAHumanTrainerIsTiedTo(PlayerEnums.Country current, PlayerEnums.Country nextSeason, string expected)
        {
            using (FakeModule fake = new FakeModule())
            {
                Seed(fake, new[] { PlayerEnums.Country.England }, new[] { PlayerEnums.Country.England });
                SeedTrainer(fake, current, nextSeason);

                Assert.Contains(expected, Entry(Read(fake), PlayerEnums.Country.England).ToggleBlockedReason);
            }
        }

        [Fact]
        public void Save_SwitchingOneOff_CompactsBothListsAndLowersTheCount()
        {
            using (FakeModule fake = new FakeModule())
            {
                PlayerEnums.Country[] all = { PlayerEnums.Country.England, PlayerEnums.Country.Frankreich, PlayerEnums.Country.Italien, PlayerEnums.Country.Spanien };
                Seed(fake, all, all);
                LeagueController controller = Read(fake);
                Entry(controller, PlayerEnums.Country.Frankreich).IsActive = false;

                Assert.Empty(controller.Save());

                Assert.Equal(3, ReadInt16(fake, League[LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT]));
                Assert.Equal(new short[] { 16, 28, 63, -1 }, Enumerable.Range(0, 4).Select(i => ReadInt16(fake, Slot(LeagueEnums.AddressKey.ADDITIONAL_COUNTRY, i))));
                Assert.Equal(new short[] { 1, 3, 4, 0 }, Enumerable.Range(0, 4).Select(i => ReadInt16(fake, Slot(LeagueEnums.AddressKey.BONUS_LEAGUE_SYSTEM, i))));
            }
        }

        [Fact]
        public void Save_SwitchingOneOn_AppendsItWithItsLeagueSystem()
        {
            using (FakeModule fake = new FakeModule())
            {
                Seed(fake, new[] { PlayerEnums.Country.Spanien }, new[] { PlayerEnums.Country.Spanien, PlayerEnums.Country.Frankreich });
                LeagueController controller = Read(fake);
                Entry(controller, PlayerEnums.Country.Frankreich).IsActive = true;

                Assert.Empty(controller.Save());

                Assert.Equal(2, ReadInt16(fake, League[LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT]));
                Assert.Equal(new short[] { 63, 20, -1, -1 }, Enumerable.Range(0, 4).Select(i => ReadInt16(fake, Slot(LeagueEnums.AddressKey.ADDITIONAL_COUNTRY, i))));
                Assert.Equal(new short[] { 4, 2, 0, 0 }, Enumerable.Range(0, 4).Select(i => ReadInt16(fake, Slot(LeagueEnums.AddressKey.BONUS_LEAGUE_SYSTEM, i))));
                // The main country is shown only, never written.
                Assert.Equal((byte)PlayerEnums.Country.Deutschland, fake.ReadDisplayCacheBytes(League[LeagueEnums.AddressKey.MAIN_COUNTRY], 1)[0]);
            }
        }

        [Fact]
        public void Save_WithoutChanges_WritesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                Seed(fake, new[] { PlayerEnums.Country.England }, new[] { PlayerEnums.Country.England });
                LeagueController controller = Read(fake);
                // Something the controller would rewrite (league system of slot 0) if it saved anyway.
                WriteInt16(fake, Slot(LeagueEnums.AddressKey.BONUS_LEAGUE_SYSTEM, 0), 7);

                Assert.Empty(controller.Save());

                Assert.Equal(7, ReadInt16(fake, Slot(LeagueEnums.AddressKey.BONUS_LEAGUE_SYSTEM, 0)));
            }
        }

        [Fact]
        public void Save_SwitchOffThatBecameBlockedSinceTheRead_IsSkippedAndReported()
        {
            using (FakeModule fake = new FakeModule())
            {
                Seed(fake, new[] { PlayerEnums.Country.England }, new[] { PlayerEnums.Country.England });
                LeagueController controller = Read(fake);
                Entry(controller, PlayerEnums.Country.England).IsActive = false;
                // e.g. a next-season change to England saved on the coach tab right before.
                SeedTrainer(fake, PlayerEnums.Country.Deutschland, PlayerEnums.Country.England);

                Assert.Equal(new[] { PlayerEnums.Country.England }, controller.Save());

                Assert.Equal(1, ReadInt16(fake, League[LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT]));
                Assert.Equal(16, ReadInt16(fake, Slot(LeagueEnums.AddressKey.ADDITIONAL_COUNTRY, 0)));
            }
        }

        [Fact]
        public void Save_ForcedSwitchOnOfABlockedCountry_IsSkippedAndReported()
        {
            using (FakeModule fake = new FakeModule())
            {
                Seed(fake, new[] { PlayerEnums.Country.England }, new[] { PlayerEnums.Country.England });
                LeagueController controller = Read(fake);
                Entry(controller, PlayerEnums.Country.Italien).IsActive = true;

                Assert.Equal(new[] { PlayerEnums.Country.Italien }, controller.Save());

                Assert.Equal(1, ReadInt16(fake, League[LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT]));
                Assert.Equal(-1, ReadInt16(fake, Slot(LeagueEnums.AddressKey.ADDITIONAL_COUNTRY, 1)));
            }
        }

        [Fact]
        public void ActivationBlockedReason_WithoutFixedAddresses_IsBlockedForThisBuild()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);

                Assert.Null(LeagueController.LookupLeagueSystem(fake.Memory, PlayerEnums.Country.England));
                Assert.Null(LeagueController.HasClubsInTopLeague(fake.Memory, 1));
                Assert.Contains("Spielversion", LeagueController.ActivationBlockedReason(fake.Memory, 1));
            }
        }

        [Fact]
        public void ActivationBlockedReason_WithoutALeagueSystem_SaysTheDataIsUnreadable()
        {
            using (FakeModule fake = new FakeModule())
            {
                Assert.Contains("nicht lesbar", LeagueController.ActivationBlockedReason(fake.Memory, null));
            }
        }

        [Fact]
        public void HasClubsInTopLeague_WithUnreadableTables_IsUnknown()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.MakeModuleRegionUnreadable(Settings.LeagueRangeTableOffset, 2);

                Assert.Null(LeagueController.HasClubsInTopLeague(fake.Memory, 0));
                Assert.Contains("nicht lesbar", LeagueController.ActivationBlockedReason(fake.Memory, 0));
            }
        }

        [Fact]
        public void HasClubsInTopLeague_WithUnreadableLeagueTable_IsUnknown()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.MakeModuleRegionUnreadable(Settings.LeagueTableOffset, 2);

                Assert.Null(LeagueController.HasClubsInTopLeague(fake.Memory, 0));
            }
        }
    }
}
