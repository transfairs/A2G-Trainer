using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    public partial class CoachView : EntityView
    {
        private const int StockColumns = 2;
        private const int StockBoxWidth = 260;
        private const int StockBoxHeight = 145;
        private const int StockBoxMargin = 6;

        private const int AdditionalCountryRowHeight = 27;
        // Top offset for the first row inside AdditionalCountriesBox - needs to clear the GroupBox's
        // own title text (6px alone had the first row's controls overlapping the "Bonusländer"
        // header), matching the ~20-24px other GroupBoxes in this view use for their first child.
        private const int AdditionalCountryMargin = 20;

        // The main country can only ever be one of these three (the game's three playable home
        // leagues); additional countries add clubs from any of these five to the stock market.
        private static readonly PlayerEnums.Country[] MainCountryChoices = { PlayerEnums.Country.Deutschland, PlayerEnums.Country.England, PlayerEnums.Country.Frankreich };
        private static readonly PlayerEnums.Country[] AdditionalCountryChoices = { PlayerEnums.Country.Deutschland, PlayerEnums.Country.England, PlayerEnums.Country.Frankreich, PlayerEnums.Country.Italien, PlayerEnums.Country.Spanien };

        private Coach coach;
        private LeagueSettings league;
        // Club-IDs are only unique per country, so the club list for a stock has to be
        // re-filtered whenever its own "Land" changes - fetched once per refresh here.
        private BindingList<Club> allClubs;

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

        // One combo per currently-active bonus-country slot, indexed by slot - built dynamically in
        // RebuildBonusCountryControls, since the game doesn't support turning a slot on or off via a
        // memory edit, only changing which country an already-active slot holds. A null entry means
        // that slot isn't in use in the current savegame, so no control exists for it.
        private readonly ComboBox[] bonusCountryCombos = new ComboBox[LeagueSettings.MaxAdditionalCountries];
        private BindingSource leagueBindingSource;

        private LeagueController leagueController;

        public CoachView(ProcessMemory memory, ProcessController controller) : base(memory, controller)
        {
            InitializeComponent();
            this.BuildStockBoxes();
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

        internal void InitClubTabControl()
        {
            this.bindingSource = new BindingSource
            {
                DataSource = this.coach
            };

            this.LevelInput.KeyPress += this.NumericOnly_KeyPress;
            this.AgeInput.KeyPress += this.NumericOnly_KeyPress;
            this.WealthInput.KeyPress += this.NumericOnly_KeyPress;

            this.LevelInput.TextChanged += this.ByteMax255_TextChanged;
            this.AgeInput.TextChanged += this.ByteMax255_TextChanged;
            this.WealthInput.TextChanged += this.IntMaxNumber_TextChanged;

            this.LastNameInput.DataBindings.Add("Text", this.bindingSource, "Lastname");
            this.FirstNameInput.DataBindings.Add("Text", this.bindingSource, "Firstname");
            this.LevelInput.DataBindings.Add("Text", this.bindingSource, "Level");
            this.AgeInput.DataBindings.Add("Text", this.bindingSource, "Age");
            this.WealthInput.DataBindings.Add("Text", this.bindingSource, "Wealth");

            this.DifficultyInput.DataSource = Enum.GetValues(typeof(CoachEnums.Difficulty)).Cast<CoachEnums.Difficulty>().Select(d => new { Value = d, Text = PlayerEnums.GetDescription(d) }).ToList();
            this.DifficultyInput.DisplayMember = "Text";
            this.DifficultyInput.ValueMember = "Value";
            this.DifficultyInput.DataBindings.Add("SelectedValue", this.bindingSource, "Difficulty");

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
                // Stored so PopulateStockCountryCombo can detach it while reassigning DataSource - see
                // stockCountryChangedHandlers.
                EventHandler countryChanged = (s, e) =>
                {
                    if (this.coach != null && countryCombo.SelectedValue is PlayerEnums.Country country)
                    {
                        this.coach.Stocks[slot].Country = country;
                        this.PopulateStockClubCombo(clubCombo, country, this.coach.Stocks[slot].ClubId);
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
                this.stockSharesInputs[i].TextChanged += this.UshortMaxNumber_TextChanged;
                this.stockPriceInputs[i].TextChanged += this.UshortMaxNumber_TextChanged;

                BindingSource stockSource = new BindingSource { DataSource = this.coach?.Stocks[i] };
                this.stockBindingSources[i] = stockSource;

                this.stockSharesInputs[i].DataBindings.Add("Text", stockSource, "Shares");
                this.stockPriceInputs[i].DataBindings.Add("Text", stockSource, "Price");
            }

            // The game doesn't support creating a new stock position (or removing one) via a memory
            // edit, only changing an existing one - so a slot with no shares (no position) is shown
            // read-only rather than left editable but ineffective.
            this.UpdateStockSlotAvailability();

            this.leagueBindingSource = new BindingSource { DataSource = this.league };

            this.MainCountryInput.DataSource = MainCountryChoices.Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
            this.MainCountryInput.DisplayMember = "Text";
            this.MainCountryInput.ValueMember = "Value";
            this.MainCountryInput.DataBindings.Add("SelectedValue", this.leagueBindingSource, "MainCountry");
            this.MainCountryInput.SelectedIndexChanged += (s, e) =>
            {
                this.RebuildBonusCountryControls();
                this.RefreshStockCountryOptions();
            };

            // RefreshValues only rebuilds the bonus-country controls once leagueBindingSource exists
            // (set a few lines up) - this call covers the very first display, which would otherwise
            // stay empty until the next background reconnect refresh.
            this.RebuildBonusCountryControls();
        }

        private void PopulateStockClubCombo(ComboBox clubCombo, PlayerEnums.Country country, byte selectedClubId)
        {
            clubCombo.DataSource = this.allClubs?.Where(c => c.Country == country).OrderBy(c => c.ClubName).ToList() ?? new List<Club>();
            clubCombo.DisplayMember = "ClubName";
            clubCombo.ValueMember = "Id";
            clubCombo.SelectedValue = selectedClubId;
        }

        // Stocks can only be bought in clubs from the main country or one of the bonus countries
        // (League tab) - the stock country combo is restricted to that set instead of every country.
        private List<PlayerEnums.Country> GetAvailableStockCountries()
        {
            if (this.league == null)
                return new List<PlayerEnums.Country>();

            return new[] { this.league.MainCountry }
                .Concat(this.league.AdditionalCountries.Where(c => c.Country.HasValue).Select(c => c.Country.Value))
                .Distinct()
                .ToList();
        }

        private void PopulateStockCountryCombo(int slot, PlayerEnums.Country selectedCountry)
        {
            ComboBox countryCombo = this.stockCountryCombos[slot];
            List<PlayerEnums.Country> countries = this.GetAvailableStockCountries();
            // Keep an already-stored stock country selectable even if it's no longer part of the
            // active bonus countries (e.g. the league selection changed after the stock was bought) -
            // dropping it silently would corrupt the stock's Country on the next save.
            if (!countries.Contains(selectedCountry))
                countries.Add(selectedCountry);

            // Reassigning DataSource fires SelectedIndexChanged immediately for whatever ends up
            // first in the new list - detach the handler for that so it can't write the wrong
            // (transient) country into the model before SelectedValue below sets the real one.
            EventHandler handler = this.stockCountryChangedHandlers[slot];
            if (handler != null)
                countryCombo.SelectedIndexChanged -= handler;

            countryCombo.DataSource = countries.Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
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

        // Keeps the Aktien tab's country choices in sync as soon as the League tab's selection
        // changes, without requiring a save/reload round-trip first.
        private void RefreshStockCountryOptions()
        {
            if (this.coach == null)
                return;

            for (int i = 0; i < Coach.MaxStocks; i++)
                this.PopulateStockCountryCombo(i, this.coach.Stocks[i].Country);
        }

        // The game doesn't support adding or removing bonus-country slots via a memory edit - only
        // swapping which country an already-active slot holds. So this builds exactly as many combos
        // as there are active slots in the current savegame (an inactive slot gets no control at all),
        // each restricted to countries not already used by the main country or another active slot.
        private void RebuildBonusCountryControls()
        {
            this.AdditionalCountriesBox.Controls.Clear();
            Array.Clear(this.bonusCountryCombos, 0, this.bonusCountryCombos.Length);

            if (this.league == null)
                return;

            int row = 0;
            for (int slot = 0; slot < LeagueSettings.MaxAdditionalCountries; slot++)
            {
                if (!this.league.AdditionalCountries[slot].Country.HasValue)
                    continue;

                int capturedSlot = slot;

                Label label = new Label
                {
                    AutoSize = true,
                    Location = new System.Drawing.Point(7, AdditionalCountryMargin + row * AdditionalCountryRowHeight + 3),
                    Text = $"Bonusland {row + 1}"
                };
                ComboBox combo = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new System.Drawing.Point(90, AdditionalCountryMargin + row * AdditionalCountryRowHeight),
                    Size = new System.Drawing.Size(120, 21)
                };

                // Handler is wired only after the initial value below is set - assigning DataSource
                // fires SelectedIndexChanged immediately for whatever ends up first in the list, and
                // since this is a fresh combo built from scratch each time, there's no existing
                // selection worth reacting to yet (unlike the stock country combo, which is reused
                // across refreshes and needs its cascade to fire on every population).
                combo.DataSource = this.GetSwappableBonusCountries(capturedSlot).Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
                combo.DisplayMember = "Text";
                combo.ValueMember = "Value";
                combo.SelectedValue = this.league.AdditionalCountries[capturedSlot].Country.Value;

                combo.SelectedIndexChanged += (s, e) =>
                {
                    if (combo.SelectedValue is PlayerEnums.Country country)
                        this.league.AdditionalCountries[capturedSlot].Country = country;
                    this.RefreshStockCountryOptions();
                };

                this.AdditionalCountriesBox.Controls.Add(label);
                this.AdditionalCountriesBox.Controls.Add(combo);
                this.bonusCountryCombos[slot] = combo;
                row++;
            }

            if (row == 0)
            {
                this.AdditionalCountriesBox.Controls.Add(new Label
                {
                    AutoSize = true,
                    Location = new System.Drawing.Point(7, AdditionalCountryMargin),
                    Text = "Keine Bonusländer in diesem Spielstand."
                });
            }
        }

        // Candidates for a bonus-country slot: every tradeable country except the main country and
        // whichever other active slots already hold - the slot's own current country stays included
        // so it remains selectable (i.e. "no change").
        private List<PlayerEnums.Country> GetSwappableBonusCountries(int slot)
        {
            HashSet<PlayerEnums.Country> usedElsewhere = new HashSet<PlayerEnums.Country>(
                this.league.AdditionalCountries
                    .Where((c, index) => index != slot && c.Country.HasValue)
                    .Select(c => c.Country.Value));

            return AdditionalCountryChoices.Where(c => c != this.league.MainCountry && !usedElsewhere.Contains(c)).ToList();
        }

        // The game doesn't support creating a new stock position (or removing one) via a memory edit,
        // only changing an existing one - a slot with zero shares has no position, so its controls are
        // disabled rather than left editable but ineffective.
        private void UpdateStockSlotAvailability()
        {
            if (this.coach == null)
                return;

            for (int i = 0; i < Coach.MaxStocks; i++)
                this.stockBoxes[i].Enabled = this.coach.Stocks[i].Shares > 0;
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
                this.ClearAllFields(this);

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
                this.leagueController.Save();
                // Re-reads from memory instead of redisplaying the pre-save "coach" object (which is
                // what ReloadBtn_Click already does) - reusing the pre-save object left the stock
                // country/club combos showing the wrong (first-in-list) club after every save, even
                // though the write itself was correct; a fresh read displays correctly.
                this.RefreshValues(this.coachController.Type);
            }
        }
    }
}
