using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using AnstossTrainer.Shared;

namespace A2G_Trainer.View
{
    /// <summary>Tab for a club - the own one or any other picked below: identity, stadium, and finances.</summary>
    public partial class ClubView : EntityView
    {
        private Club club;
        // Country and club choice; the club picked last ("Verein -> Anderer").
        private ClubSelection clubSelection;
        private ClubSquads.Entry chosenClub;

        /// <summary>Raised when the user picks a club below the tabs; the view already shows it.</summary>
        internal event Action<ClubSquads.Entry> ClubChosen;
        // Set while the block inputs are filled from the club, so their change handlers don't write back.
        private bool showingBlock;
        // The human manager training the club shown, if any: the board's trust belongs to the manager, not the club.
        private CoachController boardCoachController;
        // The human manager's club the own-club slot held last (see HumanClubInstead).
        private KeyValuePair<PlayerEnums.Country, byte>? lastHumanClub;

        /// <summary>Creates the club tab view bound to the given memory accessor and process controller.</summary>
        public ClubView(ProcessMemory memory, ProcessController controller) : base(memory, controller)
        {
            InitializeComponent();
            this.InitStadium();
            this.InitEditorSettings();
            this.BuildBudgetTab();
            this.BuildAssociationTab();
            this.BuildLiveMatchTab();
            this.clubSelection = new ClubSelection(this.ClubCountryInput, this.ClubInput);
            this.clubSelection.Picked += this.ClubPicked;
            this.BoardTrustInput.KeyPress += this.NumericOnly_KeyPress;
            this.BoardTrustInput.TextChanged += this.BoardTrustMax_TextChanged;
        }

        internal void InitClubTabControl()
        {
            this.DisplayUnit.DataSource = Enum.GetValues(typeof(ClubEnums.DisplayUnit)).Cast<ClubEnums.DisplayUnit>().Select(c => new { Value = c, Text = ClubEnums.GetDescription(c) }).ToList();
            this.DisplayUnit.DisplayMember = "Text";
            this.DisplayUnit.ValueMember = "Value";

            this.FieldCondition.DataSource = Enum.GetValues(typeof(ClubEnums.FieldCondition)).Cast<ClubEnums.FieldCondition>().Select(c => new { Value = c, Text = ClubEnums.GetDescription(c) }).ToList();
            this.FieldCondition.DisplayMember = "Text";
            this.FieldCondition.ValueMember = "Value";

            this.bindingSource = new BindingSource
            {
                DataSource = this.club
            };

            this.WealthInput.KeyPress += this.NumericOnly_KeyPress;
            this.AdsInput.KeyPress += this.NumericOnly_KeyPress;
            this.FriendlyInput.KeyPress += this.NumericOnly_KeyPress;
            this.LeagueInput.KeyPress += this.NumericOnly_KeyPress;
            this.SponsorInput.KeyPress += this.NumericOnly_KeyPress;
            this.SponsorPeriod.KeyPress += this.NumericOnly_KeyPress;

            this.RespectInput.KeyPress += this.NumericOnly_KeyPress;
            this.SpiritInput.KeyPress += this.NumericOnly_KeyPress;
            this.Will2WinInput.KeyPress += this.NumericOnly_KeyPress;
            this.TeamCohesionInput.KeyPress += this.NumericOnly_KeyPress;

            this.FreeTicketsInput.KeyPress += this.NumericOnly_KeyPress;
            this.RoadGameSupportInput.KeyPress += this.NumericOnly_KeyPress;

            this.GrantPotInput.KeyPress += this.NumericOnly_KeyPress;
            this.GrantRateInput.KeyPress += this.NumericOnly_KeyPress;

            this.WealthInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.AdsInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.LeagueInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.FriendlyInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.SponsorInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.SponsorPeriod.TextChanged += this.ByteMax255_TextChanged;

            this.RespectInput.TextChanged += this.ByteMax255_TextChanged;
            this.SpiritInput.TextChanged += this.ByteMax255_TextChanged;
            this.Will2WinInput.TextChanged += this.ByteMax255_TextChanged;
            this.TeamCohesionInput.TextChanged += this.ByteMax255_TextChanged;

            this.FreeTicketsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.RoadGameSupportInput.TextChanged += this.UshortMaxNumber_TextChanged;

            this.GrantPotInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.GrantRateInput.TextChanged += this.Int16Range_TextChanged;

            this.ClubNameInput.DataBindings.Add("Text", this.bindingSource, "ClubName");
            this.StadiumNameInput.DataBindings.Add("Text", this.bindingSource, "StadiumName");
            this.DisplayUnit.DataBindings.Add("SelectedValue", this.bindingSource, "DisplayUnit");
            this.FieldCondition.DataBindings.Add("SelectedValue", this.bindingSource, "FieldCondition");
            this.HeatingCheckBox.DataBindings.Add("Checked", this.bindingSource, "HasGrassHeating");
            this.FloodlightCheckbox.DataBindings.Add("Checked", this.bindingSource, "HasFloodLight");
            this.RunningTrackCheckBox.DataBindings.Add("Checked", this.bindingSource, nameof(Club.HasRunningTrack));
            this.UpperSeatsCheckBox.DataBindings.Add("Checked", this.bindingSource, nameof(Club.HasUpperSeats));

            this.WealthInput.DataBindings.Add("Text", this.bindingSource, "Wealth");
            this.LeagueInput.DataBindings.Add("Text", this.bindingSource, "EarningsLeagueGames");
            this.FriendlyInput.DataBindings.Add("Text", this.bindingSource, "EarningsFriendlyGames");
            this.AdsInput.DataBindings.Add("Text", this.bindingSource, "EarningsAds");
            this.SponsorInput.DataBindings.Add("Text", this.bindingSource, "SponsorCash");
            this.SponsorPeriod.DataBindings.Add("Text", this.bindingSource, "SponsorPeriod");
            this.GrantPotInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.GrantPot));
            this.GrantRateInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.GrantRate));

            this.RespectInput.DataBindings.Add("Text", this.bindingSource, "Respect");
            this.SpiritInput.DataBindings.Add("Text", this.bindingSource, "Spirit");
            this.Will2WinInput.DataBindings.Add("Text", this.bindingSource, "Will2Win");
            this.TeamCohesionInput.DataBindings.Add("Text", this.bindingSource, "TeamCohesion");

            this.OffsideTrapInput.KeyPress += this.NumericOnly_KeyPress;
            this.OffsideTrapInput.TextChanged += this.ByteMax255_TextChanged;
            this.OffsideTrapInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.OffsideTrap));
            this.BindTeamSkill(this.AntiOffsideTrapInput, nameof(Club.AntiOffsideTrap));
            this.BindTeamSkill(this.PressingInput, nameof(Club.Pressing));
            this.BindTeamSkill(this.DirtyTricksInput, nameof(Club.DirtyTricks));
            this.BindTeamSkill(this.ThrowInsInput, nameof(Club.ThrowIns));
            this.BindTeamSkill(this.FreeKicksInput, nameof(Club.FreeKicks));
            this.BindTeamSkill(this.CornersInput, nameof(Club.Corners));

            this.CoTrainerFirstnameInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.CoTrainerFirstname));
            this.CoTrainerLastnameInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.CoTrainerLastname));
            this.BindLevels(this.CoTrainerCompetenceInput, nameof(Club.CoTrainerCompetence), Settings.MaxStaffCompetence);
            this.BindStaffNumber(this.CoTrainerContractInput, nameof(Club.CoTrainerContract), this.ByteMax255_TextChanged);
            this.BindStaffNumber(this.CoTrainerSalaryInput, nameof(Club.CoTrainerSalary), this.IntMaxNumber_TextChanged);
            this.GoalkeeperCoachFirstnameInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.GoalkeeperCoachFirstname));
            this.GoalkeeperCoachLastnameInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.GoalkeeperCoachLastname));
            this.BindLevels(this.GoalkeeperCoachCompetenceInput, nameof(Club.GoalkeeperCoachCompetence), Settings.MaxStaffCompetence);
            this.BindStaffNumber(this.GoalkeeperCoachContractInput, nameof(Club.GoalkeeperCoachContract), this.ByteMax255_TextChanged);
            this.BindStaffNumber(this.GoalkeeperCoachSalaryInput, nameof(Club.GoalkeeperCoachSalary), this.IntMaxNumber_TextChanged);
            // Without a co-/goalkeeper trainer there's no entry to edit.
            foreach (Control input in new Control[] { this.CoTrainerFirstnameInput, this.CoTrainerLastnameInput, this.CoTrainerCompetenceInput, this.CoTrainerContractInput, this.CoTrainerSalaryInput })
                input.DataBindings.Add("Enabled", this.bindingSource, nameof(Club.HasCoTrainer));
            foreach (Control input in new Control[] { this.GoalkeeperCoachFirstnameInput, this.GoalkeeperCoachLastnameInput, this.GoalkeeperCoachCompetenceInput, this.GoalkeeperCoachContractInput, this.GoalkeeperCoachSalaryInput })
                input.DataBindings.Add("Enabled", this.bindingSource, nameof(Club.HasGoalkeeperCoach));

            this.FreeTicketsInput.DataBindings.Add("Text", this.bindingSource, "FreeTickets");
            this.RoadGameSupportInput.DataBindings.Add("Text", this.bindingSource, "RoadGameSupport");

            this.ShortNameInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.ShortName));
            this.ChantInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.Chant));
            this.FanNameInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.FanName));
            this.TownInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.Town));
            this.ArticleInput.DataBindings.Add("SelectedValue", this.bindingSource, nameof(Club.Article));
            this.PrepositionInput.DataBindings.Add("SelectedValue", this.bindingSource, nameof(Club.Preposition));
            this.FanTurnoutInput.DataBindings.Add("SelectedValue", this.bindingSource, nameof(Club.FanTurnout));
            this.FanTypeInput.DataBindings.Add("SelectedValue", this.bindingSource, nameof(Club.FanType));
            this.BoardInput.DataBindings.Add("SelectedValue", this.bindingSource, nameof(Club.Board));
            this.AssociationInput.DataBindings.Add("SelectedValue", this.bindingSource, nameof(Club.AssociationImage));
            this.PublicImageInput.DataBindings.Add("SelectedValue", this.bindingSource, nameof(Club.PublicImageStage));

            this.ShowStadium();
            this.ShowEditorSettings();
            this.BindBudget();
            this.BindAssociation();
        }

        private void BindTeamSkill(ComboBox input, string property) => this.BindLevels(input, property, Club.MaxTeamSkill);

        // Levels 0..max as a drop-down; each one needs its own list, a shared one would sync them.
        private void BindLevels(ComboBox input, string property, byte max)
        {
            input.DataSource = Enumerable.Range(0, max + 1).Select(i => new { Value = (byte)i, Text = i.ToString() }).ToList();
            input.DisplayMember = "Text";
            input.ValueMember = "Value";
            input.DataBindings.Add("SelectedValue", this.bindingSource, property);
        }

        private void BindStaffNumber(TextBox input, string property, EventHandler limit)
        {
            input.KeyPress += this.NumericOnly_KeyPress;
            input.TextChanged += limit;
            input.DataBindings.Add("Text", this.bindingSource, property);
        }

        #region Stadium
        // The stadium tab: the plan (StadiumPlan) selects a block, the "Block" box edits it, the
        // legend sums up the places like the game's stadium screen.
        private void InitStadium()
        {
            this.StadiumPlanView.SelectedBlockChanged += (s, e) => this.ShowBlock();

            this.BlockStandingsInput.KeyPress += this.SignedNumeric_KeyPress;
            this.BlockSeatsInput.KeyPress += this.SignedNumeric_KeyPress;
            this.BlockWeeksInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockStandingsInput.TextChanged += this.Int16Range_TextChanged;
            this.BlockSeatsInput.TextChanged += this.Int16Range_TextChanged;
            this.BlockWeeksInput.TextChanged += this.ByteMax255_TextChanged;

            this.BlockStandingsInput.TextChanged += (s, e) => this.EditBlock(block => this.club.SetStandings(block, ParseInt16(this.BlockStandingsInput.Text)));
            this.BlockSeatsInput.TextChanged += (s, e) => this.EditBlock(block => this.club.SetSeats(block, ParseInt16(this.BlockSeatsInput.Text)));
            this.BlockWeeksInput.TextChanged += (s, e) => this.EditBlock(block => this.club.SetWeeks(block, byte.TryParse(this.BlockWeeksInput.Text, out byte weeks) ? weeks : (byte)0));
            this.BlockRoofInput.CheckedChanged += (s, e) => this.EditBlock(block => this.club.SetRoof(block, this.BlockRoofInput.Checked));
        }

        // Writes one edit of the selected block into the club and redraws plan and legend.
        private void EditBlock(Action<int> edit)
        {
            if (this.showingBlock || this.club == null)
                return;
            edit(this.StadiumPlanView.SelectedBlock);
            this.StadiumPlanView.Invalidate();
            this.UpdateLegend();
        }

        private void ShowStadium()
        {
            this.StadiumPlanView.Club = this.club;
            this.ShowBlock();
        }

        // Fills the "Block" box with the selected block; block K keeps boxes ("Logen") in its standing places.
        private void ShowBlock()
        {
            int block = this.StadiumPlanView.SelectedBlock;
            this.showingBlock = true;
            try
            {
                this.BlockBox.Text = $"Block {Club.BlockLetters[block]}";
                this.BlockStandingsLabel.Text = block == Club.BoxBlock ? "Logen" : "Stehplätze";
                this.BlockStandingsInput.Text = this.club?.GetStandings(block).ToString() ?? "";
                this.BlockSeatsInput.Text = this.club?.GetSeats(block).ToString() ?? "";
                this.BlockWeeksInput.Text = this.club?.GetWeeks(block).ToString() ?? "";
                this.BlockRoofInput.Checked = this.club?.HasRoof(block) ?? false;
            }
            finally
            {
                this.showingBlock = false;
            }
            this.UpdateLegend();
        }

        // Places per kind like the game's legend (blocks under construction included), and the
        // capacity the game sells tickets against once saved (blocks under construction left out).
        private void UpdateLegend()
        {
            int standingOpen = 0, standingRoofed = 0, seatsOpen = 0, seatsRoofed = 0, boxes = 0;
            if (this.club != null)
            {
                for (int block = 0; block < Club.BlockCount; block++)
                {
                    bool roofed = this.club.HasRoof(block);
                    int standing = this.club.GetStandings(block), seats = this.club.GetSeats(block);
                    if (block == Club.BoxBlock)
                        boxes += standing;
                    else if (roofed)
                        standingRoofed += standing;
                    else
                        standingOpen += standing;
                    if (roofed)
                        seatsRoofed += seats;
                    else
                        seatsOpen += seats;
                }
            }
            this.LegendStandingOpenValue.Text = standingOpen.ToString("N0");
            this.LegendStandingRoofedValue.Text = standingRoofed.ToString("N0");
            this.LegendSeatsOpenValue.Text = seatsOpen.ToString("N0");
            this.LegendSeatsRoofedValue.Text = seatsRoofed.ToString("N0");
            this.LegendBoxesValue.Text = boxes.ToString("N0");
            this.CapacityValue.Text = this.club != null ? this.club.OpenCapacity().ToString("N0") : "";
        }

        internal static short ParseInt16(string text) => short.TryParse(text, out short value) ? value : (short)0;
        // Digits, editing keys and one leading minus sign: the game stores places as Int16, so
        // -32768 to 32767 are all valid (the "negative places" trick).
        private void SignedNumeric_KeyPress(object sender, KeyPressEventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            bool minusAllowed = e.KeyChar == '-' && textBox.SelectionStart == 0 && !textBox.Text.Substring(textBox.SelectionLength).Contains("-");
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && !minusAllowed)
                e.Handled = true;
        }

        // Caps the text at the Int16 range, keeping a lone "-" while it's being typed.
        private void Int16Range_TextChanged(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            string text = textBox.Text;
            if (string.IsNullOrEmpty(text) || text == "-" || short.TryParse(text, out _))
                return;
            textBox.Text = text.StartsWith("-") ? short.MinValue.ToString() : short.MaxValue.ToString();
            textBox.SelectionStart = textBox.Text.Length;
        }
        #endregion

        #region Editor settings
        // The settings of the game's data editor: the "Identität" tab (texts, fans, board, rival,
        // fan friendship), the "Trikotsätze" tab (JerseyView) and the "Landkarte" tab (ClubMap).
        // Texts and the enum lists are bound; the club lists, the jerseys and the map are filled
        // by ShowEditorSettings.
        private void InitEditorSettings()
        {
            FillEnum<ClubEnums.Article>(this.ArticleInput);
            FillEnum<ClubEnums.Preposition>(this.PrepositionInput);
            FillEnum<ClubEnums.FanTurnout>(this.FanTurnoutInput);
            FillEnum<ClubEnums.FanType>(this.FanTypeInput);
            FillEnum<ClubEnums.Board>(this.BoardInput);
            FillEnum<ClubEnums.AssociationImage>(this.AssociationInput);
            FillEnum<ClubEnums.PublicImage>(this.PublicImageInput);

            this.ArchRivalInput.SelectionChangeCommitted += (o, e) => this.EditClubChoice(id => this.club.ArchRivalId = id, this.ArchRivalInput);
            this.FanFriendshipInput.SelectionChangeCommitted += (o, e) => this.EditClubChoice(id => this.club.FanFriendshipId = id, this.FanFriendshipInput);

            this.MapXInput.KeyPress += this.SignedNumeric_KeyPress;
            this.MapYInput.KeyPress += this.SignedNumeric_KeyPress;
            this.MapXInput.TextChanged += this.Int16Range_TextChanged;
            this.MapYInput.TextChanged += this.Int16Range_TextChanged;
            this.MapXInput.TextChanged += (o, e) => this.EditMap(() => this.club.MapX = ParseInt16(this.MapXInput.Text));
            this.MapYInput.TextChanged += (o, e) => this.EditMap(() => this.club.MapY = ParseInt16(this.MapYInput.Text));
            this.ClubMapView.PositionChanged += (o, e) => this.ShowMapPosition();
        }

        private static void FillEnum<T>(ComboBox comboBox) where T : struct
        {
            comboBox.DataSource = Enum.GetValues(typeof(T)).Cast<Enum>().Select(v => new { Value = (T)(object)v, Text = ClubEnums.GetDescription(v) }).ToList();
            comboBox.DisplayMember = "Text";
            comboBox.ValueMember = "Value";
        }

        private void EditClubChoice(Action<byte> set, ComboBox comboBox)
        {
            if (this.club == null || !(comboBox.SelectedValue is byte id))
                return;
            set(id);
            this.ClubMapView.Invalidate();
        }

        private void EditMap(Action edit)
        {
            if (this.showingBlock || this.club == null)
                return;
            edit();
            this.ClubMapView.Invalidate();
        }

        // Fills the parts of the tabs that aren't bound: club lists, jerseys, map.
        private void ShowEditorSettings()
        {
            this.showingBlock = true;
            try
            {
                this.JerseyEditor.Club = this.club;
                this.FillClubChoice(this.ArchRivalInput, this.club?.ArchRivalId ?? Club.NoClub);
                this.FillClubChoice(this.FanFriendshipInput, this.club?.FanFriendshipId ?? Club.NoClub);

                bool hasRecord = this.club?.HasRecord ?? false;
                this.ShortNameInput.Enabled = hasRecord;
                this.ChantInput.Enabled = hasRecord;
                this.FanNameInput.Enabled = hasRecord;

                this.ClubMapView.Clubs = this.clubController?.CountryClubs;
                this.ClubMapView.Club = this.club;
            }
            finally
            {
                this.showingBlock = false;
            }
            this.ShowMapPosition();
        }

        // "–" plus every other club of the own country; a stored ID the list doesn't know stays selectable.
        private void FillClubChoice(ComboBox comboBox, byte selected)
        {
            var choices = new[] { new { Value = Club.NoClub, Text = "–" } }.ToList();
            foreach (ClubRecords.Entry entry in this.clubController?.CountryClubs ?? new System.Collections.Generic.List<ClubRecords.Entry>())
            {
                if (this.club == null || entry.Id != this.club.Id)
                    choices.Add(new { Value = entry.Id, Text = entry.Name });
            }
            if (choices.All(c => c.Value != selected))
                choices.Add(new { Value = selected, Text = $"Verein {selected}" });

            comboBox.DataSource = choices;
            comboBox.DisplayMember = "Text";
            comboBox.ValueMember = "Value";
            comboBox.SelectedValue = selected;
        }

        private void ShowMapPosition()
        {
            this.showingBlock = true;
            try
            {
                this.MapXInput.Text = this.club?.MapX.ToString() ?? "";
                this.MapYInput.Text = this.club?.MapY.ToString() ?? "";
            }
            finally
            {
                this.showingBlock = false;
            }
        }
        #endregion

        /// <summary>
        /// Shows the own club (OWN), or the club picked last or else the own club from its record (CLUB);
        /// <paramref name="club"/> re-displays the object just saved instead of reading it again.
        /// </summary>
        internal void RefreshValues(PlayerEnums.AddressType type, Club club = null)
        {
            if (this.IsGameRunning())
            {
                // Clearing empties the block inputs too - that must not reach the club still shown.
                this.showingBlock = true;
                try
                {
                    this.ClearAllFields(this);
                }
                finally
                {
                    this.showingBlock = false;
                }

                this.clubSelection.Clubs = ClubSquads.Clubs(this.Memory, FixedAddresses.For(this.Memory));
                ClubSquads.Entry entry = null;
                if (type == PlayerEnums.AddressType.CLUB || type == PlayerEnums.AddressType.OWN)
                {
                    Club own = new ClubController(this.Memory, this.processController.IsGog, PlayerEnums.AddressType.OWN).Club;
                    ClubSquads.Entry human = this.HumanClubInstead(own);
                    entry = type == PlayerEnums.AddressType.OWN ? human
                        : (this.chosenClub == null ? null : this.clubSelection.Find(this.chosenClub.Country, this.chosenClub.Id))
                            ?? human ?? this.clubSelection.Find(own.Country, own.Id);
                }
                // Without records (or a savegame) "Anderer" falls back to the own club.
                this.clubController = entry != null
                    ? new ClubController(this.Memory, this.processController.IsGog, entry)
                    : new ClubController(this.Memory, this.processController.IsGog, type == PlayerEnums.AddressType.CLUB ? PlayerEnums.AddressType.OWN : type,
                        loadFullList: type == PlayerEnums.AddressType.ALL);
                this.club = club ?? this.clubController.Club;
                // Keep the controller pointed at the exact object the UI is bound to - otherwise a
                // caller-supplied club (e.g. SaveBtn_Click re-displaying what it just saved) leaves
                // clubController.Club as the freshly-read (now stale) instance, and the next Save()
                // silently writes that stale object instead of the user's newest edits.
                this.clubController.Club = this.club;
                if (this.bindingSource != null)
                {
                    this.bindingSource.DataSource = this.club;
                    this.bindingSource.ResetBindings(false);
                }
                this.ShowStadium();
                this.ShowEditorSettings();
                this.ShowBoard();
                this.ShowBudget();
                this.ShowRequirements();
                this.ShowLiveMatch();
                this.clubSelection.Show(this.clubController.RecordClub
                    ?? (string.IsNullOrEmpty(this.club.ClubName) ? null : this.clubSelection.Find(this.club.Country, this.club.Id)));
            }
        }

        // During a match the game points the own-club slot at whichever club its engine handles, the
        // opponent included, or nowhere. Returns the record of the human manager's club to show
        // instead (the one shown last if several play), or null if the slot holds a human's club.
        private ClubSquads.Entry HumanClubInstead(Club own)
        {
            bool gog = this.processController.IsGog;
            if (own.PlayerCount > 0 && CoachController.ForClub(this.Memory, gog, own.Country, own.Id) != null)
            {
                this.lastHumanClub = new KeyValuePair<PlayerEnums.Country, byte>(own.Country, own.Id);
                return null;
            }
            List<Coach> managers = CoachController.GetActiveTrainers(this.Memory, gog, PlayerEnums.AddressType.OWN).Select(t => t.Value).ToList();
            Coach manager = managers.FirstOrDefault(c => this.lastHumanClub.HasValue && c.CurrentCountry == this.lastHumanClub.Value.Key && c.CurrentClubId == this.lastHumanClub.Value.Value)
                ?? managers.FirstOrDefault();
            return manager == null ? null : this.clubSelection.Find(manager.CurrentCountry, manager.CurrentClubId);
        }

        // "Vorstand" box on the "Identität" tab: the board's trust in the manager of the club shown, editable only if a human
        // manager trains it. The same value is on Trainer -> Vertrag; whichever view saves last wins.
        private void ShowBoard()
        {
            this.boardCoachController = this.club == null ? null : CoachController.ForClub(this.Memory, this.processController.IsGog, this.club.Country, this.club.Id);
            Coach manager = this.boardCoachController?.Coach;
            this.ManagerValue.Text = manager == null ? "–" : $"{manager.Firstname} {manager.Lastname}".Trim();
            this.BoardTrustInput.Text = manager?.BoardTrust.ToString() ?? "";
            this.BoardTrustInput.Enabled = manager != null;
        }

        private void SaveBoardTrust()
        {
            if (this.boardCoachController != null && byte.TryParse(this.BoardTrustInput.Text, out byte trust))
            {
                this.boardCoachController.Coach.BoardTrust = trust;
                this.boardCoachController.SaveBoardTrust();
            }
        }

        private void ClubPicked(ClubSquads.Entry club)
        {
            this.ShowClub(club);
            this.ClubChosen?.Invoke(club);
        }

        /// <summary>The club picked last ("Verein -> Anderer"), or null.</summary>
        internal ClubSquads.Entry ChosenClub => this.chosenClub;

        /// <summary>Shows <paramref name="club"/> and makes it the club picked last, as picking it below the tabs does - without raising ClubChosen.</summary>
        internal void ShowClub(ClubSquads.Entry club)
        {
            this.chosenClub = club;
            this.RefreshValues(PlayerEnums.AddressType.CLUB);
        }

        private void ReloadBtn_Click(object sender, System.EventArgs e)
        {
            this.RefreshValues(this.clubController.Type);
        }

        private void SaveBtn_Click(object sender, System.EventArgs e)
        {
            if (this.IsGameRunning())
            {
                this.ApplyLiveMatch();
                this.clubController.Save();
                this.SaveBoardTrust();
                this.SaveRates();
                this.RefreshValues(this.clubController.Type, this.club);
            }
        }
    }
}
