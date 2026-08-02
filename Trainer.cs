using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using A2G_Trainer_XP.View;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace A2G_Trainer_XP
{
    /// <summary>Main window: hosts the menu and swaps in the active tab (Club/Player/Coach/Help/About).</summary>
    public partial class Trainer : Form
    {
        internal bool ShutDown { get => this.shutDown; private set => this.shutDown = value; }
        private bool shutDown;
        internal Game Anstoss { get => this.anstoss; private set => this.anstoss = value; }
        private Game anstoss;

        internal ProcessMemory Memory { get => this.memory; private set => this.memory = value; }
        private ProcessMemory memory;

        private UserControl current;
        internal ClubView ClubView { get => this.clubView; private set => this.clubView = value; }
        private ClubView clubView;
        internal PlayerView PlayerView { get => this.playerView; private set => this.playerView = value; }
        private PlayerView playerView;
        internal CoachView CoachView { get => this.coachView; private set => this.coachView = value; }
        private CoachView coachView;
        internal AboutView AboutView { get => this.aboutView; private set => this.aboutView = value; }
        private AboutView aboutView;
        internal HelpView HelpView { get => this.helpView; private set => this.helpView = value; }
        private HelpView helpView;

        protected readonly ProcessController processController;

        private readonly string windowTitle;

        public Trainer()
        {
            InitializeComponent();

            this.memory = new ProcessMemory();
            this.Anstoss = new Game();

            this.ShutDown = false;
            this.FormClosing += (s, e) => this.ShutDown = true;

            this.processController = new ProcessController(this);
            this.PlayerView = new PlayerView(this.memory, this.processController);
            this.ClubView = new ClubView(this.memory, this.processController);
            this.CoachView = new CoachView(this.memory, this.processController);

            this.AboutView = new AboutView();
            this.HelpView = new HelpView();

            this.Load += (s, e) => { new Thread(this.processController.Observe).Start(); };

            this.windowTitle = this.Text;

            this.ShowScreen(this.playerView);

        }

        /// <summary>Swaps the content panel to the given tab and updates the window title.</summary>
        private void ShowScreen(UserControl userControl, String title = null)
        {
            if (this.current != userControl)
            {
                this.ContentPanel.SuspendLayout();
                this.ContentPanel.Controls.Clear();
                this.ContentPanel.Controls.Add(userControl);
                this.current = userControl;
                this.ContentPanel.ResumeLayout();
            }
            this.Text = this.windowTitle + (title != null ? " :: " + title : "");
        }

        private void OwnClubToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowScreen(this.ClubView, "Eigener Verein");
            this.ClubView.RefreshValues(PlayerEnums.AddressType.OWN);
        }
        private void TrainerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem item = (ToolStripMenuItem)sender;
            int trainerIndex = (int)item.Tag;
            this.ShowScreen(this.CoachView, item.Text);
            this.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: trainerIndex);
        }

        // Only savegame slots that actually hold a manager get a menu entry - rebuilt whenever the
        // process reconnects or a different savegame is detected (see ProcessController).
        internal void RefreshTrainerMenu(List<KeyValuePair<int, Coach>> activeTrainers)
        {
            this.trainerToolStripMenuItem.DropDownItems.Clear();
            foreach (KeyValuePair<int, Coach> trainer in activeTrainers)
            {
                string name = $"{trainer.Value.Firstname} {trainer.Value.Lastname}".Trim();
                ToolStripMenuItem item = new ToolStripMenuItem(name.Length > 0 ? name : $"Trainer {trainer.Key + 1}")
                {
                    Tag = trainer.Key
                };
                item.Click += this.TrainerToolStripMenuItem_Click;
                this.trainerToolStripMenuItem.DropDownItems.Add(item);
            }
        }
        private void AllClubsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            /*
            this.ShowScreen(this.ClubView, "Alle Vereine");
            this.ClubView.RefreshValues(PlayerEnums.AddressType.ALL);
            */
        }
        private void OwnPlayersToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowScreen(this.PlayerView, "Eigene Mannschaft");
            this.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);
        }
        private void OpponentToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowScreen(this.PlayerView, "Nächster Gegner");
            this.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OPPONENT);
        }
        private void TraineeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowScreen(this.PlayerView, "Jugendspieler");
            this.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.TRAINEE);
        }
        private void DynamicTeamToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowScreen(this.PlayerView, "Dynamische Mannschaft");
            this.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.DYNAMIC);
        }

        private void HelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowScreen(this.HelpView, "Hilfe");
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowScreen(this.AboutView, "Über");
        }
    }
}
