using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// "Kalkulation" tab (what the game's budget screen sets, the slush fund, and the costs the game
    /// charges every club) and "Bank &amp; Börse" tab (loans, investments, the savegame-wide interest
    /// rates and investment indices, share price and overdraft limit). Built in code, like the coach
    /// view's stock boxes.
    /// </summary>
    public partial class ClubView
    {
        private static readonly string[] StatisticsNames = { "Statistiken sind langweilig", "Rentner mit PC", "Chefstatistiker mit Großrechner" };

        private TabPage budgetTab, bankTab;
        private TextBox standingPriceInput, seatPriceInput, seasonTicketDiscountInput, seasonTicketQuotaInput, securityInput, legalCostsInput;
        private ComboBox lawFirmInput, statisticsInput;
        private CheckBox computerInput;
        private TextBox slushFundInput, slushFundRateInput;
        private GroupBox costsBox;
        private DataGridView lawFirmFeesGrid;
        private TextBox smallStatisticsCostInput, largeStatisticsCostInput, computerCostInput;
        private DataGridView loansGrid, investmentsGrid, indicesGrid;
        private Button addLoanButton, removeLoanButton, addInvestmentButton, removeInvestmentButton;
        private GroupBox ratesBox, stockBox;
        private TextBox depositRateInput, overdraftRateInput, overLimitRateInput, loanRateInput, callMoneyRateInput, corporationTaxInput;
        private TextBox sharePriceInput, overdraftLimitInput;
        private Label issuedSharesValue;
        private BindingSource ratesBindingSource, costsBindingSource;
        private InterestRates interestRates;
        private GameCosts gameCosts;

        private static Label AddLabel(Control box, string text, int x, int y)
        {
            Label label = new Label { AutoSize = true, Location = new Point(x, y + 3), Text = text };
            box.Controls.Add(label);
            return label;
        }

        private static TextBox AddInput(Control box, string label, int labelX, int inputX, int y, int width)
        {
            AddLabel(box, label, labelX, y);
            TextBox input = new TextBox { Location = new Point(inputX, y), Size = new Size(width, 20), TextAlign = HorizontalAlignment.Right };
            box.Controls.Add(input);
            return input;
        }

        private static ComboBox AddChoice(Control box, string label, int y, string[] items)
        {
            AddLabel(box, label, 7, y);
            ComboBox combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(110, y), Size = new Size(170, 21) };
            combo.DataSource = items.Select((text, value) => new { Value = (byte)value, Text = text }).ToList();
            combo.DisplayMember = "Text";
            combo.ValueMember = "Value";
            box.Controls.Add(combo);
            return combo;
        }

        private static DataGridView AddGrid(Control box, bool autoColumns = true)
        {
            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                AutoGenerateColumns = autoColumns,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window
            };
            // Instead of WinForms' error dialog: a number too big (small) for the field becomes the
            // field's largest (smallest) value, like the text boxes do it; anything else that isn't
            // a number goes back to the old value.
            grid.DataError += (s, e) =>
            {
                e.ThrowException = false;
                e.Cancel = false;
                string typed = grid.EditingControl?.Text ?? "";
                object limit = OverflowLimit(grid.Columns[e.ColumnIndex].ValueType, typed.TrimStart().StartsWith("-"));
                grid.CancelEdit();
                if (e.Exception is FormatException && e.Exception.InnerException is OverflowException && limit != null && e.RowIndex >= 0)
                    grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = limit;
            };
            box.Controls.Add(grid);
            return grid;
        }

        // The largest (or, for a negative number, smallest) value of a numeric column's type.
        internal static object OverflowLimit(Type valueType, bool negative)
        {
            if (valueType == typeof(int))
                return negative ? int.MinValue : int.MaxValue;
            if (valueType == typeof(short))
                return negative ? short.MinValue : short.MaxValue;
            if (valueType == typeof(byte))
                return negative ? byte.MinValue : byte.MaxValue;
            if (valueType == typeof(decimal))
                return negative ? decimal.MinValue : decimal.MaxValue;
            return null;
        }

        private void BuildBudgetTab()
        {
            this.budgetTab = new TabPage { Text = "Kalkulation", UseVisualStyleBackColor = true, AutoScroll = true, Padding = new Padding(3) };

            GroupBox budgetBox = new GroupBox { Text = "Kalkulation", Location = new Point(6, 6), Size = new Size(288, 262) };
            this.standingPriceInput = AddInput(budgetBox, "Stehplatz (DM)", 7, 220, 20, 60);
            this.seatPriceInput = AddInput(budgetBox, "Sitzplatz (DM)", 7, 220, 46, 60);
            this.seasonTicketDiscountInput = AddInput(budgetBox, "Dauerkarten-Rabatt (%)", 7, 220, 72, 60);
            this.seasonTicketQuotaInput = AddInput(budgetBox, "Dauerkarten-Kontingent (%)", 7, 220, 98, 60);
            this.securityInput = AddInput(budgetBox, "Sicherheit (%)", 7, 220, 124, 60);
            this.lawFirmInput = AddChoice(budgetBox, "Rechtsanwälte", 152, GameRates.LawFirmNames);
            this.legalCostsInput = AddInput(budgetBox, "Anwaltskosten der Saison (DM)", 7, 180, 180, 100);
            this.statisticsInput = AddChoice(budgetBox, "Statistikabteilung", 208, StatisticsNames);
            this.computerInput = new CheckBox { Text = "Computerabteilung (NORACSA)", Location = new Point(10, 236), AutoSize = true };
            budgetBox.Controls.Add(this.computerInput);

            GroupBox slushBox = new GroupBox { Text = "Schwarze Kasse", Location = new Point(6, 272), Size = new Size(288, 70) };
            this.slushFundInput = AddInput(slushBox, "Stand (DM)", 7, 180, 18, 100);
            this.slushFundRateInput = AddInput(slushBox, "Abzweigung (Promille)", 7, 237, 43, 43);

            this.costsBox = new GroupBox { Text = "Kosten für alle Vereine (bis Spielneustart)", Location = new Point(300, 6), Size = new Size(280, 236) };
            Panel feesPanel = new Panel { Location = new Point(6, 18), Size = new Size(268, 132) };
            this.costsBox.Controls.Add(feesPanel);
            this.lawFirmFeesGrid = AddGrid(feesPanel);
            this.smallStatisticsCostInput = AddInput(this.costsBox, "Rentner mit PC (DM)", 7, 175, 156, 95);
            this.largeStatisticsCostInput = AddInput(this.costsBox, "Chefstatistiker (DM)", 7, 175, 182, 95);
            this.computerCostInput = AddInput(this.costsBox, "NORACSA (DM)", 7, 175, 208, 95);

            this.budgetTab.Controls.AddRange(new Control[] { budgetBox, slushBox, this.costsBox });
            this.ClubTabControl.Controls.Add(this.budgetTab);

            this.bankTab = new TabPage { Text = "Bank & Börse", UseVisualStyleBackColor = true, AutoScroll = true, Padding = new Padding(3) };
            GroupBox loansBox = new GroupBox { Text = "Kredite", Location = new Point(6, 6), Size = new Size(288, 165) };
            Panel loansPanel = new Panel { Location = new Point(6, 18), Size = new Size(276, 112) };
            loansBox.Controls.Add(loansPanel);
            this.loansGrid = AddGrid(loansPanel, autoColumns: false);
            this.loansGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Betrag", DataPropertyName = nameof(ClubLoan.Amount), FillWeight = 130 });
            this.loansGrid.Columns.Add(new DataGridViewComboBoxColumn
            {
                HeaderText = "Laufzeit", DataPropertyName = nameof(ClubLoan.Term),
                DataSource = ClubLoan.TermYears.Select((years, term) => new { Value = (byte)term, Text = years == 1 ? "1 Jahr" : $"{years} Jahre" }).ToList(),
                DisplayMember = "Text", ValueMember = "Value", DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox
            });
            this.loansGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Zins (%)", DataPropertyName = nameof(ClubLoan.Rate), FillWeight = 70 });
            this.loansGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Kündbar", DataPropertyName = nameof(ClubLoan.Cancellable), FillWeight = 70 });
            this.addLoanButton = new Button { Text = "Neuer Kredit", Location = new Point(6, 135), Size = new Size(130, 23) };
            this.removeLoanButton = new Button { Text = "Kredit tilgen", Location = new Point(146, 135), Size = new Size(136, 23) };
            this.addLoanButton.Click += (s, e) => this.AddLoan();
            this.removeLoanButton.Click += (s, e) => this.RemoveLoan();
            loansBox.Controls.AddRange(new Control[] { this.addLoanButton, this.removeLoanButton });

            GroupBox investmentsBox = new GroupBox { Text = "Geldanlagen", Location = new Point(6, 175), Size = new Size(288, 167) };
            Panel investmentsPanel = new Panel { Location = new Point(6, 18), Size = new Size(276, 114) };
            investmentsBox.Controls.Add(investmentsPanel);
            this.investmentsGrid = AddGrid(investmentsPanel, autoColumns: false);
            this.investmentsGrid.Columns.Add(new DataGridViewComboBoxColumn
            {
                HeaderText = "Art", DataPropertyName = nameof(ClubInvestment.Kind), FillWeight = 140,
                DataSource = ClubInvestment.KindNames.Select((text, value) => new { Value = (byte)value, Text = text }).ToList(),
                DisplayMember = "Text", ValueMember = "Value", DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox
            });
            this.investmentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Einlage", DataPropertyName = nameof(ClubInvestment.Principal) });
            this.investmentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Wert", DataPropertyName = nameof(ClubInvestment.Value) });
            this.addInvestmentButton = new Button { Text = "Neue Anlage", Location = new Point(6, 137), Size = new Size(130, 23) };
            this.removeInvestmentButton = new Button { Text = "Anlage auflösen", Location = new Point(146, 137), Size = new Size(136, 23) };
            this.addInvestmentButton.Click += (s, e) => this.AddInvestment();
            this.removeInvestmentButton.Click += (s, e) => this.RemoveInvestment();
            investmentsBox.Controls.AddRange(new Control[] { this.addInvestmentButton, this.removeInvestmentButton });

            this.ratesBox = new GroupBox { Text = "Zinssätze in % (alle Vereine)", Location = new Point(300, 6), Size = new Size(280, 99) };
            this.depositRateInput = AddInput(this.ratesBox, "Guthaben", 7, 85, 20, 45);
            this.overdraftRateInput = AddInput(this.ratesBox, "Dispo", 7, 85, 46, 45);
            this.overLimitRateInput = AddInput(this.ratesBox, "Überziehung", 7, 85, 72, 45);
            this.loanRateInput = AddInput(this.ratesBox, "Kredite", 140, 228, 20, 45);
            this.callMoneyRateInput = AddInput(this.ratesBox, "Tagesgeld", 140, 228, 46, 45);
            this.corporationTaxInput = AddInput(this.ratesBox, "Körperschaftst.", 140, 228, 72, 45);

            GroupBox indicesBox = new GroupBox { Text = "Anlage-Anbieter (alle Vereine)", Location = new Point(300, 109), Size = new Size(280, 140) };
            this.indicesGrid = AddGrid(indicesBox);

            this.stockBox = new GroupBox { Text = "Börse und Dispo", Location = new Point(300, 253), Size = new Size(280, 89) };
            this.sharePriceInput = AddInput(this.stockBox, "Aktienkurs (DM)", 7, 200, 18, 70);
            AddLabel(this.stockBox, "Ausgegebene Aktien", 7, 41);
            this.issuedSharesValue = new Label { AutoSize = false, TextAlign = ContentAlignment.MiddleRight, Location = new Point(150, 44), Size = new Size(120, 13), Text = "-" };
            this.stockBox.Controls.Add(this.issuedSharesValue);
            this.overdraftLimitInput = AddInput(this.stockBox, "Dispo (x 100.000 DM)", 7, 200, 62, 70);

            this.bankTab.Controls.AddRange(new Control[] { loansBox, investmentsBox, this.ratesBox, indicesBox, this.stockBox });
            this.ClubTabControl.Controls.Add(this.bankTab);

            foreach (TextBox input in new[] { this.standingPriceInput, this.seatPriceInput, this.securityInput, this.slushFundRateInput, this.sharePriceInput, this.overdraftLimitInput })
            {
                input.KeyPress += this.NumericOnly_KeyPress;
                input.TextChanged += this.Int16Range_TextChanged;
            }
            foreach (TextBox input in new[] { this.seasonTicketDiscountInput, this.seasonTicketQuotaInput })
            {
                input.KeyPress += this.NumericOnly_KeyPress;
                input.TextChanged += this.ByteMax255_TextChanged;
            }
            foreach (TextBox input in new[] { this.slushFundInput, this.legalCostsInput, this.smallStatisticsCostInput, this.largeStatisticsCostInput, this.computerCostInput })
            {
                input.KeyPress += this.NumericOnly_KeyPress;
                input.TextChanged += this.IntMaxNumber_TextChanged;
            }
        }

        // Called from InitClubTabControl, once the club's binding source exists.
        private void BindBudget()
        {
            this.standingPriceInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.StandingPrice));
            this.seatPriceInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.SeatPrice));
            this.seasonTicketDiscountInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.SeasonTicketDiscount));
            this.seasonTicketQuotaInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.SeasonTicketQuota));
            this.securityInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.Security));
            this.lawFirmInput.DataBindings.Add("SelectedValue", this.bindingSource, nameof(Club.LawFirm));
            this.legalCostsInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.LegalCosts));
            this.statisticsInput.DataBindings.Add("SelectedValue", this.bindingSource, nameof(Club.StatisticsDepartment));
            this.computerInput.DataBindings.Add("Checked", this.bindingSource, nameof(Club.HasComputerDepartment), false, DataSourceUpdateMode.OnPropertyChanged);
            this.slushFundInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.SlushFund));
            this.slushFundRateInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.SlushFundRate));
            this.sharePriceInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.SharePrice));
            this.sharePriceInput.DataBindings.Add("Enabled", this.bindingSource, nameof(Club.IsListed));
            this.overdraftLimitInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.OverdraftLimit));

            this.ratesBindingSource = new BindingSource { DataSource = typeof(InterestRates) };
            this.depositRateInput.DataBindings.Add("Text", this.ratesBindingSource, nameof(InterestRates.Deposit));
            this.overdraftRateInput.DataBindings.Add("Text", this.ratesBindingSource, nameof(InterestRates.Overdraft));
            this.overLimitRateInput.DataBindings.Add("Text", this.ratesBindingSource, nameof(InterestRates.OverLimit));
            this.loanRateInput.DataBindings.Add("Text", this.ratesBindingSource, nameof(InterestRates.Loan));
            this.callMoneyRateInput.DataBindings.Add("Text", this.ratesBindingSource, nameof(InterestRates.CallMoney));
            this.corporationTaxInput.DataBindings.Add("Text", this.ratesBindingSource, nameof(InterestRates.CorporationTax));

            this.costsBindingSource = new BindingSource { DataSource = typeof(GameCosts) };
            this.smallStatisticsCostInput.DataBindings.Add("Text", this.costsBindingSource, nameof(GameCosts.SmallStatistics));
            this.largeStatisticsCostInput.DataBindings.Add("Text", this.costsBindingSource, nameof(GameCosts.LargeStatistics));
            this.computerCostInput.DataBindings.Add("Text", this.costsBindingSource, nameof(GameCosts.Computer));
            this.ShowBudget();
        }

        // Loans, investments, rates and costs aren't plain club fields: shown again on every refresh.
        private void ShowBudget()
        {
            this.loansGrid.DataSource = this.club?.Loans;
            this.investmentsGrid.DataSource = this.club?.Investments;
            this.issuedSharesValue.Text = this.club != null && this.club.IsListed ? this.club.IssuedShares.ToString("N0") : "-";
            if (this.ratesBindingSource == null)
                return;
            bool running = this.IsGameRunning(silent: true);
            FixedAddresses fixedAddresses = running ? FixedAddresses.For(this.Memory) : null;
            this.interestRates = running ? GameRates.Read(this.Memory, fixedAddresses) : null;
            this.ratesBindingSource.DataSource = (object)this.interestRates ?? typeof(InterestRates);
            this.ratesBindingSource.ResetBindings(false);
            this.ratesBox.Enabled = this.interestRates != null;

            this.gameCosts = running ? GameRates.ReadCosts(this.Memory, fixedAddresses) : null;
            this.costsBindingSource.DataSource = (object)this.gameCosts ?? typeof(GameCosts);
            this.costsBindingSource.ResetBindings(false);
            this.lawFirmFeesGrid.DataSource = this.gameCosts?.LawFirms;
            this.indicesGrid.DataSource = this.gameCosts?.Indices;
            this.costsBox.Enabled = this.gameCosts != null;
        }

        // A new loan of 1 Mio. DM for a year, repayable early, at the game's current loan offer.
        private void AddLoan()
        {
            if (this.club == null || this.club.Loans.Count >= Settings.ClubLoanSlots)
                return;
            short baseRate = this.interestRates != null ? (short)System.Math.Round(this.interestRates.Loan * 100m) : (short)800;
            this.club.Loans.Add(ClubLoan.New(1000000, 0, true, baseRate));
        }

        private void RemoveLoan()
        {
            if (this.club != null && this.loansGrid.CurrentRow?.DataBoundItem is ClubLoan loan)
                this.club.Loans.Remove(loan);
        }

        private void AddInvestment()
        {
            if (this.club == null || this.club.Investments.Count >= Settings.ClubInvestmentSlots)
                return;
            this.club.Investments.Add(ClubInvestment.New(0, 100000, this.club.InvestmentIndices));
        }

        private void RemoveInvestment()
        {
            if (this.club != null && this.investmentsGrid.CurrentRow?.DataBoundItem is ClubInvestment investment)
                this.club.Investments.Remove(investment);
        }

        private void SaveRates()
        {
            this.Validate();
            FixedAddresses fixedAddresses = FixedAddresses.For(this.Memory);
            GameRates.Write(this.Memory, fixedAddresses, this.interestRates);
            GameRates.WriteCosts(this.Memory, fixedAddresses, this.gameCosts);
        }
    }
}
