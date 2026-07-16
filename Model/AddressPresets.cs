using A2G_Trainer_XP.Controller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace A2G_Trainer_XP.Model
{
    class AddressPresets
    {
        public static readonly Addresses OWN_PLAYERS = Addresses.Create(
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.ID, "0"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.AGE, "1E"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.FIRSTNAME, "2"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.LASTNAME, "C"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.CLUB_COUNTRY, "23"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.CLUB_ID, "24"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.LEVEL, "1F"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.FORM, "25"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.SKIN, "1C"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.HAIR, "1D"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.CONDITION, "30"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.FRESHNESS, "31"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.NATIONALITY, "38"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.POSITION, "20"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.SECONDARY_1, "21"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.SECONDARY_2, "22"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.SKILLS, "28"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.NEG_SKILLS, "2A"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.PERSONALITY, "2C"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.CHARACTER, "2E"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.HEALTH, "2F"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.UNHAPPY, "34"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.HAPPY, "36"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.INJURED_DAYS, "44"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.INJURY, "46"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.VULNERABLE, "47"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.BANNED_MATCHES, "48"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.DOPED, "49"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.YELLOW_CARDS, "4A"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.SALARY, "B4"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.SHOWUP, "B6"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.GOALS_BONUS, "B8"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.TRANSFER_FEE, "BA"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.CONTRACT_DURATION, "C0"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.CONTRACT_DETAILS, "C1"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.YEARS_IN_CLUB, "C2"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.CAREER, "D7")
        );

        public static readonly Addresses OWN_CLUB = Addresses.Create(
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.NAME, "0"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.OpponentName, "14E0"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.Respect, "8E"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.Will2Win, "9A"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.Spirit, "9C"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.TeamCohesion, "94C"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.SponsorCash, "3C4"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.SponsorPeriod, "3BD"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.FreeTickets, "814"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.RoadGameSupport, "816"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.PLAYER_COUNT, "E6"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.ID, "102"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.COUNTRY, "103"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT, "A19"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.Wealth, "154"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.EarningsLeagueGames, "290"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.EarningsFriendlyGames, "2B8"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.EarningsAds, "470"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.StadiumName, "81C"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.ROOF, "890"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.DisplayUnit, "896"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.HasFloodLight, "897"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.HasGrassHeating, "898"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.FieldCondition, "89B"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockAWeeks, "884"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockBWeeks, "885"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockCWeeks, "886"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockDWeeks, "887"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockEWeeks, "888"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockFWeeks, "889"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockGWeeks, "88A"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockHWeeks, "88B"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockIWeeks, "88C"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockJWeeks, "88D"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockKWeeks, "88E"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockLWeeks, "88F"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockAStandings, "854"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockBStandings, "856"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockCStandings, "858"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockDStandings, "85A"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockEStandings, "85C"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockFStandings, "85E"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockGStandings, "860"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockHStandings, "862"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockIStandings, "864"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockJStandings, "866"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockKStandings, "868"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockLStandings, "86A"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockASeats, "86C"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockBSeats, "86E"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockCSeats, "870"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockDSeats, "872"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockESeats, "874"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockFSeats, "876"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockGSeats, "878"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockHSeats, "87A"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockISeats, "87C"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockJSeats, "87E"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockKSeats, "880"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockLSeats, "882")
        );

        public static readonly Addresses OPPONENT_CLUB = Addresses.Create(
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.NAME, "14E0"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.PLAYER_COUNT, "15C6"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.ID, "15E2"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.COUNTRY, "15E3"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT, "1EF9")
        );

        public static Addresses DYNAMIC_CLUB { get { return AddressPresets.OPPONENT_CLUB; } }

        public static readonly Addresses ALL_CLUBS = Addresses.Create(
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.NAME, "0"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.PLAYER_COUNT, "DE"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.ID, "F4"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.COUNTRY, "F8"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT, "5BD"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.TRAINEE, "1"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.TRAINEE_A, "-10CF7"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.TRAINEE_B, "-10CF6"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.TRAINEE_C, "-10CF5")
        );
        public static readonly Addresses NONPLAYABLE_CLUBS = Addresses.Create(
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.NAME, "0"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.PLAYER_COUNT, "D8"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.ID, "74"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.COUNTRY, "73"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT, "-1")
        );

        public static readonly Addresses COACH = Addresses.Create(
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.FIRSTNAME, "34B00"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.LAST_NAME, "34B0A"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.LEVEL, "34B22"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.AGE, "34B24"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.COUNTRY, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.CLUB, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.COUNTRY_NEXT_SEASON, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.CLUB_NEXT_SEASON, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.OPTION_TO_LEAVE, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.WEALTH, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.SALARY, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.POINT_BONUS, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.PER_TROPHY_ROUND, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.PER_TITLE, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.ACHIEVED_BONUS, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.CONTRACT_DURATION, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.EXTEND_OPTION, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.TRUST, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.NERVES, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.NATIONALTEAM, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.NATIONAL_BONUS, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.FIRST_STOCK_COUNTRY, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.FIRST_STOCK_TEAM, "")
        );
        public static Addresses OPPONENT_PLAYERS { get; private set; }

        public static Addresses DYNAMIC_PLAYERS { get; private set; }

        public static Addresses InitPreset(string lastPlayer)
        {
            return AddressPresets.OWN_PLAYERS.WithOffset(Tools.SumHex(new string[] { Settings.PlayerOffset, lastPlayer }));
        }

        public static void InitOpponent(string lastPlayer)
        {
            AddressPresets.OPPONENT_PLAYERS = AddressPresets.InitPreset(lastPlayer);
        }
        public static void InitDynamicTeam(string lastPlayer)
        {
            AddressPresets.DYNAMIC_PLAYERS = AddressPresets.InitPreset(lastPlayer);
        }

        public static Addresses From(PlayerEnums.AddressType type, bool isClub)
        {
            if (isClub)
            {
                switch (type)
                {
                    case PlayerEnums.AddressType.OWN: return AddressPresets.OWN_CLUB;
                    case PlayerEnums.AddressType.OPPONENT: return AddressPresets.OPPONENT_CLUB;
                    case PlayerEnums.AddressType.TRAINEE:
                    case PlayerEnums.AddressType.DYNAMIC: return AddressPresets.DYNAMIC_CLUB;
                    case PlayerEnums.AddressType.ALL: return AddressPresets.ALL_CLUBS;
                    case PlayerEnums.AddressType.NON_PLAYABLE: return AddressPresets.NONPLAYABLE_CLUBS;
                    default: throw new ArgumentOutOfRangeException("type");
                }
            }
            else
            {
                switch (type)
                {
                    case PlayerEnums.AddressType.OWN: return AddressPresets.OWN_PLAYERS;
                    case PlayerEnums.AddressType.OPPONENT: return AddressPresets.OPPONENT_PLAYERS;
                    case PlayerEnums.AddressType.TRAINEE:
                    case PlayerEnums.AddressType.DYNAMIC: return AddressPresets.DYNAMIC_PLAYERS;
                    default: throw new ArgumentOutOfRangeException("type");
                }
            }
        }
    }
}
