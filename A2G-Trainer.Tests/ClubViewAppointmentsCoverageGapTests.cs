using AnstossTrainer.Shared.Tests;
using System;
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
    /// The rarer states of the club view's "Terminplaner" tab: a match without a known opponent,
    /// refilling the list with a selection, no opponent to offer, no free day for a new appointment
    /// and a starting fee that isn't a number.
    /// </summary>
    public class ClubViewAppointmentsCoverageGapTests
    {
        private const PlayerEnums.Country Country = AppointmentPlanTests.Country;

        private static T Field<T>(object target, string name) =>
            (T)typeof(ClubView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void Invoke(object target, string name) =>
            typeof(ClubView).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null).Invoke(target, null);

        private static void Show(ClubView view, AppointmentPlan plan) =>
            typeof(ClubView).GetMethod("ShowAppointments", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(AppointmentPlan) }, null).Invoke(view, new object[] { plan });

        private static ListView List(ClubView view) => Field<ListView>(view, "appointmentList");

        private static string[] Row(ClubView view, int index) =>
            List(view).Items[index].SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text).ToArray();

        private static void WithPlan(Action<FakeModule, ClubView> test, Action<FakeModule> seed, short today = AppointmentPlanTests.ReferenceDay + 2) => StaThread.Run(() =>
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake(today))
            using (ClubView view = new ClubView(fake.Memory, null))
            {
                seed(fake);
                Assert.NotEqual(IntPtr.Zero, List(view).Handle);
                Show(view, AppointmentPlanTests.Read(fake));
                test(fake, view);
            }
        });

        [Fact]
        public void Matches_WithoutAKnownOpponent_AreNoNationalMatch() => WithPlan((fake, view) =>
        {
            Assert.Equal(2, List(view).Items.Count);
            Assert.Equal(new[] { "Spiel", "Gegner offen" }, Row(view, 0).Skip(2).ToArray());
            Assert.Equal(new[] { "Manager", "Spiel", "Gegner offen" }, Row(view, 1).Skip(1).ToArray());
        }, fake =>
        {
            AppointmentPlanTests.Trainer(fake, 6, 0, 0x15, 0, 0);
            AppointmentPlanTests.Manager(fake, 8, 0, 0x15);
        });

        [Fact]
        public void ASelectionWhileTheTabFills_IsShownOnceItIsFilled() => StaThread.Run(() =>
        {
            using (FakeModule fake = AppointmentPlanTests.NewFake())
            using (ClubView view = new ClubView(fake.Memory, null))
            {
                AppointmentPlanTests.Manager(fake, 5, 0, 0x12);
                Assert.NotEqual(IntPtr.Zero, List(view).Handle);
                ComboBox moveDays = Field<ComboBox>(view, "appointmentMoveInput");
                bool daysThen = true;
                // Someone else selects the appointment when the first type is chosen, while the
                // tab is still filled: its move choices follow only once it is done.
                Field<ComboBox>(view, "newKindInput").SelectedIndexChanged += (s, e) =>
                {
                    List(view).Items[0].Selected = true;
                    daysThen = moveDays.DataSource != null;
                };

                Show(view, AppointmentPlanTests.Read(fake));

                Assert.False(daysThen);
                Assert.NotNull(moveDays.DataSource);
                Assert.True(Field<Button>(view, "appointmentCancelButton").Enabled);
            }
        });

        [Fact]
        public void WithoutAnotherClub_NoOpponentIsOffered() => WithPlan((fake, view) =>
        {
            ComboBox opponent = Field<ComboBox>(view, "newOpponentInput");
            Assert.Equal("Lübeck", ((ClubSquads.Entry)opponent.SelectedItem).Name);

            // Lübeck is gone: the opponent chosen last isn't found, and there is no other.
            fake.WriteModuleBytes(Settings.ClubRecordTableOffset + Settings.ClubRecordStride, new byte[] { 0 });
            Show(view, AppointmentPlanTests.Read(fake));
            Assert.Null(opponent.SelectedItem);
            Assert.Empty(opponent.Items);

            // Nor with nothing chosen before.
            Show(view, AppointmentPlanTests.Read(fake));
            Assert.Null(opponent.SelectedItem);
        }, fake => { });

        [Fact]
        public void OnTheLastDayOfThePlans_NothingCanBeAdded() => WithPlan((fake, view) =>
        {
            Assert.Null(Field<ComboBox>(view, "newDayInput").DataSource);
            Assert.False(Field<Button>(view, "appointmentAddButton").Enabled);
            Field<ComboBox>(view, "newKindInput").SelectedIndex = 1;
            Assert.False(Field<Button>(view, "appointmentAddButton").Enabled);
        }, fake => { }, today: AppointmentPlanTests.ReferenceDay + 41);

        [Fact]
        public void TestMatch_WithAStartingFeeThatIsNoNumber_HasNone() => WithPlan((fake, view) =>
        {
            Field<ComboBox>(view, "newKindInput").SelectedIndex = 1;
            Field<TextBox>(view, "newFeeInput").Text = "";
            Invoke(view, "AddAppointment");

            Assert.Equal("Freundschaftsspiel am Mi Tag 122 angelegt.", Field<Label>(view, "appointmentStatus").Text);
            Assert.Equal(0, fake.ReadModuleBytes(Settings.TestMatchTableOffset + 0x13, 1)[0]);
            Assert.Equal(1, fake.ReadModuleBytes(Settings.TestMatchTableOffset + 1, 1)[0]);
        }, fake => { });
    }
}
