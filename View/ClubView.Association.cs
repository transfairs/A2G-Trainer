using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// "Verband" tab: the association's requirements with their limits and the licence for the new
    /// season. Built in code, like the budget tab.
    /// </summary>
    public partial class ClubView
    {
        private TabPage associationTab;
        private readonly Dictionary<ClubEnums.Requirement, CheckBox> requirementInputs = new Dictionary<ClubEnums.Requirement, CheckBox>();
        private TextBox spectatorLimitInput, debtLimitInput, transferSurplusInput, transferLimitInput, salaryLimitInput;
        private CheckBox licenceRefusedInput;
        private readonly List<KeyValuePair<CheckBox, Func<Club, bool>>> lockInputs = new List<KeyValuePair<CheckBox, Func<Club, bool>>>();
        private TextBox shortCampsInput, sponsorRequestsInput, scoutSpendingInput;
        // Set while the requirement boxes are filled from the club, so their handlers don't write back.
        private bool showingRequirements;

        // A check box for one lock bit of the club, filled by ShowRequirements.
        private KeyValuePair<CheckBox, Func<Club, bool>> AddLockCheck(Control box, string text, int y, Func<Club, bool> get, Action<Club, bool> set)
        {
            CheckBox check = new CheckBox { Text = text, Location = new Point(10, y), AutoSize = true };
            check.CheckedChanged += (s, e) =>
            {
                if (!this.showingRequirements && !this.showingBlock && this.club != null)
                    set(this.club, check.Checked);
            };
            box.Controls.Add(check);
            return new KeyValuePair<CheckBox, Func<Club, bool>>(check, get);
        }

        private void BuildAssociationTab()
        {
            this.associationTab = new TabPage { Text = "Verband", UseVisualStyleBackColor = true, AutoScroll = true, Padding = new Padding(3) };

            GroupBox requirementsBox = new GroupBox { Text = "Auflagen", Location = new Point(6, 6), Size = new Size(330, 210) };
            int y = 20;
            foreach (ClubEnums.Requirement requirement in Enum.GetValues(typeof(ClubEnums.Requirement)).Cast<ClubEnums.Requirement>())
            {
                CheckBox check = new CheckBox { Text = ClubEnums.GetDescription(requirement), Location = new Point(10, y), AutoSize = true, Tag = requirement };
                check.CheckedChanged += this.Requirement_CheckedChanged;
                requirementsBox.Controls.Add(check);
                this.requirementInputs[requirement] = check;
                TextBox limit = null;
                switch (requirement)
                {
                    case ClubEnums.Requirement.SpectatorLimit: limit = this.spectatorLimitInput = new TextBox(); break;
                    case ClubEnums.Requirement.DebtLimit: limit = this.debtLimitInput = new TextBox(); break;
                    case ClubEnums.Requirement.TransferSurplus: limit = this.transferSurplusInput = new TextBox(); break;
                    case ClubEnums.Requirement.TransferLimit: limit = this.transferLimitInput = new TextBox(); break;
                    case ClubEnums.Requirement.SalaryLimit: limit = this.salaryLimitInput = new TextBox(); break;
                }
                if (limit != null)
                {
                    limit.Location = new Point(260, y - 2);
                    limit.Size = new Size(60, 20);
                    limit.TextAlign = HorizontalAlignment.Right;
                    limit.KeyPress += this.NumericOnly_KeyPress;
                    requirementsBox.Controls.Add(limit);
                }
                y += 26;
            }
            this.spectatorLimitInput.TextChanged += this.ByteMax255_TextChanged;
            this.debtLimitInput.TextChanged += this.ByteMax255_TextChanged;
            this.salaryLimitInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.transferSurplusInput.TextChanged += this.Int16Range_TextChanged;
            this.transferLimitInput.TextChanged += this.Int16Range_TextChanged;

            GroupBox licenceBox = new GroupBox { Text = "Lizenz", Location = new Point(342, 6), Size = new Size(238, 50) };
            this.licenceRefusedInput = new CheckBox { Text = "Lizenz verweigert", Location = new Point(10, 20), AutoSize = true };
            this.licenceRefusedInput.CheckedChanged += (s, e) =>
            {
                if (!this.showingRequirements && !this.showingBlock && this.club != null)
                    this.club.LicenceRefused = this.licenceRefusedInput.Checked;
            };
            licenceBox.Controls.Add(this.licenceRefusedInput);

            GroupBox locksBox = new GroupBox { Text = "Sperren", Location = new Point(342, 62), Size = new Size(238, 200) };
            this.lockInputs.Add(this.AddLockCheck(locksBox, "Schiri-Bestechung versucht", 20, c => (c.Bribery & 0x80) != 0, (c, on) => c.Bribery = (byte)(on ? c.Bribery | 0x80 : c.Bribery & 0x7F)));
            this.lockInputs.Add(this.AddLockCheck(locksBox, "Gegner-Bestechung angefragt", 44, c => (c.Bribery & 0x20) != 0, (c, on) => c.Bribery = (byte)(on ? c.Bribery | 0x20 : c.Bribery & 0xDF)));
            this.lockInputs.Add(this.AddLockCheck(locksBox, "Gegner bezahlt", 68, c => (c.Bribery & 0x10) != 0, (c, on) => c.Bribery = (byte)(on ? c.Bribery | 0x10 : c.Bribery & 0xEF)));
            this.lockInputs.Add(this.AddLockCheck(locksBox, "Kurztrainingslager gebucht", 92, c => (c.ShortCamp & 0x800) != 0, (c, on) => c.ShortCamp = on ? c.ShortCamp | 0x800 : c.ShortCamp & ~0x800u));
            this.shortCampsInput = AddInput(locksBox, "Kurztrainingslager Saison", 7, 178, 118, 50);
            this.sponsorRequestsInput = AddInput(locksBox, "Bitten an den Sponsor", 7, 178, 144, 50);
            this.scoutSpendingInput = AddInput(locksBox, "Spielerbeobachter (DM)", 7, 138, 170, 90);
            this.shortCampsInput.KeyPress += this.NumericOnly_KeyPress;
            this.shortCampsInput.TextChanged += this.ByteMax255_TextChanged;
            this.sponsorRequestsInput.KeyPress += this.NumericOnly_KeyPress;
            this.sponsorRequestsInput.TextChanged += this.ByteMax255_TextChanged;
            this.scoutSpendingInput.KeyPress += this.NumericOnly_KeyPress;
            this.scoutSpendingInput.TextChanged += this.IntMaxNumber_TextChanged;

            this.associationTab.Controls.AddRange(new Control[] { requirementsBox, licenceBox, locksBox });
            this.ClubTabControl.Controls.Add(this.associationTab);
        }

        // Called from InitClubTabControl, once the club's binding source exists. Both transfer
        // requirements share one stored limit, so both inputs show it.
        private void BindAssociation()
        {
            this.spectatorLimitInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.SpectatorLimit));
            this.debtLimitInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.DebtLimit));
            this.salaryLimitInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.SalaryLimit));
            this.transferSurplusInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.TransferLimit));
            this.transferLimitInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.TransferLimit));
            this.shortCampsInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.ShortCampsThisSeason));
            this.sponsorRequestsInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.SponsorRequests));
            this.scoutSpendingInput.DataBindings.Add("Text", this.bindingSource, nameof(Club.ScoutSpending));
            this.ShowRequirements();
        }

        // The requirement bits and the licence aren't plain bindings: shown again on every refresh.
        // RefreshValues clears every box first (showingBlock); that must not reach the club, which
        // after a save is the same object shown again.
        private void ShowRequirements()
        {
            this.showingRequirements = true;
            try
            {
                foreach (KeyValuePair<ClubEnums.Requirement, CheckBox> entry in this.requirementInputs)
                    entry.Value.Checked = this.club != null && this.club.HasRequirement(entry.Key);
                this.licenceRefusedInput.Checked = this.club != null && this.club.LicenceRefused;
                foreach (KeyValuePair<CheckBox, Func<Club, bool>> entry in this.lockInputs)
                    entry.Key.Checked = this.club != null && entry.Value(this.club);
            }
            finally
            {
                this.showingRequirements = false;
            }
            this.UpdateRequirementLimits();
        }

        private void Requirement_CheckedChanged(object sender, EventArgs e)
        {
            if (this.showingRequirements || this.showingBlock || this.club == null)
                return;
            CheckBox check = (CheckBox)sender;
            this.club.SetRequirement((ClubEnums.Requirement)check.Tag, check.Checked);
            this.ShowRequirements();
        }

        // A limit can only be set while its requirement is imposed.
        private void UpdateRequirementLimits()
        {
            this.spectatorLimitInput.Enabled = this.requirementInputs[ClubEnums.Requirement.SpectatorLimit].Checked;
            this.debtLimitInput.Enabled = this.requirementInputs[ClubEnums.Requirement.DebtLimit].Checked;
            this.transferSurplusInput.Enabled = this.requirementInputs[ClubEnums.Requirement.TransferSurplus].Checked;
            this.transferLimitInput.Enabled = this.requirementInputs[ClubEnums.Requirement.TransferLimit].Checked;
            this.salaryLimitInput.Enabled = this.requirementInputs[ClubEnums.Requirement.SalaryLimit].Checked;
        }
    }
}
