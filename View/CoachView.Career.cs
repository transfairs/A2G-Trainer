using A2G_Trainer.Model;
using A2G_Trainer.Controller;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// "Karriere" tab: how long the manager plays, the Managerfortbildung,
    /// the titles - and the difficulty lock next to the difficulty. Built in code,
    /// like the friendlies.
    /// </summary>
    public partial class CoachView
    {
        // Counters per column of the titles box (indices into CoachAchievements.Names).
        private static readonly int[][] AchievementColumns = { new[] { 0, 1, 4, 5, 6, 8 }, new[] { 2, 3, 7, 9, 10, 11 } };

        private TabPage careerTab;
        private ComboBox careerModeInput, trainingResultInput;
        private TextBox careerStartInput, retirementAgeInput;
        private Label careerEndValue;
        private CheckBox unlockDifficultyInput, withDismissalsInput;
        private readonly TextBox[] achievementInputs = new TextBox[CoachAchievements.Names.Length];
        private GroupBox achievementsBox;
        // Set while the career controls are filled from the coach, so their handlers don't write back.
        private bool showingCareer;

        private static Label AddCareerLabel(Control box, string text, int x, int y)
        {
            Label label = new Label { AutoSize = true, Location = new Point(x, y + 3), Text = text };
            box.Controls.Add(label);
            return label;
        }

        private static TextBox AddCareerInput(Control box, int x, int y, int width = 45)
        {
            TextBox input = new TextBox { Location = new Point(x, y), Size = new Size(width, 20), TextAlign = HorizontalAlignment.Right };
            box.Controls.Add(input);
            return input;
        }

        private void BuildCareerTab()
        {
            // The difficulty's lock sits right under the difficulty; the rows below move down for it.
            this.unlockDifficultyInput = new CheckBox { Text = "Sperre aufheben", Location = new Point(90, 218), AutoSize = true };
            this.PersonalBox.Controls.Add(this.unlockDifficultyInput);

            this.withDismissalsInput = new CheckBox { Text = "Mit Entlassungen", Location = new Point(240, 23), AutoSize = true };
            this.LeagueBox.Controls.Add(this.withDismissalsInput);

            this.careerTab = new TabPage { Text = "Karriere", UseVisualStyleBackColor = true, AutoScroll = true, Padding = new Padding(3) };

            GroupBox durationBox = new GroupBox { Text = "Spieldauer", Location = new Point(6, 6), Size = new Size(248, 116) };
            AddCareerLabel(durationBox, "Modus", 7, 18);
            this.careerModeInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(95, 18), Size = new Size(145, 21) };
            durationBox.Controls.Add(this.careerModeInput);
            AddCareerLabel(durationBox, "Beginn (Jahr)", 7, 42);
            this.careerStartInput = AddCareerInput(durationBox, 95, 42, 50);
            AddCareerLabel(durationBox, "Letzte Saison", 7, 66);
            this.careerEndValue = new Label { AutoSize = true, Location = new Point(95, 69), Text = "-" };
            durationBox.Controls.Add(this.careerEndValue);
            AddCareerLabel(durationBox, "Rentenalter", 7, 90);
            this.retirementAgeInput = AddCareerInput(durationBox, 95, 90, 50);

            GroupBox trainingBox = new GroupBox { Text = "Fortbildung", Location = new Point(6, 126), Size = new Size(248, 46) };
            AddCareerLabel(trainingBox, "Ergebnis", 7, 18);
            this.trainingResultInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(95, 18), Size = new Size(145, 21) };
            trainingBox.Controls.Add(this.trainingResultInput);

            this.achievementsBox = new GroupBox { Text = "Erfolge", Location = new Point(260, 6), Size = new Size(320, 182) };
            for (int column = 0; column < AchievementColumns.Length; column++)
            {
                for (int row = 0; row < AchievementColumns[column].Length; row++)
                {
                    int index = AchievementColumns[column][row];
                    int y = 20 + row * 26;
                    AddCareerLabel(this.achievementsBox, CoachAchievements.Names[index], column == 0 ? 7 : 186, y);
                    TextBox input = AddCareerInput(this.achievementsBox, column == 0 ? 140 : 275, y, 38);
                    input.KeyPress += this.NumericOnly_KeyPress;
                    input.TextChanged += this.CareerCounter_TextChanged;
                    input.Tag = index;
                    this.achievementInputs[index] = input;
                }
            }

            this.careerTab.Controls.AddRange(new Control[] { durationBox, trainingBox, this.achievementsBox });
            this.ClubTabControl.Controls.Add(this.careerTab);
            this.BuildLevelTab();

            foreach (TextBox input in new[] { this.careerStartInput, this.retirementAgeInput })
            {
                input.KeyPress += this.NumericOnly_KeyPress;
                input.TextChanged += this.CareerCounter_TextChanged;
            }
            this.careerStartInput.TextChanged += (s, e) =>
            {
                if (!this.showingCareer && this.coach != null && int.TryParse(this.careerStartInput.Text, out int year))
                {
                    this.coach.CareerStartYear = year;
                    this.ShowCareerEnd();
                }
            };
            this.retirementAgeInput.TextChanged += (s, e) =>
            {
                if (!this.showingCareer && this.coach != null && short.TryParse(this.retirementAgeInput.Text, out short age))
                    this.coach.RetirementAge = age;
            };
            this.careerModeInput.SelectedIndexChanged += (s, e) =>
            {
                if (this.coach != null && this.careerModeInput.SelectedValue is CoachEnums.CareerMode mode)
                    this.coach.CareerMode = mode;
                this.ShowCareerEnd();
            };
        }

        // Called from InitClubTabControl, once the coach's binding source exists.
        private void InitCareerTab()
        {
            this.BindEnumCombo<CoachEnums.CareerMode>(this.careerModeInput, nameof(Coach.CareerMode));
            this.BindEnumCombo<CoachEnums.TrainingResult>(this.trainingResultInput, nameof(Coach.TrainingResult));
            this.unlockDifficultyInput.DataBindings.Add("Checked", this.bindingSource, nameof(Coach.UnlockDifficulty), false, DataSourceUpdateMode.OnPropertyChanged);
            this.unlockDifficultyInput.DataBindings.Add("Enabled", this.bindingSource, nameof(Coach.DifficultyLocked));
            this.withDismissalsInput.DataBindings.Add("Checked", this.leagueBindingSource, nameof(LeagueSettings.WithDismissals), false, DataSourceUpdateMode.OnPropertyChanged);
            this.ShowCareer();
        }

        // Start year and titles aren't plain bindings: shown again on every refresh.
        private void ShowCareer()
        {
            if (this.careerTab == null)
                return;
            this.showingCareer = true;
            try
            {
                bool hasYear = this.coach != null && this.coach.CurrentYear > 0;
                this.careerStartInput.Text = this.coach == null ? "" : hasYear ? this.coach.CareerStartYear.ToString() : this.coach.CareerStartSeason.ToString();
                this.careerStartInput.Enabled = hasYear;
                this.retirementAgeInput.Text = this.coach?.RetirementAge.ToString() ?? "";

                CoachAchievements achievements = this.coach?.Achievements;
                for (int i = 0; i < this.achievementInputs.Length; i++)
                    this.achievementInputs[i].Text = achievements != null ? achievements.Counters[i].ToString() : "";
                this.achievementsBox.Enabled = achievements != null;
            }
            finally
            {
                this.showingCareer = false;
            }
            this.ShowCareerEnd();
            this.ShowLevelState();
        }

        private void ShowCareerEnd()
        {
            int? end = this.coach?.CurrentYear > 0 ? this.coach.CareerEndYear : null;
            this.careerEndValue.Text = end.HasValue ? $"{end}/{(end + 1) % 100:00}" : "-";
        }

        // Caps at the Int16 range and, for a title counter, hands the value to the coach.
        private void CareerCounter_TextChanged(object sender, EventArgs e)
        {
            TextBox input = (TextBox)sender;
            if (!string.IsNullOrEmpty(input.Text) && !short.TryParse(input.Text, out _))
            {
                input.Text = short.MaxValue.ToString();
                input.SelectionStart = input.Text.Length;
                return;
            }
            if (!this.showingCareer && input.Tag is int index && this.coach?.Achievements != null && short.TryParse(input.Text, out short value))
                this.coach.Achievements.Set(index, value);
        }
    }
}
