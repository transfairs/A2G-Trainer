using A2G_Trainer_XP.Controller;
using System;
using System.Windows.Forms;

namespace A2G_Trainer_XP
{
    static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (sender, e) => Logger.Error("Unhandled UI thread exception", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => Logger.Error("Unhandled exception", e.ExceptionObject as Exception);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Trainer());
        }
    }
}
