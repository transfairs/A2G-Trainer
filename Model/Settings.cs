using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;

namespace A2G_Trainer_XP.Controller
{
    static class Settings
    {
        private const bool   debug                    = false;
        private const ushort clubCount                = 294;
        private const ushort nonPlayableCount         = 100;

        private const string playerAddress            = "0x423690";
        private const string clubAddress              = "0x400710";

        private const string gogOffset                = "3140";
        private const string playerOffset             = "178";
        private const string clubOffset               = "770";
        private const string allTraineeOffset         = "1A";
        private const string nonPlayableOffset        = "138";

        private const string allClubInitialOffset     = "64FA8";
        private const string nonPlayableInitialOffset = "3D1190";



        internal static bool   IsDebug           { get => debug; }

        internal static string PlayerOffset      { get => playerOffset; }
        internal static string ClubOffset        { get => clubOffset; }
        internal static string AllTraineeOffset  { get => allTraineeOffset; }
        internal static string NonPlayableOffset { get => nonPlayableOffset; }

        internal static KeyValuePair<string, Tuple<ushort, PlayerEnums.AddressType>> AllClubInitialOffset     = new KeyValuePair<string, Tuple<ushort, PlayerEnums.AddressType>>(allClubInitialOffset, new Tuple<ushort, PlayerEnums.AddressType>(clubCount, PlayerEnums.AddressType.ALL));
        internal static KeyValuePair<string, Tuple<ushort, PlayerEnums.AddressType>> NonPlayableInitialOffset = new KeyValuePair<string, Tuple<ushort, PlayerEnums.AddressType>>(nonPlayableInitialOffset, new Tuple<ushort, PlayerEnums.AddressType>(nonPlayableCount, PlayerEnums.AddressType.NON_PLAYABLE));

        internal static string[] PlayerAddress = { playerAddress, Tools.SumHex(new string[] { playerAddress, gogOffset }) };
        internal static string[] ClubAddress   = { clubAddress,   Tools.SumHex(new string[] { clubAddress, gogOffset   }) };
    }
}
