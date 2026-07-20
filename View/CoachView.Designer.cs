
namespace A2G_Trainer_XP.View
{
    partial class CoachView
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
            this.ClubTabControl = new System.Windows.Forms.TabControl();
            this.GeneralTab = new System.Windows.Forms.TabPage();
            this.PersonalBox = new System.Windows.Forms.GroupBox();
            this.WealthLabel = new System.Windows.Forms.Label();
            this.WealthInput = new System.Windows.Forms.TextBox();
            this.LevelLabel = new System.Windows.Forms.Label();
            this.LevelInput = new System.Windows.Forms.TextBox();
            this.Age = new System.Windows.Forms.Label();
            this.AgeInput = new System.Windows.Forms.TextBox();
            this.LastName = new System.Windows.Forms.Label();
            this.LastNameInput = new System.Windows.Forms.TextBox();
            this.FirstNameLabel = new System.Windows.Forms.Label();
            this.FirstNameInput = new System.Windows.Forms.TextBox();
            this.StocksTab = new System.Windows.Forms.TabPage();
            this.SaveBtn = new System.Windows.Forms.Button();
            this.ReloadBtn = new System.Windows.Forms.Button();
            this.ClubTabControl.SuspendLayout();
            this.GeneralTab.SuspendLayout();
            this.PersonalBox.SuspendLayout();
            this.SuspendLayout();
            //
            // ClubTabControl
            //
            this.ClubTabControl.Controls.Add(this.GeneralTab);
            this.ClubTabControl.Controls.Add(this.StocksTab);
            this.ClubTabControl.Location = new System.Drawing.Point(20, 13);
            this.ClubTabControl.Name = "ClubTabControl";
            this.ClubTabControl.SelectedIndex = 0;
            this.ClubTabControl.Size = new System.Drawing.Size(594, 372);
            this.ClubTabControl.TabIndex = 0;
            //
            // GeneralTab
            //
            this.GeneralTab.Controls.Add(this.PersonalBox);
            this.GeneralTab.Location = new System.Drawing.Point(4, 22);
            this.GeneralTab.Name = "GeneralTab";
            this.GeneralTab.Padding = new System.Windows.Forms.Padding(3);
            this.GeneralTab.Size = new System.Drawing.Size(586, 346);
            this.GeneralTab.TabIndex = 0;
            this.GeneralTab.Text = "Allgemein";
            this.GeneralTab.UseVisualStyleBackColor = true;
            //
            // PersonalBox
            //
            this.PersonalBox.Controls.Add(this.WealthLabel);
            this.PersonalBox.Controls.Add(this.WealthInput);
            this.PersonalBox.Controls.Add(this.LevelLabel);
            this.PersonalBox.Controls.Add(this.LevelInput);
            this.PersonalBox.Controls.Add(this.Age);
            this.PersonalBox.Controls.Add(this.AgeInput);
            this.PersonalBox.Controls.Add(this.LastName);
            this.PersonalBox.Controls.Add(this.LastNameInput);
            this.PersonalBox.Controls.Add(this.FirstNameLabel);
            this.PersonalBox.Controls.Add(this.FirstNameInput);
            this.PersonalBox.Location = new System.Drawing.Point(6, 6);
            this.PersonalBox.Name = "PersonalBox";
            this.PersonalBox.Size = new System.Drawing.Size(220, 195);
            this.PersonalBox.TabIndex = 41;
            this.PersonalBox.TabStop = false;
            this.PersonalBox.Text = "Persönliche Daten";
            //
            // LevelLabel
            //
            this.LevelLabel.AutoSize = true;
            this.LevelLabel.Location = new System.Drawing.Point(7, 134);
            this.LevelLabel.Name = "LevelLabel";
            this.LevelLabel.Size = new System.Drawing.Size(60, 13);
            this.LevelLabel.TabIndex = 47;
            this.LevelLabel.Text = "Kompetenz";
            //
            // LevelInput
            //
            this.LevelInput.Location = new System.Drawing.Point(67, 131);
            this.LevelInput.Name = "LevelInput";
            this.LevelInput.Size = new System.Drawing.Size(43, 20);
            this.LevelInput.TabIndex = 3;
            this.LevelInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // Age
            //
            this.Age.AutoSize = true;
            this.Age.Location = new System.Drawing.Point(131, 134);
            this.Age.Name = "Age";
            this.Age.Size = new System.Drawing.Size(28, 13);
            this.Age.TabIndex = 45;
            this.Age.Text = "Alter";
            //
            // AgeInput
            //
            this.AgeInput.Location = new System.Drawing.Point(167, 131);
            this.AgeInput.Name = "AgeInput";
            this.AgeInput.Size = new System.Drawing.Size(43, 20);
            this.AgeInput.TabIndex = 4;
            this.AgeInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // LastName
            //
            this.LastName.AutoSize = true;
            this.LastName.Location = new System.Drawing.Point(7, 21);
            this.LastName.Name = "LastName";
            this.LastName.Size = new System.Drawing.Size(59, 13);
            this.LastName.TabIndex = 43;
            this.LastName.Text = "Nachname";
            //
            // LastNameInput
            //
            this.LastNameInput.Location = new System.Drawing.Point(10, 37);
            this.LastNameInput.Name = "LastNameInput";
            this.LastNameInput.Size = new System.Drawing.Size(200, 20);
            this.LastNameInput.TabIndex = 1;
            //
            // FirstNameLabel
            //
            this.FirstNameLabel.AutoSize = true;
            this.FirstNameLabel.Location = new System.Drawing.Point(6, 70);
            this.FirstNameLabel.Name = "FirstNameLabel";
            this.FirstNameLabel.Size = new System.Drawing.Size(49, 13);
            this.FirstNameLabel.TabIndex = 41;
            this.FirstNameLabel.Text = "Vorname";
            //
            // FirstNameInput
            //
            this.FirstNameInput.Location = new System.Drawing.Point(9, 86);
            this.FirstNameInput.Name = "FirstNameInput";
            this.FirstNameInput.Size = new System.Drawing.Size(200, 20);
            this.FirstNameInput.TabIndex = 2;
            //
            // WealthLabel
            //
            this.WealthLabel.AutoSize = true;
            this.WealthLabel.Location = new System.Drawing.Point(7, 165);
            this.WealthLabel.Name = "WealthLabel";
            this.WealthLabel.Size = new System.Drawing.Size(58, 13);
            this.WealthLabel.TabIndex = 48;
            this.WealthLabel.Text = "Vermögen";
            //
            // WealthInput
            //
            this.WealthInput.Location = new System.Drawing.Point(90, 162);
            this.WealthInput.Name = "WealthInput";
            this.WealthInput.Size = new System.Drawing.Size(120, 20);
            this.WealthInput.TabIndex = 5;
            this.WealthInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // StocksTab
            //
            // Stock boxes (up to Coach.MaxStocks) are built at runtime in CoachView.cs, since their
            // count is a game-data constant, not something the Designer can express statically.
            this.StocksTab.AutoScroll = true;
            this.StocksTab.Location = new System.Drawing.Point(4, 22);
            this.StocksTab.Name = "StocksTab";
            this.StocksTab.Padding = new System.Windows.Forms.Padding(3);
            this.StocksTab.Size = new System.Drawing.Size(586, 346);
            this.StocksTab.TabIndex = 1;
            this.StocksTab.Text = "Aktien";
            this.StocksTab.UseVisualStyleBackColor = true;
            //
            // SaveBtn
            //
            this.SaveBtn.Location = new System.Drawing.Point(500, 391);
            this.SaveBtn.Name = "SaveBtn";
            this.SaveBtn.Size = new System.Drawing.Size(114, 23);
            this.SaveBtn.TabIndex = 44;
            this.SaveBtn.Text = "Speichern";
            this.SaveBtn.UseVisualStyleBackColor = true;
            this.SaveBtn.Click += new System.EventHandler(this.SaveBtn_Click);
            //
            // ReloadBtn
            //
            this.ReloadBtn.Location = new System.Drawing.Point(20, 391);
            this.ReloadBtn.Name = "ReloadBtn";
            this.ReloadBtn.Size = new System.Drawing.Size(114, 23);
            this.ReloadBtn.TabIndex = 45;
            this.ReloadBtn.Text = "Werte neuladen";
            this.ReloadBtn.UseVisualStyleBackColor = true;
            this.ReloadBtn.Click += new System.EventHandler(this.ReloadBtn_Click);
            //
            // CoachView
            //
            this.BackgroundImage = global::A2G_Trainer_XP.Properties.Resources.TabControl;
            this.Controls.Add(this.ReloadBtn);
            this.Controls.Add(this.SaveBtn);
            this.Controls.Add(this.ClubTabControl);
            this.Name = "CoachView";
            this.Size = new System.Drawing.Size(630, 423);
            this.ClubTabControl.ResumeLayout(false);
            this.GeneralTab.ResumeLayout(false);
            this.PersonalBox.ResumeLayout(false);
            this.PersonalBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl ClubTabControl;
        private System.Windows.Forms.TabPage GeneralTab;
        private System.Windows.Forms.Button SaveBtn;
        private System.Windows.Forms.Button ReloadBtn;
        private System.Windows.Forms.GroupBox PersonalBox;
        private System.Windows.Forms.Label LevelLabel;
        private System.Windows.Forms.TextBox LevelInput;
        private System.Windows.Forms.Label Age;
        private System.Windows.Forms.TextBox AgeInput;
        private System.Windows.Forms.Label LastName;
        private System.Windows.Forms.TextBox LastNameInput;
        private System.Windows.Forms.Label FirstNameLabel;
        private System.Windows.Forms.TextBox FirstNameInput;
        private System.Windows.Forms.Label WealthLabel;
        private System.Windows.Forms.TextBox WealthInput;
        private System.Windows.Forms.TabPage StocksTab;
    }
}
