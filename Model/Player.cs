using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// A single football player. Most boolean properties (IsX/HasX) are convenience views over
    /// an underlying enum (Position/Skills/Character/...) for data-binding to individual
    /// checkboxes; setting one updates the shared enum and notifies every sibling property so
    /// the UI stays in sync.
    /// </summary>
    public class Player : Entity, INotifyPropertyChanged
    {
        /// <summary>Player's numeric ID within the savegame.</summary>
        public uint Id { get => this.id; set { this.id = value; this.OnPropertyChanged(nameof(this.Id)); } }
        private uint id = 0;

        /// <summary>ID of this player's record in the persistent name pool.</summary>
        public ushort NameRecordId { get => this.nameRecordId; set { this.nameRecordId = value; this.OnPropertyChanged(nameof(this.NameRecordId)); } }
        private ushort nameRecordId = 0;

        /// <summary>ID of the club this player currently belongs to.</summary>
        public ushort ClubId { get => this.clubId; set { this.clubId = value; this.OnPropertyChanged(nameof(this.ClubId)); } }
        private ushort clubId;
        /// <summary>Country of the club this player currently belongs to.</summary>
        public PlayerEnums.Country ClubCountry { get => this.clubCountry; set { this.clubCountry = value; this.OnPropertyChanged(nameof(this.ClubCountry)); } }
        private PlayerEnums.Country clubCountry;

        /// <summary>Resolved memory addresses for this player's fields.</summary>
        public Addresses Addresses { get => this.addresses; set { this.addresses = value; this.OnPropertyChanged(nameof(this.Addresses)); } }
        private Addresses addresses;

        #region Helpers
        private readonly List<string> positionHelpers = new List<string>() { nameof(IsTO), nameof(IsL), nameof(IsMD), nameof(IsLV), nameof(IsRV), nameof(IsRM), nameof(IsLM), nameof(IsDM), nameof(IsOM), nameof(IsS) };
        private readonly List<string> secondaryPositionHelpers = new List<string>() { nameof(IsSecondaryTO), nameof(IsSecondaryL), nameof(IsSecondaryMD), nameof(IsSecondaryLV), nameof(IsSecondaryRV), nameof(IsSecondaryRM), nameof(IsSecondaryLM), nameof(IsSecondaryDM), nameof(IsSecondaryOM), nameof(IsSecondaryS) };
        // SkinColor/HairColor are plain (non-[Flags]) enums modelling a single selection, not bits -
        // equality is the correct test. HasFlag(x) would be wrong here even for the non-zero members,
        // and silently always-true for the zero-valued member (Fair/Hellblond), since Enum.HasFlag(0)
        // is defined to always return true regardless of the actual value.
        /// <summary>Whether the player's <see cref="SkinColor"/> is Fair.</summary>
        public bool HasFairSkin  { get => this.SkinColor == PlayerEnums.SkinColor.Fair;  set => this.Setter(PlayerEnums.SkinColor.Fair, value, nameof(this.HasFairSkin)); }
        /// <summary>Whether the player's <see cref="SkinColor"/> is Dark.</summary>
        public bool HasDarkSkin  { get => this.SkinColor == PlayerEnums.SkinColor.Dark;  set => this.Setter(PlayerEnums.SkinColor.Dark, value, nameof(this.HasDarkSkin)); }
        /// <summary>Whether the player's <see cref="SkinColor"/> is Black.</summary>
        public bool HasBlackSkin { get => this.SkinColor == PlayerEnums.SkinColor.Black; set => this.Setter(PlayerEnums.SkinColor.Black, value, nameof(this.HasBlackSkin)); }
        /// <summary>Whether the player's <see cref="HairColor"/> is light blond (Hellblond).</summary>
        public bool HasLightBlondHair { get => this.HairColor == PlayerEnums.HairColor.Hellblond; set => this.Setter(PlayerEnums.HairColor.Hellblond, value, nameof(this.HasLightBlondHair)); }
        /// <summary>Whether the player's <see cref="HairColor"/> is blond.</summary>
        public bool HasBlondHair      { get => this.HairColor == PlayerEnums.HairColor.Blond;     set => this.Setter(PlayerEnums.HairColor.Blond, value, nameof(this.HasBlondHair)); }
        /// <summary>Whether the player's <see cref="HairColor"/> is brown (Braun).</summary>
        public bool HasBrownHair      { get => this.HairColor == PlayerEnums.HairColor.Braun;     set => this.Setter(PlayerEnums.HairColor.Braun, value, nameof(this.HasBrownHair)); }
        /// <summary>Whether the player's <see cref="HairColor"/> is red (Rot).</summary>
        public bool IsGinger          { get => this.HairColor == PlayerEnums.HairColor.Rot;       set => this.Setter(PlayerEnums.HairColor.Rot, value, nameof(this.IsGinger)); }
        /// <summary>Whether the player's <see cref="HairColor"/> is black (Schwarz).</summary>
        public bool HasBlackHair      { get => this.HairColor == PlayerEnums.HairColor.Schwarz;   set => this.Setter(PlayerEnums.HairColor.Schwarz, value, nameof(this.HasBlackHair)); }
        /// <summary>Whether the player's <see cref="HairColor"/> is bald (Glatze).</summary>
        public bool HasNoHair         { get => this.HairColor == PlayerEnums.HairColor.Glatze;    set => this.Setter(PlayerEnums.HairColor.Glatze, value, nameof(this.HasNoHair)); }
        /// <summary>Whether the outfield player has the Heading (Kopfball) skill.</summary>
        public bool HasKopfball        { get => this.Skills.HasFlag(PlayerEnums.Skills.Kopfball)        && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Kopfball,        true, value, nameof(this.HasKopfball)); }
        /// <summary>Whether the outfield player has the Tackling (Zweikampf) skill.</summary>
        public bool HasZweikampf       { get => this.Skills.HasFlag(PlayerEnums.Skills.Zweikampf)       && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Zweikampf,       true, value, nameof(this.HasZweikampf)); }
        /// <summary>Whether the outfield player has the Speed (Schnelligkeit) skill.</summary>
        public bool HasSchnelligkeit   { get => this.Skills.HasFlag(PlayerEnums.Skills.Schnelligkeit)   && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Schnelligkeit,   true, value, nameof(this.HasSchnelligkeit)); }
        /// <summary>Whether the outfield player has the Shot Power (Schusskraft) skill.</summary>
        public bool HasSchusskraft     { get => this.Skills.HasFlag(PlayerEnums.Skills.Schusskraft)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Schusskraft,     true, value, nameof(this.HasSchusskraft)); }
        /// <summary>Whether the outfield player has the Free Kicks (Freistoesse) skill.</summary>
        public bool HasFreistoesse     { get => this.Skills.HasFlag(PlayerEnums.Skills.Freistoesse)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Freistoesse,     true, value, nameof(this.HasFreistoesse)); }
        /// <summary>Whether the outfield player has the Crossing (Flanken) skill.</summary>
        public bool HasFlanken         { get => this.Skills.HasFlag(PlayerEnums.Skills.Flanken)         && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Flanken,         true, value, nameof(this.HasFlanken)); }
        /// <summary>Whether the outfield player has the Goal Instinct (Torinstinkt) skill.</summary>
        public bool HasTorinstinkt     { get => this.Skills.HasFlag(PlayerEnums.Skills.Torinstinkt)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Torinstinkt,     true, value, nameof(this.HasTorinstinkt)); }
        /// <summary>Whether the outfield player has the Stamina (Laufstaerke) skill.</summary>
        public bool HasLaufstaerke     { get => this.Skills.HasFlag(PlayerEnums.Skills.Laufstaerke)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Laufstaerke,     true, value, nameof(this.HasLaufstaerke)); }
        /// <summary>Whether the outfield player has the Technique (Technik) skill.</summary>
        public bool HasTechnik         { get => this.Skills.HasFlag(PlayerEnums.Skills.Technik)         && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Technik,         true, value, nameof(this.HasTechnik)); }
        /// <summary>Whether the outfield player has the Playmaking (Spielmacher) skill.</summary>
        public bool HasSpielmacher     { get => this.Skills.HasFlag(PlayerEnums.Skills.Spielmacher)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Spielmacher,     true, value, nameof(this.HasSpielmacher)); }
        /// <summary>Whether the outfield player has the Two-Footedness (Beidfuessigkeit) skill.</summary>
        public bool HasBeidfuessigkeit { get => this.Skills.HasFlag(PlayerEnums.Skills.Beidfuessigkeit) && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Beidfuessigkeit, true, value, nameof(this.HasBeidfuessigkeit)); }
        /// <summary>Whether the goalkeeper has the Penalty Killer (Elfmetertoeter) skill.</summary>
        public bool HasElfmetertoeter  { get => this.Skills.HasFlag(PlayerEnums.Skills.Elfmetertoeter)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Elfmetertoeter,  true, value, nameof(this.HasElfmetertoeter)); }
        /// <summary>Whether the goalkeeper has the Strong Reflexes (StarkeReflexe) skill.</summary>
        public bool HasStarkeReflexe   { get => this.Skills.HasFlag(PlayerEnums.Skills.StarkeReflexe)   && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.StarkeReflexe,   true, value, nameof(this.HasStarkeReflexe)); }
        /// <summary>Whether the goalkeeper has the Coming Off the Line (Herauslaufen) skill.</summary>
        public bool HasHerauslaufen    { get => this.Skills.HasFlag(PlayerEnums.Skills.Herauslaufen)    && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Herauslaufen,    true, value, nameof(this.HasHerauslaufen)); }
        /// <summary>Whether the goalkeeper has the Safe Catching (Fangsicherheit) skill.</summary>
        public bool HasFangsicherheit  { get => this.Skills.HasFlag(PlayerEnums.Skills.Fangsicherheit)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Fangsicherheit,  true, value, nameof(this.HasFangsicherheit)); }
        /// <summary>Whether the goalkeeper has the Punching Clear (Fausten) skill.</summary>
        public bool HasFausten         { get => this.Skills.HasFlag(PlayerEnums.Skills.Fausten)         && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Fausten,         true, value, nameof(this.HasFausten)); }
        /// <summary>Whether the goalkeeper has the Ball Security (Ballsicherheit) skill.</summary>
        public bool HasBallsicherheit  { get => this.Skills.HasFlag(PlayerEnums.Skills.Ballsicherheit)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Ballsicherheit,  true, value, nameof(this.HasBallsicherheit)); }
        /// <summary>Whether the outfield player has the negative Heading (Kopfball) trait.</summary>
        public bool HasNegKopfball        { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Kopfball)        && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Kopfball,        false, value, nameof(this.HasNegKopfball)); }
        /// <summary>Whether the outfield player has the negative Tackling (Zweikampf) trait.</summary>
        public bool HasNegZweikampf       { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Zweikampf)       && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Zweikampf,       false, value, nameof(this.HasNegZweikampf)); }
        /// <summary>Whether the outfield player has the negative Speed (Schnelligkeit) trait.</summary>
        public bool HasNegSchnelligkeit   { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Schnelligkeit)   && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Schnelligkeit,   false, value, nameof(this.HasNegSchnelligkeit)); }
        /// <summary>Whether the outfield player has the negative Shot Power (Schusskraft) trait.</summary>
        public bool HasNegSchusskraft     { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Schusskraft)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Schusskraft,     false, value, nameof(this.HasNegSchusskraft)); }
        /// <summary>Whether the outfield player has the negative Free Kicks (Freistoesse) trait.</summary>
        public bool HasNegFreistoesse     { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Freistoesse)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Freistoesse,     false, value, nameof(this.HasNegFreistoesse)); }
        /// <summary>Whether the outfield player has the negative Crossing (Flanken) trait.</summary>
        public bool HasNegFlanken         { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Flanken)         && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Flanken,         false, value, nameof(this.HasNegFlanken)); }
        /// <summary>Whether the outfield player has the negative Goal Instinct (Torinstinkt) trait.</summary>
        public bool HasNegTorinstinkt     { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Torinstinkt)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Torinstinkt,     false, value, nameof(this.HasNegTorinstinkt)); }
        /// <summary>Whether the outfield player has the negative Stamina (Laufstaerke) trait.</summary>
        public bool HasNegLaufstaerke     { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Laufstaerke)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Laufstaerke,     false, value, nameof(this.HasNegLaufstaerke)); }
        /// <summary>Whether the outfield player has the negative Technique (Technik) trait.</summary>
        public bool HasNegTechnik         { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Technik)         && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Technik,         false, value, nameof(this.HasNegTechnik)); }
        /// <summary>Whether the outfield player has the negative Playmaking (Spielmacher) trait.</summary>
        public bool HasNegSpielmacher     { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Spielmacher)     && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Spielmacher,     false, value, nameof(this.HasNegSpielmacher)); }
        /// <summary>Whether the outfield player has the negative Two-Footedness (Beidfuessigkeit) trait.</summary>
        public bool HasNegBeidfuessigkeit { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Beidfuessigkeit) && !this.IsTO; set => this.Multiplex(PlayerEnums.Skills.Beidfuessigkeit, false, value, nameof(this.HasNegBeidfuessigkeit)); }
        /// <summary>Whether the goalkeeper has the negative Penalty Killer (Elfmetertoeter) trait.</summary>
        public bool HasNegElfmetertoeter  { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Elfmetertoeter)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Elfmetertoeter,  false, value, nameof(this.HasNegElfmetertoeter)); }
        /// <summary>Whether the goalkeeper has the negative Strong Reflexes (StarkeReflexe) trait.</summary>
        public bool HasNegStarkeReflexe   { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.StarkeReflexe)   && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.StarkeReflexe,   false, value, nameof(this.HasNegStarkeReflexe)); }
        /// <summary>Whether the goalkeeper has the negative Coming Off the Line (Herauslaufen) trait.</summary>
        public bool HasNegHerauslaufen    { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Herauslaufen)    && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Herauslaufen,    false, value, nameof(this.HasNegHerauslaufen)); }
        /// <summary>Whether the goalkeeper has the negative Safe Catching (Fangsicherheit) trait.</summary>
        public bool HasNegFangsicherheit  { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Fangsicherheit)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Fangsicherheit,  false, value, nameof(this.HasNegFangsicherheit)); }
        /// <summary>Whether the goalkeeper has the negative Punching Clear (Fausten) trait.</summary>
        public bool HasNegFausten         { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Fausten)         && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Fausten,         false, value, nameof(this.HasNegFausten)); }
        /// <summary>Whether the goalkeeper has the negative Ball Security (Ballsicherheit) trait.</summary>
        public bool HasNegBallsicherheit  { get => this.NegativeSkills.HasFlag(PlayerEnums.Skills.Ballsicherheit)  && this.IsTO;  set => this.Multiplex(PlayerEnums.Skills.Ballsicherheit,  false, value, nameof(this.HasNegBallsicherheit)); }

        /// <summary>Whether the player's main <see cref="Position"/> is Goalkeeper (TO).</summary>
        public bool IsTO { get => this.Position == PlayerEnums.Position.TO; set => this.Setter(PlayerEnums.Position.TO, value, nameof(this.IsTO)); }
        /// <summary>Whether the player's main <see cref="Position"/> is not Goalkeeper.</summary>
        public bool IsNotTO => !this.IsTO;
        /// <summary>Whether the player's main <see cref="Position"/> is Libero (L).</summary>
        public bool IsL  { get => this.Position == PlayerEnums.Position.L;  set => this.Setter(PlayerEnums.Position.L,  value, nameof(this.IsL)); }
        /// <summary>Whether the player's main <see cref="Position"/> is Center Back (MD, Manndecker).</summary>
        public bool IsMD { get => this.Position == PlayerEnums.Position.MD; set => this.Setter(PlayerEnums.Position.MD, value, nameof(this.IsMD)); }
        /// <summary>Whether the player's main <see cref="Position"/> is Left Back (LV, Linksverteidiger).</summary>
        public bool IsLV { get => this.Position == PlayerEnums.Position.LV; set => this.Setter(PlayerEnums.Position.LV, value, nameof(this.IsLV)); }
        /// <summary>Whether the player's main <see cref="Position"/> is Right Back (RV, Rechtsverteidiger).</summary>
        public bool IsRV { get => this.Position == PlayerEnums.Position.RV; set => this.Setter(PlayerEnums.Position.RV, value, nameof(this.IsRV)); }
        /// <summary>Whether the player's main <see cref="Position"/> is Right Midfield (RM).</summary>
        public bool IsRM { get => this.Position == PlayerEnums.Position.RM; set => this.Setter(PlayerEnums.Position.RM, value, nameof(this.IsRM)); }
        /// <summary>Whether the player's main <see cref="Position"/> is Left Midfield (LM).</summary>
        public bool IsLM { get => this.Position == PlayerEnums.Position.LM; set => this.Setter(PlayerEnums.Position.LM, value, nameof(this.IsLM)); }
        /// <summary>Whether the player's main <see cref="Position"/> is Defensive Midfield (DM).</summary>
        public bool IsDM { get => this.Position == PlayerEnums.Position.DM; set => this.Setter(PlayerEnums.Position.DM, value, nameof(this.IsDM)); }
        /// <summary>Whether the player's main <see cref="Position"/> is Attacking Midfield (OM, Offensives Mittelfeld).</summary>
        public bool IsOM { get => this.Position == PlayerEnums.Position.OM; set => this.Setter(PlayerEnums.Position.OM, value, nameof(this.IsOM)); }
        /// <summary>Whether the player's main <see cref="Position"/> is Striker (S, Stuermer).</summary>
        public bool IsS  { get => this.Position == PlayerEnums.Position.S;  set => this.Setter(PlayerEnums.Position.S,  value, nameof(this.IsS)); }
        /// <summary>Whether Goalkeeper (TO) is one of the player's <see cref="SecondaryPositions"/>.</summary>
        public bool IsSecondaryTO { get => this.SecondaryPositions.Contains(PlayerEnums.Position.TO); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.TO); else this.RemoveSecondaryPosition(PlayerEnums.Position.TO); } }
        /// <summary>Whether Libero (L) is one of the player's <see cref="SecondaryPositions"/>.</summary>
        public bool IsSecondaryL  { get => this.SecondaryPositions.Contains(PlayerEnums.Position.L);  set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.L);  else this.RemoveSecondaryPosition(PlayerEnums.Position.L);  } }
        /// <summary>Whether Center Back (MD) is one of the player's <see cref="SecondaryPositions"/>.</summary>
        public bool IsSecondaryMD { get => this.SecondaryPositions.Contains(PlayerEnums.Position.MD); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.MD); else this.RemoveSecondaryPosition(PlayerEnums.Position.MD); } }
        /// <summary>Whether Left Back (LV) is one of the player's <see cref="SecondaryPositions"/>.</summary>
        public bool IsSecondaryLV { get => this.SecondaryPositions.Contains(PlayerEnums.Position.LV); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.LV); else this.RemoveSecondaryPosition(PlayerEnums.Position.LV); } }
        /// <summary>Whether Right Back (RV) is one of the player's <see cref="SecondaryPositions"/>.</summary>
        public bool IsSecondaryRV { get => this.SecondaryPositions.Contains(PlayerEnums.Position.RV); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.RV); else this.RemoveSecondaryPosition(PlayerEnums.Position.RV); } }
        /// <summary>Whether Right Midfield (RM) is one of the player's <see cref="SecondaryPositions"/>.</summary>
        public bool IsSecondaryRM { get => this.SecondaryPositions.Contains(PlayerEnums.Position.RM); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.RM); else this.RemoveSecondaryPosition(PlayerEnums.Position.RM); } }
        /// <summary>Whether Left Midfield (LM) is one of the player's <see cref="SecondaryPositions"/>.</summary>
        public bool IsSecondaryLM { get => this.SecondaryPositions.Contains(PlayerEnums.Position.LM); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.LM); else this.RemoveSecondaryPosition(PlayerEnums.Position.LM); } }
        /// <summary>Whether Defensive Midfield (DM) is one of the player's <see cref="SecondaryPositions"/>.</summary>
        public bool IsSecondaryDM { get => this.SecondaryPositions.Contains(PlayerEnums.Position.DM); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.DM); else this.RemoveSecondaryPosition(PlayerEnums.Position.DM); } }
        /// <summary>Whether Attacking Midfield (OM) is one of the player's <see cref="SecondaryPositions"/>.</summary>
        public bool IsSecondaryOM { get => this.SecondaryPositions.Contains(PlayerEnums.Position.OM); set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.OM); else this.RemoveSecondaryPosition(PlayerEnums.Position.OM); } }
        /// <summary>Whether Striker (S) is one of the player's <see cref="SecondaryPositions"/>.</summary>
        public bool IsSecondaryS  { get => this.SecondaryPositions.Contains(PlayerEnums.Position.S);  set { if (value) this.AddSecondaryPosition(PlayerEnums.Position.S);  else this.RemoveSecondaryPosition(PlayerEnums.Position.S);  } }

        /// <summary>Whether the player's <see cref="Character"/> is Normal.</summary>
        public bool IsNormalChar     { get => this.Character == PlayerEnums.Character.Normal;         set => this.Setter(PlayerEnums.Character.Normal, value, nameof(this.IsNormalChar)); }
        /// <summary>Whether the player's <see cref="Character"/> is Hothead (Hitzkopf).</summary>
        public bool IsHitzkopf       { get => this.Character == PlayerEnums.Character.Hitzkopf;       set => this.Setter(PlayerEnums.Character.Hitzkopf, value, nameof(this.IsHitzkopf)); }
        /// <summary>Whether the player's <see cref="Character"/> is Cheerful (Frohnatur).</summary>
        public bool IsFrohnatur      { get => this.Character == PlayerEnums.Character.Frohnatur;      set => this.Setter(PlayerEnums.Character.Frohnatur, value, nameof(this.IsFrohnatur)); }
        /// <summary>Whether the player's <see cref="Character"/> is Nerves of Steel (MannOhneNerven).</summary>
        public bool IsMannOhneNerven { get => this.Character == PlayerEnums.Character.MannOhneNerven; set => this.Setter(PlayerEnums.Character.MannOhneNerven, value, nameof(this.IsMannOhneNerven)); }
        /// <summary>Whether the player's <see cref="Character"/> is Bundle of Nerves (Nervenbuendel).</summary>
        public bool IsNervenbuendel  { get => this.Character == PlayerEnums.Character.Nervenbuendel;  set => this.Setter(PlayerEnums.Character.Nervenbuendel, value, nameof(this.IsNervenbuendel)); }
        /// <summary>Whether the player's <see cref="Character"/> is Phlegmatic (Phlegma).</summary>
        public bool IsPhlegma        { get => this.Character == PlayerEnums.Character.Phlegma;        set => this.Setter(PlayerEnums.Character.Phlegma, value, nameof(this.IsPhlegma)); }
        /// <summary>Whether the player's <see cref="Character"/> is Money-Grubber (Geldgeier).</summary>
        public bool IsGeldgeier      { get => this.Character == PlayerEnums.Character.Geldgeier;      set => this.Setter(PlayerEnums.Character.Geldgeier, value, nameof(this.IsGeldgeier)); }
        /// <summary>Whether the player's <see cref="Character"/> is Model Professional (Musterprofi).</summary>
        public bool IsMusterprofi    { get => this.Character == PlayerEnums.Character.Musterprofi;    set => this.Setter(PlayerEnums.Character.Musterprofi, value, nameof(this.IsMusterprofi)); }
        /// <summary>Whether the player's <see cref="Character"/> is Scandal-Prone (Skandalnudel).</summary>
        public bool IsSkandalnudel   { get => this.Character == PlayerEnums.Character.Skandalnudel;   set => this.Setter(PlayerEnums.Character.Skandalnudel, value, nameof(this.IsSkandalnudel)); }
        /// <summary>Whether the player's <see cref="Health"/> trait is Normal.</summary>
        public bool HasNormalHealth { get => this.Health == PlayerEnums.Health.Normal;        set => this.Setter(PlayerEnums.Health.Normal, value, nameof(this.HasNormalHealth)); }
        /// <summary>Whether the player's <see cref="Health"/> trait is Robust (Robustheit).</summary>
        public bool IsRobust        { get => this.Health == PlayerEnums.Health.Robustheit;    set => this.Setter(PlayerEnums.Health.Robustheit, value, nameof(this.IsRobust)); }
        /// <summary>Whether the player's <see cref="Health"/> trait is Injury-Prone (Anfaelligkeit).</summary>
        public bool IsAnfaellig     { get => this.Health == PlayerEnums.Health.Anfaelligkeit; set => this.Setter(PlayerEnums.Health.Anfaelligkeit, value, nameof(this.IsAnfaellig)); }
        /// <summary>Whether the player's <see cref="Health"/> trait is Knee Problems (Knieprobleme).</summary>
        public bool HasKnieprobleme { get => this.Health == PlayerEnums.Health.Knieprobleme;  set => this.Setter(PlayerEnums.Health.Knieprobleme, value, nameof(this.HasKnieprobleme)); }
        /// <summary>Whether the player has no <see cref="Personality"/> trait set.</summary>
        public bool IsNoone                { get => this.Personality == PlayerEnums.Personality.None;                 set => this.Setter(PlayerEnums.Personality.None, value, nameof(this.IsNoone)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Leader (Fuehrungsperson).</summary>
        public bool IsFuehrungsperson      { get => this.Personality == PlayerEnums.Personality.Fuehrungsperson;      set => this.Setter(PlayerEnums.Personality.Fuehrungsperson, value, nameof(this.IsFuehrungsperson)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Fighter (Kaempfernatur).</summary>
        public bool IsKaempfernatur        { get => this.Personality == PlayerEnums.Personality.Kaempfernatur;        set => this.Setter(PlayerEnums.Personality.Kaempfernatur, value, nameof(this.IsKaempfernatur)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Training World Champion (Trainingsweltmeister).</summary>
        public bool IsTrainingsweltmeister { get => this.Personality == PlayerEnums.Personality.Trainingsweltmeister; set => this.Setter(PlayerEnums.Personality.Trainingsweltmeister, value, nameof(this.IsTrainingsweltmeister)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Training-Averse (Trainingsfaul).</summary>
        public bool IsTrainingsfaul        { get => this.Personality == PlayerEnums.Personality.Trainingsfaul;        set => this.Setter(PlayerEnums.Personality.Trainingsfaul, value, nameof(this.IsTrainingsfaul)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Diver/Kicker (Treter).</summary>
        public bool IsTreter               { get => this.Personality == PlayerEnums.Personality.Treter;               set => this.Setter(PlayerEnums.Personality.Treter, value, nameof(this.IsTreter)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Fair Player.</summary>
        public bool IsFairPlayer           { get => this.Personality == PlayerEnums.Personality.FairPlayer;           set => this.Setter(PlayerEnums.Personality.FairPlayer, value, nameof(this.IsFairPlayer)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Dive King (Schwalbenkoenig).</summary>
        public bool IsSchwalbenkoenig      { get => this.Personality == PlayerEnums.Personality.Schwalbenkoenig;      set => this.Setter(PlayerEnums.Personality.Schwalbenkoenig, value, nameof(this.IsSchwalbenkoenig)); }
        /// <summary>Whether the player's <see cref="Personality"/> is All-Rounder (Allrounder).</summary>
        public bool IsAllrounder           { get => this.Personality == PlayerEnums.Personality.Allrounder;           set => this.Setter(PlayerEnums.Personality.Allrounder, value, nameof(this.IsAllrounder)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Flexible Player.</summary>
        public bool IsFlexiblePlayer       { get => this.Personality == PlayerEnums.Personality.FlexiblePlayer;       set => this.Setter(PlayerEnums.Personality.FlexiblePlayer, value, nameof(this.IsFlexiblePlayer)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Home-Game Player (Heimspieler).</summary>
        public bool IsHeimspieler          { get => this.Personality == PlayerEnums.Personality.Heimspieler;          set => this.Setter(PlayerEnums.Personality.Heimspieler, value, nameof(this.IsHeimspieler)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Away-Game Player (AuswaertsSpieler).</summary>
        public bool IsAuswaertsspieler     { get => this.Personality == PlayerEnums.Personality.AuswaertsSpieler;     set => this.Setter(PlayerEnums.Personality.AuswaertsSpieler, value, nameof(this.IsAuswaertsspieler)); }
        /// <summary>Whether the player's <see cref="Personality"/> is Talent.</summary>
        public bool IsTalent               { get => this.Personality == PlayerEnums.Personality.Talent;               set => this.Setter(PlayerEnums.Personality.Talent, value, nameof(this.IsTalent)); }
        /// <summary>Display label for the <see cref="IsTalent"/> trait, distinguishing "eternal talent" for players 25 or older.</summary>
        public string TalentLabel          { get => this.Age >= 25 ? "ewiges Talent" : "" + "Talent"; private set { } }

        /// <summary>Whether the player's <see cref="Happy"/> mood flags include "great contract" (TollerVertrag).</summary>
        public bool IsTollerVertrag        { get => this.Happy.HasFlag(PlayerEnums.Happy.TollerVertrag);        set => this.Multiplex(PlayerEnums.Happy.TollerVertrag, value, nameof(this.IsTollerVertrag)); }
        /// <summary>Whether the player's <see cref="Happy"/> mood flags include "great mood" (TolleStimmung).</summary>
        public bool IsTolleStimmung        { get => this.Happy.HasFlag(PlayerEnums.Happy.TolleStimmung);        set => this.Multiplex(PlayerEnums.Happy.TolleStimmung, value, nameof(this.IsTolleStimmung)); }
        /// <summary>Whether the player's <see cref="Happy"/> mood flags include "great bonuses" (TollePraemien).</summary>
        public bool IsTollePraemien        { get => this.Happy.HasFlag(PlayerEnums.Happy.TollePraemien);        set => this.Multiplex(PlayerEnums.Happy.TollePraemien, value, nameof(this.IsTollePraemien)); }
        /// <summary>Whether the player's <see cref="Happy"/> mood flags include "double great contract" (TollerVertragDoppelt).</summary>
        public bool IsTollerVertragDoppelt { get => this.Happy.HasFlag(PlayerEnums.Happy.TollerVertragDoppelt); set => this.Multiplex(PlayerEnums.Happy.TollerVertragDoppelt, value, nameof(this.IsTollerVertragDoppelt)); }
        /// <summary>Whether the player's <see cref="Happy"/> mood flags include "great trainer" (TollerTrainer).</summary>
        public bool IsTollerTrainer        { get => this.Happy.HasFlag(PlayerEnums.Happy.TollerTrainer);        set => this.Multiplex(PlayerEnums.Happy.TollerTrainer, value, nameof(this.IsTollerTrainer)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "wants to finally play" (WillEndlichSpielen).</summary>
        public bool IsWillEndlichSpielen    { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.WillEndlichSpielen);    set => this.Multiplex(PlayerEnums.Unhappy.WillEndlichSpielen, value, nameof(this.IsWillEndlichSpielen)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "wants more money" (WillMehrGeld).</summary>
        public bool IsWillMehrGeld          { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.WillMehrGeld);          set => this.Multiplex(PlayerEnums.Unhappy.WillMehrGeld, value, nameof(this.IsWillMehrGeld)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "poor bonuses" (MiesePraemien).</summary>
        public bool IsMiesePraemien         { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.MiesePraemien);         set => this.Multiplex(PlayerEnums.Unhappy.MiesePraemien, value, nameof(this.IsMiesePraemien)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "not based on performance" (GehtNichtNachLeistung).</summary>
        public bool IsGehtNichtNachLeistung { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.GehtNichtNachLeistung); set => this.Multiplex(PlayerEnums.Unhappy.GehtNichtNachLeistung, value, nameof(this.IsGehtNichtNachLeistung)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "wrong position" (FalschePosition).</summary>
        public bool IsFalschePosition       { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.FalschePosition);       set => this.Multiplex(PlayerEnums.Unhappy.FalschePosition, value, nameof(this.IsFalschePosition)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "lousy contract" (ScheissVertrag).</summary>
        public bool IsScheissVertrag        { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.ScheissVertrag);        set => this.Multiplex(PlayerEnums.Unhappy.ScheissVertrag, value, nameof(this.IsScheissVertrag)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "poor team" (MieseMannschaft).</summary>
        public bool IsMieseMannschaft       { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.MieseMannschaft);       set => this.Multiplex(PlayerEnums.Unhappy.MieseMannschaft, value, nameof(this.IsMieseMannschaft)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "bad mood" (SchlechteStimmung).</summary>
        public bool IsSchlechteStimmung     { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.SchlechteStimmung);     set => this.Multiplex(PlayerEnums.Unhappy.SchlechteStimmung, value, nameof(this.IsSchlechteStimmung)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "injured and never visited" (VerletztUndNieBesucht).</summary>
        public bool IsVerletztUndNieBesucht { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.VerletztUndNieBesucht); set => this.Multiplex(PlayerEnums.Unhappy.VerletztUndNieBesucht, value, nameof(this.IsVerletztUndNieBesucht)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "unfair treatment" (UngerechteBehandlung).</summary>
        public bool IsUngerechteBehandlung  { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.UngerechteBehandlung);  set => this.Multiplex(PlayerEnums.Unhappy.UngerechteBehandlung, value, nameof(this.IsUngerechteBehandlung)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "stupid fans" (BloedeFans).</summary>
        public bool IsBloedeFans            { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.BloedeFans);            set => this.Multiplex(PlayerEnums.Unhappy.BloedeFans, value, nameof(this.IsBloedeFans)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "stupid teammates" (BloedeMitspieler).</summary>
        public bool IsBloedeMitspieler      { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.BloedeMitspieler);      set => this.Multiplex(PlayerEnums.Unhappy.BloedeMitspieler, value, nameof(this.IsBloedeMitspieler)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "lousy trainer" (ScheissTrainer).</summary>
        public bool IsScheissTrainer        { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.ScheissTrainer);        set => this.Multiplex(PlayerEnums.Unhappy.ScheissTrainer, value, nameof(this.IsScheissTrainer)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "negotiation fell through" (VerhandlungGeplatzt).</summary>
        public bool IsVerhandlungGeplatzt   { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.VerhandlungGeplatzt);   set => this.Multiplex(PlayerEnums.Unhappy.ScheissTrainer, value, nameof(this.IsVerhandlungGeplatzt)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "lousy manager" (ScheissManager).</summary>
        public bool IsScheissManager        { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.ScheissManager);        set => this.Multiplex(PlayerEnums.Unhappy.ScheissTrainer, value, nameof(this.IsScheissManager)); }
        /// <summary>Whether the player's <see cref="Unhappy"/> mood flags include "lousy youth round" (ScheissNachwuchsrunde).</summary>
        public bool IsScheissNachwuchsrunde { get => this.Unhappy.HasFlag(PlayerEnums.Unhappy.ScheissNachwuchsrunde); set => this.Multiplex(PlayerEnums.Unhappy.ScheissNachwuchsrunde, value, nameof(this.IsScheissNachwuchsrunde)); }

        /// <summary>Whether the player's <see cref="ContractDetails"/> flags include Leased (on loan).</summary>
        public bool IsLeased { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.Leased); set => this.Multiplex(PlayerEnums.Contract.Leased, value, nameof(this.IsLeased)); }
        /// <summary>Whether the player's <see cref="ContractDetails"/> flags include a buy option.</summary>
        public bool HasBuyOption { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.BuyOption); set => this.Multiplex(PlayerEnums.Contract.BuyOption, value, nameof(this.HasBuyOption)); }
        /// <summary>Whether the player's <see cref="ContractDetails"/> flags include the unmapped Unknown bit.</summary>
        public bool IsUnknownContractDetail { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.Unknown); set => this.Multiplex(PlayerEnums.Contract.Unknown, value, nameof(this.IsUnknownContractDetail)); }
        /// <summary>Whether the player's <see cref="ContractDetails"/> flags include "joined this season".</summary>
        public bool IsJoinedThisSeason { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.JoinedThisSeason); set => this.Multiplex(PlayerEnums.Contract.JoinedThisSeason, value, nameof(this.IsJoinedThisSeason)); }
        /// <summary>Whether the player's <see cref="ContractDetails"/> flags include an option held by the player.</summary>
        public bool HasOptionPlayer { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.OptionPlayer); set => this.Multiplex(PlayerEnums.Contract.OptionPlayer, value, nameof(this.HasOptionPlayer)); }
        /// <summary>Whether the player's <see cref="ContractDetails"/> flags include an option held by the club.</summary>
        public bool HasOptionClub { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.OptionClub); set => this.Multiplex(PlayerEnums.Contract.OptionClub, value, nameof(this.HasOptionClub)); }
        /// <summary>Whether the player's <see cref="ContractDetails"/> flags include a seated (guaranteed) contract.</summary>
        public bool IsSeatedGuarantee { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.Seated); set => this.Multiplex(PlayerEnums.Contract.Seated, value, nameof(this.IsSeatedGuarantee)); }
        /// <summary>Whether the player's <see cref="ContractDetails"/> flags include the Unset bit.</summary>
        public bool IsUnsetContractDetail { get => this.ContractDetails.HasFlag(PlayerEnums.Contract.Unset); set => this.Multiplex(PlayerEnums.Contract.Unset, value, nameof(this.IsUnsetContractDetail)); }
        /// <summary>Whether the player's <see cref="Career"/> flags include Retires.</summary>
        public bool IsRetires { get => this.Career.HasFlag(PlayerEnums.Career.Retires); set => this.Multiplex(PlayerEnums.Career.Retires, value, nameof(this.IsRetires)); }
        #endregion

        #region Overview
        /// <summary>Player's first name, truncated to the 9-byte field width the game allows.</summary>
        public string Firstname { get => this.firstname; set { if (Tools.LimitedStringEquals(value, this.firstname, 9)) {
                    this.firstname = value != null && value.Length > 9 ? value.Substring(0, 9) : value; this.OnPropertyChanged(nameof(this.Firstname));
                } } }
        private string firstname = string.Empty;
        /// <summary>Player's last name, truncated to the 15-byte field width the game allows.</summary>
        public string Lastname { get => this.lastname; set { if (Tools.LimitedStringEquals(value, this.lastname, 15)) {
                    this.lastname = value != null && value.Length > 15 ? value.Substring(0, 15) : value; this.OnPropertyChanged(nameof(this.Lastname));
                } } }
        private string lastname = string.Empty;
        /// <summary>Player's skin color.</summary>
        public PlayerEnums.SkinColor SkinColor { get => this.skinColor; set => this.Setter(value); }
        private PlayerEnums.SkinColor skinColor = 0;
        /// <summary>Player's hair color.</summary>
        public PlayerEnums.HairColor HairColor { get => this.hairColor; set => this.Setter(value); }
        private PlayerEnums.HairColor hairColor = 0;
        /// <summary>Player's age in years.</summary>
        public byte Age { get => this.age; set { this.age = value; this.OnPropertyChanged(nameof(this.Age)); } }
        private byte age = 0;
        /// <summary>Player's overall skill level.</summary>
        public byte Level { get => this.level; set { this.level = value; this.OnPropertyChanged(nameof(this.Level)); } }
        private byte level = 0;
        /// <summary>Player's current form rating.</summary>
        public byte Form { get => this.form; set { this.form = value; this.OnPropertyChanged(nameof(this.Form)); } }
        private byte form = 0;
        /// <summary>Player's current fitness condition.</summary>
        public byte Condition { get => this.condition; set { this.condition = value; this.OnPropertyChanged(nameof(this.Condition)); } }
        private byte condition = 0;
        /// <summary>Player's current freshness rating.</summary>
        public byte Freshness { get => this.freshness; set { this.freshness = value; this.OnPropertyChanged(nameof(this.Freshness)); } }
        byte freshness = 0;
        /// <summary>Player's nationality.</summary>
        public PlayerEnums.Country Nationality { get => this.nationality; set => this.Setter(value); }
        private PlayerEnums.Country nationality = PlayerEnums.Country.Sonstige;
        #endregion

        #region Position
        /// <summary>Player's main playing position.</summary>
        public PlayerEnums.Position Position { get => this.position; set => this.Setter(value); }
        private PlayerEnums.Position position = PlayerEnums.Position.None;
        /// <summary>Player's up-to-two secondary playing positions.</summary>
        public List<PlayerEnums.Position> SecondaryPositions { get => this.secondaryPositions; set => this.Setter(value); }
        private List<PlayerEnums.Position> secondaryPositions;
        #endregion

        #region Skills
        /// <summary>Bitmask of the player's positive skills.</summary>
        public PlayerEnums.Skills Skills { get => this.skills; set => this.Setter(value, true); }
        private PlayerEnums.Skills skills = PlayerEnums.Skills.None;
        /// <summary>Bitmask of the player's negative skills/weaknesses.</summary>
        public PlayerEnums.Skills NegativeSkills { get => this.negativeSkills; set => this.Setter(value, false); }
        private PlayerEnums.Skills negativeSkills = PlayerEnums.Skills.None;
        #endregion

        #region Character
        /// <summary>Player's personality trait.</summary>
        public PlayerEnums.Personality Personality { get => this.personality; set => this.Setter(value); }
        private PlayerEnums.Personality personality = PlayerEnums.Personality.None;
        /// <summary>Player's character trait.</summary>
        public PlayerEnums.Character Character { get => this.character; set => this.Setter(value); }
        private PlayerEnums.Character character = PlayerEnums.Character.Normal;
        /// <summary>Player's health/injury-proneness trait.</summary>
        public PlayerEnums.Health Health { get => this.health; set => this.Setter(value); }
        private PlayerEnums.Health health = PlayerEnums.Health.Normal;
        #endregion

        #region Constitution
        /// <summary>Bitmask of the player's negative mood factors.</summary>
        public PlayerEnums.Unhappy Unhappy { get => this.unhappy; set => this.Setter(value); }
        private PlayerEnums.Unhappy unhappy = PlayerEnums.Unhappy.None;
        /// <summary>Bitmask of the player's positive mood factors.</summary>
        public PlayerEnums.Happy Happy { get => this.happy; set => this.Setter(value); }
        private PlayerEnums.Happy happy = PlayerEnums.Happy.None;
        /// <summary>Type/severity of the player's current injury.</summary>
        public byte Injury { get => this.injury; set { this.injury = value; this.OnPropertyChanged(nameof(this.Injury)); } }
        private byte injury = 0;
        /// <summary>Number of days remaining on the player's current injury.</summary>
        public ushort InjuredDays { get => this.injuredDays; set { this.injuredDays = value; this.OnPropertyChanged(nameof(this.InjuredDays)); } }
        private ushort injuredDays = 0;
        /// <summary>Whether the player is currently vulnerable to injury.</summary>
        public bool Vulnerable { get => this.vulnerable; set { this.vulnerable = value; this.OnPropertyChanged(nameof(this.Vulnerable)); } }
        private bool vulnerable = false;
        /// <summary>Number of matches the player is banned for due to a red card.</summary>
        public byte RedCardBannedMatches { get => this.redCardBannedMatches; set { this.redCardBannedMatches = value; this.OnPropertyChanged(nameof(this.RedCardBannedMatches)); } }
        private byte redCardBannedMatches = 0;
        /// <summary>Whether the player is currently doped.</summary>
        public bool Doped { get => this.doped; set { this.doped = value; this.OnPropertyChanged(nameof(this.Doped)); } }
        private bool doped = false;
        /// <summary>Number of yellow cards the player has received this season.</summary>
        public byte YellowCardsSeason { get => this.yellowCardsSeason; set { this.yellowCardsSeason = value; this.OnPropertyChanged(nameof(this.YellowCardsSeason)); } }
        private byte yellowCardsSeason = 0;
        #endregion

        #region Contract
        /// <summary>Player's salary.</summary>
        public ushort Salary { get => this.salary; set { this.salary = value; this.OnPropertyChanged(nameof(this.Salary)); } }
        private ushort salary = 0;
        /// <summary>Player's per-appearance show-up bonus.</summary>
        public ushort ShowUpBonus { get => this.showUpBonus; set { this.showUpBonus = value; this.OnPropertyChanged(nameof(this.ShowUpBonus)); } }
        private ushort showUpBonus = 0;
        /// <summary>Player's per-goal bonus.</summary>
        public ushort GoalsBonus { get => this.goalsBonus; set { this.goalsBonus = value; this.OnPropertyChanged(nameof(this.GoalsBonus)); } }
        private ushort goalsBonus = 0;
        /// <summary>Player's transfer fee.</summary>
        public ushort TransferFee { get => this.transferFee; set { this.transferFee = value; this.OnPropertyChanged(nameof(this.TransferFee)); } }
        private ushort transferFee = 0;
        /// <summary>Remaining duration of the player's contract.</summary>
        public byte ContractDuration { get => this.contractDuration; set { this.contractDuration = value; this.OnPropertyChanged(nameof(this.ContractDuration)); } }
        private byte contractDuration = 0;
        /// <summary>Bitmask of the player's contract details (loan/buy-option/etc.).</summary>
        public PlayerEnums.Contract ContractDetails { get => this.contractDetails; set => this.Setter(value); }
        private PlayerEnums.Contract contractDetails = PlayerEnums.Contract.None;
        /// <summary>Number of years the player has been with their current club.</summary>
        public byte YearsInClub { get => this.yearsInClub; set { this.yearsInClub = value; this.OnPropertyChanged(nameof(this.YearsInClub)); } }
        private byte yearsInClub = 0;
        /// <summary>Bitmask of the player's career milestones.</summary>
        public PlayerEnums.Career Career { get => this.career; set => this.Setter(value); }
        private PlayerEnums.Career career = PlayerEnums.Career.None;
        #endregion

        #region NotImplemented
        /*
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

        /// <summary>Creates a player with its secondary-position slots initialized to empty.</summary>
        public Player() : base()
        {
            this.SecondaryPositions = new List<PlayerEnums.Position> { PlayerEnums.Position.None, PlayerEnums.Position.None };
        }

        /// <summary>Raises PropertyChanged for every sibling IsX/HasX helper property in the given list.</summary>
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
            else if (this.skinColor == value)
            {
                // Unchecking the currently-selected option clears back to the neutral default,
                // rather than leaving the (now supposedly "false") color in place.
                this.skinColor = PlayerEnums.SkinColor.Fair;
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
            else if (this.hairColor == value)
            {
                this.hairColor = PlayerEnums.HairColor.Hellblond;
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
            else if (this.position == pos)
            {
                this.position = PlayerEnums.Position.None;
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
            else if (this.character == character)
            {
                this.character = PlayerEnums.Character.Normal;
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
            else if (this.health == health)
            {
                this.health = PlayerEnums.Health.Normal;
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
            else if (this.personality == personality)
            {
                this.personality = PlayerEnums.Personality.None;
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
        /// <summary>Adds a secondary position, keeping only the 2 most recently added (the game supports at most two).</summary>
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

        /// <summary>Returns the player's "Lastname, Firstname" display string.</summary>
        public override string ToString()
        {
            return this.Lastname + ", " + this.Firstname;
        }
    }

}
