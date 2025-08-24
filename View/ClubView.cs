using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Memory;
using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    public partial class ClubView : EntityView
    {
        private Club club;
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
            this.ClubSelect.DataSource = this.clubController.EntityList;
            this.ClubSelect.DisplayMember = "ClubName";

            this.DisplayUnit.DataSource = Enum.GetValues(typeof(ClubEnums.DisplayUnit)).Cast<ClubEnums.DisplayUnit>().Select(c => new { Value = c, Text = ClubEnums.GetDescription(c) }).ToList();
            this.DisplayUnit.DisplayMember = "Text";
            this.DisplayUnit.ValueMember = "Value";

            this.FieldCondition.DataSource = Enum.GetValues(typeof(ClubEnums.FieldCondition)).Cast<ClubEnums.FieldCondition>().Select(c => new { Value = c, Text = ClubEnums.GetDescription(c) }).ToList();
            this.FieldCondition.DisplayMember = "Text";
            this.FieldCondition.ValueMember = "Value";




            this.bindingSource = new BindingSource
            {
                DataSource = this.club
            };

            this.ConstructionWeeksBlockA.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockB.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockC.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockD.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockE.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockF.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockG.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockH.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockI.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockJ.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockK.KeyPress += this.NumericOnly_KeyPress;
            this.ConstructionWeeksBlockL.KeyPress += this.NumericOnly_KeyPress;

            this.BlockAStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockASeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockBStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockBSeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockCStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockCSeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockDStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockDSeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockEStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockESeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockFStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockFSeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockGStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockGSeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockHStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockHSeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockIStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockISeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockJStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockJSeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockKStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockKSeatsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockLStandingsInput.KeyPress += this.NumericOnly_KeyPress;
            this.BlockLSeatsInput.KeyPress += this.NumericOnly_KeyPress;

            this.AdsInput.KeyPress += this.NumericOnly_KeyPress;
            this.FriendlyInput.KeyPress += this.NumericOnly_KeyPress;
            this.LeagueInput.KeyPress += this.NumericOnly_KeyPress;

            this.ConstructionWeeksBlockA.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockB.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockC.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockD.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockE.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockF.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockG.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockH.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockI.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockJ.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockK.TextChanged += this.ByteMax255_TextChanged;
            this.ConstructionWeeksBlockL.TextChanged += this.ByteMax255_TextChanged;

            this.BlockAStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockASeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockBStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockBSeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockCStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockCSeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockDStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockDSeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockEStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockESeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockFStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockFSeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockGStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockGSeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockHStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockHSeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockIStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockISeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockJStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockJSeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockKStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockKSeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockLStandingsInput.TextChanged += this.UshortMaxNumber_TextChanged;
            this.BlockLSeatsInput.TextChanged += this.UshortMaxNumber_TextChanged;

            this.AdsInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.LeagueInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.FriendlyInput.TextChanged += this.IntMaxNumber_TextChanged;

            this.ClubNameInput.DataBindings.Add("Text", this.bindingSource, "ClubName");
            this.StadiumNameInput.DataBindings.Add("Text", this.bindingSource, "StadiumName");
            this.DisplayUnit.DataBindings.Add("SelectedValue", this.bindingSource, "DisplayUnit");
            this.FieldCondition.DataBindings.Add("SelectedValue", this.bindingSource, "FieldCondition");
            this.HeatingCheckBox.DataBindings.Add("Checked", this.bindingSource, "HasGrassHeating");
            this.FloodlightCheckbox.DataBindings.Add("Checked", this.bindingSource, "HasFloodLight");


            this.LeagueInput.DataBindings.Add("Text", this.bindingSource, "EarningsLeagueGames");
            this.FriendlyInput.DataBindings.Add("Text", this.bindingSource, "EarningsFriendlyGames");
            this.AdsInput.DataBindings.Add("Text", this.bindingSource, "EarningsAds");

            this.ConstructionWeeksBlockA.DataBindings.Add("Text", this.bindingSource, "BlockAWeeks");
            this.ConstructionWeeksBlockB.DataBindings.Add("Text", this.bindingSource, "BlockBWeeks");
            this.ConstructionWeeksBlockC.DataBindings.Add("Text", this.bindingSource, "BlockCWeeks");
            this.ConstructionWeeksBlockD.DataBindings.Add("Text", this.bindingSource, "BlockDWeeks");
            this.ConstructionWeeksBlockE.DataBindings.Add("Text", this.bindingSource, "BlockEWeeks");
            this.ConstructionWeeksBlockF.DataBindings.Add("Text", this.bindingSource, "BlockFWeeks");
            this.ConstructionWeeksBlockG.DataBindings.Add("Text", this.bindingSource, "BlockGWeeks");
            this.ConstructionWeeksBlockH.DataBindings.Add("Text", this.bindingSource, "BlockHWeeks");
            this.ConstructionWeeksBlockI.DataBindings.Add("Text", this.bindingSource, "BlockIWeeks");
            this.ConstructionWeeksBlockJ.DataBindings.Add("Text", this.bindingSource, "BlockJWeeks");
            this.ConstructionWeeksBlockK.DataBindings.Add("Text", this.bindingSource, "BlockKWeeks");
            this.ConstructionWeeksBlockL.DataBindings.Add("Text", this.bindingSource, "BlockLWeeks");

            this.BlockARoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockARoof");
            this.BlockBRoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockBRoof");
            this.BlockCRoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockCRoof");
            this.BlockDRoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockDRoof");
            this.BlockERoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockERoof");
            this.BlockFRoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockFRoof");
            this.BlockGRoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockGRoof");
            this.BlockHRoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockHRoof");
            this.BlockIRoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockIRoof");
            this.BlockJRoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockJRoof");
            this.BlockKRoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockKRoof");
            this.BlockLRoof.DataBindings.Add("Checked", this.bindingSource, "IsBlockLRoof");

            this.BlockAStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockAStandings");
            this.BlockASeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockASeats");
            this.BlockBStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockBStandings");
            this.BlockBSeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockBSeats");
            this.BlockCStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockCStandings");
            this.BlockCSeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockCSeats");
            this.BlockDStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockDStandings");
            this.BlockDSeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockDSeats");
            this.BlockEStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockEStandings");
            this.BlockESeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockESeats");
            this.BlockFStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockFStandings");
            this.BlockFSeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockFSeats");
            this.BlockGStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockGStandings");
            this.BlockGSeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockGSeats");
            this.BlockHStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockHStandings");
            this.BlockHSeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockHSeats");
            this.BlockIStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockIStandings");
            this.BlockISeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockISeats");
            this.BlockJStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockJStandings");
            this.BlockJSeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockJSeats");
            this.BlockKStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockKStandings");
            this.BlockKSeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockKSeats");
            this.BlockLStandingsInput.DataBindings.Add("Text", this.bindingSource, "BlockLStandings");
            this.BlockLSeatsInput.DataBindings.Add("Text", this.bindingSource, "BlockLSeats");
        }

        internal void RefreshValues(Club club = null)
        {
            if (this.IsGameRunning())
            {
                this.ClearAllFields(this);


                this.clubController = new ClubController(this.Memory, this.processController.IsGog);
                this.club = club ?? this.clubController.Club;
                Console.WriteLine($"Verdienste: {this.club.EarningsLeagueGames}");
                if (this.bindingSource != null)
                {
                    this.bindingSource.DataSource = this.club;
                    this.bindingSource.ResetBindings(false);
                }
            }
        }
        private void ReloadBtn_Click(object sender, System.EventArgs e)
        {
            this.RefreshValues();
        }

        private void SaveBtn_Click(object sender, System.EventArgs e)
        {
            if (this.IsGameRunning())
            {
                this.clubController.Save();
                this.RefreshValues(this.club);
            }
        }

        private void ClubSelect_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (this.IsGameRunning())
            {
                Club club = this.ClubSelect.SelectedItem as Club;
                this.RefreshValues(club);
            }
        }
    }
}
