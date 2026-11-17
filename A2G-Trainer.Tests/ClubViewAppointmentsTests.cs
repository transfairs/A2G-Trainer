using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for the club view's "Terminplaner" tab: listing both plans, cancelling, moving and adding
    /// appointments through its buttons, and the tab without a human-run club.
    /// </summary>
    public class ClubViewAppointmentsTests
    {
        private const PlayerEnums.Country Country = AppointmentPlanTests.Country;

        private static T Field<T>(object target, string name) =>
            (T)typeof(ClubView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void SetField(object target, string name, object value) =>
            typeof(ClubView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static void Invoke(object target, string name) =>
            typeof(ClubView).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null).Invoke(target, null);

        private static void Show(ClubView view, AppointmentPlan plan) =>
            typeof(ClubView).GetMethod("ShowAppointments", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(AppointmentPlan) }, null).Invoke(view, new object[] { plan });

        // Whether the control is meant to show; Visible also asks the (hidden) parents.
        private static bool Shown(Control control) =>
            (bool)typeof(Control).GetMethod("GetState", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(control, new object[] { 2 });

        private static ListView List(ClubView view) => Field<ListView>(view, "appointmentList");

        private static string[] Row(ClubView view, int index) =>
            List(view).Items[index].SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text).ToArray();

        private static void Select(ClubView view, int index)
        {
            List(view).SelectedIndices.Clear();
            List(view).Items[index].Selected = true;
        }

        // The own club's view on a fake game with the given plans.
        private static void WithPlan(Action<FakeModule, ClubView> test, Action<FakeModule> seed) => StaThread.Run(() =>
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            using (ClubView view = new ClubView(fake.Memory, null))
            {
                seed(fake);
                // The list raises its selection events only with a window handle.
                Assert.NotEqual(IntPtr.Zero, List(view).Handle);
                Show(view, AppointmentPlanTests.Read(fake));
                test(fake, view);
            }
        });

        [Fact]
        public void WithoutAHumanClub_TheTabIsEmptyAndLocked() => StaThread.Run(() =>
        {
            using (ClubView view = new ClubView(new ProcessMemory(), new ProcessController(null)))
            {
                Invoke(view, "ShowAppointments");
                Assert.Equal("Terminplaner", Field<TabPage>(view, "appointmentTab").Text);
                Assert.Empty(List(view).Items);
                Assert.Equal("Kein Verein mit menschlichem Manager", Field<Label>(view, "appointmentStatus").Text);
                Assert.False(Field<GroupBox>(view, "appointmentBox").Enabled);
                Assert.False(Field<GroupBox>(view, "newAppointmentBox").Enabled);

                // The buttons do nothing then.
                Invoke(view, "CancelAppointment");
                Invoke(view, "MoveAppointment");
                Invoke(view, "AddAppointment");
                Assert.Equal("Kein Verein mit menschlichem Manager", Field<Label>(view, "appointmentStatus").Text);
            }
        });

        [Fact]
        public void List_ShowsBothPlans_AndTheButtonsFollowTheSelection() => WithPlan((fake, view) =>
        {
            Assert.Equal(4, List(view).Items.Count);
            Assert.Equal(new[] { "Fr Tag 124", "Manager", "Fanclubtreffen", "" }, Row(view, 0));
            Assert.Equal(new[] { "Sa Tag 125", "Trainer, Manager", "Spiel", "gegen Lübeck" }, Row(view, 1));
            Assert.Equal(new[] { "So Tag 126 (7 Tage)", "Trainer", "Trainingslager", "Regeneration, Höhentrainingslager in den Anden" }, Row(view, 2));
            Assert.Equal(new[] { "Sa Tag 139", "Trainer, Manager", "Länderspiel", "Deutschland" }, Row(view, 3));
            Assert.True(Field<GroupBox>(view, "appointmentBox").Enabled);

            Button cancel = Field<Button>(view, "appointmentCancelButton");
            Button move = Field<Button>(view, "appointmentMoveButton");
            Select(view, 1);
            Assert.False(cancel.Enabled);
            Assert.False(move.Enabled);
            Select(view, 0);
            Assert.True(cancel.Enabled);
            Assert.True(move.Enabled);
            ComboBox days = Field<ComboBox>(view, "appointmentMoveInput");
            Assert.DoesNotContain(days.Items.Cast<object>(), d => d.ToString() == "Fr Tag 124");
            Assert.Equal("Mi Tag 122", days.Items[0].ToString());
            // The camp can only go to another free Sunday.
            Select(view, 2);
            Assert.All(days.Items.Cast<object>(), d => Assert.StartsWith("So", d.ToString()));

            // Another club of the game has no plans.
            SetField(view, "club", new Club { Country = Country, Id = AppointmentPlanTests.OpponentId });
            Invoke(view, "ShowAppointments");
            Assert.Empty(List(view).Items);
            Assert.Equal("Kein Verein mit menschlichem Manager", Field<Label>(view, "appointmentStatus").Text);
        }, fake =>
        {
            AppointmentPlanTests.Manager(fake, 5, 0, 0x12);
            AppointmentPlanTests.Trainer(fake, 6, 0, 0x15, 0, 0, (byte)Country, 0, AppointmentPlanTests.OpponentId, 0);
            AppointmentPlanTests.Manager(fake, 6, 0, 0x15);
            for (int i = 0; i < 7; i++)
                AppointmentPlanTests.Trainer(fake, 7 + i, 0, 3, (byte)(i + 1), 0, 1, 0, 0, 0, 0, 0, 4, 0);
            AppointmentPlanTests.Trainer(fake, 20, 0, 0x15, 0, 0, (byte)Country, 0, 200, 0);
            AppointmentPlanTests.Manager(fake, 20, 0, 0x15);
        });

        [Fact]
        public void Buttons_CancelMoveAndAdd() => WithPlan((fake, view) =>
        {
            Label status = Field<Label>(view, "appointmentStatus");

            Select(view, 0);
            Field<ComboBox>(view, "appointmentMoveInput").SelectedIndex = 1;
            Invoke(view, "MoveAppointment");
            Assert.Equal("Fanclubtreffen auf Do Tag 123 verschoben.", status.Text);
            Assert.Equal("Do Tag 123", Row(view, 0)[0]);

            Select(view, 0);
            Invoke(view, "CancelAppointment");
            Assert.Equal("Fanclubtreffen am Do Tag 123 abgesagt.", status.Text);
            Assert.Empty(List(view).Items);

            // Nothing selected: nothing happens.
            Invoke(view, "CancelAppointment");
            Invoke(view, "MoveAppointment");
            Assert.Equal("Fanclubtreffen am Do Tag 123 abgesagt.", status.Text);

            ComboBox kind = Field<ComboBox>(view, "newKindInput");
            ComboBox day = Field<ComboBox>(view, "newDayInput");
            ComboBox campKind = Field<ComboBox>(view, "newCampKindInput");
            ComboBox opponent = Field<ComboBox>(view, "newOpponentInput");

            // A camp: only Sundays, with its type and place.
            kind.SelectedIndex = 0;
            Assert.True(Shown(campKind));
            Assert.False(Shown(opponent));
            Assert.All(day.Items.Cast<object>(), d => Assert.StartsWith("So", d.ToString()));
            campKind.SelectedIndex = 1;
            Field<ComboBox>(view, "newCampLocationInput").SelectedIndex = 3;
            Invoke(view, "AddAppointment");
            Assert.Equal("Trainingslager am So Tag 126 angelegt.", status.Text);
            Assert.Equal("Regeneration, Höhentrainingslager in den Anden", Row(view, 0)[3]);

            // A test match against the only other club, away, with a starting fee.
            kind.SelectedIndex = 1;
            Assert.True(Shown(opponent));
            Assert.False(Shown(campKind));
            Assert.Equal("Lübeck", ((ClubSquads.Entry)opponent.SelectedItem).Name);
            Assert.Equal(Country, Field<ComboBox>(view, "newOpponentCountryInput").SelectedValue);
            Field<ComboBox>(view, "newVenueInput").SelectedIndex = 1;
            Field<TextBox>(view, "newFeeInput").Text = "60";
            Invoke(view, "AddAppointment");
            Assert.Equal("Freundschaftsspiel am Mi Tag 122 angelegt.", status.Text);
            Assert.Equal(new[] { "Mi Tag 122", "Trainer", "Freundschaftsspiel", "bei Lübeck" }, Row(view, 0));
            Assert.Equal(60, fake.ReadModuleBytes(Settings.TestMatchTableOffset + 0x13, 1)[0]);

            // The club's own event.
            kind.SelectedIndex = 4;
            Assert.False(Shown(opponent));
            Invoke(view, "AddAppointment");
            Assert.Equal("Tag der offenen Tür am Do Tag 123 angelegt.", status.Text);

            // No opponent: the test match isn't arranged.
            kind.SelectedIndex = 1;
            opponent.Items.Clear();
            Invoke(view, "AddAppointment");
            Assert.Equal("Konnte nicht angelegt werden, Details in der Logdatei.", status.Text);
        }, fake => AppointmentPlanTests.Manager(fake, 5, 0, 0x12));

        [Fact]
        public void Buttons_ReportWhatTheGameRefuses() => WithPlan((fake, view) =>
        {
            Label status = Field<Label>(view, "appointmentStatus");
            // The negotiation's #70 entry is gone meanwhile.
            Select(view, 0);
            Invoke(view, "CancelAppointment");
            Assert.Equal("Konnte nicht abgesagt werden, Details in der Logdatei.", status.Text);
            Select(view, 0);
            Invoke(view, "MoveAppointment");
            Assert.Equal("Konnte nicht verschoben werden, Details in der Logdatei.", status.Text);
            Assert.EndsWith("(Lübeck)", Row(view, 0)[3]);
        }, fake => AppointmentPlanTests.Manager(fake, 4, 0, 0x0C, 0x70, 0x0A, (byte)Country, 0, AppointmentPlanTests.OpponentId, 0));

        [Fact]
        public void AppointmentDetails_NameWhatTheyCan()
        {
            List<ClubSquads.Entry> clubs = new List<ClubSquads.Entry> { new ClubSquads.Entry { Country = Country, Id = 1, Name = "Lübeck" } };
            Dictionary<ushort, string> players = new Dictionary<ushort, string> { { 5, "Uwe Schmelich" } };
            AppointmentPlan.Appointment Make(byte code, byte[] trainer = null, byte[] manager = null) => new AppointmentPlan.Appointment
            {
                Kind = AppointmentPlan.KindOf(code),
                InTrainerPlan = trainer != null,
                InManagerPlan = manager != null,
                TrainerEntry = (trainer ?? new byte[0]).Concat(new byte[12]).Take(12).ToArray(),
                ManagerEntry = (manager ?? new byte[0]).Concat(new byte[8]).Take(8).ToArray(),
            };

            Assert.Equal("Art 7, Ort 30", ClubView.AppointmentDetails(Make(0x03, new byte[] { 0, 3, 1, 0, 7, 0, 0, 0, 0, 0, 30, 0 }), clubs, players));
            Assert.Equal("Kondition, fällt aus", ClubView.AppointmentDetails(Make(0x03, new byte[] { 0, 3, 1, 0 }), clubs, players));
            Assert.Equal("Gegner offen", ClubView.AppointmentDetails(Make(0x14, new byte[] { 0, 0x14, 1, 0 }), clubs, players));
            Assert.Equal($"gegen {PlayerEnums.GetDescription(Country)} 9", ClubView.AppointmentDetails(Make(0x15, new byte[] { 0, 0x15, 0, 0, (byte)Country, 0, 9, 0 }), clubs, players));
            Assert.Equal("Uwe Schmelich", ClubView.AppointmentDetails(Make(0x04, manager: new byte[] { 0, 4, 5, 0 }), clubs, players));
            Assert.Equal("Uwe Schmelich (Lübeck)", ClubView.AppointmentDetails(Make(0x0B, manager: new byte[] { 0, 0x0B, 5, 0, (byte)Country, 0, 1, 0 }), clubs, players));
            Assert.Equal("Spieler 6", ClubView.AppointmentDetails(Make(0x11, manager: new byte[] { 0, 0x11, 6, 0 }), clubs, players));
            Assert.Equal("", ClubView.AppointmentDetails(Make(0x13, manager: new byte[] { 0, 0x13 }), clubs, players));
        }
    }
}
