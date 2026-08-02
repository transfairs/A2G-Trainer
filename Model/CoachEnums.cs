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
            OPTION_TO_LEAVE,
            WEALTH,
            SALARY,
            POINT_BONUS,
            PER_TROPHY_ROUND,
            PER_TITLE,
            ACHIEVED_BONUS,
            CONTRACT_DURATION,
            EXTEND_OPTION,
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
