using System;
using System.IO;

namespace A2G_Trainer_XP.Controller
{
    internal enum LogLevel
    {
        Debug,
        Info,
        Warn,
        Error
    }

    internal static class Logger
    {
        private const long MaxLogFileBytes = 2 * 1024 * 1024;

        private static readonly object SyncRoot = new object();
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "A2G-Trainer-XP");
        private static readonly string LogFilePath = Path.Combine(LogDirectory, "trainer.log");

        // Gated behind Settings.IsDebug so a Release build's log stays readable; Info/Warn/Error
        // always write, since they're the ones a bug report would actually need.
        internal static void Debug(string message)
        {
            if (Settings.IsDebug)
                Write(LogLevel.Debug, message, null);
        }

        internal static void Info(string message) => Write(LogLevel.Info, message, null);

        internal static void Warn(string message) => Write(LogLevel.Warn, message, null);
        internal static void Warn(string message, Exception ex) => Write(LogLevel.Warn, message, ex);

        internal static void Error(string message) => Write(LogLevel.Error, message, null);
        internal static void Error(string message, Exception ex) => Write(LogLevel.Error, message, ex);

        private static void Write(LogLevel level, string message, Exception ex)
        {
            try
            {
                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
                if (ex != null)
                    line += Environment.NewLine + ex;

                lock (SyncRoot)
                {
                    Directory.CreateDirectory(LogDirectory);
                    RotateIfTooLarge();
                    File.AppendAllText(LogFilePath, line + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never be the reason the trainer crashes (e.g. an unwritable/locked
                // log directory) - drop the entry rather than throw.
            }
        }

        // Keeps disk usage bounded (current file + one backup) without needing full rolling
        // multi-file rotation for what is a low-volume, single-user log.
        private static void RotateIfTooLarge()
        {
            FileInfo info = new FileInfo(LogFilePath);
            if (!info.Exists || info.Length < MaxLogFileBytes)
                return;

            string backupPath = LogFilePath + ".old";
            if (File.Exists(backupPath))
                File.Delete(backupPath);
            File.Move(LogFilePath, backupPath);
        }
    }
}
