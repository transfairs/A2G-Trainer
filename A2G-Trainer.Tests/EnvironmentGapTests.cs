using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Threading;
using System.Windows.Forms;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Paths that depend on the surroundings: the quiz help while the game is in the foreground and
    /// while no window is, and a weekly limit mode in the game the view doesn't know.
    /// </summary>
    public class EnvironmentGapTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;
        private const int FirstAnswerId = 0x1182;
        private const int QuestionId = 0x1185;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsChild = 0x40000000;
        private const int WsVisible = 0x10000000;
        private const int WsExToolWindow = 0x80;
        private const int WsExNoActivate = 0x08000000;
        private const int SwShowNoActivate = 4;
        private const int Far = -3000;

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWindowEx(int exStyle, string className, string title, int style, int x, int y, int width, int height, IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);
        [DllImport("user32.dll")]
        private static extern bool DestroyWindow(IntPtr window);
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr window, int command);
        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr dialog, int id);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateDesktop(string name, IntPtr device, IntPtr deviceMode, uint flags, uint access, IntPtr attributes);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetThreadDesktop(IntPtr desktop);
        [DllImport("user32.dll")]
        private static extern bool CloseDesktop(IntPtr desktop);

        private static object Call(object target, string method, params object[] arguments)
        {
            try
            {
                return target.GetType().GetMethod(method, Private).Invoke(target, arguments);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
        }

        private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);

        private static ProcessMemory SelfAttached()
        {
            ProcessMemory memory = new ProcessMemory();
            Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));
            return memory;
        }

        // ---- QuizHelper -------------------------------------------------------------------------

        // A view's thread that never runs what is posted to it: the marks are only asked for.
        private sealed class SilentContext : SynchronizationContext
        {
            public override void Post(SendOrPostCallback d, object state)
            {
            }
        }

        // A culture that runs an action whenever a number is formatted on its thread.
        private sealed class HookCulture : CultureInfo
        {
            private readonly Action hook;

            public HookCulture(Action hook) : base("en-US") => this.hook = hook;

            public override object GetFormat(Type formatType)
            {
                if (formatType == typeof(NumberFormatInfo))
                    this.hook();
                return base.GetFormat(formatType);
            }
        }

        // The quiz (a stand-in dialog of this process) is found as the game's; the first number the
        // tick formats (its log line about the new dialog) then attaches the helper to the process that
        // owns the foreground window - so the game is in the foreground while its quiz is open, and the
        // marks are asked for in front of the buttons.
        [Fact]
        public void QuizHelper_Tick_WithTheGameInTheForeground_PlacesTheMarks() => StaThread.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(new SilentContext());
            ProcessMemory memory = SelfAttached();
            IntPtr dialog = CreateWindowEx(WsExToolWindow | WsExNoActivate, "#32770", "Quiz", WsPopup, Far, Far, 400, 300, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Assert.NotEqual(IntPtr.Zero, dialog);
            int[] ids = { FirstAnswerId, FirstAnswerId + 1, FirstAnswerId + 2, QuestionId };
            for (int i = 0; i < ids.Length; i++)
                Assert.NotEqual(IntPtr.Zero, CreateWindowEx(0, "STATIC", "Antwort", WsChild | WsVisible, 10, 20 + 40 * i, 200, 30, dialog, new IntPtr(ids[i]), IntPtr.Zero, IntPtr.Zero));
            ShowWindow(dialog, SwShowNoActivate);
            IntPtr dialogObject = Marshal.AllocHGlobal(0x1000);
            CultureInfo culture = Thread.CurrentThread.CurrentCulture;
            QuizHelper helper = new QuizHelper(memory);
            try
            {
                Marshal.Copy(new byte[0x1000], 0, dialogObject, 0x1000);
                Marshal.WriteInt32(dialogObject + 0x1C, dialog.ToInt32());
                Marshal.WriteInt32(dialogObject + 0x41C, GetDlgItem(dialog, FirstAnswerId).ToInt32());
                Marshal.WriteInt32(dialogObject + 0x4E8, GetDlgItem(dialog, FirstAnswerId + 1).ToInt32());
                Marshal.WriteInt16(dialogObject + (int)QuizHelper.QuestionOffset, 3);
                Marshal.WriteInt16(dialogObject + (int)QuizHelper.RightAnswerOffset, 2);
                int foreground = 0;
                Thread.CurrentThread.CurrentCulture = new HookCulture(() =>
                {
                    GetWindowThreadProcessId(GetForegroundWindow(), out foreground);
                    if (foreground != 0)
                        memory.mProc.Process = Process.GetProcessById(foreground);
                });

                helper.Tick();

                Thread.CurrentThread.CurrentCulture = culture;
                Assert.Equal(dialog, Get<IntPtr>(helper, "dialog"));
                Assert.Equal((uint)dialogObject.ToInt32(), Get<uint>(helper, "dialogObject"));
                GetWindowThreadProcessId(GetForegroundWindow(), out int after);
                // Only if a window was in the foreground and stayed there (no window: nothing is shown).
                if (foreground != 0 && after == foreground)
                {
                    Rectangle[] shown = Get<Rectangle[]>(helper, "shownButtons");
                    Assert.NotNull(shown);
                    Assert.Equal(2, Get<int>(helper, "shownRight"));
                    Assert.Equal(new Rectangle(Far + 10, Far + 20, 200, 30), shown[0]);
                }
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = culture;
                helper.Dispose();
                DestroyWindow(dialog);
                Marshal.FreeHGlobal(dialogObject);
            }
        });

        // A thread on a desktop of its own (not the one with the user's input) sees no foreground window.
        [Fact]
        public void QuizHelper_GameInForeground_WithoutAForegroundWindow_IsFalse()
        {
            const uint GenericAll = 0x10000000;
            IntPtr desktop = CreateDesktop("A2GTrainerTest" + Guid.NewGuid().ToString("N"), IntPtr.Zero, IntPtr.Zero, 0, GenericAll, IntPtr.Zero);
            Assert.NotEqual(IntPtr.Zero, desktop);
            try
            {
                QuizHelper helper = new QuizHelper(SelfAttached());
                bool switched = false;
                IntPtr seen = new IntPtr(-1);
                object inForeground = null;
                Exception failure = null;
                Thread thread = new Thread(() =>
                {
                    try
                    {
                        switched = SetThreadDesktop(desktop);
                        seen = GetForegroundWindow();
                        inForeground = Call(helper, "GameInForeground");
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                });
                thread.Start();
                thread.Join();

                Assert.Null(failure);
                Assert.True(switched);
                Assert.Equal(IntPtr.Zero, seen);
                Assert.Equal(false, inForeground);
            }
            finally
            {
                CloseDesktop(desktop);
            }
        }

        // ---- MechanicsView ----------------------------------------------------------------------

        // A weekly mode the view has no entry for (set by something else) shows as the game's limit.
        [Fact]
        public void MechanicsView_RefreshValues_WithAnUnknownWeeklyMode_ShowsTheGamesLimit() => StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            {
                AppointmentLocksTests.WriteOriginalCode(fake);
                CoachViewTests.SeedLeagueAndStock(fake);
                AppointmentLocks locks = new AppointmentLocks(fake.Memory);
                Trainer trainer = (Trainer)FormatterServices.GetUninitializedObject(typeof(Trainer));
                GC.SuppressFinalize(trainer);
                typeof(Trainer).GetField("memory", Private).SetValue(trainer, fake.Memory);
                using (MechanicsView view = new MechanicsView(fake.Memory, new ProcessController(trainer)))
                {
                    // Lifted through the view, then set to a mode it doesn't know.
                    ComboBox weekly = (ComboBox)typeof(MechanicsView).GetField("weeklyLimitInput", Private).GetValue(view);
                    weekly.SelectedIndex = 1;
                    Assert.Equal(1, locks.WeeklyMode);
                    byte[] jump = fake.ReadModuleBytes(Settings.WeeklyLimitFunctionOffset, 5);
                    uint stub = unchecked((uint)(fake.ModuleBase + Settings.WeeklyLimitFunctionOffset + 5 + BitConverter.ToInt32(jump, 1))) - AppointmentLocks.CodeOffset;
                    fake.Memory.WriteBytesAtAddress(stub + AppointmentLocks.ModeOffset, new byte[] { 7 });
                    Assert.Equal(7, locks.WeeklyMode);
                    view.SetTrainers(new List<KeyValuePair<int, Coach>> { new KeyValuePair<int, Coach>(0, new Coach { Firstname = "Erwin", Lastname = "Kramer" }) });

                    view.RefreshValues();

                    Assert.Equal(0, weekly.SelectedIndex);
                }
            }
        });
    }

    /// <summary>Tests that swap a static of the trainer for a moment; they run on their own, after all others.</summary>
    [CollectionDefinition("StaticSwapGapTests", DisableParallelization = true)]
    public class StaticSwapGapCollection
    {
    }

    /// <summary>
    /// Paths behind a static the tests change: OpenProcess under Mono (Wine), and the speech display's
    /// stub when its code would outgrow its room.
    /// </summary>
    [Collection("StaticSwapGapTests")]
    public class StaticSwapGapTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

        /// <summary>Runs in an application domain of its own, where the trainer's statics are fresh.</summary>
        public sealed class MonoAttach : MarshalByRefObject
        {
            /// <summary>Marks the runtime as Mono before OpenProcess is first compiled, then attaches to <paramref name="processId"/>.</summary>
            public int[] Attach(int processId)
            {
                FieldInfo isMono = typeof(ProcessMemory).GetField("IsMono", PrivateStatic);
                isMono.SetValue(null, true);
                ProcessMemory memory = new ProcessMemory();
                bool opened = memory.OpenProcess(processId);
                return new[] { (bool)isMono.GetValue(null) ? 1 : 0, opened ? 1 : 0, memory.mProc.Process?.Id ?? 0 };
            }
        }

        // Under Mono the process isn't asked whether it responds (Wine Mono always says it doesn't).
        // IsMono is static readonly: once OpenProcess is compiled its value is fixed in this domain,
        // so the check runs in a new domain that sets it before OpenProcess is first used.
        [Fact]
        public void OpenProcess_UnderMono_SkipsTheRespondingCheck()
        {
            AppDomain domain = AppDomain.CreateDomain("A2GTrainerMonoTest", null, AppDomain.CurrentDomain.SetupInformation);
            try
            {
                MonoAttach attach = (MonoAttach)domain.CreateInstanceAndUnwrap(typeof(MonoAttach).Assembly.FullName, typeof(MonoAttach).FullName);
                int processId = Process.GetCurrentProcess().Id;

                Assert.Equal(new[] { 1, 1, processId }, attach.Attach(processId));
            }
            finally
            {
                AppDomain.Unload(domain);
            }
            Assert.False(ProcessMemory.IsMono);
        }

        // With many more dialog fields to push the list code would run into the text buffer.
        [Fact]
        public void SpeechEffectsDisplay_BuildStub_WithCodeTooLongForItsRoom_Throws()
        {
            FieldInfo arguments = typeof(SpeechEffectsDisplay).GetField("DialogArguments", PrivateStatic | BindingFlags.Public);
            object original = arguments.GetValue(null);
            arguments.SetValue(null, new uint[100]);
            try
            {
                InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => SpeechEffectsDisplay.BuildStub(0x10000000, 1, 2, 3));
                Assert.Contains("too long", thrown.Message);
            }
            finally
            {
                arguments.SetValue(null, original);
            }
            Assert.Equal(SpeechEffectsDisplay.StubSize, SpeechEffectsDisplay.BuildStub(0x10000000, 1, 2, 3).Length);
        }
    }
}
