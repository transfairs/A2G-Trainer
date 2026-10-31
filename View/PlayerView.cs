using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.ListView;
using AnstossTrainer.Shared;

namespace A2G_Trainer_XP.View
{
    /// <summary>Roster tab: lists a club's players and edits the selected player's fields.</summary>
    public partial class PlayerView : EntityView
    {
        private Timer freezeTimer;
        // Every club in the savegame, for the "Stammverein" choice (see ShowHomeClub).
        private List<Club> allClubs;
        private bool homeClubHandlersAttached;
        private bool populatingHomeClub;
        private bool transferHandlersAttached;
        // Country the club list of the "Wechsel" tab was last filled for.
        private PlayerEnums.Country transferClubCountry;
        private bool populatingTransferCountries;

        /// <summary>Creates the roster tab view bound to the given memory accessor and process controller.</summary>
        public PlayerView(ProcessMemory memory, ProcessController controller) : base(memory, controller)
        {
            InitializeComponent();
            InitPlayerListView();
            InitializeFreezeTimer();
        }

        private void InitPlayerListView()
        {
            // Column widths must leave room for the vertical scrollbar (~17px) once a squad has
            // more players than fit in view - otherwise they no longer fit the shrunk client area
            // and a spurious horizontal scrollbar appears too, which then eats into the height and
            // makes the vertical scrolling worse. Keep the sum comfortably under
            // PlayerListView.Width (see Designer) minus border and scrollbar width.
            this.PlayerListView.Columns.Add("Pos", 30);
            this.PlayerListView.Columns.Add("Name", 105);
            this.PlayerListView.Columns.Add("Stärke", 45, HorizontalAlignment.Center);
            this.PlayerListView.SelectedIndexChanged += this.PlayerListView_SelectedIndexChanged;
        }

        private void DisableEdits(bool enable)
        {
            // Used to be disabled for TRAINEE (Jugendspieler) entirely, since every field for them
            // used to route through a transient, shared display cache the game silently discards
            // writes to. Every field on the Overview/Position/Skills/Character/Constitution/Contract
            // tabs now also writes through the separate, persistent, PlayerId-keyed record (see
            // PlayerController.Save), whose complete layout is known from the game's own code - so
            // all of those are editable for TRAINEE too.
            this.SaveBtn.Enabled = enable;
            foreach (Control tabPage in this.MainTabControl.Controls)
            {
                if (tabPage is TabPage)
                {
                    foreach (Control control in tabPage.Controls)
                    {
                        if (!(control is VScrollBar || control is HScrollBar))
                            control.Enabled = enable;
                    }
                    // OtherTab (statistics) isn't part of what the persistent write path covers, so
                    // it stays disabled for TRAINEE.
                    if (tabPage == this.OtherTab)
                    {
                        tabPage.Enabled = this.clubController.Type != PlayerEnums.AddressType.TRAINEE && enable;
                    }
                }
            }
        }

        #region InitMainTabControl
        internal void InitMainTabControl()
        {

            this.NationalityCombo.DataSource = Enum.GetValues(typeof(PlayerEnums.Country)).Cast<PlayerEnums.Country>().Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
            this.NationalityCombo.DisplayMember = "Text";
            this.NationalityCombo.ValueMember = "Value";

            if (this.PlayerListView != null && this.PlayerListView.Items != null && this.PlayerListView.Items.Count > 0)
            {
                this.bindingSource = new BindingSource
                {
                    DataSource = this.PlayerListView.Items[0].Tag
                };

                this.ShowClubLabel();
                this.DisableEdits(this.IsClub());

                this.LevelInput.KeyPress += this.NumericOnly_KeyPress;
                this.AgeInput.KeyPress += this.NumericOnly_KeyPress;
                this.ConditionInput.KeyPress += this.NumericOnly_KeyPress;
                this.FreshnessInput.KeyPress += this.NumericOnly_KeyPress;
                this.FormInput.KeyPress += this.NumericOnly_KeyPress;
                this.RedCardsInput.KeyPress += this.NumericOnly_KeyPress;
                this.YellowCardsInput.KeyPress += this.NumericOnly_KeyPress;
                this.TeamLevelInput.KeyPress += this.NumericOnly_KeyPress;
                this.TeamFormInput.KeyPress += this.NumericOnly_KeyPress;
                this.TeamAgeInput.KeyPress += this.NumericOnly_KeyPress;
                this.TeamConditionInput.KeyPress += this.NumericOnly_KeyPress;
                this.TeamFreshnessInput.KeyPress += this.NumericOnly_KeyPress;
                this.SalaryInput.KeyPress += this.NumericOnly_KeyPress;
                this.ShowUpBonusInput.KeyPress += this.NumericOnly_KeyPress;
                this.GoalBonusInput.KeyPress += this.NumericOnly_KeyPress;
                this.FixedTransferFeeInput.KeyPress += this.NumericOnly_KeyPress;
                this.BuyPriceInput.KeyPress += this.NumericOnly_KeyPress;
                this.ContractDurationInput.KeyPress += this.NumericOnly_KeyPress;
                this.YearsInClubInput.KeyPress += this.NumericOnly_KeyPress;
                this.Retires.KeyPress += this.NumericOnly_KeyPress;
                foreach (TextBox statisticInput in this.StatisticInputs)
                    statisticInput.KeyPress += this.NumericOnly_KeyPress;

                this.LevelInput.TextChanged += this.ByteMax255_TextChanged;
                this.AgeInput.TextChanged += this.ByteMax255_TextChanged;
                this.ConditionInput.TextChanged += this.ByteMax255_TextChanged;
                this.FreshnessInput.TextChanged += this.ByteMax255_TextChanged;
                this.FormInput.TextChanged += this.ByteMax255_TextChanged;
                this.RedCardsInput.TextChanged += this.ByteMax255_TextChanged;
                this.YellowCardsInput.TextChanged += this.ByteMax255_TextChanged;
                this.TeamLevelInput.TextChanged += this.ByteMax255_TextChanged;
                this.TeamFormInput.TextChanged += this.ByteMax255_TextChanged;
                this.TeamAgeInput.TextChanged += this.ByteMax255_TextChanged;
                this.TeamConditionInput.TextChanged += this.ByteMax255_TextChanged;
                this.TeamFreshnessInput.TextChanged += this.ByteMax255_TextChanged;
                this.SalaryInput.TextChanged += this.UshortMaxNumber_TextChanged;
                this.ShowUpBonusInput.TextChanged += this.UshortMaxNumber_TextChanged;
                this.GoalBonusInput.TextChanged += this.UshortMaxNumber_TextChanged;
                this.FixedTransferFeeInput.TextChanged += this.UshortMaxNumber_TextChanged;
                this.BuyPriceInput.TextChanged += this.IntMaxNumber_TextChanged;
                this.ContractDurationInput.TextChanged += this.ByteMax255_TextChanged;
                this.YearsInClubInput.TextChanged += this.ByteMax255_TextChanged;
                this.Retires.TextChanged += this.ByteMax255_TextChanged;
                foreach (TextBox statisticInput in this.StatisticInputs)
                    statisticInput.TextChanged += this.ByteMax255_TextChanged;

                this.LastNameInput.DataBindings.Add("Text", this.bindingSource, "Lastname");
                this.FirstNameInput.DataBindings.Add("Text", this.bindingSource, "Firstname");
                this.LevelInput.DataBindings.Add("Text", this.bindingSource, "Level");
                this.AgeInput.DataBindings.Add("Text", this.bindingSource, "Age");
                this.NationalityCombo.DataBindings.Add("SelectedValue", this.bindingSource, "Nationality");
                this.ConditionInput.DataBindings.Add("Text", this.bindingSource, "Condition");
                this.FreshnessInput.DataBindings.Add("Text", this.bindingSource, "Freshness");
                this.FormInput.DataBindings.Add("Text", this.bindingSource, "Form");

                this.FairSkin.DataBindings.Add("Checked", this.bindingSource, "HasFairSkin");
                this.DarkSkin.DataBindings.Add("Checked", this.bindingSource, "HasDarkSkin");
                this.BlackSkin.DataBindings.Add("Checked", this.bindingSource, "HasBlackSkin");

                this.LightBlondHair.DataBindings.Add("Checked", this.bindingSource, "HasLightBlondHair");
                this.BlondHair.DataBindings.Add("Checked", this.bindingSource, "HasBlondHair");
                this.BrownHair.DataBindings.Add("Checked", this.bindingSource, "HasBrownHair");
                this.RedHead.DataBindings.Add("Checked", this.bindingSource, "IsGinger");
                this.BlackHair.DataBindings.Add("Checked", this.bindingSource, "HasBlackHair");
                this.NoHair.DataBindings.Add("Checked", this.bindingSource, "HasNoHair");

                this.MainGoalKeeper.DataBindings.Add("Checked", this.bindingSource, "IsTO");
                this.MainSweeper.DataBindings.Add("Checked", this.bindingSource, "IsL");
                this.MainMan2Man.DataBindings.Add("Checked", this.bindingSource, "IsMD");
                this.MainLeftBack.DataBindings.Add("Checked", this.bindingSource, "IsLV");
                this.MainRightBack.DataBindings.Add("Checked", this.bindingSource, "IsRV");
                this.MainLeftWing.DataBindings.Add("Checked", this.bindingSource, "IsLM");
                this.MainRightWing.DataBindings.Add("Checked", this.bindingSource, "IsRM");
                this.MainDefensive.DataBindings.Add("Checked", this.bindingSource, "IsDM");
                this.MainCentralMidfield.DataBindings.Add("Checked", this.bindingSource, "IsOM");
                this.MainStriker.DataBindings.Add("Checked", this.bindingSource, "IsS");

                this.GoalKeeper.DataBindings.Add("Checked", this.bindingSource, "IsSecondaryTO");
                this.Sweeper.DataBindings.Add("Checked", this.bindingSource, "IsSecondaryL");
                this.Man2Man.DataBindings.Add("Checked", this.bindingSource, "IsSecondaryMD");
                this.LeftBack.DataBindings.Add("Checked", this.bindingSource, "IsSecondaryLV");
                this.RightBack.DataBindings.Add("Checked", this.bindingSource, "IsSecondaryRV");
                this.LeftWing.DataBindings.Add("Checked", this.bindingSource, "IsSecondaryLM");
                this.RightWing.DataBindings.Add("Checked", this.bindingSource, "IsSecondaryRM");
                this.Defensive.DataBindings.Add("Checked", this.bindingSource, "IsSecondaryDM");
                this.CentralMidfield.DataBindings.Add("Checked", this.bindingSource, "IsSecondaryOM");
                this.Striker.DataBindings.Add("Checked", this.bindingSource, "IsSecondaryS");

                this.KopfballLabel.DataBindings.Add("Checked", this.bindingSource, "HasKopfBall");
                this.ZweikampfLabel.DataBindings.Add("Checked", this.bindingSource, "HasZweikampf");
                this.SchnelligkeitLabel.DataBindings.Add("Checked", this.bindingSource, "HasSchnelligkeit");
                this.SchusskraftLabel.DataBindings.Add("Checked", this.bindingSource, "HasSchusskraft");
                this.FreistoesseLabel.DataBindings.Add("Checked", this.bindingSource, "HasFreistoesse");
                this.FlankenLabel.DataBindings.Add("Checked", this.bindingSource, "HasFlanken");
                this.TorinstinktLabel.DataBindings.Add("Checked", this.bindingSource, "HasTorinstinkt");
                this.LaufstaerkeLabel.DataBindings.Add("Checked", this.bindingSource, "HasLaufstaerke");
                this.TechnikLabel.DataBindings.Add("Checked", this.bindingSource, "HasTechnik");
                this.SpielmacherLabel.DataBindings.Add("Checked", this.bindingSource, "HasSpielmacher");
                this.BeidfuessigkeitLabel.DataBindings.Add("Checked", this.bindingSource, "HasBeidfuessigkeit");
                this.Elfmetertoeter.DataBindings.Add("Checked", this.bindingSource, "HasElfmetertoeter");
                this.StarkeReflexe.DataBindings.Add("Checked", this.bindingSource, "HasStarkeReflexe");
                this.Herauslaufen.DataBindings.Add("Checked", this.bindingSource, "HasHerauslaufen");
                this.Fangsicherheit.DataBindings.Add("Checked", this.bindingSource, "HasFangsicherheit");
                this.Fausten.DataBindings.Add("Checked", this.bindingSource, "HasFausten");
                this.Ballsicherheit.DataBindings.Add("Checked", this.bindingSource, "HasBallsicherheit");
                this.NegativeKopfballLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegKopfBall");
                this.NegativeZweikampfLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegZweikampf");
                this.NegativeSchnelligkeitLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegSchnelligkeit");
                this.NegativeSchusskraftLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegSchusskraft");
                this.NegativeFreistoesseLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegFreistoesse");
                this.NegativeFlankenLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegFlanken");
                this.NegativeTorinstinktLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegTorinstinkt");
                this.NegativeLaufstaerkeLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegLaufstaerke");
                this.NegativeTechnikLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegTechnik");
                this.NegativeSpielmacherLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegSpielmacher");
                this.NegativeBeidfuessigkeitLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegBeidfuessigkeit");
                this.NegElfmetertoeterLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegElfmetertoeter");
                this.NegStarkeReflexeLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegStarkeReflexe");
                this.NegHerauslaufenLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegHerauslaufen");
                this.NegFangsicherheitLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegFangsicherheit");
                this.NegFaustenLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegFausten");
                this.NegBallsicherheitLabel.DataBindings.Add("Checked", this.bindingSource, "HasNegBallsicherheit");
                this.KopfballLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.ZweikampfLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.SchnelligkeitLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.SchusskraftLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.FreistoesseLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.FlankenLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.TorinstinktLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.LaufstaerkeLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.TechnikLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.SpielmacherLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.BeidfuessigkeitLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.Elfmetertoeter.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.StarkeReflexe.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.Herauslaufen.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.Fangsicherheit.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.Fausten.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.Ballsicherheit.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.NegativeKopfballLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegativeZweikampfLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegativeSchnelligkeitLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegativeSchusskraftLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegativeFreistoesseLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegativeFlankenLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegativeTorinstinktLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegativeLaufstaerkeLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegativeTechnikLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegativeSpielmacherLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegativeBeidfuessigkeitLabel.DataBindings.Add("Enabled", this.bindingSource, "IsNotTO");
                this.NegElfmetertoeterLabel.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.NegStarkeReflexeLabel.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.NegHerauslaufenLabel.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.NegFangsicherheitLabel.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.NegFaustenLabel.DataBindings.Add("Enabled", this.bindingSource, "IsTO");
                this.NegBallsicherheitLabel.DataBindings.Add("Enabled", this.bindingSource, "IsTO");

                this.NormalCharLabel.DataBindings.Add("Checked", this.bindingSource, "IsNormalChar");
                this.HitzkopfLabel.DataBindings.Add("Checked", this.bindingSource, "IsHitzkopf");
                this.FrohnaturLabel.DataBindings.Add("Checked", this.bindingSource, "IsFrohnatur");
                this.MannOhneNervenLabel.DataBindings.Add("Checked", this.bindingSource, "IsMannOhneNerven");
                this.NervenBuendelLabel.DataBindings.Add("Checked", this.bindingSource, "IsNervenbuendel");
                this.PhlegmaLabel.DataBindings.Add("Checked", this.bindingSource, "IsPhlegma");
                this.GeldgeierLabel.DataBindings.Add("Checked", this.bindingSource, "IsGeldgeier");
                this.MusterprofiLabel.DataBindings.Add("Checked", this.bindingSource, "IsMusterprofi");
                this.SkandalnudelLabel.DataBindings.Add("Checked", this.bindingSource, "IsSkandalnudel");
                this.NormalHealthLabel.DataBindings.Add("Checked", this.bindingSource, "HasNormalHealth");
                this.RobustLabel.DataBindings.Add("Checked", this.bindingSource, "IsRobust");
                this.AnfaelligkeitLabel.DataBindings.Add("Checked", this.bindingSource, "IsAnfaellig");
                this.KneeProblemsLabel.DataBindings.Add("Checked", this.bindingSource, "HasKnieprobleme");
                this.NoPropLabel.DataBindings.Add("Checked", this.bindingSource, "IsNoone");
                this.FuehrungspersonLabel.DataBindings.Add("Checked", this.bindingSource, "IsFuehrungsperson");
                this.KaempfernaturLabel.DataBindings.Add("Checked", this.bindingSource, "IsKaempfernatur");
                this.TrainingsweltmeisterLabel.DataBindings.Add("Checked", this.bindingSource, "IsTrainingsweltmeister");
                this.TrainingsfaulLabel.DataBindings.Add("Checked", this.bindingSource, "IsTrainingsfaul");
                this.TreterLabel.DataBindings.Add("Checked", this.bindingSource, "IsTreter");
                this.FairPlayerLabel.DataBindings.Add("Checked", this.bindingSource, "IsFairPlayer");
                this.SchwalbenkoenigLabel.DataBindings.Add("Checked", this.bindingSource, "IsSchwalbenkoenig");
                this.AllrounderLabel.DataBindings.Add("Checked", this.bindingSource, "IsAllrounder");
                this.FlexiblePlayerLabel.DataBindings.Add("Checked", this.bindingSource, "IsFlexiblePlayer");
                this.HeimspielerLabel.DataBindings.Add("Checked", this.bindingSource, "IsHeimspieler");
                this.AuswaertsSpielerLabel.DataBindings.Add("Checked", this.bindingSource, "IsAuswaertsspieler");
                this.TalentLabel.DataBindings.Add("Checked", this.bindingSource, "IsTalent");
                this.TalentLabel.DataBindings.Add("Text", this.bindingSource, "TalentLabel");

                this.TollerVertrag.DataBindings.Add("Checked", this.bindingSource, "IsTollerVertrag");
                this.TolleStimmung.DataBindings.Add("Checked", this.bindingSource, "IsTolleStimmung");
                this.TollePraemien.DataBindings.Add("Checked", this.bindingSource, "IsTollePraemien");
                this.TollerVertragDoppelt.DataBindings.Add("Checked", this.bindingSource, "IsTollerVertragDoppelt");
                this.TollerTrainer.DataBindings.Add("Checked", this.bindingSource, "IsTollerTrainer");

                this.WillEndlichSpielen.DataBindings.Add("Checked", this.bindingSource, "IsWillEndlichSpielen");
                this.WillMehrGeld.DataBindings.Add("Checked", this.bindingSource, "IsWillMehrGeld");
                this.MiesePraemien.DataBindings.Add("Checked", this.bindingSource, "IsMiesePraemien");
                this.GehtNichtNachLeistung.DataBindings.Add("Checked", this.bindingSource, "IsGehtNichtNachLeistung");
                this.FalschePosition.DataBindings.Add("Checked", this.bindingSource, "IsFalschePosition");
                this.ScheissVertrag.DataBindings.Add("Checked", this.bindingSource, "IsScheissVertrag");
                this.MieseMannschaft.DataBindings.Add("Checked", this.bindingSource, "IsMieseMannschaft");
                this.SchlechteStimmung.DataBindings.Add("Checked", this.bindingSource, "IsSchlechteStimmung");
                this.VerletztUndNieBesucht.DataBindings.Add("Checked", this.bindingSource, "IsVerletztUndNieBesucht");
                this.UngerechteBehandlung.DataBindings.Add("Checked", this.bindingSource, "IsUngerechteBehandlung");
                this.BloedeFans.DataBindings.Add("Checked", this.bindingSource, "IsBloedeFans");
                this.BloedeMitspieler.DataBindings.Add("Checked", this.bindingSource, "IsBloedeMitspieler");
                this.ScheissTrainer.DataBindings.Add("Checked", this.bindingSource, "IsScheissTrainer");
                this.VerhandlungGeplatzt.DataBindings.Add("Checked", this.bindingSource, "IsVerhandlungGeplatzt");
                this.ScheissManager.DataBindings.Add("Checked", this.bindingSource, "IsScheissManager");
                this.ScheissNachwuchsrunde.DataBindings.Add("Checked", this.bindingSource, "IsScheissNachwuchsrunde");

                this.InjuredDaysInput.DataBindings.Add("Text", this.bindingSource, "InjuredDays");
                this.Vulnerable.DataBindings.Add("Checked", this.bindingSource, "Vulnerable");
                this.Doped.DataBindings.Add("Checked", this.bindingSource, "Doped");
                this.RedCardsInput.DataBindings.Add("Text", this.bindingSource, "RedCardBannedMatches");
                this.YellowCardsInput.DataBindings.Add("Text", this.bindingSource, "YellowCardsSeason");

                this.SalaryInput.DataBindings.Add("Text", this.bindingSource, "Salary");
                this.ShowUpBonusInput.DataBindings.Add("Text", this.bindingSource, "ShowUpBonus");
                this.GoalBonusInput.DataBindings.Add("Text", this.bindingSource, "GoalsBonus");
                this.FixedTransferFeeInput.DataBindings.Add("Text", this.bindingSource, "TransferFee");
                this.BuyPriceInput.DataBindings.Add("Text", this.bindingSource, "BuyPrice");
                this.ContractDurationInput.DataBindings.Add("Text", this.bindingSource, "ContractDuration");
                this.Leased.DataBindings.Add("Checked", this.bindingSource, "IsLeased");
                this.LeasedWithOption.DataBindings.Add("Checked", this.bindingSource, "HasBuyOption");
                this.BuyOptionOnHold.DataBindings.Add("Checked", this.bindingSource, "IsBuyOptionOnHold");
                this.JoinedThisSeason.DataBindings.Add("Checked", this.bindingSource, "IsJoinedThisSeason");
                this.OptionPlayer.DataBindings.Add("Checked", this.bindingSource, "HasOptionPlayer");
                this.OptionClub.DataBindings.Add("Checked", this.bindingSource, "HasOptionClub");
                this.SeatedGuarantee.DataBindings.Add("Checked", this.bindingSource, "IsSeatedGuarantee");
                this.YearsInClubInput.DataBindings.Add("Text", this.bindingSource, "YearsInClub");
                this.Retires.DataBindings.Add("Checked", this.bindingSource, "IsRetires");

                this.RedCardsSeasonInput.DataBindings.Add("Text", this.bindingSource, nameof(Player.RedCardsSeason));
                this.YellowRedCardsInput.DataBindings.Add("Text", this.bindingSource, nameof(Player.YellowRedCardsSeason));
                this.GoalsInput.DataBindings.Add("Text", this.bindingSource, nameof(Player.Goals));
                this.OwnGoalsInput.DataBindings.Add("Text", this.bindingSource, nameof(Player.OwnGoals));
                this.AssistsInput.DataBindings.Add("Text", this.bindingSource, nameof(Player.Assists));
                this.JokerGoalsInput.DataBindings.Add("Text", this.bindingSource, nameof(Player.JokerGoals));
                this.AppearancesInput.DataBindings.Add("Text", this.bindingSource, nameof(Player.Appearances));
                this.SubbedInInput.DataBindings.Add("Text", this.bindingSource, nameof(Player.SubbedIn));
                this.SubbedOutInput.DataBindings.Add("Text", this.bindingSource, nameof(Player.SubbedOut));
                this.UpgradeGoalsInput.DataBindings.Add("Text", this.bindingSource, nameof(Player.UpgradeGoals));

                this.AttachHomeClubHandlers();
                this.ShowHomeClub(this.bindingSource.Current as Player);
                this.AttachTransferHandlers();
                this.ShowTransfer();
            }
        }

        #region Wechsel
        // "Wechsel" tab (TransferInjector): the target club is chosen from the club scan like the
        // "Stammverein"; the move itself happens at the next daily processing.
        private void AttachTransferHandlers()
        {
            if (this.transferHandlersAttached)
                return;
            this.transferHandlersAttached = true;

            this.TransferKindInput.DisplayMember = "Text";
            this.TransferKindInput.ValueMember = "Value";
            this.TransferKindInput.DataSource = new[]
            {
                new { Value = TransferInjector.Kind.Transfer, Text = "Wechsel" },
                new { Value = TransferInjector.Kind.Loan, Text = "Leihe" },
                new { Value = TransferInjector.Kind.LoanWithBuyOption, Text = "Leihe mit Kaufoption" },
            }.ToList();
            this.TransferKindInput.SelectedIndexChanged += (s, e) => this.UpdateTransferKind();
            this.TransferCountryInput.SelectedIndexChanged += (s, e) =>
            {
                if (!this.populatingTransferCountries)
                    this.PopulateTransferClubCombo();
            };
            this.TransferFeeInput.KeyPress += this.NumericOnly_KeyPress;
            this.TransferFeeInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.TransferBuyPriceInput.KeyPress += this.NumericOnly_KeyPress;
            this.TransferBuyPriceInput.TextChanged += this.IntMaxNumber_TextChanged;
            this.TransferRequestBtn.Click += (s, e) => this.RequestTransfer();
            this.UpdateTransferKind();
        }

        private TransferInjector.Kind SelectedTransferKind =>
            this.TransferKindInput.SelectedValue is TransferInjector.Kind kind ? kind : TransferInjector.Kind.Transfer;

        // The buy price only exists for a loan with buy option, "sofort" only for a transfer (loans have no deadline).
        private void UpdateTransferKind()
        {
            TransferInjector.Kind kind = this.SelectedTransferKind;
            this.TransferBuyPriceInput.Enabled = kind == TransferInjector.Kind.LoanWithBuyOption;
            this.TransferImmediate.Enabled = kind == TransferInjector.Kind.Transfer;
        }

        private void ShowTransfer()
        {
            PlayerEnums.Country? previous = this.TransferCountryInput.SelectedValue as PlayerEnums.Country?;
            List<PlayerEnums.Country> countries = (this.allClubs ?? new List<Club>())
                .Select(c => c.Country)
                .Where(c => c != PlayerEnums.Country.Sonstige && Enum.IsDefined(typeof(PlayerEnums.Country), c))
                .Distinct()
                .OrderBy(HomeCountryText)
                .ToList();

            this.TransferCountryInput.DisplayMember = "Text";
            this.TransferCountryInput.ValueMember = "Value";
            // Rebuilding the list selects other countries on the way - the club list follows only the final one.
            this.populatingTransferCountries = true;
            try
            {
                this.TransferCountryInput.DataSource = countries.Select(c => new { Value = c, Text = HomeCountryText(c) }).ToList();
                if (previous.HasValue && countries.Contains(previous.Value))
                    this.TransferCountryInput.SelectedValue = previous.Value;
                else if (countries.Count > 0)
                    this.TransferCountryInput.SelectedIndex = 0;
            }
            finally
            {
                this.populatingTransferCountries = false;
            }
            this.PopulateTransferClubCombo();
            this.UpdateTransferBox();
        }

        // Clubs of the chosen country without the player's own; keeps the chosen club while the country stays.
        private void PopulateTransferClubCombo()
        {
            Player player = this.bindingSource?.Current as Player;
            PlayerEnums.Country country = this.TransferCountryInput.SelectedValue is PlayerEnums.Country selected ? selected : PlayerEnums.Country.Sonstige;
            ushort? previous = country == this.transferClubCountry ? this.TransferClubInput.SelectedValue as ushort? : null;

            var clubs = this.HomeClubsOf(country)
                .Where(c => player == null || !(c.Country == player.ClubCountry && c.Id == player.ClubId))
                .Select(c => new { Value = (ushort)c.Id, Text = c.ClubName })
                .ToList();
            this.TransferClubInput.DisplayMember = "Text";
            this.TransferClubInput.ValueMember = "Value";
            this.TransferClubInput.DataSource = clubs;
            if (previous.HasValue && clubs.Any(c => c.Value == previous.Value))
                this.TransferClubInput.SelectedValue = previous.Value;
            else if (clubs.Count > 0)
                this.TransferClubInput.SelectedIndex = 0;
            this.transferClubCountry = country;
        }

        // Original build only, and not for youth players (they aren't in a squad); DisableEdits decides the rest.
        private void UpdateTransferBox()
        {
            bool possible = this.playerController != null
                && this.playerController.Type != PlayerEnums.AddressType.TRAINEE
                && new TransferInjector(this.Memory).IsSupported;
            if (!possible)
                this.TransferBox.Enabled = false;
        }

        private void RequestTransfer()
        {
            if (!(this.bindingSource?.Current is Player player) || !(this.TransferCountryInput.SelectedValue is PlayerEnums.Country country) || !(this.TransferClubInput.SelectedValue is ushort clubId))
                return;
            uint.TryParse(this.TransferFeeInput.Text, out uint fee);
            uint.TryParse(this.TransferBuyPriceInput.Text, out uint buyPrice);

            TransferInjector.RequestResult result = new TransferInjector(this.Memory).Request(player, country, (byte)clubId, this.SelectedTransferKind, fee, buyPrice, this.TransferImmediate.Checked);
            this.TransferStatus.Text = TransferMessage(result, player.ToString(), this.TransferClubInput.GetItemText(this.TransferClubInput.SelectedItem));
        }

        // What the last request came to, once the daily processing ran it (see RefreshPlayerListView).
        private void ReportTransferOutcome()
        {
            TransferInjector.Outcome outcome = new TransferInjector(this.Memory).TakeOutcome();
            if (outcome != TransferInjector.Outcome.None)
                this.TransferStatus.Text = TransferOutcomeMessage(outcome);
        }

        internal static string TransferMessage(TransferInjector.RequestResult result, string playerName, string clubName)
        {
            switch (result)
            {
                case TransferInjector.RequestResult.Requested:
                    return $"{playerName} wechselt beim nächsten Tagesabschluss zu {clubName}.";
                case TransferInjector.RequestResult.NoClub:
                    return "Der Spieler hat keinen Verein.";
                case TransferInjector.RequestResult.SameClub:
                    return "Das ist der aktuelle Verein des Spielers.";
                case TransferInjector.RequestResult.NotSupported:
                    return "Für diese Spielversion nicht verfügbar.";
                default:
                    return "Konnte im Spiel nicht vorbereitet werden, Details in der Logdatei.";
            }
        }

        internal static string TransferOutcomeMessage(TransferInjector.Outcome outcome)
        {
            switch (outcome)
            {
                case TransferInjector.Outcome.Done:
                    return "Wechsel erledigt.";
                case TransferInjector.Outcome.NotInClub:
                    return "Wechsel abgelehnt: Der Spieler war nicht mehr beim alten Verein.";
                case TransferInjector.Outcome.SquadFull:
                    return "Wechsel abgelehnt: Der Kader des neuen Vereins ist voll.";
                default:
                    return "";
            }
        }
        #endregion

        // "Stammverein" (Player.HomeCountry/HomeClubId): both combos are filled per player and written
        // in their change handlers instead of being bound - the club list depends on the country, the
        // same reason CoachView's "Nächste Saison" combos aren't plain bindings.
        private void AttachHomeClubHandlers()
        {
            if (this.homeClubHandlersAttached)
                return;
            this.homeClubHandlersAttached = true;

            this.HomeCountryInput.SelectedIndexChanged += (s, e) =>
            {
                if (this.populatingHomeClub || !(this.bindingSource?.Current is Player player) || !(this.HomeCountryInput.SelectedValue is PlayerEnums.Country country))
                    return;
                player.HomeCountry = country;
                Club first = this.HomeClubsOf(country).FirstOrDefault();
                player.HomeClubId = first != null ? first.Id : (ushort)0;
                this.PopulateHomeClubCombo(player);
            };
            this.HomeClubInput.SelectedIndexChanged += (s, e) =>
            {
                if (!this.populatingHomeClub && this.bindingSource?.Current is Player player && this.HomeClubInput.SelectedValue is ushort id)
                    player.HomeClubId = id;
            };
        }

        // Country 0 means "no Stammverein" (not on loan); its entries in the club scan are unused slots.
        private IEnumerable<Club> HomeClubsOf(PlayerEnums.Country country) =>
            country == PlayerEnums.Country.Sonstige || this.allClubs == null
                ? Enumerable.Empty<Club>()
                : this.allClubs.Where(c => c.Country == country).OrderBy(c => c.ClubName);

        private void ShowHomeClub(Player player)
        {
            this.populatingHomeClub = true;
            try
            {
                List<PlayerEnums.Country> countries = (this.allClubs ?? new List<Club>())
                    .Select(c => c.Country)
                    .Where(c => c != PlayerEnums.Country.Sonstige && Enum.IsDefined(typeof(PlayerEnums.Country), c))
                    .Distinct()
                    .OrderBy(HomeCountryText)
                    .ToList();
                // Keep a stored country selectable even if the club scan doesn't cover it.
                if (player != null && player.HomeCountry != PlayerEnums.Country.Sonstige && !countries.Contains(player.HomeCountry))
                    countries.Add(player.HomeCountry);
                countries.Insert(0, PlayerEnums.Country.Sonstige);

                this.HomeCountryInput.DisplayMember = "Text";
                this.HomeCountryInput.ValueMember = "Value";
                this.HomeCountryInput.DataSource = countries.Select(c => new { Value = c, Text = HomeCountryText(c) }).ToList();
                this.HomeCountryInput.SelectedValue = player?.HomeCountry ?? PlayerEnums.Country.Sonstige;
            }
            finally
            {
                this.populatingHomeClub = false;
            }
            this.PopulateHomeClubCombo(player);
        }

        private void PopulateHomeClubCombo(Player player)
        {
            this.populatingHomeClub = true;
            try
            {
                PlayerEnums.Country country = player?.HomeCountry ?? PlayerEnums.Country.Sonstige;
                var clubs = this.HomeClubsOf(country).Select(c => new { Value = (ushort)c.Id, Text = c.ClubName }).ToList();
                // Keep a stored club selectable even if the club scan doesn't know it.
                if (player != null && country != PlayerEnums.Country.Sonstige && !clubs.Any(c => c.Value == player.HomeClubId))
                    clubs.Add(new { Value = player.HomeClubId, Text = $"Verein {player.HomeClubId}" });

                this.HomeClubInput.DisplayMember = "Text";
                this.HomeClubInput.ValueMember = "Value";
                this.HomeClubInput.DataSource = clubs;
                if (clubs.Count > 0)
                    this.HomeClubInput.SelectedValue = player.HomeClubId;
                this.HomeClubInput.Enabled = clubs.Count > 0;
            }
            finally
            {
                this.populatingHomeClub = false;
            }
        }

        internal const string NoHomeClubText = "–";

        // The club scan also turns up country values the enum doesn't name.
        private static string HomeCountryText(PlayerEnums.Country country) =>
            country == PlayerEnums.Country.Sonstige ? NoHomeClubText
            : Enum.IsDefined(typeof(PlayerEnums.Country), country) ? PlayerEnums.GetDescription(country)
            : $"Land {(byte)country}";
        // The "Statistik" tab's inputs, all season counts of one byte each.
        private TextBox[] StatisticInputs => new[]
        {
            this.AppearancesInput, this.SubbedInInput, this.SubbedOutInput, this.RedCardsSeasonInput, this.YellowRedCardsInput,
            this.GoalsInput, this.AssistsInput, this.JokerGoalsInput, this.OwnGoalsInput, this.UpgradeGoalsInput
        };

        private bool IsClub()
        {
            bool output = !String.IsNullOrEmpty(this.clubController.Club.ClubName);
            int i = 0;
            if (output)
            {
                Player previous = new Player
                {
                    ClubId = this.ClubController.Club.Id,
                    ClubCountry = this.ClubController.Club.Country
                };

                foreach (Player player in this.PlayerController.EntityList)
                {
                    i++;
                    if (previous.ClubId != player.ClubId || previous.ClubCountry != player.ClubCountry)
                    {
                        output = false;
                        break;
                    }
                }
            }
            return output;
        }

        private void ShowClubLabel()
        {
            this.CurrentClubLabel.Text = !this.IsClub() ? "!! Keine Vereinsansicht !!" : this.clubController.Club.ClubName;
        }
        #endregion

        internal void RefreshPlayerListView(PlayerEnums.AddressType type)
        {
            if (this.IsGameRunning())
            {
                this.processController.UpdatePlayerOffsets();
                this.allClubs = new ClubController(this.Memory, this.processController.IsGog, PlayerEnums.AddressType.ALL, loadFullList: true).EntityList.ToList();

                this.clubController = new ClubController(this.Memory, this.processController.IsGog, type == PlayerEnums.AddressType.TRAINEE ? PlayerEnums.AddressType.OWN : type,
                    loadFullList: type == PlayerEnums.AddressType.DYNAMIC || type == PlayerEnums.AddressType.TRAINEE);

                if (type == PlayerEnums.AddressType.DYNAMIC || type == PlayerEnums.AddressType.TRAINEE)
                {
                    ushort pc = this.clubController.Club.PlayerCount;
                    byte apc = this.clubController.Club.AmateurPlayerCount;
                    this.playerController = new PlayerController(this.Memory, this.clubController.Club, this.processController.IsGog, type);
                    // .FirstOrDefault(), not .First(): EntityList is legitimately empty when no
                    // savegame is loaded (e.g. clicking a menu item at the main menu) - IsClubMember
                    // below is already null-safe, so a null firstPlayer just naturally resolves to
                    // "not a member"/"" instead of throwing.
                    Player firstPlayer = this.playerController.EntityList.FirstOrDefault();

                    if (type == PlayerEnums.AddressType.DYNAMIC)
                    {
                        // The club table we scan only covers leagues the game actually models (~27
                        // countries) - a dynamic club from a country outside that set (e.g. Slowakei)
                        // will never show up here even though it was read correctly below. Only the
                        // direct read's PlayerCount/AmateurPlayerCount get clobbered by constructing
                        // the PlayerController above, so those are restored - the already-correct
                        // ClubName is kept rather than blanked.
                        Club dynamic = this.clubController.EntityList.FirstOrDefault(c => c.IsClubMember(firstPlayer));
                        // firstPlayer can be null (no savegame loaded / empty EntityList, see above) -
                        // the interpolated string below is evaluated unconditionally regardless of the
                        // Debug log level, so firstPlayer.ClubId/.ClubCountry must be accessed via ?.
                        // rather than assuming firstPlayer is non-null.
                        Logger.Debug($"Dynamic match: firstPlayer ClubId={firstPlayer?.ClubId} ClubCountry={firstPlayer?.ClubCountry} -> {(dynamic != null ? $"found {dynamic.ClubName} ({dynamic.Id}, {dynamic.Country}), {dynamic.PlayerCount} players" : "NOT found in EntityList (keeping direct-read club)")}");
                        if (dynamic == null)
                        {
                            this.clubController.Club.PlayerCount = pc;
                            this.clubController.Club.AmateurPlayerCount = apc;
                        }
                        this.clubController.Club = dynamic ?? this.clubController.Club;
                    }
                    if (type == PlayerEnums.AddressType.TRAINEE)
                    {
                        this.clubController.Club.ClubName = this.clubController.Club.IsClubMember(firstPlayer) ? "Jugendspieler" : "";
                        Logger.Debug($"3. Spieler {firstPlayer} gehört zum Club: {this.clubController.Club.ClubName}, {this.clubController.Club.IsClubMember(firstPlayer)}");

                        this.clubController.Club.PlayerCount = (ushort)(this.clubController.Club.TraineeACount + this.clubController.Club.TraineeBCount + this.clubController.Club.TraineeCCount);
                        this.clubController.Club.AmateurPlayerCount = Byte.MinValue;
                        this.clubController.Type = type;
                    }
                }

                this.playerController = new PlayerController(this.Memory, this.clubController.Club, this.processController.IsGog, type);

                Player player = (this.PlayerListView.SelectedItems.Count > 0) ? (Player)this.PlayerListView.SelectedItems[0].Tag : null;
                this.PlayerListView.Items.Clear();

                if (this.playerController != null)
                {
                    int i = 0;
                    int pId = 0;
                    foreach (var p in this.playerController.EntityList)
                    {
                        ListViewItem listViewItem = new ListViewItem(p.Position.ToString());
                        listViewItem.SubItems.Add(p.ToString());
                        listViewItem.SubItems.Add(p.Level.ToString());
                        listViewItem.Tag = p;
                        this.PlayerListView.Items.Add(listViewItem);

                        if (player != null && p.Id == player.Id)
                        {
                            pId = i;
                        }
                        i++;
                    }

                    if (this.PlayerListView.Items.Count > pId)
                    {
                        this.PlayerListView.Items[pId].Selected = true;
                        this.PlayerListView.Items[pId].Focused = true;
                        this.PlayerListView.EnsureVisible(pId);
                    }

                    this.ShowClubLabel();
                    this.DisableEdits(this.IsClub());
                    this.ShowTransfer();
                    this.ReportTransferOutcome();
                }
            }
        }
        private void PlayerListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            SelectedListViewItemCollection selectedPlayers = PlayerListView.SelectedItems;

            if (selectedPlayers.Count > 0 && this.bindingSource != null)
            {
                ListViewItem item = PlayerListView.SelectedItems[0];

                if (item.Tag is Player player)
                {
                    this.bindingSource.DataSource = player;
                    this.ShowHomeClub(player);
                    this.PopulateTransferClubCombo();
                }
            }
        }
        private void ReloadBtn_Click(object sender, EventArgs e)
        {
            if (this.playerController != null)
            {
                this.RefreshPlayerListView(this.playerController.Type);
            }
        }

        private void SaveBtn_Click(object sender, EventArgs e)
        {
            if (this.IsGameRunning())
            {
                bool hasLevel = !String.IsNullOrEmpty(this.TeamLevelInput.Text);
                bool hasForm = !String.IsNullOrEmpty(this.TeamFormInput.Text);
                bool hasAge = !String.IsNullOrEmpty(this.TeamAgeInput.Text);
                bool hasCondition = !String.IsNullOrEmpty(this.TeamConditionInput.Text);
                bool hasFreshness = !String.IsNullOrEmpty(this.TeamFreshnessInput.Text);
                bool hasContractDuration = !String.IsNullOrEmpty(this.TeamContractDurationInput.Text);

                foreach (Player player in this.playerController.EntityList)
                {
                    if (hasLevel) player.Level = byte.Parse(this.TeamLevelInput.Text);
                    if (hasForm) player.Form = byte.Parse(this.TeamFormInput.Text);
                    if (hasAge) player.Age = byte.Parse(this.TeamAgeInput.Text);
                    if (hasCondition) player.Condition = byte.Parse(this.TeamConditionInput.Text);
                    if (hasFreshness) player.Freshness = byte.Parse(this.TeamFreshnessInput.Text);
                    if (hasContractDuration) player.ContractDuration = byte.Parse(this.TeamContractDurationInput.Text);
                }
                List<string> nameWarnings = this.playerController.SaveEntityList();
                if (nameWarnings.Count > 0)
                {
                    const int maxLines = 4;
                    IEnumerable<string> shown = nameWarnings.Take(maxLines);
                    int hidden = nameWarnings.Count - maxLines;
                    string text = string.Join(Environment.NewLine + Environment.NewLine, shown)
                        + (hidden > 0 ? $"{Environment.NewLine}{Environment.NewLine}... und {hidden} weitere" : "");

                    MessageBox.Show(this, text, "Nicht dauerhaft gespeichert", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }

                HashSet<Control> frozenControls = new HashSet<Control>();
                if (this.TeamConditionFreezeCheck.Checked)
                {
                    frozenControls.Add(this.TeamConditionFreezeCheck);
                    frozenControls.Add(this.TeamConditionInput);
                }
                if (this.TeamFreshnessFreezeCheck.Checked)
                {
                    frozenControls.Add(this.TeamFreshnessFreezeCheck);
                    frozenControls.Add(this.TeamFreshnessInput);
                }
                this.ClearAllFields(this.TeamBus, frozenControls);

                this.RefreshPlayerListView(this.playerController.Type);
            }
        }

        private void InitializeFreezeTimer ()
        {
            this.freezeTimer = new Timer();
            this.freezeTimer.Interval = 1000;
            this.freezeTimer.Tick += new EventHandler(FreezeTimer_Tick);
            this.freezeTimer.Start();
        }

        private void FreezeTimer_Tick (object sender, EventArgs e)
        {
            if (!this.IsGameRunning(silent: true)) return;

            try {
                // Get all active and valid frozen values
                Dictionary<PlayerEnums.AddressKey, byte> frozenValues = this.GetFrozenValues();

                // Only proceed if there is actually something to update
                if (frozenValues.Count > 0) {
                    List<PlayerEnums.AddressKey> keysToUpdate = new List<PlayerEnums.AddressKey>(frozenValues.Keys);

                    foreach (Player player in this.playerController.EntityList) {
                        // Update the model state
                        this.ApplyFrozenValuesToPlayer(player, frozenValues);

                        // Execute dynamic memory write
                        this.playerController.Save(player, keysToUpdate);
                    }
                }
            }
            catch (Exception ex) {
                // Fail safely
                Logger.Warn("Freeze Timer Exception", ex);
            }
        }

        private void ApplyFrozenValuesToPlayer (Player player, Dictionary<PlayerEnums.AddressKey, byte> frozenValues)
        {
            if (frozenValues.ContainsKey(PlayerEnums.AddressKey.CONDITION))
                player.Condition = frozenValues[PlayerEnums.AddressKey.CONDITION];

            if (frozenValues.ContainsKey(PlayerEnums.AddressKey.FRESHNESS))
                player.Freshness = frozenValues[PlayerEnums.AddressKey.FRESHNESS];

            // Future properties can just be added here
        }

        private Dictionary<PlayerEnums.AddressKey, byte> GetFrozenValues ()
        {
            Dictionary<PlayerEnums.AddressKey, byte> values = new Dictionary<PlayerEnums.AddressKey, byte>();

            this.TryParseFreezeValue(this.TeamConditionFreezeCheck, this.TeamConditionInput, PlayerEnums.AddressKey.CONDITION, values);
            this.TryParseFreezeValue(this.TeamFreshnessFreezeCheck, this.TeamFreshnessInput, PlayerEnums.AddressKey.FRESHNESS, values);

            return values;
        }

        private void TryParseFreezeValue (CheckBox checkBox, TextBox textBox, PlayerEnums.AddressKey key, Dictionary<PlayerEnums.AddressKey, byte> dictionary)
        {
            if (checkBox.Checked && !String.IsNullOrEmpty(textBox.Text)) {
                byte parsedValue;
                if (byte.TryParse(textBox.Text, out parsedValue)) {
                    dictionary.Add(key, parsedValue);
                }
            }
        }

        private void GithubLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.LinkLabel_LinkClicked(sender, e);
        }
    }
}
