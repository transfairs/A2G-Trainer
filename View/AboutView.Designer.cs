
namespace A2G_Trainer_XP.View
{
    partial class AboutView
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

        #region Vom Komponenten-Designer generierter Code

        /// <summary>
        /// Erforderliche Methode für die Designerunterstützung.
        /// Der Inhalt der Methode darf nicht mit dem Code-Editor geändert werden.
        /// </summary>
        private void InitializeComponent()
        {
            this.AboutBox = new System.Windows.Forms.GroupBox();
            this.GithubLinkLabel = new System.Windows.Forms.LinkLabel();
            this.AlexanderLabel = new System.Windows.Forms.Label();
            this.StrajkLinkLabel = new System.Windows.Forms.LinkLabel();
            this.AnstossJuengerLinkLabel = new System.Windows.Forms.LinkLabel();
            this.VersionLabel = new System.Windows.Forms.Label();
            this.AboutTitleLabel = new System.Windows.Forms.Label();
            this.MainTabControl = new System.Windows.Forms.TabControl();
            this.AboutTab = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.AboutBox.SuspendLayout();
            this.MainTabControl.SuspendLayout();
            this.AboutTab.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // AboutBox
            // 
            this.AboutBox.Controls.Add(this.GithubLinkLabel);
            this.AboutBox.Controls.Add(this.AlexanderLabel);
            this.AboutBox.Controls.Add(this.StrajkLinkLabel);
            this.AboutBox.Controls.Add(this.AnstossJuengerLinkLabel);
            this.AboutBox.Controls.Add(this.VersionLabel);
            this.AboutBox.Controls.Add(this.AboutTitleLabel);
            this.AboutBox.Location = new System.Drawing.Point(6, 6);
            this.AboutBox.Name = "AboutBox";
            this.AboutBox.Size = new System.Drawing.Size(367, 137);
            this.AboutBox.TabIndex = 2;
            this.AboutBox.TabStop = false;
            this.AboutBox.Text = "Über";
            // 
            // GithubLinkLabel
            // 
            this.GithubLinkLabel.AutoSize = true;
            this.GithubLinkLabel.LinkArea = new System.Windows.Forms.LinkArea(8, 41);
            this.GithubLinkLabel.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.GithubLinkLabel.Location = new System.Drawing.Point(6, 38);
            this.GithubLinkLabel.Name = "GithubLinkLabel";
            this.GithubLinkLabel.Size = new System.Drawing.Size(241, 17);
            this.GithubLinkLabel.TabIndex = 6;
            this.GithubLinkLabel.TabStop = true;
            this.GithubLinkLabel.Text = "Github: https://github.com/transfairs/a2g-trainer";
            this.GithubLinkLabel.UseCompatibleTextRendering = true;
            // 
            // AlexanderLabel
            // 
            this.AlexanderLabel.AutoSize = true;
            this.AlexanderLabel.Location = new System.Drawing.Point(6, 65);
            this.AlexanderLabel.Name = "AlexanderLabel";
            this.AlexanderLabel.Size = new System.Drawing.Size(197, 13);
            this.AlexanderLabel.TabIndex = 4;
            this.AlexanderLabel.Text = "Nach einer Idee von Alexander Neufeld.";
            // 
            // StrajkLinkLabel
            // 
            this.StrajkLinkLabel.AutoSize = true;
            this.StrajkLinkLabel.LinkArea = new System.Windows.Forms.LinkArea(19, 7);
            this.StrajkLinkLabel.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.StrajkLinkLabel.Location = new System.Drawing.Point(6, 110);
            this.StrajkLinkLabel.Name = "StrajkLinkLabel";
            this.StrajkLinkLabel.Size = new System.Drawing.Size(231, 17);
            this.StrajkLinkLabel.TabIndex = 8;
            this.StrajkLinkLabel.TabStop = true;
            this.StrajkLinkLabel.Text = "Besonderen Dank an strajk- für die Vorarbeit!";
            this.StrajkLinkLabel.UseCompatibleTextRendering = true;
            // 
            // AnstossJuengerLinkLabel
            // 
            this.AnstossJuengerLinkLabel.AutoSize = true;
            this.AnstossJuengerLinkLabel.LinkArea = new System.Windows.Forms.LinkArea(37, 18);
            this.AnstossJuengerLinkLabel.LinkColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.AnstossJuengerLinkLabel.Location = new System.Drawing.Point(6, 87);
            this.AnstossJuengerLinkLabel.Name = "AnstossJuengerLinkLabel";
            this.AnstossJuengerLinkLabel.Size = new System.Drawing.Size(331, 17);
            this.AnstossJuengerLinkLabel.TabIndex = 7;
            this.AnstossJuengerLinkLabel.TabStop = true;
            this.AnstossJuengerLinkLabel.Text = "Mit Unterstützung der Mitglieder des anstoss-juenger.de-Forums.";
            this.AnstossJuengerLinkLabel.UseCompatibleTextRendering = true;
            // 
            // VersionLabel
            // 
            this.VersionLabel.AutoSize = true;
            this.VersionLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.VersionLabel.Location = new System.Drawing.Point(298, 20);
            this.VersionLabel.Name = "VersionLabel";
            this.VersionLabel.Size = new System.Drawing.Size(66, 13);
            this.VersionLabel.TabIndex = 1;
            this.VersionLabel.Text = "v0.4.0 alpha";
            // 
            // AboutTitleLabel
            // 
            this.AboutTitleLabel.AutoSize = true;
            this.AboutTitleLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.AboutTitleLabel.Location = new System.Drawing.Point(6, 16);
            this.AboutTitleLabel.Name = "AboutTitleLabel";
            this.AboutTitleLabel.Size = new System.Drawing.Size(95, 17);
            this.AboutTitleLabel.TabIndex = 0;
            this.AboutTitleLabel.Text = "A2G - Trainer";
            // 
            // MainTabControl
            // 
            this.MainTabControl.Controls.Add(this.AboutTab);
            this.MainTabControl.Location = new System.Drawing.Point(20, 13);
            this.MainTabControl.Margin = new System.Windows.Forms.Padding(0);
            this.MainTabControl.Name = "MainTabControl";
            this.MainTabControl.Padding = new System.Drawing.Point(0, 0);
            this.MainTabControl.SelectedIndex = 0;
            this.MainTabControl.Size = new System.Drawing.Size(390, 261);
            this.MainTabControl.TabIndex = 42;
            // 
            // AboutTab
            // 
            this.AboutTab.Controls.Add(this.panel1);
            this.AboutTab.Location = new System.Drawing.Point(4, 22);
            this.AboutTab.Name = "AboutTab";
            this.AboutTab.Padding = new System.Windows.Forms.Padding(3);
            this.AboutTab.Size = new System.Drawing.Size(382, 235);
            this.AboutTab.TabIndex = 0;
            this.AboutTab.Text = "Über";
            this.AboutTab.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.AboutBox);
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(379, 370);
            this.panel1.TabIndex = 4;
            // 
            // AboutView
            // 
            this.BackgroundImage = global::A2G_Trainer_XP.Properties.Resources.TabControl;
            this.Controls.Add(this.MainTabControl);
            this.Name = "AboutView";
            this.Size = new System.Drawing.Size(630, 423);
            this.AboutBox.ResumeLayout(false);
            this.AboutBox.PerformLayout();
            this.MainTabControl.ResumeLayout(false);
            this.AboutTab.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.GroupBox AboutBox;
        private System.Windows.Forms.LinkLabel GithubLinkLabel;
        private System.Windows.Forms.Label AlexanderLabel;
        private System.Windows.Forms.LinkLabel StrajkLinkLabel;
        private System.Windows.Forms.LinkLabel AnstossJuengerLinkLabel;
        private System.Windows.Forms.Label VersionLabel;
        private System.Windows.Forms.Label AboutTitleLabel;
        private System.Windows.Forms.TabControl MainTabControl;
        private System.Windows.Forms.TabPage AboutTab;
        private System.Windows.Forms.Panel panel1;
    }
}
