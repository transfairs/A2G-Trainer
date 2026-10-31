
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
            this.ImageBox = new System.Windows.Forms.GroupBox();
            this.ImageInput = new System.Windows.Forms.ComboBox();
            this.ContractBox = new System.Windows.Forms.GroupBox();
            this.NextSeasonCountryLabel = new System.Windows.Forms.Label();
            this.NextSeasonCountryInput = new System.Windows.Forms.ComboBox();
            this.NextSeasonClubLabel = new System.Windows.Forms.Label();
            this.NextSeasonClubInput = new System.Windows.Forms.ComboBox();
            this.CurrentClubLabel = new System.Windows.Forms.Label();
            this.CurrentClubValue = new System.Windows.Forms.Label();
            this.ReleaseClauseInput = new System.Windows.Forms.CheckBox();
            this.ReleaseClausePublishedInput = new System.Windows.Forms.CheckBox();
            this.BoardReleaseInput = new System.Windows.Forms.CheckBox();
            this.JobOfferBox = new System.Windows.Forms.GroupBox();
            this.JobOfferCountryLabel = new System.Windows.Forms.Label();
            this.JobOfferCountryInput = new System.Windows.Forms.ComboBox();
            this.JobOfferClubLabel = new System.Windows.Forms.Label();
            this.JobOfferClubInput = new System.Windows.Forms.ComboBox();
            this.JobOfferButton = new System.Windows.Forms.Button();
            this.JobOfferStatus = new System.Windows.Forms.Label();
            this.ContractTab = new System.Windows.Forms.TabPage();
            this.PayBox = new System.Windows.Forms.GroupBox();
            this.SalaryLabel = new System.Windows.Forms.Label();
            this.SalaryInput = new System.Windows.Forms.TextBox();
            this.PointBonusLabel = new System.Windows.Forms.Label();
            this.PointBonusInput = new System.Windows.Forms.TextBox();
            this.CupRoundBonusLabel = new System.Windows.Forms.Label();
            this.CupRoundBonusInput = new System.Windows.Forms.TextBox();
            this.TitleBonusLabel = new System.Windows.Forms.Label();
            this.TitleBonusInput = new System.Windows.Forms.TextBox();
            this.SeasonGoalLabel = new System.Windows.Forms.Label();
            this.SeasonGoalInput = new System.Windows.Forms.ComboBox();
            this.SeasonGoalBonusLabel = new System.Windows.Forms.Label();
            this.SeasonGoalBonusInput = new System.Windows.Forms.TextBox();
            this.TermsBox = new System.Windows.Forms.GroupBox();
            this.ContractYearsLabel = new System.Windows.Forms.Label();
            this.ContractYearsInput = new System.Windows.Forms.TextBox();
            this.VehiclePerkLabel = new System.Windows.Forms.Label();
            this.VehiclePerkInput = new System.Windows.Forms.ComboBox();
            this.HousingPerkLabel = new System.Windows.Forms.Label();
            this.HousingPerkInput = new System.Windows.Forms.ComboBox();
            this.ExtensionOptionInput = new System.Windows.Forms.CheckBox();
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
            this.ImageBox.SuspendLayout();
            this.ContractBox.SuspendLayout();
            this.JobOfferBox.SuspendLayout();
            this.ContractTab.SuspendLayout();
            this.PayBox.SuspendLayout();
            this.TermsBox.SuspendLayout();
            this.LeagueTab.SuspendLayout();
            this.LeagueBox.SuspendLayout();
            this.SuspendLayout();
            //
            // ClubTabControl
            //
            this.ClubTabControl.Controls.Add(this.GeneralTab);
            this.ClubTabControl.Controls.Add(this.ContractTab);
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
            this.GeneralTab.AutoScroll = true;
            this.GeneralTab.Controls.Add(this.PersonalBox);
            this.GeneralTab.Controls.Add(this.CompetencyBox);
            this.GeneralTab.Controls.Add(this.ImageBox);
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
            // ContractTab
            //
            this.ContractTab.AutoScroll = true;
            this.ContractTab.Controls.Add(this.PayBox);
            this.ContractTab.Controls.Add(this.TermsBox);
            this.ContractTab.Controls.Add(this.ContractBox);
            this.ContractTab.Controls.Add(this.JobOfferBox);
            this.ContractTab.Location = new System.Drawing.Point(4, 22);
            this.ContractTab.Name = "ContractTab";
            this.ContractTab.Padding = new System.Windows.Forms.Padding(3);
            this.ContractTab.Size = new System.Drawing.Size(586, 346);
            this.ContractTab.TabIndex = 3;
            this.ContractTab.Text = "Vertrag";
            this.ContractTab.UseVisualStyleBackColor = true;
            //
            // PayBox
            //
            this.PayBox.Controls.Add(this.SalaryLabel);
            this.PayBox.Controls.Add(this.SalaryInput);
            this.PayBox.Controls.Add(this.PointBonusLabel);
            this.PayBox.Controls.Add(this.PointBonusInput);
            this.PayBox.Controls.Add(this.CupRoundBonusLabel);
            this.PayBox.Controls.Add(this.CupRoundBonusInput);
            this.PayBox.Controls.Add(this.TitleBonusLabel);
            this.PayBox.Controls.Add(this.TitleBonusInput);
            this.PayBox.Controls.Add(this.SeasonGoalLabel);
            this.PayBox.Controls.Add(this.SeasonGoalInput);
            this.PayBox.Controls.Add(this.SeasonGoalBonusLabel);
            this.PayBox.Controls.Add(this.SeasonGoalBonusInput);
            this.PayBox.Location = new System.Drawing.Point(6, 6);
            this.PayBox.Name = "PayBox";
            this.PayBox.Size = new System.Drawing.Size(272, 190);
            this.PayBox.TabIndex = 70;
            this.PayBox.TabStop = false;
            this.PayBox.Text = "Bezüge";
            //
            // SalaryLabel
            //
            this.SalaryLabel.AutoSize = true;
            this.SalaryLabel.Location = new System.Drawing.Point(7, 23);
            this.SalaryLabel.Name = "SalaryLabel";
            this.SalaryLabel.Size = new System.Drawing.Size(118, 13);
            this.SalaryLabel.TabIndex = 71;
            this.SalaryLabel.Text = "Grundgehalt (pro Jahr)";
            //
            // SalaryInput
            //
            this.SalaryInput.Location = new System.Drawing.Point(150, 20);
            this.SalaryInput.Name = "SalaryInput";
            this.SalaryInput.Size = new System.Drawing.Size(112, 20);
            this.SalaryInput.TabIndex = 20;
            this.SalaryInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // PointBonusLabel
            //
            this.PointBonusLabel.AutoSize = true;
            this.PointBonusLabel.Location = new System.Drawing.Point(7, 50);
            this.PointBonusLabel.Name = "PointBonusLabel";
            this.PointBonusLabel.Size = new System.Drawing.Size(66, 13);
            this.PointBonusLabel.TabIndex = 72;
            this.PointBonusLabel.Text = "Punktprämie";
            //
            // PointBonusInput
            //
            this.PointBonusInput.Location = new System.Drawing.Point(150, 47);
            this.PointBonusInput.Name = "PointBonusInput";
            this.PointBonusInput.Size = new System.Drawing.Size(112, 20);
            this.PointBonusInput.TabIndex = 21;
            this.PointBonusInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // CupRoundBonusLabel
            //
            this.CupRoundBonusLabel.AutoSize = true;
            this.CupRoundBonusLabel.Location = new System.Drawing.Point(7, 77);
            this.CupRoundBonusLabel.Name = "CupRoundBonusLabel";
            this.CupRoundBonusLabel.Size = new System.Drawing.Size(97, 13);
            this.CupRoundBonusLabel.TabIndex = 73;
            this.CupRoundBonusLabel.Text = "Pokalrundenprämie";
            //
            // CupRoundBonusInput
            //
            this.CupRoundBonusInput.Location = new System.Drawing.Point(150, 74);
            this.CupRoundBonusInput.Name = "CupRoundBonusInput";
            this.CupRoundBonusInput.Size = new System.Drawing.Size(112, 20);
            this.CupRoundBonusInput.TabIndex = 22;
            this.CupRoundBonusInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // TitleBonusLabel
            //
            this.TitleBonusLabel.AutoSize = true;
            this.TitleBonusLabel.Location = new System.Drawing.Point(7, 104);
            this.TitleBonusLabel.Name = "TitleBonusLabel";
            this.TitleBonusLabel.Size = new System.Drawing.Size(117, 13);
            this.TitleBonusLabel.TabIndex = 74;
            this.TitleBonusLabel.Text = "Titel-/Aufstiegsprämie";
            //
            // TitleBonusInput
            //
            this.TitleBonusInput.Location = new System.Drawing.Point(150, 101);
            this.TitleBonusInput.Name = "TitleBonusInput";
            this.TitleBonusInput.Size = new System.Drawing.Size(112, 20);
            this.TitleBonusInput.TabIndex = 23;
            this.TitleBonusInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // SeasonGoalLabel
            //
            this.SeasonGoalLabel.AutoSize = true;
            this.SeasonGoalLabel.Location = new System.Drawing.Point(7, 131);
            this.SeasonGoalLabel.Name = "SeasonGoalLabel";
            this.SeasonGoalLabel.Size = new System.Drawing.Size(61, 13);
            this.SeasonGoalLabel.TabIndex = 75;
            this.SeasonGoalLabel.Text = "Saisonziel";
            //
            // SeasonGoalInput
            //
            this.SeasonGoalInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.SeasonGoalInput.Location = new System.Drawing.Point(150, 128);
            this.SeasonGoalInput.Name = "SeasonGoalInput";
            this.SeasonGoalInput.Size = new System.Drawing.Size(112, 21);
            this.SeasonGoalInput.TabIndex = 24;
            //
            // SeasonGoalBonusLabel
            //
            this.SeasonGoalBonusLabel.AutoSize = true;
            this.SeasonGoalBonusLabel.Location = new System.Drawing.Point(7, 158);
            this.SeasonGoalBonusLabel.Name = "SeasonGoalBonusLabel";
            this.SeasonGoalBonusLabel.Size = new System.Drawing.Size(132, 13);
            this.SeasonGoalBonusLabel.TabIndex = 76;
            this.SeasonGoalBonusLabel.Text = "Prämie für das Saisonziel";
            //
            // SeasonGoalBonusInput
            //
            this.SeasonGoalBonusInput.Location = new System.Drawing.Point(150, 155);
            this.SeasonGoalBonusInput.Name = "SeasonGoalBonusInput";
            this.SeasonGoalBonusInput.Size = new System.Drawing.Size(112, 20);
            this.SeasonGoalBonusInput.TabIndex = 25;
            this.SeasonGoalBonusInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // TermsBox
            //
            this.TermsBox.Controls.Add(this.ContractYearsLabel);
            this.TermsBox.Controls.Add(this.ContractYearsInput);
            this.TermsBox.Controls.Add(this.VehiclePerkLabel);
            this.TermsBox.Controls.Add(this.VehiclePerkInput);
            this.TermsBox.Controls.Add(this.HousingPerkLabel);
            this.TermsBox.Controls.Add(this.HousingPerkInput);
            this.TermsBox.Controls.Add(this.ExtensionOptionInput);
            this.TermsBox.Location = new System.Drawing.Point(284, 6);
            this.TermsBox.Name = "TermsBox";
            this.TermsBox.Size = new System.Drawing.Size(272, 190);
            this.TermsBox.TabIndex = 77;
            this.TermsBox.TabStop = false;
            this.TermsBox.Text = "Laufzeit und Extras";
            //
            // ContractYearsLabel
            //
            this.ContractYearsLabel.AutoSize = true;
            this.ContractYearsLabel.Location = new System.Drawing.Point(7, 23);
            this.ContractYearsLabel.Name = "ContractYearsLabel";
            this.ContractYearsLabel.Size = new System.Drawing.Size(117, 13);
            this.ContractYearsLabel.TabIndex = 78;
            this.ContractYearsLabel.Text = "Restlaufzeit (in Jahren)";
            //
            // ContractYearsInput
            //
            this.ContractYearsInput.Location = new System.Drawing.Point(150, 20);
            this.ContractYearsInput.Name = "ContractYearsInput";
            this.ContractYearsInput.Size = new System.Drawing.Size(43, 20);
            this.ContractYearsInput.TabIndex = 26;
            this.ContractYearsInput.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            //
            // VehiclePerkLabel
            //
            this.VehiclePerkLabel.AutoSize = true;
            this.VehiclePerkLabel.Location = new System.Drawing.Point(7, 50);
            this.VehiclePerkLabel.Name = "VehiclePerkLabel";
            this.VehiclePerkLabel.Size = new System.Drawing.Size(51, 13);
            this.VehiclePerkLabel.TabIndex = 79;
            this.VehiclePerkLabel.Text = "Fahrzeug";
            //
            // VehiclePerkInput
            //
            this.VehiclePerkInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.VehiclePerkInput.Location = new System.Drawing.Point(110, 47);
            this.VehiclePerkInput.Name = "VehiclePerkInput";
            this.VehiclePerkInput.Size = new System.Drawing.Size(152, 21);
            this.VehiclePerkInput.TabIndex = 27;
            //
            // HousingPerkLabel
            //
            this.HousingPerkLabel.AutoSize = true;
            this.HousingPerkLabel.Location = new System.Drawing.Point(7, 77);
            this.HousingPerkLabel.Name = "HousingPerkLabel";
            this.HousingPerkLabel.Size = new System.Drawing.Size(52, 13);
            this.HousingPerkLabel.TabIndex = 80;
            this.HousingPerkLabel.Text = "Wohnung";
            //
            // HousingPerkInput
            //
            this.HousingPerkInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.HousingPerkInput.Location = new System.Drawing.Point(110, 74);
            this.HousingPerkInput.Name = "HousingPerkInput";
            this.HousingPerkInput.Size = new System.Drawing.Size(152, 21);
            this.HousingPerkInput.TabIndex = 28;
            //
            // ExtensionOptionInput
            //
            this.ExtensionOptionInput.AutoSize = true;
            this.ExtensionOptionInput.Location = new System.Drawing.Point(10, 104);
            this.ExtensionOptionInput.Name = "ExtensionOptionInput";
            this.ExtensionOptionInput.Size = new System.Drawing.Size(212, 17);
            this.ExtensionOptionInput.TabIndex = 29;
            this.ExtensionOptionInput.Text = "Verlängerungsoption für den Verein";
            this.ExtensionOptionInput.UseVisualStyleBackColor = true;
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
            // ImageBox
            //
            this.ImageBox.Controls.Add(this.ImageInput);
            this.ImageBox.Location = new System.Drawing.Point(234, 262);
            this.ImageBox.Name = "ImageBox";
            this.ImageBox.Size = new System.Drawing.Size(240, 52);
            this.ImageBox.TabIndex = 58;
            this.ImageBox.TabStop = false;
            this.ImageBox.Text = "Image";
            //
            // ImageInput
            //
            this.ImageInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.ImageInput.Location = new System.Drawing.Point(10, 20);
            this.ImageInput.Name = "ImageInput";
            this.ImageInput.Size = new System.Drawing.Size(220, 21);
            this.ImageInput.TabIndex = 0;
            //
            // ContractBox
            //
            this.ContractBox.Controls.Add(this.CurrentClubLabel);
            this.ContractBox.Controls.Add(this.CurrentClubValue);
            this.ContractBox.Controls.Add(this.NextSeasonCountryLabel);
            this.ContractBox.Controls.Add(this.NextSeasonCountryInput);
            this.ContractBox.Controls.Add(this.NextSeasonClubLabel);
            this.ContractBox.Controls.Add(this.NextSeasonClubInput);
            this.ContractBox.Controls.Add(this.ReleaseClauseInput);
            this.ContractBox.Controls.Add(this.ReleaseClausePublishedInput);
            this.ContractBox.Controls.Add(this.BoardReleaseInput);
            this.ContractBox.Location = new System.Drawing.Point(6, 202);
            this.ContractBox.Name = "ContractBox";
            this.ContractBox.Size = new System.Drawing.Size(272, 155);
            this.ContractBox.TabIndex = 58;
            this.ContractBox.TabStop = false;
            this.ContractBox.Text = "Verein und Freigabe";
            //
            // CurrentClubLabel
            //
            this.CurrentClubLabel.AutoSize = true;
            this.CurrentClubLabel.Location = new System.Drawing.Point(7, 23);
            this.CurrentClubLabel.Name = "CurrentClubLabel";
            this.CurrentClubLabel.Size = new System.Drawing.Size(88, 13);
            this.CurrentClubLabel.TabIndex = 62;
            this.CurrentClubLabel.Text = "Aktueller Verein";
            //
            // CurrentClubValue
            //
            this.CurrentClubValue.AutoEllipsis = true;
            this.CurrentClubValue.Location = new System.Drawing.Point(115, 23);
            this.CurrentClubValue.Name = "CurrentClubValue";
            this.CurrentClubValue.Size = new System.Drawing.Size(150, 13);
            this.CurrentClubValue.TabIndex = 63;
            this.CurrentClubValue.Text = "-";
            //
            // NextSeasonCountryLabel
            //
            this.NextSeasonCountryLabel.AutoSize = true;
            this.NextSeasonCountryLabel.Location = new System.Drawing.Point(7, 50);
            this.NextSeasonCountryLabel.Name = "NextSeasonCountryLabel";
            this.NextSeasonCountryLabel.Size = new System.Drawing.Size(97, 13);
            this.NextSeasonCountryLabel.TabIndex = 59;
            this.NextSeasonCountryLabel.Text = "Land (n. Saison)";
            //
            // NextSeasonCountryInput
            //
            this.NextSeasonCountryInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.NextSeasonCountryInput.Location = new System.Drawing.Point(115, 47);
            this.NextSeasonCountryInput.Name = "NextSeasonCountryInput";
            this.NextSeasonCountryInput.Size = new System.Drawing.Size(150, 21);
            this.NextSeasonCountryInput.TabIndex = 10;
            //
            // NextSeasonClubLabel
            //
            this.NextSeasonClubLabel.AutoSize = true;
            this.NextSeasonClubLabel.Location = new System.Drawing.Point(7, 77);
            this.NextSeasonClubLabel.Name = "NextSeasonClubLabel";
            this.NextSeasonClubLabel.Size = new System.Drawing.Size(102, 13);
            this.NextSeasonClubLabel.TabIndex = 60;
            this.NextSeasonClubLabel.Text = "Verein (n. Saison)";
            //
            // NextSeasonClubInput
            //
            this.NextSeasonClubInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.NextSeasonClubInput.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.NextSeasonClubInput.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.NextSeasonClubInput.Location = new System.Drawing.Point(115, 74);
            this.NextSeasonClubInput.Name = "NextSeasonClubInput";
            this.NextSeasonClubInput.Size = new System.Drawing.Size(150, 21);
            this.NextSeasonClubInput.TabIndex = 11;
            //
            // ReleaseClauseInput
            //
            this.ReleaseClauseInput.AutoSize = true;
            this.ReleaseClauseInput.Location = new System.Drawing.Point(10, 105);
            this.ReleaseClauseInput.Name = "ReleaseClauseInput";
            this.ReleaseClauseInput.Size = new System.Drawing.Size(101, 17);
            this.ReleaseClauseInput.TabIndex = 12;
            this.ReleaseClauseInput.Text = "Freigabeklausel";
            this.ReleaseClauseInput.UseVisualStyleBackColor = true;
            //
            // ReleaseClausePublishedInput
            //
            this.ReleaseClausePublishedInput.AutoSize = true;
            this.ReleaseClausePublishedInput.Location = new System.Drawing.Point(118, 105);
            this.ReleaseClausePublishedInput.Name = "ReleaseClausePublishedInput";
            this.ReleaseClausePublishedInput.Size = new System.Drawing.Size(99, 17);
            this.ReleaseClausePublishedInput.TabIndex = 13;
            this.ReleaseClausePublishedInput.Text = "bekanntgegeben";
            this.ReleaseClausePublishedInput.UseVisualStyleBackColor = true;
            //
            // BoardReleaseInput
            //
            this.BoardReleaseInput.AutoSize = true;
            this.BoardReleaseInput.Location = new System.Drawing.Point(10, 128);
            this.BoardReleaseInput.Name = "BoardReleaseInput";
            this.BoardReleaseInput.Size = new System.Drawing.Size(141, 17);
            this.BoardReleaseInput.TabIndex = 17;
            this.BoardReleaseInput.Text = "Freigabe durch Vorstand";
            this.BoardReleaseInput.UseVisualStyleBackColor = true;
            //
            // JobOfferBox
            //
            this.JobOfferBox.Controls.Add(this.JobOfferCountryLabel);
            this.JobOfferBox.Controls.Add(this.JobOfferCountryInput);
            this.JobOfferBox.Controls.Add(this.JobOfferClubLabel);
            this.JobOfferBox.Controls.Add(this.JobOfferClubInput);
            this.JobOfferBox.Controls.Add(this.JobOfferButton);
            this.JobOfferBox.Controls.Add(this.JobOfferStatus);
            this.JobOfferBox.Location = new System.Drawing.Point(284, 202);
            this.JobOfferBox.Name = "JobOfferBox";
            this.JobOfferBox.Size = new System.Drawing.Size(272, 155);
            this.JobOfferBox.TabIndex = 64;
            this.JobOfferBox.TabStop = false;
            this.JobOfferBox.Text = "Jobangebot anfordern";
            //
            // JobOfferCountryLabel
            //
            this.JobOfferCountryLabel.AutoSize = true;
            this.JobOfferCountryLabel.Location = new System.Drawing.Point(7, 23);
            this.JobOfferCountryLabel.Name = "JobOfferCountryLabel";
            this.JobOfferCountryLabel.Size = new System.Drawing.Size(31, 13);
            this.JobOfferCountryLabel.TabIndex = 65;
            this.JobOfferCountryLabel.Text = "Land";
            //
            // JobOfferCountryInput
            //
            this.JobOfferCountryInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.JobOfferCountryInput.Location = new System.Drawing.Point(115, 20);
            this.JobOfferCountryInput.Name = "JobOfferCountryInput";
            this.JobOfferCountryInput.Size = new System.Drawing.Size(150, 21);
            this.JobOfferCountryInput.TabIndex = 14;
            //
            // JobOfferClubLabel
            //
            this.JobOfferClubLabel.AutoSize = true;
            this.JobOfferClubLabel.Location = new System.Drawing.Point(7, 50);
            this.JobOfferClubLabel.Name = "JobOfferClubLabel";
            this.JobOfferClubLabel.Size = new System.Drawing.Size(39, 13);
            this.JobOfferClubLabel.TabIndex = 66;
            this.JobOfferClubLabel.Text = "Verein";
            //
            // JobOfferClubInput
            //
            this.JobOfferClubInput.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDown;
            this.JobOfferClubInput.AutoCompleteMode = System.Windows.Forms.AutoCompleteMode.SuggestAppend;
            this.JobOfferClubInput.AutoCompleteSource = System.Windows.Forms.AutoCompleteSource.ListItems;
            this.JobOfferClubInput.Location = new System.Drawing.Point(115, 47);
            this.JobOfferClubInput.Name = "JobOfferClubInput";
            this.JobOfferClubInput.Size = new System.Drawing.Size(150, 21);
            this.JobOfferClubInput.TabIndex = 15;
            //
            // JobOfferButton
            //
            this.JobOfferButton.Location = new System.Drawing.Point(7, 75);
            this.JobOfferButton.Name = "JobOfferButton";
            this.JobOfferButton.Size = new System.Drawing.Size(258, 23);
            this.JobOfferButton.TabIndex = 16;
            this.JobOfferButton.Text = "Beim nächsten Tagesabschluss anbieten";
            this.JobOfferButton.UseVisualStyleBackColor = true;
            //
            // JobOfferStatus
            //
            this.JobOfferStatus.Location = new System.Drawing.Point(7, 102);
            this.JobOfferStatus.Name = "JobOfferStatus";
            this.JobOfferStatus.Size = new System.Drawing.Size(258, 47);
            this.JobOfferStatus.TabIndex = 67;
            this.JobOfferStatus.Text = "";
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
            this.LeagueBox.Size = new System.Drawing.Size(574, 52);
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
            this.AdditionalCountriesBox.Location = new System.Drawing.Point(6, 64);
            this.AdditionalCountriesBox.Name = "AdditionalCountriesBox";
            this.AdditionalCountriesBox.Size = new System.Drawing.Size(574, 136);
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
            this.ImageBox.ResumeLayout(false);
            this.ContractBox.ResumeLayout(false);
            this.ContractBox.PerformLayout();
            this.JobOfferBox.ResumeLayout(false);
            this.JobOfferBox.PerformLayout();
            this.ContractTab.ResumeLayout(false);
            this.PayBox.ResumeLayout(false);
            this.PayBox.PerformLayout();
            this.TermsBox.ResumeLayout(false);
            this.TermsBox.PerformLayout();
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
        private System.Windows.Forms.GroupBox ImageBox;
        private System.Windows.Forms.ComboBox ImageInput;
        private System.Windows.Forms.GroupBox ContractBox;
        private System.Windows.Forms.Label NextSeasonCountryLabel;
        private System.Windows.Forms.ComboBox NextSeasonCountryInput;
        private System.Windows.Forms.Label NextSeasonClubLabel;
        private System.Windows.Forms.ComboBox NextSeasonClubInput;
        private System.Windows.Forms.Label CurrentClubLabel;
        private System.Windows.Forms.Label CurrentClubValue;
        private System.Windows.Forms.CheckBox ReleaseClauseInput;
        private System.Windows.Forms.CheckBox ReleaseClausePublishedInput;
        private System.Windows.Forms.CheckBox BoardReleaseInput;
        private System.Windows.Forms.GroupBox JobOfferBox;
        private System.Windows.Forms.Label JobOfferCountryLabel;
        private System.Windows.Forms.ComboBox JobOfferCountryInput;
        private System.Windows.Forms.Label JobOfferClubLabel;
        private System.Windows.Forms.ComboBox JobOfferClubInput;
        private System.Windows.Forms.Button JobOfferButton;
        private System.Windows.Forms.Label JobOfferStatus;
        private System.Windows.Forms.TabPage ContractTab;
        private System.Windows.Forms.GroupBox PayBox;
        private System.Windows.Forms.Label SalaryLabel;
        private System.Windows.Forms.TextBox SalaryInput;
        private System.Windows.Forms.Label PointBonusLabel;
        private System.Windows.Forms.TextBox PointBonusInput;
        private System.Windows.Forms.Label CupRoundBonusLabel;
        private System.Windows.Forms.TextBox CupRoundBonusInput;
        private System.Windows.Forms.Label TitleBonusLabel;
        private System.Windows.Forms.TextBox TitleBonusInput;
        private System.Windows.Forms.Label SeasonGoalLabel;
        private System.Windows.Forms.ComboBox SeasonGoalInput;
        private System.Windows.Forms.Label SeasonGoalBonusLabel;
        private System.Windows.Forms.TextBox SeasonGoalBonusInput;
        private System.Windows.Forms.GroupBox TermsBox;
        private System.Windows.Forms.Label ContractYearsLabel;
        private System.Windows.Forms.TextBox ContractYearsInput;
        private System.Windows.Forms.Label VehiclePerkLabel;
        private System.Windows.Forms.ComboBox VehiclePerkInput;
        private System.Windows.Forms.Label HousingPerkLabel;
        private System.Windows.Forms.ComboBox HousingPerkInput;
        private System.Windows.Forms.CheckBox ExtensionOptionInput;
        private System.Windows.Forms.TabPage StocksTab;
        private System.Windows.Forms.TabPage LeagueTab;
        private System.Windows.Forms.GroupBox LeagueBox;
        private System.Windows.Forms.Label MainCountryLabel;
        private System.Windows.Forms.ComboBox MainCountryInput;
        private System.Windows.Forms.GroupBox AdditionalCountriesBox;
    }
}
