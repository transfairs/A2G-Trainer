using A2G_Trainer_XP.Controller;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// Static table of reverse-engineered hex memory offsets for each entity/context combination
    /// (e.g. own club vs. opponent club, own players vs. dynamic team). <see cref="From"/> selects
    /// the right preset for a given <see cref="PlayerEnums.AddressType"/>.
    /// </summary>
    class AddressPresets
    {
        /// <summary>Field offsets for the user's own player roster.</summary>
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
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.BUY_PRICE, "BC"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.HOME_COUNTRY, "9E"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.HOME_CLUB, "A0"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.CONTRACT_DURATION, "C0"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.CONTRACT_DETAILS, "C1"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.YEARS_IN_CLUB, "C2"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.CAREER, "D7"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.RED_CARDS, "4B"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.YELLOW_RED_CARDS, "4C"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.GOALS, "56"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.OWN_GOALS, "6A"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.ASSISTS, "6B"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.JOKER_GOALS, "6C"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.APPEARANCES, "77"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.SUBBED_IN, "78"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.SUBBED_OUT, "79"),
            new KeyValuePair<Enum, string>(PlayerEnums.AddressKey.UPGRADE_GOALS, "166")
        );

        /// <summary>Field offsets for the user's own club/stadium.</summary>
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
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.BlockLSeats, "882"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.Capacity, "850")
        );

        /// <summary>Field offsets for the currently loaded opponent club.</summary>
        public static readonly Addresses OPPONENT_CLUB = Addresses.Create(
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.NAME, "14E0"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.PLAYER_COUNT, "15C6"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.ID, "15E2"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.COUNTRY, "15E3"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT, "1EF9")
        );

        /// <summary>Field offsets for whichever club is currently shown (club-membership lookups, trainee picker); shares OPPONENT_CLUB's layout.</summary>
        public static Addresses DYNAMIC_CLUB { get { return AddressPresets.OPPONENT_CLUB; } }

        /// <summary>Field offsets for scanning every club in the league (used for club-membership/trainee lookups).</summary>
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
        /// <summary>Field offsets for non-playable (e.g. bot-only) clubs.</summary>
        public static readonly Addresses NONPLAYABLE_CLUBS = Addresses.Create(
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.NAME, "0"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.PLAYER_COUNT, "D8"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.ID, "74"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.COUNTRY, "73"),
            new KeyValuePair<Enum, string>(ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT, "-1")
        );

        /// <summary>
        /// Field offsets for one coach slot. Empty-string entries are not yet mapped in memory
        /// (see the WriteFields/ReadFields callers for which fields are actually wired up).
        /// </summary>
        public static readonly Addresses COACH = Addresses.Create(
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.FIRSTNAME, "34B00"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.LAST_NAME, "34B0A"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.LEVEL, "34B22"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.AGE, "34B24"),
            // COUNTRY/CLUB: the club this trainer coaches right now (UInt16 each in the game, values
            // fit a byte). Found via the original build's GetMitspieler and confirmed live: Trainer
            // slot 0 read 14/55, exactly the club in team slot 0. Read-only in the Trainer - the game
            // keeps further state for the current club that a plain write wouldn't update.
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.COUNTRY, "34B1C"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.CLUB, "34B1E"),
            // COUNTRY_NEXT_SEASON/CLUB_NEXT_SEASON/RELEASE_CLAUSE: Robin found these live via Cheat
            // Engine against Trainer slot 0 (0085783C/0085783E/00857864). Same session, same struct
            // instance as the FIRSTNAME reference below (0x857800 -> pointer value 0x822D00), so the
            // offset from that pointer is address-0x822D00: 34B3C/34B3E/34B64 respectively.
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.COUNTRY_NEXT_SEASON, "34B3C"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.CLUB_NEXT_SEASON, "34B3E"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.RELEASE_CLAUSE, "34B64"),
            // BOARD_RELEASE: the board has released the trainer ("Vorstand um Freigabe bitten"
            // granted) - with it, like with a release clause, the game lets him negotiate with other
            // clubs (FUN_004a0be0). Read-only in the Trainer.
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.BOARD_RELEASE, "34B65"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.OPTION_TO_LEAVE, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.WEALTH, "34B90"),
            // The current contract (working copy +0x48..+0x67), as FUN_00629be0 fills it from a signed
            // offer: five Int32 amounts, then season goal, years left, extension option and extras
            // (UInt16 each), then the release clause byte. SALARY is per year (the monthly accounts
            // pay a twelfth); SALARY and POINT_BONUS move by 20% on promotion/relegation
            // (FUN_0059f140). POINT_BONUS is paid per league point (FUN_005fedd0), PER_TROPHY_ROUND
            // per cup round won (FUN_005dadb0), PER_TITLE for the title or promotion (FUN_005722c0)
            // and ACHIEVED_BONUS when the season goal is reached (FUN_00572cf0).
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.SALARY, "34B48"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.POINT_BONUS, "34B4C"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.PER_TROPHY_ROUND, "34B50"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.PER_TITLE, "34B54"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.ACHIEVED_BONUS, "34B58"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.SEASON_GOAL, "34B5C"),
            // CONTRACT_DURATION: years left on the current contract (UInt16). Below 2 the contract
            // counts as running out and the game allows negotiating without a release clause
            // (FUN_004a0be0). Read-only in the Trainer.
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.CONTRACT_DURATION, "34B5E"),
            // EXTEND_OPTION: the club's option to extend by a year. With one year left, FUN_0062ac60 lets
            // the club draw it (FUN_00555b40: years left + 1, option cleared). PERKS: see CoachEnums.VehiclePerk.
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.EXTEND_OPTION, "34B60"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.PERKS, "34B62"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.TRUST, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.NERVES, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.NATIONALTEAM, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.NATIONAL_BONUS, ""),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.DIFFICULTY, "34B34"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.GAMES, "34BCC"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.WINS, "34BCE"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.STOCK_COUNTRY, "34BD8"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.STOCK_CLUB, "34BDA"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.STOCK_SHARES, "34BDC"),
            new KeyValuePair<Enum, string>(CoachEnums.AddressKey.STOCK_PRICE, "34BE0")
        );
        /// <summary>
        /// Field offsets for the savegame-wide league/country settings (0x82663E.. on the original
        /// build, set up by the game's new-game code FUN_006441e0): Int16 bonus-country count, Int16
        /// main country (+ its league system at 3942), Int16[4] bonus countries (FFFF = unused) and
        /// the parallel Int16[4] league system of each bonus country (0 = unused).
        /// </summary>
        public static readonly Addresses LEAGUE_SETTINGS = Addresses.Create(
            new KeyValuePair<Enum, string>(LeagueEnums.AddressKey.BONUS_COUNTRY_COUNT, "393E"),
            new KeyValuePair<Enum, string>(LeagueEnums.AddressKey.MAIN_COUNTRY, "3940"),
            new KeyValuePair<Enum, string>(LeagueEnums.AddressKey.ADDITIONAL_COUNTRY, "3944"),
            new KeyValuePair<Enum, string>(LeagueEnums.AddressKey.BONUS_LEAGUE_SYSTEM, "394C")
        );

        /// <summary>Player-field offsets for the current opponent roster; set by <see cref="InitOpponent"/>.</summary>
        public static Addresses OPPONENT_PLAYERS { get; private set; }

        /// <summary>Player-field offsets for whichever roster is dynamically selected (e.g. trainees); set by <see cref="InitDynamicTeam"/>.</summary>
        public static Addresses DYNAMIC_PLAYERS { get; private set; }

        /// <summary>Re-bases OWN_PLAYERS' field offsets onto a roster starting at <paramref name="lastPlayer"/>.</summary>
        public static Addresses InitPreset(string lastPlayer)
        {
            return AddressPresets.OWN_PLAYERS.WithOffset(Tools.SumHex(new string[] { Settings.PlayerOffset, lastPlayer }));
        }

        /// <summary>Computes and stores <see cref="OPPONENT_PLAYERS"/> for the given roster start.</summary>
        public static void InitOpponent(string lastPlayer)
        {
            AddressPresets.OPPONENT_PLAYERS = AddressPresets.InitPreset(lastPlayer);
        }
        /// <summary>Computes and stores <see cref="DYNAMIC_PLAYERS"/> for the given roster start.</summary>
        public static void InitDynamicTeam(string lastPlayer)
        {
            AddressPresets.DYNAMIC_PLAYERS = AddressPresets.InitPreset(lastPlayer);
        }

        /// <summary>Selects the address preset matching the given lookup context (own/opponent/dynamic/etc.) and entity kind.</summary>
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
