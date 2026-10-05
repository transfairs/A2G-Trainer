using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for ProcessController's process-discovery/attach and reconnect-detection logic.
    /// Its private methods are exercised via reflection since they're only ever driven internally
    /// by Observe()'s polling loop; a real self-attach (see FakeModule) stands in for the actual
    /// game process throughout, same as the other Controller FakeModule tests. Everything runs on
    /// an STA thread (see StaThread) because it constructs a real Trainer.
    /// </summary>
    public class ProcessControllerTests
    {
        private static object InvokePrivate(object target, string methodName, params object[] args) =>
            target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        private static T GetPrivateField<T>(object target, string fieldName) =>
            (T)target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void SetPrivateField(object target, string fieldName, object value) =>
            target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static void WriteMinimalValidClub(FakeModule fake, byte id, PlayerEnums.Country country, ushort playerCount = 15)
        {
            fake.RoutePointerSlot(Settings.ClubAddress[0]);
            Addresses own = AddressPresets.OWN_CLUB;
            Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.NAME], latin1.GetBytes("Test FC\0"));
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.ID], new byte[] { id });
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.COUNTRY], new byte[] { (byte)country });
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.PLAYER_COUNT], BitConverter.GetBytes(playerCount));
        }

        [Fact]
        public void TryAttach_WhenClubDataUnreadable_ReturnsFalse() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            // Deliberately doesn't call RoutePointerSlot: a fresh FakeModule's module block is
            // guaranteed zero-initialized by VirtualAlloc, so the club-data pointer TryAttach
            // dereferences resolves to 0/unreadable deterministically - unlike a bare self-attach
            // reading real, uncontrolled process memory at a fixed offset (which isn't guaranteed
            // to come out zero, and stopped doing so once the test assembly grew large enough to
            // shift what's actually mapped at that offset in the test host process).
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                ProcessController controller = new ProcessController(trainer);

                bool attached = (bool)InvokePrivate(controller, "TryAttach", Process.GetCurrentProcess());

                Assert.False(attached);
            }
        });

        [Fact]
        public void TryAttach_WhenClubDataReadable_ReturnsTrueAndSetsIsGog() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                WriteMinimalValidClub(fake, id: 7, country: PlayerEnums.Country.Deutschland);
                ProcessController controller = new ProcessController(trainer);

                bool attached = (bool)InvokePrivate(controller, "TryAttach", Process.GetCurrentProcess());

                Assert.True(attached);
                Assert.False(controller.IsGog);
                Assert.Same(PersistentLayout.Original, trainer.Memory.Layout);
            }
        });

        [Fact]
        public void FindGame_WhenAlreadyTrackingALiveProcess_DoesNotRescan() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                ProcessController controller = new ProcessController(trainer);
                Process current = Process.GetCurrentProcess();
                SetPrivateField(controller, "gameProcess", current);

                InvokePrivate(controller, "FindGame");

                Assert.Same(current, GetPrivateField<Process>(controller, "gameProcess"));
            }
        });

        // Deliberately doesn't assert on the resulting gameProcess field: this scans every real
        // process on the machine running the test by window title, so whether a candidate is found
        // depends on what else happens to be running (e.g. a real Anstoss 2 Gold instance) - only
        // "doesn't throw" is something every environment can guarantee.
        [Fact]
        public void FindGame_ScansRealProcessesWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                ProcessController controller = new ProcessController(trainer);

                Exception thrown = Record.Exception(() => InvokePrivate(controller, "FindGame"));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void DetectOwnClubChange_WhenMemoryNotAttached_SwallowsExceptionAndReturnsFalse() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                ProcessController controller = new ProcessController(trainer);

                bool changed = (bool)InvokePrivate(controller, "DetectOwnClubChange");

                Assert.False(changed);
            }
        });

        [Fact]
        public void DetectOwnClubChange_FullLifecycle_DetectsRecoveryThenHandoffButNotAnUnchangedClub() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                ProcessController controller = new ProcessController(trainer);

                // Starts invalid (PlayerCount 0) - marks sawInvalidClubState, reports no change.
                WriteMinimalValidClub(fake, id: 1, country: PlayerEnums.Country.Deutschland, playerCount: 0);
                Assert.False((bool)InvokePrivate(controller, "DetectOwnClubChange"));
                Assert.True(GetPrivateField<bool>(controller, "sawInvalidClubState"));

                // Recovers (now readable) - this is the "savegame just loaded" tick.
                WriteMinimalValidClub(fake, id: 1, country: PlayerEnums.Country.Deutschland);
                Assert.True((bool)InvokePrivate(controller, "DetectOwnClubChange"));
                Assert.False(GetPrivateField<bool>(controller, "sawInvalidClubState"));

                // Same club again - no change.
                Assert.False((bool)InvokePrivate(controller, "DetectOwnClubChange"));

                // Different club Id now shown in the own-club slot - a hot-seat handoff.
                WriteMinimalValidClub(fake, id: 2, country: PlayerEnums.Country.Deutschland);
                Assert.True((bool)InvokePrivate(controller, "DetectOwnClubChange"));
            }
        });

        [Fact]
        public void RefreshView_WhenActionThrows_SwallowsException() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                ProcessController controller = new ProcessController(trainer);

                Exception thrown = Record.Exception(() => InvokePrivate(controller, "RefreshView", "TestView", new Action(() => throw new InvalidOperationException("boom"))));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void RefreshView_WhenActionSucceeds_RunsItOnce() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                ProcessController controller = new ProcessController(trainer);
                int calls = 0;

                InvokePrivate(controller, "RefreshView", "TestView", new Action(() => calls++));

                Assert.Equal(1, calls);
            }
        });

        [Fact]
        public void UpdateGameProcess_WhenTrainerHandleNotCreated_DoesNothing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                ProcessController controller = new ProcessController(trainer);
                SetPrivateField(controller, "gameProcess", Process.GetCurrentProcess());

                Exception thrown = Record.Exception(() => InvokePrivate(controller, "UpdateGameProcess"));

                Assert.Null(thrown);
                Assert.False(trainer.IsHandleCreated);
            }
        });

        [Fact]
        public void ClearGameProcess_WhenHandleNotCreated_DoesNotThrow() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                ProcessController controller = new ProcessController(trainer);

                Exception thrown = Record.Exception(() => InvokePrivate(controller, "ClearGameProcess"));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void UpdatePlayerOffsets_WithReadableClub_DoesNotThrow() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                WriteMinimalValidClub(fake, id: 3, country: PlayerEnums.Country.England);
                ProcessController controller = new ProcessController(trainer);

                Exception thrown = Record.Exception(() => controller.UpdatePlayerOffsets());

                Assert.Null(thrown);
            }
        });
    }
}
