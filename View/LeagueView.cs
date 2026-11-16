using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// "Ligen": the tables of all 13 leagues as the game computes them, a rolled league's final order
    /// to choose, licence and points deduction of any league club, and the champion and cup winner of
    /// the European countries without league table before the cup draws. Built in code, laid out like
    /// the other views: two tabs, "Werte neuladen" below them.
    /// </summary>
    public class LeagueView : EntityView
    {
        private readonly TabControl tabControl;
        private readonly Button reloadButton;

        private readonly ComboBox leagueInput;
        private readonly Label leagueStatus;
        private readonly ListView tableList;
        private readonly Button upButton, downButton, applyOrderButton;
        private readonly CheckBox licenceRefusedInput;
        private readonly ComboBox deductionInput;

        private readonly ComboBox cupCountryInput, championInput, cupWinnerInput;
        private readonly Button cupApplyButton;
        private readonly Label seasonStatus, cupStatus;

        private List<LeagueTable> leagues = new List<LeagueTable>();
        private Dictionary<PlayerEnums.Country, List<EuropeanCupEntries.Team>> cupCountries = new Dictionary<PlayerEnums.Country, List<EuropeanCupEntries.Team>>();
        private LeagueTables.SeasonPhase phase;
        // Set while the selection's licence inputs are filled, so their handlers don't write.
        private bool showing;

        /// <summary>Background of the list positions 0-9 of a rolled league, which always finish on top (the same pale tint as the amateurs).</summary>
        internal static readonly Color BracketBackColor = PlayerView.AmateurBackColor;

        /// <summary>Creates the view bound to the given memory accessor and process controller.</summary>
        public LeagueView(ProcessMemory memory, ProcessController controller) : base(memory, controller)
        {
            this.Name = "LeagueView";
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25F);
            this.BackgroundImage = Properties.Resources.TabControl;

            this.tabControl = new TabControl { Name = "MainTabControl", Location = new Point(20, 13), Size = new Size(594, 372) };
            TabPage tablesTab = new TabPage { Text = "Tabellen", UseVisualStyleBackColor = true, Padding = new Padding(3) };
            TabPage cupTab = new TabPage { Text = "Europapokal", UseVisualStyleBackColor = true, Padding = new Padding(3) };
            this.tabControl.TabPages.AddRange(new[] { tablesTab, cupTab });

            // Tables: the league, the table, and on the right what can be done with the selection.
            tablesTab.Controls.Add(new Label { Text = "Liga", Location = new Point(6, 11), AutoSize = true });
            this.leagueInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(48, 8), Size = new Size(230, 21) };
            this.leagueInput.SelectedIndexChanged += (s, e) => this.ShowLeague(null);
            this.leagueStatus = new Label { Name = "LeagueStatus", Location = new Point(290, 11), Size = new Size(290, 15) };
            this.tableList = new ListView { View = System.Windows.Forms.View.Details, FullRowSelect = true, MultiSelect = false, HideSelection = false, Location = new Point(6, 36), Size = new Size(420, 304) };
            string[] columns = { "Pl.", "Verein", "Sp.", "S", "U", "N", "Tore", "Pkt." };
            int[] widths = { 30, 150, 32, 28, 28, 28, 50, 38 };
            for (int column = 0; column < columns.Length; column++)
                this.tableList.Columns.Add(columns[column], widths[column], column == 1 ? HorizontalAlignment.Left : HorizontalAlignment.Right);
            this.tableList.SelectedIndexChanged += (s, e) => this.ShowSelection();

            GroupBox orderBox = new GroupBox { Text = "Endstand", Location = new Point(432, 36), Size = new Size(148, 108) };
            this.upButton = new Button { Text = "Nach oben", Location = new Point(10, 20), Size = new Size(128, 23), UseVisualStyleBackColor = true };
            this.downButton = new Button { Text = "Nach unten", Location = new Point(10, 47), Size = new Size(128, 23), UseVisualStyleBackColor = true };
            this.applyOrderButton = new Button { Text = "Reihenfolge festlegen", Location = new Point(10, 76), Size = new Size(128, 23), UseVisualStyleBackColor = true };
            this.upButton.Click += (s, e) => this.MoveSelection(-1);
            this.downButton.Click += (s, e) => this.MoveSelection(1);
            this.applyOrderButton.Click += (s, e) => this.ApplyOrder();
            orderBox.Controls.AddRange(new Control[] { this.upButton, this.downButton, this.applyOrderButton });

            GroupBox licenceBox = new GroupBox { Text = "Lizenz", Location = new Point(432, 150), Size = new Size(148, 76) };
            this.licenceRefusedInput = new CheckBox { Text = "Lizenz verweigert", Location = new Point(10, 20), AutoSize = true };
            this.licenceRefusedInput.CheckedChanged += (s, e) => this.WriteLicence();
            licenceBox.Controls.Add(new Label { Text = "Punktabzug", Location = new Point(10, 49), AutoSize = true });
            this.deductionInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(76, 46), Size = new Size(62, 21) };
            this.deductionInput.Items.AddRange(ClubView.DeductionChoices());
            this.deductionInput.SelectedIndexChanged += (s, e) => this.WriteLicence();
            licenceBox.Controls.AddRange(new Control[] { this.licenceRefusedInput, this.deductionInput });

            tablesTab.Controls.AddRange(new Control[] { this.leagueInput, this.leagueStatus, this.tableList, orderBox, licenceBox });

            // European cup: where the season stands, then champion and cup winner of one country.
            GroupBox seasonBox = new GroupBox { Text = "Saison", Location = new Point(6, 6), Size = new Size(574, 46) };
            this.seasonStatus = new Label { Name = "SeasonStatus", Location = new Point(10, 20), Size = new Size(554, 18) };
            seasonBox.Controls.Add(this.seasonStatus);

            GroupBox titlesBox = new GroupBox { Text = "Länder ohne Liga", Location = new Point(6, 58), Size = new Size(320, 160) };
            this.cupCountryInput = AddChoice(titlesBox, "Land", 20);
            this.championInput = AddChoice(titlesBox, "Meister", 47);
            this.cupWinnerInput = AddChoice(titlesBox, "Pokalsieger", 74);
            this.cupApplyButton = new Button { Text = "Übernehmen", Location = new Point(105, 101), Size = new Size(205, 23), UseVisualStyleBackColor = true };
            this.cupStatus = new Label { Name = "CupStatus", Location = new Point(7, 128), Size = new Size(306, 28) };
            this.cupCountryInput.SelectedIndexChanged += (s, e) => this.ShowCupCountry();
            this.cupApplyButton.Click += (s, e) => this.ApplyCupTitles();
            titlesBox.Controls.AddRange(new Control[] { this.cupApplyButton, this.cupStatus });
            cupTab.Controls.AddRange(new Control[] { seasonBox, titlesBox });

            this.reloadButton = new Button { Name = "ReloadBtn", Text = "Werte neuladen", Location = new Point(20, 391), Size = new Size(114, 23), UseVisualStyleBackColor = true };
            this.reloadButton.Click += (s, e) => this.RefreshValues();
            this.Controls.AddRange(new Control[] { this.reloadButton, this.tabControl });
            this.UpdateButtons();
        }

        private static ComboBox AddChoice(Control box, string label, int y)
        {
            box.Controls.Add(new Label { AutoSize = true, Location = new Point(7, y + 3), Text = label });
            ComboBox combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(105, y), Size = new Size(205, 21) };
            box.Controls.Add(combo);
            return combo;
        }

        private FixedAddresses Fixed => FixedAddresses.For(this.Memory);

        /// <summary>Where the European clubs' records are read from instead of the game's place (tests: that lies beyond their fake module).</summary>
        internal uint? CupClubRecords { get; set; }

        /// <summary>Reads all leagues, the season phase and the countries without league table from the running game.</summary>
        internal void RefreshValues()
        {
            int previous = this.leagueInput.SelectedIndex;
            PlayerEnums.Country? previousCountry = this.cupCountryInput.SelectedItem is CountryItem item ? item.Country : (PlayerEnums.Country?)null;
            bool running = this.IsGameRunning(silent: true);
            FixedAddresses fixedAddresses = running ? this.Fixed : null;

            this.leagues = LeagueTables.ReadAll(this.Memory, fixedAddresses);
            this.phase = fixedAddresses == null ? LeagueTables.SeasonPhase.Unknown : LeagueTables.Phase(this.Memory, fixedAddresses);
            this.cupCountries = EuropeanCupEntries.Countries(this.Memory, fixedAddresses, this.CupClubRecords);

            this.leagueInput.Items.Clear();
            foreach (LeagueTable league in this.leagues)
                this.leagueInput.Items.Add($"{PlayerEnums.GetDescription(league.Country)} – {league.Name}");
            if (this.leagueInput.Items.Count > 0)
                this.leagueInput.SelectedIndex = previous >= 0 && previous < this.leagueInput.Items.Count ? previous : 0;
            else
                this.ShowLeague(null);

            this.seasonStatus.Text = SeasonText(this.phase, fixedAddresses == null ? (short?)null : this.HalfWeek(fixedAddresses));
            this.cupCountryInput.Items.Clear();
            foreach (PlayerEnums.Country country in this.cupCountries.Keys.OrderBy(c => PlayerEnums.GetDescription(c)))
                this.cupCountryInput.Items.Add(new CountryItem(country));
            CountryItem keep = this.cupCountryInput.Items.Cast<CountryItem>().FirstOrDefault(c => c.Country == previousCountry);
            if (keep != null)
                this.cupCountryInput.SelectedItem = keep;
            else if (this.cupCountryInput.Items.Count > 0)
                this.cupCountryInput.SelectedIndex = 0;
            else
                this.ShowCupCountry();
        }

        private short? HalfWeek(FixedAddresses fixedAddresses)
        {
            byte[] bytes = this.Memory.ReadBytesAtAddress(fixedAddresses.Runtime(this.Memory, Settings.HalfWeekOffset), 2);
            return bytes == null ? (short?)null : BitConverter.ToInt16(bytes, 0);
        }

        /// <summary>
        /// Where the season stands, as the European cup tab shows it. The game counts the season in
        /// half-weeks (two a week); the user sees weeks.
        /// </summary>
        internal static string SeasonText(LeagueTables.SeasonPhase phase, short? halfWeek)
        {
            switch (phase)
            {
                case LeagueTables.SeasonPhase.BeforeCupDraw:
                    int weeks = (Settings.HalfWeekCupDraw - (halfWeek ?? 0) + 1) / 2;
                    return "Europapokal noch nicht ausgelost, die Auslosung ist " + (weeks <= 1 ? "in dieser Woche." : $"in etwa {weeks} Wochen.");
                case LeagueTables.SeasonPhase.Running:
                    return "Europapokal ist ausgelost. Meister und Pokalsieger lassen sich erst nach dem nächsten Saisonstart wieder ändern.";
                case LeagueTables.SeasonPhase.SeasonOver:
                    return "Saison ist beendet. Der nächste Saisonstart lost Meister und Pokalsieger neu aus.";
                default:
                    return "Kein Spiel verbunden.";
            }
        }

        private LeagueTable League => this.leagueInput.SelectedIndex >= 0 && this.leagueInput.SelectedIndex < this.leagues.Count ? this.leagues[this.leagueInput.SelectedIndex] : null;

        /// <summary>Whether the selected league's final order can be chosen: a rolled league before its season end.</summary>
        private bool CanOrder => this.League?.Mode == LeagueMode.Simulated && (this.phase == LeagueTables.SeasonPhase.BeforeCupDraw || this.phase == LeagueTables.SeasonPhase.Running);

        /// <summary>The league's mode as the status beside the league shows it.</summary>
        internal static string ModeText(LeagueTable league)
        {
            if (league == null)
                return "";
            switch (league.Mode)
            {
                case LeagueMode.Simulated: return "Ergebnisse beim Saisonstart ausgewürfelt";
                case LeagueMode.SimulatedInactive: return "ausgewürfelt, ohne Auf- und Abstieg";
                case LeagueMode.NoResults: return "ohne Spiele, Reihenfolge der Vereinsliste";
                default: return $"gespielt, {league.Played} von {league.Matchdays} Spieltagen";
            }
        }

        // Fills the table; keeps the club selected by its list position.
        private void ShowLeague(int? selectPosition)
        {
            LeagueTable league = this.League;
            this.leagueStatus.Text = ModeText(league);
            this.tableList.BeginUpdate();
            this.tableList.Items.Clear();
            if (league != null)
            {
                int place = 1;
                foreach (LeagueTableRow row in league.Standings())
                {
                    ListViewItem item = new ListViewItem((place++).ToString()) { Tag = row };
                    item.SubItems.Add(row.Name);
                    item.SubItems.Add(row.Games.ToString());
                    item.SubItems.Add(row.Wins.ToString());
                    item.SubItems.Add(row.Draws.ToString());
                    item.SubItems.Add(row.Losses.ToString());
                    item.SubItems.Add($"{row.GoalsFor}:{row.GoalsAgainst}");
                    item.SubItems.Add(row.Points.ToString());
                    if ((league.Mode == LeagueMode.Simulated || league.Mode == LeagueMode.SimulatedInactive) && row.ListPosition < LeagueTable.Bracket)
                        item.BackColor = BracketBackColor;
                    if (row.LicenceRefused)
                        item.ForeColor = Color.Gray;
                    this.tableList.Items.Add(item);
                    if (row.ListPosition == selectPosition)
                        item.Selected = true;
                }
            }
            this.tableList.EndUpdate();
            this.ShowSelection();
        }

        private LeagueTableRow SelectedRow => this.tableList.SelectedItems.Count == 1 ? this.tableList.SelectedItems[0].Tag as LeagueTableRow : null;

        private void ShowSelection()
        {
            LeagueTableRow row = this.SelectedRow;
            this.showing = true;
            try
            {
                this.licenceRefusedInput.Checked = row != null && row.LicenceRefused;
                this.deductionInput.SelectedIndex = row == null ? -1 : row.Licence & 3;
            }
            finally
            {
                this.showing = false;
            }
            this.UpdateButtons();
        }

        private void UpdateButtons()
        {
            int index = this.tableList.SelectedIndices.Count == 1 ? this.tableList.SelectedIndices[0] : -1;
            this.upButton.Enabled = this.CanOrder && index > 0;
            this.downButton.Enabled = this.CanOrder && index >= 0 && index < this.tableList.Items.Count - 1;
            this.applyOrderButton.Enabled = this.CanOrder;
            // A country not chosen has no licence procedure and no season change that would heed one.
            bool licence = this.SelectedRow != null && (this.League?.Mode == LeagueMode.Played || this.League?.Mode == LeagueMode.Simulated);
            this.licenceRefusedInput.Enabled = licence;
            this.deductionInput.Enabled = licence;
            this.championInput.Enabled = this.cupWinnerInput.Enabled = this.cupApplyButton.Enabled = this.phase == LeagueTables.SeasonPhase.BeforeCupDraw && this.cupCountryInput.SelectedItem != null;
        }

        // Moves the selected club in the list; nothing is written before "Reihenfolge festlegen".
        private void MoveSelection(int by)
        {
            if (this.tableList.SelectedIndices.Count != 1)
                return;
            int from = this.tableList.SelectedIndices[0], to = from + by;
            if (to < 0 || to >= this.tableList.Items.Count)
                return;
            ListViewItem item = this.tableList.Items[from];
            this.tableList.Items.RemoveAt(from);
            this.tableList.Items.Insert(to, item);
            for (int i = 0; i < this.tableList.Items.Count; i++)
                this.tableList.Items[i].Text = (i + 1).ToString();
            item.Selected = true;
            item.EnsureVisible();
            this.UpdateButtons();
        }

        /// <summary>The order shown, as list positions, best first.</summary>
        internal List<int> ShownOrder() => this.tableList.Items.Cast<ListViewItem>().Select(i => ((LeagueTableRow)i.Tag).ListPosition).ToList();

        private void ApplyOrder()
        {
            LeagueTable league = this.League;
            if (league == null || !this.CanOrder)
                return;
            int? selected = this.SelectedRow?.ListPosition;
            if (LeagueTables.WriteOrder(this.Memory, this.Fixed, league, this.ShownOrder()))
                this.ShowLeague(selected);
        }

        // Licence refused and this season's deduction of the selected club, written right away.
        private void WriteLicence()
        {
            LeagueTable league = this.League;
            LeagueTableRow row = this.SelectedRow;
            if (this.showing || league == null || row == null || this.deductionInput.SelectedIndex < 0)
                return;
            byte licence = (byte)((row.Licence & 0x7C) | (this.licenceRefusedInput.Checked ? 0x80 : 0) | this.deductionInput.SelectedIndex);
            if (licence == row.Licence || !LeagueTables.WriteLicence(this.Memory, this.Fixed, league.Country, row.ClubId, licence))
                return;
            league.Licences[row.ListPosition] = licence;
            this.ShowLeague(row.ListPosition);
        }

        /// <summary>A country in the European cup tab's list.</summary>
        internal sealed class CountryItem
        {
            public PlayerEnums.Country Country { get; }
            public CountryItem(PlayerEnums.Country country) { this.Country = country; }
            public override string ToString() => PlayerEnums.GetDescription(this.Country);
        }

        private List<EuropeanCupEntries.Team> CupTeams => this.cupCountryInput.SelectedItem is CountryItem item && this.cupCountries.TryGetValue(item.Country, out List<EuropeanCupEntries.Team> teams) ? teams : new List<EuropeanCupEntries.Team>();

        // Fills champion and cup winner of the chosen country with the current holders.
        private void ShowCupCountry()
        {
            List<EuropeanCupEntries.Team> teams = this.CupTeams;
            foreach (EuropeanCupEntries.Title title in new[] { EuropeanCupEntries.Title.Champion, EuropeanCupEntries.Title.CupWinner })
            {
                ComboBox input = title == EuropeanCupEntries.Title.Champion ? this.championInput : this.cupWinnerInput;
                input.Items.Clear();
                input.Items.AddRange(teams.Cast<object>().ToArray());
                short? holder = this.cupCountryInput.SelectedItem is CountryItem item && this.IsGameRunning(silent: true)
                    ? EuropeanCupEntries.Holder(this.Memory, this.Fixed, item.Country, title)
                    : null;
                input.SelectedItem = teams.FirstOrDefault(t => t.Index == holder);
            }
            this.cupStatus.Text = "";
            this.UpdateButtons();
        }

        private void ApplyCupTitles()
        {
            if (!(this.cupCountryInput.SelectedItem is CountryItem item) || !(this.championInput.SelectedItem is EuropeanCupEntries.Team champion) || !(this.cupWinnerInput.SelectedItem is EuropeanCupEntries.Team cupWinner))
                return;
            if (champion.Index == cupWinner.Index)
            {
                this.cupStatus.Text = "Meister und Pokalsieger müssen verschiedene Vereine sein.";
                return;
            }
            FixedAddresses fixedAddresses = this.Fixed;
            int changed = EuropeanCupEntries.Assign(this.Memory, fixedAddresses, item.Country, EuropeanCupEntries.Title.Champion, champion.Index);
            if (changed >= 0)
                changed = EuropeanCupEntries.Assign(this.Memory, fixedAddresses, item.Country, EuropeanCupEntries.Title.CupWinner, cupWinner.Index);
            this.ShowCupCountry();
            this.cupStatus.Text = changed < 0
                ? "Nicht übernommen: Der Europapokal ist schon ausgelost."
                : $"Übernommen: {champion.Name} ist Meister, {cupWinner.Name} Pokalsieger.";
        }
    }
}
