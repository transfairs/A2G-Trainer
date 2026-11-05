using A2G_Trainer.View;
using AnstossTrainer.Shared;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Puts a green tick in front of the right answer of the Managerfortbildung quiz and a red cross in
    /// front of the two wrong ones while the game shows it; all three stay clickable. The quiz is a dialog (resource 0x1F9, answer buttons 0x1182..0x1184, question 0x1185) whose object lives on
    /// the game's stack (constructor FUN_004872d0); it keeps the position of the right button at +0xF5A
    /// and the question number at +0xF58 (FUN_00487dc0, FUN_00488000). The object is found once per
    /// dialog by its window handles, the same in every build. The marks are windows of the trainer
    /// (<see cref="QuizMark"/>), shown only while the game is in the foreground.
    /// </summary>
    internal sealed class QuizHelper : IDisposable
    {
        private const int FirstAnswerId = 0x1182;
        private const int QuestionId = 0x1185;
        // Within the dialog object: its window handle (+0x1C, MFC) and those of the first two answer buttons.
        private const uint WindowHandleOffset = 0x1C;
        private const uint FirstButtonHandleOffset = 0x41C;
        private const uint SecondButtonHandleOffset = 0x4E8;
        internal const uint QuestionOffset = 0xF58;
        internal const uint RightAnswerOffset = 0xF5A;
        private const int TickMilliseconds = 250;
        private const int ScanChunk = 0x10000;
        private readonly ProcessMemory memory;
        // The thread the marks live on (the view's); without one (tests) no mark is shown.
        private readonly SynchronizationContext ui;
        private readonly QuizMark[] marks = new QuizMark[3];
        private Timer timer;
        private int ticking;
        private IntPtr dialog;
        private uint dialogObject;
        // What the last tick asked the marks to show: the buttons' screen bounds and the right one's position.
        private Rectangle[] shownButtons;
        private int shownRight;

        /// <summary>
        /// Creates the helper for the attached game; it does nothing until <see cref="Enabled"/>. Create it
        /// on the view's thread, which then shows the marks.
        /// </summary>
        internal QuizHelper(ProcessMemory memory)
        {
            this.memory = memory;
            this.ui = SynchronizationContext.Current;
        }

        /// <summary>Whether the answers are marked while the quiz is open.</summary>
        internal bool Enabled
        {
            get => this.timer != null;
            set
            {
                if (value == this.Enabled)
                    return;
                if (value)
                    this.timer = new Timer(_ => this.Tick(), null, 0, TickMilliseconds);
                else
                {
                    this.timer.Dispose();
                    this.timer = null;
                    this.dialog = IntPtr.Zero;
                    this.dialogObject = 0;
                    this.Show(null, 0);
                }
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            this.Enabled = false;
            this.ui?.Post(_ =>
            {
                foreach (QuizMark mark in this.marks)
                    mark?.Dispose();
            }, null);
        }

        // One tick: find the open quiz and its object, and mark the answers. While a tick still runs (the
        // first search for the object can take a while) the next ones skip.
        internal void Tick()
        {
            if (Interlocked.Exchange(ref this.ticking, 1) == 1)
                return;
            try
            {
                IntPtr found = this.FindQuizDialog();
                if (found == IntPtr.Zero)
                {
                    this.dialog = IntPtr.Zero;
                    this.dialogObject = 0;
                    this.Show(null, 0);
                    return;
                }
                if (found != this.dialog)
                {
                    this.dialog = found;
                    this.dialogObject = FindDialogObject(this.memory, (uint)found.ToInt64(),
                        (uint)GetDlgItem(found, FirstAnswerId).ToInt64(), (uint)GetDlgItem(found, FirstAnswerId + 1).ToInt64()) ?? 0;
                    Logger.Debug($"QuizHelper: quiz dialog 0x{found.ToInt64():X}, object 0x{this.dialogObject:X}.");
                }
                int? right = RightAnswer(this.memory, this.dialogObject);
                this.Show(right.HasValue && !IsIconic(found) && this.GameInForeground() ? ButtonBounds(found) : null, right ?? 0);
            }
            catch (Exception ex)
            {
                Logger.Warn("QuizHelper", ex);
            }
            finally
            {
                Interlocked.Exchange(ref this.ticking, 0);
            }
        }

        /// <summary>
        /// Where the mark of an answer button with the screen bounds <paramref name="button"/> goes: a square
        /// flush with the left edge <paramref name="left"/> of the question box, centred vertically on the button.
        /// </summary>
        internal static Rectangle MarkBounds(Rectangle button, int left)
        {
            int size = Math.Max(14, Math.Min(28, button.Height * 3 / 4));
            return new Rectangle(left, button.Top + (button.Height - size) / 2, size, size);
        }

        // The screen bounds of the three answer buttons and, last, of the question box, or null if one of them can't be read.
        private static Rectangle[] ButtonBounds(IntPtr dialog)
        {
            Rectangle[] bounds = new Rectangle[4];
            for (int position = 0; position < 4; position++)
            {
                IntPtr control = GetDlgItem(dialog, position < 3 ? FirstAnswerId + position : QuestionId);
                if (control == IntPtr.Zero || !IsWindowVisible(control) || !GetWindowRect(control, out NativeRect rect))
                    return null;
                bounds[position] = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
            }
            return bounds;
        }

        // Whether the foreground window belongs to the game: the marks are topmost and would otherwise float over other programs.
        private bool GameInForeground()
        {
            GetWindowThreadProcessId(GetForegroundWindow(), out int owner);
            return owner != 0 && owner == (this.memory.mProc.Process?.Id ?? 0);
        }

        // Asks the view's thread to put the marks in front of the buttons, or to hide them (null); only on a change.
        private void Show(Rectangle[] buttons, int right)
        {
            bool unchanged = buttons == null ? this.shownButtons == null
                : this.shownButtons != null && right == this.shownRight
                    && ((IStructuralEquatable)buttons).Equals(this.shownButtons, EqualityComparer<Rectangle>.Default);
            if (this.ui == null || unchanged)
                return;
            this.shownButtons = buttons;
            this.shownRight = right;
            this.ui.Post(_ => this.Place(buttons, right), null);
        }

        // On the view's thread: places the marks, or hides them without buttons or once the help is switched off.
        private void Place(Rectangle[] buttons, int right)
        {
            for (int position = 0; position < 3; position++)
            {
                if (buttons == null || !this.Enabled)
                {
                    this.marks[position]?.Hide();
                    continue;
                }
                if (this.marks[position] == null || this.marks[position].IsDisposed)
                    this.marks[position] = new QuizMark(position == right);
                QuizMark mark = this.marks[position];
                mark.IsRight = position == right;
                mark.Bounds = MarkBounds(buttons[position], buttons[3].Left);
                if (!mark.Visible)
                    mark.Show();
            }
        }

        /// <summary>The position (0..2) of the right answer button, or null if the object can't be read.</summary>
        internal static int? RightAnswer(ProcessMemory memory, uint dialogObject)
        {
            if (dialogObject == 0)
                return null;
            byte[] values = memory.ReadBytesAtAddress(dialogObject + QuestionOffset, 4);
            if (values == null)
                return null;
            short question = BitConverter.ToInt16(values, 0);
            short right = BitConverter.ToInt16(values, 2);
            return question >= 1 && question <= 6 && right >= 0 && right <= 2 ? right : (int?)null;
        }

        // The game's quiz dialog: a dialog of the game process with the three answer buttons and the question.
        private IntPtr FindQuizDialog()
        {
            int processId = this.memory.mProc.Process?.Id ?? 0;
            if (processId == 0)
                return IntPtr.Zero;
            IntPtr result = IntPtr.Zero;
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out int owner);
                if (owner != processId || !IsWindowVisible(window))
                    return true;
                StringBuilder className = new StringBuilder(16);
                GetClassName(window, className, className.Capacity);
                if (className.ToString() != "#32770" || GetDlgItem(window, QuestionId) == IntPtr.Zero)
                    return true;
                for (int position = 0; position < 3; position++)
                {
                    if (GetDlgItem(window, FirstAnswerId + position) == IntPtr.Zero)
                        return true;
                }
                result = window;
                return false;
            }, IntPtr.Zero);
            return result;
        }

        /// <summary>
        /// Finds the dialog object in the process's private writable memory: a dword holding the dialog's
        /// window handle at +0x1C, with the first two answer buttons' handles at +0x41C/+0x4E8. Null if none.
        /// <paramref name="from"/>/<paramref name="to"/> limit the search; by default it covers the whole 32-bit user space.
        /// </summary>
        internal static uint? FindDialogObject(ProcessMemory memory, uint dialogHandle, uint firstButton, uint secondButton, uint from = 0x10000, uint to = 0xFFFF0000)
        {
            IntPtr process = memory.mProc.Handle;
            long address = from;
            while (address < to)
            {
                // Addresses above 2 GB (large-address-aware processes) are negative as a 32-bit IntPtr.
                if (VirtualQueryEx(process, new IntPtr(unchecked((int)(uint)address)), out MemoryBasicInformation info, (uint)Marshal.SizeOf(typeof(MemoryBasicInformation))) == 0)
                    break;
                long start = Unsigned(info.BaseAddress);
                long size = Unsigned(info.RegionSize);
                if (size <= 0)
                    break;
                if (info.State == MemCommit && info.Type == MemPrivate && (info.Protect & PageReadWrite) != 0 && (info.Protect & PageGuard) == 0)
                {
                    uint? found = ScanRegion(memory, (uint)start, size, dialogHandle, firstButton, secondButton);
                    if (found.HasValue)
                        return found;
                }
                address = start + size;
            }
            return null;
        }

        private static long Unsigned(IntPtr value) => IntPtr.Size == 4 ? (uint)value.ToInt32() : value.ToInt64();

        // Reads the region in chunks (each overlapping the next by the object's size) and checks every dword.
        private static uint? ScanRegion(ProcessMemory memory, uint start, long size, uint dialogHandle, uint firstButton, uint secondButton)
        {
            const int overlap = (int)SecondButtonHandleOffset;
            for (long at = 0; at < size; at += ScanChunk)
            {
                int length = (int)Math.Min(ScanChunk + overlap, size - at);
                byte[] chunk = memory.ReadBytesAtAddress(start + (uint)at, length);
                if (chunk == null)
                    continue;
                for (int i = 0; i + 4 <= Math.Min(length, ScanChunk); i += 4)
                {
                    if (BitConverter.ToUInt32(chunk, i) != dialogHandle || i < WindowHandleOffset)
                        continue;
                    uint candidate = start + (uint)at + (uint)i - WindowHandleOffset;
                    byte[] buttons = memory.ReadBytesAtAddress(candidate + FirstButtonHandleOffset, (int)(SecondButtonHandleOffset - FirstButtonHandleOffset) + 4);
                    if (buttons != null && BitConverter.ToUInt32(buttons, 0) == firstButton
                        && BitConverter.ToUInt32(buttons, (int)(SecondButtonHandleOffset - FirstButtonHandleOffset)) == secondButton)
                        return candidate;
                }
            }
            return null;
        }

        #region Win32
        private const uint MemCommit = 0x1000;
        private const uint MemPrivate = 0x20000;
        private const uint PageReadWrite = 0x04;
        private const uint PageGuard = 0x100;

        [StructLayout(LayoutKind.Sequential)]
        private struct MemoryBasicInformation
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left, Top, Right, Bottom;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("kernel32.dll")]
        private static extern int VirtualQueryEx(IntPtr process, IntPtr address, out MemoryBasicInformation buffer, uint length);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);
        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr dialog, int id);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        #endregion
    }
}
