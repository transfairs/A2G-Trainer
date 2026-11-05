using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// "Levelwertung" tab (what the game's weekly level evaluation looks at) and the "Merker und
    /// Sperren" box on the "Karriere" tab (event flags, sabotage, naturalisation). Built in code,
    /// like the career tab; see <see cref="CoachLevelState"/>.
    /// </summary>
    public partial class CoachView
    {
        private TabPage levelTab;
        private BindingSource levelBindingSource;
        private readonly List<KeyValuePair<CheckBox, Func<CoachLevelState, bool>>> levelChecks = new List<KeyValuePair<CheckBox, Func<CoachLevelState, bool>>>();
        private CheckBox stopSabotageInput, resetNaturalisationInput;
        private Label naturalisationValue;
        private Button prepareLevelUpButton;
        // Set while the bit boxes are filled from the coach, so their handlers don't write back.
        private bool showingLevel;

        // A text box bound to a CoachLevelState property, capped at a byte or the Int16 range.
        private TextBox AddLevelInput(Control box, string label, string property, int x, int y, bool isByte)
        {
            AddCareerLabel(box, label, x, y);
            TextBox input = AddCareerInput(box, x + 150, y, 50);
            input.KeyPress += this.NumericOnly_KeyPress;
            if (isByte)
                input.TextChanged += this.ByteMax255_TextChanged;
            else
                input.TextChanged += this.CareerCounter_TextChanged;
            input.DataBindings.Add("Text", this.levelBindingSource, property);
            return input;
        }

        // A check box for one bit of the level state.
        private CheckBox AddLevelCheck(Control box, string text, int x, int y, Func<CoachLevelState, bool> get, Action<CoachLevelState, bool> set)
        {
            CheckBox check = new CheckBox { Text = text, Location = new Point(x, y), AutoSize = true };
            check.CheckedChanged += (s, e) =>
            {
                if (!this.showingLevel && this.coach != null)
                    set(this.coach.LevelState, check.Checked);
            };
            box.Controls.Add(check);
            this.levelChecks.Add(new KeyValuePair<CheckBox, Func<CoachLevelState, bool>>(check, get));
            return check;
        }

        private CheckBox AddMarkCheck(Control box, string text, ushort mask, int x, int y) =>
            this.AddLevelCheck(box, text, x, y, s => s.HasMark(mask), (s, on) => s.SetMark(mask, on));

        private CheckBox AddByteBitCheck(Control box, string text, Func<CoachLevelState, byte> get, Action<CoachLevelState, byte> set, byte mask, int x, int y) =>
            this.AddLevelCheck(box, text, x, y, s => (get(s) & mask) != 0, (s, on) => set(s, (byte)(on ? get(s) | mask : get(s) & ~mask)));

        private CheckBox AddFlagCheck(Control box, string text, uint mask, int x, int y) =>
            this.AddLevelCheck(box, text, x, y, s => s.HasFlag(mask), (s, on) => s.SetFlag(mask, on));

        // Called from BuildCareerTab: the tab and the box; their bindings need the binding source,
        // which exists from the start (its data source follows the coach, see ShowLevelState).
        private void BuildLevelTab()
        {
            this.levelBindingSource = new BindingSource { DataSource = typeof(CoachLevelState) };
            this.levelTab = new TabPage { Text = "Levelwertung", UseVisualStyleBackColor = true, AutoScroll = true, Padding = new Padding(3) };

            GroupBox upBox = new GroupBox { Text = "Aufstieg", Location = new Point(6, 6), Size = new Size(280, 236) };
            this.AddLevelInput(upBox, "Siegesserie (x2)", nameof(CoachLevelState.WinStreak), 7, 20, false);
            this.AddLevelInput(upBox, "Wochen im Amt", nameof(CoachLevelState.WeeksInOffice), 7, 46, false);
            this.AddLevelInput(upBox, "Ungeschlagen (Spiele)", nameof(CoachLevelState.Unbeaten), 7, 72, false);
            this.AddLevelInput(upBox, "Platz Vorsaison", nameof(CoachLevelState.LastPosition), 7, 98, true);
            this.AddLevelInput(upBox, "Aufstiege in Folge", nameof(CoachLevelState.PromotionsInARow), 7, 124, true);
            this.AddLevelInput(upBox, "Große Titel (Karriere)", nameof(CoachLevelState.BigTitles), 7, 150, false);
            this.AddLevelInput(upBox, "Gutschrift", nameof(CoachLevelState.Credit), 7, 176, true);
            this.AddLevelInput(upBox, "Serie (Punkte)", nameof(CoachLevelState.SeriesPoints), 7, 202, false);

            GroupBox changeBox = new GroupBox { Text = "Letzte Leveländerung", Location = new Point(6, 248), Size = new Size(280, 94) };
            this.AddLevelInput(changeBox, "Saison (Index)", nameof(CoachLevelState.LastChangeSeason), 7, 20, false);
            this.AddLevelInput(changeBox, "Halbwoche", nameof(CoachLevelState.LastChangeHalfWeek), 7, 46, false);
            this.prepareLevelUpButton = new Button { Text = "Aufstieg vorbereiten", Location = new Point(140, 68), Size = new Size(134, 23) };
            this.prepareLevelUpButton.Click += (s, e) =>
            {
                if (this.coach == null)
                    return;
                this.coach.LevelState.PrepareLevelUp(this.coach.CurrentSeason);
                this.ShowLevelState();
            };
            changeBox.Controls.Add(this.prepareLevelUpButton);

            GroupBox marksBox = new GroupBox { Text = "Marken", Location = new Point(292, 6), Size = new Size(288, 118) };
            this.AddMarkCheck(marksBox, "Europapokal-Viertelfinale", 0x08, 8, 18);
            this.AddMarkCheck(marksBox, "Finanzen saniert", 0x20, 8, 42);
            this.AddMarkCheck(marksBox, "Finanzmarke vergeben", 0x40, 8, 66);
            this.AddMarkCheck(marksBox, "Halbfinale", 0x100, 160, 18);
            this.AddMarkCheck(marksBox, "Turniersieg", 0x200, 160, 42);
            this.AddByteBitCheck(marksBox, "Aufstieg", s => s.SeasonSuccess, (s, v) => s.SeasonSuccess = v, 0x01, 160, 66);
            this.AddByteBitCheck(marksBox, "Meister", s => s.SeasonSuccess, (s, v) => s.SeasonSuccess = v, 0x02, 160, 90);

            GroupBox downBox = new GroupBox { Text = "Abstieg", Location = new Point(292, 130), Size = new Size(288, 212) };
            this.AddByteBitCheck(downBox, "Saisonziel verfehlt", s => s.DemotionReasons, (s, v) => s.DemotionReasons = v, 0x01, 8, 18);
            this.AddByteBitCheck(downBox, "Abstiegsplatz", s => s.DemotionReasons, (s, v) => s.DemotionReasons = v, 0x02, 8, 42);
            this.AddByteBitCheck(downBox, "Lizenzstrafe", s => s.DemotionReasons, (s, v) => s.DemotionReasons = v, 0x04, 160, 18);
            this.AddByteBitCheck(downBox, "Schulden", s => s.DemotionReasons, (s, v) => s.DemotionReasons = v, 0x08, 160, 42);
            this.AddLevelInput(downBox, "Entlassungen Vorsaison", nameof(CoachLevelState.DismissalsLastSeason), 7, 68, true);
            this.AddLevelInput(downBox, "Entlassungen Saison", nameof(CoachLevelState.DismissalsThisSeason), 7, 94, true);
            this.AddLevelInput(downBox, "Rücktritte Vorsaison", nameof(CoachLevelState.ResignationsLastSeason), 7, 120, true);
            this.AddLevelInput(downBox, "Rücktritte Saison", nameof(CoachLevelState.ResignationsThisSeason), 7, 146, true);

            this.levelTab.Controls.AddRange(new Control[] { upBox, changeBox, marksBox, downBox });
            this.ClubTabControl.Controls.Add(this.levelTab);

            GroupBox flagsBox = new GroupBox { Text = "Merker und Sperren", Location = new Point(6, 220), Size = new Size(574, 122) };
            this.AddLevelInput(flagsBox, "Schiri-Ausraster", nameof(CoachLevelState.RefereeOutbursts), 7, 18, true);
            this.AddFlagCheck(flagsBox, "Schiri-Rüge diese Saison erhalten", 0x1, 8, 44);
            this.AddFlagCheck(flagsBox, "Staatsorden erhalten", 0x2, 8, 68);
            this.AddFlagCheck(flagsBox, "Stadion-Meldung ausstehend", 0x4, 8, 92);
            this.AddFlagCheck(flagsBox, "Kassengriff diesen Monat", 0x8, 260, 18);
            this.stopSabotageInput = new CheckBox { Text = "Sabotage gegen ihn beenden", Location = new Point(260, 44), AutoSize = true };
            this.resetNaturalisationInput = new CheckBox { Text = "Einbürgerung zurücksetzen", Location = new Point(260, 68), AutoSize = true };
            this.naturalisationValue = new Label { AutoSize = true, Location = new Point(276, 92), Text = "" };
            flagsBox.Controls.AddRange(new Control[] { this.stopSabotageInput, this.resetNaturalisationInput, this.naturalisationValue });
            this.careerTab.Controls.Add(flagsBox);

            this.stopSabotageInput.DataBindings.Add("Checked", this.levelBindingSource, nameof(CoachLevelState.StopSabotage), false, DataSourceUpdateMode.OnPropertyChanged);
            this.resetNaturalisationInput.DataBindings.Add("Checked", this.levelBindingSource, nameof(CoachLevelState.ResetNaturalisation), false, DataSourceUpdateMode.OnPropertyChanged);
        }

        // The bit boxes, the sabotage and the naturalisation aren't plain bindings: shown again on every refresh.
        private void ShowLevelState()
        {
            if (this.levelBindingSource == null)
                return;
            CoachLevelState state = this.coach?.LevelState;
            this.levelBindingSource.DataSource = (object)state ?? typeof(CoachLevelState);
            this.levelBindingSource.ResetBindings(false);
            this.showingLevel = true;
            try
            {
                foreach (KeyValuePair<CheckBox, Func<CoachLevelState, bool>> check in this.levelChecks)
                    check.Key.Checked = state != null && check.Value(state);
            }
            finally
            {
                this.showingLevel = false;
            }
            this.stopSabotageInput.Enabled = state != null && state.HasSabotage;
            this.resetNaturalisationInput.Enabled = state != null && (state.NaturalisationTalks > 0 || state.NaturalisationRefusals > 0);
            this.naturalisationValue.Text = state == null ? "" : $"{state.NaturalisationTalks} Gespräche, {state.NaturalisationRefusals} Ablehnungen";
        }
    }
}
