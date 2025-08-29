
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
            this.DynamicTab = new System.Windows.Forms.TabPage();
            this.panel1 = new System.Windows.Forms.Panel();
            this.MainTabControl.SuspendLayout();
            this.DynamicTab.SuspendLayout();
            this.SuspendLayout();
            // 
            // MainTabControl
            // 
            this.MainTabControl.Controls.Add(this.DynamicTab);
            this.MainTabControl.Location = new System.Drawing.Point(20, 13);
            this.MainTabControl.Margin = new System.Windows.Forms.Padding(0);
            this.MainTabControl.Name = "MainTabControl";
            this.MainTabControl.Padding = new System.Drawing.Point(0, 0);
            this.MainTabControl.SelectedIndex = 0;
            this.MainTabControl.Size = new System.Drawing.Size(594, 372);
            this.MainTabControl.TabIndex = 42;
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
            this.panel1.Location = new System.Drawing.Point(0, 0);
            this.panel1.Margin = new System.Windows.Forms.Padding(0);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(593, 368);
            this.panel1.TabIndex = 4;
            // 
            // HelpView
            // 
            this.BackgroundImage = global::A2G_Trainer_XP.Properties.Resources.TabControl;
            this.Controls.Add(this.MainTabControl);
            this.Name = "HelpView";
            this.Size = new System.Drawing.Size(630, 423);
            this.MainTabControl.ResumeLayout(false);
            this.DynamicTab.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.TabControl MainTabControl;
        private System.Windows.Forms.TabPage DynamicTab;
        private System.Windows.Forms.Panel panel1;
    }
}
