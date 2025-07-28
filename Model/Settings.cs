namespace A2G_Trainer_XP.Controller
{
    static class Settings
    {
        private const bool   debug            = false;
        private const int    playerOffset     = 178;
        private const string playerAddress    = "0x423690";
        private const string playerAddressGog = "0x4267D0";
        private const string clubAddress      = "0x400710";
        private const string clubAddressGog   = "0x403850";


        internal static bool   IsDebug          { get => debug;            }
        internal static int    PlayerOffset     { get => playerOffset;     }
        internal static string ClubAddress      { get => clubAddress;      }
        internal static string PlayerAddress    { get => playerAddress;    }
        internal static string ClubAddressGog   { get => clubAddressGog;   }
        internal static string PlayerAddressGog { get => playerAddressGog; }
    }
}
