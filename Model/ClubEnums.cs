using System;
using System.ComponentModel;

namespace A2G_Trainer.Model
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
            Capacity,
            HasRunningTrack,
            HasUpperSeats,
            GrantPot,
            GrantRate,
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
            Wealth,
            Jersey,
            FanTurnout,
            FanType,
            Article,
            FanFriendship,
            ArchRival,
            Board,
            Preposition,
            Town,
            MapX,
            MapY
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

        /// <summary>Jersey colours in the editor's order (low nibble of the shirt byte, whole shorts/socks byte).</summary>
        public enum JerseyColor : byte
        {
            [Description("weiß")]
            Weiss = 0,
            [Description("rot")]
            Rot = 1,
            [Description("gelb")]
            Gelb = 2,
            [Description("blau")]
            Blau = 3,
            [Description("hellgrün")]
            Hellgruen = 4,
            [Description("grün")]
            Gruen = 5,
            [Description("türkis")]
            Tuerkis = 6,
            [Description("hellblau")]
            Hellblau = 7,
            [Description("braun")]
            Braun = 8,
            [Description("lila")]
            Lila = 9,
            [Description("orange")]
            Orange = 10,
            [Description("schwarz")]
            Schwarz = 11,
            [Description("weinrot")]
            Weinrot = 12
        }

        /// <summary>Shirt pattern in the editor's order (high nibble of the shirt byte).</summary>
        public enum ShirtPattern : byte
        {
            [Description("einfarbig")]
            Neutral = 0,
            [Description("längs gestreift")]
            Laengs = 1,
            [Description("quer gestreift")]
            Quer = 2,
            [Description("halb/halb")]
            HalbHalb = 3,
            [Description("Schulter")]
            Schulter = 4,
            [Description("Brustring")]
            Brustring = 5,
            [Description("Ärmel wie Hose")]
            AermelWieHose = 6
        }

        /// <summary>"Fanaufkommen" (club +0xEE): lower means more fans, the game's income formulas use 8 - value.</summary>
        public enum FanTurnout : byte
        {
            [Description("wahre Horden")]
            WahreHorden = 0,
            [Description("sehr hoch")]
            SehrHoch = 1,
            [Description("hoch")]
            Hoch = 2,
            [Description("Durchschnitt")]
            Durchschnitt = 3,
            [Description("bescheiden")]
            Bescheiden = 4,
            [Description("ärmlich")]
            Aermlich = 5,
            [Description("Fans?")]
            KeineFans = 6
        }

        /// <summary>
        /// "Art der Fans" (club +0xEF, texts 6023-6030). The game sets it anew after every match
        /// (FUN_00649cd0) from the stadium, recent results and titles; until then it changes the
        /// crowd (FUN_00649d80).
        /// </summary>
        public enum FanType : byte
        {
            [Description("normal")]
            Normal = 0,
            [Description("lautstark")]
            Lautstark = 1,
            [Description("verwöhnt")]
            Verwoehnt = 2,
            [Description("anspruchsvoll")]
            Anspruchsvoll = 3,
            [Description("treu")]
            Treu = 4,
            [Description("erfolgshungrig")]
            Erfolgshungrig = 5,
            [Description("frustriert")]
            Frustriert = 6,
            [Description("euphorisch")]
            Euphorisch = 7
        }

        /// <summary>"Vorstand" (club +0xF5), with the game's own names (the editor calls 4 "souverän").</summary>
        public enum Board : byte
        {
            [Description("Pulverfaß")]
            Pulverfass = 0,
            [Description("Schleudersitz")]
            Schleudersitz = 1,
            [Description("nervös")]
            Nervoes = 2,
            [Description("normal")]
            Normal = 3,
            [Description("anspruchslos")]
            Anspruchslos = 4,
            [Description("treu")]
            Treu = 5
        }

        /// <summary>Article in front of the club name (club +0xF2, picks texts 1569-1572: "Der %s", "Die %s", "%s").</summary>
        public enum Article : byte
        {
            [Description("der")]
            Der = 0,
            [Description("die")]
            Die = 1,
            [Description("ohne Artikel")]
            Ohne = 2,
            [Description("ohne Artikel (Nationalteam)")]
            OhneNationalteam = 3
        }

        /// <summary>Preposition for "...vom MSV Duisburg kam..." (club +0xF8), in the editor's order.</summary>
        public enum Preposition : byte
        {
            [Description("vom")]
            Vom = 0,
            [Description("von den")]
            VonDen = 1,
            [Description("von")]
            Von = 2,
            [Description("von der")]
            VonDer = 3
        }
    }
}
