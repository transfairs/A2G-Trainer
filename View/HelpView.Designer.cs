
namespace A2G_Trainer_XP.View
{
    partial class HelpView
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
            this.MainTabControl = new System.Windows.Forms.TabControl();
            this.GeneralTab = new System.Windows.Forms.TabPage();
            this.GeneralHelp = new System.Windows.Forms.RichTextBox();
            this.DynamicTab = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.DynamicHelp = new System.Windows.Forms.RichTextBox();
            this.TraineeTab = new System.Windows.Forms.TabPage();
            this.TraineeHelp = new System.Windows.Forms.RichTextBox();
            this.MainTabControl.SuspendLayout();
            this.GeneralTab.SuspendLayout();
            this.DynamicTab.SuspendLayout();
            this.panel1.SuspendLayout();
            this.TraineeTab.SuspendLayout();
            this.SuspendLayout();
            // 
            // MainTabControl
            // 
            this.MainTabControl.Controls.Add(this.GeneralTab);
            this.MainTabControl.Controls.Add(this.DynamicTab);
            this.MainTabControl.Controls.Add(this.TraineeTab);
            this.MainTabControl.Location = new System.Drawing.Point(20, 13);
            this.MainTabControl.Margin = new System.Windows.Forms.Padding(0);
            this.MainTabControl.Name = "MainTabControl";
            this.MainTabControl.Padding = new System.Drawing.Point(0, 0);
            this.MainTabControl.SelectedIndex = 0;
            this.MainTabControl.Size = new System.Drawing.Size(594, 372);
            this.MainTabControl.TabIndex = 42;
            // 
            // GeneralTab
            // 
            this.GeneralTab.Controls.Add(this.GeneralHelp);
            this.GeneralTab.Location = new System.Drawing.Point(4, 22);
            this.GeneralTab.Name = "GeneralTab";
            this.GeneralTab.Size = new System.Drawing.Size(586, 346);
            this.GeneralTab.TabIndex = 2;
            this.GeneralTab.Text = "Allgemeines";
            this.GeneralTab.UseVisualStyleBackColor = true;
            // 
            // GeneralHelp
            // 
            this.GeneralHelp.BackColor = System.Drawing.SystemColors.HighlightText;
            this.GeneralHelp.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.GeneralHelp.Location = new System.Drawing.Point(5, 6);
            this.GeneralHelp.Name = "GeneralHelp";
            this.GeneralHelp.ReadOnly = true;
            this.GeneralHelp.Size = new System.Drawing.Size(577, 337);
            this.GeneralHelp.TabIndex = 2;
            this.GeneralHelp.Text = "";
            // 
            // DynamicTab
            // 
            this.DynamicTab.Controls.Add(this.panel1);
            this.DynamicTab.Location = new System.Drawing.Point(4, 22);
            this.DynamicTab.Name = "DynamicTab";
            this.DynamicTab.Padding = new System.Windows.Forms.Padding(3);
            this.DynamicTab.Size = new System.Drawing.Size(586, 346);
            this.DynamicTab.TabIndex = 0;
            this.DynamicTab.Text = "Dynamische Mannschaft";
            this.DynamicTab.UseVisualStyleBackColor = true;
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.DynamicHelp);
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(593, 368);
            this.panel1.TabIndex = 4;
            // 
            // DynamicHelp
            // 
            this.DynamicHelp.BackColor = System.Drawing.SystemColors.HighlightText;
            this.DynamicHelp.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.DynamicHelp.Location = new System.Drawing.Point(3, 6);
            this.DynamicHelp.Name = "DynamicHelp";
            this.DynamicHelp.ReadOnly = true;
            this.DynamicHelp.Size = new System.Drawing.Size(577, 334);
            this.DynamicHelp.TabIndex = 1;
            this.DynamicHelp.Text = "";
            // 
            // TraineeTab
            // 
            this.TraineeTab.Controls.Add(this.TraineeHelp);
            this.TraineeTab.Location = new System.Drawing.Point(4, 22);
            this.TraineeTab.Name = "TraineeTab";
            this.TraineeTab.Size = new System.Drawing.Size(586, 346);
            this.TraineeTab.TabIndex = 1;
            this.TraineeTab.Text = "Jugendspieler";
            this.TraineeTab.UseVisualStyleBackColor = true;
            // 
            // TraineeHelp
            // 
            this.TraineeHelp.BackColor = System.Drawing.SystemColors.HighlightText;
            this.TraineeHelp.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.TraineeHelp.Location = new System.Drawing.Point(5, 6);
            this.TraineeHelp.Name = "TraineeHelp";
            this.TraineeHelp.ReadOnly = true;
            this.TraineeHelp.Size = new System.Drawing.Size(577, 334);
            this.TraineeHelp.TabIndex = 2;
            this.TraineeHelp.Text = "";
            // 
            // HelpView
            // 
            this.BackgroundImage = global::A2G_Trainer_XP.Properties.Resources.TabControl;
            this.Controls.Add(this.MainTabControl);
            this.Name = "HelpView";
            this.Size = new System.Drawing.Size(630, 423);
            this.MainTabControl.ResumeLayout(false);
            this.GeneralTab.ResumeLayout(false);
            this.DynamicTab.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.TraineeTab.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.TabControl MainTabControl;
        private System.Windows.Forms.TabPage DynamicTab;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.RichTextBox DynamicHelp;
        private System.Windows.Forms.TabPage TraineeTab;
        private System.Windows.Forms.RichTextBox TraineeHelp;
        private System.Windows.Forms.TabPage GeneralTab;
        private System.Windows.Forms.RichTextBox GeneralHelp;
    }
}
