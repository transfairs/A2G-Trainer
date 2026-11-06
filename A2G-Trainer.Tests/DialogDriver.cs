using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Answers the next common dialog (open/save file, folder browser) the test's own process shows,
    /// from a background thread while the test's thread sits in the dialog's modal loop: cancels it,
    /// or enters a file name / selects a folder and confirms it. Dispose waits until it has answered.
    /// </summary>
    internal sealed class DialogDriver : IDisposable
    {
        private const int WmClose = 0x0010;
        private const int WmSetText = 0x000C;
        private const int WmGetText = 0x000D;
        private const int TvmGetCount = 0x1105;
        private const int BmClick = 0x00F5;
        private const int BffmSetSelectionW = 0x0400 + 103;
        private const int IdOk = 1;
        private const int AddressBar = 0xA205;

        private readonly Thread thread;
        private bool answered;

        private DialogDriver(string fileName, string folder)
        {
            uint owner = GetCurrentThreadId();
            this.thread = new Thread(() => this.answered = Answer(owner, fileName, folder)) { IsBackground = true };
            this.thread.Start();
        }

        /// <summary>Cancels the next dialog.</summary>
        public static DialogDriver Cancel() => new DialogDriver(null, null);

        /// <summary>Enters this file name in the next file dialog and confirms it.</summary>
        public static DialogDriver ConfirmFile(string fileName) => new DialogDriver(fileName, null);

        /// <summary>Selects this folder in the next folder browser and confirms it.</summary>
        public static DialogDriver ConfirmFolder(string folder) => new DialogDriver(null, folder);

        public void Dispose()
        {
            this.thread.Join(TimeSpan.FromSeconds(30));
            if (!this.answered)
                throw new TimeoutException("No dialog showed up to answer, or it stayed open after the answer.");
        }

        private static bool Answer(uint owner, string fileName, string folder)
        {
            Stopwatch watch = Stopwatch.StartNew();
            while (watch.Elapsed < TimeSpan.FromSeconds(15))
            {
                IntPtr dialog = FindDialog(owner);
                if (dialog != IntPtr.Zero)
                {
                    // Let the dialog finish setting itself up before it is answered.
                    Thread.Sleep(300);
                    if (fileName == null && folder == null)
                    {
                        PostMessage(dialog, WmClose, IntPtr.Zero, IntPtr.Zero);
                        return true;
                    }
                    // Under load the dialog may still be filling itself: enter the answer until it shows.
                    Stopwatch entering = Stopwatch.StartNew();
                    while (!(fileName != null ? EnterFileName(dialog, fileName) : SelectFolder(dialog, folder)))
                    {
                        if (entering.Elapsed > TimeSpan.FromSeconds(10))
                            break;
                        Thread.Sleep(100);
                    }
                    Thread.Sleep(200);
                    PostMessage(GetDlgItem(dialog, IdOk), BmClick, IntPtr.Zero, IntPtr.Zero);

                    // A dialog still open after that (e.g. an overwrite prompt) would block the test for good:
                    // close whatever is left and report the answer as failed.
                    Stopwatch closing = Stopwatch.StartNew();
                    while (closing.Elapsed < TimeSpan.FromSeconds(5))
                    {
                        if (FindDialog(owner) == IntPtr.Zero)
                            return true;
                        Thread.Sleep(50);
                    }
                    for (IntPtr left = FindDialog(owner); left != IntPtr.Zero; left = FindDialog(owner))
                    {
                        PostMessage(left, WmClose, IntPtr.Zero, IntPtr.Zero);
                        Thread.Sleep(200);
                    }
                    return false;
                }
                Thread.Sleep(50);
            }
            return false;
        }

        // The file name box: the edit of a combo box, which the Explorer-style dialogs nest a few windows
        // deep (so GetDlgItem on the dialog doesn't reach it). The address bar's edit (id 0xA205) is one too.
        private static IntPtr FileNameEdit(IntPtr dialog)
        {
            IntPtr edit = IntPtr.Zero;
            EnumChildWindows(dialog, (window, parameter) =>
            {
                if (ClassOf(window) != "Edit" || ClassOf(GetParent(window)) != "ComboBox" || GetDlgCtrlID(window) == AddressBar)
                    return true;
                edit = window;
                return false;
            }, IntPtr.Zero);
            return edit;
        }

        // Writes the file name into the dialog's box; true once the box holds it.
        private static bool EnterFileName(IntPtr dialog, string fileName)
        {
            IntPtr edit = FileNameEdit(dialog);
            if (edit == IntPtr.Zero)
                return false;
            SendMessage(edit, WmSetText, IntPtr.Zero, fileName);
            StringBuilder text = new StringBuilder(fileName.Length + 2);
            SendMessage(edit, WmGetText, new IntPtr(text.Capacity), text);
            return text.ToString() == fileName;
        }

        // Selects the folder once the folder browser's tree is filled (before that the selection gets lost).
        private static bool SelectFolder(IntPtr dialog, string folder)
        {
            IntPtr tree = IntPtr.Zero;
            EnumChildWindows(dialog, (window, parameter) =>
            {
                if (ClassOf(window) != "SysTreeView32")
                    return true;
                tree = window;
                return false;
            }, IntPtr.Zero);
            if (tree == IntPtr.Zero || SendMessage(tree, TvmGetCount, IntPtr.Zero, IntPtr.Zero) == IntPtr.Zero)
                return false;
            SendMessage(dialog, BffmSetSelectionW, new IntPtr(1), folder);
            return true;
        }

        private static string ClassOf(IntPtr window)
        {
            StringBuilder className = new StringBuilder(64);
            GetClassName(window, className, className.Capacity);
            return className.ToString();
        }

        // A visible dialog-class window of the test's thread (parallel tests may show message boxes of their own).
        private static IntPtr FindDialog(uint ownerThread)
        {
            IntPtr found = IntPtr.Zero;
            EnumWindows((window, parameter) =>
            {
                if (GetWindowThreadProcessId(window, out _) != ownerThread || !IsWindowVisible(window) || ClassOf(window) != "#32770")
                    return true;
                found = window;
                return false;
            }, IntPtr.Zero);
            return found;
        }

        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out int processId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, StringBuilder className, int maxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDlgItem(IntPtr dialog, int id);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern IntPtr GetParent(IntPtr window);

        [DllImport("user32.dll")]
        private static extern int GetDlgCtrlID(IntPtr window);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, string lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, StringBuilder lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    }
}
