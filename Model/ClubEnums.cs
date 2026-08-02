using System;
using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// Enum groups used to look up club/stadium memory addresses, plus helpers for mapping
    /// their raw byte values to display-friendly enums.
    /// </summary>
    public static class ClubEnums
    {
        /// <summary>Returns an enum value's [Description] text, falling back to its name.</summary>
        public static string GetDescription(Enum value)
        {
            var f = value.GetType().GetField(value.ToString());
            var attributes = (DescriptionAttribute[])f.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : value.ToString();
        }

        /// <summary>
        /// Identifies which club/stadium field an address entry resolves. The BlockX* groups (Weeks/
        /// Standings/Seats) repeat per stadium block A-L.
        /// </summary>
        public enum AddressKey
        {
            NAME,
            PLAYER_COUNT,
            AMATEUR_PLAYER_COUNT,
            EarningsLeagueGames,
            EarningsFriendlyGames,
            EarningsAds,
            StadiumName,
            ROOF,
            DisplayUnit,
            HasFloodLight,
            HasGrassHeating,
            FieldCondition,
            BlockAWeeks,
            BlockBWeeks,
            BlockCWeeks,
            BlockDWeeks,
            BlockEWeeks,
            BlockFWeeks,
            BlockGWeeks,
            BlockHWeeks,
            BlockIWeeks,
            BlockJWeeks,
            BlockKWeeks,
            BlockLWeeks,
            BlockAStandings,
            BlockBStandings,
            BlockCStandings,
            BlockDStandings,
            BlockEStandings,
            BlockFStandings,
            BlockGStandings,
            BlockHStandings,
            BlockIStandings,
            BlockJStandings,
            BlockKStandings,
            BlockLStandings,
            BlockASeats,
            BlockBSeats,
            BlockCSeats,
            BlockDSeats,
            BlockESeats,
            BlockFSeats,
            BlockGSeats,
            BlockHSeats,
            BlockISeats,
            BlockJSeats,
            BlockKSeats,
            BlockLSeats,
            OpponentName,
            ID,
            COUNTRY,
            TRAINEE,
            TRAINEE_A,
            TRAINEE_B,
            TRAINEE_C,
            Respect,
            Will2Win,
            Spirit,
            SponsorCash,
            SponsorPeriod,
            FreeTickets,
            RoadGameSupport,
            TeamCohesion,
            Wealth
        }

        /// <summary>Maps the raw field-condition byte (lower is better) to its display bucket.</summary>
        public static FieldCondition MapToCondition(byte value)
        {
            if (value >= 30)
                return FieldCondition.SpielfeldFraglich;
            if (value >= 20)
                return FieldCondition.Kartoffelacker;
            if (value >= 10)
                return FieldCondition.Holprig;
            if (value >= 5)
                return FieldCondition.Mittelmaessig;
            if (value >= 2)
                return FieldCondition.Ordentlich;
            if (value == 1)
                return FieldCondition.Optimal;

            return FieldCondition.Clean;
        }

        /// <summary>Stadium scoreboard type.</summary>
        public enum DisplayUnit : byte
        {
            [Description("keine Anzeigetafel")]
            None = 0,
            [Description("Ergebnisanzeige mit Tafeln")]
            ErgebnisAnzeige = 1,
            [Description("kleine LED-Anzeigetafel")]
            KleineLED = 2,
            [Description("große LED-Anzeigetafel")]
            GrosseLED = 3,
            Videowand = 4
        }

        /// <summary>Pitch condition bucket; values are the upper bound of the raw byte range they cover.</summary>
        public enum FieldCondition : byte
        {
            [Description("Perfekt")]
            Clean =  0,
            Optimal           =  1, // 1
            Ordentlich        =  3, // 2-4
            [Description("Mittelmäßig")]
            Mittelmaessig =  7, // 5-9
            Holprig           = 15, // 10-19
            Kartoffelacker    = 25, // 20-29
            [Description("Spielfeld ???")]
            SpielfeldFraglich = 30, // > 29
        }
        /// <summary>Bitmask of which stadium blocks (A-L) have a roof.</summary>
        public enum Roof : ushort
        {
            None   = 0,
            BlockA = 1 <<  0,
            BlockB = 1 <<  1,
            BlockC = 1 <<  2,
            BlockD = 1 <<  3,
            BlockE = 1 <<  4,
            BlockF = 1 <<  5,
            BlockG = 1 <<  6,
            BlockH = 1 <<  7,
            BlockI = 1 <<  8,
            BlockJ = 1 <<  9,
            BlockK = 1 << 10,
            BlockL = 1 << 11
        }
    }
}
