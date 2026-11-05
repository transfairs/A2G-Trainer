using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// PlayerView on its own (no Trainer window): the "Wechsel" and "Stammverein" controls without a
    /// club scan or player, requests that can't be made, the club selection and the jersey check
    /// before saving.
    /// </summary>
    public class PlayerViewCoverageTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private static T Field<T>(object target, string name) => (T)typeof(PlayerView).GetField(name, Private).GetValue(target);
        private static void SetField(object target, string name, object value) =>
            (typeof(PlayerView).GetField(name, Private) ?? typeof(EntityView).GetField(name, Private)).SetValue(target, value);
        private static object Invoke(object target, string name, params object[] args) => typeof(PlayerView).GetMethod(name, Private).Invoke(target, args);

        // A process controller whose trainer is only a stand-in carrying the memory (UpdatePlayerOffsets
        // reads it) - no Trainer window is created.
        private static ProcessController ControllerFor(ProcessMemory memory)
        {
            Trainer trainer = (Trainer)FormatterServices.GetUninitializedObject(typeof(Trainer));
            GC.SuppressFinalize(trainer);
            typeof(Trainer).GetField("memory", Private).SetValue(trainer, memory);
            return new ProcessController(trainer);
        }

        private static void WithView(Action<FakeModule, PlayerView> test) => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (PlayerView view = new PlayerView(fake.Memory, ControllerFor(fake.Memory)))
            {
                IntPtr handle = view.Handle;
                test(fake, view);
            }
        });

        private static List<Club> SampleClubs() => new List<Club>
        {
            new Club { Country = PlayerEnums.Country.Belgien, Id = 2, ClubName = "Brügge" },
            new Club { Country = PlayerEnums.Country.Chile, Id = 4, ClubName = "Colo-Colo" },
        };

        [Fact]
        public void AttachTransferHandlers_Twice_SetsTheControlsUpOnce() => WithView((fake, view) =>
        {
            ComboBox kind = Field<ComboBox>(view, "TransferKindInput");

            Invoke(view, "AttachTransferHandlers");
            object kinds = kind.DataSource;
            Invoke(view, "AttachTransferHandlers");

            Assert.Same(kinds, kind.DataSource);
            Assert.Equal(3, kind.Items.Count);
        });

        [Fact]
        public void UpdateTransferKind_WithoutKinds_TreatsItAsATransfer() => WithView((fake, view) =>
        {
            TextBox buyPrice = Field<TextBox>(view, "TransferBuyPriceInput");
            CheckBox immediate = Field<CheckBox>(view, "TransferImmediate");
            buyPrice.Enabled = true;
            immediate.Enabled = false;

            Invoke(view, "UpdateTransferKind");

            Assert.False(buyPrice.Enabled);
            Assert.True(immediate.Enabled);
        });

        [Fact]
        public void ShowTransfer_WithoutClubScanOrPlayer_OffersNothingAndDisablesTheBox() => WithView((fake, view) =>
        {
            GroupBox box = Field<GroupBox>(view, "TransferBox");
            box.Enabled = true;

            Invoke(view, "ShowTransfer");

            Assert.Empty(Field<ComboBox>(view, "TransferCountryInput").Items);
            Assert.Empty(Field<ComboBox>(view, "TransferClubInput").Items);
            Assert.False(box.Enabled);
        });

        [Fact]
        public void RequestTransfer_WithoutPlayerCountryOrClub_DoesNothing() => WithView((fake, view) =>
        {
            Label status = Field<Label>(view, "TransferStatus");
            ComboBox country = Field<ComboBox>(view, "TransferCountryInput");
            status.Text = "";

            // No player shown.
            Invoke(view, "RequestTransfer");
            SetField(view, "bindingSource", new BindingSource { DataSource = typeof(Player) });
            Invoke(view, "RequestTransfer");
            // A player, but no country to go to.
            Player player = new Player { NameRecordId = 1, ClubCountry = PlayerEnums.Country.Belgien, ClubId = 2 };
            SetField(view, "bindingSource", new BindingSource { DataSource = player });
            Invoke(view, "RequestTransfer");
            // A country without clubs.
            country.DisplayMember = "Text";
            country.ValueMember = "Value";
            country.DataSource = new[] { new { Value = PlayerEnums.Country.Chile, Text = "Chile" } }.ToList();
            Assert.Equal(PlayerEnums.Country.Chile, country.SelectedValue);
            Invoke(view, "RequestTransfer");

            Assert.Equal("", status.Text);
        });

        [Fact]
        public void HomeCountryChange_IsIgnoredWhileTheCombosAreFilled() => WithView((fake, view) =>
        {
            ComboBox homeCountry = Field<ComboBox>(view, "HomeCountryInput");
            Invoke(view, "AttachHomeClubHandlers");
            SetField(view, "allClubs", SampleClubs());
            Player player = new Player { HomeCountry = PlayerEnums.Country.Sonstige };
            homeCountry.DisplayMember = "Text";
            homeCountry.ValueMember = "Value";
            homeCountry.DataSource = new[] { PlayerEnums.Country.Sonstige, PlayerEnums.Country.Belgien, PlayerEnums.Country.Chile }
                .Select(c => new { Value = c, Text = c.ToString() }).ToList();

            // Attaching again adds no second handler.
            Func<int> handlers = () => ((Delegate)((System.ComponentModel.EventHandlerList)typeof(System.ComponentModel.Component)
                .GetProperty("Events", Private).GetValue(homeCountry))[typeof(ComboBox).GetField("EVENT_SELECTEDINDEXCHANGED", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null)]).GetInvocationList().Length;
            int attached = handlers();
            Invoke(view, "AttachHomeClubHandlers");
            Assert.Equal(attached, handlers());

            // No player shown.
            homeCountry.SelectedIndex = 2;
            SetField(view, "bindingSource", new BindingSource { DataSource = player });
            // Being filled.
            SetField(view, "populatingHomeClub", true);
            homeCountry.SelectedIndex = 1;
            Assert.Equal(PlayerEnums.Country.Sonstige, player.HomeCountry);

            SetField(view, "populatingHomeClub", false);
            homeCountry.SelectedIndex = 2;
            Assert.Equal(PlayerEnums.Country.Chile, player.HomeCountry);
            Assert.Equal((ushort)4, player.HomeClubId);
        });

        [Fact]
        public void ShowHomeClub_WithoutClubScanOrPlayer_OffersOnlyNoHomeClub() => WithView((fake, view) =>
        {
            ComboBox homeCountry = Field<ComboBox>(view, "HomeCountryInput");
            ComboBox homeClub = Field<ComboBox>(view, "HomeClubInput");

            Invoke(view, "ShowHomeClub", new object[] { null });

            Assert.Single(homeCountry.Items);
            Assert.Equal(PlayerEnums.Country.Sonstige, homeCountry.SelectedValue);
            Assert.Equal(PlayerView.NoHomeClubText, homeCountry.GetItemText(homeCountry.Items[0]));
            Assert.Empty(homeClub.Items);
            Assert.False(homeClub.Enabled);
        });

        [Fact]
        public void WithoutAList_ItIsNoClubAndNothingIsReloaded() => WithView((fake, view) =>
        {
            Assert.False((bool)Invoke(view, "IsClub"));
            Assert.Null(Invoke(view, "DisplayedClub"));

            Invoke(view, "ReloadPlayerListView");
            Assert.Null(view.PlayerController);
            Assert.Empty(Field<ListView>(view, "PlayerListView").Items);

            // Without a list the statistics tab follows the switch alone.
            Invoke(view, "DisableEdits", true);
            Assert.True(Field<TabPage>(view, "OtherTab").Enabled);
        });

        [Fact]
        public void ClubPicked_WithoutAListOrListener_ShowsTheClubsSquad() => WithView((fake, view) =>
        {
            fake.Memory.Layout = new PersistentLayout(Settings.PlayerRecordTableOffset, null, Settings.AgeReferenceYearOffset, Settings.ActiveTrainerCountOffset, isVerified: true, hasOriginalBuildAnchors: true);
            uint record = Settings.ClubRecordTableOffset + 2 * Settings.ClubRecordStride;
            fake.WriteModuleBytes(record, Latin1.GetBytes("Arsenal\0"));
            fake.WriteModuleBytes(record + Settings.ClubRecordCountryOffset, new[] { (byte)PlayerEnums.Country.England });
            fake.WriteModuleBytes(record + Settings.ClubRecordIdOffset, new byte[] { 4 });
            ClubSquads.Entry arsenal = ClubSquads.Clubs(fake.Memory, FixedAddresses.For(fake.Memory)).Single(c => c.Name == "Arsenal");

            Invoke(view, "ClubPicked", arsenal);

            Assert.Same(arsenal, view.ChosenClub);
            Assert.Equal(PlayerEnums.AddressType.CLUB, view.PlayerController.Type);
            Assert.Equal("Arsenal", view.PlayerController.RecordClub.Name);
            // The list belongs to the club it was read from.
            Assert.True((bool)Invoke(view, "IsClub"));
            Assert.Equal("Arsenal", ((ClubSquads.Entry)Invoke(view, "DisplayedClub")).Name);

            // The youth list stays a youth list and doesn't become the club picked last.
            SetField(view, "playerController", new PlayerController(fake.Memory, arsenal, isGog: false, PlayerEnums.AddressType.TRAINEE));
            SetField(view, "chosenClub", null);
            List<PlayerEnums.AddressType> chosen = new List<PlayerEnums.AddressType>();
            view.ClubChosen += (club, type) => chosen.Add(type);
            Invoke(view, "ClubPicked", arsenal);
            Assert.Equal(new[] { PlayerEnums.AddressType.TRAINEE }, chosen);
            Assert.Null(view.ChosenClub);
            Assert.Equal(PlayerEnums.AddressType.TRAINEE, view.PlayerController.Type);
        });

        [Fact]
        public void Save_WithAJerseyOutOfRange_WarnsAndWritesNothing() => WithView((fake, view) =>
        {
            fake.RoutePointerSlot(Settings.PlayerAddress[0]);
            PlayerController controller = new PlayerController(fake.Memory, new Club { PlayerCount = 1, AmateurPlayerCount = 0 }, isGog: false, PlayerEnums.AddressType.OWN);
            SetField(view, "playerController", controller);
            Player player = controller.EntityList.Single();
            player.Jersey = 120;
            player.Level = 9;

            string seen = null;
            bool stop = false;
            Thread poller = new Thread(() =>
            {
                while (!Volatile.Read(ref stop))
                {
                    Thread.Sleep(50);
                    IntPtr dialog = FindWindow(null, "Rückennummern");
                    if (dialog != IntPtr.Zero)
                    {
                        seen = "Rückennummern";
                        PostMessage(dialog, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                        return;
                    }
                }
            });
            poller.Start();
            try
            {
                Invoke(view, "SaveBtn_Click", null, EventArgs.Empty);
            }
            finally
            {
                Volatile.Write(ref stop, true);
                poller.Join(TimeSpan.FromSeconds(5));
            }

            Assert.Equal("Rückennummern", seen);
            Assert.Equal(0, fake.ReadDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.LEVEL], 1)[0]);
        });
    }
}
