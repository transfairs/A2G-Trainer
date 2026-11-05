using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// "Live-Spiel" tab: the bench, the substitutions made and attitude, hardness and effort of the
    /// club shown while the game plays its match (see <see cref="LiveMatch"/>). The view's "Werte
    /// neuladen" reads the match again, "Speichern" writes what was changed here - only that, since
    /// the match goes on meanwhile. Built in code, like the budget tab.
    /// </summary>
    public partial class ClubView
    {
        private TabPage liveTab;
        private Label liveStatusValue;
        private TextBox liveSubstitutionsInput;
        private ComboBox liveAttitudeInput, liveHardnessInput, liveEffortInput;
        private readonly ComboBox[] liveBenchInputs = new ComboBox[LiveMatch.BenchSize];
        private GroupBox liveTacticsBox, liveBenchBox;
        private LiveMatch liveMatch;
        // What was changed on the tab since the match was read; Save writes only these.
        private readonly HashSet<Control> liveChanged = new HashSet<Control>();
        // Set while the tab is filled from the match, so the change handlers don't count it.
        private bool showingLive;
        // The players the bench places may take, read with the match.
        private List<NationalSquads.Member> liveBenchMembers = new List<NationalSquads.Member>();

        // A drop-down of names whose index is the stored value.
        private static ComboBox AddLiveChoice(Control box, string label, int y, string[] names)
        {
            AddLabel(box, label, 7, y);
            ComboBox combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(110, y), Size = new Size(150, 21) };
            combo.Items.AddRange(names.Cast<object>().ToArray());
            box.Controls.Add(combo);
            return combo;
        }

        // Remembers that the user changed this control (not the view filling or clearing it).
        private void LiveInput_Changed(object sender, EventArgs e)
        {
            if (!this.showingLive && !this.showingBlock)
                this.liveChanged.Add((Control)sender);
        }

        // A player taken for one place leaves the others' lists (and one given up comes back).
        private void LiveBench_Changed(object sender, EventArgs e)
        {
            if (this.showingLive || this.showingBlock)
                return;
            this.LiveInput_Changed(sender, e);
            this.FillBenchPlaces(this.liveBenchInputs.Select(c => c.SelectedValue is ushort id ? id : (ushort)0).ToArray(), (ComboBox)sender);
        }

        // Each place offers "–", the free players and the one it holds.
        private void FillBenchPlaces(ushort[] bench, ComboBox keep = null)
        {
            bool showing = this.showingLive;
            this.showingLive = true;
            try
            {
                for (int i = 0; i < LiveMatch.BenchSize; i++)
                {
                    if (this.liveBenchInputs[i] == keep)
                        continue;
                    HashSet<ushort> taken = new HashSet<ushort>(bench.Where((id, j) => j != i && id != 0));
                    List<NationalSquads.Member> choices = new List<NationalSquads.Member> { new NationalSquads.Member { Id = 0, Name = "–" } };
                    choices.AddRange(this.liveBenchMembers.Where(m => !taken.Contains(m.Id)));
                    // Without a DisplayMember the list would show the ValueMember, i.e. the IDs.
                    this.liveBenchInputs[i].DisplayMember = nameof(NationalSquads.Member.Text);
                    this.liveBenchInputs[i].DataSource = choices;
                    this.liveBenchInputs[i].ValueMember = nameof(NationalSquads.Member.Id);
                    this.liveBenchInputs[i].SelectedValue = bench[i];
                }
            }
            finally
            {
                this.showingLive = showing;
            }
        }

        private void BuildLiveMatchTab()
        {
            this.liveTab = new TabPage { Text = "Live-Spiel", UseVisualStyleBackColor = true, AutoScroll = true, Padding = new Padding(3) };

            this.liveStatusValue = new Label { AutoSize = false, Location = new Point(9, 10), Size = new Size(570, 16), Text = "" };

            this.liveTacticsBox = new GroupBox { Text = "Taktik im Spiel", Location = new Point(6, 36), Size = new Size(270, 128) };
            this.liveAttitudeInput = AddLiveChoice(this.liveTacticsBox, "Einstellung", 20, LiveMatch.AttitudeNames);
            this.liveHardnessInput = AddLiveChoice(this.liveTacticsBox, "Härte", 46, LiveMatch.HardnessNames);
            this.liveEffortInput = AddLiveChoice(this.liveTacticsBox, "Einsatz", 72, LiveMatch.EffortNames);
            this.liveSubstitutionsInput = AddInput(this.liveTacticsBox, "Wechsel bisher", 7, 110, 98, 40);
            this.liveSubstitutionsInput.KeyPress += this.NumericOnly_KeyPress;
            this.liveSubstitutionsInput.TextChanged += this.ByteMax255_TextChanged;
            foreach (ComboBox combo in new[] { this.liveAttitudeInput, this.liveHardnessInput, this.liveEffortInput })
                combo.SelectedIndexChanged += this.LiveInput_Changed;
            this.liveSubstitutionsInput.TextChanged += this.LiveInput_Changed;

            this.liveBenchBox = new GroupBox { Text = "Ersatzbank", Location = new Point(282, 36), Size = new Size(298, 210) };
            for (int i = 0; i < LiveMatch.BenchSize; i++)
            {
                AddLabel(this.liveBenchBox, $"{i + 1}.", 7, 20 + i * 26);
                this.liveBenchInputs[i] = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(30, 20 + i * 26), Size = new Size(260, 21) };
                this.liveBenchInputs[i].SelectedIndexChanged += this.LiveBench_Changed;
                this.liveBenchBox.Controls.Add(this.liveBenchInputs[i]);
            }

            this.liveTab.Controls.AddRange(new Control[] { this.liveStatusValue, this.liveTacticsBox, this.liveBenchBox });
            this.ClubTabControl.Controls.Add(this.liveTab);
        }

        // Finds the shown club's running match and shows it; without one everything is disabled.
        private void ShowLiveMatch()
        {
            bool running = this.club != null && this.IsGameRunning(silent: true);
            this.ShowLiveMatch(running ? LiveMatch.Find(this.Memory, FixedAddresses.For(this.Memory), this.club.Country, this.club.Id) : null);
        }

        private void ShowLiveMatch(LiveMatch match)
        {
            this.liveChanged.Clear();
            this.liveMatch = match;
            FixedAddresses fixedAddresses = match != null ? FixedAddresses.For(this.Memory) : null;
            bool live = this.liveMatch != null;
            this.liveTacticsBox.Enabled = this.liveBenchBox.Enabled = live;
            this.showingLive = true;
            try
            {
                if (!live)
                {
                    this.liveStatusValue.Text = "Kein laufendes Spiel";
                    foreach (ComboBox place in this.liveBenchInputs)
                        place.DataSource = null;
                    return;
                }

                List<ClubSquads.Entry> clubs = ClubSquads.Clubs(this.Memory, fixedAddresses);
                KeyValuePair<PlayerEnums.Country, ushort> opponent = this.liveMatch.Opponent;
                string opponentName = clubs.FirstOrDefault(c => c.Is(opponent.Key, opponent.Value))?.Name ?? $"{opponent.Key} {opponent.Value}";
                this.liveStatusValue.Text = $"{this.liveMatch.Minute}. Minute, {this.liveMatch.HomeGoals}:{this.liveMatch.AwayGoals} {(this.liveMatch.IsHome ? "gegen" : "bei")} {opponentName}";
                this.liveAttitudeInput.SelectedIndex = Math.Min(this.liveMatch.Attitude, (byte)(LiveMatch.AttitudeNames.Length - 1));
                this.liveHardnessInput.SelectedIndex = Math.Min(this.liveMatch.Hardness, (byte)(LiveMatch.HardnessNames.Length - 1));
                this.liveEffortInput.SelectedIndex = Math.Min(this.liveMatch.Effort, (byte)(LiveMatch.EffortNames.Length - 1));
                this.liveSubstitutionsInput.Text = this.liveMatch.Substitutions.ToString();

                // The squad from the record: the engine's working copy of the club is only complete
                // while it handles this club's move.
                ClubSquads.Entry entry = clubs.FirstOrDefault(c => c.Is(this.club.Country, this.club.Id));
                List<ushort> squad = entry != null ? ClubSquads.SquadIds(this.Memory, fixedAddresses, entry, fromRecord: true) : new List<ushort>();
                ushort[] bench = this.liveMatch.Bench;
                List<ushort> candidates = this.liveMatch.BenchCandidates(squad);
                this.liveBenchMembers = NationalSquads.Members(this.Memory, fixedAddresses, candidates.Union(bench.Where(id => id != 0)));
                this.FillBenchPlaces(bench);
            }
            finally
            {
                this.showingLive = false;
            }
        }

        // Called by the view's Save: writes what was changed on the tab; a player chosen twice keeps
        // only his first place. The bench is written as a whole once any place changed.
        private void ApplyLiveMatch()
        {
            if (this.liveMatch == null || this.liveChanged.Count == 0)
                return;
            if (this.liveChanged.Contains(this.liveAttitudeInput) && this.liveAttitudeInput.SelectedIndex >= 0)
                this.liveMatch.Attitude = (byte)this.liveAttitudeInput.SelectedIndex;
            if (this.liveChanged.Contains(this.liveHardnessInput) && this.liveHardnessInput.SelectedIndex >= 0)
                this.liveMatch.Hardness = (byte)this.liveHardnessInput.SelectedIndex;
            if (this.liveChanged.Contains(this.liveEffortInput) && this.liveEffortInput.SelectedIndex >= 0)
                this.liveMatch.Effort = (byte)this.liveEffortInput.SelectedIndex;
            if (this.liveChanged.Contains(this.liveSubstitutionsInput) && byte.TryParse(this.liveSubstitutionsInput.Text, out byte substitutions))
                this.liveMatch.Substitutions = substitutions;
            if (this.liveBenchInputs.Any(this.liveChanged.Contains))
            {
                HashSet<ushort> seen = new HashSet<ushort>();
                ushort[] bench = this.liveBenchInputs.Select(c => c.SelectedValue is ushort id && id != 0 && seen.Add(id) ? id : (ushort)0).ToArray();
                this.liveMatch.WriteBench(bench);
            }
            this.liveChanged.Clear();
        }
    }
}
