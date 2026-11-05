using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace A2G_Trainer
{
    /// <summary>Main window: hosts the menu and swaps in the active tab (Club/Player/Coach/Help/About).</summary>
    public partial class Trainer : TrainerShell
    {
        internal Game Anstoss { get => this.anstoss; private set => this.anstoss = value; }
        private Game anstoss;

        internal ProcessMemory Memory { get => this.memory; private set => this.memory = value; }
        private ProcessMemory memory;

        internal ClubView ClubView { get => this.clubView; private set => this.clubView = value; }
        private ClubView clubView;
        internal PlayerView PlayerView { get => this.playerView; private set => this.playerView = value; }
        private PlayerView playerView;
        internal CoachView CoachView { get => this.coachView; private set => this.coachView = value; }
        private CoachView coachView;
        internal HelpView HelpView { get => this.helpView; private set => this.helpView = value; }
        private HelpView helpView;
        internal SaveGameView SaveGameView { get => this.saveGameView; private set => this.saveGameView = value; }
        private SaveGameView saveGameView;
        internal HighscoreView HighscoreView { get; private set; }
        internal MechanicsView MechanicsView { get; private set; }

        protected readonly ProcessController processController;

        /// <summary>
        /// Creates the main window with its views (player, club, coach, game mechanics, help,
        /// savegame editor, highscores) and wires up the menu and the game process controller.
        /// </summary>
        public Trainer()
        {
            InitializeComponent();

            this.memory = new ProcessMemory();
            this.Anstoss = new Game();

            this.processController = new ProcessController(this);
            this.PlayerView = new PlayerView(this.memory, this.processController);
            this.PlayerView.ClubChosen += this.PlayerView_ClubChosen;
            this.ClubView = new ClubView(this.memory, this.processController);
            this.ClubView.ClubChosen += this.ClubView_ClubChosen;
            this.CoachView = new CoachView(this.memory, this.processController);
            this.MechanicsView = new MechanicsView(this.memory, this.processController);

            this.HelpView = new HelpView();
            this.SaveGameView = new SaveGameView();
            this.SaveGameView.Modified += (s, e) => this.ShowSaveGameScreen();
            this.ShowMessage = (text, caption, buttons, icon) => MessageBox.Show(this, text, caption, buttons, icon);
            this.ShowDialogBox = form => form.ShowDialog(this);
            this.HighscoreView = new HighscoreView();
            this.HighscoreView.SaveFailed = message => this.ShowMessage(message, "Highscores speichern", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            ToolStripMenuItem highscores = new ToolStripMenuItem { Name = "highscoresToolStripMenuItem", Text = "Highscores …" };
            highscores.Click += this.HighscoresToolStripMenuItem_Click;
            this.spielstandToolStripMenuItem.DropDownItems.Insert(this.spielstandToolStripMenuItem.DropDownItems.IndexOf(this.toolStripSeparator3), highscores);
            ToolStripMenuItem mechanics = new ToolStripMenuItem { Name = "spielmechanikenToolStripMenuItem", Text = "Spielmechaniken" };
            mechanics.Click += this.MechanicsToolStripMenuItem_Click;
            this.menuStrip.Items.Insert(this.menuStrip.Items.IndexOf(this.spielstandToolStripMenuItem), mechanics);
            // The savegame editor and the highscores are not released yet: the menu is hidden and
            // its shortcuts cleared (a hidden item still answers them), the code stays in place.
            this.spielstandToolStripMenuItem.Visible = false;
            this.spielstandOeffnenToolStripMenuItem.ShortcutKeys = Keys.None;
            this.spielstandSpeichernToolStripMenuItem.ShortcutKeys = Keys.None;

            this.InitShell(this.ContentPanel, this.processController.Observe);
            this.FormClosing += this.Trainer_FormClosing;
            // Only the view shown sits in the window; the others go with it too, so what they
            // switched on in the game (see MechanicsView) is taken back when the trainer closes.
            this.Disposed += (s, e) =>
            {
                foreach (Control view in new Control[] { this.PlayerView, this.ClubView, this.CoachView, this.MechanicsView, this.HelpView, this.SaveGameView, this.HighscoreView })
                    view.Dispose();
            };

            this.ShowScreen(this.playerView);
            this.AddNavigationButtons(this.menuStrip);
            this.RecordNavigation(ScreenKey(this.PlayerView, "Eigene Mannschaft"), () =>
            {
                this.ShowScreen(this.PlayerView, "Eigene Mannschaft");
                this.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);
            });
        }

        // A screen goes into the back/forward history as its view, its title and what fills the
        // view; going back or forward runs the same again.
        private void Open(UserControl view, string title, Action load = null) =>
            this.Navigate(ScreenKey(view, title), () =>
            {
                this.ShowScreen(view, title);
                load?.Invoke();
            });

        // A screen already shown, e.g. a club picked inside a view, only goes into the history.
        private void Remember(UserControl view, string title, Action load) =>
            this.RecordNavigation(ScreenKey(view, title), () =>
            {
                this.ShowScreen(view, title);
                load();
            });

        private static string ScreenKey(UserControl view, string title) => view.GetType().Name + ":" + title;

        private void OwnClubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Open(this.ClubView, "Eigener Verein", () => this.ClubView.RefreshValues(PlayerEnums.AddressType.OWN));
        }
        private void TrainerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            int trainerIndex = (int)item.Tag;
            this.Open(this.CoachView, item.Text, () => this.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: trainerIndex));
        }

        // Only savegame slots that actually hold a manager get a menu entry - rebuilt whenever the
        // process reconnects or a different savegame is detected (see ProcessController).
        internal void RefreshTrainerMenu(List<KeyValuePair<int, Coach>> activeTrainers)
        {
            this.trainerToolStripMenuItem.DropDownItems.Clear();
            foreach (KeyValuePair<int, Coach> trainer in activeTrainers)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(TrainerName(trainer))
                {
                    Tag = trainer.Key
                };
                item.Click += this.TrainerToolStripMenuItem_Click;
                this.trainerToolStripMenuItem.DropDownItems.Add(item);
            }
            this.MechanicsView.SetTrainers(activeTrainers);
        }

        /// <summary>A savegame slot's manager as the menus show it: the name, or "Trainer n" without one.</summary>
        internal static string TrainerName(KeyValuePair<int, Coach> trainer)
        {
            string name = $"{trainer.Value.Firstname} {trainer.Value.Lastname}".Trim();
            return name.Length > 0 ? name : $"Trainer {trainer.Key + 1}";
        }

        private void MechanicsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Open(this.MechanicsView, "Spielmechaniken", this.MechanicsView.RefreshValues);
        }
        private void OtherClubDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Going back shows the club picked at the time, not the one picked last.
            ClubSquads.Entry club = this.ClubView.ChosenClub;
            this.Open(this.ClubView, "Anderer Verein", () => this.ClubView.ShowClub(club));
        }

        // A club picked below the club tabs: the view already shows it, only the title and the history follow.
        private void ClubView_ClubChosen(ClubSquads.Entry club)
        {
            string title = $"Vereinsdaten: {club.Name}";
            this.ShowScreen(this.ClubView, title);
            this.Remember(this.ClubView, title, () => this.ClubView.ShowClub(club));
        }
        private void OwnPlayersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Open(this.PlayerView, "Eigene Mannschaft", () => this.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN));
        }
        private void OpponentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Open(this.PlayerView, "Nächster Gegner", () => this.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OPPONENT));
        }
        private void TraineeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Open(this.PlayerView, "Jugendspieler", () => this.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.TRAINEE));
        }
        private void OtherClubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Going back shows the club picked at the time, not the one picked last.
            ClubSquads.Entry club = this.PlayerView.ChosenClub;
            this.Open(this.PlayerView, "Anderer Verein", () => this.PlayerView.ShowClub(club, PlayerEnums.AddressType.CLUB));
        }

        // A club picked above the player list: the view already shows it, only the title and the history follow.
        private void PlayerView_ClubChosen(ClubSquads.Entry club, PlayerEnums.AddressType type)
        {
            string title = $"{(type == PlayerEnums.AddressType.TRAINEE ? "Jugendspieler" : "Verein")}: {club.Name}";
            this.ShowScreen(this.PlayerView, title);
            this.Remember(this.PlayerView, title, () => this.PlayerView.ShowClub(club, type));
        }

        private void HelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Filled before it's shown, so it's laid out and scaled with the rest of the screen.
            this.HelpView.EnsureHelpLoaded();
            this.Open(this.HelpView, "Hilfe");
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (AboutDialog dialog = new AboutDialog())
                this.ShowDialogBox(dialog);
        }

        // Replaceable so tests don't block on a modal dialog.
        internal Func<Form, DialogResult> ShowDialogBox { get; set; }

        #region Spielstand
        private const string SaveGameFilter = "Spielstände (*.gld)|*.gld|Alle Dateien (*.*)|*.*";

        // Replaceable so tests don't block on a dialog.
        internal Func<string, string, MessageBoxButtons, MessageBoxIcon, DialogResult> ShowMessage { get; set; }

        private SaveGameEditor SaveGame => this.SaveGameView.Editor;

        private void SaveGameToolStripMenuItem_DropDownOpening(object sender, EventArgs e)
        {
            bool open = this.SaveGame != null;
            this.spielstandSpeichernToolStripMenuItem.Enabled = open;
            this.spielstandSpeichernUnterToolStripMenuItem.Enabled = open;
            this.spielstandSchliessenToolStripMenuItem.Enabled = open;
        }

        private void OpenSaveGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!this.ConfirmDiscardSaveGame())
                return;

            using (OpenFileDialog dialog = new OpenFileDialog { Filter = SaveGameFilter, Title = "Spielstand öffnen" })
            {
                string folder = this.SaveGameFolder();
                if (folder != null)
                    dialog.InitialDirectory = folder;
                if (dialog.ShowDialog(this) != DialogResult.OK)
                    return;
                this.OpenSaveGame(dialog.FileName);
            }
        }

        /// <summary>Opens a savegame file in the editor; reports a file that can't be read instead of throwing.</summary>
        internal bool OpenSaveGame(string path)
        {
            SaveGameEditor editor;
            try
            {
                editor = SaveGameEditor.Open(path);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is System.IO.InvalidDataException || ex is UnauthorizedAccessException)
            {
                Logger.Warn($"Spielstand {path} konnte nicht geöffnet werden", ex);
                this.ShowMessage(ex.Message, "Spielstand öffnen", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            this.SaveGameView.ShowSaveGame(editor);
            this.ShowSaveGameScreen();
            return true;
        }

        private void SaveSaveGameToolStripMenuItem_Click(object sender, EventArgs e) => this.SaveSaveGame(null);

        private void SaveSaveGameAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (this.SaveGame == null)
                return;
            using (SaveFileDialog dialog = new SaveFileDialog { Filter = SaveGameFilter, Title = "Spielstand speichern unter", FileName = System.IO.Path.GetFileName(this.SaveGame.File.Path) })
            {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(this.SaveGame.File.Path);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    this.SaveSaveGame(dialog.FileName);
            }
        }

        // Saving over the opened file keeps the previous version as "<name>.bak".
        internal bool SaveSaveGame(string path)
        {
            if (this.SaveGame == null)
                return false;
            try
            {
                this.SaveGame.File.Save(path, backup: path == null || path == this.SaveGame.File.Path);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                Logger.Warn($"Spielstand konnte nicht gespeichert werden", ex);
                this.ShowMessage(ex.Message, "Spielstand speichern", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            this.ShowSaveGameScreen();
            return true;
        }

        private void CloseSaveGameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!this.ConfirmDiscardSaveGame())
                return;
            this.SaveGameView.Clear();
            this.ShowScreen(this.PlayerView, "Eigene Mannschaft");
        }

        private void Trainer_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!e.Cancel && !this.ConfirmDiscardSaveGame())
                e.Cancel = true;
        }

        // True if there's nothing unsaved or the user chose to save or discard it.
        private bool ConfirmDiscardSaveGame()
        {
            if (this.SaveGame == null || !this.SaveGame.File.IsModified)
                return true;
            DialogResult answer = this.ShowMessage($"Änderungen an {System.IO.Path.GetFileName(this.SaveGame.File.Path)} speichern?", "Spielstand", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer == DialogResult.Yes)
                return this.SaveSaveGame(null);
            return answer == DialogResult.No;
        }

        private void ShowSaveGameScreen()
        {
            if (this.SaveGame == null)
                return;
            string name = System.IO.Path.GetFileName(this.SaveGame.File.Path);
            this.ShowScreen(this.SaveGameView, $"Spielstand {name}{(this.SaveGame.File.IsModified ? " *" : "")}");
        }

        // The highscore lists and season records live in the game folder: the running game's, else one the user picks.
        private void HighscoresToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string folder = this.SaveGameFolder();
            if (!HighscoreFiles.IsGameFolder(folder))
            {
                using (FolderBrowserDialog dialog = new FolderBrowserDialog { Description = "Ordner von Anstoss 2 Gold" })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                        return;
                    folder = dialog.SelectedPath;
                }
            }
            this.OpenHighscores(folder);
        }

        /// <summary>Shows the highscore files of <paramref name="folder"/>; reports a folder that can't be read instead of throwing.</summary>
        internal bool OpenHighscores(string folder)
        {
            HighscoreFiles files;
            try
            {
                files = HighscoreFiles.Load(folder);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
            {
                Logger.Warn($"Highscores in {folder} konnten nicht gelesen werden", ex);
                this.ShowMessage(ex.Message, "Highscores", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            this.HighscoreView.ShowFiles(files);
            this.Open(this.HighscoreView, "Highscores");
            return true;
        }

        // The game's folder, where it keeps its savegames, if the game is running.
        private string SaveGameFolder()
        {
            try
            {
                string exe = this.Anstoss.Process?.MainModule?.FileName;
                return exe != null ? System.IO.Path.GetDirectoryName(exe) : null;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception)
            {
                return null;
            }
        }
        #endregion
    }
}
