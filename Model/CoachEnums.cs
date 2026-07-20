using System;
using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    public static class CoachEnums
    {
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
            // Relative offsets for stock slot 0 (Aktie 1) - every further slot (up to the
            // game's max of 6) sits at this offset + slotIndex * 0x10, see CoachController.
            STOCK_COUNTRY,
            STOCK_CLUB,
            STOCK_SHARES,
            STOCK_PRICE
        }

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
    }
}
