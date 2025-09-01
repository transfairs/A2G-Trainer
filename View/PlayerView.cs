using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Memory;
using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.ListView;

namespace A2G_Trainer_XP.View
{
    public partial class PlayerView : EntityView
    {
        public PlayerView(Mem memory, ProcessController controller) : base(memory, controller)
        {
            InitializeComponent();
            InitPlayerListView();
        }

        public PlayerView(IContainer container) : base(container)
        {
            InitializeComponent();
            InitPlayerListView();
        }
        private void InitPlayerListView()
        {
            this.PlayerListView.Columns.Add("Pos", 35);
            this.PlayerListView.Columns.Add("Name", 105);
            this.PlayerListView.Columns.Add("Stärke", 45, HorizontalAlignment.Center);
            this.PlayerListView.SelectedIndexChanged += this.PlayerListView_SelectedIndexChanged;
        }

        #region InitMainTabControl
        internal void InitMainTabControl()
        {
            this.ShowClubLabel();

            this.NationalityCombo.DataSource = Enum.GetValues(typeof(PlayerEnums.Country)).Cast<PlayerEnums.Country>().Select(c => new { Value = c, Text = PlayerEnums.GetDescription(c) }).ToList();
            this.NationalityCombo.DisplayMember = "Text";
            this.NationalityCombo.ValueMember = "Value";

            if (this.PlayerListView != null && this.PlayerListView.Items != null && this.PlayerListView.Items.Count > 0)
            {
                this.bindingSource = new BindingSource
                {
                    DataSource = this.PlayerListView.Items[0].Tag
                };
                /*
                BindingSource clubBindingSource = new BindingSource
                {
                    DataSource = this.clubController.Club
                };

                this.ClubSelect.DataBindings.Add("SelectedValue", clubBindingSource, "ClubName");
                */

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
                this.ContractDurationInput.KeyPress += this.NumericOnly_KeyPress;
                this.YearsInClubInput.KeyPress += this.NumericOnly_KeyPress;
                this.Retires.KeyPress += this.NumericOnly_KeyPress;

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
                this.ContractDurationInput.TextChanged += this.ByteMax255_TextChanged;
                this.YearsInClubInput.TextChanged += this.ByteMax255_TextChanged;
                this.Retires.TextChanged += this.ByteMax255_TextChanged;

                this.LastNameInput.DataBindings.Add("Text", this.bindingSource, "Lastname");
                this.FirstNameInput.DataBindings.Add("Text", this.bindingSource, "Firstname");
                this.LevelInput.DataBindings.Add("Text", this.bindingSource, "Level");
                this.AgeInput.DataBindings.Add("Text", this.bindingSource, "Age");
                this.NationalityCombo.DataBindings.Add("SelectedValue", this.bindingSource, "Nationality");
                this.ConditionInput.DataBindings.Add("Text", this.bindingSource, "Condition");
                this.FreshnessInput.DataBindings.Add("Text", this.bindingSource, "Freshness");
                this.FormInput.DataBindings.Add("Text", this.bindingSource, "Form");

                this.FairSkin.DataBindings.Add("Checked", this.bindingSource, "HasFairSkin");
                // Console.WriteLine($"{((Player) this.bindingSource.DataSource).HasFairSkin}, {((Player) this.bindingSource.DataSource).SkinColor}");
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
                this.ContractDurationInput.DataBindings.Add("Text", this.bindingSource, "ContractDuration");
                this.Leased.DataBindings.Add("Checked", this.bindingSource, "IsLeased");
                this.LeasedWithOption.DataBindings.Add("Checked", this.bindingSource, "HasBuyOption");
                this.UnknownContractCheckbox.DataBindings.Add("Checked", this.bindingSource, "IsUnknownContractDetail");
                this.JoinedThisSeason.DataBindings.Add("Checked", this.bindingSource, "IsJoinedThisSeason");
                this.OptionPlayer.DataBindings.Add("Checked", this.bindingSource, "HasOptionPlayer");
                this.OptionClub.DataBindings.Add("Checked", this.bindingSource, "HasOptionClub");
                this.SeatedGuarantee.DataBindings.Add("Checked", this.bindingSource, "IsSeatedGuarantee");
                this.UnsetContractDetail.DataBindings.Add("Checked", this.bindingSource, "IsUnsetContractDetail");
                this.YearsInClubInput.DataBindings.Add("Text", this.bindingSource, "YearsInClub");
                this.Retires.DataBindings.Add("Checked", this.bindingSource, "IsRetires");
            }
        }

        private void ShowClubLabel()
        {
            this.CurrentClubLabel.Text = String.IsNullOrEmpty(this.clubController.Club.ClubName) ? "!! Kein spielbarer Verein !!" : this.clubController.Club.ClubName;
        }
        #endregion

        internal void RefreshPlayerListView(PlayerEnums.AddressType type)
        {
            if (this.IsGameRunning())
            {
                //this.DebugLabel.Text = $"{this.processController.IsGog}: {this.memory.mProc.Process.MainModule.ModuleName}, {this.memory.mProc.Process.MainModule.FileName}";

                this.clubController = new ClubController(this.Memory, this.processController.IsGog, type);
                Console.WriteLine($"Eigener/Gegner: {this.clubController.Club.ClubName}, {this.clubController.Club.Id}, {this.clubController.Club.Country}");
                if (type == PlayerEnums.AddressType.DYNAMIC)
                {
                    ushort pc = this.clubController.Club.PlayerCount;
                    byte apc = this.clubController.Club.AmateurPlayerCount;
                    this.playerController = new PlayerController(this.Memory, this.clubController.Club, this.processController.IsGog, type);
                    Player firstPlayer = this.playerController.EntityList.First();
                    Club dynamic = this.clubController.EntityList.FirstOrDefault(c => c.Id == firstPlayer.ClubId && c.Country == firstPlayer.ClubCountry);
                    if (dynamic == null)
                    {
                        this.clubController.Club.PlayerCount = pc;
                        this.clubController.Club.AmateurPlayerCount = apc;
                        this.clubController.Club.ClubName = "";
                    }
                    this.clubController.Club = dynamic ?? this.clubController.Club;

                    //Console.WriteLine($"Erster Spieler: {firstPlayer.Lastname}, {firstPlayer.ClubCountry}, {firstPlayer.ClubId}, {this.ClubController.Club.ClubName}, {this.ClubController.Club.PlayerCount}, {this.ClubController.Club.AmateurPlayerCount}");
                }

                /* Backup solution, if PlayerCount is wrong.
                if (type == PlayerEnums.AddressType.OPPONENT)
                {
                    String opponentName = this.Memory.ReadString($"{this.Memory.mProc.MainModule.ModuleName}+{Settings.OpponentName}", length: 19, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
                    Club opponent = this.clubController.EntityList.FirstOrDefault(c => c.ClubName == opponentName);
                    this.clubController.Club = opponent ?? this.clubController.Club;
                }
                */
                this.playerController = new PlayerController(this.Memory, this.clubController.Club, this.processController.IsGog, type);

                Player player = (this.PlayerListView.SelectedItems.Count > 0) ? (Player)this.PlayerListView.SelectedItems[0].Tag : null;
                //Console.WriteLine($"Refresh: {player}");
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
                }
            }
        }
        private void ReloadBtn_Click(object sender, EventArgs e)
        {
            this.RefreshPlayerListView(this.playerController.Type);
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
                this.playerController.SaveEntityList();
                this.ClearAllFields(this.TeamBus);
                this.RefreshPlayerListView(this.playerController.Type);
            }
        }

        private void GithubLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.LinkLabel_LinkClicked(sender, e);
        }
    }
}
