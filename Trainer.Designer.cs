
namespace A2G_Trainer_XP
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
            this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            this.ausAnderemVereinToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
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
            this.spielerToolStripMenuItem.Enabled = false;
            this.spielerToolStripMenuItem.Name = "spielerToolStripMenuItem";
            this.spielerToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.spielerToolStripMenuItem.Text = "Alle (kommt später)";
            this.spielerToolStripMenuItem.Click += new System.EventHandler(this.AllClubsToolStripMenuItem_Click);
            // 
            // alleVereineToolStripMenuItem
            // 
            this.alleVereineToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.eigeneToolStripMenuItem,
            this.toolStripSeparator2,
            this.ausAnderemVereinToolStripMenuItem});
            this.alleVereineToolStripMenuItem.Name = "alleVereineToolStripMenuItem";
            this.alleVereineToolStripMenuItem.Size = new System.Drawing.Size(54, 20);
            this.alleVereineToolStripMenuItem.Text = "Spieler";
            // 
            // eigeneToolStripMenuItem
            // 
            this.eigeneToolStripMenuItem.Name = "eigeneToolStripMenuItem";
            this.eigeneToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.eigeneToolStripMenuItem.Text = "Eigene";
            this.eigeneToolStripMenuItem.Click += new System.EventHandler(this.OwnPlayersToolStripMenuItem_Click);
            // 
            // toolStripSeparator2
            // 
            this.toolStripSeparator2.Name = "toolStripSeparator2";
            this.toolStripSeparator2.Size = new System.Drawing.Size(177, 6);
            // 
            // ausAnderemVereinToolStripMenuItem
            // 
            this.ausAnderemVereinToolStripMenuItem.Name = "ausAnderemVereinToolStripMenuItem";
            this.ausAnderemVereinToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.ausAnderemVereinToolStripMenuItem.Text = "Dynamisch";
            this.ausAnderemVereinToolStripMenuItem.Click += new System.EventHandler(this.AllPlayersToolStripMenuItem_Click);
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
            this.überToolStripMenuItem.Text = "Über";
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
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.Fixed3D;
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
    }
}

