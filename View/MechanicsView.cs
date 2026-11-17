using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using AnstossTrainer.Shared;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// "Spielmechaniken": what works on the running game's code instead of its data and so can't go
    /// into a savegame - the quiz, interview and speech helps, the lifted goal limits, the
    /// overconfidence rule, the requested job offer and, on the "Termine" tab, the appointment locks. The switches are taken back when the trainer closes and are gone with the game.
    /// Built in code, laid out like the other views: one tab, "Werte neuladen" below it.
    /// </summary>
    public class MechanicsView : EntityView
    {
        private readonly TabControl tabControl;
        private readonly Button reloadButton;
        private readonly Label noticeLabel;
        private readonly CheckBox quizHelpInput, interviewDisplayInput, interviewFixInput, goalLimitInput, speechDisplayInput;
        private readonly GroupBox jobOfferBox;
        private readonly ComboBox jobOfferTrainerInput, jobOfferCountryInput, jobOfferClubInput;
        private readonly ComboBox overconfidenceOwnInput, overconfidenceOpponentInput;
        private readonly ComboBox weeklyLimitInput;
        private readonly CheckBox clubLimitInput;
        private readonly Button jobOfferButton;
        private readonly Label jobOfferStatus;

        private readonly QuizHelper quizHelper;
        private readonly InterviewEffectsDisplay interviewDisplay;
        private readonly InterviewEffectsFix interviewFix;
        private readonly GoalLimitPatch goalLimitPatch;
        private readonly OverconfidenceRule overconfidenceRule;
        private readonly SpeechEffectsDisplay speechDisplay;
        private readonly AppointmentLocks appointmentLocks;

        // The managers the job offer can go to (see Trainer.RefreshTrainerMenu) and the one chosen.
        private List<KeyValuePair<int, Coach>> trainers = new List<KeyValuePair<int, Coach>>();
        private Coach coach;
        private int trainerIndex;
        private LeagueSettings league;
        private BindingList<Club> allClubs;
        // Set while the switches are filled from the game, so their handlers don't switch anything.
        private bool showing;
        // Whether the trainer lifted the goal limits itself: only then it restores them on closing,
        // not where A2G-Patch lifted them (exe or msvfw32.dll) and the switch just shows that.
        private bool goalLimitLifted;
        // The same for the interview effects (A2G-Patch puts in the same stub, flag set).
        private bool interviewFixApplied;
        // Whether the trainer lifted the limit per selling club, so it restores it on closing.
        private bool clubLimitLifted;

        private static Label AddLabel(Control box, string text, int x, int y)
        {
            Label label = new Label { AutoSize = true, Location = new Point(x, y + 3), Text = text };
            box.Controls.Add(label);
            return label;
        }

        private static ComboBox AddChoice(Control box, string label, int y)
        {
            return AddChoice(box, label, y, ComboBoxStyle.DropDownList);
        }

        // No optional parameter here: coverlet can't resolve a WinForms-typed default value
        // and then skips instrumenting the whole exe.
        private static ComboBox AddChoice(Control box, string label, int y, ComboBoxStyle style)
        {
            AddLabel(box, label, 7, y);
            ComboBox combo = new ComboBox { DropDownStyle = style, Location = new Point(115, y), Size = new Size(150, 21) };
            if (style == ComboBoxStyle.DropDown)
            {
                combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
                combo.AutoCompleteSource = AutoCompleteSource.ListItems;
            }
            box.Controls.Add(combo);
            return combo;
        }

        /// <summary>Creates the view bound to the given memory accessor and process controller.</summary>
        public MechanicsView(ProcessMemory memory, ProcessController controller) : base(memory, controller)
        {
            this.Name = "MechanicsView";
            this.Size = new Size(630, 423);
            this.Font = new Font("Microsoft Sans Serif", 8.25F);
            this.BackgroundImage = Properties.Resources.TabControl;

            this.quizHelper = new QuizHelper(this.Memory);
            this.interviewDisplay = new InterviewEffectsDisplay(this.Memory);
            this.interviewFix = new InterviewEffectsFix(this.Memory);
            this.goalLimitPatch = new GoalLimitPatch(this.Memory);
            this.overconfidenceRule = new OverconfidenceRule(this.Memory);
            this.appointmentLocks = new AppointmentLocks(this.Memory);
            // The human managers' clubs, as the trainer menu lists them.
            this.speechDisplay = new SpeechEffectsDisplay(this.Memory, () => this.trainers
                .Where(t => t.Value != null && t.Value.CurrentClubId != 0)
                .Select(t => new KeyValuePair<PlayerEnums.Country, ushort>(t.Value.CurrentCountry, t.Value.CurrentClubId))
                .ToList());

            this.tabControl = new TabControl { Name = "MainTabControl", Location = new Point(20, 13), Size = new Size(594, 372) };
            TabPage generalTab = new TabPage { Text = "Allgemein", UseVisualStyleBackColor = true, AutoScroll = true, Padding = new Padding(3) };
            this.tabControl.TabPages.Add(generalTab);

            GroupBox quizBox = new GroupBox { Text = "Fortbildung", Location = new Point(6, 6), Size = new Size(272, 46) };
            this.quizHelpInput = new CheckBox { Text = "Quiz: richtige Antwort markieren", Location = new Point(10, 20), AutoSize = true };
            this.quizHelpInput.CheckedChanged += (s, e) => this.quizHelper.Enabled = this.quizHelpInput.Checked;
            quizBox.Controls.Add(this.quizHelpInput);

            GroupBox interviewBox = new GroupBox { Text = "Interviews", Location = new Point(6, 58), Size = new Size(272, 68) };
            this.interviewDisplayInput = new CheckBox { Text = "Wirkung der Antworten anzeigen", Location = new Point(10, 20), AutoSize = true };
            this.interviewDisplayInput.CheckedChanged += (s, e) => this.interviewDisplay.Enabled = this.interviewDisplayInput.Checked;
            this.interviewFixInput = new CheckBox { Text = "Wirkung der Antworten anwenden", Location = new Point(10, 43), AutoSize = true };
            this.interviewFixInput.CheckedChanged += (s, e) =>
            {
                // Stays unticked where the game's code isn't as expected (see the log).
                if (this.showing)
                    return;
                if (!this.interviewFix.SetEnabled(this.interviewFixInput.Checked))
                    this.interviewFixInput.Checked = false;
                this.interviewFixApplied = this.interviewFixInput.Checked;
            };
            interviewBox.Controls.AddRange(new Control[] { this.interviewDisplayInput, this.interviewFixInput });

            GroupBox engineBox = new GroupBox { Text = "Spielengine", Location = new Point(6, 132), Size = new Size(272, 46) };
            this.goalLimitInput = new CheckBox { Text = "Torgrenzen aufheben", Location = new Point(10, 20), AutoSize = true };
            this.goalLimitInput.CheckedChanged += (s, e) =>
            {
                // Stays unticked where the game's code isn't as expected (see the log).
                if (this.showing)
                    return;
                if (!this.goalLimitPatch.SetEnabled(this.goalLimitInput.Checked))
                    this.goalLimitInput.Checked = false;
                this.goalLimitLifted = this.goalLimitInput.Checked;
            };
            engineBox.Controls.Add(this.goalLimitInput);

            GroupBox overconfidenceBox = new GroupBox { Text = "Überheblichkeit", Location = new Point(6, 184), Size = new Size(272, 76) };
            this.overconfidenceOwnInput = AddChoice(overconfidenceBox, "Eigene Teams", 20);
            this.overconfidenceOpponentInput = AddChoice(overconfidenceBox, "Deren Gegner", 47);
            foreach (ComboBox rule in new[] { this.overconfidenceOwnInput, this.overconfidenceOpponentInput })
            {
                rule.Items.AddRange(OverconfidenceRule.RuleNames.Cast<object>().ToArray());
                rule.SelectedIndex = 0;
                rule.SelectedIndexChanged += (s, e) => this.ApplyOverconfidenceRule();
            }

            GroupBox speechBox = new GroupBox { Text = "Mannschaftssitzung", Location = new Point(284, 184), Size = new Size(272, 76) };
            this.speechDisplayInput = new CheckBox { Text = "Wirkung der Ansprachen anzeigen", Location = new Point(10, 20), AutoSize = true };
            this.speechDisplayInput.CheckedChanged += (s, e) =>
            {
                // Stays unticked where the game's code isn't as expected (see the log).
                if (!this.speechDisplay.SetEnabled(this.speechDisplayInput.Checked))
                    this.speechDisplayInput.Checked = false;
            };
            speechBox.Controls.Add(this.speechDisplayInput);

            this.jobOfferBox = new GroupBox { Text = "Jobangebot anfordern", Location = new Point(284, 6), Size = new Size(272, 172) };
            this.jobOfferTrainerInput = AddChoice(this.jobOfferBox, "Trainer", 20);
            this.jobOfferCountryInput = AddChoice(this.jobOfferBox, "Land", 47);
            this.jobOfferClubInput = AddChoice(this.jobOfferBox, "Verein", 74, ComboBoxStyle.DropDown);
            this.jobOfferButton = new Button { Text = "Beim nächsten Tagesabschluss anbieten", Location = new Point(7, 101), Size = new Size(258, 23), UseVisualStyleBackColor = true };
            this.jobOfferStatus = new Label { Location = new Point(7, 128), Size = new Size(258, 40), Text = "" };
            this.jobOfferBox.Controls.AddRange(new Control[] { this.jobOfferButton, this.jobOfferStatus });
            this.jobOfferTrainerInput.SelectedIndexChanged += (s, e) => this.LoadJobOfferTrainer();
            this.jobOfferCountryInput.SelectedIndexChanged += (s, e) => this.PopulateJobOfferClubCombo();
            this.jobOfferButton.Click += (s, e) => this.RequestJobOffer();
            this.jobOfferBox.Enabled = false;

            // Both columns end at the same height; the notice spans them right below.
            GroupBox noticeBox = new GroupBox { Text = "Hinweis", Location = new Point(6, 266), Size = new Size(550, 46) };
            this.noticeLabel = new Label { Name = "NoticeLabel", AutoSize = true, Location = new Point(7, 21), Text = "Diese Funktionen wirken nur, solange A2G-Trainer und Spiel laufen." };
            noticeBox.Controls.Add(this.noticeLabel);
            generalTab.Controls.AddRange(new Control[] { quizBox, interviewBox, engineBox, overconfidenceBox, this.jobOfferBox, speechBox, noticeBox });

            TabPage appointmentTab = new TabPage { Text = "Termine", UseVisualStyleBackColor = true, AutoScroll = true, Padding = new Padding(3) };
            this.tabControl.TabPages.Add(appointmentTab);
            GroupBox lockBox = new GroupBox { Text = "Terminsperren", Location = new Point(6, 6), Size = new Size(550, 76) };
            AddLabel(lockBox, "Verhandlungen je Woche", 7, 20);
            this.weeklyLimitInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(160, 20), Size = new Size(200, 21) };
            this.weeklyLimitInput.Items.AddRange(AppointmentLocks.WeeklyModeNames.Cast<object>().ToArray());
            this.weeklyLimitInput.SelectedIndex = 0;
            this.weeklyLimitInput.SelectedIndexChanged += (s, e) =>
            {
                // Back to the game's limit where that isn't possible (other build, see the log).
                if (this.showing)
                    return;
                if (!this.appointmentLocks.SetWeeklyMode((byte)this.weeklyLimitInput.SelectedIndex))
                    this.ShowWithoutHandlers(() => this.weeklyLimitInput.SelectedIndex = 0);
            };
            this.clubLimitInput = new CheckBox { Text = "Höchstens 3 Termine je abgebendem Verein aufheben", Location = new Point(10, 49), AutoSize = true };
            this.clubLimitInput.CheckedChanged += (s, e) =>
            {
                // Stays unticked where the game's code isn't as expected (see the log).
                if (this.showing)
                    return;
                if (!this.appointmentLocks.SetClubLimitLifted(this.clubLimitInput.Checked))
                    this.ShowWithoutHandlers(() => this.clubLimitInput.Checked = false);
                this.clubLimitLifted = this.clubLimitInput.Checked;
            };
            lockBox.Controls.AddRange(new Control[] { this.weeklyLimitInput, this.clubLimitInput });
            appointmentTab.Controls.Add(lockBox);

            this.reloadButton = new Button { Name = "ReloadBtn", Text = "Werte neuladen", Location = new Point(20, 391), Size = new Size(114, 23), UseVisualStyleBackColor = true };
            this.reloadButton.Click += (s, e) => this.RefreshValues();

            this.Controls.AddRange(new Control[] { this.reloadButton, this.tabControl });

            this.Disposed += (s, e) =>
            {
                this.quizHelper.Dispose();
                this.interviewDisplay.Dispose();
                this.speechDisplay.Dispose();
                if (this.interviewFixApplied)
                    this.interviewFix.SetEnabled(false);
                if (this.goalLimitLifted)
                    this.goalLimitPatch.SetEnabled(false);
                if (this.overconfidenceOwnInput.SelectedIndex > 0 || this.overconfidenceOpponentInput.SelectedIndex > 0)
                    this.overconfidenceRule.SetRule(0, 0);
                if (this.weeklyLimitInput.SelectedIndex > 0)
                    this.appointmentLocks.SetWeeklyMode(0);
                if (this.clubLimitLifted)
                    this.appointmentLocks.SetClubLimitLifted(false);
            };
        }

        // Changes a switch without its handler acting on the game.
        private void ShowWithoutHandlers(Action change)
        {
            bool showing = this.showing;
            this.showing = true;
            try
            {
                change();
            }
            finally
            {
                this.showing = showing;
            }
        }

        // Sends both rules to the game; where that isn't possible (other build, see the log) both go back to the game's dice.
        private void ApplyOverconfidenceRule()
        {
            if (this.showing)
                return;
            if (this.overconfidenceRule.SetRule(OverconfidenceRule.RuleValue(this.overconfidenceOwnInput.SelectedIndex),
                    OverconfidenceRule.RuleValue(this.overconfidenceOpponentInput.SelectedIndex)))
                return;
            this.showing = true;
            try
            {
                this.overconfidenceOwnInput.SelectedIndex = this.overconfidenceOpponentInput.SelectedIndex = 0;
            }
            finally
            {
                this.showing = false;
            }
        }

        /// <summary>The managers of the savegame (slot, coach), offered for the job offer; keeps the one chosen where it's still there.</summary>
        internal void SetTrainers(List<KeyValuePair<int, Coach>> activeTrainers)
        {
            int? previous = this.jobOfferTrainerInput.SelectedValue as int?;
            this.trainers = activeTrainers;
            List<KeyValuePair<int, string>> items = activeTrainers
                .Select(t => new KeyValuePair<int, string>(t.Key, Trainer.TrainerName(t)))
                .ToList();
            this.jobOfferTrainerInput.DataSource = items;
            this.jobOfferTrainerInput.DisplayMember = "Value";
            this.jobOfferTrainerInput.ValueMember = "Key";
            if (previous.HasValue && items.Any(i => i.Key == previous.Value))
                this.jobOfferTrainerInput.SelectedValue = previous.Value;
        }

        /// <summary>Reads what the switches and the job offer need from the running game; without one only the switches' state is cleared.</summary>
        internal void RefreshValues()
        {
            bool running = this.IsGameRunning(silent: true);
            this.showing = true;
            try
            {
                // The patch lives in the game, which may have been restarted meanwhile.
                this.goalLimitInput.Checked = running && this.goalLimitPatch.IsEnabled;
                this.goalLimitLifted &= this.goalLimitInput.Checked;
                this.interviewFixInput.Checked = running && this.interviewFix.IsEnabled;
                this.interviewFixApplied &= this.interviewFixInput.Checked;
                // The rule lives in the game, which may have been restarted meanwhile.
                byte own = 0, opponent = 0;
                if (running)
                    this.overconfidenceRule.ReadRule(out own, out opponent);
                this.overconfidenceOwnInput.SelectedIndex = OverconfidenceRule.RuleIndex(own);
                this.overconfidenceOpponentInput.SelectedIndex = OverconfidenceRule.RuleIndex(opponent);
                // The locks live in the game too.
                byte weekly = running ? this.appointmentLocks.WeeklyMode : (byte)0;
                this.weeklyLimitInput.SelectedIndex = weekly < AppointmentLocks.WeeklyModeNames.Length ? weekly : 0;
                this.clubLimitInput.Checked = running && this.appointmentLocks.IsClubLimitLifted;
                this.clubLimitLifted &= this.clubLimitInput.Checked;
            }
            finally
            {
                this.showing = false;
            }
            if (!running)
            {
                this.allClubs = null;
                this.league = null;
                this.coach = null;
                this.jobOfferBox.Enabled = false;
                return;
            }

            this.league = new LeagueController(this.Memory, this.processController.IsGog, PlayerEnums.AddressType.OWN).League;
            this.allClubs = new ClubController(this.Memory, this.processController.IsGog, PlayerEnums.AddressType.ALL, loadFullList: true).EntityList;
            this.LoadJobOfferTrainer();
        }

        // Reads the chosen manager again: the offer depends on the manager's current club.
        private void LoadJobOfferTrainer()
        {
            this.trainerIndex = this.jobOfferTrainerInput.SelectedValue is int index ? index : this.trainers.Select(t => t.Key).DefaultIfEmpty(0).First();
            this.coachController = this.allClubs != null && this.IsGameRunning(silent: true)
                ? new CoachController(this.Memory, this.processController.IsGog, PlayerEnums.AddressType.OWN, this.trainerIndex)
                : null;
            this.coach = this.coachController?.Coach;
            this.PopulateJobOfferCountryCombo();
        }

        // Countries are the main + bonus countries (the only ones the game makes offers from), keeping
        // the previous choice. The whole box is disabled where the injector isn't supported (other builds).
        private void PopulateJobOfferCountryCombo()
        {
            PlayerEnums.Country? previous = this.jobOfferCountryInput.SelectedValue as PlayerEnums.Country?;
            List<PlayerEnums.Country> countries = this.league == null ? new List<PlayerEnums.Country>() : new[] { this.league.MainCountry }
                .Concat(this.league.ActiveBonusCountries)
                .Distinct()
                .ToList();

            this.jobOfferCountryInput.DataSource = countries.Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
            this.jobOfferCountryInput.DisplayMember = "Text";
            this.jobOfferCountryInput.ValueMember = "Value";
            if (previous.HasValue && countries.Contains(previous.Value))
                this.jobOfferCountryInput.SelectedValue = previous.Value;

            this.jobOfferBox.Enabled = this.coach != null && new JobOfferInjector(this.Memory).IsSupported;
            this.PopulateJobOfferClubCombo();
        }

        // Same club eligibility as for next season (a club in that country's leagues, only the top
        // league for a bonus country), minus the trainer's own current club.
        private void PopulateJobOfferClubCombo()
        {
            PlayerEnums.Country? country = this.jobOfferCountryInput.SelectedValue as PlayerEnums.Country?;
            List<Club> clubs = !country.HasValue || this.allClubs == null ? new List<Club>() : this.allClubs
                .Where(c => c.Country == country.Value)
                .Where(c => this.coach == null || !(c.Country == this.coach.CurrentCountry && c.Id == this.coach.CurrentClubId))
                .Where(c => CoachController.IsEligibleNextSeasonClub(this.Memory, country.Value, c.Id) != false)
                .OrderBy(c => c.ClubName)
                .ToList();

            this.jobOfferClubInput.DataSource = clubs;
            this.jobOfferClubInput.DisplayMember = "ClubName";
            this.jobOfferClubInput.ValueMember = "Id";
        }

        // No dialog: the outcome goes to the status line inside the box. If the offer could only be
        // seen but not accepted (no clause, no board release, contract not running out), the release
        // clause is set and written right away so the player can actually take the offer.
        private void RequestJobOffer()
        {
            if (this.coach == null || !(this.jobOfferCountryInput.SelectedValue is PlayerEnums.Country country) || !(this.jobOfferClubInput.SelectedItem is Club club))
                return;

            JobOfferInjector.RequestResult result = new JobOfferInjector(this.Memory).Request(this.coach, this.trainerIndex, country, club.Id);
            bool clauseSet = result == JobOfferInjector.RequestResult.Requested && this.coachController != null && this.coachController.EnsureCanNegotiateOffers();
            this.jobOfferStatus.Text = JobOfferMessage(result, club.ClubName, clauseSet);
        }

        internal static string JobOfferMessage(JobOfferInjector.RequestResult result, string clubName, bool clauseSet = false)
        {
            switch (result)
            {
                case JobOfferInjector.RequestResult.Requested:
                    return $"{clubName} bietet beim nächsten Tagesabschluss einen Job an."
                        + (clauseSet ? " Freigabeklausel wurde dafür gesetzt." : "");
                case JobOfferInjector.RequestResult.NotEmployed:
                    return "Trainer ist vereinslos – das Spiel zeigt Angebote nur Trainern mit Verein.";
                case JobOfferInjector.RequestResult.ChangePending:
                    return "Wechsel zur nächsten Saison ist eingetragen – vorher „Kein Wechsel“ einstellen und speichern.";
                case JobOfferInjector.RequestResult.SameClub:
                    return "Das ist der aktuelle Verein des Trainers.";
                case JobOfferInjector.RequestResult.NotSupported:
                    return "Für diese Spielversion nicht verfügbar.";
                default:
                    return "Konnte im Spiel nicht vorbereitet werden, Details in der Logdatei.";
            }
        }
    }
}
