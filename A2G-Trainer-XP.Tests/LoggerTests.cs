using System;
using System.IO;
using System.Reflection;
using A2G_Trainer_XP.Controller;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for the file-based Logger. It writes to a fixed real path under %LocalAppData%, so
    /// these tests read that file back rather than injecting a fake one; the rotation/catch tests
    /// save and restore any pre-existing log directory around themselves.
    /// </summary>
    public class LoggerTests
    {
        private static string LogDirectory => (string)typeof(Logger).GetField("LogDirectory", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        private static string LogFilePath => (string)typeof(Logger).GetField("LogFilePath", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        private static void RotateIfTooLarge() => typeof(Logger).GetMethod("RotateIfTooLarge", BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);

        private static string ReadLogTail()
        {
            using (FileStream stream = new FileStream(LogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (StreamReader reader = new StreamReader(stream))
                return reader.ReadToEnd();
        }

        [Fact]
        public void Warn_WritesMessageToLogFile()
        {
            string marker = $"WarnMarker-{Guid.NewGuid()}";

            Logger.Warn(marker);

            Assert.Contains(marker, ReadLogTail());
        }

        // Settings.IsDebug is false by default (release behavior) but backed by a plain static
        // field rather than a const specifically so tests can flip it - restored in finally, and
        // safe to do because this assembly runs its tests sequentially (see AssemblyInfo.cs).
        private static void WithDebugLoggingEnabled(Action action)
        {
            FieldInfo debugField = typeof(Settings).GetField("debug", BindingFlags.NonPublic | BindingFlags.Static);
            bool original = (bool)debugField.GetValue(null);
            debugField.SetValue(null, true);
            try
            {
                action();
            }
            finally
            {
                debugField.SetValue(null, original);
            }
        }

        [Fact]
        public void Info_WhenDebugLoggingDisabled_DoesNotWriteToTheLogFile()
        {
            string marker = $"InfoDisabledMarker-{Guid.NewGuid()}";

            Logger.Info(marker);

            Assert.DoesNotContain(marker, ReadLogTail());
        }

        [Fact]
        public void Debug_WhenDebugLoggingEnabled_WritesMessageToLogFile()
        {
            string marker = $"DebugMarker-{Guid.NewGuid()}";

            WithDebugLoggingEnabled(() => Logger.Debug(marker));

            Assert.Contains(marker, ReadLogTail());
        }

        [Fact]
        public void Info_WhenDebugLoggingEnabled_WritesMessageToLogFile()
        {
            string marker = $"InfoEnabledMarker-{Guid.NewGuid()}";

            WithDebugLoggingEnabled(() => Logger.Info(marker));

            Assert.Contains(marker, ReadLogTail());
        }

        [Fact]
        public void Warn_WithException_AppendsExceptionText()
        {
            string marker = $"WarnExMarker-{Guid.NewGuid()}";
            InvalidOperationException ex = new InvalidOperationException("boom-" + marker);

            Logger.Warn(marker, ex);

            string tail = ReadLogTail();
            Assert.Contains(marker, tail);
            Assert.Contains("boom-" + marker, tail);
        }

        [Fact]
        public void Error_WritesMessageToLogFile()
        {
            string marker = $"ErrorMarker-{Guid.NewGuid()}";

            Logger.Error(marker);

            Assert.Contains(marker, ReadLogTail());
        }

        [Fact]
        public void Error_WithException_AppendsExceptionText()
        {
            string marker = $"ErrorExMarker-{Guid.NewGuid()}";
            InvalidOperationException ex = new InvalidOperationException("kaboom-" + marker);

            Logger.Error(marker, ex);

            string tail = ReadLogTail();
            Assert.Contains(marker, tail);
            Assert.Contains("kaboom-" + marker, tail);
        }

        [Fact]
        public void RotateIfTooLarge_WhenFileBelowThreshold_LeavesFileInPlace()
        {
            Directory.CreateDirectory(LogDirectory);
            File.WriteAllText(LogFilePath, "small");

            RotateIfTooLarge();

            Assert.True(File.Exists(LogFilePath));
            Assert.Equal("small", File.ReadAllText(LogFilePath));
        }

        [Fact]
        public void RotateIfTooLarge_WhenFileMissing_DoesNotThrow()
        {
            Directory.CreateDirectory(LogDirectory);
            if (File.Exists(LogFilePath))
                File.Delete(LogFilePath);

            Exception thrown = Record.Exception(() => RotateIfTooLarge());

            Assert.Null(thrown);
        }

        [Fact]
        public void RotateIfTooLarge_WhenOverThreshold_MovesToBackup_ReplacingAnyExistingOne()
        {
            Directory.CreateDirectory(LogDirectory);
            string backupPath = LogFilePath + ".old";
            byte[] oversized = new byte[2 * 1024 * 1024 + 1];
            File.WriteAllBytes(LogFilePath, oversized);
            File.WriteAllText(backupPath, "stale-backup");

            try
            {
                RotateIfTooLarge();

                Assert.False(File.Exists(LogFilePath));
                Assert.True(File.Exists(backupPath));
                Assert.Equal(oversized.Length, new FileInfo(backupPath).Length);
            }
            finally
            {
                File.Delete(backupPath);
            }
        }

        [Fact]
        public void Warn_WhenLogDirectoryPathIsBlockedByAFile_SwallowsTheFailure()
        {
            bool directoryExisted = Directory.Exists(LogDirectory);
            string tempAside = LogDirectory + "-test-aside";
            if (directoryExisted)
            {
                if (Directory.Exists(tempAside))
                    Directory.Delete(tempAside, recursive: true);
                Directory.Move(LogDirectory, tempAside);
            }

            try
            {
                File.WriteAllText(LogDirectory, "blocking file where the log directory should be");

                Exception thrown = Record.Exception(() => Logger.Warn("this must not throw"));

                Assert.Null(thrown);
            }
            finally
            {
                if (File.Exists(LogDirectory))
                    File.Delete(LogDirectory);
                if (directoryExisted)
                    Directory.Move(tempAside, LogDirectory);
            }
        }
    }
}
