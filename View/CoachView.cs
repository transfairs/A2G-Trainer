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

        private Coach coach;
        // Club-IDs are only unique per country, so the club list for a stock has to be
        // re-filtered whenever its own "Land" changes - fetched once per refresh here.
        private BindingList<Club> allClubs;

        private readonly ComboBox[] stockCountryCombos = new ComboBox[Coach.MaxStocks];
        private readonly ComboBox[] stockClubCombos = new ComboBox[Coach.MaxStocks];
        private readonly TextBox[] stockSharesInputs = new TextBox[Coach.MaxStocks];
        private readonly TextBox[] stockPriceInputs = new TextBox[Coach.MaxStocks];
        private readonly BindingSource[] stockBindingSources = new BindingSource[Coach.MaxStocks];

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

            for (int i = 0; i < Coach.MaxStocks; i++)
            {
                int slot = i;
                ComboBox countryCombo = this.stockCountryCombos[i];
                ComboBox clubCombo = this.stockClubCombos[i];

                countryCombo.DataSource = Enum.GetValues(typeof(PlayerEnums.Country)).Cast<PlayerEnums.Country>().Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
                countryCombo.DisplayMember = "Text";
                countryCombo.ValueMember = "Value";

                // Club-IDs repeat per country, so the club combo can't just bind to Stock.ClubId -
                // it has to show only the selected country's clubs, keyed by Id under the hood.
                // Wired before the DataBindings below so that the initial SelectedValue push (when a
                // binding is added) already populates the club combo with the right country's clubs.
                countryCombo.SelectedIndexChanged += (s, e) =>
                {
                    if (this.coach != null && countryCombo.SelectedValue is PlayerEnums.Country country)
                        this.PopulateStockClubCombo(clubCombo, country, this.coach.Stocks[slot].ClubId);
                };
                clubCombo.SelectedIndexChanged += (s, e) =>
                {
                    if (this.coach != null && clubCombo.SelectedItem is Club club)
                        this.coach.Stocks[slot].ClubId = club.Id;
                };

                this.stockSharesInputs[i].KeyPress += this.NumericOnly_KeyPress;
                this.stockPriceInputs[i].KeyPress += this.NumericOnly_KeyPress;
                this.stockSharesInputs[i].TextChanged += this.UshortMaxNumber_TextChanged;
                this.stockPriceInputs[i].TextChanged += this.UshortMaxNumber_TextChanged;

                BindingSource stockSource = new BindingSource { DataSource = this.coach?.Stocks[i] };
                this.stockBindingSources[i] = stockSource;

                countryCombo.DataBindings.Add("SelectedValue", stockSource, "Country");
                this.stockSharesInputs[i].DataBindings.Add("Text", stockSource, "Shares");
                this.stockPriceInputs[i].DataBindings.Add("Text", stockSource, "Price");
            }
        }

        private void PopulateStockClubCombo(ComboBox clubCombo, PlayerEnums.Country country, byte selectedClubId)
        {
            clubCombo.DataSource = this.allClubs?.Where(c => c.Country == country).OrderBy(c => c.ClubName).ToList() ?? new List<Club>();
            clubCombo.DisplayMember = "ClubName";
            clubCombo.ValueMember = "Id";
            clubCombo.SelectedValue = selectedClubId;
        }

        // Which of the up to Coach.MaxTrainers savegame slots is currently shown - preserved across
        // reloads/saves (which omit trainerIndex) and background reconnect refreshes.
        private int trainerIndex;
        internal int CurrentTrainerIndex => this.trainerIndex;

        internal void RefreshValues(PlayerEnums.AddressType type, Coach coach = null, int? trainerIndex = null)
        {
            if (this.IsGameRunning())
            {
                this.trainerIndex = trainerIndex ?? this.trainerIndex;
                this.ClearAllFields(this);

                this.coachController = new CoachController(this.Memory, this.processController.IsGog, type, this.trainerIndex);
                this.coach = coach ?? this.CoachController.Coach;
                // Keep the controller pointed at the exact object the UI is bound to - otherwise a
                // caller-supplied coach (e.g. SaveBtn_Click re-displaying what it just saved) leaves
                // coachController.Coach as the freshly-read (now stale) instance, and the next Save()
                // silently writes that stale object instead of the user's newest edits.
                this.coachController.Coach = this.coach;

                this.allClubs = new ClubController(this.Memory, this.processController.IsGog, PlayerEnums.AddressType.ALL, loadFullList: true).EntityList;

                if (this.bindingSource != null)
                {
                    this.bindingSource.DataSource = this.coach;
                    this.bindingSource.ResetBindings(false);

                    for (int i = 0; i < Coach.MaxStocks; i++)
                    {
                        this.stockBindingSources[i].DataSource = this.coach.Stocks[i];
                        this.stockBindingSources[i].ResetBindings(false);
                    }
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
                this.RefreshValues(this.coachController.Type, this.coach);
            }
        }
    }
}
