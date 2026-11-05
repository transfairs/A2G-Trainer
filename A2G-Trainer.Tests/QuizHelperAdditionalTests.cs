using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using A2G_Trainer.Controller;
using A2G_Trainer.View;
using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// More tests for the quiz help (QuizHelper): finding a quiz dialog (a stand-in window of this
    /// process) and its object, placing and hiding the marks on the view's thread, and the search
    /// skipping memory it must not or can't read.
    /// </summary>
    public class QuizHelperAdditionalTests
    {
        private const int FirstAnswerId = 0x1182;
        private const int QuestionId = 0x1185;
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsChild = 0x40000000;
        private const int WsVisible = 0x10000000;
        private const int WsExToolWindow = 0x80;
        private const int WsExNoActivate = 0x08000000;
        private const int SwHide = 0;
        private const int SwShowNoActivate = 4;
        private const int SwShowMinNoActive = 7;
        // Far off every screen, so nothing the tests show is seen.
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
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
        [DllImport("kernel32.dll")]
        private static extern bool VirtualProtect(IntPtr address, UIntPtr size, uint protect, out uint old);
        [DllImport("kernel32.dll")]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic;

        private static T Get<T>(QuizHelper helper, string field) => (T)typeof(QuizHelper).GetField(field, Private).GetValue(helper);
        private static void Set(QuizHelper helper, string field, object value) => typeof(QuizHelper).GetField(field, Private).SetValue(helper, value);
        private static object Call(QuizHelper helper, string method, params object[] arguments)
        {
            try
            {
                return typeof(QuizHelper).GetMethod(method, Private).Invoke(helper, arguments);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
        }

        private static ProcessMemory SelfAttached()
        {
            ProcessMemory memory = new ProcessMemory();
            Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));
            return memory;
        }

        // A top-level window of the given class at (Far, Far), with a visible child for each of the given control IDs.
        private static IntPtr CreateDialog(string className, bool visible, params int[] ids)
        {
            IntPtr window = CreateWindowEx(WsExToolWindow | WsExNoActivate, className, "Quiz", WsPopup, Far, Far, 400, 300, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            Assert.NotEqual(IntPtr.Zero, window);
            for (int i = 0; i < ids.Length; i++)
            {
                IntPtr child = CreateWindowEx(0, "STATIC", "Antwort", WsChild | WsVisible, 10, 20 + 40 * i, 200, 30, window, new IntPtr(ids[i]), IntPtr.Zero, IntPtr.Zero);
                Assert.NotEqual(IntPtr.Zero, child);
            }
            if (visible)
                ShowWindow(window, SwShowNoActivate);
            return window;
        }

        private static readonly int[] AllIds = { FirstAnswerId, FirstAnswerId + 1, FirstAnswerId + 2, QuestionId };

        [Fact]
        public void Tick_FindsTheQuizDialogAndItsObject_AndForgetsThemOnceTheDialogIsGone()
        {
            StaThread.Run(() =>
            {
                ProcessMemory memory = SelfAttached();
                // Created without a view's thread: no marks are shown.
                using (QuizHelper helper = new QuizHelper(memory))
                {
                    // Windows that aren't the quiz: not a dialog, hidden, without the question, without an answer.
                    IntPtr[] others =
                    {
                        CreateDialog("STATIC", true),
                        CreateDialog("#32770", false, AllIds),
                        CreateDialog("#32770", true, FirstAnswerId, FirstAnswerId + 1, FirstAnswerId + 2),
                        CreateDialog("#32770", true, FirstAnswerId, FirstAnswerId + 2, QuestionId),
                    };
                    helper.Tick();
                    Assert.Equal(IntPtr.Zero, Get<IntPtr>(helper, "dialog"));
                    foreach (IntPtr window in others)
                        DestroyWindow(window);

                    IntPtr dialog = CreateDialog("#32770", true, AllIds);
                    IntPtr dialogObject = Marshal.AllocHGlobal(0x1000);
                    try
                    {
                        Marshal.Copy(new byte[0x1000], 0, dialogObject, 0x1000);
                        // The dialog's object isn't in memory yet: found, but without its object.
                        helper.Tick();
                        Assert.Equal(dialog, Get<IntPtr>(helper, "dialog"));
                        Assert.Equal(0u, Get<uint>(helper, "dialogObject"));

                        Marshal.WriteInt32(dialogObject + 0x1C, dialog.ToInt32());
                        Marshal.WriteInt32(dialogObject + 0x41C, GetDlgItem(dialog, FirstAnswerId).ToInt32());
                        Marshal.WriteInt32(dialogObject + 0x4E8, GetDlgItem(dialog, FirstAnswerId + 1).ToInt32());
                        Marshal.WriteInt16(dialogObject + (int)QuizHelper.QuestionOffset, 3);
                        Marshal.WriteInt16(dialogObject + (int)QuizHelper.RightAnswerOffset, 1);
                        // A dialog it hasn't seen yet is searched for again.
                        Set(helper, "dialog", IntPtr.Zero);
                        helper.Tick();
                        Assert.Equal(dialog, Get<IntPtr>(helper, "dialog"));
                        Assert.Equal((uint)dialogObject.ToInt32(), Get<uint>(helper, "dialogObject"));
                        Assert.Equal(1, QuizHelper.RightAnswer(memory, Get<uint>(helper, "dialogObject")));

                        // Minimised, and back: the same dialog keeps its object.
                        ShowWindow(dialog, SwShowMinNoActive);
                        helper.Tick();
                        ShowWindow(dialog, SwShowNoActivate);
                        helper.Tick();
                        Assert.Equal((uint)dialogObject.ToInt32(), Get<uint>(helper, "dialogObject"));
                        Assert.Null(Get<Rectangle[]>(helper, "shownButtons"));

                        DestroyWindow(dialog);
                        helper.Tick();
                        Assert.Equal(IntPtr.Zero, Get<IntPtr>(helper, "dialog"));
                        Assert.Equal(0u, Get<uint>(helper, "dialogObject"));
                    }
                    finally
                    {
                        DestroyWindow(dialog);
                        Marshal.FreeHGlobal(dialogObject);
                    }
                }
            });
        }

        [Fact]
        public void Tick_WithoutAGame_WhileATickRuns_AndOnAFailure_KeepsGoing()
        {
            StaThread.Run(() =>
            {
                ProcessMemory memory = new ProcessMemory();
                using (QuizHelper helper = new QuizHelper(memory))
                {
                    // Without a game: no dialog.
                    Set(helper, "dialog", new IntPtr(0x1234));
                    Set(helper, "dialogObject", 0x5678u);
                    helper.Tick();
                    Assert.Equal(IntPtr.Zero, Get<IntPtr>(helper, "dialog"));
                    Assert.Equal(0u, Get<uint>(helper, "dialogObject"));

                    // While a tick still runs the next one does nothing.
                    Set(helper, "dialog", new IntPtr(0x1234));
                    Set(helper, "ticking", 1);
                    helper.Tick();
                    Assert.Equal(new IntPtr(0x1234), Get<IntPtr>(helper, "dialog"));
                    Assert.Equal(1, Get<int>(helper, "ticking"));

                    // A process object that isn't one: the tick fails, is logged and the next one may run.
                    Set(helper, "ticking", 0);
                    memory.mProc.Process = new Process();
                    helper.Tick();
                    Assert.Equal(new IntPtr(0x1234), Get<IntPtr>(helper, "dialog"));
                    Assert.Equal(0, Get<int>(helper, "ticking"));
                }
            });
        }

        // Runs the work posted to the view's thread so far: posts a last item and pumps until it has run.
        private static void Flush()
        {
            bool done = false;
            SynchronizationContext.Current.Post(_ => done = true, null);
            for (int i = 0; i < 1000 && !done; i++)
            {
                Application.DoEvents();
                if (!done)
                    Thread.Sleep(1);
            }
            Assert.True(done);
        }

        private static Rectangle[] Buttons(int top) => new[]
        {
            new Rectangle(Far + 50, Far + top, 300, 30),
            new Rectangle(Far + 50, Far + top + 40, 300, 30),
            new Rectangle(Far + 50, Far + top + 80, 300, 30),
            new Rectangle(Far + 40, Far, 320, 30),
        };

        [Fact]
        public void Show_PlacesTheMarksOnTheViewsThread_OnlyOnAChange_AndHidesAndDisposesThem()
        {
            StaThread.Run(() =>
            {
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                QuizHelper helper = new QuizHelper(new ProcessMemory());
                QuizMark[] marks = Get<QuizMark[]>(helper, "marks");
                try
                {
                    // Hiding before any mark exists does nothing.
                    Call(helper, "Place", null, 0);
                    Assert.All(marks, Assert.Null);

                    // Switched on, with every timer tick skipped as if one still ran.
                    Set(helper, "ticking", 1);
                    helper.Enabled = true;

                    Rectangle[] buttons = Buttons(40);
                    Call(helper, "Show", buttons, 1);
                    Flush();
                    Assert.All(marks, m => Assert.True(m.Visible));
                    Assert.Equal(new[] { false, true, false }, marks.Select(m => m.IsRight));
                    Assert.Equal(Enumerable.Range(0, 3).Select(i => QuizHelper.MarkBounds(buttons[i], buttons[3].Left)), marks.Select(m => m.Bounds));

                    // The same again: nothing is posted, so a mark hidden meanwhile stays hidden.
                    marks[0].Hide();
                    Call(helper, "Show", Buttons(40), 1);
                    Flush();
                    Assert.False(marks[0].Visible);

                    // Another right answer: placed again, the hidden mark shown again.
                    Call(helper, "Show", Buttons(40), 2);
                    Flush();
                    Assert.Equal(new[] { false, false, true }, marks.Select(m => m.IsRight));
                    Assert.True(marks[0].Visible);

                    // Moved buttons, with a mark closed meanwhile: a new mark in its place.
                    QuizMark closed = marks[1];
                    closed.Dispose();
                    Rectangle[] moved = Buttons(100);
                    Call(helper, "Show", moved, 2);
                    Flush();
                    Assert.NotSame(closed, marks[1]);
                    Assert.True(marks[1].Visible);
                    Assert.Equal(QuizHelper.MarkBounds(moved[1], moved[3].Left), marks[1].Bounds);

                    // No buttons: all hidden; and again nothing is posted.
                    Call(helper, "Show", null, 0);
                    Flush();
                    Assert.All(marks, m => Assert.False(m.Visible));
                    marks[0].Show();
                    Call(helper, "Show", null, 0);
                    Flush();
                    Assert.True(marks[0].Visible);
                    marks[0].Hide();

                    // Switched off: a placement still queued only hides.
                    Call(helper, "Show", moved, 0);
                    helper.Enabled = false;
                    Flush();
                    Assert.All(marks, m => Assert.False(m.Visible));
                    Call(helper, "Place", moved, 0);
                    Assert.All(marks, m => Assert.False(m.Visible));
                }
                finally
                {
                    helper.Dispose();
                }
                Flush();
                Assert.All(marks, m => Assert.True(m.IsDisposed));
            });
        }

        [Fact]
        public void RightAnswer_OfAnObjectThatCantBeRead_IsUnknown()
        {
            // Below the lowest address ProcessMemory reads.
            Assert.Null(QuizHelper.RightAnswer(SelfAttached(), 0x2000));
        }

        [Fact]
        public void ButtonBounds_AreTheAnswersAndTheQuestionOnScreen_OrNullIfOneIsMissingOrHidden()
        {
            StaThread.Run(() =>
            {
                IntPtr dialog = CreateDialog("#32770", true, AllIds);
                try
                {
                    Rectangle[] bounds = (Rectangle[])Call(null, "ButtonBounds", dialog);

                    Assert.Equal(Enumerable.Range(0, 4).Select(i => new Rectangle(Far + 10, Far + 20 + 40 * i, 200, 30)), bounds);

                    ShowWindow(GetDlgItem(dialog, QuestionId), SwHide);
                    Assert.Null(Call(null, "ButtonBounds", dialog));
                    DestroyWindow(GetDlgItem(dialog, FirstAnswerId + 1));
                    Assert.Null(Call(null, "ButtonBounds", dialog));
                }
                finally
                {
                    DestroyWindow(dialog);
                }
            });
        }

        [Fact]
        public void GameInForeground_IsWhetherTheForegroundWindowBelongsToTheAttachedProcess()
        {
            ProcessMemory memory = new ProcessMemory();
            QuizHelper helper = new QuizHelper(memory);
            Assert.False((bool)Call(helper, "GameInForeground"));

            // Attached to the process that owns the foreground window (whichever that is right now).
            for (int attempt = 0; attempt < 10; attempt++)
            {
                GetWindowThreadProcessId(GetForegroundWindow(), out int owner);
                memory.mProc.Process = owner != 0 ? Process.GetProcessById(owner) : Process.GetCurrentProcess();
                bool inForeground = (bool)Call(helper, "GameInForeground");
                GetWindowThreadProcessId(GetForegroundWindow(), out int ownerAfter);
                if (owner != ownerAfter)
                    continue;
                Assert.Equal(owner != 0, inForeground);
                if (owner == 0)
                    continue;

                // Attached to another process than that.
                memory.mProc.Process = Process.GetProcessById(owner == Process.GetCurrentProcess().Id ? 4 : Process.GetCurrentProcess().Id);
                Assert.False((bool)Call(helper, "GameInForeground"));
                return;
            }
        }

        // A block of six pages: the object at offset 0x200 of the last one, and around it memory the search must skip.
        [Fact]
        public void FindDialogObject_SkipsMemoryItMustNotOrCantRead()
        {
            const uint Dialog = 0x7A5C3F11, First = 0x7A5C3F21, Second = 0x7A5C3F31;
            const int Page = 0x1000;
            const uint MemCommit = 0x1000, MemReserve = 0x2000, MemRelease = 0x8000;
            const uint PageNoAccess = 0x01, PageReadOnly = 0x02, PageReadWrite = 0x04, PageGuard = 0x100;
            ProcessMemory memory = SelfAttached();
            IntPtr block = VirtualAlloc(IntPtr.Zero, (UIntPtr)(6 * Page), MemReserve, PageNoAccess);
            Assert.NotEqual(IntPtr.Zero, block);
            try
            {
                void Commit(int page) => Assert.NotEqual(IntPtr.Zero, VirtualAlloc(block + page * Page, (UIntPtr)Page, MemCommit, PageReadWrite));
                void Plant(int page, int at)
                {
                    Marshal.WriteInt32(block + page * Page + at + 0x1C, unchecked((int)Dialog));
                    Marshal.WriteInt32(block + page * Page + at + 0x41C, unchecked((int)First));
                    Marshal.WriteInt32(block + page * Page + at + 0x4E8, unchecked((int)Second));
                }
                // Page 0 only reserved. Page 1 read-only and page 2 a guard page, each with an object.
                Commit(1);
                Plant(1, 0x100);
                Assert.True(VirtualProtect(block + Page, (UIntPtr)Page, PageReadOnly, out _));
                Commit(2);
                Plant(2, 0x100);
                Assert.True(VirtualProtect(block + 2 * Page, (UIntPtr)Page, PageReadWrite | PageGuard, out _));
                // Page 3: the dialog's handle at its very start (no room for an object before it) and in
                // its last dword (the buttons would be on page 4, which is only reserved).
                Commit(3);
                Marshal.WriteInt32(block + 3 * Page, unchecked((int)Dialog));
                Marshal.WriteInt32(block + 4 * Page - 4, unchecked((int)Dialog));
                Commit(5);
                Plant(5, 0x200);
                uint from = (uint)block.ToInt32(), to = from + 6 * Page;

                Assert.Equal(from + 5 * Page + 0x200, QuizHelper.FindDialogObject(memory, Dialog, First, Second, from, to));

                // A handle that may look at the memory but not read it.
                IntPtr full = memory.mProc.Handle;
                IntPtr queryOnly = OpenProcess(0x0400, false, Process.GetCurrentProcess().Id);
                Assert.NotEqual(IntPtr.Zero, queryOnly);
                try
                {
                    memory.mProc.Handle = queryOnly;
                    Assert.Null(QuizHelper.FindDialogObject(memory, Dialog, First, Second, from, to));
                }
                finally
                {
                    memory.mProc.Handle = full;
                    CloseHandle(queryOnly);
                }

                // Past the highest address a process can have: the search ends at once.
                Assert.Null(QuizHelper.FindDialogObject(memory, Dialog, First, Second, 0xFFFF0000, 0xFFFFFFFF));
            }
            finally
            {
                VirtualFree(block, UIntPtr.Zero, MemRelease);
            }
        }
    }
}
