using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// The country and club choice of the player and club views: fills both from the clubs with a
    /// record (ClubSquads), selects the club shown and reports a club the user picks.
    /// </summary>
    internal sealed class ClubSelection
    {
        private readonly ComboBox countryInput;
        private readonly ComboBox clubInput;
        private List<ClubSquads.Entry> clubs = new List<ClubSquads.Entry>();
        private ClubSquads.Entry shown;
        private bool filling;

        /// <summary>Raised when the user picks a club other than the one shown.</summary>
        internal event Action<ClubSquads.Entry> Picked;

        /// <summary>Wires up the two combo boxes (DropDownList).</summary>
        internal ClubSelection(ComboBox countryInput, ComboBox clubInput)
        {
            this.countryInput = countryInput;
            this.clubInput = clubInput;
            this.countryInput.DisplayMember = "Text";
            this.countryInput.ValueMember = "Value";
            this.countryInput.SelectedIndexChanged += (s, e) => this.CountryPicked();
            this.clubInput.SelectedIndexChanged += (s, e) => this.ClubPicked();
        }

        /// <summary>The clubs offered, in record order; shown with the next <see cref="Show"/>.</summary>
        internal List<ClubSquads.Entry> Clubs { get => this.clubs; set => this.clubs = value ?? new List<ClubSquads.Entry>(); }

        /// <summary>The club of <paramref name="country"/>/<paramref name="id"/> among the clubs offered, or null.</summary>
        internal ClubSquads.Entry Find(PlayerEnums.Country country, ushort id) => this.clubs.FirstOrDefault(c => c.Is(country, id));

        /// <summary>Offers the <see cref="Clubs"/> and selects <paramref name="shown"/> (nothing if null).</summary>
        internal void Show(ClubSquads.Entry shown)
        {
            this.shown = shown;
            this.filling = true;
            try
            {
                this.countryInput.DataSource = this.clubs
                    .Select(c => c.Country)
                    .Distinct()
                    .OrderBy(CountryText)
                    .Select(c => new { Value = c, Text = CountryText(c) })
                    .ToList();
                if (shown != null)
                    this.countryInput.SelectedValue = shown.Country;
                else
                    this.countryInput.SelectedIndex = -1;
                this.FillClubs(shown?.Country);
                this.clubInput.SelectedItem = shown;
            }
            finally
            {
                this.filling = false;
            }
        }

        private static string CountryText(PlayerEnums.Country country) => PlayerEnums.GetDescription(country);

        private void FillClubs(PlayerEnums.Country? country)
        {
            this.clubInput.Items.Clear();
            if (country.HasValue)
                this.clubInput.Items.AddRange(this.clubs.Where(c => c.Country == country.Value).OrderBy(c => c.Name).Cast<object>().ToArray());
        }

        // Another country shows its first club.
        private void CountryPicked()
        {
            if (this.filling || !(this.countryInput.SelectedValue is PlayerEnums.Country country))
                return;
            this.filling = true;
            try
            {
                this.FillClubs(country);
                if (this.clubInput.Items.Count > 0)
                    this.clubInput.SelectedIndex = 0;
            }
            finally
            {
                this.filling = false;
            }
            this.ClubPicked();
        }

        private void ClubPicked()
        {
            if (this.filling || !(this.clubInput.SelectedItem is ClubSquads.Entry club) || club == this.shown)
                return;
            this.Picked?.Invoke(club);
        }
    }
}
