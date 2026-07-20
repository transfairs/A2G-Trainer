using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using System;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    public partial class CoachView : EntityView
    {
        private Coach coach;
        public CoachView(ProcessMemory memory, ProcessController controller) : base(memory, controller)
        {
            InitializeComponent();
        }

        internal void InitClubTabControl()
        {
            this.bindingSource = new BindingSource
            {
                DataSource = this.coach
            };

            this.LevelInput.KeyPress += this.NumericOnly_KeyPress;
            this.AgeInput.KeyPress += this.NumericOnly_KeyPress;

            this.LevelInput.TextChanged += this.ByteMax255_TextChanged;
            this.AgeInput.TextChanged += this.ByteMax255_TextChanged;

            this.LastNameInput.DataBindings.Add("Text", this.bindingSource, "Lastname");
            this.FirstNameInput.DataBindings.Add("Text", this.bindingSource, "Firstname");
            this.LevelInput.DataBindings.Add("Text", this.bindingSource, "Level");
            this.AgeInput.DataBindings.Add("Text", this.bindingSource, "Age");
        }

        internal void RefreshValues(PlayerEnums.AddressType type, Coach coach = null)
        {
            if (this.IsGameRunning())
            {
                this.ClearAllFields(this);


                this.coachController = new CoachController(this.Memory, this.processController.IsGog, type);
                this.coach = coach ?? this.CoachController.Coach;
                // Keep the controller pointed at the exact object the UI is bound to - otherwise a
                // caller-supplied coach (e.g. SaveBtn_Click re-displaying what it just saved) leaves
                // coachController.Coach as the freshly-read (now stale) instance, and the next Save()
                // silently writes that stale object instead of the user's newest edits.
                this.coachController.Coach = this.coach;

                if (this.bindingSource != null)
                {
                    this.bindingSource.DataSource = this.coach;
                    this.bindingSource.ResetBindings(false);
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
