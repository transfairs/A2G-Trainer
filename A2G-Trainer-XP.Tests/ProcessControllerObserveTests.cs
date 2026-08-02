using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    // Overrides the real Process.GetProcesses() scan with a controlled list - the real one depends
    // on whatever else is running on the test machine (including a real Anstoss 2 Gold instance,
    // which several tests below tripped over before this existed).
    internal class TestableProcessController : ProcessController
    {
        public TestableProcessController(Trainer trainer) : base(trainer) { }
        public System.Collections.Generic.IEnumerable<Process> GameCandidates { get; set; } = System.Linq.Enumerable.Empty<Process>();
        internal override System.Collections.Generic.IEnumerable<Process> GetGameCandidates() => this.GameCandidates;
    }

    /// <summary>
    /// Tests for ProcessController.Observe's polling loop and the exception-swallowing catches
    /// around it and around DetectOwnClubChange/TryAttach. Several of these force a construction
    /// failure by reflectively nulling an already-attached Trainer's Memory field after the fact -
    /// production code can never observe that state (the constructor always sets a real one), but
    /// it's the only way to make the guarded ClubController construction actually throw so the
    /// surrounding catch block (defensive against "construction failed for any reason") is
    /// genuinely exercised rather than just its ordinary not-yet-readable-club fallback.
    /// </summary>
    public class ProcessControllerObserveTests
    {
        // Private members aren't visible via reflection on a derived type (e.g.
        // TestableProcessController) even with NonPublic set - walk up to whichever type in the
        // hierarchy actually declares the member.
        private static MethodInfo FindMethod(Type type, string methodName)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                MethodInfo method = t.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (method != null)
                    return method;
            }
            throw new MissingMethodException(type.FullName, methodName);
        }

        private static FieldInfo FindField(Type type, string fieldName)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;
            }
            throw new MissingFieldException(type.FullName, fieldName);
        }

        private static object InvokePrivate(object target, string methodName, params object[] args) =>
            FindMethod(target.GetType(), methodName).Invoke(target, args);

        private static T GetPrivateField<T>(object target, string fieldName) =>
            (T)FindField(target.GetType(), fieldName).GetValue(target);

        private static void SetPrivateField(object target, string fieldName, object value) =>
            FindField(target.GetType(), fieldName).SetValue(target, value);

        private static void NullOutTrainerMemory(Trainer trainer) => SetPrivateField(trainer, "memory", null);

        [Fact]
        public void Observe_RunsUntilShutDownThenReturnsWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                // Empty candidates guarantee gameProcess stays null, so this deterministically hits
                // the "else" branch (ClearGameProcess) every tick regardless of what's really
                // running on the machine.
                TestableProcessController controller = new TestableProcessController(trainer);
                Thread observeThread = new Thread(() => InvokePrivate(controller, "Observe"));
                observeThread.Start();

                // Let Observe() run at least one full iteration (FindGame + the gameProcess==null
                // "else" branch, i.e. ClearGameProcess) before asking it to stop.
                Thread.Sleep(200);
                SetPrivateField(trainer, "shutDown", true);

                Assert.True(observeThread.Join(TimeSpan.FromSeconds(5)));
            }
        });

        [Fact]
        public void FindGame_WithNoCandidates_LeavesGameProcessNullAfterScanningTheEmptyList() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                TestableProcessController controller = new TestableProcessController(trainer);

                InvokePrivate(controller, "FindGame");

                Assert.Null(GetPrivateField<Process>(controller, "gameProcess"));
            }
        });

        [Fact]
        public void FindGame_WithACandidateThatFailsToAttach_CompletesTheLoopWithoutAttaching() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                // Deliberately doesn't route the club pointer, so TryAttach fails for this
                // candidate - the foreach loop then has to run its enumerator to exhaustion
                // (rather than exiting empty or via an early return) to reach the closing brace.
                TestableProcessController controller = new TestableProcessController(trainer) { GameCandidates = new[] { Process.GetCurrentProcess() } };

                InvokePrivate(controller, "FindGame");

                Assert.Null(GetPrivateField<Process>(controller, "gameProcess"));
            }
        });

        [Fact]
        public void FindGame_WhenACandidateHasReadableClubData_AttachesToItAndStops() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 1 });
                Process self = Process.GetCurrentProcess();
                TestableProcessController controller = new TestableProcessController(trainer) { GameCandidates = new[] { self } };

                InvokePrivate(controller, "FindGame");

                Assert.Same(self, GetPrivateField<Process>(controller, "gameProcess"));
            }
        });

        [Fact]
        public void TryAttach_WhenOpenProcessFails_ReturnsFalse() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                ProcessController controller = new ProcessController(trainer);

                // PID 4 ("System") is always a different, protected process - PROCESS_ALL_ACCESS
                // against it is denied for a non-elevated caller, so OpenProcess itself fails here
                // (distinct from the "construction throws" and "unreadable club" cases covered
                // elsewhere).
                bool attached = (bool)InvokePrivate(controller, "TryAttach", Process.GetProcessById(4));

                Assert.False(attached);
            }
        });

        [Fact]
        public void Observe_WhenTrainerIsUnusable_LogsAndReturnsWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                ProcessController controller = new ProcessController(trainer);
                // this.trainer.ShutDown throws immediately if trainer itself is null - forces
                // Observe()'s outer catch without needing a live loop iteration.
                SetPrivateField(controller, "trainer", null);

                Exception thrown = Record.Exception(() => InvokePrivate(controller, "Observe"));

                Assert.Null(thrown);
            }
        });

        // TryAttach's own equivalent catch (around the same ClubController construction) can't use
        // the "null out Memory" trick above - TryAttach calls
        // this.trainer.Memory.OpenProcess(candidate.Id) *before* that construction, and that call
        // needs Memory to be non-null/attached to succeed. Instead, this makes the construction
        // itself throw a FormatException by temporarily corrupting the shared, mutable
        // Settings.ClubAddress[0] (not readonly) with an unparsable hex string - restored in
        // finally, and safe only because the test assembly disables parallelization (see
        // AssemblyInfo.cs), so no other test can observe the address mid-flight.
        [Fact]
        public void TryAttach_WhenClubControllerConstructionThrows_ReturnsFalse() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                ProcessController controller = new ProcessController(trainer);
                string original = Settings.ClubAddress[0];
                Settings.ClubAddress[0] = "ZZZZ";
                try
                {
                    bool attached = (bool)InvokePrivate(controller, "TryAttach", Process.GetCurrentProcess());

                    Assert.False(attached);
                }
                finally
                {
                    Settings.ClubAddress[0] = original;
                }
            }
        });

        [Fact]
        public void DetectOwnClubChange_WhenClubControllerConstructionThrows_ReturnsFalse() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                NullOutTrainerMemory(trainer);
                ProcessController controller = new ProcessController(trainer);

                bool changed = (bool)InvokePrivate(controller, "DetectOwnClubChange");

                Assert.False(changed);
                Assert.True(GetPrivateField<bool>(controller, "sawInvalidClubState"));
            }
        });

        [Fact]
        public void UpdateGameProcess_WhenGameProcessIsNull_HitsItsOwnCatchAndMarksUninitialised() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                IntPtr forceHandleCreation = trainer.Handle; // IsHandleCreated must be true for Invoke to run inline here.
                ProcessController controller = new ProcessController(trainer);
                // gameProcess is deliberately left null (FindGame was never run) so
                // this.gameProcess.Id inside the try block throws a NullReferenceException.

                Exception thrown = Record.Exception(() => InvokePrivate(controller, "UpdateGameProcess"));

                Assert.Null(thrown);
                Assert.False(GetPrivateField<bool>(controller, "initialised"));
            }
        });

        [Fact]
        public void ClearGameProcess_WhenCalledFromAnotherThread_MarshalsToTheOwningThreadAndClearsState() => StaThread.Run(() =>
        {
            // Control.Invoke's marshaled call is only processed by a message pump running on the
            // thread that created the control's handle - that's *this* STA thread, so the pump
            // below must run here too, not on some other thread, even though it's also STA.
            using (Trainer trainer = new Trainer())
            {
                IntPtr forceHandleCreation = trainer.Handle;
                trainer.Anstoss.Process = Process.GetCurrentProcess();
                trainer.Anstoss.IsRunning = true;
                ProcessController controller = new ProcessController(trainer);

                Thread callFromOtherThread = new Thread(() => InvokePrivate(controller, "ClearGameProcess"));
                callFromOtherThread.Start();
                while (callFromOtherThread.IsAlive)
                    Application.DoEvents();
                callFromOtherThread.Join(TimeSpan.FromSeconds(5));

                Assert.Null(trainer.Anstoss.Process);
                Assert.False(trainer.Anstoss.IsRunning);
            }
        });
    }
}
