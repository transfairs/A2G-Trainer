using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for the "Stammverein" of a loaned player: read from the persistent record over the
    /// display cache, written to both, and chosen on the player view's contract tab.
    /// </summary>
    public class HomeClubTests
    {
        private const ushort CurrentYear = 2001;

        private static Addresses Own => AddressPresets.OWN_PLAYERS;

        private static Club NewSinglePlayerClub() => new Club { PlayerCount = 1, AmateurPlayerCount = 0 };

        private static FakeModule NewFake()
        {
            FakeModule fake = new FakeModule();
            fake.RoutePointerSlot(Settings.PlayerAddress[0]);
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.ID], BitConverter.GetBytes((ushort)0));
            fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes(CurrentYear));
            return fake;
        }

        [Fact]
        public void Read_PrefersTheRecordOverTheDisplayCache()
        {
            using (FakeModule fake = NewFake())
            {
                fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.HOME_COUNTRY], new byte[] { 5 });
                fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.HOME_CLUB], BitConverter.GetBytes((ushort)7));
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset + Settings.PlayerRecordHomeCountryOffset, new byte[] { (byte)PlayerEnums.Country.Belgien, 0x00, 12, 0 });

                Player player = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN).EntityList.Single();

                Assert.Equal(PlayerEnums.Country.Belgien, player.HomeCountry);
                Assert.Equal((ushort)12, player.HomeClubId);
            }
        }

        [Fact]
        public void Read_WhenTheRecordIsUnreadable_KeepsTheDisplayCacheValues()
        {
            using (FakeModule fake = NewFake())
            {
                fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.HOME_COUNTRY], new byte[] { (byte)PlayerEnums.Country.Chile });
                fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.HOME_CLUB], BitConverter.GetBytes((ushort)9));
                fake.MakeModuleRegionUnreadable(Settings.PlayerRecordTableOffset + Settings.PlayerRecordHomeCountryOffset, 4);

                Player player = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN).EntityList.Single();

                Assert.Equal(PlayerEnums.Country.Chile, player.HomeCountry);
                Assert.Equal((ushort)9, player.HomeClubId);
            }
        }

        [Fact]
        public void Save_WritesRecordAndDisplayCache_AndLeavesTheByteInBetweenAlone()
        {
            using (FakeModule fake = NewFake())
            {
                uint b = Settings.PlayerRecordTableOffset;
                fake.WriteModuleBytes(b + Settings.PlayerRecordHomeCountryOffset + 1, new byte[] { 0x5A });
                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();
                player.HomeCountry = PlayerEnums.Country.Bulgarien;
                player.HomeClubId = 0x0103;

                controller.Save(player);

                Assert.Equal(new byte[] { (byte)PlayerEnums.Country.Bulgarien, 0x5A, 0x03, 0x01 }, fake.ReadModuleBytes(b + Settings.PlayerRecordHomeCountryOffset, 4));
                Assert.Equal((byte)PlayerEnums.Country.Bulgarien, fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.HOME_COUNTRY], 1)[0]);
                Assert.Equal((ushort)0x0103, BitConverter.ToUInt16(fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.HOME_CLUB], 2), 0));
            }
        }

        [Fact]
        public void HomeProperties_RaisePropertyChanged()
        {
            Player player = new Player();
            var changed = new List<string>();
            player.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            player.HomeCountry = PlayerEnums.Country.Belgien;
            player.HomeClubId = 4;

            Assert.Equal(new[] { nameof(Player.HomeCountry), nameof(Player.HomeClubId) }, changed);
        }

        private static T GetPrivateField<T>(object target, string fieldName) =>
            (T)target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void SetPrivateField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static void ShowHomeClub(PlayerView view, Player player) =>
            typeof(PlayerView).GetMethod("ShowHomeClub", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { player });

        private static List<Club> SampleClubs() => new List<Club>
        {
            new Club { Country = PlayerEnums.Country.Belgien, Id = 2, ClubName = "Brügge" },
            new Club { Country = PlayerEnums.Country.Belgien, Id = 1, ClubName = "Anderlecht" },
            new Club { Country = PlayerEnums.Country.Chile, Id = 4, ClubName = "Colo-Colo" },
            // Country 0 entries are unused slots of the club scan and must not show up.
            new Club { Country = PlayerEnums.Country.Sonstige, Id = 9, ClubName = "Müll" },
        };

        // Runs the test against the player view's first (and only) player, with SampleClubs as the club scan.
        private static void WithPlayerView(Action<PlayerView, Player, ComboBox, ComboBox> test) => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 1 });
                trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);
                SetPrivateField(trainer.PlayerView, "allClubs", SampleClubs());
                trainer.PlayerView.InitMainTabControl();

                BindingSource source = GetPrivateField<BindingSource>(trainer.PlayerView, "bindingSource");
                test(trainer.PlayerView, (Player)source.Current,
                    GetPrivateField<ComboBox>(trainer.PlayerView, "HomeCountryInput"),
                    GetPrivateField<ComboBox>(trainer.PlayerView, "HomeClubInput"));
            }
        });

        [Fact]
        public void PlayerView_WithoutHomeClub_ShowsTheEmptyChoice() => WithPlayerView((view, player, country, club) =>
        {
            Assert.Equal(PlayerEnums.Country.Sonstige, country.SelectedValue);
            Assert.Equal(PlayerView.NoHomeClubText, country.Text);
            Assert.Equal(0, club.Items.Count);
            // Countries come from the club scan, without country 0's unused slots.
            Assert.Equal(3, country.Items.Count);
        });

        [Fact]
        public void PlayerView_ChoosingACountry_TakesItsFirstClubAlphabetically() => WithPlayerView((view, player, country, club) =>
        {
            country.SelectedValue = PlayerEnums.Country.Belgien;

            Assert.Equal(PlayerEnums.Country.Belgien, player.HomeCountry);
            Assert.Equal((ushort)1, player.HomeClubId);
            Assert.Equal("Anderlecht", club.Text);

            club.SelectedValue = (ushort)2;
            Assert.Equal((ushort)2, player.HomeClubId);

            country.SelectedValue = PlayerEnums.Country.Sonstige;
            Assert.Equal(PlayerEnums.Country.Sonstige, player.HomeCountry);
            Assert.Equal((ushort)0, player.HomeClubId);
        });

        [Fact]
        public void PlayerView_ShowsTheStoredHomeClub_EvenIfTheScanDoesNotKnowIt() => WithPlayerView((view, player, country, club) =>
        {
            player.HomeCountry = PlayerEnums.Country.Belgien;
            player.HomeClubId = 2;
            ShowHomeClub(view, player);
            Assert.Equal("Brügge", club.Text);

            player.HomeCountry = PlayerEnums.Country.Albanien;
            player.HomeClubId = 17;
            ShowHomeClub(view, player);

            Assert.Equal(PlayerEnums.Country.Albanien, country.SelectedValue);
            Assert.Equal("Verein 17", club.Text);
            // Showing a player doesn't change him.
            Assert.Equal((ushort)17, player.HomeClubId);

            player.HomeCountry = (PlayerEnums.Country)250;
            ShowHomeClub(view, player);
            Assert.Equal("Land 250", country.Text);
        });
    }
}
