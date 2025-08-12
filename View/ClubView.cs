using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Memory;
using System.ComponentModel;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    public partial class ClubView : EntityView
    {
        public ClubView(Mem memory, ProcessController controller) : base(memory, controller)
        {
            InitializeComponent();
        }

        public ClubView(IContainer container) : base(container)
        {
            container.Add(this);

            InitializeComponent();
        }

        internal void InitClubTabControl()
        {
            //this.NationalityCombo.DataSource = Enum.GetValues(typeof(PlayerEnums.Country)).Cast<PlayerEnums.Country>().Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
            //this.NationalityCombo.DisplayMember = "Text";
            //this.NationalityCombo.ValueMember = "Value";

            this.bindingSource = new BindingSource
            {
                DataSource = this.clubController.Club
            };

            this.BlockAStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockASeatsInput.KeyPress += this.NumericOnly_KeyPress;

            this.AdsInput.KeyPress += this.NumericOnly_KeyPress;
            this.FriendlyInput.KeyPress += this.NumericOnly_KeyPress;
            this.LeagueInput.KeyPress += this.NumericOnly_KeyPress;

            this.BlockAStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockASeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;

            this.AdsInput.TextChanged += this.IntMaxNumber_TextChanged;

            this.ClubNameInput.DataBindings.Add("Text", this.bindingSource, "ClubName");
            this.StadiumNameInput.DataBindings.Add("Text", this.bindingSource, "StadiumName");
            this.BlockARoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockARoof");
            this.BlockAStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockAStandings");
            this.BlockASeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockASeats");
        }

        internal void RefreshValues()
        {
            if (this.IsGameRunning())
            {
                this.clubController = new ClubController(this.Memory, this.processController.IsGog);
            }
        }
        private void ReloadBtn_Click(object sender, System.EventArgs e)
        {
            this.RefreshValues();
        }
    }
}
