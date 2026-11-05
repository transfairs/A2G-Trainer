using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for MechanicsView's choice of manager for the job offer when the list of managers changes.</summary>
    public class MechanicsViewAdditionalTests
    {
        [Fact]
        public void SetTrainers_KeepsTheChosenManagerWhileHeIsStillThere() => StaThread.Run(() =>
        {
            using (MechanicsView view = new MechanicsView(new ProcessMemory(), null))
            {
                ComboBox trainers = (ComboBox)typeof(MechanicsView).GetField("jobOfferTrainerInput", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                KeyValuePair<int, Coach> first = new KeyValuePair<int, Coach>(0, new Coach { Firstname = "Erwin", Lastname = "Kramer" });
                KeyValuePair<int, Coach> second = new KeyValuePair<int, Coach>(1, new Coach { Firstname = "Kai", Lastname = "Keller" });

                view.SetTrainers(new List<KeyValuePair<int, Coach>> { first, second });
                Assert.Equal(0, trainers.SelectedValue);
                trainers.SelectedValue = 1;

                // The list comes again (e.g. after a reload): the second manager stays chosen, not the first.
                view.SetTrainers(new List<KeyValuePair<int, Coach>> { first, second });

                Assert.Equal(1, trainers.SelectedValue);
                Assert.Equal("Kai Keller", trainers.Text);
            }
        });
    }
}
