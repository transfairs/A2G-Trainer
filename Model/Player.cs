using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace A2G_Trainer_XP.Model
{
    public class Player : Entity, INotifyPropertyChanged
    {
        public uint Id { get => this.id; set { this.id = value; this.OnPropertyChanged(nameof(this.Id)); } }
        private uint id = 0;

        public Addresses Addresses { get => this.addresses; set { this.addresses = value; this.OnPropertyChanged(nameof(this.Addresses)); } }
        private Addresses addresses;

        #region Helpers
        private readonly List<string> positionHelpers = new List<string>() { nameof(IsTO), nameof(IsL), nameof(IsMD), nameof(IsLV), nameof(IsRV), nameof(IsRM), nameof(IsLM), nameof(IsDM), nameof(IsOM), nameof(IsS) };
        private readonly List<string> secondaryPositionHelpers = new List<string>() { nameof(IsSecondaryTO), nameof(IsSecondaryL), nameof(IsSecondaryMD), nameof(IsSecondaryLV), nameof(IsSecondaryRV), nameof(IsSecondaryRM), nameof(IsSecondaryLM), nameof(IsSecondaryDM), nameof(IsSecondaryOM), nameof(IsSecondaryS) };
        public bool HasFairSkin  { get => this.SkinColor.HasFlag(PlayerEnums.SkinColor.Fair);  set => this.Setter(PlayerEnums.SkinColor.Fair, value, nameof(this.HasFairSkin)); }
        public bool HasDarkSkin  { get => this.SkinColor.HasFlag(PlayerEnums.SkinColor.Dark);  set => this.Setter(PlayerEnums.SkinColor.Dark, value, nameof(this.HasDarkSkin)); }
        public bool HasBlackSkin { get => this.SkinColor.HasFlag(PlayerEnums.SkinColor.Black); set => this.Setter(PlayerEnums.SkinColor.Black, value, nameof(this.HasBlackSkin)); }
        public bool HasLightBlondHair { get => this.HairColor.HasFlag(PlayerEnums.HairColor.Hellblond); set => this.Setter(PlayerEnums.HairColor.Hellblond, value, nameof(this.HasLightBlondHair)); }
        public bool HasBlondHair      { get => this.HairColor.HasFlag(PlayerEnums.HairColor.Blond);     set => this.Setter(PlayerEnums.HairColor.Blond, value, nameof(this.HasBlondHair)); }
        public bool HasBrownHair      { get => this.HairColor.HasFlag(PlayerEnums.HairColor.Braun);     set => this.Setter(PlayerEnums.HairColor.Braun, value, nameof(this.HasBrownHair)); }
        public bool IsGinger          { get => this.HairColor.HasFlag(PlayerEnums.HairColor.Rot);       set => this.Setter(PlayerEnums.HairColor.Rot, value, nameof(this.IsGinger)); }
        public bool HasBlackHair      { get => this.HairColor.HasFlag(PlayerEnums.HairColor.Schwarz);   set => this.Setter(PlayerEnums.HairColor.Schwarz, value, nameof(this.HasBlackHair)); }
        public bool HasNoHair         { get => this.HairColor.HasFlag(PlayerEnums.HairColor.Glatze);    set => this.Setter(PlayerEnums.HairColor.Glatze, value, nameof(this.HasNoHair)); }
        public bool HasKopfball        { get => this.Skills.HasFlag(PlayerEnums.Skills.Kopfball)        && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Kopfball,        true, value, nameof(this.HasKopfball)); }
        public bool HasZweikampf       { get => this.Skills.HasFlag(PlayerEnums.Skills.Zweikampf)       && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Zweikampf,       true, value, nameof(this.HasZweikampf)); }
        public bool HasSchnelligkeit   { get => this.Skills.HasFlag(PlayerEnums.Skills.Schnelligkeit)   && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Schnelligkeit,   true, value, nameof(this.HasSchnelligkeit)); }
        public bool HasSchusskraft     { get => this.Skills.HasFlag(PlayerEnums.Skills.Schusskraft)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Schusskraft,     true, value, nameof(this.HasSchusskraft)); }
        public bool HasFreistoesse     { get => this.Skills.HasFlag(PlayerEnums.Skills.Freistoesse)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Freistoesse,     true, value, nameof(this.HasFreistoesse)); }
        public bool HasFlanken         { get => this.Skills.HasFlag(PlayerEnums.Skills.Flanken)         && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Flanken,         true, value, nameof(this.HasFlanken)); }
        public bool HasTorinstinkt     { get => this.Skills.HasFlag(PlayerEnums.Skills.Torinstinkt)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Torinstinkt,     true, value, nameof(this.HasTorinstinkt)); }
        public bool HasLaufstaerke     { get => this.Skills.HasFlag(PlayerEnums.Skills.Laufstaerke)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Laufstaerke,     true, value, nameof(this.HasLaufstaerke)); }
        public bool HasTechnik         { get => this.Skills.HasFlag(PlayerEnums.Skills.Technik)         && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Technik,         true, value, nameof(this.HasTechnik)); }
        public bool HasSpielmacher     { get => this.Skills.HasFlag(PlayerEnums.Skills.Spielmacher)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Spielmacher,     true, value, nameof(this.HasSpielmacher)); }
        public bool HasBeidfuessigkeit { get => this.Skills.HasFlag(PlayerEnums.Skills.Beidfuessigkeit) && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Beidfuessigkeit, true, value, nameof(this.HasBeidfuessigkeit)); }
        public bool HasElfmetertoeter  { get => this.Skills.HasFlag(PlayerEnums.Skills.Elfmetertoeter)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Elfmetertoeter,  true, value, nameof(this.HasElfmetertoeter)); }
        public bool HasStarkeReflexe   { get => this.Skills.HasFlag(PlayerEnums.Skills.StarkeReflexe)   && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.StarkeReflexe,   true, value, nameof(this.HasStarkeReflexe)); }
        public bool HasHerauslaufen    { get => this.Skills.HasFlag(PlayerEnums.Skills.Herauslaufen)    && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Herauslaufen,    true, value, nameof(this.HasHerauslaufen)); }
        public bool HasFangsicherheit  { get => this.Skills.HasFlag(PlayerEnums.Skills.Fangsicherheit)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Fangsicherheit,  true, value, nameof(this.HasFangsicherheit)); }
        public bool HasFausten         { get => this.Skills.HasFlag(PlayerEnums.Skills.Fausten)         && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Fausten,         true, value, nameof(this.HasFausten)); }
        public bool HasBallsicherheit  { get => this.Skills.HasFlag(PlayerEnums.Skills.Ballsicherheit)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Ballsicherheit,  true, value, nameof(this.HasBallsicherheit)); }
        public bool HasNegKopfball        { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Kopfball)        && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Kopfball,        false, value, nameof(this.HasNegKopfball)); }
        public bool HasNegZweikampf       { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Zweikampf)       && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Zweikampf,       false, value, nameof(this.HasNegZweikampf)); }
        public bool HasNegSchnelligkeit   { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Schnelligkeit)   && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Schnelligkeit,   false, value, nameof(this.HasNegSchnelligkeit)); }
        public bool HasNegSchusskraft     { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Schusskraft)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Schusskraft,     false, value, nameof(this.HasNegSchusskraft)); }
        public bool HasNegFreistoesse     { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Freistoesse)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Freistoesse,     false, value, nameof(this.HasNegFreistoesse)); }
        public bool HasNegFlanken         { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Flanken)         && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Flanken,         false, value, nameof(this.HasNegFlanken)); }
        public bool HasNegTorinstinkt     { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Torinstinkt)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Torinstinkt,     false, value, nameof(this.HasNegTorinstinkt)); }
        public bool HasNegLaufstaerke     { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Laufstaerke)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Laufstaerke,     false, value, nameof(this.HasNegLaufstaerke)); }
        public bool HasNegTechnik         { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Technik)         && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Technik,         false, value, nameof(this.HasNegTechnik)); }
        public bool HasNegSpielmacher     { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Spielmacher)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Spielmacher,     false, value, nameof(this.HasNegSpielmacher)); }
        public bool HasNegBeidfuessigkeit { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Beidfuessigkeit) && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Beidfuessigkeit, false, value, nameof(this.HasNegBeidfuessigkeit)); }
        public bool HasNegElfmetertoeter  { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Elfmetertoeter)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Elfmetertoeter,  false, value, nameof(this.HasNegElfmetertoeter)); }
        public bool HasNegStarkeReflexe   { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.StarkeReflexe)   && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.StarkeReflexe,   false, value, nameof(this.HasNegStarkeReflexe)); }
        public bool HasNegHerauslaufen    { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Herauslaufen)    && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Herauslaufen,    false, value, nameof(this.HasNegHerauslaufen)); }
        public bool HasNegFangsicherheit  { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Fangsicherheit)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Fangsicherheit,  false, value, nameof(this.HasNegFangsicherheit)); }
        public bool HasNegFausten         { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Fausten)         && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Fausten,         false, value, nameof(this.HasNegFausten)); }
        public bool HasNegBallsicherheit  { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Ballsicherheit)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Ballsicherheit,  false, value, nameof(this.HasNegBallsicherheit)); }

        public bool IsTO { get => this.Position == PlayerEnums.Position.TO; set => this.Setter(PlayerEnums.Position.TO, value, nameof(this.IsTO)); }
        public bool IsNotTO => !this.IsTO;
        public bool IsL  { get => this.Position == PlayerEnums.Position.L;  set => this.Setter(PlayerEnums.Position.L,  value, nameof(this.IsL)); }
        public bool IsMD { get => this.Position == PlayerEnums.Position.MD; set => this.Setter(PlayerEnums.Position.MD, value, nameof(this.IsMD)); }
        public bool IsLV { get => this.Position == PlayerEnums.Position.LV; set => this.Setter(PlayerEnums.Position.LV, value, nameof(this.IsLV)); }
        public bool IsRV { get => this.Position == PlayerEnums.Position.RV; set => this.Setter(PlayerEnums.Position.RV, value, nameof(this.IsRV)); }
        public bool IsRM { get => this.Position == PlayerEnums.Position.RM; set => this.Setter(PlayerEnums.Position.RM, value, nameof(this.IsRM)); }
        public bool IsLM { get => this.Position == PlayerEnums.Position.LM; set => this.Setter(PlayerEnums.Position.LM, value, nameof(this.IsLM)); }
        public bool IsDM { get => this.Position == PlayerEnums.Position.DM; set => this.Setter(PlayerEnums.Position.DM, value, nameof(this.IsDM)); }
        public bool IsOM { get => this.Position == PlayerEnums.Position.OM; set => this.Setter(PlayerEnums.Position.OM, value, nameof(this.IsOM)); }
        public bool IsS  { get => this.Position == PlayerEnums.Position.S;  set => this.Setter(PlayerEnums.Position.S,  value, nameof(this.IsS)); }
        public bool IsSecondaryTO { get => this.SecondaryPositions.Contains(PlayerEnums.Position.TO); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.TO); else this.RemoveSecondaryPosition(PlayerEnums.Position.TO); } }
        public bool IsSecondaryL  { get => this.SecondaryPositions.Contains(PlayerEnums.Position.L);  set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.L);  else this.RemoveSecondaryPosition(PlayerEnums.Position.L);  } }
        public bool IsSecondaryMD { get => this.SecondaryPositions.Contains(PlayerEnums.Position.MD); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.MD); else this.RemoveSecondaryPosition(PlayerEnums.Position.MD); } }
        public bool IsSecondaryLV { get => this.SecondaryPositions.Contains(PlayerEnums.Position.LV); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.LV); else this.RemoveSecondaryPosition(PlayerEnums.Position.LV); } }
        public bool IsSecondaryRV { get => this.SecondaryPositions.Contains(PlayerEnums.Position.RV); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.RV); else this.RemoveSecondaryPosition(PlayerEnums.Position.RV); } }
        public bool IsSecondaryRM { get => this.SecondaryPositions.Contains(PlayerEnums.Position.RM); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.RM); else this.RemoveSecondaryPosition(PlayerEnums.Position.RM); } }
        public bool IsSecondaryLM { get => this.SecondaryPositions.Contains(PlayerEnums.Position.LM); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.LM); else this.RemoveSecondaryPosition(PlayerEnums.Position.LM); } }
        public bool IsSecondaryDM { get => this.SecondaryPositions.Contains(PlayerEnums.Position.DM); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.DM); else this.RemoveSecondaryPosition(PlayerEnums.Position.DM); } }
        public bool IsSecondaryOM { get => this.SecondaryPositions.Contains(PlayerEnums.Position.OM); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.OM); else this.RemoveSecondaryPosition(PlayerEnums.Position.OM); } }
        public bool IsSecondaryS  { get => this.SecondaryPositions.Contains(PlayerEnums.Position.S);  set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.S);  else this.RemoveSecondaryPosition(PlayerEnums.Position.S);  } }

        public bool IsNormalChar     { get => this.Character == PlayerEnums.Character.Normal;         set => this.Setter(PlayerEnums.Character.Normal, value, nameof(this.IsNormalChar)); }
        public bool IsHitzkopf       { get => this.Character == PlayerEnums.Character.Hitzkopf;       set => this.Setter(PlayerEnums.Character.Hitzkopf, value, nameof(this.IsHitzkopf)); }
        public bool IsFrohnatur      { get => this.Character == PlayerEnums.Character.Frohnatur;      set => this.Setter(PlayerEnums.Character.Frohnatur, value, nameof(this.IsFrohnatur)); }
        public bool IsMannOhneNerven { get => this.Character == PlayerEnums.Character.MannOhneNerven; set => this.Setter(PlayerEnums.Character.MannOhneNerven, value, nameof(this.IsMannOhneNerven)); }
        public bool IsNervenbuendel  { get => this.Character == PlayerEnums.Character.Nervenbuendel;  set => this.Setter(PlayerEnums.Character.Nervenbuendel, value, nameof(this.IsNervenbuendel)); }
        public bool IsPhlegma        { get => this.Character == PlayerEnums.Character.Phlegma;        set => this.Setter(PlayerEnums.Character.Phlegma, value, nameof(this.IsPhlegma)); }
        public bool IsGeldgeier      { get => this.Character == PlayerEnums.Character.Geldgeier;      set => this.Setter(PlayerEnums.Character.Geldgeier, value, nameof(this.IsGeldgeier)); }
        public bool IsMusterprofi    { get => this.Character == PlayerEnums.Character.Musterprofi;    set => this.Setter(PlayerEnums.Character.Musterprofi, value, nameof(this.IsMusterprofi)); }
        public bool IsSkandalnudel   { get => this.Character == PlayerEnums.Character.Skandalnudel;   set => this.Setter(PlayerEnums.Character.Skandalnudel, value, nameof(this.IsSkandalnudel)); }
        public bool HasNormalHealth { get => this.Health == PlayerEnums.Health.Normal;        set => this.Setter(PlayerEnums.Health.Normal, value, nameof(this.HasNormalHealth)); }
        public bool IsRobust        { get => this.Health == PlayerEnums.Health.Robustheit;    set => this.Setter(PlayerEnums.Health.Robustheit, value, nameof(this.IsRobust)); }
        public bool IsAnfaellig     { get => this.Health == PlayerEnums.Health.Anfaelligkeit; set => this.Setter(PlayerEnums.Health.Anfaelligkeit, value, nameof(this.IsAnfaellig)); }
        public bool HasKnieprobleme { get => this.Health == PlayerEnums.Health.Knieprobleme;  set => this.Setter(PlayerEnums.Health.Knieprobleme, value, nameof(this.HasKnieprobleme)); }
        public bool IsNoone                { get => this.Personality == PlayerEnums.Personality.None;                 set => this.Setter(PlayerEnums.Personality.None, value, nameof(this.IsNoone)); }
        public bool IsFuehrungsperson      { get => this.Personality == PlayerEnums.Personality.Fuehrungsperson;      set => this.Setter(PlayerEnums.Personality.Fuehrungsperson, value, nameof(this.IsFuehrungsperson)); }
        public bool IsKaempfernatur        { get => this.Personality == PlayerEnums.Personality.Kaempfernatur;        set => this.Setter(PlayerEnums.Personality.Kaempfernatur, value, nameof(this.IsKaempfernatur)); }
        public bool IsTrainingsweltmeister { get => this.Personality == PlayerEnums.Personality.Trainingsweltmeister; set => this.Setter(PlayerEnums.Personality.Trainingsweltmeister, value, nameof(this.IsTrainingsweltmeister)); }
        public bool IsTrainingsfaul        { get => this.Personality == PlayerEnums.Personality.Trainingsfaul;        set => this.Setter(PlayerEnums.Personality.Trainingsfaul, value, nameof(this.IsTrainingsfaul)); }
        public bool IsTreter               { get => this.Personality == PlayerEnums.Personality.Treter;               set => this.Setter(PlayerEnums.Personality.Treter, value, nameof(this.IsTreter)); }
        public bool IsFairPlayer           { get => this.Personality == PlayerEnums.Personality.FairPlayer;           set => this.Setter(PlayerEnums.Personality.FairPlayer, value, nameof(this.IsFairPlayer)); }
        public bool IsSchwalbenkoenig      { get => this.Personality == PlayerEnums.Personality.Schwalbenkoenig;      set => this.Setter(PlayerEnums.Personality.Schwalbenkoenig, value, nameof(this.IsSchwalbenkoenig)); }
        public bool IsAllrounder           { get => this.Personality == PlayerEnums.Personality.Allrounder;           set => this.Setter(PlayerEnums.Personality.Allrounder, value, nameof(this.IsAllrounder)); }
        public bool IsFlexiblePlayer       { get => this.Personality == PlayerEnums.Personality.FlexiblePlayer;       set => this.Setter(PlayerEnums.Personality.FlexiblePlayer, value, nameof(this.IsFlexiblePlayer)); }
        public bool IsHeimspieler          { get => this.Personality == PlayerEnums.Personality.Heimspieler;          set => this.Setter(PlayerEnums.Personality.Heimspieler, value, nameof(this.IsHeimspieler)); }
        public bool IsAuswaertsspieler     { get => this.Personality == PlayerEnums.Personality.AuswaertsSpieler;     set => this.Setter(PlayerEnums.Personality.AuswaertsSpieler, value, nameof(this.IsAuswaertsspieler)); }
        public bool IsTalent               { get => this.Personality == PlayerEnums.Personality.Talent;               set => this.Setter(PlayerEnums.Personality.Talent, value, nameof(this.IsTalent)); }
        public string TalentLabel          { get => this.Age >= 25 ? "ewiges Talent" : "" + "Talent"; private set { } }

        public bool IsTollerVertrag        { get => this.Happy.HasFlag(PlayerEnums.Happy.TollerVertrag);        set => this.Multiplex(PlayerEnums.Happy.TollerVertrag, value, nameof(this.IsTollerVertrag)); }
        public bool IsTolleStimmung        { get => this.Happy.HasFlag(PlayerEnums.Happy.TolleStimmung);        set => this.Multiplex(PlayerEnums.Happy.TolleStimmung, value, nameof(this.IsTolleStimmung)); }
        public bool IsTollePraemien        { get => this.Happy.HasFlag(PlayerEnums.Happy.TollePraemien);        set => this.Multiplex(PlayerEnums.Happy.TollePraemien, value, nameof(this.IsTollePraemien)); }
        public bool IsTollerVertragDoppelt { get => this.Happy.HasFlag(PlayerEnums.Happy.TollerVertragDoppelt); set => this.Multiplex(PlayerEnums.Happy.TollerVertragDoppelt, value, nameof(this.IsTollerVertragDoppelt)); }
        public bool IsTollerTrainer        { get => this.Happy.HasFlag(PlayerEnums.Happy.TollerTrainer);        set => this.Multiplex(PlayerEnums.Happy.TollerTrainer, value, nameof(this.IsTollerTrainer)); }
        public bool IsWillEndlichSpielen    { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.WillEndlichSpielen);    set => this.Multiplex(PlayerEnums.Unhappy.WillEndlichSpielen, value, nameof(this.IsWillEndlichSpielen)); }
        public bool IsWillMehrGeld          { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.WillMehrGeld);          set => this.Multiplex(PlayerEnums.Unhappy.WillMehrGeld, value, nameof(this.IsWillMehrGeld)); }
        public bool IsMiesePraemien         { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.MiesePraemien);         set => this.Multiplex(PlayerEnums.Unhappy.MiesePraemien, value, nameof(this.IsMiesePraemien)); }
        public bool IsGehtNichtNachLeistung { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.GehtNichtNachLeistung); set => this.Multiplex(PlayerEnums.Unhappy.GehtNichtNachLeistung, value, nameof(this.IsGehtNichtNachLeistung)); }
        public bool IsFalschePosition       { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.FalschePosition);       set => this.Multiplex(PlayerEnums.Unhappy.FalschePosition, value, nameof(this.IsFalschePosition)); }
        public bool IsScheissVertrag        { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.ScheissVertrag);        set => this.Multiplex(PlayerEnums.Unhappy.ScheissVertrag, value, nameof(this.IsScheissVertrag)); }
        public bool IsMieseMannschaft       { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.MieseMannschaft);       set => this.Multiplex(PlayerEnums.Unhappy.MieseMannschaft, value, nameof(this.IsMieseMannschaft)); }
        public bool IsSchlechteStimmung     { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.SchlechteStimmung);     set => this.Multiplex(PlayerEnums.Unhappy.SchlechteStimmung, value, nameof(this.IsSchlechteStimmung)); }
        public bool IsVerletztUndNieBesucht { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.VerletztUndNieBesucht); set => this.Multiplex(PlayerEnums.Unhappy.VerletztUndNieBesucht, value, nameof(this.IsVerletztUndNieBesucht)); }
        public bool IsUngerechteBehandlung  { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.UngerechteBehandlung);  set => this.Multiplex(PlayerEnums.Unhappy.UngerechteBehandlung, value, nameof(this.IsUngerechteBehandlung)); }
        public bool IsBloedeFans            { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.BloedeFans);            set => this.Multiplex(PlayerEnums.Unhappy.BloedeFans, value, nameof(this.IsBloedeFans)); }
        public bool IsBloedeMitspieler      { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.BloedeMitspieler);      set => this.Multiplex(PlayerEnums.Unhappy.BloedeMitspieler, value, nameof(this.IsBloedeMitspieler)); }
        public bool IsScheissTrainer        { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.ScheissTrainer);        set => this.Multiplex(PlayerEnums.Unhappy.ScheissTrainer, value, nameof(this.IsScheissTrainer)); }
        public bool IsVerhandlungGeplatzt   { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.VerhandlungGeplatzt);   set => this.Multiplex(PlayerEnums.Unhappy.ScheissTrainer, value, nameof(this.IsVerhandlungGeplatzt)); }
        public bool IsScheissManager        { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.ScheissManager);        set => this.Multiplex(PlayerEnums.Unhappy.ScheissTrainer, value, nameof(this.IsScheissManager)); }
        public bool IsScheissNachwuchsrunde { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.ScheissNachwuchsrunde); set => this.Multiplex(PlayerEnums.Unhappy.ScheissNachwuchsrunde, value, nameof(this.IsScheissNachwuchsrunde)); }

        public bool IsLeased { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.Leased); set => this.Multiplex(PlayerEnums.Contract.Leased, value, nameof(this.IsLeased)); }
        public bool HasBuyOption { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.BuyOption); set => this.Multiplex(PlayerEnums.Contract.BuyOption, value, nameof(this.HasBuyOption)); }
        public bool IsUnknownContractDetail { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.Unknown); set => this.Multiplex(PlayerEnums.Contract.Unknown, value, nameof(this.IsUnknownContractDetail)); }
        public bool IsJoinedThisSeason { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.JoinedThisSeason); set => this.Multiplex(PlayerEnums.Contract.JoinedThisSeason, value, nameof(this.IsJoinedThisSeason)); }
        public bool HasOptionPlayer { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.OptionPlayer); set => this.Multiplex(PlayerEnums.Contract.OptionPlayer, value, nameof(this.HasOptionPlayer)); }
        public bool HasOptionClub { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.OptionClub); set => this.Multiplex(PlayerEnums.Contract.OptionClub, value, nameof(this.HasOptionClub)); }
        public bool IsSeatedGuarantee { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.Seated); set => this.Multiplex(PlayerEnums.Contract.Seated, value, nameof(this.IsSeatedGuarantee)); }
        public bool IsUnsetContractDetail { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.Unset); set => this.Multiplex(PlayerEnums.Contract.Unset, value, nameof(this.IsUnsetContractDetail)); }
        public bool IsRetires { get => this.Career.HasFlag(PlayerEnums.Career.Retires); set => this.Multiplex(PlayerEnums.Career.Retires, value, nameof(this.IsRetires)); }
        #endregion

        #region Overview
        public string Firstname { get => this.firstname; set { if (Tools.LimitedStringEquals(value, this.firstname, 9)) {
                    this.firstname = value != null && value.Length > 9 ? value.Substring(0, 9) : value; this.OnPropertyChanged(nameof(this.Firstname));
                } } }
        private string firstname = string.Empty;
        public string Lastname { get => this.lastname; set { if (Tools.LimitedStringEquals(value, this.lastname, 15)) {
                    this.lastname = value != null && value.Length > 15 ? value.Substring(0, 15) : value; this.OnPropertyChanged(nameof(this.Lastname));
                } } }
        private string lastname = string.Empty;
        public PlayerEnums.SkinColor SkinColor { get => this.skinColor; set => this.Setter(value); }
        private PlayerEnums.SkinColor skinColor = 0;
        public PlayerEnums.HairColor HairColor { get => this.hairColor; set => this.Setter(value); }
        private PlayerEnums.HairColor hairColor = 0;
        public byte Age { get => this.age; set { this.age = value; this.OnPropertyChanged(nameof(this.Age)); } }
        private byte age = 0;
        public byte Level { get => this.level; set { this.level = value; this.OnPropertyChanged(nameof(this.Age)); } }
        private byte level = 0;
        public byte Form { get => this.form; set { this.form = value; this.OnPropertyChanged(nameof(this.Form)); } }
        private byte form = 0;
        public byte Condition { get => this.condition; set { this.condition = value; this.OnPropertyChanged(nameof(this.Condition)); } }
        private byte condition = 0;
        public byte Freshness { get => this.freshness; set { this.freshness = value; this.OnPropertyChanged(nameof(this.Freshness)); } }
        byte freshness = 0;
        public PlayerEnums.Country Nationality { get => this.nationality; set => this.Setter(value); }
        private PlayerEnums.Country nationality = PlayerEnums.Country.Sonstige;
        #endregion

        #region Position
        public PlayerEnums.Position Position { get => this.position; set => this.Setter(value); }
        private PlayerEnums.Position position = PlayerEnums.Position.None;
        public List<PlayerEnums.Position> SecondaryPositions { get => this.secondaryPositions; set => this.Setter(value); }
        private List<PlayerEnums.Position> secondaryPositions;
        #endregion

        #region Skills
        public PlayerEnums.Skills Skills { get => this.skills; set => this.Setter(value, true); }
        private PlayerEnums.Skills skills = PlayerEnums.Skills.None;
        public PlayerEnums.Skills NegativeSkills { get => this.negativeSkills; set => this.Setter(value, false); }
        private PlayerEnums.Skills negativeSkills = PlayerEnums.Skills.None;
        #endregion

        #region Character
        public PlayerEnums.Personality Personality { get => this.personality; set => this.Setter(value); }
        private PlayerEnums.Personality personality = PlayerEnums.Personality.None;
        public PlayerEnums.Character Character { get => this.character; set => this.Setter(value); }
        private PlayerEnums.Character character = PlayerEnums.Character.Normal;
        public PlayerEnums.Health Health { get => this.health; set => this.Setter(value); }
        private PlayerEnums.Health health = PlayerEnums.Health.Normal;
        #endregion

        #region Constitution
        public PlayerEnums.Unhappy Unhappy { get => this.unhappy; set => this.Setter(value); }
        private PlayerEnums.Unhappy unhappy = PlayerEnums.Unhappy.None;
        public PlayerEnums.Happy Happy { get => this.happy; set => this.Setter(value); }
        private PlayerEnums.Happy happy = PlayerEnums.Happy.None;
        public byte Injury { get => this.injury; set { this.injury = value; this.OnPropertyChanged(nameof(this.Injury)); } }
        private byte injury = 0;
        public ushort InjuredDays { get => this.injuredDays; set { this.injuredDays = value; this.OnPropertyChanged(nameof(this.InjuredDays)); } }
        private ushort injuredDays = 0;
        public bool Vulnerable { get => this.vulnerable; set { this.vulnerable = value; this.OnPropertyChanged(nameof(this.Vulnerable)); } }
        private bool vulnerable = false;
        public byte RedCardBannedMatches { get => this.redCardBannedMatches; set { this.redCardBannedMatches = value; this.OnPropertyChanged(nameof(this.RedCardBannedMatches)); } }
        private byte redCardBannedMatches = 0;
        public bool Doped { get => this.doped; set { this.doped = value; this.OnPropertyChanged(nameof(this.Doped)); } }
        private bool doped = false;
        public byte YellowCardsSeason { get => this.yellowCardsSeason; set { this.yellowCardsSeason = value; this.OnPropertyChanged(nameof(this.YellowCardsSeason)); } }
        private byte yellowCardsSeason = 0;
        #endregion

        #region Contract
        public ushort Salary { get => this.salary; set { this.salary = value; this.OnPropertyChanged(nameof(this.Salary)); } }
        private ushort salary = 0;
        public ushort ShowUpBonus { get => this.showUpBonus; set { this.showUpBonus = value; this.OnPropertyChanged(nameof(this.ShowUpBonus)); } }
        private ushort showUpBonus = 0;
        public ushort GoalsBonus { get => this.goalsBonus; set { this.goalsBonus = value; this.OnPropertyChanged(nameof(this.GoalsBonus)); } }
        private ushort goalsBonus = 0;
        public ushort TransferFee { get => this.transferFee; set { this.transferFee = value; this.OnPropertyChanged(nameof(this.TransferFee)); } }
        private ushort transferFee = 0;
        public byte ContractDuration { get => this.contractDuration; set { this.contractDuration = value; this.OnPropertyChanged(nameof(this.ContractDuration)); } }
        private byte contractDuration = 0;
        public PlayerEnums.Contract ContractDetails { get => this.contractDetails; set => this.Setter(value); }
        private PlayerEnums.Contract contractDetails = PlayerEnums.Contract.None;
        public byte YearsInClub { get => this.yearsInClub; set { this.yearsInClub = value; this.OnPropertyChanged(nameof(this.YearsInClub)); } }
        private byte yearsInClub = 0;
        public PlayerEnums.Career Career { get => this.career; set => this.Setter(value); }
        private PlayerEnums.Career career = PlayerEnums.Career.None;
        #endregion

        #region NotImplemented
        /*
        public ushort TeamId { get => this.teamId; set => this. = this.Setter(value, this.teamId); }
        private ushort teamId = 0;
        public byte PreviousForm { get => this.previousForm; set => this. = this.Setter(value, this.previousForm); }
        private byte previousForm = 0;
        public byte Jersey { get => this.jersey; set => this. = this.Setter(value, this.jersey); }
        private byte jersey = 0;
        public byte RedCardsSeason { get => this.redCardsSeason; set => this. = this.Setter(value, this.redCardsSeason); }
        private byte redCardsSeason = 0;
        public byte YellowRedCardsSeason { get => this.yellowRedCardsSeason; set => this. = this.Setter(value, this.yellowRedCardsSeason); }
        private byte yellowRedCardsSeason = 0;
        public byte Goals { get => this.goals; set => this. = this.Setter(value, this.goals); }
        private byte goals = 0;
        public byte GoalsCupNational { get => this.goalsCupNational; set => this. = this.Setter(value, this.goalsCupNational); }
        private byte goalsCupNational = 0;
        public byte GoalsCupInternational { get => this.goalsCupInternational; set => this. = this.Setter(value, this.goalsCupInternational); }
        private byte goalsCupInternational = 0;
        public ushort Goals1stLeague { get => this.goals1stLeague; set => this. = this.Setter(value, this.goals1stLeague); }
        private ushort goals1stLeague = 0;
        public byte Assists { get => this.assists; set => this. = this.Setter(value, this.assists); }
        private byte assists = 0;
        public byte JokerGoals { get => this.jokerGoals; set => this. = this.Setter(value, this.jokerGoals); }
        private byte jokerGoals = 0;
        public byte PenaltiesSeason { get => this.penaltiesSeason; set => this. = this.Setter(value, this.penaltiesSeason); }
        private byte penaltiesSeason = 0;
        public byte Penalties { get => this.penalties; set => this. = this.Setter(value, this.penalties); }
        private byte penalties = 0;
        public byte FreekicksSeason { get => this.freekicksSeason; set => this. = this.Setter(value, this.freekicksSeason); }
        private byte freekicksSeason = 0;
        public byte Freekicks { get => this.freekicks; set => this. = this.Setter(value, this.freekicks); }
        private byte freekicks = 0;
        public byte AppearancesSeason { get => this.appearancesSeason; set => this. = this.Setter(value, this.appearancesSeason); }
        private byte appearancesSeason = 0;
        public byte JokerAppearances { get => this.jokerAppearances; set => this. = this.Setter(value, this.jokerAppearances); }
        private byte jokerAppearances = 0;
        public ushort Appearances1stLeague { get => this.appearances1stLeaque; set => this. = this.Setter(value, this.appearances1stLeaque); }
        private ushort appearances1stLeaque = 0;
        */
        #endregion

        public Player() : base()
        {
            this.SecondaryPositions = new List<PlayerEnums.Position> { PlayerEnums.Position.None, PlayerEnums.Position.None };
        }

        private void NotifyHelpers(List<string> helper)
        {
            helper.ForEach(this.OnPropertyChanged);
        }

        #region Setter
        protected void Setter(PlayerEnums.SkinColor value, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.skinColor = value;
                this.OnPropertyChanged(nameof(this.SkinColor));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        protected void Setter(PlayerEnums.HairColor value, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.hairColor = value;
                this.OnPropertyChanged(nameof(this.HairColor));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        protected void Setter(PlayerEnums.Country value)
        {
            this.nationality = value;
            this.OnPropertyChanged(nameof(this.Nationality));
        }
        protected void Setter(List<PlayerEnums.Position> value)
        {
            this.secondaryPositions = value;
            if (this.secondaryPositions != null)
            {
                while (this.secondaryPositions.Count > 2)
                {
                    this.secondaryPositions.RemoveAt(0);
                }
            }
            this.NotifyHelpers(this.secondaryPositionHelpers);
            this.OnPropertyChanged(nameof(this.SecondaryPositions));
        }
        private void Setter(PlayerEnums.Position pos, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.position = pos;
                this.OnPropertyChanged(nameof(this.Position));
            }
            this.NotifyHelpers(this.positionHelpers);
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Setter(PlayerEnums.Skills skill, bool positive)
        {
            if (positive)
            {
                this.skills = skill;
                this.OnPropertyChanged(nameof(this.Skills));
            }
            else
            {
                this.negativeSkills = skill;
                this.OnPropertyChanged(nameof(this.NegativeSkills));
            }
        }
        private void Setter(PlayerEnums.Character character, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.character = character;
                this.OnPropertyChanged(nameof(this.Character));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Setter(PlayerEnums.Contract contract, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.contractDetails = contract;
                this.OnPropertyChanged(nameof(this.ContractDetails));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Setter(PlayerEnums.Career career, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.career = career;
                this.OnPropertyChanged(nameof(this.Career));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Setter(PlayerEnums.Health health, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.health = health;
                this.OnPropertyChanged(nameof(this.Health));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Setter(PlayerEnums.Personality personality, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                this.personality = personality;
                this.OnPropertyChanged(nameof(this.Personality));
            }
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Setter(PlayerEnums.Unhappy unhappy)
        {
            this.unhappy = unhappy;
            this.OnPropertyChanged(nameof(this.Unhappy));
        }
        private void Setter(PlayerEnums.Happy happy)
        {
            this.happy = happy;
            this.OnPropertyChanged(nameof(this.Happy));
        }
        #endregion

        #region Modifier
        protected void AddSecondaryPosition(PlayerEnums.Position position)
        {
            if (this.secondaryPositions == null)
            {
                this.secondaryPositions = new List<PlayerEnums.Position>();
            }
            this.secondaryPositions.Add(position);
            while (this.secondaryPositions.Count > 2)
            {
                this.secondaryPositions.RemoveAt(0);
            }
            this.NotifyHelpers(this.secondaryPositionHelpers);
            this.OnPropertyChanged(nameof(this.SecondaryPositions));
        }
        private void Multiplex(PlayerEnums.Skills skill, bool positive, bool enabled = true, string helper = null)
        {
            if (enabled)
            {
                if (positive)
                    this.skills |= skill;
                else
                    this.negativeSkills |= skill;
            }
            else
            {
                if (positive)
                    this.skills &= ~skill;
                else
                    this.negativeSkills &= ~skill;
            }
            if (positive)
                this.OnPropertyChanged(nameof(this.Skills));
            else
                this.OnPropertyChanged(nameof(this.NegativeSkills));
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Multiplex(PlayerEnums.Contract contract, bool enabled, string helper)
        {
            if (enabled)
            {
                this.contractDetails |= contract;
            }
            else this.contractDetails &= ~contract;
            this.OnPropertyChanged(nameof(this.ContractDetails));
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Multiplex(PlayerEnums.Career career, bool enabled, string helper)
        {
            if (enabled)
            {
                this.career |= career;
            }
            else this.career &= ~career;
            this.OnPropertyChanged(nameof(this.Career));
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Multiplex(PlayerEnums.Happy happy, bool enabled, string helper)
        {
            if (enabled)
            {
                this.happy |= happy;
            }
            else this.happy &= ~happy;
            this.OnPropertyChanged(nameof(this.Happy));
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void Multiplex(PlayerEnums.Unhappy unhappy, bool enabled, string helper)
        {
            if (enabled)
            {
                this.unhappy |= unhappy;
            }
            else this.unhappy &= ~unhappy;
            this.OnPropertyChanged(nameof(this.Unhappy));
            if (helper != null) this.OnPropertyChanged(helper);
        }
        private void RemoveSecondaryPosition(PlayerEnums.Position pos)
        {
            if (this.secondaryPositions != null && this.secondaryPositions.Contains(pos))
            {
                this.secondaryPositions.Remove(pos);
                this.OnPropertyChanged(nameof(this.SecondaryPositions));
            }
            this.NotifyHelpers(this.secondaryPositionHelpers);
        }
        #endregion

        public override string ToString()
        {
            return this.Lastname + ", " + this.Firstname;
        }
    }

}
