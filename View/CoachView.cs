using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using AnstossTrainer.Shared;

namespace A2G_Trainer.View
{
    /// <summary>
    /// Tab for one trainer/manager slot: identity, stock holdings, and the savegame's shared
    /// league/country configuration. Stock and bonus-country controls are built dynamically
    /// (see BuildStockBoxes/RebuildBonusCountryControls) since their counts are game-data
    /// constants rather than fixed UI.
    /// </summary>
    public partial class CoachView : EntityView
    {
        private const int StockColumns = 2;
        private const int StockBoxWidth = 260;
        private const int StockBoxHeight = 145;
        private const int StockBoxMargin = 6;

        // Top offset for the first row inside AdditionalCountriesBox - needs to clear the GroupBox's
        // own title text (6px alone had the first row's controls overlapping the "Bonusländer"
        // header), matching the ~20-24px other GroupBoxes in this view use for their first child.
        private const int AdditionalCountryRowHeight = 27;
        private const int AdditionalCountryMargin = 20;
        private const int BonusReasonX = 130;

        private Coach coach;
        private LeagueSettings league;
        // Club-IDs are only unique per country, so the club list for a stock has to be
        // re-filtered whenever its own "Land" changes - fetched once per refresh here.
        private BindingList<Club> allClubs;

        // Fixed order the six competency inputs are built/read in - matches the in-memory layout
        // CoachController reads/writes (see CoachController.CompetencyOrder).
        private static readonly CoachEnums.CompetencyKey[] CompetencyOrder = (CoachEnums.CompetencyKey[])Enum.GetValues(typeof(CoachEnums.CompetencyKey));
        // Level selector sits in its own row above the six competency rows - laid out the same way
        // as PersonalBox/LeagueBox's label-left/input-right rows so the box matches the rest of the app.
        private const int CompetencyRowHeight = 27;
        private const int CompetencyFirstRowTop = 20;
        private const int CompetencyLabelX = 7;
        private const int CompetencyInputX = 170;

        private readonly TextBox[] competencyInputs = new TextBox[CompetencyOrder.Length];
        private BindingSource competencyBindingSource;
        private Label competencyTotalLabel;
        // Doubles as two things: (1) the bound control for Coach.Level itself (see the
        // "SelectedValue" binding added in InitClubTabControl - there's no separate Level field in
        // PersonalBox anymore), and (2) the selector for which of the 16 levels' competency
        // distribution is shown/edited below (see ShowCompetencyLevel). Changing it therefore both
        // promotes/demotes the coach AND switches which stored distribution is being edited - the
        // game keeps all 16 levels' distributions regardless of which one is active (see
        // Coach.CompetencyLevels), so this always lands on a valid, already-stored 6-point split.
        private ComboBox competencyLevelInput;

        private readonly GroupBox[] stockBoxes = new GroupBox[Coach.MaxStocks];
        private readonly ComboBox[] stockCountryCombos = new ComboBox[Coach.MaxStocks];
        private readonly ComboBox[] stockClubCombos = new ComboBox[Coach.MaxStocks];
        private readonly TextBox[] stockSharesInputs = new TextBox[Coach.MaxStocks];
        private readonly TextBox[] stockPriceInputs = new TextBox[Coach.MaxStocks];
        private readonly BindingSource[] stockBindingSources = new BindingSource[Coach.MaxStocks];

        // Kept so PopulateStockCountryCombo can detach/reattach it around a DataSource reassignment -
        // WinForms fires SelectedIndexChanged immediately for the new DataSource's default (first)
        // item, and since the handler writes the model, that transient firing would otherwise
        // overwrite Stock.Country (and, via the club-combo cascade, Stock.ClubId) with the wrong
        // value before the real selection gets applied.
        private readonly EventHandler[] stockCountryChangedHandlers = new EventHandler[Coach.MaxStocks];

        // Same detach/reattach rationale as stockCountryChangedHandlers above, for the single
        // "Nächste Saison" country combo (see PopulateNextSeasonCountryCombo).
        private EventHandler nextSeasonCountryChangedHandler;

        // One "aktiv" checkbox per league country other than the main country, in
        // LeagueSettings.BonusCountries order - rebuilt in RebuildBonusCountryControls on every refresh.
        private readonly List<CheckBox> bonusCountryChecks = new List<CheckBox>();
        // Countries whose change the last save had to skip (see LeagueController.Save), marked next
        // to their checkbox until the next save.
        private List<PlayerEnums.Country> skippedBonusCountries = new List<PlayerEnums.Country>();
        private BindingSource leagueBindingSource;

        private LeagueController leagueController;

        /// <summary>Creates the coach tab view bound to the given memory accessor and process controller, and builds its dynamic stock/competency controls.</summary>
        public CoachView(ProcessMemory memory, ProcessController controller) : base(memory, controller)
        {
            InitializeComponent();
            this.BuildStockBoxes();
            this.BuildFriendlyRows();
            this.BuildCompetencyBox();
            this.BuildCareerTab();
        }

        // The game caps stock holdings at Coach.MaxStocks slots - built here in a loop instead of
        // as static Designer boilerplate, since the count is a game-data constant, not a fixed UI.
        private void BuildStockBoxes()
        {
            for (int i = 0; i < Coach.MaxStocks; i++)
            {
                int col = i % StockColumns;
                int row = i / StockColumns;

                GroupBox box = new GroupBox
                {
                    Text = $"Aktie {i + 1}",
                    Location = new System.Drawing.Point(StockBoxMargin + col * (StockBoxWidth + StockBoxMargin), StockBoxMargin + row * (StockBoxHeight + StockBoxMargin)),
                    Size = new System.Drawing.Size(StockBoxWidth, StockBoxHeight)
                };

                Label countryLabel = new Label { AutoSize = true, Location = new System.Drawing.Point(7, 23), Text = "Land" };
                ComboBox countryCombo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new System.Drawing.Point(90, 20),
                    Size = new System.Drawing.Size(150, 21)
                };

                Label clubLabel = new Label { AutoSize = true, Location = new System.Drawing.Point(7, 54), Text = "Verein" };
                // DropDown (editable) + AutoCompleteSource.ListItems gives a type-to-filter dropdown
                // out of the box - no need for a custom/searchable combo control.
                ComboBox clubCombo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDown,
                    AutoCompleteMode = AutoCompleteMode.SuggestAppend,
                    AutoCompleteSource = AutoCompleteSource.ListItems,
                    Location = new System.Drawing.Point(90, 51),
                    Size = new System.Drawing.Size(150, 21)
                };

                Label sharesLabel = new Label { AutoSize = true, Location = new System.Drawing.Point(7, 85), Text = "Anzahl Aktien" };
                TextBox sharesInput = new TextBox
                {
                    Location = new System.Drawing.Point(120, 82),
                    Size = new System.Drawing.Size(60, 20),
                    TextAlign = HorizontalAlignment.Right
                };

                Label priceLabel = new Label { AutoSize = true, Location = new System.Drawing.Point(7, 116), Text = "Kaufpreis" };
                TextBox priceInput = new TextBox
                {
                    Location = new System.Drawing.Point(120, 113),
                    Size = new System.Drawing.Size(60, 20),
                    TextAlign = HorizontalAlignment.Right
                };

                box.Controls.Add(countryLabel);
                box.Controls.Add(countryCombo);
                box.Controls.Add(clubLabel);
                box.Controls.Add(clubCombo);
                box.Controls.Add(sharesLabel);
                box.Controls.Add(sharesInput);
                box.Controls.Add(priceLabel);
                box.Controls.Add(priceInput);

                this.StocksTab.Controls.Add(box);

                this.stockBoxes[i] = box;
                this.stockCountryCombos[i] = countryCombo;
                this.stockClubCombos[i] = clubCombo;
                this.stockSharesInputs[i] = sharesInput;
                this.stockPriceInputs[i] = priceInput;
            }
        }

        // The trainer-competency table is a game-data constant (6 competencies, 16 levels) rather
        // than fixed UI, so it's built here in a loop, same rationale as BuildStockBoxes above.
        // Lives in CompetencyBox on the Allgemein tab (label-left/input-right rows, matching
        // PersonalBox/LeagueBox) and shows one level's 6-point distribution at a time, picked via
        // competencyLevelInput.
        private void BuildCompetencyBox()
        {
            Label levelLabel = new Label { AutoSize = true, Location = new System.Drawing.Point(CompetencyLabelX, CompetencyFirstRowTop + 3), Text = "Level" };
            this.competencyLevelInput = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new System.Drawing.Point(CompetencyInputX, CompetencyFirstRowTop),
                Size = new System.Drawing.Size(50, 21)
            };
            // A DataSource of plain bytes (no ValueMember) works fine for manual SelectedItem/
            // SelectedIndexChanged handling, but a real "SelectedValue" Binding (added in
            // InitClubTabControl, for Coach.Level) throws InvalidOperationException ("SelectedValue
            // kann in einem ListControl nicht mit einem leeren ValueMember festgelegt werden") the
            // moment the control's BindingContext is attached - i.e. as soon as this tab is shown -
            // which crashed the whole trainer window on tab switch. Wrapping in the same Value/Text
            // shape (and DisplayMember/ValueMember) as DifficultyInput/MainCountryInput below avoids that.
            this.competencyLevelInput.DataSource = Enumerable.Range(Coach.MinLevel, Coach.LevelCount).Select(l => new { Value = (byte)l, Text = l.ToString() }).ToList();
            this.competencyLevelInput.DisplayMember = "Text";
            this.competencyLevelInput.ValueMember = "Value";
            this.competencyLevelInput.SelectedIndexChanged += (s, e) =>
            {
                if (this.competencyLevelInput.SelectedValue is byte level)
                    this.ShowCompetencyLevel(level);
            };

            this.CompetencyBox.Controls.Add(levelLabel);
            this.CompetencyBox.Controls.Add(this.competencyLevelInput);

            this.competencyBindingSource = new BindingSource();

            for (int i = 0; i < CompetencyOrder.Length; i++)
            {
                // Row 0 is the level selector above, so the competency rows start one row further down.
                int top = CompetencyFirstRowTop + (i + 1) * CompetencyRowHeight;

                Label nameLabel = new Label
                {
                    AutoSize = true,
                    Location = new System.Drawing.Point(CompetencyLabelX, top + 3),
                    Text = PlayerEnums.GetDescription(CompetencyOrder[i])
                };
                TextBox valueInput = new TextBox
                {
                    Location = new System.Drawing.Point(CompetencyInputX, top),
                    Size = new System.Drawing.Size(50, 20),
                    TextAlign = HorizontalAlignment.Right
                };
                valueInput.KeyPress += this.NumericOnly_KeyPress;
                valueInput.TextChanged += this.UshortMaxNumber_TextChanged;
                valueInput.DataBindings.Add("Text", this.competencyBindingSource, CompetencyOrder[i].ToString());

                this.CompetencyBox.Controls.Add(nameLabel);
                this.CompetencyBox.Controls.Add(valueInput);
                this.competencyInputs[i] = valueInput;
            }

            this.competencyTotalLabel = new Label
            {
                AutoSize = true,
                Location = new System.Drawing.Point(CompetencyLabelX, CompetencyFirstRowTop + (CompetencyOrder.Length + 1) * CompetencyRowHeight + 10)
            };
            Binding totalBinding = new Binding("Text", this.competencyBindingSource, "Total");
            // The pool to distribute isn't a flat 6 - it's 6 points per level actually reached, i.e.
            // 6 x the level shown in competencyLevelInput (Level 0 -> 0 Punkte, Level 1 -> 6, ...,
            // Level 15 -> 90), so this has to be recomputed for whichever level is currently selected
            // rather than using a single constant.
            totalBinding.Format += (s, e) =>
            {
                int level = this.competencyLevelInput.SelectedValue is byte lvl ? lvl : 0;
                int expectedTotal = CoachCompetencyLevel.PointsPerLevel * level;
                e.Value = $"Punkte verteilt: {e.Value}/{expectedTotal}";
            };
            this.competencyTotalLabel.DataBindings.Add(totalBinding);

            this.CompetencyBox.Controls.Add(this.competencyTotalLabel);
        }

        // Points competencyBindingSource (and therefore the six inputs + total label) at one of the
        // coach's 16 stored levels, without touching Coach.Level itself.
        private void ShowCompetencyLevel(int level)
        {
            if (this.coach == null || level < Coach.MinLevel || level > Coach.MaxLevel)
                return;

            this.competencyBindingSource.DataSource = this.coach.CompetencyLevels[level];
            this.competencyBindingSource.ResetBindings(false);
        }

        internal void InitClubTabControl()
        {
            this.bindingSource = new BindingSource
            {
                DataSource = this.coach
            };

            this.AgeInput.KeyPress += this.NumericOnly_KeyPress;
            this.WealthInput.KeyPress += this.NumericOnly_KeyPress;
            this.GamesInput.KeyPress += this.NumericOnly_KeyPress;
            this.WinsInput.KeyPress += this.NumericOnly_KeyPress;

            this.AgeInput.TextChanged += this.ByteMax255_TextChanged;
            this.WealthInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.GamesInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.WinsInput.TextChanged += this.UshortMaxNumber_TextChanged;

            // Wired before the Level binding below on purpose: RefreshView (ProcessController) swallows
            // any exception InitClubTabControl throws and just logs it, which previously left every
            // binding added AFTER the old LevelInput's setup never wired up at all (Vorname/Nachname/
            // Alter/Schwierigkeit silently stopped working) if that setup ever threw. Setting these up
            // first means they still work even if something below them fails.
            this.LastNameInput.DataBindings.Add("Text", this.bindingSource, "Lastname");
            this.FirstNameInput.DataBindings.Add("Text", this.bindingSource, "Firstname");
            this.AgeInput.DataBindings.Add("Text", this.bindingSource, "Age");
            this.WealthInput.DataBindings.Add("Text", this.bindingSource, "Wealth");
            this.GamesInput.DataBindings.Add("Text", this.bindingSource, "Games");
            this.WinsInput.DataBindings.Add("Text", this.bindingSource, "Wins");

            Binding winPercentageBinding = new Binding("Text", this.bindingSource, "WinPercentage");
            winPercentageBinding.Format += (s, e) => e.Value = $"{(double)e.Value:0.0} %";
            this.WinPercentageValue.DataBindings.Add(winPercentageBinding);

            this.DifficultyInput.DataSource = Enum.GetValues(typeof(CoachEnums.Difficulty)).Cast<CoachEnums.Difficulty>().Select(d => new { Value = d, Text = PlayerEnums.GetDescription(d) }).ToList();
            this.DifficultyInput.DisplayMember = "Text";
            this.DifficultyInput.ValueMember = "Value";
            this.DifficultyInput.DataBindings.Add("SelectedValue", this.bindingSource, "Difficulty");

            // No separate Level field in PersonalBox anymore - the level selector inside CompetencyBox
            // (competencyLevelInput, built in BuildCompetencyBox) doubles as the bound control for
            // Coach.Level, since it already has to show the coach's current level by default anyway.
            this.competencyLevelInput.DataBindings.Add("SelectedValue", this.bindingSource, "Level");

            // Only builds with known fixed addresses have the image (see CoachController.ImageAddress).
            BindEnumCombo<CoachEnums.ManagerImage>(this.ImageInput, nameof(Coach.Image));
            this.ImageBox.Enabled = this.coach?.HasImage ?? false;

            // "Nächste Saison" country/club (Coach.CountryNextSeason/ClubIdNextSeason). Choices here
            // are restricted to the main + bonus countries, same as the stock country combos below -
            // a manager can only take over a club from one of those, not literally any country in the
            // game. That restriction means this combo's DataSource has to be rebuilt on every refresh
            // (bonus countries can change), so - same reason as the stock country combo - this can't
            // be a plain DataBindings "SelectedValue" binding: the binding's own re-push races with
            // the DataSource rebuild and can silently leave the combo on the new list's default (first)
            // item while the model still holds the old value. Written directly in the handler instead,
            // see PopulateNextSeasonCountryCombo for the same detach/reattach dance PopulateStock-
            // CountryCombo uses to guard against the DataSource-reassignment's own transient firing.
            EventHandler nextSeasonCountryChanged = (s, e) =>
            {
                if (this.coach != null && this.NextSeasonCountryInput.SelectedValue is PlayerEnums.Country country)
                {
                    this.coach.CountryNextSeason = country;
                    if (country == NoNextSeasonCountry)
                        this.PopulateNoNextSeasonClubCombo();
                    else
                        this.PopulateNextSeasonClubCombo(country);
                }
            };
            this.nextSeasonCountryChangedHandler = nextSeasonCountryChanged;
            this.NextSeasonCountryInput.SelectedIndexChanged += nextSeasonCountryChanged;

            this.NextSeasonClubInput.SelectedIndexChanged += (s, e) =>
            {
                if (this.coach != null && this.NextSeasonClubInput.SelectedItem is Club club)
                    this.coach.ClubIdNextSeason = club.Id;
            };
            this.PopulateNextSeasonCountryCombo(this.coach?.CountryNextSeason ?? PlayerEnums.Country.Sonstige);

            // Freigabeklausel and its announcement are two bits of one byte (see Coach.HasReleaseClause/
            // IsReleaseClausePublished), each bound to its own checkbox. Both properties raise change
            // notifications for each other, so ticking "bekanntgegeben" also ticks the clause and
            // unticking the clause also unticks the announcement. OnPropertyChanged pushes the click
            // into the model right away, so the other box follows immediately, not on focus loss.
            this.ReleaseClauseInput.DataBindings.Add("Checked", this.bindingSource, nameof(Coach.HasReleaseClause), false, DataSourceUpdateMode.OnPropertyChanged);
            this.ReleaseClausePublishedInput.DataBindings.Add("Checked", this.bindingSource, nameof(Coach.IsReleaseClausePublished), false, DataSourceUpdateMode.OnPropertyChanged);
            this.BoardReleaseInput.DataBindings.Add("Checked", this.bindingSource, nameof(Coach.HasBoardRelease), false, DataSourceUpdateMode.OnPropertyChanged);

            this.InitContractTab();
            this.InitNationalTab();
            this.InitStandingTab();

            this.UpdateCurrentClubDisplay();

            for (int i = 0; i < Coach.MaxStocks; i++)
            {
                int slot = i;
                ComboBox countryCombo = this.stockCountryCombos[i];
                ComboBox clubCombo = this.stockClubCombos[i];

                // Country is written directly here rather than through a DataBindings "SelectedValue"
                // binding - that binding's own re-push (via ResetBindings on every refresh) raced with
                // PopulateStockCountryCombo's explicit selection and could silently leave the combo on
                // its DataSource's default (first) item, which then cascaded a wrong country into the
                // club combo below (the club field being reported as "reset to the first Hauptland
                // club" after every save). Club-IDs repeat per country, so the club combo can't bind
                // to Stock.ClubId directly either - it has to show only the selected country's clubs,
                // keyed by Id under the hood.
                EventHandler countryChanged = (s, e) =>
                {
                    if (this.coach != null && countryCombo.SelectedValue is PlayerEnums.Country country)
                    {
                        Stock stock = this.coach.Stocks[slot];
                        stock.Country = country;
                        if (country == PlayerEnums.Country.Sonstige)
                        {
                            stock.ClubId = 0;
                            ShowPlaceholder(clubCombo);
                        }
                        else
                        {
                            this.PopulateStockClubs(clubCombo, country, stock.ClubId);
                        }
                        this.UpdateStockSlotAvailability();
                    }
                };
                this.stockCountryChangedHandlers[i] = countryChanged;
                countryCombo.SelectedIndexChanged += countryChanged;

                clubCombo.SelectedIndexChanged += (s, e) =>
                {
                    if (this.coach != null && clubCombo.SelectedItem is Club club)
                        this.coach.Stocks[slot].ClubId = club.Id;
                };

                // Stocks can only be bought in clubs from the savegame's main country or one of its
                // bonus countries - see PopulateStockCountryCombo.
                this.PopulateStockCountryCombo(i, this.coach?.Stocks[i].Country ?? PlayerEnums.Country.Sonstige);

                this.stockSharesInputs[i].KeyPress += this.NumericOnly_KeyPress;
                this.stockPriceInputs[i].KeyPress += this.NumericOnly_KeyPress;
                this.stockSharesInputs[i].TextChanged += this.IntMaxNumber_TextChanged;
                this.stockPriceInputs[i].TextChanged += this.UshortMaxNumber_TextChanged;

                BindingSource stockSource = new BindingSource { DataSource = this.coach?.Stocks[i] };
                this.stockBindingSources[i] = stockSource;

                this.stockSharesInputs[i].DataBindings.Add("Text", stockSource, "Shares");
                this.stockPriceInputs[i].DataBindings.Add("Text", stockSource, "Price");
            }

            // A free slot gets a new position by picking a country and club; "–" (or 0 shares)
            // clears it on save (see CoachController.Save).
            this.UpdateStockSlotAvailability();

            this.leagueBindingSource = new BindingSource { DataSource = this.league };

            // Shown only: the game loads the main country's leagues into its league system at the
            // start of a new game, so changing it mid-game isn't supported (see LeagueController).
            this.MainCountryInput.DataSource = LeagueSettings.LeagueCountries.Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
            this.MainCountryInput.DisplayMember = "Text";
            this.MainCountryInput.ValueMember = "Value";
            this.MainCountryInput.DataBindings.Add("SelectedValue", this.leagueBindingSource, "MainCountry", false, DataSourceUpdateMode.Never);
            this.MainCountryInput.Enabled = false;

            // RefreshValues only rebuilds the bonus-country controls once leagueBindingSource exists
            // (set a few lines up) - this call covers the very first display, which would otherwise
            // stay empty until the next background reconnect refresh.
            this.RebuildBonusCountryControls();
            this.InitCareerTab();
        }

        // "Vertrag" tab: the current contract's terms (see Coach.Salary .. Coach.Housing). The amounts
        // are Int32 in the game, the years left a UInt16.
        private void InitContractTab()
        {
            foreach (TextBox amountInput in new[] { this.SalaryInput, this.PointBonusInput, this.CupRoundBonusInput, this.TitleBonusInput, this.SeasonGoalBonusInput })
            {
                amountInput.KeyPress += this.NumericOnly_KeyPress;
                amountInput.TextChanged += this.IntMaxNumber_TextChanged;
            }
            this.ContractYearsInput.KeyPress += this.NumericOnly_KeyPress;
            this.ContractYearsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BoardTrustInput.KeyPress += this.NumericOnly_KeyPress;
            this.BoardTrustInput.TextChanged += this.BoardTrustMax_TextChanged;

            this.SalaryInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.Salary));
            this.PointBonusInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.PointBonus));
            this.CupRoundBonusInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.CupRoundBonus));
            this.TitleBonusInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.TitleBonus));
            this.SeasonGoalBonusInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.SeasonGoalBonus));
            this.ContractYearsInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.ContractYearsLeft));
            this.BoardTrustInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.BoardTrust));

            BindEnumCombo<CoachEnums.SeasonGoal>(this.SeasonGoalInput, nameof(Coach.SeasonGoal));
            BindEnumCombo<CoachEnums.VehiclePerk>(this.VehiclePerkInput, nameof(Coach.Vehicle));
            BindEnumCombo<CoachEnums.HousingPerk>(this.HousingPerkInput, nameof(Coach.Housing));

            this.ExtensionOptionInput.DataBindings.Add("Checked", this.bindingSource, nameof(Coach.HasExtensionOption), false, DataSourceUpdateMode.OnPropertyChanged);
        }

        // "Ansehen" tab: nerves and the three standings (0..100 each, the game clamps them), and the
        // champion bets of the Wettbüro, one per league country, edited one country at a time.
        private BindingSource championTipBindingSource;

        private void InitStandingTab()
        {
            foreach (TextBox input in new[] { this.NervesInput, this.PublicStandingInput, this.FanStandingInput, this.MediaStandingInput })
            {
                input.KeyPress += this.NumericOnly_KeyPress;
                input.TextChanged += this.BoardTrustMax_TextChanged;
            }
            this.NervesInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.Nerves));
            this.PublicStandingInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.PublicStanding));
            this.FanStandingInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.FanStanding));
            this.MediaStandingInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.MediaStanding));

            this.TipStakeInput.KeyPress += this.NumericOnly_KeyPress;
            this.TipStakeInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.TipOddsInput.KeyPress += this.NumericOnly_KeyPress;
            this.championTipBindingSource = new BindingSource { DataSource = typeof(ChampionTip) };
            this.TipStakeInput.DataBindings.Add("Text", this.championTipBindingSource, nameof(ChampionTip.Stake));
            this.TipOddsInput.DataBindings.Add("Text", this.championTipBindingSource, nameof(ChampionTip.Odds));
            this.TipCountryInput.SelectedIndexChanged += (s, e) => this.ShowChampionTip();
            this.TipClubInput.SelectionChangeCommitted += (s, e) =>
            {
                if (this.championTipBindingSource.Current is ChampionTip tip && this.TipClubInput.SelectedValue is ushort club)
                    tip.ClubId = club;
            };
            this.ShowChampionTips();
        }

        // The countries with a bet block (the league countries); without known fixed addresses none.
        private void ShowChampionTips()
        {
            if (this.championTipBindingSource == null)
                return;
            PlayerEnums.Country? previous = this.TipCountryInput.SelectedValue as PlayerEnums.Country?;
            List<PlayerEnums.Country> countries = this.coach?.ChampionTips.Keys.ToList() ?? new List<PlayerEnums.Country>();
            this.TipCountryInput.DataSource = countries.Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
            this.TipCountryInput.DisplayMember = "Text";
            this.TipCountryInput.ValueMember = "Value";
            if (previous.HasValue && countries.Contains(previous.Value))
                this.TipCountryInput.SelectedValue = previous.Value;
            this.ChampionTipBox.Enabled = countries.Count > 0;
            this.ShowChampionTip();
        }

        private void ShowChampionTip()
        {
            ChampionTip tip = this.coach != null && this.TipCountryInput.SelectedValue is PlayerEnums.Country country && this.coach.ChampionTips.TryGetValue(country, out ChampionTip found) ? found : null;
            this.championTipBindingSource.DataSource = (object)tip ?? typeof(ChampionTip);
            this.championTipBindingSource.ResetBindings(false);

            // The tip's club among the country's clubs; a stored ID the list doesn't know stays selectable.
            var clubs = (tip == null || this.allClubs == null ? new List<Club>() : this.allClubs.Where(c => c.Country == tip.Country).OrderBy(c => c.ClubName).ToList())
                .Select(c => new { Value = (ushort)c.Id, Text = c.ClubName }).ToList();
            if (tip != null && clubs.All(c => c.Value != tip.ClubId))
                clubs.Insert(0, new { Value = tip.ClubId, Text = $"Verein {tip.ClubId}" });
            this.TipClubInput.DataSource = clubs;
            this.TipClubInput.DisplayMember = "Text";
            this.TipClubInput.ValueMember = "Value";
            if (tip != null)
                this.TipClubInput.SelectedValue = tip.ClubId;
        }

        // "Nationalmannschaft" tab: which national team the coach trains and that contract. The
        // country combo leaves out national teams other trainers already have, so it's rebuilt on
        // every refresh and written directly, like the next-season country combo.
        private void InitNationalTab()
        {
            foreach (TextBox amountInput in new[] { this.NationalSalaryInput, this.NationalRoundBonusInput })
            {
                amountInput.KeyPress += this.NumericOnly_KeyPress;
                amountInput.TextChanged += this.IntMaxNumber_TextChanged;
            }
            this.NationalSalaryInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.NationalSalary));
            this.NationalRoundBonusInput.DataBindings.Add("Text", this.bindingSource, nameof(Coach.NationalRoundBonus));

            this.NationalTeamInput.SelectedIndexChanged += (s, e) =>
            {
                if (this.coach != null && this.NationalTeamInput.SelectedValue is PlayerEnums.Country country)
                {
                    this.coach.NationalTeam = country;
                    this.UpdateNationalContractAvailability();
                }
            };
            this.PopulateNationalTeamCombo();
            this.ShowFriendlies();
            this.InitNationalSquadBox();
        }

        // "Nationalkader": the squad of any country, independent of the trainer slot - it starts on
        // the coach's own national team. Changes stay in the lists until "Kader speichern".
        private readonly BindingList<NationalSquads.Member> nationalSquad = new BindingList<NationalSquads.Member>();
        private readonly BindingList<NationalSquads.Member> nationalCandidates = new BindingList<NationalSquads.Member>();

        private void InitNationalSquadBox()
        {
            this.NationalSquadList.DataSource = this.nationalSquad;
            this.NationalCandidatesList.DataSource = this.nationalCandidates;
            this.nationalSquad.ListChanged += (s, e) => this.UpdateNationalSquadButtons();
            this.nationalCandidates.ListChanged += (s, e) => this.UpdateNationalSquadButtons();

            PlayerEnums.Country start = this.coach != null && this.coach.IsNationalCoach ? this.coach.NationalTeam : this.league?.MainCountry ?? PlayerEnums.Country.Deutschland;
            this.NationalSquadCountryInput.DataSource = NationalTeamChoices(new PlayerEnums.Country[0]).Skip(1).Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
            this.NationalSquadCountryInput.DisplayMember = "Text";
            this.NationalSquadCountryInput.ValueMember = "Value";
            this.NationalSquadCountryInput.SelectedValue = start;
            this.NationalSquadCountryInput.SelectedIndexChanged += (s, e) => this.LoadNationalSquad();

            this.NationalSquadAddBtn.Click += (s, e) => MoveSelected(this.NationalCandidatesList, this.nationalCandidates, this.nationalSquad, Settings.NationalTeamSquadCapacity);
            this.NationalSquadRemoveBtn.Click += (s, e) => MoveSelected(this.NationalSquadList, this.nationalSquad, this.nationalCandidates);
            this.NationalSquadSaveBtn.Click += (s, e) => this.SaveNationalSquad();
            this.LoadNationalSquad();
        }

        private PlayerEnums.Country? NationalSquadCountry => this.NationalSquadCountryInput.SelectedValue as PlayerEnums.Country?;

        private void LoadNationalSquad()
        {
            this.NationalSquadStatus.Text = "";

            FixedAddresses fixedAddresses = this.coach != null && this.IsGameRunning() ? FixedAddresses.For(this.Memory) : null;
            this.NationalSquadBox.Enabled = fixedAddresses != null;
            List<NationalSquads.Member> squad = new List<NationalSquads.Member>();
            List<NationalSquads.Member> candidates = new List<NationalSquads.Member>();
            if (fixedAddresses != null && this.NationalSquadCountry is PlayerEnums.Country country)
            {
                List<ushort> ids = NationalSquads.SquadIds(this.Memory, fixedAddresses, country);
                squad = NationalSquads.Members(this.Memory, fixedAddresses, ids);
                HashSet<ushort> inSquad = new HashSet<ushort>(ids);
                candidates = NationalSquads.PlayersOfNationality(this.Memory, fixedAddresses, country).Where(p => !inSquad.Contains(p.Id)).ToList();
            }
            Fill(this.nationalSquad, squad);
            Fill(this.nationalCandidates, candidates);
        }

        // A big country has a few thousand players: one list update for all of them, not one per player.
        private static void Fill(BindingList<NationalSquads.Member> list, IEnumerable<NationalSquads.Member> members)
        {
            list.RaiseListChangedEvents = false;
            list.Clear();
            foreach (NationalSquads.Member member in members)
                list.Add(member);
            list.RaiseListChangedEvents = true;
            list.ResetBindings();
        }

        private static void MoveSelected(ListBox list, BindingList<NationalSquads.Member> from, BindingList<NationalSquads.Member> to, int capacity = int.MaxValue)
        {
            if (!(list.SelectedItem is NationalSquads.Member member) || to.Count >= capacity)
                return;
            from.Remove(member);
            to.Add(member);
        }

        private void UpdateNationalSquadButtons()
        {
            this.NationalSquadLabel.Text = $"Kader ({this.nationalSquad.Count}/{Settings.NationalTeamSquadCapacity})";
            this.NationalSquadAddBtn.Enabled = this.nationalCandidates.Count > 0 && this.nationalSquad.Count < Settings.NationalTeamSquadCapacity;
            this.NationalSquadRemoveBtn.Enabled = this.nationalSquad.Count > 0;
        }

        private void SaveNationalSquad()
        {
            if (!(this.NationalSquadCountry is PlayerEnums.Country country) || !this.IsGameRunning())
                return;

            NationalSquads.WriteResult result = NationalSquads.Write(this.Memory, FixedAddresses.For(this.Memory), country, this.nationalSquad);
            if (result == NationalSquads.WriteResult.Written)
                this.LoadNationalSquad();
            this.NationalSquadStatus.Text = NationalSquadMessage(result, PlayerEnums.GetDescription(country));
        }

        internal static string NationalSquadMessage(NationalSquads.WriteResult result, string country)
        {
            switch (result)
            {
                case NationalSquads.WriteResult.Written:
                    return $"Kader von {country} gespeichert.";
                case NationalSquads.WriteResult.Fetched:
                    return "Das Spiel bearbeitet die Mannschaft gerade – später erneut speichern.";
                case NationalSquads.WriteResult.TooMany:
                    return $"Höchstens {Settings.NationalTeamSquadCapacity} Spieler.";
                default:
                    return "Für diese Spielversion nicht verfügbar.";
            }
        }

        private void PopulateNationalTeamCombo()
        {
            List<PlayerEnums.Country> taken = this.coach == null ? new List<PlayerEnums.Country>() : CoachController.NationalTeamsOfOtherTrainers(this.Memory, this.trainerIndex);
            // Assigning the DataSource selects its first entry, which the handler writes into the model.
            PlayerEnums.Country selected = this.coach?.NationalTeam ?? PlayerEnums.Country.Sonstige;
            this.NationalTeamInput.DataSource = NationalTeamChoices(taken).Select(c => new { Value = c, Text = c == PlayerEnums.Country.Sonstige ? "keine" : PlayerEnums.GetDescription(c) }).ToList();
            this.NationalTeamInput.DisplayMember = "Text";
            this.NationalTeamInput.ValueMember = "Value";
            if (this.coach != null)
            {
                this.NationalTeamInput.SelectedValue = selected;
                this.coach.NationalTeam = selected;
            }

            this.NationalCoachBox.Enabled = this.coach?.HasNationalTeam ?? false;
            this.NationalCoachStatus.Text = NationalCoachMessage(this.coach);
            this.UpdateNationalContractAvailability();
        }

        internal static string NationalCoachMessage(Coach coach)
        {
            if (coach == null || !coach.IsNationalTeamPending)
                return "";
            return coach.IsNationalCoach
                ? $"Das Spiel übernimmt {PlayerEnums.GetDescription(coach.NationalTeam)} beim nächsten Tagesabschluss."
                : "Das Spiel beendet das Amt beim nächsten Tagesabschluss.";
        }

        /// <summary>"keine" first, then every country with a national team (all but Sonstige) other trainers don't already train, alphabetically.</summary>
        internal static List<PlayerEnums.Country> NationalTeamChoices(IEnumerable<PlayerEnums.Country> takenByOthers)
        {
            return new[] { PlayerEnums.Country.Sonstige }
                .Concat(Enum.GetValues(typeof(PlayerEnums.Country)).Cast<PlayerEnums.Country>()
                    .Where(c => c != PlayerEnums.Country.Sonstige && !takenByOthers.Contains(c))
                    .OrderBy(c => PlayerEnums.GetDescription(c)))
                .ToList();
        }

        // Without a national team there's no contract: saving clears salary and bonus.
        private void UpdateNationalContractAvailability()
        {
            bool enabled = this.coach?.IsNationalCoach ?? false;
            this.NationalSalaryInput.Enabled = enabled;
            this.NationalRoundBonusInput.Enabled = enabled;
        }

        private void BindEnumCombo<T>(ComboBox combo, string property) where T : Enum
        {
            combo.DataSource = Enum.GetValues(typeof(T)).Cast<T>().Select(v => new { Value = v, Text = PlayerEnums.GetDescription(v) }).ToList();
            combo.DisplayMember = "Text";
            combo.ValueMember = "Value";
            Binding binding = new Binding("SelectedValue", this.bindingSource, property, false, DataSourceUpdateMode.OnPropertyChanged);
            // RefreshValues blanks every combo first (ClearAllFields, SelectedIndex -1); that empty
            // selection must not reach the enum property, which throws on null.
            binding.Parse += (s, e) =>
            {
                if (!(e.Value is T) && this.bindingSource.Current != null)
                    e.Value = TypeDescriptor.GetProperties(this.bindingSource.Current)[property].GetValue(this.bindingSource.Current);
            };
            combo.DataBindings.Add(binding);
        }

        private void PopulateStockClubCombo(ComboBox clubCombo, PlayerEnums.Country country, byte selectedClubId)
        {
            clubCombo.DataSource = this.allClubs?.Where(c => c.Country == country).OrderBy(c => c.ClubName).ToList() ?? new List<Club>();
            clubCombo.DisplayMember = "ClubName";
            clubCombo.ValueMember = "Id";
            clubCombo.SelectedValue = selectedClubId;
        }

        // Clubs a stock slot offers: only those listed on the stock market (only they have shares),
        // plus the one already held. Where the flag can't be read, every club stays.
        private void PopulateStockClubs(ComboBox clubCombo, PlayerEnums.Country country, byte selectedClubId)
        {
            this.PopulateStockClubCombo(clubCombo, country, selectedClubId);
            if (clubCombo.DataSource is List<Club> clubs)
            {
                FixedAddresses fixedAddresses = FixedAddresses.For(this.Memory);
                List<Club> listed = clubs.Where(c => c.Id == selectedClubId || ClubRecords.IsListed(this.Memory, fixedAddresses, country, c.Id) != false).ToList();
                if (listed.Count != clubs.Count)
                {
                    clubCombo.DataSource = listed;
                    clubCombo.SelectedValue = selectedClubId;
                }
            }
            // A new position starts on the first club offered, so the model never keeps a club the
            // combo doesn't show.
            if (clubCombo.SelectedIndex < 0 && clubCombo.Items.Count > 0)
                clubCombo.SelectedIndex = 0;
        }

        // Stocks can only be bought in clubs from the main country or one of the bonus countries
        // (League tab) - the stock country combo is restricted to that set instead of every country.
        private List<PlayerEnums.Country> GetAvailableStockCountries()
        {
            if (this.league == null)
                return new List<PlayerEnums.Country>();

            return new[] { this.league.MainCountry }
                .Concat(this.league.ActiveBonusCountries)
                .Distinct()
                .ToList();
        }

        private void PopulateStockCountryCombo(int slot, PlayerEnums.Country selectedCountry)
        {
            ComboBox countryCombo = this.stockCountryCombos[slot];
            // A slot without shares holds no position (the game clears it when the last share is
            // sold); it shows "–" until a country is picked for a new one.
            if (this.coach != null && this.coach.Stocks[slot].Shares <= 0)
                selectedCountry = PlayerEnums.Country.Sonstige;

            List<PlayerEnums.Country> countries = this.GetAvailableStockCountries();
            // Keep an already-stored stock country selectable even if it's no longer part of the
            // active bonus countries (e.g. the league selection changed after the stock was bought) -
            // dropping it silently would corrupt the stock's Country on the next save.
            if (selectedCountry != PlayerEnums.Country.Sonstige && !countries.Contains(selectedCountry))
                countries.Add(selectedCountry);

            // Reassigning DataSource fires SelectedIndexChanged immediately for whatever ends up
            // first in the new list - detach the handler for that so it can't write the wrong
            // (transient) country into the model before SelectedValue below sets the real one.
            EventHandler handler = this.stockCountryChangedHandlers[slot];
            if (handler != null)
                countryCombo.SelectedIndexChanged -= handler;

            countryCombo.DataSource = new[] { new { Value = PlayerEnums.Country.Sonstige, Text = EmptyStockText } }
                .Concat(countries.Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }))
                .ToList();
            countryCombo.DisplayMember = "Text";
            countryCombo.ValueMember = "Value";
            countryCombo.SelectedValue = selectedCountry;

            if (handler != null)
            {
                countryCombo.SelectedIndexChanged += handler;
                // The handler was detached while the real selection was applied above, so its
                // club-combo cascade never ran for it - invoke it once now, same as a real selection.
                handler(countryCombo, EventArgs.Empty);
            }
        }

        // Single "–" entry with no Country/Club behind it, so the combos' change handlers (which only
        // act on a Country value or a Club item) ignore it.
        private static void ShowPlaceholder(ComboBox combo)
        {
            combo.DataSource = null;
            combo.Items.Clear();
            combo.Items.Add(EmptyStockText);
            combo.SelectedIndex = 0;
        }

        internal const string EmptyStockText = "–";

        // A trainer with no club lined up for next season has country 0 / club 0 there (confirmed
        // live: both read 0 for a trainer staying put). Country 0 is "Sonstige" elsewhere, but here it
        // means "no change" - and the club scan's entries with country 0 are unused slots with
        // garbage names, so they must not be offered as choices for it.
        private const PlayerEnums.Country NoNextSeasonCountry = PlayerEnums.Country.Sonstige;

        // Read-only: the club this trainer coaches right now, looked up by country + ID in the full
        // club scan. Country 0 means the trainer is currently without a club.
        private void UpdateCurrentClubDisplay()
        {
            if (this.coach == null)
            {
                this.CurrentClubValue.Text = "-";
                return;
            }

            if (this.coach.CurrentCountry == PlayerEnums.Country.Sonstige)
            {
                this.CurrentClubValue.Text = "vereinslos";
                return;
            }

            Club club = this.allClubs?.FirstOrDefault(c => c.Country == this.coach.CurrentCountry && c.Id == this.coach.CurrentClubId);
            string country = PlayerEnums.GetDescription(this.coach.CurrentCountry);
            this.CurrentClubValue.Text = club != null ? $"{club.ClubName} ({country})" : $"Verein {this.coach.CurrentClubId} ({country})";
        }

        // Clubs offered for next season: everything the game's season change would actually accept
        // (see CoachController.IsEligibleNextSeasonClub) - for a bonus country that's only its top
        // league. Falls back to the unfiltered list where that can't be checked (unconfirmed build).
        private void PopulateNextSeasonClubCombo(PlayerEnums.Country country)
        {
            this.PopulateStockClubCombo(this.NextSeasonClubInput, country, this.coach.ClubIdNextSeason);
            List<Club> clubs = (List<Club>)this.NextSeasonClubInput.DataSource;   // PopulateStockClubCombo always sets a list
            List<Club> eligible = clubs.Where(c => CoachController.IsEligibleNextSeasonClub(this.Memory, country, c.Id) != false).ToList();
            if (eligible.Count == clubs.Count)
                return;

            this.NextSeasonClubInput.DataSource = eligible;
            this.NextSeasonClubInput.DisplayMember = "ClubName";
            this.NextSeasonClubInput.ValueMember = "Id";
            this.NextSeasonClubInput.SelectedValue = this.coach.ClubIdNextSeason;
        }

        private void PopulateNoNextSeasonClubCombo()
        {
            this.NextSeasonClubInput.DataSource = new List<Club> { new Club { Id = 0, ClubName = "–" } };
            this.NextSeasonClubInput.DisplayMember = "ClubName";
            this.NextSeasonClubInput.ValueMember = "Id";
            this.NextSeasonClubInput.SelectedValue = (byte)0;
        }

        // Same restricted-list/detach-reattach pattern as PopulateStockCountryCombo above, for the
        // single "Nächste Saison" country combo instead of one-per-stock-slot. "Kein Wechsel"
        // (NoNextSeasonCountry) always comes first, so a pending change can also be withdrawn.
        private void PopulateNextSeasonCountryCombo(PlayerEnums.Country selectedCountry)
        {
            List<PlayerEnums.Country> countries = new[] { NoNextSeasonCountry }.Concat(this.GetAvailableStockCountries()).Distinct().ToList();
            // Keep an already-stored value selectable even if it's no longer part of the active bonus
            // countries (e.g. the league selection changed afterwards) - dropping it silently would
            // corrupt CountryNextSeason on the next save.
            if (!countries.Contains(selectedCountry))
                countries.Add(selectedCountry);

            EventHandler handler = this.nextSeasonCountryChangedHandler;
            if (handler != null)
                this.NextSeasonCountryInput.SelectedIndexChanged -= handler;

            this.NextSeasonCountryInput.DataSource = countries.Select(c => new { Value = c, Text = c == NoNextSeasonCountry ? "Kein Wechsel" : PlayerEnums.GetDescription(c) }).ToList();
            this.NextSeasonCountryInput.DisplayMember = "Text";
            this.NextSeasonCountryInput.ValueMember = "Value";
            this.NextSeasonCountryInput.SelectedValue = selectedCountry;

            if (handler != null)
            {
                this.NextSeasonCountryInput.SelectedIndexChanged += handler;
                handler(this.NextSeasonCountryInput, EventArgs.Empty);
            }
        }

        // Keeps the Aktien tab's (and the "Nächste Saison" combo's) country choices in sync as soon
        // as the League tab's selection changes, without requiring a save/reload round-trip first.
        private void RefreshStockCountryOptions()
        {
            if (this.coach == null)
                return;

            for (int i = 0; i < Coach.MaxStocks; i++)
                this.PopulateStockCountryCombo(i, this.coach.Stocks[i].Country);

            this.PopulateNextSeasonCountryCombo(this.coach.CountryNextSeason);
        }

        // One "aktiv" checkbox per league country other than the main country. A country that can't
        // be switched right now (see BonusCountry.ToggleBlockedReason) keeps its checkbox disabled
        // with the reason next to it.
        private void RebuildBonusCountryControls()
        {
            this.AdditionalCountriesBox.Controls.Clear();
            this.bonusCountryChecks.Clear();

            if (this.league == null)
                return;

            int row = 0;
            foreach (BonusCountry entry in this.league.BonusCountries)
            {
                BonusCountry captured = entry;
                int top = AdditionalCountryMargin + row * AdditionalCountryRowHeight;

                CheckBox check = new CheckBox
                {
                    AutoSize = true,
                    Location = new System.Drawing.Point(7, top),
                    Text = PlayerEnums.GetDescription(entry.Country),
                    Checked = entry.IsActive,
                    Enabled = entry.CanToggle
                };
                check.CheckedChanged += (s, e) =>
                {
                    captured.IsActive = check.Checked;
                    this.RefreshStockCountryOptions();
                };
                this.AdditionalCountriesBox.Controls.Add(check);
                this.bonusCountryChecks.Add(check);

                string note = BonusCountryNote(entry, this.skippedBonusCountries.Contains(entry.Country));
                if (note != null)
                {
                    this.AdditionalCountriesBox.Controls.Add(new Label
                    {
                        AutoSize = true,
                        Location = new System.Drawing.Point(BonusReasonX, top + 4),
                        ForeColor = System.Drawing.SystemColors.GrayText,
                        Text = note
                    });
                }
                row++;
            }
        }

        // Text next to a country's checkbox: why it can't be switched, prefixed with "Nicht
        // übernommen" when the last save had to skip its change. Null when there's nothing to say.
        internal static string BonusCountryNote(BonusCountry entry, bool skipped)
        {
            if (!skipped)
                return entry.ToggleBlockedReason;
            return entry.ToggleBlockedReason == null ? "Nicht übernommen." : $"Nicht übernommen: {entry.ToggleBlockedReason}";
        }

        // The game doesn't support creating a new stock position (or removing one) via a memory edit,
        // only changing an existing one - a slot with zero shares has no position, so its controls are
        // disabled rather than left editable but ineffective.
        private void UpdateStockSlotAvailability()
        {
            if (this.coach == null)
                return;

            for (int i = 0; i < Coach.MaxStocks; i++)
            {
                bool hasClub = this.coach.Stocks[i].Country != PlayerEnums.Country.Sonstige;
                this.stockClubCombos[i].Enabled = hasClub;
                this.stockSharesInputs[i].Enabled = hasClub;
                this.stockPriceInputs[i].Enabled = hasClub;
            }
        }

        // Which of the up to Coach.MaxTrainers savegame slots is currently shown - preserved across
        // reloads/saves (which omit trainerIndex) and background reconnect refreshes.
        private int trainerIndex;
        internal int CurrentTrainerIndex => this.trainerIndex;

        internal void RefreshValues(PlayerEnums.AddressType type, int? trainerIndex = null)
        {
            if (this.IsGameRunning())
            {
                this.trainerIndex = trainerIndex ?? this.trainerIndex;
                // The squad's country isn't the trainer's data - it stays on the country being edited.
                this.ClearAllFields(this, new HashSet<Control> { this.NationalSquadCountryInput });

                this.coachController = new CoachController(this.Memory, this.processController.IsGog, type, this.trainerIndex);
                this.coach = this.CoachController.Coach;
                // Keep the controller pointed at the exact object the UI is bound to, so the next
                // Save() writes what's actually shown/edited rather than a second, divergent read.
                this.coachController.Coach = this.coach;

                // Global to the savegame (not per-trainer), so this doesn't depend on trainerIndex -
                // still refreshed here alongside the coach so a background reconnect picks up changes.
                this.leagueController = new LeagueController(this.Memory, this.processController.IsGog, type);
                this.league = this.leagueController.League;

                this.allClubs = new ClubController(this.Memory, this.processController.IsGog, PlayerEnums.AddressType.ALL, loadFullList: true).EntityList;

                if (this.bindingSource != null)
                {
                    this.bindingSource.DataSource = this.coach;
                    this.bindingSource.ResetBindings(false);

                    // Not covered by ResetBindings above - CountryNextSeason/ClubIdNextSeason aren't
                    // plain DataBindings-bound scalars (same reason as the stock country/club combos
                    // below), so they need their own explicit repopulation. Repopulating the country
                    // combo cascades into the club combo too, same as PopulateStockCountryCombo below.
                    this.PopulateNextSeasonCountryCombo(this.coach.CountryNextSeason);
                    this.UpdateCurrentClubDisplay();
                    this.PopulateNationalTeamCombo();
                    this.ShowFriendlies();
                    this.LoadNationalSquad();
                    this.ShowChampionTips();
                    this.ShowCareer();

                    for (int i = 0; i < Coach.MaxStocks; i++)
                    {
                        // Repopulate the country choices (main + bonus countries) before rebinding,
                        // since the league selection may have changed since the last refresh; this
                        // also cascades into repopulating the club combo via the SelectedIndexChanged
                        // handler wired in InitClubTabControl.
                        this.PopulateStockCountryCombo(i, this.coach.Stocks[i].Country);

                        this.stockBindingSources[i].DataSource = this.coach.Stocks[i];
                        this.stockBindingSources[i].ResetBindings(false);
                    }

                    this.UpdateStockSlotAvailability();
                    this.ImageBox.Enabled = this.coach.HasImage;
                }

                if (this.competencyLevelInput != null)
                {
                    // Reselecting the same numeric level as before a reload wouldn't fire
                    // SelectedIndexChanged (no visible change), which would leave the competency
                    // inputs bound to the previous (now stale) Coach object - show the freshly
                    // loaded coach's data for that level explicitly instead of relying on the event.
                    // SelectedValue (not SelectedItem) resolves through ValueMember - the list holds
                    // Value/Text wrapper objects now, not raw bytes (see BuildCompetencyBox).
                    this.competencyLevelInput.SelectedValue = this.coach.Level;
                    this.ShowCompetencyLevel(this.coach.Level);
                }

                if (this.leagueBindingSource != null)
                {
                    this.leagueBindingSource.DataSource = this.league;
                    this.leagueBindingSource.ResetBindings(false);

                    this.RebuildBonusCountryControls();
                }
            }
        }
        private void ReloadBtn_Click(object sender, System.EventArgs e)
        {
            this.RefreshValues(this.coachController.Type);
        }

        private void SaveBtn_Click(object sender, System.EventArgs e)
        {
            if (this.IsGameRunning())
            {
                this.coachController.Save();
                // After the coach, so a next-season change saved just now already blocks switching
                // off that country.
                this.skippedBonusCountries = this.leagueController.Save();
                // Re-reads from memory instead of redisplaying the pre-save "coach" object (which is
                // what ReloadBtn_Click already does) - reusing the pre-save object left the stock
                // country/club combos showing the wrong (first-in-list) club after every save, even
                // though the write itself was correct; a fresh read displays correctly.
                this.RefreshValues(this.coachController.Type);
            }
        }
    }
}
