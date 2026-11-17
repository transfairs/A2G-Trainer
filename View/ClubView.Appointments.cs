using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// "Terminplaner" tab: the appointments of the club's next six weeks from both plans (see
    /// <see cref="AppointmentPlan"/>), for a club with a human manager. The selected one can be
    /// cancelled or moved, a camp, a test match and the club's own events can be added. Each button
    /// changes the game right away and reads the plan again; "Speichern" has nothing to do here.
    /// Built in code, like the budget tab.
    /// </summary>
    public partial class ClubView
    {
        private static readonly string[] WeekDayNames = { "So", "Mo", "Di", "Mi", "Do", "Fr", "Sa" };

        private TabPage appointmentTab;
        private ListView appointmentList;
        private GroupBox appointmentBox, newAppointmentBox;
        private Button appointmentCancelButton, appointmentMoveButton, appointmentAddButton;
        private ComboBox appointmentMoveInput, newKindInput, newDayInput, newCampKindInput, newCampLocationInput, newOpponentCountryInput, newOpponentInput, newVenueInput;
        private ClubSelection opponentSelection;
        private TextBox newFeeInput;
        private Label appointmentStatus;
        private Control[] campInputs, testMatchInputs;
        private AppointmentPlan appointmentPlan;
        private GameCalendar appointmentCalendar;
        private List<AppointmentPlan.Appointment> appointments = new List<AppointmentPlan.Appointment>();
        // Set while the tab is filled, so the selection handlers don't refill the choices.
        private bool showingAppointments;

        // A choice of plan days, shown with weekday and date.
        private sealed class DayChoice
        {
            /// <summary>The plan day this choice stands for.</summary>
            public int Day { get; set; }
            /// <summary>The day as shown in the drop-down (weekday and date).</summary>
            public string Text { get; set; }
            public override string ToString() => this.Text;
        }

        // A drop-down with its label; both are returned, so a row can be shown or hidden as a whole.
        private static ComboBox AddAppointmentChoice(Control box, string label, int y, out Label caption)
        {
            caption = AddLabel(box, label, 7, y);
            ComboBox combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(110, y), Size = new Size(180, 21) };
            box.Controls.Add(combo);
            return combo;
        }

        private void BuildAppointmentTab()
        {
            this.appointmentTab = new TabPage { Text = "Terminplaner", UseVisualStyleBackColor = true, AutoScroll = true, Padding = new Padding(3) };

            this.appointmentList = new ListView
            {
                Location = new Point(6, 6),
                Size = new Size(574, 150),
                View = System.Windows.Forms.View.Details,
                FullRowSelect = true,
                HideSelection = false,
                MultiSelect = false,
            };
            this.appointmentList.Columns.Add("Datum", 110);
            this.appointmentList.Columns.Add("Plan", 110);
            this.appointmentList.Columns.Add("Termin", 150);
            this.appointmentList.Columns.Add("Details", 180);
            this.appointmentList.SelectedIndexChanged += (s, e) => this.ShowSelectedAppointment();

            this.appointmentBox = new GroupBox { Text = "Ausgewählter Termin", Location = new Point(6, 162), Size = new Size(270, 76) };
            this.appointmentCancelButton = new Button { Text = "Absagen", Location = new Point(7, 18), Size = new Size(98, 23), UseVisualStyleBackColor = true };
            this.appointmentMoveInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(7, 47), Size = new Size(150, 21) };
            this.appointmentMoveButton = new Button { Text = "Verschieben", Location = new Point(163, 46), Size = new Size(98, 23), UseVisualStyleBackColor = true };
            this.appointmentCancelButton.Click += (s, e) => this.CancelAppointment();
            this.appointmentMoveButton.Click += (s, e) => this.MoveAppointment();
            this.appointmentBox.Controls.AddRange(new Control[] { this.appointmentCancelButton, this.appointmentMoveInput, this.appointmentMoveButton });

            this.appointmentStatus = new Label { AutoSize = false, Location = new Point(9, 246), Size = new Size(264, 60), Text = "" };

            this.newAppointmentBox = new GroupBox { Text = "Neuer Termin", Location = new Point(282, 162), Size = new Size(298, 182) };
            this.newKindInput = AddAppointmentChoice(this.newAppointmentBox, "Art", 20, out _);
            this.newKindInput.Items.AddRange(AppointmentPlan.AddableKinds.Select(k => (object)AppointmentPlan.KindOf(k).Name).ToArray());
            this.newDayInput = AddAppointmentChoice(this.newAppointmentBox, "Datum", 46, out _);
            this.newCampKindInput = AddAppointmentChoice(this.newAppointmentBox, "Lagerart", 72, out Label campKindLabel);
            this.newCampKindInput.Items.AddRange(AppointmentPlan.CampKindNames.Cast<object>().ToArray());
            this.newCampLocationInput = AddAppointmentChoice(this.newAppointmentBox, "Ort", 98, out Label campLocationLabel);
            this.newCampLocationInput.Items.AddRange(AppointmentPlan.CampLocationNames.Skip(1).Cast<object>().ToArray());
            this.newOpponentCountryInput = AddAppointmentChoice(this.newAppointmentBox, "Land", 72, out Label opponentCountryLabel);
            this.newOpponentInput = AddAppointmentChoice(this.newAppointmentBox, "Gegner", 98, out Label opponentLabel);
            this.opponentSelection = new ClubSelection(this.newOpponentCountryInput, this.newOpponentInput);
            this.newVenueInput = AddAppointmentChoice(this.newAppointmentBox, "Spielort", 124, out Label venueLabel);
            this.newVenueInput.Items.AddRange(new object[] { "Zu Hause", "Auswärts" });
            this.newFeeInput = AddInput(this.newAppointmentBox, "Antrittsprämie", 7, 110, 151, 40);
            Label feeLabel = this.newAppointmentBox.Controls.OfType<Label>().Last();
            this.newFeeInput.KeyPress += this.NumericOnly_KeyPress;
            this.newFeeInput.TextChanged += this.ByteMax255_TextChanged;
            this.appointmentAddButton = new Button { Text = "Anlegen", Location = new Point(192, 150), Size = new Size(98, 23), UseVisualStyleBackColor = true };
            this.appointmentAddButton.Click += (s, e) => this.AddAppointment();
            this.newAppointmentBox.Controls.Add(this.appointmentAddButton);
            this.campInputs = new Control[] { this.newCampKindInput, campKindLabel, this.newCampLocationInput, campLocationLabel };
            this.testMatchInputs = new Control[] { this.newOpponentCountryInput, opponentCountryLabel, this.newOpponentInput, opponentLabel, this.newVenueInput, venueLabel, this.newFeeInput, feeLabel };
            this.newKindInput.SelectedIndexChanged += (s, e) => this.ShowNewAppointmentKind();

            this.appointmentTab.Controls.AddRange(new Control[] { this.appointmentList, this.appointmentBox, this.appointmentStatus, this.newAppointmentBox });
            this.ClubTabControl.Controls.Add(this.appointmentTab);
        }

        // Reads the shown club's plans and lists them; a club without a human manager shows nothing.
        private void ShowAppointments()
        {
            bool running = this.club != null && this.IsGameRunning(silent: true);
            FixedAddresses fixedAddresses = running ? FixedAddresses.For(this.Memory) : null;
            this.ShowAppointments(fixedAddresses == null ? null : AppointmentPlan.Read(this.Memory, fixedAddresses, this.club.Country, this.club.Id));
        }

        private void ShowAppointments(AppointmentPlan plan)
        {
            this.appointmentPlan = plan;
            this.appointmentCalendar = plan != null ? GameCalendar.Read(this.Memory) : null;
            this.appointments = plan?.Appointments() ?? new List<AppointmentPlan.Appointment>();
            this.appointmentBox.Enabled = this.newAppointmentBox.Enabled = plan != null;
            this.appointmentStatus.Text = "";
            this.showingAppointments = true;
            try
            {
                this.appointmentList.Items.Clear();
                if (plan == null)
                {
                    this.appointmentStatus.Text = "Kein Verein mit menschlichem Manager";
                    this.newDayInput.DataSource = null;
                    this.appointmentMoveInput.DataSource = null;
                    return;
                }
                FixedAddresses fixedAddresses = FixedAddresses.For(this.Memory);
                List<ClubSquads.Entry> clubs = ClubSquads.Clubs(this.Memory, fixedAddresses);
                Dictionary<ushort, string> players = NationalSquads.Members(this.Memory, fixedAddresses, this.appointments.Select(a => a.PlayerId).Where(id => id != 0).Distinct())
                    .GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First().Name);
                foreach (AppointmentPlan.Appointment appointment in this.appointments)
                {
                    ListViewItem item = new ListViewItem(this.DayText(appointment.Day) + (appointment.Length > 1 ? $" ({appointment.Length} Tage)" : ""));
                    item.SubItems.Add(appointment.InTrainerPlan && appointment.InManagerPlan ? "Trainer, Manager" : appointment.InTrainerPlan ? "Trainer" : "Manager");
                    item.SubItems.Add(appointment.Kind.Code == 0x15 && appointment.Club?.Value == 200 ? "Länderspiel" : appointment.Kind.Name);
                    item.SubItems.Add(AppointmentDetails(appointment, clubs, players));
                    item.Tag = appointment;
                    this.appointmentList.Items.Add(item);
                }
                // The opponent chosen last stays chosen; first the club's own country.
                ClubSquads.Entry opponent = this.newOpponentInput.SelectedItem as ClubSquads.Entry;
                this.opponentSelection.Clubs = clubs.Where(c => !c.Is(plan.Country, plan.ClubId)).ToList();
                this.opponentSelection.Show((opponent == null ? null : this.opponentSelection.Find(opponent.Country, opponent.Id))
                    ?? this.opponentSelection.Clubs.Where(c => c.Country == plan.Country).OrderBy(c => c.Name).FirstOrDefault()
                    ?? this.opponentSelection.Clubs.OrderBy(c => c.Name).FirstOrDefault());
                if (this.newKindInput.SelectedIndex < 0)
                    this.newKindInput.SelectedIndex = 0;
                if (this.newCampKindInput.SelectedIndex < 0)
                    this.newCampKindInput.SelectedIndex = 0;
                if (this.newCampLocationInput.SelectedIndex < 0)
                    this.newCampLocationInput.SelectedIndex = 0;
                if (this.newVenueInput.SelectedIndex < 0)
                    this.newVenueInput.SelectedIndex = 0;
                if (this.newFeeInput.Text.Length == 0)
                    this.newFeeInput.Text = "0";
            }
            finally
            {
                this.showingAppointments = false;
            }
            this.ShowSelectedAppointment();
            this.ShowNewAppointmentKind();
        }

        // "Mo 16.11." for plan day `day`, "Tag 121" where the calendar isn't known.
        private string DayText(int day)
        {
            int seasonDay = this.appointmentPlan.ReferenceDay + day;
            DateTime? date = this.appointmentCalendar?.Date(seasonDay - this.appointmentPlan.Today);
            return $"{WeekDayNames[((seasonDay % 7) + 7) % 7]} " + (date.HasValue ? date.Value.ToString("dd.MM.") : $"Tag {seasonDay}");
        }

        internal static string AppointmentDetails(AppointmentPlan.Appointment appointment, List<ClubSquads.Entry> clubs, Dictionary<ushort, string> players)
        {
            string ClubName(KeyValuePair<PlayerEnums.Country, ushort> club) =>
                clubs.FirstOrDefault(c => c.Is(club.Key, club.Value))?.Name ?? $"{PlayerEnums.GetDescription(club.Key)} {club.Value}";
            KeyValuePair<PlayerEnums.Country, ushort>? other = appointment.Club;
            switch (appointment.Kind.Code)
            {
                case 0x03:
                    int kind = appointment.CampKind, location = appointment.CampLocation;
                    return (kind >= 0 && kind < AppointmentPlan.CampKindNames.Length ? AppointmentPlan.CampKindNames[kind] : $"Art {kind}") + ", "
                        + (location >= 0 && location < AppointmentPlan.CampLocationNames.Length ? AppointmentPlan.CampLocationNames[location] : $"Ort {location}");
                case 0x14:
                case 0x15:
                    if (!other.HasValue)
                        return "Gegner offen";
                    if (other.Value.Value == 200)
                        return PlayerEnums.GetDescription(other.Value.Key);
                    return (appointment.TrainerEntry[8] == 1 ? "bei " : "gegen ") + ClubName(other.Value);
            }
            string text = appointment.PlayerId != 0 ? (players.TryGetValue(appointment.PlayerId, out string name) ? name : $"Spieler {appointment.PlayerId}") : "";
            if ((appointment.Kind.Code == 0x0B || appointment.Kind.Code == 0x0C) && other.HasValue)
                text += $" ({ClubName(other.Value)})";
            return text;
        }

        private AppointmentPlan.Appointment SelectedAppointment =>
            this.appointmentList.SelectedItems.Count == 1 ? this.appointmentList.SelectedItems[0].Tag as AppointmentPlan.Appointment : null;

        // Cancel and move only for an appointment that allows it; the move choices are the days it fits.
        private void ShowSelectedAppointment()
        {
            if (this.showingAppointments)
                return;
            AppointmentPlan.Appointment appointment = this.SelectedAppointment;
            bool cancel = appointment != null && this.appointmentPlan != null && this.appointmentPlan.CanCancel(appointment);
            bool move = appointment != null && this.appointmentPlan != null && this.appointmentPlan.CanMove(appointment);
            List<DayChoice> days = move ? this.FreeDays(appointment.Kind, appointment) : new List<DayChoice>();
            this.appointmentCancelButton.Enabled = cancel;
            this.appointmentMoveInput.DataSource = days.Count > 0 ? days : null;
            this.appointmentMoveInput.Enabled = this.appointmentMoveButton.Enabled = days.Count > 0;
        }

        private List<DayChoice> FreeDays(AppointmentPlan.Kind kind, AppointmentPlan.Appointment except = null) =>
            Enumerable.Range(0, AppointmentPlan.Days)
                .Where(d => (except == null || d != except.Day) && this.appointmentPlan.Fits(kind, d, except))
                .Select(d => new DayChoice { Day = d, Text = this.DayText(d) })
                .ToList();

        // The inputs of the type chosen and the days it fits.
        private void ShowNewAppointmentKind()
        {
            if (this.showingAppointments || this.appointmentPlan == null || this.newKindInput.SelectedIndex < 0)
                return;
            byte code = AppointmentPlan.AddableKinds[this.newKindInput.SelectedIndex];
            foreach (Control control in this.campInputs.Concat(this.testMatchInputs))
                control.Visible = false;
            foreach (Control control in code == 0x03 ? this.campInputs : code == 0x14 ? this.testMatchInputs : new Control[0])
                control.Visible = true;
            List<DayChoice> days = this.FreeDays(AppointmentPlan.KindOf(code));
            this.newDayInput.DataSource = days.Count > 0 ? days : null;
            this.appointmentAddButton.Enabled = days.Count > 0;
        }

        private void CancelAppointment()
        {
            AppointmentPlan.Appointment appointment = this.SelectedAppointment;
            if (appointment == null || this.appointmentPlan == null)
                return;
            bool done = this.appointmentPlan.Cancel(appointment);
            this.ShowAppointmentResult(done ? $"{appointment.Kind.Name} am {this.DayText(appointment.Day)} abgesagt." : "Konnte nicht abgesagt werden, Details in der Logdatei.");
        }

        private void MoveAppointment()
        {
            AppointmentPlan.Appointment appointment = this.SelectedAppointment;
            if (appointment == null || this.appointmentPlan == null || !(this.appointmentMoveInput.SelectedItem is DayChoice day))
                return;
            bool done = this.appointmentPlan.Move(appointment, day.Day);
            this.ShowAppointmentResult(done ? $"{appointment.Kind.Name} auf {day.Text} verschoben." : "Konnte nicht verschoben werden, Details in der Logdatei.");
        }

        private void AddAppointment()
        {
            if (this.appointmentPlan == null || this.newKindInput.SelectedIndex < 0 || !(this.newDayInput.SelectedItem is DayChoice day))
                return;
            byte code = AppointmentPlan.AddableKinds[this.newKindInput.SelectedIndex];
            bool done;
            if (code == 0x03)
                done = this.appointmentPlan.AddCamp(day.Day, this.newCampKindInput.SelectedIndex, this.newCampLocationInput.SelectedIndex + 1);
            else if (code == 0x14)
                done = this.newOpponentInput.SelectedItem is ClubSquads.Entry opponent
                    && this.appointmentPlan.AddTestMatch(day.Day, opponent.Country, opponent.Id, this.newVenueInput.SelectedIndex == 1,
                        byte.TryParse(this.newFeeInput.Text, out byte fee) ? fee : (byte)0);
            else
                done = this.appointmentPlan.AddSimple(code, day.Day);
            this.ShowAppointmentResult(done ? $"{AppointmentPlan.KindOf(code).Name} am {day.Text} angelegt." : "Konnte nicht angelegt werden, Details in der Logdatei.");
        }

        // Reads the plan again after a change and says how it went.
        private void ShowAppointmentResult(string message)
        {
            this.ShowAppointments(this.appointmentPlan.Reload());
            this.appointmentStatus.Text = message;
        }
    }
}
