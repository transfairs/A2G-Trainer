
namespace A2G_Trainer
{
    partial class Trainer
    {
        /// <summary>
        /// Erforderliche Designervariable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Verwendete Ressourcen bereinigen.
        /// </summary>
        /// <param name="disposing">True, wenn verwaltete Ressourcen gelöscht werden sollen; andernfalls False.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Vom Windows Form-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Trainer));
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.menuStrip = new System.Windows.Forms.MenuStrip();
            this.ansichtToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.vereinToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            this.spielerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.alleVereineToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.eigeneToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.jugendspielerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.ausAnderemVereinToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.andererVereinToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.trainerToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.spielstandToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.spielstandOeffnenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.spielstandSpeichernToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.spielstandSpeichernUnterToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripSeparator3 = new System.Windows.Forms.ToolStripSeparator();
            this.spielstandSchliessenToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ansichtToolStripMenuItem1 = new System.Windows.Forms.ToolStripMenuItem();
            this.hilfeToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.überToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.ContentPanel = new System.Windows.Forms.Panel();
            this.menuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(61, 4);
            // 
            // menuStrip
            // 
            this.menuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.ansichtToolStripMenuItem,
            this.alleVereineToolStripMenuItem,
            this.trainerToolStripMenuItem,
            this.spielstandToolStripMenuItem,
            this.ansichtToolStripMenuItem1});
            this.menuStrip.Location = new System.Drawing.Point(0, 0);
            this.menuStrip.Name = "menuStrip";
            this.menuStrip.Size = new System.Drawing.Size(630, 24);
            this.menuStrip.TabIndex = 42;
            this.menuStrip.Text = "menuStrip1";
            // 
            // ansichtToolStripMenuItem
            // 
            this.ansichtToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.vereinToolStripMenuItem,
            this.toolStripSeparator1,
            this.spielerToolStripMenuItem});
            this.ansichtToolStripMenuItem.Name = "ansichtToolStripMenuItem";
            this.ansichtToolStripMenuItem.Size = new System.Drawing.Size(51, 20);
            this.ansichtToolStripMenuItem.Text = "Verein";
            // 
            // vereinToolStripMenuItem
            // 
            this.vereinToolStripMenuItem.Name = "vereinToolStripMenuItem";
            this.vereinToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.vereinToolStripMenuItem.Text = "Eigener";
            this.vereinToolStripMenuItem.Click += new System.EventHandler(this.OwnClubToolStripMenuItem_Click);
            // 
            // toolStripSeparator1
            // 
            this.toolStripSeparator1.Name = "toolStripSeparator1";
            this.toolStripSeparator1.Size = new System.Drawing.Size(177, 6);
            // 
            // spielerToolStripMenuItem
            // 
            this.spielerToolStripMenuItem.Name = "spielerToolStripMenuItem";
            this.spielerToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.spielerToolStripMenuItem.Text = "Anderer";
            this.spielerToolStripMenuItem.Click += new System.EventHandler(this.OtherClubDataToolStripMenuItem_Click);
            // 
            // alleVereineToolStripMenuItem
            // 
            this.alleVereineToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.eigeneToolStripMenuItem,
            this.jugendspielerToolStripMenuItem,
            this.toolStripSeparator2,
            this.ausAnderemVereinToolStripMenuItem,
            this.andererVereinToolStripMenuItem});
            this.alleVereineToolStripMenuItem.Name = "alleVereineToolStripMenuItem";
            this.alleVereineToolStripMenuItem.Size = new System.Drawing.Size(54, 20);
            this.alleVereineToolStripMenuItem.Text = "Spieler";
            // 
            // eigeneToolStripMenuItem
            // 
            this.eigeneToolStripMenuItem.Name = "eigeneToolStripMenuItem";
            this.eigeneToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.eigeneToolStripMenuItem.Text = "Eigener Kader";
            this.eigeneToolStripMenuItem.Click += new System.EventHandler(this.OwnPlayersToolStripMenuItem_Click);
            // 
            // jugendspielerToolStripMenuItem
            // 
            this.jugendspielerToolStripMenuItem.Name = "jugendspielerToolStripMenuItem";
            this.jugendspielerToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.jugendspielerToolStripMenuItem.Text = "Jugendspieler";
            this.jugendspielerToolStripMenuItem.Click += new System.EventHandler(this.TraineeToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(173, 6);
            // 
            // ausAnderemVereinToolStripMenuItem
            // 
            this.ausAnderemVereinToolStripMenuItem.Name = "ausAnderemVereinToolStripMenuItem";
            this.ausAnderemVereinToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.ausAnderemVereinToolStripMenuItem.Text = "Nächster Gegner";
            this.ausAnderemVereinToolStripMenuItem.Click += new System.EventHandler(this.OpponentToolStripMenuItem_Click);
            // 
            // andererVereinToolStripMenuItem
            // 
            this.andererVereinToolStripMenuItem.Name = "andererVereinToolStripMenuItem";
            this.andererVereinToolStripMenuItem.Size = new System.Drawing.Size(176, 22);
            this.andererVereinToolStripMenuItem.Text = "Anderer Verein";
            this.andererVereinToolStripMenuItem.Click += new System.EventHandler(this.OtherClubToolStripMenuItem_Click);
            //
            // trainerToolStripMenuItem
            //
            this.trainerToolStripMenuItem.Name = "trainerToolStripMenuItem";
            this.trainerToolStripMenuItem.Size = new System.Drawing.Size(54, 20);
            this.trainerToolStripMenuItem.Text = "Trainer";
            // 
            // spielstandToolStripMenuItem
            // 
            this.spielstandToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.spielstandOeffnenToolStripMenuItem,
            this.spielstandSpeichernToolStripMenuItem,
            this.spielstandSpeichernUnterToolStripMenuItem,
            this.toolStripSeparator3,
            this.spielstandSchliessenToolStripMenuItem});
            this.spielstandToolStripMenuItem.Name = "spielstandToolStripMenuItem";
            this.spielstandToolStripMenuItem.Size = new System.Drawing.Size(74, 20);
            this.spielstandToolStripMenuItem.Text = "Spielstand";
            this.spielstandToolStripMenuItem.DropDownOpening += new System.EventHandler(this.SaveGameToolStripMenuItem_DropDownOpening);
            // 
            // spielstandOeffnenToolStripMenuItem
            // 
            this.spielstandOeffnenToolStripMenuItem.Name = "spielstandOeffnenToolStripMenuItem";
            this.spielstandOeffnenToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.O)));
            this.spielstandOeffnenToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.spielstandOeffnenToolStripMenuItem.Text = "Öffnen …";
            this.spielstandOeffnenToolStripMenuItem.Click += new System.EventHandler(this.OpenSaveGameToolStripMenuItem_Click);
            // 
            // spielstandSpeichernToolStripMenuItem
            // 
            this.spielstandSpeichernToolStripMenuItem.Name = "spielstandSpeichernToolStripMenuItem";
            this.spielstandSpeichernToolStripMenuItem.ShortcutKeys = ((System.Windows.Forms.Keys)((System.Windows.Forms.Keys.Control | System.Windows.Forms.Keys.S)));
            this.spielstandSpeichernToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.spielstandSpeichernToolStripMenuItem.Text = "Speichern";
            this.spielstandSpeichernToolStripMenuItem.Click += new System.EventHandler(this.SaveSaveGameToolStripMenuItem_Click);
            // 
            // spielstandSpeichernUnterToolStripMenuItem
            // 
            this.spielstandSpeichernUnterToolStripMenuItem.Name = "spielstandSpeichernUnterToolStripMenuItem";
            this.spielstandSpeichernUnterToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.spielstandSpeichernUnterToolStripMenuItem.Text = "Speichern unter …";
            this.spielstandSpeichernUnterToolStripMenuItem.Click += new System.EventHandler(this.SaveSaveGameAsToolStripMenuItem_Click);
            // 
            // toolStripSeparator3
            // 
            this.toolStripSeparator3.Name = "toolStripSeparator3";
            this.toolStripSeparator3.Size = new System.Drawing.Size(177, 6);
            // 
            // spielstandSchliessenToolStripMenuItem
            // 
            this.spielstandSchliessenToolStripMenuItem.Name = "spielstandSchliessenToolStripMenuItem";
            this.spielstandSchliessenToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.spielstandSchliessenToolStripMenuItem.Text = "Schließen";
            this.spielstandSchliessenToolStripMenuItem.Click += new System.EventHandler(this.CloseSaveGameToolStripMenuItem_Click);
            //
            // ansichtToolStripMenuItem1
            // 
            this.ansichtToolStripMenuItem1.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.hilfeToolStripMenuItem,
            this.überToolStripMenuItem});
            this.ansichtToolStripMenuItem1.Name = "ansichtToolStripMenuItem1";
            this.ansichtToolStripMenuItem1.Size = new System.Drawing.Size(24, 20);
            this.ansichtToolStripMenuItem1.Text = "?";
            // 
            // hilfeToolStripMenuItem
            // 
            this.hilfeToolStripMenuItem.Name = "hilfeToolStripMenuItem";
            this.hilfeToolStripMenuItem.Size = new System.Drawing.Size(99, 22);
            this.hilfeToolStripMenuItem.Text = "Hilfe";
            this.hilfeToolStripMenuItem.Click += new System.EventHandler(this.HelpToolStripMenuItem_Click);
            // 
            // überToolStripMenuItem
            // 
            this.überToolStripMenuItem.Name = "überToolStripMenuItem";
            this.überToolStripMenuItem.Size = new System.Drawing.Size(99, 22);
            this.überToolStripMenuItem.Text = "Über …";
            this.überToolStripMenuItem.Click += new System.EventHandler(this.AboutToolStripMenuItem_Click);
            // 
            // ContentPanel
            // 
            this.ContentPanel.Dock = System.Windows.Forms.DockStyle.Fill;
            this.ContentPanel.Location = new System.Drawing.Point(0, 24);
            this.ContentPanel.Name = "ContentPanel";
            this.ContentPanel.Size = new System.Drawing.Size(630, 423);
            this.ContentPanel.TabIndex = 43;
            // 
            // Trainer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Control;
            this.ClientSize = new System.Drawing.Size(630, 447);
            this.Controls.Add(this.ContentPanel);
            this.Controls.Add(this.menuStrip);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Sizable;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MainMenuStrip = this.menuStrip;
            this.Name = "Trainer";
            this.Text = "A2G - Trainer";
            this.menuStrip.ResumeLayout(false);
            this.menuStrip.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.Windows.Forms.ToolStripMenuItem ansichtToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem spielerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem vereinToolStripMenuItem;
        private System.Windows.Forms.MenuStrip menuStrip;
        private System.Windows.Forms.Panel ContentPanel;
        private System.Windows.Forms.ToolStripMenuItem alleVereineToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem eigeneToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem ausAnderemVereinToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripMenuItem ansichtToolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem hilfeToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem überToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem andererVereinToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem jugendspielerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem trainerToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem spielstandToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem spielstandOeffnenToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem spielstandSpeichernToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem spielstandSpeichernUnterToolStripMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator3;
        private System.Windows.Forms.ToolStripMenuItem spielstandSchliessenToolStripMenuItem;
    }
}

