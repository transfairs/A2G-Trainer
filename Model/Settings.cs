using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Reverse-engineered, module-relative memory addresses/offsets the rest of the app
    /// reads and writes savegame data through. Each PlayerAddress/ClubAddress/... array holds
    /// both the original and the GOG build's address for the same field (GOG is offset by gogOffset).
    /// </summary>
    static class Settings
    {
        // Not const: tests flip this via reflection (restoring it in a finally block) to exercise
        // Logger.Debug/Info's write path, which is otherwise permanently dead in a release build -
        // the test assembly disables parallelization specifically so a temporary flip here can't
        // race another test (see A2G-Trainer-XP.Tests/AssemblyInfo.cs).
        private static bool  debug                    = false;
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
        // and across savegames sharing the same roster.
        //   namePoolPointerOffset: module-relative offset of a static pointer that always holds the
        //     current savegame's name-string-pool base address.
        //   playerRecordTableOffset/playerRecordStride: module-relative base and per-player stride of
        //     an array where each player's record holds, at +0/+2, the index of their Firstname/
        //     Lastname string within that pool.
        private const uint namePoolPointerOffset    = 0x7FDD58;
        private const uint playerRecordTableOffset  = 0x516678;
        private const uint playerRecordStride       = 0xC8;

        // Offset within the same per-player record (above) that holds Level/"Stärke". Confirmed
        // live: for a Jugendspieler - whose other fields live in a transient "Dynamische
        // Mannschaft" display cache the game silently discards writes to (see HelpView.cs) -
        // editing this one byte changed the in-game value AND survived both a Tagesabschluss and
        // a full save-to-file + reload. Only this field plus the three above (+0/+2 name indices,
        // +4 PlayerId) are mapped so far; the rest of the 0xC8-byte record is unexplored.
        private const uint playerRecordLevelOffset  = 0xA;

        // Offset within the same per-player record that holds Age - but NOT as the age itself.
        // The byte counts DOWN as age goes up, encoded relative to the current savegame's in-game
        // year (a live UInt16 at anstoss2.exe+426662, stable across restarts): byte = (currentYear
        // - ageReferenceYearEpoch) - age. The epoch was solved from two known savegames and holds
        // across both (2001 -> constant 209; a later savegame at 2003 -> constant 211). See
        // PlayerController.GetAgeEncodingConstant for where this gets read live instead of
        // hardcoded.
        private const uint playerRecordAgeOffset    = 0x8;
        private const uint ageReferenceYearOffset   = 0x426662;
        private const int  ageReferenceYearEpoch    = 1792;

        // Right next to the reference year above (4 bytes further into the same small "savegame
        // meta" struct): anstoss2.exe+426666 holds the number of active human trainers/"Mitspieler"
        // in the current savegame. Used as the authoritative count for the Trainer-Menü instead of
        // (or alongside) CoachController.GetActiveTrainers' fallback heuristic of scanning
        // Coach.MaxTrainers slots for a non-empty Firstname.
        private const uint activeTrainerCountOffset = 0x426666;

        // Offset within the same per-player record for Position (Hauptposition). The next two
        // bytes (+0xC, +0xD) are the two secondary positions, contiguous with the main one, so all
        // three can be written in one 3-byte call.
        private const uint playerRecordPositionOffset = 0xB;

        // Offset within the same per-player record for Skills (UInt16 bitmask). NegativeSkills
        // (also UInt16) follows immediately at +0x10, so both go out in one 4-byte call.
        private const uint playerRecordSkillsOffset = 0xE;

        // Offset within the same per-player record for Personality/"Eigenschaften" (UInt16
        // bitmask, same as the display cache). Character (byte) and Health/"Gesundheitszustand"
        // (byte) follow immediately at +0x14/+0x15, so all four bytes go out in one write.
        private const uint playerRecordPersonalityOffset = 0x12;
        private const uint playerRecordCharacterOffset    = 0x14;
        private const uint playerRecordHealthOffset        = 0x15;

        // Offset within the same per-player record for SkinColor. HairColor follows immediately
        // at +0x7, so both go out in one 2-byte call.
        private const uint playerRecordSkinColorOffset = 0x6;

        // Offset within the same per-player record for Unhappy (UInt16 bitmask). Happy (byte)
        // follows immediately at +0x1A, so all three bytes go out in one write.
        private const uint playerRecordUnhappyOffset = 0x18;

        // Offset within the same per-player record for Salary/"Gehalt" (UInt16) - much closer to
        // the start than the display cache's 0xB4, since this record has none of the display
        // cache's large gaps (unused/statistical bytes). ShowUpBonus/"Auflaufprämie" (also UInt16)
        // follows immediately at +0x86, so both go out in one 4-byte write.
        private const uint playerRecordSalaryOffset = 0x84;

        // The "matches display-cache order, no gaps" pattern that worked for everything above
        // does not hold past Health/Unhappy. The confirmed offsets here were found directly rather
        // than by extrapolating: Form at +0x1E, Condition at +0x21, Freshness at +0x22 (contiguous
        // with Condition), Nationality/"Land" at +0x25. Form being present at all was unexpected -
        // it had been assumed omitted like the club-context fields (ClubId/ClubCountry), but it
        // isn't.
        private const uint playerRecordFormOffset        = 0x1E;
        private const uint playerRecordConditionOffset   = 0x21;
        private const uint playerRecordFreshnessOffset   = 0x22;

        // Only the low 7 bits of this byte are the Country ID - a Russian player (Country=54=0x36)
        // was found encoded as 0xB6 (0x36 | 0x80), and writing the plain 0x36 still works. Bit 7's
        // meaning is unknown, so PlayerController masks it off on read and preserves whatever it
        // finds there on write (see ReadPersistentFields/WritePersistentNationality) rather than
        // guess.
        private const uint playerRecordNationalityOffset = 0x25;

        // Still unconfirmed/not implemented: the remaining Constitution fields (InjuredDays/
        // Injury/Vulnerable/BannedMatches/Doped/YellowCardsSeason) and the remaining Contract
        // fields (GoalsBonus/TransferFee/ContractDuration/ContractDetails/YearsInClub/Career).
        // Given Form/Condition/Freshness/Nationality turned out to be much further out than the
        // "no gaps after ShowUpBonus" pattern predicted, the Contract remainder is presumably
        // similarly offset rather than sitting right after ShowUpBonus - needs the same live
        // tracing done for everything else in this block.


        internal static uint NamePoolPointerOffset        { get => namePoolPointerOffset; }
        internal static uint PlayerRecordTableOffset      { get => playerRecordTableOffset; }
        internal static uint PlayerRecordStride           { get => playerRecordStride; }
        internal static uint PlayerRecordLevelOffset      { get => playerRecordLevelOffset; }
        internal static uint PlayerRecordAgeOffset        { get => playerRecordAgeOffset; }
        internal static uint AgeReferenceYearOffset       { get => ageReferenceYearOffset; }
        internal static int  AgeReferenceYearEpoch        { get => ageReferenceYearEpoch; }
        internal static uint ActiveTrainerCountOffset     { get => activeTrainerCountOffset; }
        internal static uint PlayerRecordPositionOffset   { get => playerRecordPositionOffset; }
        internal static uint PlayerRecordSkillsOffset     { get => playerRecordSkillsOffset; }
        internal static uint PlayerRecordPersonalityOffset { get => playerRecordPersonalityOffset; }
        internal static uint PlayerRecordCharacterOffset    { get => playerRecordCharacterOffset; }
        internal static uint PlayerRecordHealthOffset       { get => playerRecordHealthOffset; }
        internal static uint PlayerRecordSkinColorOffset   { get => playerRecordSkinColorOffset; }
        internal static uint PlayerRecordUnhappyOffset      { get => playerRecordUnhappyOffset; }
        internal static uint PlayerRecordSalaryOffset       { get => playerRecordSalaryOffset; }
        internal static uint PlayerRecordFormOffset         { get => playerRecordFormOffset; }
        internal static uint PlayerRecordConditionOffset    { get => playerRecordConditionOffset; }
        internal static uint PlayerRecordFreshnessOffset    { get => playerRecordFreshnessOffset; }
        internal static uint PlayerRecordNationalityOffset  { get => playerRecordNationalityOffset; }

        // gogOffset as a number, for the persistent anchors above (see PersistentLayout.CreateGogGuess).
        internal static uint   GogOffset         { get => Convert.ToUInt32(gogOffset, 16); }

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
