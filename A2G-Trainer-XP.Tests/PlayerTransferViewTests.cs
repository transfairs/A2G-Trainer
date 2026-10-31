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
    /// <summary>Tests for the player view's "Wechsel" tab on top of TransferInjector.</summary>
    public class PlayerTransferViewTests
    {
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void SetField(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static void Invoke(object target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);

        private static List<Club> SampleClubs() => new List<Club>
        {
            new Club { Country = PlayerEnums.Country.Belgien, Id = 2, ClubName = "Brügge" },
            new Club { Country = PlayerEnums.Country.Belgien, Id = 1, ClubName = "Anderlecht" },
            new Club { Country = PlayerEnums.Country.Chile, Id = 4, ClubName = "Colo-Colo" },
        };

        private static void WriteOriginalCalls(FakeModule fake)
        {
            void Call(uint site, uint target)
            {
                int rel = unchecked((int)(target - (site + 5)));
                fake.WriteModuleBytes(site, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
            }
            foreach (uint site in Settings.TransferMarketCallSiteOffsets)
                Call(site, Settings.TransferMarketFunctionOffset);
            Call(Settings.TransferDeadlineCallSiteOffset, Settings.TransferDeadlineFunctionOffset);
        }

        // The own club's only player plays for Anderlecht (Belgien, club 1); SampleClubs is the club scan.
        private static void WithTransferTab(Action<Trainer, FakeModule, PlayerView> test, PlayerEnums.AddressType type = PlayerEnums.AddressType.OWN) => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 1 });
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.CLUB_COUNTRY], new byte[] { (byte)PlayerEnums.Country.Belgien });
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.CLUB_ID], new byte[] { 1 });
                WriteOriginalCalls(fake);
                trainer.PlayerView.RefreshPlayerListView(type);
                SetField(trainer.PlayerView, "allClubs", SampleClubs());
                trainer.PlayerView.InitMainTabControl();
                Invoke(trainer.PlayerView, "ShowTransfer");
                test(trainer, fake, trainer.PlayerView);
            }
        });

        [Fact]
        public void Kind_EnablesBuyPriceOnlyForALoanWithOption_AndSofortOnlyForATransfer() => WithTransferTab((trainer, fake, view) =>
        {
            ComboBox kind = Field<ComboBox>(view, "TransferKindInput");
            TextBox buyPrice = Field<TextBox>(view, "TransferBuyPriceInput");
            CheckBox immediate = Field<CheckBox>(view, "TransferImmediate");
            Field<GroupBox>(view, "TransferBox").Enabled = true;

            Assert.Equal(3, kind.Items.Count);
            Assert.Equal("sofort", immediate.Text);

            kind.SelectedValue = TransferInjector.Kind.Transfer;
            Assert.False(buyPrice.Enabled);
            Assert.True(immediate.Enabled);
            kind.SelectedValue = TransferInjector.Kind.LoanWithBuyOption;
            Assert.True(buyPrice.Enabled);
            Assert.False(immediate.Enabled);
            kind.SelectedValue = TransferInjector.Kind.Loan;
            Assert.False(buyPrice.Enabled);
            Assert.False(immediate.Enabled);
        });

        [Fact]
        public void ClubList_LeavesOutThePlayersOwnClub() => WithTransferTab((trainer, fake, view) =>
        {
            ComboBox country = Field<ComboBox>(view, "TransferCountryInput");
            ComboBox club = Field<ComboBox>(view, "TransferClubInput");

            country.SelectedValue = PlayerEnums.Country.Belgien;
            Assert.Equal(1, club.Items.Count);
            Assert.Equal("Brügge", club.GetItemText(club.SelectedItem));

            country.SelectedValue = PlayerEnums.Country.Chile;
            Assert.Equal("Colo-Colo", club.GetItemText(club.SelectedItem));
        });

        [Fact]
        public void ClubList_KeepsTheChosenClubWhileTheCountryStays() => WithTransferTab((trainer, fake, view) =>
        {
            ComboBox country = Field<ComboBox>(view, "TransferCountryInput");
            ComboBox club = Field<ComboBox>(view, "TransferClubInput");
            SetField(view, "allClubs", SampleClubs().Concat(new[] { new Club { Country = PlayerEnums.Country.Chile, Id = 5, ClubName = "Universidad" } }).ToList());
            Invoke(view, "ShowTransfer");
            country.SelectedValue = PlayerEnums.Country.Chile;
            club.SelectedValue = (ushort)5;

            Invoke(view, "ShowTransfer");

            Assert.Equal(PlayerEnums.Country.Chile, country.SelectedValue);
            Assert.Equal((ushort)5, club.SelectedValue);
        });

        [Fact]
        public void Request_StoresTheMoveAndReportsIt() => WithTransferTab((trainer, fake, view) =>
        {
            Field<GroupBox>(view, "TransferBox").Enabled = true;
            Field<ComboBox>(view, "TransferCountryInput").SelectedValue = PlayerEnums.Country.Chile;
            Field<ComboBox>(view, "TransferKindInput").SelectedValue = TransferInjector.Kind.Transfer;
            Field<TextBox>(view, "TransferFeeInput").Text = "12";
            Field<CheckBox>(view, "TransferImmediate").Checked = true;

            Invoke(view, "RequestTransfer");

            TransferInjector injector = new TransferInjector(fake.Memory);
            Assert.True(injector.IsRequestPending(), Field<Label>(view, "TransferStatus").Text);
            Assert.EndsWith("wechselt beim nächsten Tagesabschluss zu Colo-Colo.", Field<Label>(view, "TransferStatus").Text);
        });

        [Fact]
        public void Refresh_ReportsTheOutcomeOnce() => WithTransferTab((trainer, fake, view) =>
        {
            Field<GroupBox>(view, "TransferBox").Enabled = true;
            Field<ComboBox>(view, "TransferCountryInput").SelectedValue = PlayerEnums.Country.Chile;
            Invoke(view, "RequestTransfer");
            uint stub = new TransferInjector(fake.Memory).EnsureInstalled().Value;
            fake.Memory.WriteBytesAtAddress(stub + TransferInjector.FlagOffset, BitConverter.GetBytes(0u));
            fake.Memory.WriteBytesAtAddress(stub + TransferInjector.OutcomeOffset, BitConverter.GetBytes((uint)TransferInjector.Outcome.Done));

            view.RefreshPlayerListView(PlayerEnums.AddressType.OWN);

            Assert.Equal("Wechsel erledigt.", Field<Label>(view, "TransferStatus").Text);
            Assert.Equal(TransferInjector.Outcome.None, new TransferInjector(fake.Memory).TakeOutcome());
        });

        [Fact]
        public void TransferBox_IsOffForYouthPlayers() => WithTransferTab((trainer, fake, view) =>
        {
            Assert.False(Field<GroupBox>(view, "TransferBox").Enabled);
        }, PlayerEnums.AddressType.TRAINEE);

        [Fact]
        public void TransferBox_IsOffWithoutOriginalBuildAnchors() => WithTransferTab((trainer, fake, view) =>
        {
            fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
            Invoke(view, "ShowTransfer");
            Assert.False(Field<GroupBox>(view, "TransferBox").Enabled);
        });

        [Fact]
        public void Messages_CoverEveryResult()
        {
            Assert.Equal("Max wechselt beim nächsten Tagesabschluss zu Brügge.", PlayerView.TransferMessage(TransferInjector.RequestResult.Requested, "Max", "Brügge"));
            Assert.Equal("Der Spieler hat keinen Verein.", PlayerView.TransferMessage(TransferInjector.RequestResult.NoClub, "", ""));
            Assert.Equal("Das ist der aktuelle Verein des Spielers.", PlayerView.TransferMessage(TransferInjector.RequestResult.SameClub, "", ""));
            Assert.Equal("Für diese Spielversion nicht verfügbar.", PlayerView.TransferMessage(TransferInjector.RequestResult.NotSupported, "", ""));
            Assert.StartsWith("Konnte im Spiel nicht", PlayerView.TransferMessage(TransferInjector.RequestResult.HookFailed, "", ""));

            Assert.Equal("Wechsel erledigt.", PlayerView.TransferOutcomeMessage(TransferInjector.Outcome.Done));
            Assert.Contains("nicht mehr beim alten Verein", PlayerView.TransferOutcomeMessage(TransferInjector.Outcome.NotInClub));
            Assert.Contains("voll", PlayerView.TransferOutcomeMessage(TransferInjector.Outcome.SquadFull));
            Assert.Equal("", PlayerView.TransferOutcomeMessage(TransferInjector.Outcome.None));
        }
    }
}
