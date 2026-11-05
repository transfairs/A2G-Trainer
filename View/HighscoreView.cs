using A2G_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// The highscore editor: the ten highscore lists (one per game mode) and the all-time season
    /// records of the game folder (see <see cref="HighscoreFiles"/>), each in a table; "Speichern"
    /// writes the changed files.
    /// </summary>
    public class HighscoreView : UserControl
    {
        private readonly ListBox lists;
        private readonly DataGridView grid;
        private readonly Button saveButton;
        private readonly Label folderLabel;

        /// <summary>Creates the (empty) view.</summary>
        public HighscoreView()
        {
            this.Name = "HighscoreView";
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25F);

            this.folderLabel = new Label { Name = "FolderLabel", Location = new Point(8, 8), Size = new Size(614, 16), AutoEllipsis = true };
            this.lists = new ListBox { Name = "HighscoreLists", Location = new Point(8, 30), Size = new Size(150, 355), IntegralHeight = false };
            this.lists.Items.AddRange(HighscoreFiles.ListNames.Cast<object>().ToArray());
            this.lists.Items.Add("Saisonrekorde");
            this.grid = new DataGridView
            {
                Name = "HighscoreGrid",
                Location = new Point(166, 30),
                Size = new Size(456, 355),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window
            };
            // A value that doesn't fit the field goes back to the old one instead of WinForms' error dialog.
            this.grid.DataError += (s, e) =>
            {
                e.ThrowException = false;
                e.Cancel = false;
                this.grid.CancelEdit();
            };
            this.saveButton = new Button { Name = "SaveButton", Text = "Speichern", Location = new Point(508, 392), Size = new Size(114, 23) };

            this.Controls.AddRange(new Control[] { this.folderLabel, this.lists, this.grid, this.saveButton });
            this.lists.SelectedIndexChanged += (s, e) => this.ShowSelected();
            this.saveButton.Click += (s, e) => this.Save();
            this.Enabled = false;
        }

        /// <summary>The files shown, or null.</summary>
        internal HighscoreFiles Files { get; private set; }

        /// <summary>Called with a message when saving fails.</summary>
        internal Action<string> SaveFailed { get; set; }

        /// <summary>Shows the highscore files of <paramref name="files"/>' folder.</summary>
        public void ShowFiles(HighscoreFiles files)
        {
            this.Files = files;
            this.folderLabel.Text = files?.Folder ?? "";
            this.Enabled = files != null;
            if (this.lists.SelectedIndex == HighscoreFiles.ListNames.Length - 1)
                this.ShowSelected();
            else
                this.lists.SelectedIndex = HighscoreFiles.ListNames.Length - 1;
        }

        private void ShowSelected()
        {
            this.grid.Columns.Clear();
            int index = this.lists.SelectedIndex;
            if (this.Files == null || index < 0)
            {
                this.grid.DataSource = null;
                return;
            }

            this.grid.AutoGenerateColumns = false;
            if (index < HighscoreFiles.ListNames.Length)
            {
                bool career = index == HighscoreFiles.ListNames.Length - 1;
                this.AddColumn(nameof(HighscoreEntry.Rank), "Platz", 40, readOnly: true);
                this.AddColumn(nameof(HighscoreEntry.Firstname), "Vorname", 90);
                this.AddColumn(nameof(HighscoreEntry.Lastname), "Nachname", 110);
                this.AddColumn(nameof(HighscoreEntry.Years), career ? "Alter" : "Jahre", 50);
                if (career)
                    this.AddColumn(nameof(HighscoreEntry.Seasons), "Saisons", 55);
                this.AddColumn(nameof(HighscoreEntry.Points), "Punkte", 70);
                this.AddColumn(nameof(HighscoreEntry.Level), "Level", 45);
                this.AddColumn(nameof(HighscoreEntry.Date), "Datum", 80);
                this.grid.DataSource = this.Files.Entries(index);
            }
            else
            {
                this.AddColumn(nameof(SeasonRecord.Name), "Rekord", 160, readOnly: true);
                this.AddColumn(nameof(SeasonRecord.Value), "Wert", 80);
                this.AddColumn(nameof(SeasonRecord.SecondValue), "Gegentore/Spiele", 80);
                this.AddColumn(nameof(SeasonRecord.Manager), "Manager", 100);
                this.AddColumn(nameof(SeasonRecord.Date), "Datum", 80);
                this.grid.DataSource = this.Files.SeasonRecords();
                foreach (DataGridViewRow row in this.grid.Rows)
                    row.Cells[2].ReadOnly = !((SeasonRecord)row.DataBoundItem).HasSecondValue;
            }
        }

        private void AddColumn(string property, string header, int weight, bool readOnly = false) =>
            this.grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = property, HeaderText = header, FillWeight = weight, ReadOnly = readOnly });

        /// <summary>Writes the files; reports an error through <see cref="SaveFailed"/> instead of throwing.</summary>
        internal bool Save()
        {
            if (this.Files == null)
                return false;
            this.grid.EndEdit();
            try
            {
                this.Files.Save();
                return true;
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                Logger.Warn($"Highscores in {this.Files.Folder} konnten nicht gespeichert werden", ex);
                this.SaveFailed?.Invoke(ex.Message);
                return false;
            }
        }
    }
}
