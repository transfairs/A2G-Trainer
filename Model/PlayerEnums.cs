using System;
using System.ComponentModel;

namespace A2G_Trainer.Model
{
    /// <summary>
    /// Enum groups for player memory addresses, plus the enums modelling every player-facing
    /// selectable trait (position, skills, character, ...) and their in-game bitmask/byte encodings.
    /// </summary>
    public static class PlayerEnums
    {
        /// <summary>Returns an enum value's [Description] text, falling back to its name.</summary>
        public static string GetDescription(Enum value)
        {
            var f = value.GetType().GetField(value.ToString());
            var attributes = (DescriptionAttribute[])f.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : value.ToString();
        }

        /// <summary>Which roster/lookup context a set of player addresses applies to.</summary>
        public enum AddressType
        {
            OWN,
            OPPONENT,
            ALL,
            /// <summary>Any club's squad, listed from the records.</summary>
            CLUB,
            NON_PLAYABLE,
            /// <summary>A club's youth players, listed from the records.</summary>
            TRAINEE
        }

        /// <summary>Identifies which player field an address entry resolves.</summary>
        public enum AddressKey
        {
            ID,
            AGE,
            FIRSTNAME,
            LASTNAME,
            LEVEL,
            FORM,
            JERSEY,
            SKIN,
            HAIR,
            CONDITION,
            FRESHNESS,
            NATIONALITY,
            POSITION,
            SECONDARY_1,
            SECONDARY_2,
            SKILLS,
            NEG_SKILLS,
            PERSONALITY,
            CHARACTER,
            HEALTH,
            UNHAPPY,
            HAPPY,
            INJURED_DAYS,
            INJURY,
            VULNERABLE,
            BANNED_MATCHES,
            /// <summary>League ban: matches left (bits 0-4) and flags, AK +0x4D.</summary>
            LEAGUE_BAN,
            /// <summary>Reason of the league ban (bit 7 red card), AK +0x4E.</summary>
            LEAGUE_BAN_REASON,
            /// <summary>Season day the league ban ends (UInt16), AK +0x16C.</summary>
            LEAGUE_BAN_END,
            DOPED,
            YELLOW_CARDS,
            SALARY,
            SHOWUP,
            GOALS_BONUS,
            TRANSFER_FEE,
            BUY_PRICE,
            HOME_COUNTRY,
            HOME_CLUB,
            CONTRACT_DURATION,
            CONTRACT_DETAILS,
            YEARS_IN_CLUB,
            CAREER,
            CLUB_ID,
            CLUB_COUNTRY,
            RED_CARDS,
            YELLOW_RED_CARDS,
            GOALS,
            OWN_GOALS,
            ASSISTS,
            JOKER_GOALS,
            APPEARANCES,
            SUBBED_IN,
            SUBBED_OUT,
            UPGRADE_GOALS,
            CAPS
        }

        /// <summary>Player character trait (single selection, bit-packed but mutually exclusive in practice).</summary>
        public enum Character : byte
        {
            Normal         = 0,
            Hitzkopf       = 1 << 0,
            Frohnatur      = 1 << 1,
            MannOhneNerven = 1 << 2,
            Nervenbuendel  = 1 << 3,
            Phlegma        = 1 << 4,
            Geldgeier      = 1 << 5,
            Musterprofi    = 1 << 6,
            Skandalnudel   = 1 << 7
        }
        /// <summary>
        /// Career byte (record +0xAC, working copy +0xD7). Bit 3: retired from the national team
        /// (FUN_005d0700; every nomination skips him). Bit 4: on the transfer list - the list itself is
        /// global (Settings.TransferListOffset), this bit mirrors it. Bit 7: ends his career at season end.
        /// </summary>
        public enum Career : byte
        {
            None = 0,
            Unknown0 = 1 << 0,
            Unknown1 = 1 << 1,
            Unknown2 = 1 << 2,
            NationalTeamRetired = 1 << 3,
            TransferListed = 1 << 4,
            Unknown5 = 1 << 5,
            Unknown6 = 1 << 6,
            Retires  = 1 << 7
        }
        /// <summary>
        /// Contract-status bitmask (loan/buy-option/etc.). Lending a player (FUN_005fb710) sets
        /// Leased, plus BuyOption for a loan with a buy option. At the season end FUN_00427ad0 decides
        /// for every loan with a buy option whether it's taken up (FUN_004278f0 clears bits 0-2) or the
        /// player goes back - except when BuyOptionOnHold is set, which skips that decision. No code
        /// in the game sets BuyOptionOnHold, and none reads Unused at all.
        /// </summary>
        public enum Contract : byte
        {
            None             = 0,
            Leased           = 1 << 0,
            BuyOption        = 1 << 1,
            BuyOptionOnHold  = 1 << 2,
            JoinedThisSeason = 1 << 3,
            OptionPlayer     = 1 << 4,
            OptionClub       = 1 << 5,
            Seated           = 1 << 6,
            Unused           = 1 << 7
        }
        /// <summary>Nationality/club country, single-byte encoded (see Settings.playerRecordNationalityOffset for the persistent-record quirk).</summary>
        public enum Country : byte
        {
            Sonstige        =  0,
            [Description("Ägypten")]
            Aegypten        =  1,
            Albanien        =  2,
            Algerien        =  3,
            Argentinien     =  4,
            Australien      =  5,
            Belgien         =  6,
            Bolivien        =  7,
            Brasilien       =  8,
            Bulgarien       =  9,
            Chile           = 10,
            China           = 11,
            [Description("Costa Rica")]
            CostaRica       = 12,
            Dänemark        = 13,
            Deutschland     = 14,
            Ecuador         = 15,
            England         = 16,
            Estland         = 17,
            [Description("Färörer Inseln")]
            FaeroererInseln = 18,
            Finnland        = 19,
            Frankreich      = 20,
            Georgien        = 21,
            Ghana           = 22,
            Griechenland    = 23,
            Holland         = 24,
            Irland          = 25,
            Island          = 26,
            Israel          = 27,
            Italien         = 28,
            Jamaika         = 29,
            Japan           = 30,
            Kamerun         = 31,
            Kanada          = 32,
            Kolumbien       = 33,
            Kroatien        = 34,
            Lettland        = 35,
            Liberia         = 36,
            Litauen         = 37,
            Luxemburg       = 38,
            Malta           = 39,
            Marokko         = 40,
            Mexiko          = 41,
            Moldavien       = 42,
            Neuseeland      = 43,
            Nigeria         = 44,
            Nordirland      = 45,
            Norwegen        = 46,
            [Description("Österreich")]
            Oesterreich     = 47,
            Paraguay        = 48,
            Peru            = 49,
            Polen           = 50,
            Portugal        = 51,
            [Description("Puerto Rico")]
            PuertoRico      = 52,
            [Description("Rumänien")]
            Rumaenien       = 53,
            Russland        = 54,
            [Description("San Marino")]
            SanMarino       = 55,
            [Description("Saudi Arabien")]
            SaudiArabien    = 56,
            Schottland      = 57,
            Schweden        = 58,
            Schweiz         = 59,
            Senegal         = 60,
            Slowakei        = 61,
            Slowenien       = 62,
            Spanien         = 63,
            [Description("Südkorea")]
            Suedkorea       = 64,
            Tschechien      = 65,
            Tunesien        = 66,
            [Description("Türkei")]
            Tuerkei         = 67,
            Ukraine         = 68,
            Ungarn          = 69,
            Uruguay         = 70,
            USA             = 71,
            Venezuela       = 72,
            Wales           = 73,
            Zaire           = 74,
            Zypern          = 75
        }
        /// <summary>Hair color (single selection).</summary>
        public enum HairColor : byte
        {
            Hellblond = 0,
            Blond     = 1,
            Braun     = 2,
            Rot       = 3,
            Schwarz   = 4,
            Glatze    = 5
        }
        /// <summary>Positive mood-factor bitmask.</summary>
        public enum Happy : byte
        {
            None                 = 0,
            TollerVertrag        = 1 << 0,
            TollePraemien        = 1 << 1,
            TolleStimmung        = 1 << 2,
            TollerVertragDoppelt = 1 << 3,
            TollerTrainer        = 1 << 4
        }
        /// <summary>Health/injury-proneness trait bitmask.</summary>
        public enum Health : byte
        {
            Normal         = 0,
            Robustheit     = 1 << 0,
            Anfaelligkeit = 1 << 1,
            Knieprobleme   = 1 << 2
        }
        /// <summary>Personality-trait bitmask (single selection in practice, modelled as flags for the underlying byte layout).</summary>
        public enum Personality : ushort
        {
            None                 = 0,
            Fuehrungsperson      = 1 <<  0,
            Kaempfernatur        = 1 <<  1,
            Trainingsweltmeister = 1 <<  2,
            Trainingsfaul        = 1 <<  3,
            Treter               = 1 <<  4,
            FairPlayer           = 1 <<  5,
            Schwalbenkoenig      = 1 <<  6,
            Allrounder           = 1 <<  7,
            FlexiblePlayer       = 1 <<  8,
            Heimspieler          = 1 <<  9,
            AuswaertsSpieler     = 1 << 10,
            Talent               = 1 << 11
        }
        /// <summary>Playing position (Hauptposition/secondary positions use the same values).</summary>
        public enum Position : byte
        {
            None =  0,
            TO   =  1,
            L    =  2,
            MD   =  3,
            LV   =  4,
            RV   =  5,
            RM   =  6,
            LM   =  7,
            DM   =  8,
            OM   =  9,
            S    = 10
        }
        /// <summary>
        /// Skill bitmask. Goalkeeper skills (Elfmetertoeter..Ballsicherheit) and outfield skills
        /// (Kopfball..Beidfuessigkeit) intentionally reuse the same bit positions - only one group
        /// is meaningful at a time, selected by whether the player's Position is TO (see Player.IsTO).
        /// </summary>
        public enum Skills : ushort
        {
            None            = 0,
            Elfmetertoeter  = 1 <<  0,
            StarkeReflexe   = 1 <<  1,
            Herauslaufen    = 1 <<  2,
            Fangsicherheit  = 1 <<  3,
            Fausten         = 1 <<  4,
            Ballsicherheit  = 1 <<  5,
            Kopfball        = 1 <<  0,
            Zweikampf       = 1 <<  1,
            Schnelligkeit   = 1 <<  2,
            Schusskraft     = 1 <<  3,
            Freistoesse     = 1 <<  4,
            Flanken         = 1 <<  5,
            Torinstinkt     = 1 <<  6,
            Laufstaerke     = 1 <<  7,
            Technik         = 1 <<  8,
            Spielmacher     = 1 <<  9,
            Beidfuessigkeit = 1 << 10,
        }
        /// <summary>Skin color (single selection).</summary>
        public enum SkinColor : byte
        {
            Fair  = 0,
            Dark  = 1,
            Black = 2
        }
        /// <summary>Negative mood-factor bitmask.</summary>
        public enum Unhappy : ushort
        {
            None                  = 0,
            WillEndlichSpielen    = 1 <<  0,
            WillMehrGeld          = 1 <<  1,
            MiesePraemien         = 1 <<  2,
            GehtNichtNachLeistung = 1 <<  3,
            FalschePosition       = 1 <<  4,
            ScheissVertrag        = 1 <<  5,
            MieseMannschaft       = 1 <<  6,
            SchlechteStimmung     = 1 <<  7,
            VerletztUndNieBesucht = 1 <<  8,
            UngerechteBehandlung  = 1 <<  9,
            BloedeFans            = 1 << 10,
            BloedeMitspieler      = 1 << 11,
            ScheissTrainer        = 1 << 12,
            VerhandlungGeplatzt   = 1 << 13,
            ScheissManager        = 1 << 14,
            ScheissNachwuchsrunde = 1 << 15
        }
        /// <summary>
        /// Kind of injury or illness (player byte +0x46 in the working copy). The game shows the text
        /// from its table at 0x6C78F8 (RESDLL texts). 1-61 are injuries (shown in weeks), 65-81
        /// illnesses (shown in days, FUN_0061b070); the game rolls injuries from 1-61 only
        /// (FUN_00633ae0). 0 and the unused 62-64 show the first text.
        /// </summary>
        public enum Injury : byte
        {
            [Description("keine")] Keine = 0,
            [Description("Achillessehnenanriß")] Achillessehnenanriss = 1,
            [Description("Achillessehnenabriß")] Achillessehnenabriss = 2,
            [Description("Achillessehnenentzündung")] Achillessehnenentzuendung = 3,
            [Description("Achillessehnenreizung")] Achillessehnenreizung = 4,
            [Description("Adduktorenreizung")] Adduktorenreizung = 5,
            [Description("Adduktorenzerrung")] Adduktorenzerrung = 6,
            [Description("Adduktorenabriß")] Adduktorenabriss = 7,
            [Description("Außenbanddehnung")] Aussenbanddehnung = 8,
            [Description("Außenbandanriß")] Aussenbandanriss = 9,
            [Description("Außenbandriß")] Aussenbandriss = 10,
            [Description("Bänderdehnung Knöchel")] BaenderdehnungKnoechel = 11,
            [Description("Bauchmuskelzerrung")] Bauchmuskelzerrung = 12,
            [Description("Fußgelenkstauchung")] Fussgelenkstauchung = 13,
            [Description("Innenbandanriß")] Innenbandanriss = 14,
            [Description("Innenbanddehnung")] Innenbanddehnung = 15,
            [Description("Innenbandriß")] Innenbandriss = 16,
            [Description("Jochbeinbruch")] Jochbeinbruch = 17,
            [Description("Kapselprellung am Knie")] KapselprellungKnie = 18,
            [Description("Kapselprellung am Knöchel")] KapselprellungKnoechel = 19,
            [Description("Kapselriß im Knie")] KapselrissKnie = 20,
            [Description("Kapselriß im Knöchel")] KapselrissKnoechel = 21,
            [Description("Kniescheibenbruch")] Kniescheibenbruch = 22,
            [Description("Kniescheibenprellung")] Kniescheibenprellung = 23,
            [Description("Knöchelbruch")] Knoechelbruch = 24,
            [Description("Knöchelprellung")] Knoechelprellung = 25,
            [Description("Knöchelverstauchung")] Knoechelverstauchung = 26,
            [Description("Knorpelschaden im Knie")] KnorpelschadenKnie = 27,
            [Description("Kopfplatzwunde")] Kopfplatzwunde = 28,
            [Description("Kreuzbandanriß")] Kreuzbandanriss = 29,
            [Description("Kreuzbandriß")] Kreuzbandriss = 30,
            [Description("leichte Gehirnerschütterung")] LeichteGehirnerschuetterung = 31,
            [Description("leichte Knieprellung")] LeichteKnieprellung = 32,
            [Description("Leistenzerrung")] Leistenzerrung = 33,
            [Description("Meniskusreizung")] Meniskusreizung = 34,
            [Description("Meniskusabriß")] Meniskusabriss = 35,
            [Description("Meniskuseinriß")] Meniskuseinriss = 36,
            [Description("Mittelfußbruch")] Mittelfussbruch = 37,
            [Description("Muskelbündelriß")] Muskelbuendelriss = 38,
            [Description("Muskelfaserriß")] Muskelfaserriss = 39,
            [Description("Muskelprobleme")] Muskelprobleme = 40,
            [Description("Muskelverhärtung")] Muskelverhaertung = 41,
            [Description("Oberschenkelbruch")] Oberschenkelbruch = 42,
            [Description("Oberschenkelfleischwunde")] Oberschenkelfleischwunde = 43,
            [Description("Oberschenkelprellung")] Oberschenkelprellung = 44,
            [Description("Oberschenkelzerrung")] Oberschenkelzerrung = 45,
            [Description("Rippenbruch")] Rippenbruch = 46,
            [Description("Rißwunde am Knie")] RisswundeKnie = 47,
            [Description("Schienbeinbruch")] Schienbeinbruch = 48,
            [Description("Schlüsselbeinbruch")] Schluesselbeinbruch = 49,
            [Description("schwere Gehirnerschütterung")] SchwereGehirnerschuetterung = 50,
            [Description("schwerer Meniskusschaden")] SchwererMeniskusschaden = 51,
            [Description("schwere Zehenprellung")] SchwereZehenprellung = 52,
            [Description("Sprunggelenksverletzung")] Sprunggelenksverletzung = 53,
            [Description("Syndesmoseriß")] Syndesmoseriss = 54,
            [Description("Wadenbeinbruch")] Wadenbeinbruch = 55,
            [Description("Wadenprellung")] Wadenprellung = 56,
            [Description("Wadenzerrung")] Wadenzerrung = 57,
            [Description("Zehenbruch")] Zehenbruch = 58,
            [Description("Haarriß im Fuß")] HaarrissFuss = 59,
            [Description("Finger gebrochen")] FingerGebrochen = 60,
            [Description("Rückenprellung")] Rueckenprellung = 61,
            [Description("Angina")] Angina = 65,
            [Description("Darminfektion")] Darminfektion = 66,
            [Description("Erkältung")] Erkaeltung = 67,
            [Description("Fischvergiftung")] Fischvergiftung = 68,
            [Description("grippaler Infekt")] GrippalerInfekt = 69,
            [Description("Hexenschuß")] Hexenschuss = 70,
            [Description("Magenschmerzen")] Magenschmerzen = 71,
            [Description("Pilzvergiftung")] Pilzvergiftung = 72,
            [Description("Rückenbeschwerden")] Rueckenbeschwerden = 73,
            [Description("Tennisarm")] Tennisarm = 74,
            [Description("Übelkeit")] Uebelkeit = 75,
            [Description("Virusinfektion")] Virusinfektion = 76,
            [Description("Durchfall")] Durchfall = 77,
            [Description("Nervenzusammenbruch")] Nervenzusammenbruch = 78,
            [Description("in psychiatrischer Behandlung")] InPsychiatrischerBehandlung = 79,
            [Description("Malaria")] Malaria = 80,
            [Description("Sonnenstich")] Sonnenstich = 81
        }
    }
}
