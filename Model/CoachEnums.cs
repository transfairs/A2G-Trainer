using System;
using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// Enum groups used to look up coach memory addresses and describe coach-facing values.
    /// </summary>
    public static class CoachEnums
    {
        /// <summary>Identifies which coach field an address entry resolves.</summary>
        public enum AddressKey
        {
            FIRSTNAME,
            LAST_NAME,
            AGE,
            LEVEL,
            COUNTRY,
            CLUB,
            COUNTRY_NEXT_SEASON,
            CLUB_NEXT_SEASON,
            RELEASE_CLAUSE,
            BOARD_RELEASE,
            OPTION_TO_LEAVE,
            WEALTH,
            SALARY,
            POINT_BONUS,
            PER_TROPHY_ROUND,
            PER_TITLE,
            ACHIEVED_BONUS,
            SEASON_GOAL,
            CONTRACT_DURATION,
            EXTEND_OPTION,
            PERKS,
            TRUST,
            NERVES,
            NATIONALTEAM,
            NATIONAL_BONUS,
            DIFFICULTY,
            GAMES,
            WINS,
            STOCK_COUNTRY,
            STOCK_CLUB,
            STOCK_SHARES,
            STOCK_PRICE
        }

        /// <summary>In-game difficulty levels, ordered from easiest (1) to hardest (5).</summary>
        public enum Difficulty : byte
        {
            [Description("Leicht & Locker")]
            LeichtUndLocker = 1,
            [Description("Nicht zu einfach")]
            NichtZuEinfach  = 2,
            Realistisch     = 3,
            [Description("Nur für die Besten")]
            NurFuerDieBesten = 4,
            [Description("Ultra Violence")]
            UltraViolence   = 5
        }

        /// <summary>
        /// Season goal agreed in the contract (UInt16 at +0x5C), in the order of the game's own text
        /// table (RESDLL.dll, "Meisterschaft" .. "zu Ende bringen"): 0 is the most ambitious. The
        /// board's offer picks it from the club's strength via the table at anstoss2.exe+2C9778 -
        /// top-league clubs never get "Aufstieg", lower-league favourites always do.
        /// </summary>
        public enum SeasonGoal : ushort
        {
            Meisterschaft = 0,
            [Description("Erste Drei")]
            ErsteDrei = 1,
            Aufstieg = 2,
            [Description("UEFA-Pokalplatz")]
            UefaPokalplatz = 3,
            [Description("oben mitspielen")]
            ObenMitspielen = 4,
            [Description("einstelliger Platz")]
            EinstelligerPlatz = 5,
            [Description("gute Show")]
            GuteShow = 6,
            [Description("guter Fußball")]
            GuterFussball = 7,
            Mittelfeldplatz = 8,
            [Description("ehrliche Arbeit")]
            EhrlicheArbeit = 9,
            Klassenerhalt = 10,
            [Description("zu Ende bringen")]
            ZuEndeBringen = 11
        }

        /// <summary>
        /// Contract extras ("Extras", UInt16 bitmask at +0x62): the game's 12 extras alternate between
        /// a means of transport (even bits) and a home (odd bits), cheapest first. The monthly
        /// accounts (FUN_00642e20) cut the coach's living costs to 80% for any home and by another
        /// 20 points for any vehicle except the Bahncard. Values are bit numbers; -1 means none.
        /// </summary>
        public enum VehiclePerk
        {
            Keines = -1,
            Bahncard = 0,
            Dienstente = 2,
            Mittelklassewagen = 4,
            Sportwagen = 6,
            Luxusschlitten = 8,
            [Description("RR mit Chauffeur")]
            RrMitChauffeur = 10
        }

        /// <summary>The home half of the contract extras (odd bits of +0x62) - see <see cref="VehiclePerk"/>.</summary>
        public enum HousingPerk
        {
            Keine = -1,
            [Description("kostenlose Untermiete")]
            KostenloseUntermiete = 1,
            [Description("2-Zimmer Wohnung")]
            ZweiZimmerWohnung = 3,
            [Description("4-Zimmer Wohnung")]
            VierZimmerWohnung = 5,
            [Description("eigener Bungalow")]
            EigenerBungalow = 7,
            Luxusvilla = 9,
            Palast = 11
        }

        /// <summary>
        /// The manager's image (UInt16 in the per-ID block at Settings.CoachImageRecordOffset). The
        /// values index the game's text table at anstoss2.exe+2B96C8 (RESDLL.dll IDs 6249..6265), which
        /// is why they don't follow the texts' own order. Each week the game re-assigns it once the
        /// trainer has been at his club for more than 28 days (FUN_0062af30): e.g. "Schleifer" after
        /// more than 8 of the last 20 weeks of hard training, "Wissenschaftler" after more than 7 weeks
        /// of the other training kinds, "PR-Mann" for a high charisma value; if nothing applies it
        /// keeps the current image (or, one week in ten, drops to "kein Image"). Hoffnungsträger,
        /// Motivationskünstler, Weltenbummler and Trunkenbold are never assigned by that check.
        /// </summary>
        public enum ManagerImage : ushort
        {
            [Description("kein Image")]
            KeinImage = 0,
            [Description("harter Hund")]
            HarterHund = 1,
            Schleifer = 2,
            Schlitzohr = 3,
            Streber = 4,
            Ahnungsloser = 5,
            Absteiger = 6,
            Choleriker = 7,
            Weichei = 8,
            Weltenbummler = 9,
            Wissenschaftler = 10,
            [Description("Hoffnungsträger")]
            Hoffnungstraeger = 11,
            [Description("PR-Mann")]
            PrMann = 12,
            [Description("lustiger Geselle")]
            LustigerGeselle = 13,
            [Description("unbekannte Größe")]
            UnbekannteGroesse = 14,
            [Description("Motivationskünstler")]
            Motivationskuenstler = 15,
            Trunkenbold = 16
        }

        /// <summary>
        /// The six competencies a trainer level's 6 distributable points are spread across (Robin
        /// found the memory layout directly: 16 levels x these 6 entries x 2 bytes each, no padding,
        /// in this exact order). Declaration order here must match the in-memory order.
        /// </summary>
        public enum CompetencyKey
        {
            Verhandlungsgeschick,
            [Description("Motivationsfähigkeit")]
            Motivationsfaehigkeit,
            Trainingsgestaltung,
            [Description("Autorität")]
            Autoritaet,
            Fremdsprachenkenntnisse,
            Ausstrahlung
        }
    }
}
