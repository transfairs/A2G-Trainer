using AnstossTrainer.Shared.Tests;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for the highscore lists and season records (HighscoreFiles) and their view.</summary>
    public class HighscoreTests : IDisposable
    {
        private readonly string folder = Path.Combine(Path.GetTempPath(), "a2g-highscores-" + Guid.NewGuid().ToString("N"));

        public HighscoreTests() => Directory.CreateDirectory(this.folder);

        public void Dispose()
        {
            // A backup of a read-only file is read-only too.
            foreach (string file in Directory.GetFiles(this.folder))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(this.folder, true);
        }

        // A career list with one entry: Erwin Kramer, age 45, 20 seasons, 12345 points, level 9, 1.11.2026.
        private void WriteCareerList()
        {
            byte[] data = new byte[5 * HighscoreEntry.Size];
            Encoding.ASCII.GetBytes("Erwin").CopyTo(data, 0);
            Encoding.ASCII.GetBytes("Kramer").CopyTo(data, 0x0A);
            BitConverter.GetBytes((short)45).CopyTo(data, 0x1A);
            BitConverter.GetBytes(20).CopyTo(data, 0x1C);
            BitConverter.GetBytes(12345).CopyTo(data, 0x20);
            data[0x24] = 9;
            data[0x25] = 9;
            data[0x2A] = 1;
            data[0x2B] = 11;
            BitConverter.GetBytes((short)2026).CopyTo(data, 0x2C);
            File.WriteAllBytes(Path.Combine(this.folder, "high09"), data);
        }

        [Fact]
        public void Load_ReadsTheCareerList_AndMissingFilesAreEmpty()
        {
            this.WriteCareerList();

            HighscoreFiles files = HighscoreFiles.Load(this.folder);

            HighscoreEntry first = files.Entries(9)[0];
            Assert.Equal(5, files.Entries(9).Count);
            Assert.Equal("Erwin", first.Firstname);
            Assert.Equal("Kramer", first.Lastname);
            Assert.Equal((short)45, first.Years);
            Assert.Equal(20, first.Seasons);
            Assert.Equal(12345, first.Points);
            Assert.Equal((byte)9, first.Level);
            Assert.Equal("01.11.2026", first.Date);
            Assert.Equal("", files.Entries(9)[1].Date);
            Assert.Single(files.Entries(0));
            Assert.Equal(0, files.Entries(0)[0].Points);
            Assert.Equal(21, files.SeasonRecords().Count);
        }

        [Fact]
        public void Save_WritesOnlyChangedOrNonEmptyFiles_AndKeepsABackup()
        {
            this.WriteCareerList();
            HighscoreFiles files = HighscoreFiles.Load(this.folder);
            HighscoreEntry first = files.Entries(9)[0];
            first.Points = 99999;
            first.Lastname = "Ein sehr langer Nachname";
            first.Level = 15;
            first.TitlesA = 3;
            first.TitlesB = 4;
            first.Date = "5.7.1999";
            files.Entries(2)[0].Firstname = "Kim";

            files.Save();

            byte[] career = File.ReadAllBytes(Path.Combine(this.folder, "high09"));
            Assert.Equal(99999, BitConverter.ToInt32(career, 0x20));
            Assert.Equal("Ein sehr langer", Encoding.ASCII.GetString(career, 0x0A, 15));
            Assert.Equal((byte)0, career[0x0A + 15]);
            Assert.Equal(new byte[] { 15, 15, 3, 0, 4, 0, 5, 7 }, career.Skip(0x24).Take(8).ToArray());
            Assert.True(File.Exists(Path.Combine(this.folder, "high09.bak")));
            Assert.True(File.Exists(Path.Combine(this.folder, "high02")));
            Assert.False(File.Exists(Path.Combine(this.folder, "high02.bak")));
            // Untouched and empty: not created.
            Assert.False(File.Exists(Path.Combine(this.folder, "high00")));
            Assert.False(File.Exists(Path.Combine(this.folder, HighscoreFiles.RecordsFileName)));

            // Saving again without changes leaves the files alone.
            File.Delete(Path.Combine(this.folder, "high09.bak"));
            files.Save();
            Assert.False(File.Exists(Path.Combine(this.folder, "high09.bak")));
        }

        [Fact]
        public void SeasonRecords_ReadAndWriteTheirValues()
        {
            byte[] data = new byte[HighscoreFiles.RecordsFileSize];
            // Höchster Heimsieg 7:1, Beste Serie 21 Punkte aus 7 Spielen, Teuerster Verkauf 100 Mio. by "Kramer".
            BitConverter.GetBytes((short)7).CopyTo(data, 0x30);
            BitConverter.GetBytes((short)1).CopyTo(data, 0x32);
            data[0x0E8 + 0x2C] = 21;
            data[0x0E8 + 0x2D] = 7;
            BitConverter.GetBytes(100000000).CopyTo(data, 0x2C8 + 0x30);
            Encoding.ASCII.GetBytes("Kramer").CopyTo(data, 0x2C8 + 0x1C);
            File.WriteAllBytes(Path.Combine(this.folder, HighscoreFiles.RecordsFileName), data);

            HighscoreFiles files = HighscoreFiles.Load(this.folder);
            var records = files.SeasonRecords();

            Assert.Equal("Höchster Heimsieg", records[0].Name);
            Assert.Equal(7, records[0].Value);
            Assert.Equal(1, records[0].SecondValue);
            Assert.Equal(21, records[4].Value);
            Assert.Equal(7, records[4].SecondValue);
            Assert.Equal(100000000, records[14].Value);
            Assert.Equal("Kramer", records[14].Manager);
            Assert.False(records[6].HasSecondValue);
            Assert.Equal(0, records[6].SecondValue);

            records[4].Value = 300;
            records[6].SecondValue = 5;
            records[6].Value = 99999;
            records[14].Manager = "Neuer Rekordhalter";
            records[14].Date = "3.11.2026";
            records[0].Date = "";
            records[0].Date = "kein Datum";
            files.Save();

            byte[] saved = File.ReadAllBytes(Path.Combine(this.folder, HighscoreFiles.RecordsFileName));
            Assert.Equal((byte)255, saved[0x0E8 + 0x2C]);
            Assert.Equal(short.MaxValue, BitConverter.ToInt16(saved, 0x144 + 0x2C));
            Assert.Equal("Neuer Rekordhal", Encoding.ASCII.GetString(saved, 0x2C8 + 0x1C, 15));
            Assert.Equal("03.11.2026", HighscoreFiles.Load(this.folder).SeasonRecords()[14].Date);
            Assert.Equal("", HighscoreFiles.Load(this.folder).SeasonRecords()[0].Date);
        }

        [Fact]
        public void IsGameFolder_LooksForTheTextsFolder()
        {
            Assert.False(HighscoreFiles.IsGameFolder(null));
            Assert.False(HighscoreFiles.IsGameFolder(this.folder));
            Directory.CreateDirectory(Path.Combine(this.folder, "texte"));
            Assert.True(HighscoreFiles.IsGameFolder(this.folder));
            Assert.Equal("high07", HighscoreFiles.ListFileName(7));
        }

        private static T Field<T>(HighscoreView view, string name) =>
            (T)typeof(HighscoreView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        [Fact]
        public void View_ShowsListsAndRecords_AndSaves() => StaThread.Run(() =>
        {
            this.WriteCareerList();
            using (HighscoreView view = new HighscoreView())
            {
                Assert.False(view.Enabled);
                Assert.False(view.Save());
                view.ShowFiles(HighscoreFiles.Load(this.folder));

                DataGridView grid = Field<DataGridView>(view, "grid");
                ListBox lists = Field<ListBox>(view, "lists");
                Assert.Equal(11, lists.Items.Count);
                Assert.Equal(9, lists.SelectedIndex);
                Assert.Equal(5, grid.Rows.Count);
                Assert.Contains(grid.Columns.Cast<DataGridViewColumn>(), c => c.HeaderText == "Alter");
                Assert.Equal("Erwin", grid.Rows[0].Cells[1].Value);

                lists.SelectedIndex = 0;
                Assert.Single(grid.Rows.Cast<DataGridViewRow>());
                Assert.Contains(grid.Columns.Cast<DataGridViewColumn>(), c => c.HeaderText == "Jahre");

                lists.SelectedIndex = 10;
                Assert.Equal(21, grid.Rows.Count);
                Assert.False(grid.Rows[0].Cells[2].ReadOnly);
                Assert.True(grid.Rows[6].Cells[2].ReadOnly);

                // Showing the files again keeps the career list in view.
                lists.SelectedIndex = 9;
                view.ShowFiles(HighscoreFiles.Load(this.folder));
                Assert.Equal(5, grid.Rows.Count);

                Assert.True(view.Save());

                // A file the game folder doesn't let us write is reported, not thrown.
                string blocked = Path.Combine(this.folder, "high09");
                string message = null;
                view.SaveFailed = e => message = e;
                view.Files.Entries(9)[0].Points = 1;
                File.SetAttributes(blocked, FileAttributes.ReadOnly);
                try
                {
                    Assert.False(view.Save());
                    Assert.NotNull(message);
                }
                finally
                {
                    File.SetAttributes(blocked, FileAttributes.Normal);
                }

                view.ShowFiles(null);
                Assert.Null(grid.DataSource);
            }
        });
    }
}
