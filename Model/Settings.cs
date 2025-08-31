namespace A2G_Trainer_XP.Controller
{
    static class Settings
    {
        private const bool   debug               = false;
        private const int    playerOffset        = 178;
        private const string playerAddress       = "0x423690";
        private const string playerAddressAll    = "0x423690"; //"0x3B4F4C";
        private const string playerAddressGog    = "0x4267D0";
        private const string playerAddressAllGog = "0x4267D0"; //"0x3B8098";

        private const int    clubOffset          = 770;
        public const string  AllClubOffset       = "64FA8";
        public const string  OpponentOffset      = "14E0";
        public const string  OpponentName        = "0x4241E0";
        private const string clubAddress         = "0x400710";
        private const string clubAddressAll      = "0x400710"; //"0x487CA8";
        private const string clubAddressGog      = "0x403850";
        private const string clubAddressAllGog   = "0x403850"; //"0x48ADE8";


        internal static bool   IsDebug           { get => debug;        }
        internal static int    PlayerOffset      { get => playerOffset; }
        internal static int    ClubOffset        { get => clubOffset;   }

        internal static string[] PlayerAddress = { playerAddress, playerAddressGog, playerAddressAll, playerAddressAllGog };
        internal static string[] ClubAddress   = { clubAddress,   clubAddressGog,   clubAddressAll,   clubAddressAllGog   };
    }
}
