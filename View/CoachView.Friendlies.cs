using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// The national coach's three friendlies, right of the contract in the "Nationaltrainer" box:
    /// opponent and venue of each, as long as it isn't played or about to be (NationalFriendly.IsLocked).
    /// Built in code, like the stock boxes.
    /// </summary>
    public partial class CoachView
    {
        private readonly ComboBox[] friendlyOpponentInputs = new ComboBox[3];
        private readonly CheckBox[] friendlyHomeInputs = new CheckBox[3];

        private void BuildFriendlyRows()
        {
            for (int n = 0; n < 3; n++)
            {
                int y = 20 + n * 27;
                Label label = new Label { AutoSize = true, Location = new Point(290, y + 3), Text = $"Freundschaftsspiel {n + 1}" };
                ComboBox opponent = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(405, y), Size = new Size(110, 21) };
                CheckBox home = new CheckBox { Text = "Heim", AutoSize = true, Location = new Point(520, y + 2) };
                int index = n;
                opponent.SelectionChangeCommitted += (s, e) =>
                {
                    NationalFriendly friendly = this.Friendly(index);
                    if (friendly != null && opponent.SelectedValue is PlayerEnums.Country country)
                        friendly.Opponent = country;
                };
                home.CheckedChanged += (s, e) =>
                {
                    NationalFriendly friendly = this.Friendly(index);
                    if (friendly != null)
                        friendly.AtHome = home.Checked;
                };
                this.NationalCoachBox.Controls.AddRange(new Control[] { label, opponent, home });
                this.friendlyOpponentInputs[n] = opponent;
                this.friendlyHomeInputs[n] = home;
            }
        }

        private NationalFriendly Friendly(int index) => this.coach?.Friendlies.FirstOrDefault(f => f.Index == index);

        // Any national team but the own one and those of other human national coaches (whose own
        // friendly list wouldn't know the match); "–" for no match.
        private void ShowFriendlies()
        {
            List<PlayerEnums.Country> taken = this.coach == null ? new List<PlayerEnums.Country>() : CoachController.NationalTeamsOfOtherTrainers(this.Memory, this.trainerIndex);
            var choices = NationalTeamChoices(taken)
                .Where(c => this.coach == null || c != this.coach.NationalTeam)
                .Select(c => new { Value = c, Text = c == PlayerEnums.Country.Sonstige ? "–" : PlayerEnums.GetDescription(c) })
                .ToList();
            for (int n = 0; n < 3; n++)
            {
                NationalFriendly friendly = this.Friendly(n);
                ComboBox opponent = this.friendlyOpponentInputs[n];
                opponent.DataSource = choices.ToList();
                opponent.DisplayMember = "Text";
                opponent.ValueMember = "Value";
                if (friendly != null && choices.All(c => c.Value != friendly.Opponent))
                    opponent.DataSource = choices.Concat(new[] { new { Value = friendly.Opponent, Text = $"Land {(byte)friendly.Opponent}" } }).ToList();
                opponent.SelectedValue = friendly?.Opponent ?? PlayerEnums.Country.Sonstige;
                this.friendlyHomeInputs[n].Checked = friendly?.AtHome ?? false;
                bool editable = friendly != null && !friendly.IsLocked;
                opponent.Enabled = editable;
                this.friendlyHomeInputs[n].Enabled = editable;
            }
        }
    }
}
