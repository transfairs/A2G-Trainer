using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;

namespace A2G_Trainer_XP.Controller
{
    static class Settings
    {
        private const bool   debug                    = false;
        private const ushort clubCount                = 294;
        private const ushort nonPlayableCount         = 130;

        private const string playerAddress            = "0x423690";
        private const string clubAddress              = "0x400710";
        private const string countrySelectionAddress  = "0x400710";

        private const string gogOffset                = "3140";
        private const string playerOffset             = "178";
        private const string clubOffset               = "770";
        private const string allTraineeOffset         = "1A";
        private const string nonPlayableOffset        = "138";

        private const string allClubInitialOffset     = "64FA8";
        private const string nonPlayableInitialOffset = "3D1190";

        // Anchors for the permanent Firstname/Lastname resolution (see Controller/NamePoolResolver.cs).
        // Unlike playerAddress above (a transient display cache), these two are stable across restarts
        // and across savegames sharing the same roster - found via live Cheat Engine tracing of the
        // savegame-load path, see docs/pointer-investigation-guide.md.
        //   namePoolPointerOffset: module-relative offset of a static pointer that always holds the
        //     current savegame's name-string-pool base address.
        //   playerRecordTableOffset/playerRecordStride: module-relative base and per-player stride of
        //     an array where each player's record holds, at +0/+2, the index of their Firstname/
        //     Lastname string within that pool.
        private const uint namePoolPointerOffset    = 0x7FDD58;
        private const uint playerRecordTableOffset  = 0x516678;
        private const uint playerRecordStride       = 0xC8;

        internal static uint NamePoolPointerOffset   { get => namePoolPointerOffset; }
        internal static uint PlayerRecordTableOffset { get => playerRecordTableOffset; }
        internal static uint PlayerRecordStride      { get => playerRecordStride; }

        internal static bool   IsDebug           { get => debug; }

        internal static string PlayerOffset      { get => playerOffset; }
        internal static string ClubOffset        { get => clubOffset; }
        internal static string AllTraineeOffset  { get => allTraineeOffset; }
        internal static string NonPlayableOffset { get => nonPlayableOffset; }

        internal static KeyValuePair<string, Tuple<ushort, PlayerEnums.AddressType>> AllClubInitialOffset     = new KeyValuePair<string, Tuple<ushort, PlayerEnums.AddressType>>(allClubInitialOffset, new Tuple<ushort, PlayerEnums.AddressType>(clubCount, PlayerEnums.AddressType.ALL));
        internal static KeyValuePair<string, Tuple<ushort, PlayerEnums.AddressType>> NonPlayableInitialOffset = new KeyValuePair<string, Tuple<ushort, PlayerEnums.AddressType>>(nonPlayableInitialOffset, new Tuple<ushort, PlayerEnums.AddressType>(nonPlayableCount, PlayerEnums.AddressType.NON_PLAYABLE));

        internal static string[] PlayerAddress = { playerAddress, Tools.SumHex(new string[] { playerAddress, gogOffset }) };
        internal static string[] ClubAddress   = { clubAddress,   Tools.SumHex(new string[] { clubAddress, gogOffset   }) };
        internal static string[] CountrySelectionAddress = { countrySelectionAddress, Tools.SumHex(new string[] { countrySelectionAddress, gogOffset }) };
    }
}
