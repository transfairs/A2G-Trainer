using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using A2G_Trainer_XP.View;
using Memory;
using System;
using System.Threading;
using System.Windows.Forms;

namespace A2G_Trainer_XP
{
    public partial class Trainer : Form
    {
        internal bool ShutDown { get => this.shutDown; private set => this.shutDown = value; }
        private bool shutDown;
        internal Game Anstoss { get => this.anstoss; private set => this.anstoss = value; }
        private Game anstoss;

        internal Mem Memory { get => this.memory; private set => this.memory = value; }
        private Mem memory;

        private EntityView current;
        internal ClubView ClubView { get => this.clubView; private set => this.clubView = value; }
        private ClubView clubView;
        internal PlayerView PlayerView { get => this.playerView; private set => this.playerView = value; }
        private PlayerView playerView;
        protected readonly ProcessController processController;


        public Trainer()
        {
            InitializeComponent();

            this.memory = new Mem();
            this.Anstoss = new Game();

            this.ShutDown = false;
            this.FormClosing += (s, e) => this.ShutDown = true;

            this.processController = new ProcessController(this);
            this.playerView = new PlayerView(this.memory, this.processController);
            this.clubView = new ClubView(this.memory, this.processController);

            this.Load += (s, e) => { new Thread(this.processController.Observe).Start(); };

            this.ShowScreen(this.playerView);
        }

        private void ShowScreen(EntityView entityView)
        {
            if (this.current != entityView)
            {
                this.ContentPanel.SuspendLayout();
                this.ContentPanel.Controls.Clear();
                this.ContentPanel.Controls.Add(entityView);
                this.current = entityView;
                this.ContentPanel.ResumeLayout();
            }
        }

        private void SpielerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowScreen(this.playerView);
            this.PlayerView.RefreshPlayerListView();
        }

        private void VereinToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.ShowScreen(this.clubView);
            this.ClubView.RefreshValues();
        }
    }
}
