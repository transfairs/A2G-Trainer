using AnstossTrainer.Shared.Tests;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
    /// Tests for PlayerView's save/reload buttons, roster selection, link-click handler, and the
    /// background freeze-timer tick (Condition/Freshness auto-write). A FakeModule with PlayerCount
    /// 1 is enough here - the player field values themselves don't matter for these handlers, only
    /// that RefreshPlayerListView/InitMainTabControl produce one real, bound Player row to act on.
    /// Every FakeModule below is explicitly built on trainer.Memory (memory: trainer.Memory) rather
    /// than its own default - otherwise the view's IsGameRunning() sees a still-unattached
    /// trainer.Memory and pops a real, blocking "process not found" MessageBox.
    /// </summary>
    public class PlayerViewAdditionalTests
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        private static object InvokePrivate(object target, string methodName, params object[] args) =>
            target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        private static T GetPrivateField<T>(object target, string fieldName) =>
            (T)target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        // SaveBtn_Click pops a real modal "Nicht dauerhaft gespeichert" MessageBox whenever a
        // persistent-name write couldn't be resolved (guaranteed here, since no name-pool pointer is
        // routed). The MessageBox.Show call itself must stay on the view's own STA thread (it - and
        // the controls SaveBtn_Click touches - were created there), so only the dialog-closing poll
        // runs on a second thread, same technique as EntityViewTests' IsGameRunning dialog test.
        private static void InvokeAndDismissWarningDialog(object target, string methodName, params object[] args)
        {
            bool stopPolling = false;
            Thread poller = new Thread(() =>
            {
                IntPtr dialogHandle = IntPtr.Zero;
                while (!Volatile.Read(ref stopPolling) && dialogHandle == IntPtr.Zero)
                {
                    Thread.Sleep(50);
                    dialogHandle = FindWindow(null, "Nicht dauerhaft gespeichert");
                }
                if (dialogHandle != IntPtr.Zero)
                    PostMessage(dialogHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            });
            poller.Start();
            try
            {
                InvokePrivate(target, methodName, args);
            }
            finally
            {
                Volatile.Write(ref stopPolling, true);
                poller.Join(TimeSpan.FromSeconds(5));
            }
        }

        private static void SeedOneOwnPlayerAndRefresh(Trainer trainer, FakeModule fake)
        {
            fake.RoutePointerSlot(Settings.ClubAddress[0]);
            Addresses own = AddressPresets.OWN_CLUB;
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 1 });

            trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);
            trainer.PlayerView.InitMainTabControl();
        }

        // The freeze tick only writes while the roster's slot still holds the same players, which it
        // checks via the player ID - so the player pointer has to resolve too, not just the club's.
        private static void SeedOneOwnPlayerWithSlotAndRefresh(Trainer trainer, FakeModule fake)
        {
            fake.RoutePointerSlot(Settings.PlayerAddress[0]);
            SeedOneOwnPlayerAndRefresh(trainer, fake);
        }

        // Accessing a WinForms control's own Handle property forces it to be created even with no
        // parent Form ever Shown - and, once created, a ListViewItem's Selected setter goes through
        // the real native LVM_SETITEMSTATE call, which both populates SelectedItems and raises
        // SelectedIndexChanged synchronously (verified empirically; unlike the ComboBox/Binding
        // timing issues noted elsewhere in this suite, forcing the handle *does* fix ListView).
        [Fact]
        public void PlayerListViewSelectedIndexChanged_SelectingARow_RebindsTheDetailFields() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedOneOwnPlayerAndRefresh(trainer, fake);
                ListView playerListView = GetPrivateField<ListView>(trainer.PlayerView, "PlayerListView");
                IntPtr forceHandle = playerListView.Handle;

                Exception thrown = Record.Exception(() => playerListView.Items[0].Selected = true);

                Assert.Null(thrown);
                Assert.Single(playerListView.SelectedItems);
                var bindingSource = GetPrivateField<BindingSource>(trainer.PlayerView, "bindingSource");
                Assert.Same(playerListView.Items[0].Tag, bindingSource.DataSource);

                // The kind of injury is chosen by the game's names.
                ComboBox injury = GetPrivateField<ComboBox>(trainer.PlayerView, "InjuryKindInput");
                Assert.Equal(nameof(Player.InjuryKind), injury.DataBindings["SelectedValue"].BindingMemberInfo.BindingField);
                Assert.Equal(Enum.GetValues(typeof(PlayerEnums.Injury)).Length, injury.Items.Count);
                Assert.Equal("keine", injury.GetItemText(injury.Items[0]));
            }
        });

        // Counterpart covering the other half of PlayerListView_SelectedIndexChanged's condition
        // (selectedPlayers.Count > 0 true, but bindingSource still null since InitMainTabControl
        // hasn't run yet) - only reachable, same as above, once the ListView's handle is forced.
        [Fact]
        public void PlayerListViewSelectedIndexChanged_BeforeInitMainTabControl_DoesNothingSinceBindingSourceIsStillNull() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 1 });
                // RefreshPlayerListView only, no InitMainTabControl yet - bindingSource is still null,
                // so selecting a row must be a safe no-op rather than a NullReferenceException.
                trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);
                ListView playerListView = GetPrivateField<ListView>(trainer.PlayerView, "PlayerListView");
                IntPtr forceHandle = playerListView.Handle;

                Exception thrown = Record.Exception(() => playerListView.Items[0].Selected = true);

                Assert.Null(thrown);
                Assert.Single(playerListView.SelectedItems);
                Assert.Null(GetPrivateField<BindingSource>(trainer.PlayerView, "bindingSource"));
            }
        });

        // RefreshPlayerListView's own "re-find and reselect the previously selected player" branch
        // (PlayerListView.SelectedItems.Count > 0, then matching the previous Tag's Id against each
        // freshly-read Player - View/PlayerView.cs lines 421/436-439) needs a genuinely populated
        // SelectedItems going into the reload, same handle-forcing fix as above, plus at least two
        // players so the match (i=0) and non-match (i=1) branches of the Id comparison both fire.
        [Fact]
        public void RefreshPlayerListView_CalledAgainWithARowSelected_ReselectsTheMatchingPlayer() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 2 });

                trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);
                trainer.PlayerView.InitMainTabControl();

                ListView playerListView = GetPrivateField<ListView>(trainer.PlayerView, "PlayerListView");
                IntPtr forceHandle = playerListView.Handle;
                Player previouslySelected = (Player)playerListView.Items[1].Tag;
                playerListView.Items[1].Selected = true;
                Assert.Single(playerListView.SelectedItems);

                Exception thrown = Record.Exception(() => trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN));

                Assert.Null(thrown);
                Assert.True(playerListView.Items[1].Selected);
                Assert.Equal(previouslySelected.Id, ((Player)playerListView.Items[1].Tag).Id);
            }
        });
        [Fact]
        public void RefreshPlayerListView_ShowsTheAmateursOnTheirOwnBackground() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 2 });
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT], new byte[] { 1 });

                trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);

                ListView playerListView = GetPrivateField<ListView>(trainer.PlayerView, "PlayerListView");
                Assert.Equal(3, playerListView.Items.Count);
                Assert.Equal(new[] { false, false, true }, playerListView.Items.Cast<ListViewItem>().Select(item => ((Player)item.Tag).IsAmateur));
                Assert.NotEqual(PlayerView.AmateurBackColor, playerListView.Items[1].BackColor);
                Assert.Equal(PlayerView.AmateurBackColor, playerListView.Items[2].BackColor);
            }
        });

        [Fact]
        public void ReloadBtnClick_CalledASecondTime_StillDoesNotThrow() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedOneOwnPlayerAndRefresh(trainer, fake);

                InvokePrivate(trainer.PlayerView, "ReloadBtn_Click", null, EventArgs.Empty);
                Exception thrown = Record.Exception(() => InvokePrivate(trainer.PlayerView, "ReloadBtn_Click", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void IsClub_WhenAPlayersClubIdDoesNotMatchTheClub_ReturnsFalseViaTheRosterMismatchPath() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                Addresses own = AddressPresets.OWN_CLUB;
                // A non-empty club name is required to even enter IsClub()'s comparison loop; the
                // club's own Id is set to a value no (unrouted, so default-Id-0) player will match,
                // triggering the roster-mismatch branch instead of the empty-name early return.
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.NAME], System.Text.Encoding.GetEncoding("iso-8859-1").GetBytes("Test FC\0"));
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.ID], new byte[] { 5 });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 1 });

                trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);

                Assert.False((bool)InvokePrivate(trainer.PlayerView, "IsClub"));
            }
        });

        [Fact]
        public void SaveBtnClick_WithMoreThanFourNameWarnings_TruncatesTheDialogAndCountsTheRest() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                // 3 players x up to 2 name warnings each (Firstname+Lastname both unresolvable, since
                // no name-pool pointer is routed) comfortably clears the dialog's 4-line preview cap.
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 3 });
                trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);
                trainer.PlayerView.InitMainTabControl();

                Exception thrown = Record.Exception(() => InvokeAndDismissWarningDialog(trainer.PlayerView, "SaveBtn_Click", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void ReloadBtnClick_RefreshesUsingTheCurrentControllerType() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedOneOwnPlayerAndRefresh(trainer, fake);

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.PlayerView, "ReloadBtn_Click", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void SaveBtnClick_WithTeamOverridesAndAFreezeCheck_SavesAndRefreshesWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedOneOwnPlayerAndRefresh(trainer, fake);

                GetPrivateField<TextBox>(trainer.PlayerView, "TeamLevelInput").Text = "10";
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamFormInput").Text = "20";
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamAgeInput").Text = "25";
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamConditionInput").Text = "30";
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamFreshnessInput").Text = "40";
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamContractDurationInput").Text = "3";
                GetPrivateField<CheckBox>(trainer.PlayerView, "TeamConditionFreezeCheck").Checked = true;
                GetPrivateField<CheckBox>(trainer.PlayerView, "TeamFreshnessFreezeCheck").Checked = true;

                Exception thrown = Record.Exception(() => InvokeAndDismissWarningDialog(trainer.PlayerView, "SaveBtn_Click", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void SaveBtnClick_WithNoTeamOverridesEntered_LeavesPlayerFieldsAlone() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedOneOwnPlayerAndRefresh(trainer, fake);

                Exception thrown = Record.Exception(() => InvokeAndDismissWarningDialog(trainer.PlayerView, "SaveBtn_Click", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void FreezeTimerTick_WithBothFreezeChecksEnabled_WritesConditionAndFreshness() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedOneOwnPlayerWithSlotAndRefresh(trainer, fake);

                GetPrivateField<CheckBox>(trainer.PlayerView, "TeamConditionFreezeCheck").Checked = true;
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamConditionInput").Text = "55";
                GetPrivateField<CheckBox>(trainer.PlayerView, "TeamFreshnessFreezeCheck").Checked = true;
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamFreshnessInput").Text = "66";

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.PlayerView, "FreezeTimer_Tick", null, EventArgs.Empty));

                Assert.Null(thrown);
                Assert.Equal((byte)55, trainer.PlayerView.PlayerController.EntityList[0].Condition);
                Assert.Equal((byte)66, trainer.PlayerView.PlayerController.EntityList[0].Freshness);
                Assert.Equal((byte)55, fake.ReadDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.CONDITION], 1)[0]);
                Assert.Equal((byte)66, fake.ReadDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.FRESHNESS], 1)[0]);
            }
        });

        [Fact]
        public void FreezeTimerTick_WhenAnotherClubIsLoadedIntoTheSlot_LeavesItsPlayersAlone() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedOneOwnPlayerWithSlotAndRefresh(trainer, fake);
                Player player = trainer.PlayerView.PlayerController.EntityList[0];
                byte conditionBefore = player.Condition;

                // Wochenabschluss/match: the game swaps a foreign player into the frozen roster's slot
                string idAddress = AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.ID];
                fake.WriteDisplayCacheBytes(idAddress, BitConverter.GetBytes((ushort)(player.NameRecordId + 1)));

                GetPrivateField<CheckBox>(trainer.PlayerView, "TeamConditionFreezeCheck").Checked = true;
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamConditionInput").Text = "55";

                InvokePrivate(trainer.PlayerView, "FreezeTimer_Tick", null, EventArgs.Empty);

                Assert.Equal(conditionBefore, player.Condition);
                Assert.Equal(conditionBefore, fake.ReadDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.CONDITION], 1)[0]);
            }
        });

        [Fact]
        public void FreezeTimerTick_WithNoFreezeChecksEnabled_DoesNothing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedOneOwnPlayerWithSlotAndRefresh(trainer, fake);

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.PlayerView, "FreezeTimer_Tick", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void FreezeTimerTick_WhenGameNotRunning_ReturnsSilentlyWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                // Not attached at all - IsGameRunning(silent: true) must short-circuit before
                // touching playerController (still null at this point).
                Exception thrown = Record.Exception(() => InvokePrivate(trainer.PlayerView, "FreezeTimer_Tick", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void FreezeTimerTick_WhenSaveThrows_LogsAndSwallowsTheException() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedOneOwnPlayerWithSlotAndRefresh(trainer, fake);

                GetPrivateField<CheckBox>(trainer.PlayerView, "TeamConditionFreezeCheck").Checked = true;
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamConditionInput").Text = "55";
                // A null EntityList entry makes ApplyFrozenValuesToPlayer/Save throw a
                // NullReferenceException partway through the foreach - the tick must still return
                // normally (logged, not rethrown) rather than take the app down.
                trainer.PlayerView.PlayerController.EntityList.Add(null);

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.PlayerView, "FreezeTimer_Tick", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void GithubLinkLabelLinkClicked_WithNonStringLinkData_DoesNothing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                LinkLabel.Link link = new LinkLabel.Link { LinkData = 7 };
                LinkLabelLinkClickedEventArgs e = new LinkLabelLinkClickedEventArgs(link);

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.PlayerView, "GithubLinkLabel_LinkClicked", null, e));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void InitMainTabControl_WithNoPlayersInTheListView_SkipsBindingSetup() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                // PlayerListView starts empty (never refreshed) - InitMainTabControl's own guard
                // must skip the whole binding block rather than fail on an empty Items[0].
                Exception thrown = Record.Exception(() => trainer.PlayerView.InitMainTabControl());

                Assert.Null(thrown);
            }
        });
    }
}
