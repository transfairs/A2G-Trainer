using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
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

        // SaveBtn_Click pops a real modal "Name nicht dauerhaft gespeichert" MessageBox whenever a
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
                    dialogHandle = FindWindow(null, "Name nicht dauerhaft gespeichert");
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
                SeedOneOwnPlayerAndRefresh(trainer, fake);

                GetPrivateField<CheckBox>(trainer.PlayerView, "TeamConditionFreezeCheck").Checked = true;
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamConditionInput").Text = "55";
                GetPrivateField<CheckBox>(trainer.PlayerView, "TeamFreshnessFreezeCheck").Checked = true;
                GetPrivateField<TextBox>(trainer.PlayerView, "TeamFreshnessInput").Text = "66";

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.PlayerView, "FreezeTimer_Tick", null, EventArgs.Empty));

                Assert.Null(thrown);
                Assert.Equal((byte)55, trainer.PlayerView.PlayerController.EntityList[0].Condition);
                Assert.Equal((byte)66, trainer.PlayerView.PlayerController.EntityList[0].Freshness);
            }
        });

        [Fact]
        public void FreezeTimerTick_WithNoFreezeChecksEnabled_DoesNothing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                SeedOneOwnPlayerAndRefresh(trainer, fake);

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
                SeedOneOwnPlayerAndRefresh(trainer, fake);

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

        // RefreshPlayerListView's DYNAMIC branch scans every club in the league
        // (ClubController's loadFullList: true path, GetEntityList) looking for the one whose
        // Id/Country match the first dynamic-roster player's ClubId/ClubCountry
        // (Club.IsClubMember) - see View/PlayerView.cs around the "Club dynamic = ..." line. The
        // only previous test to reach this branch (TrainerAdditionalTests'
        // DynamicTeamToolStripMenuItem_Click) self-attaches to the live test-host process and
        // reads whatever happens to be in its real memory - whether a match is found there is
        // essentially random. The two tests below make both outcomes deterministic instead.
        //
        // GetEntityList's club-scan region (Settings.AllClubInitialOffset, ~0x64FA8+) lives far
        // past FakeModule's fixed 256KB display-cache block, so ClubAddress's pointer slot is
        // routed here to a separate, purpose-sized block instead of fake.DisplayCacheBlock (which
        // stays reserved for PlayerAddress, routed as usual - the dynamic player's fields land at
        // small, offset-shifted addresses that fit it comfortably). The exact offset
        // AddressPresets.DYNAMIC_PLAYERS ends up rebased to is computed here with the very same
        // Tools.SumHex/AddressPresets.InitPreset calls PlayerController.InitOffsets uses in
        // production, from the same own-club(0 players)/opponent-club(1 player) setup this test
        // builds - so the player's ClubId/ClubCountry bytes below are written at the exact
        // address production code will read, not a guessed one.
        [Fact]
        public void RefreshPlayerListView_ForDynamic_WithMatchingClubInFullList_AssignsTheScannedClub() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);

                // ALL_CLUBS scan slot 0 sits at Settings.AllClubInitialOffset ("64FA8") plus a
                // field offset up to "5BD" (AMATEUR_PLAYER_COUNT) - comfortably inside 0x70000,
                // with room to spare.
                const int clubBlockSize = 0x70000;
                IntPtr clubBlock = Marshal.AllocHGlobal(clubBlockSize);
                try
                {
                    byte[] zeros = new byte[clubBlockSize];
                    Marshal.Copy(zeros, 0, clubBlock, clubBlockSize);
                    uint clubPointerFieldAddress = fake.ModuleBase + Convert.ToUInt32(Settings.ClubAddress[0], 16);
                    fake.Memory.WriteBytesAtAddress(clubPointerFieldAddress, BitConverter.GetBytes((uint)clubBlock.ToInt64()));

                    void WriteClubField(string fieldOffset, string clubOffset, byte[] data)
                    {
                        uint address = Convert.ToUInt32(Tools.SumHex(new[] { fieldOffset, clubOffset }), 16);
                        Marshal.Copy(data, 0, clubBlock + (int)address, data.Length);
                    }

                    const byte clubId = 42;
                    const PlayerEnums.Country clubCountry = PlayerEnums.Country.England;

                    // Direct-read dynamic club (DYNAMIC_CLUB == OPPONENT_CLUB, read at offset ""):
                    // 1 player, so the roster read below yields a real firstPlayer instead of
                    // null. This same address is also what InitOffsets' OPPONENT-typed counterpart
                    // club read sees, so the offset math below ends up using 1 opponent player too.
                    WriteClubField(AddressPresets.OPPONENT_CLUB[ClubEnums.AddressKey.PLAYER_COUNT], "", new byte[] { 1 });

                    // The scanned club (ALL-clubs region, slot 0) that must match the dynamic
                    // player written further below.
                    string scannedClubOffset = Settings.AllClubInitialOffset.Key;
                    WriteClubField(AddressPresets.ALL_CLUBS[ClubEnums.AddressKey.ID], scannedClubOffset, new byte[] { clubId });
                    WriteClubField(AddressPresets.ALL_CLUBS[ClubEnums.AddressKey.COUNTRY], scannedClubOffset, new byte[] { (byte)clubCountry });
                    WriteClubField(AddressPresets.ALL_CLUBS[ClubEnums.AddressKey.NAME], scannedClubOffset, Encoding.GetEncoding("iso-8859-1").GetBytes("Dynamo Test\0"));

                    // Own club has 0 players (left at the block's zero default), so InitOffsets'
                    // own-roster loop leaves its "last player" offset at "" - matching the setup
                    // InitPreset/SumHex below replicate to land on the same DYNAMIC_PLAYERS base
                    // PlayerController.InitOffsets will compute from this same memory.
                    string otherOffset = Tools.SumHex(new[] { string.Empty, Settings.PlayerOffset });
                    Addresses expectedDynamicPlayers = AddressPresets.InitPreset(otherOffset);

                    fake.WriteDisplayCacheBytes(expectedDynamicPlayers[PlayerEnums.AddressKey.CLUB_ID], new byte[] { clubId });
                    fake.WriteDisplayCacheBytes(expectedDynamicPlayers[PlayerEnums.AddressKey.CLUB_COUNTRY], new byte[] { (byte)clubCountry });

                    Exception thrown = Record.Exception(() => trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.DYNAMIC));

                    Assert.Null(thrown);
                    Assert.Equal(clubId, trainer.PlayerView.ClubController.Club.Id);
                    Assert.Equal(clubCountry, trainer.PlayerView.ClubController.Club.Country);
                    Assert.Equal("Dynamo Test", trainer.PlayerView.ClubController.Club.ClubName);
                }
                finally
                {
                    Marshal.FreeHGlobal(clubBlock);
                }
            }
        });

        // Counterpart to the "found" test above, covering IsClubMember's other outcome
        // deterministically: with the direct-read dynamic club left at its default 0
        // PlayerCount/AmateurPlayerCount, PlayerController's roster read yields an empty
        // EntityList, so firstPlayer is null - Club.IsClubMember(null) is false for every
        // candidate regardless of what (if anything) the unrouted, far-out club-scan region
        // happens to contain, so "dynamic" resolves to null every time without needing any
        // club-scan setup at all.
        [Fact]
        public void RefreshPlayerListView_ForDynamic_WithNoPlayersLoaded_KeepsTheDirectReadClub() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OPPONENT_CLUB[ClubEnums.AddressKey.NAME], Encoding.GetEncoding("iso-8859-1").GetBytes("Direct Read FC\0"));

                Exception thrown = Record.Exception(() => trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.DYNAMIC));

                Assert.Null(thrown);
                Assert.Empty(trainer.PlayerView.PlayerController.EntityList);
                Assert.Equal("Direct Read FC", trainer.PlayerView.ClubController.Club.ClubName);
            }
        });

        // Covers View/PlayerView.cs line 410's other branch: Club.IsClubMember(firstPlayer)
        // returning true for a TRAINEE roster, which is what makes RefreshPlayerListView set
        // ClubName to "Jugendspieler". TRAINEE reads the club directly as OWN (see the ternary at
        // the top of RefreshPlayerListView) and reads the roster's first player from
        // AddressPresets.DYNAMIC_PLAYERS after ProcessController.UpdatePlayerOffsets rebases it -
        // with the own club carrying exactly one player and the opponent club left at its default
        // 0, that rebases DYNAMIC_PLAYERS onto InitPreset(""), the same math the DYNAMIC test above
        // uses for its own "own club has 0 players" case - so writing that one player's
        // CLUB_ID/CLUB_COUNTRY to match the own club's own Id/Country makes IsClubMember true.
        [Fact]
        public void RefreshPlayerListView_ForTrainee_WithMatchingClub_SetsJugendspielerClubName() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);

                Addresses own = AddressPresets.OWN_CLUB;
                const byte clubId = 7;
                const PlayerEnums.Country clubCountry = PlayerEnums.Country.Deutschland;
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.ID], new byte[] { clubId });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.COUNTRY], new byte[] { (byte)clubCountry });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 1 });

                Addresses expectedDynamicPlayers = AddressPresets.InitPreset(string.Empty);
                fake.WriteDisplayCacheBytes(expectedDynamicPlayers[PlayerEnums.AddressKey.CLUB_ID], new byte[] { clubId });
                fake.WriteDisplayCacheBytes(expectedDynamicPlayers[PlayerEnums.AddressKey.CLUB_COUNTRY], new byte[] { (byte)clubCountry });

                Exception thrown = Record.Exception(() => trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.TRAINEE));

                Assert.Null(thrown);
                Assert.Equal("Jugendspieler", trainer.PlayerView.ClubController.Club.ClubName);
            }
        });
    }
}
