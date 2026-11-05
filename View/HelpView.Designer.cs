namespace A2G_Trainer.View
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
            this.SuspendLayout();
            //
            // MainTabControl
            //
            this.MainTabControl.Location = new System.Drawing.Point(20, 13);
            this.MainTabControl.Margin = new System.Windows.Forms.Padding(0);
            this.MainTabControl.Name = "MainTabControl";
            this.MainTabControl.Padding = new System.Drawing.Point(4, 3);
            this.MainTabControl.SelectedIndex = 0;
            this.MainTabControl.Size = new System.Drawing.Size(594, 372);
            this.MainTabControl.TabIndex = 42;
            //
            // HelpView
            //
            this.BackgroundImage = global::A2G_Trainer.Properties.Resources.TabControl;
            this.Controls.Add(this.MainTabControl);
            this.Name = "HelpView";
            this.Size = new System.Drawing.Size(630, 423);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl MainTabControl;
    }
}
