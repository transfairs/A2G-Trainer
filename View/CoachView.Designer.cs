
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
            this.Age = new System.Windows.Forms.Label();
            this.AgeInput = new System.Windows.Forms.TextBox();
            this.LastName = new System.Windows.Forms.Label();
            this.LastNameInput = new System.Windows.Forms.TextBox();
            this.FirstNameLabel = new System.Windows.Forms.Label();
            this.FirstNameInput = new System.Windows.Forms.TextBox();
            this.DifficultyLabel = new System.Windows.Forms.Label();
            this.DifficultyInput = new System.Windows.Forms.ComboBox();
            this.GamesLabel = new System.Windows.Forms.Label();
            this.GamesInput = new System.Windows.Forms.TextBox();
            this.WinsLabel = new System.Windows.Forms.Label();
            this.WinsInput = new System.Windows.Forms.TextBox();
            this.WinPercentageLabel = new System.Windows.Forms.Label();
            this.WinPercentageValue = new System.Windows.Forms.Label();
            this.CompetencyBox = new System.Windows.Forms.GroupBox();
            this.StocksTab = new System.Windows.Forms.TabPage();
            this.LeagueTab = new System.Windows.Forms.TabPage();
            this.LeagueBox = new System.Windows.Forms.GroupBox();
            this.MainCountryLabel = new System.Windows.Forms.Label();
            this.MainCountryInput = new System.Windows.Forms.ComboBox();
            this.AdditionalCountriesBox = new System.Windows.Forms.GroupBox();
            this.SaveBtn = new System.Windows.Forms.Button();
            this.ReloadBtn = new System.Windows.Forms.Button();
            this.ClubTabControl.SuspendLayout();
            this.GeneralTab.SuspendLayout();
            this.PersonalBox.SuspendLayout();
            this.CompetencyBox.SuspendLayout();
            this.LeagueTab.SuspendLayout();
            this.LeagueBox.SuspendLayout();
            this.SuspendLayout();
            //
            // ClubTabControl
            //
            this.ClubTabControl.Controls.Add(this.GeneralTab);
            this.ClubTabControl.Controls.Add(this.StocksTab);
            this.ClubTabControl.Controls.Add(this.LeagueTab);
            this.ClubTabControl.Location = new System.Drawing.Point(20, 13);
            this.ClubTabControl.Name = "ClubTabControl";
            this.ClubTabControl.SelectedIndex = 0;
            this.ClubTabControl.Size = new System.Drawing.Size(594, 372);
            this.ClubTabControl.TabIndex = 0;
            //
            // GeneralTab
            //
            this.GeneralTab.Controls.Add(this.PersonalBox);
            this.GeneralTab.Controls.Add(this.CompetencyBox);
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
            this.PersonalBox.Controls.Add(this.Age);
            this.PersonalBox.Controls.Add(this.AgeInput);
            this.PersonalBox.Controls.Add(this.LastName);
            this.PersonalBox.Controls.Add(this.LastNameInput);
            this.PersonalBox.Controls.Add(this.FirstNameLabel);
            this.PersonalBox.Controls.Add(this.FirstNameInput);
            this.PersonalBox.Controls.Add(this.DifficultyLabel);
            this.PersonalBox.Controls.Add(this.DifficultyInput);
            this.PersonalBox.Controls.Add(this.GamesLabel);
            this.PersonalBox.Controls.Add(this.GamesInput);
            this.PersonalBox.Controls.Add(this.WinsLabel);
            this.PersonalBox.Controls.Add(this.WinsInput);
            this.PersonalBox.Controls.Add(this.WinPercentageLabel);
            this.PersonalBox.Controls.Add(this.WinPercentageValue);
            this.PersonalBox.Location = new System.Drawing.Point(6, 6);
            this.PersonalBox.Name = "PersonalBox";
            this.PersonalBox.Size = new System.Drawing.Size(220, 318);
            this.PersonalBox.TabIndex = 41;
            this.PersonalBox.TabStop = false;
            this.PersonalBox.Text = "Persönliche Daten";
            //
            // Age
            //
            this.Age.AutoSize = true;
            this.Age.Location = new System.Drawing.Point(7, 134);
            this.Age.Name = "Age";
            this.Age.Size = new System.Drawing.Size(28, 13);
            this.Age.TabIndex = 45;
            this.Age.Text = "Alter";
            //
            // AgeInput
            //
            this.AgeInput.Location = new System.Drawing.Point(90, 131);
            this.AgeInput.Name = "AgeInput";
            this.AgeInput.Size = new System.Drawing.Size(43, 20);
            this.AgeInput.TabIndex = 3;
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
            // DifficultyLabel
            //
            this.DifficultyLabel.AutoSize = true;
            this.DifficultyLabel.Location = new System.Drawing.Point(7, 196);
            this.DifficultyLabel.Name = "DifficultyLabel";
            this.DifficultyLabel.Size = new System.Drawing.Size(80, 13);
            this.DifficultyLabel.TabIndex = 49;
            this.DifficultyLabel.Text = "Schwierigkeit";
            //
            // DifficultyInput
            //
            this.DifficultyInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.DifficultyInput.Location = new System.Drawing.Point(90, 193);
            this.DifficultyInput.Name = "DifficultyInput";
            this.DifficultyInput.Size = new System.Drawing.Size(120, 21);
            this.DifficultyInput.TabIndex = 6;
            //
            // GamesLabel
            //
            this.GamesLabel.AutoSize = true;
            this.GamesLabel.Location = new System.Drawing.Point(7, 227);
            this.GamesLabel.Name = "GamesLabel";
            this.GamesLabel.Size = new System.Drawing.Size(36, 13);
            this.GamesLabel.TabIndex = 54;
            this.GamesLabel.Text = "Spiele";
            //
            // GamesInput
            //
            this.GamesInput.Location = new System.Drawing.Point(90, 224);
            this.GamesInput.Name = "GamesInput";
            this.GamesInput.Size = new System.Drawing.Size(43, 20);
            this.GamesInput.TabIndex = 8;
            this.GamesInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // WinsLabel
            //
            this.WinsLabel.AutoSize = true;
            this.WinsLabel.Location = new System.Drawing.Point(7, 258);
            this.WinsLabel.Name = "WinsLabel";
            this.WinsLabel.Size = new System.Drawing.Size(33, 13);
            this.WinsLabel.TabIndex = 55;
            this.WinsLabel.Text = "Siege";
            //
            // WinsInput
            //
            this.WinsInput.Location = new System.Drawing.Point(90, 255);
            this.WinsInput.Name = "WinsInput";
            this.WinsInput.Size = new System.Drawing.Size(43, 20);
            this.WinsInput.TabIndex = 9;
            this.WinsInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // WinPercentageLabel
            //
            this.WinPercentageLabel.AutoSize = true;
            this.WinPercentageLabel.Location = new System.Drawing.Point(7, 289);
            this.WinPercentageLabel.Name = "WinPercentageLabel";
            this.WinPercentageLabel.Size = new System.Drawing.Size(56, 13);
            this.WinPercentageLabel.TabIndex = 56;
            this.WinPercentageLabel.Text = "Siegquote";
            //
            // WinPercentageValue
            //
            this.WinPercentageValue.AutoSize = true;
            this.WinPercentageValue.Location = new System.Drawing.Point(90, 289);
            this.WinPercentageValue.Name = "WinPercentageValue";
            this.WinPercentageValue.Size = new System.Drawing.Size(30, 13);
            this.WinPercentageValue.TabIndex = 57;
            this.WinPercentageValue.Text = "0 %";
            //
            // StocksTab
            //
            this.StocksTab.AutoScroll = true;
            this.StocksTab.Location = new System.Drawing.Point(4, 22);
            this.StocksTab.Name = "StocksTab";
            this.StocksTab.Padding = new System.Windows.Forms.Padding(3);
            this.StocksTab.Size = new System.Drawing.Size(586, 346);
            this.StocksTab.TabIndex = 1;
            this.StocksTab.Text = "Aktien";
            this.StocksTab.UseVisualStyleBackColor = true;
            //
            // CompetencyBox
            //
            // Children (level selector, the six competency fields, and the points-total label)
            // are built dynamically in CoachView.BuildCompetencyBox, same as StocksTab's contents -
            // the six-competency table is a game-data constant, not fixed UI.
            this.CompetencyBox.Location = new System.Drawing.Point(234, 6);
            this.CompetencyBox.Name = "CompetencyBox";
            this.CompetencyBox.Size = new System.Drawing.Size(240, 250);
            this.CompetencyBox.TabIndex = 53;
            this.CompetencyBox.TabStop = false;
            this.CompetencyBox.Text = "Kompetenzen";
            //
            // LeagueTab
            //
            this.LeagueTab.Controls.Add(this.LeagueBox);
            this.LeagueTab.Controls.Add(this.AdditionalCountriesBox);
            this.LeagueTab.Location = new System.Drawing.Point(4, 22);
            this.LeagueTab.Name = "LeagueTab";
            this.LeagueTab.Padding = new System.Windows.Forms.Padding(3);
            this.LeagueTab.Size = new System.Drawing.Size(586, 346);
            this.LeagueTab.TabIndex = 2;
            this.LeagueTab.Text = "Länderauswahl";
            this.LeagueTab.UseVisualStyleBackColor = true;
            //
            // LeagueBox
            //
            this.LeagueBox.Controls.Add(this.MainCountryLabel);
            this.LeagueBox.Controls.Add(this.MainCountryInput);
            this.LeagueBox.Location = new System.Drawing.Point(6, 6);
            this.LeagueBox.Name = "LeagueBox";
            this.LeagueBox.Size = new System.Drawing.Size(220, 60);
            this.LeagueBox.TabIndex = 50;
            this.LeagueBox.TabStop = false;
            this.LeagueBox.Text = "Hauptland";
            //
            // MainCountryLabel
            //
            this.MainCountryLabel.AutoSize = true;
            this.MainCountryLabel.Location = new System.Drawing.Point(7, 24);
            this.MainCountryLabel.Name = "MainCountryLabel";
            this.MainCountryLabel.Size = new System.Drawing.Size(31, 13);
            this.MainCountryLabel.TabIndex = 51;
            this.MainCountryLabel.Text = "Land";
            //
            // MainCountryInput
            //
            this.MainCountryInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.MainCountryInput.Location = new System.Drawing.Point(90, 21);
            this.MainCountryInput.Name = "MainCountryInput";
            this.MainCountryInput.Size = new System.Drawing.Size(120, 21);
            this.MainCountryInput.TabIndex = 7;
            //
            // AdditionalCountriesBox
            //
            this.AdditionalCountriesBox.Location = new System.Drawing.Point(6, 72);
            this.AdditionalCountriesBox.Name = "AdditionalCountriesBox";
            this.AdditionalCountriesBox.Size = new System.Drawing.Size(220, 160);
            this.AdditionalCountriesBox.TabIndex = 52;
            this.AdditionalCountriesBox.TabStop = false;
            this.AdditionalCountriesBox.Text = "Bonusländer";
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
            this.CompetencyBox.ResumeLayout(false);
            this.CompetencyBox.PerformLayout();
            this.LeagueTab.ResumeLayout(false);
            this.LeagueBox.ResumeLayout(false);
            this.LeagueBox.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl ClubTabControl;
        private System.Windows.Forms.TabPage GeneralTab;
        private System.Windows.Forms.Button SaveBtn;
        private System.Windows.Forms.Button ReloadBtn;
        private System.Windows.Forms.GroupBox PersonalBox;
        private System.Windows.Forms.Label Age;
        private System.Windows.Forms.TextBox AgeInput;
        private System.Windows.Forms.Label LastName;
        private System.Windows.Forms.TextBox LastNameInput;
        private System.Windows.Forms.Label FirstNameLabel;
        private System.Windows.Forms.TextBox FirstNameInput;
        private System.Windows.Forms.Label WealthLabel;
        private System.Windows.Forms.TextBox WealthInput;
        private System.Windows.Forms.Label DifficultyLabel;
        private System.Windows.Forms.ComboBox DifficultyInput;
        private System.Windows.Forms.Label GamesLabel;
        private System.Windows.Forms.TextBox GamesInput;
        private System.Windows.Forms.Label WinsLabel;
        private System.Windows.Forms.TextBox WinsInput;
        private System.Windows.Forms.Label WinPercentageLabel;
        private System.Windows.Forms.Label WinPercentageValue;
        private System.Windows.Forms.GroupBox CompetencyBox;
        private System.Windows.Forms.TabPage StocksTab;
        private System.Windows.Forms.TabPage LeagueTab;
        private System.Windows.Forms.GroupBox LeagueBox;
        private System.Windows.Forms.Label MainCountryLabel;
        private System.Windows.Forms.ComboBox MainCountryInput;
        private System.Windows.Forms.GroupBox AdditionalCountriesBox;
    }
}
