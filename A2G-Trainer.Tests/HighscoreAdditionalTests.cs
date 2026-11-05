using AnstossTrainer.Shared.Tests;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Further tests for the highscore files and their view: ranks, cleared texts, four-byte record
    /// values, rejected grid input and a failed save without a listener.
    /// </summary>
    public class HighscoreAdditionalTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "a2g-highscores-add-" + Guid.NewGuid().ToString("N"));

        public HighscoreAdditionalTests() => Directory.CreateDirectory(this.folder);

        public void Dispose()
        {
            foreach (string file in Directory.GetFiles(this.folder))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(this.folder, true);
        }

        [Fact]
        public void Entries_AreRankedFromOne_AndNullNamesClearTheField()
        {
            HighscoreFiles files = HighscoreFiles.Load(this.folder);
            HighscoreEntry entry = files.Entries(9)[2];
            entry.Firstname = "Erwin";

            entry.Firstname = null;

            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, files.Entries(9).Select(e => e.Rank));
            Assert.Equal(string.Empty, entry.Firstname);
        }

        [Fact]
        public void SeasonRecord_FourByteValue_AndNullManager()
        {
            HighscoreFiles files = HighscoreFiles.Load(this.folder);
            // Teuerster Verkauf: a four-byte value (+0x30).
            SeasonRecord record = files.SeasonRecords()[14];
            record.Manager = "Kramer";

            record.Value = 123456789;
            record.Manager = null;
            files.Save();

            byte[] saved = File.ReadAllBytes(Path.Combine(this.folder, HighscoreFiles.RecordsFileName));
            Assert.Equal(123456789, BitConverter.ToInt32(saved, 0x2C8 + 0x30));
            Assert.Equal(new byte[16], saved.Skip(0x2C8 + 0x1C).Take(16).ToArray());
            SeasonRecord reloaded = HighscoreFiles.Load(this.folder).SeasonRecords()[14];
            Assert.Equal(123456789, reloaded.Value);
            Assert.Equal(string.Empty, reloaded.Manager);
        }

        [Fact]
        public void View_RejectedInput_KeepsTheOldValueWithoutErrorDialog() => StaThread.Run(() =>
        {
            using (HighscoreView view = new HighscoreView())
            {
                view.ShowFiles(HighscoreFiles.Load(this.folder));
                DataGridView grid = (DataGridView)typeof(HighscoreView).GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                DataGridViewDataErrorEventArgs error = new DataGridViewDataErrorEventArgs(new FormatException("keine Zahl"), 5, 0, DataGridViewDataErrorContexts.Commit)
                {
                    ThrowException = true,
                    Cancel = true
                };

                typeof(DataGridView).GetMethod("OnDataError", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(grid, new object[] { true, error });

                Assert.False(error.ThrowException);
                Assert.False(error.Cancel);
                Assert.Equal(0, view.Files.Entries(9)[0].Points);
            }
        });

        [Fact]
        public void View_FailedSaveWithoutListener_ReturnsFalse() => StaThread.Run(() =>
        {
            string blocked = Path.Combine(this.folder, "high09");
            File.WriteAllBytes(blocked, new byte[5 * HighscoreEntry.Size]);
            using (HighscoreView view = new HighscoreView())
            {
                view.ShowFiles(HighscoreFiles.Load(this.folder));
                view.Files.Entries(9)[0].Points = 42;
                File.SetAttributes(blocked, FileAttributes.ReadOnly);

                Assert.Null(view.SaveFailed);
                Assert.False(view.Save());
                Assert.Equal(new byte[5 * HighscoreEntry.Size], File.ReadAllBytes(blocked));
            }
        });
    }
}
