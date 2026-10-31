using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using AnstossTrainer.Shared;

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

        // The rest of the name pool's bookkeeping, from the original build's own add-a-name
        // function (FUN_005b3380/FUN_005b3470) and its savegame writer (FUN_005ef380):
        //   namePoolEndPointerOffset: pointer to the first free byte after the last string.
        //   namePoolCountOffset: number of strings (UInt16) - also the index the next one gets.
        //   namePoolTableOffset: one char* per index into the pool. This is how the game itself
        //     finds a name; it's rebuilt from the pool on every load (FUN_005a61f0).
        //   namePoolCapacity: the pool's allocation size (FUN_005af480).
        //   namePoolTableCapacity: entries that fit before the next global (0xC1BA30).
        // Appending = string at the end pointer, table[count] = its address, count + 1, end pointer
        // past the string - exactly what the game does; the savegame then stores count and
        // end - base, so an appended name survives saving and reloading.
        private const uint namePoolEndPointerOffset = 0x7FDD5C;
        private const uint namePoolCountOffset      = 0x7FDD60;
        private const uint namePoolTableOffset      = 0x7FE540;
        private const int  namePoolCapacity         = 250000;
        private const int  namePoolTableCapacity    = (0x81BA30 - 0x7FE540) / 4;
        private const uint playerRecordTableOffset  = 0x516678;
        private const uint playerRecordStride       = 0xC8;

        // Offset within the same per-player record (above) that holds Level/"Stärke". Confirmed
        // live: for a Jugendspieler - whose other fields live in a transient "Dynamische
        // Mannschaft" display cache the game silently discards writes to (see HelpView.cs) -
        // editing this one byte changed the in-game value AND survived both a Tagesabschluss and
        // a full save-to-file + reload. Only this field plus the three above (+0/+2 name indices,
        // +4 PlayerId) are mapped so far; the rest of the 0xC8-byte record is unexplored.
        private const uint playerRecordLevelOffset  = 0xA;

        // Offset within the same per-player record that holds Age - but NOT as the age itself: it's
        // the player's BIRTH YEAR (UInt16), and the game derives Age = current in-game year - birth
        // year (FUN_00582a60/FUN_00583120). The current year is a live UInt16 at anstoss2.exe+426662
        // (per savegame, stable across restarts). The earlier byte-only "(year - 1792) - age"
        // encoding was the low byte of this same value (1792 = 0x700), so it only held for birth
        // years 1792-2047.
        private const uint playerRecordAgeOffset    = 0x8;
        private const uint ageReferenceYearOffset   = 0x426662;

        // Right next to the reference year above (4 bytes further into the same small "savegame
        // meta" struct): anstoss2.exe+426666 holds the number of active human trainers/"Mitspieler"
        // in the current savegame. Used as the authoritative count for the Trainer-Menü instead of
        // (or alongside) CoachController.GetActiveTrainers' fallback heuristic of scanning
        // Coach.MaxTrainers slots for a non-empty Firstname.
        private const uint activeTrainerCountOffset = 0x426666;

        // Human trainers/"Mitspieler" (found by decompiling the original build's
        // GetMitspieler/PutMitspieler pair, FUN_005b9df0/FUN_005ba2e0). Each Mitspieler ID (1-based,
        // so trainer slot N is ID N+1) has two copies at fixed module-relative addresses:
        //   coachSlotTableOffset + ID*coachSlotStride: the working copy the game (and the Trainer-Menü)
        //     reads and edits. Robin's 0x857800 for slot 0 is exactly this for ID 1.
        //   coachRecordTableOffset + ID*coachRecordStride: the saved record - the only copy written to
        //     the savegame file. GetMitspieler refreshes the working copy from it whenever the game
        //     starts using a trainer again, and PutMitspieler copies the working copy back when it's
        //     done - so a write to the working copy alone is lost if the game re-fetches before it
        //     writes back. Every field the Trainer-Menü edits sits at the same offset in both copies,
        //     except the competency table, which the record keeps in a separate per-ID array.
        // Unlike the club-pointer path (Settings.ClubAddress), these never go stale or null: the
        // club pointer is cleared whenever the game frees team slot 0.
        private const uint coachSlotTableOffset           = 0x455EA8;
        private const uint coachSlotStride                = 0x1958;
        private const uint coachRecordTableOffset         = 0x3A2730;
        private const uint coachRecordStride              = 0x2100;
        private const uint coachCompetencyRecordOffset    = 0x3ACAE2;
        private const uint coachCompetencyRecordStride    = 0x160;
        // The manager image (UInt16, 0 "kein Image" .. 16 "Trunkenbold", see CoachEnums.ManagerImage)
        // shares the competency table's per-ID block, 6 bytes before it. It has no working copy: the
        // game reads (FUN_005baf90) and assigns it (FUN_0062af30 -> FUN_005baf70) only here.
        private const uint coachImageRecordOffset         = 0x3ACADC;

        // Calendar values of the savegame (same meta block as ageReferenceYearOffset, plus one table
        // loaded with the savegame), which the player record stores injuries against - from the
        // original build's FUN_00582a60/FUN_00583120 and confirmed against a live savegame:
        //   startYearOffset: the year the savegame started in (Int16).
        //   dayCounterOffset: days since 1 January of that year (Int16) - "today".
        //   daysPerYearOffset: 365 (Int16), used when turning an injury end date back into days.
        //   calendarTableOffset: one entry per day counter value: Int16 year, Int16 day of that year.
        //   globalCounterOffset: the counter "verletzungsanfällig" is stored as an end value of.
        private const uint startYearOffset         = 0x426676;
        private const uint dayCounterOffset        = 0x426678;
        private const uint daysPerYearOffset       = 0x426728;
        private const uint calendarTableOffset     = 0x46069C;
        private const uint globalCounterOffset     = 0x42666C;

        // League structure, for checking a next-season club the way the game's season change does
        // (FUN_005b37b0/FUN_005a48f0), confirmed against a live savegame:
        //   leagueSystemByCountryOffset: one byte per country -> league system (0 = main country).
        //   leagueRangeTableOffset: Int16 per league system: its first league; the next entry ends it.
        //   leagueTableOffset/leagueStride: the leagues themselves; Int16 club count at +0, then the
        //     clubs' Int16 IDs from leagueClubIdsOffset on, league 0 being each system's top league.
        //   bonusCountryCountOffset/bonusCountryListOffset: Int16 count and Int16 list of the bonus
        //     countries (the same list LeagueSettings.ADDITIONAL_COUNTRY edits) - only their top
        //     league is accepted.
        private const uint leagueSystemByCountryOffset = 0x2C1918;
        private const uint leagueRangeTableOffset      = 0x2C1C58;
        private const uint leagueTableOffset           = 0x3CBBE8;
        private const uint leagueStride                = 0x9E8;
        private const uint leagueClubIdsOffset         = 0xA;
        private const uint bonusCountryCountOffset     = 0x42663E;
        private const uint bonusCountryListOffset      = 0x426644;

        // Job offers (original build). The daily processing FUN_005e5e00 first generates the week's
        // offers (FUN_00627390 -> FUN_00625c40, which also empties both offer lists) and right
        // after that presents them (call to FUN_006296f0 at jobOfferPresentCallSiteOffset). An offer
        // is one entry in an offer list, added by the game's __thiscall FUN_00574740(list, Mitspieler
        // ID, country, club ID, kind 3), which pops its four arguments itself (RET 0x10). List A is
        // presented with priority, so an offer added there right before the presentation call is
        // shown like any offer the game made itself - see JobOfferInjector.
        private const uint jobOfferPresentCallSiteOffset = 0x1E5FE9;   // E8 rel32 -> FUN_006296f0
        private const uint jobOfferPresentFunctionOffset = 0x2296F0;   // FUN_006296f0
        private const uint jobOfferAddFunctionOffset     = 0x174740;   // FUN_00574740
        private const uint jobOfferListOffset            = 0x38BA08;   // offer list A (0x78BA08)

        // Player transfers (original build). The daily processing FUN_0058f650 resolves the transfer
        // market through FUN_00636b60 at two call sites (two near-identical branches, each
        // "push ebp; call"); the game's own transfers there run, all cdecl: fetch the player
        // (FUN_00582740) and both clubs (FUN_00585b00(country, club, 0xFFFF)), build a contract
        // (FUN_005fac80(&contract, player, newClub), 0x10 bytes, years at +0xC), move the player
        // (FUN_005fb480(player, new, old, fee, &contract), or as a loan FUN_005fb710(player, new,
        // old, fee, buy price, &contract, kind 3 = with buy option)) and put everything back
        // (FUN_00585c50 per club, FUN_00582870 for the player). FUN_005fb480 asks FUN_0062b800
        // (day counter > 216) right at its start whether to postpone the move to the season end -
        // TransferInjector redirects that one call. Fees are in money units, the buy price as
        // stored = fee / moneyFactor (DAT_0078bbb8, i.e. x100 DM). See TransferInjector.
        private static readonly uint[] transferMarketCallSiteOffsets = { 0x18F8E4, 0x190002 };   // E8 rel32 -> FUN_00636b60
        private const uint transferMarketFunctionOffset   = 0x236B60;   // FUN_00636b60
        private const uint transferDeadlineCallSiteOffset = 0x1FB487;   // E8 rel32 -> FUN_0062b800 in FUN_005fb480
        private const uint transferDeadlineFunctionOffset = 0x22B800;   // FUN_0062b800
        private const uint transferFetchPlayerOffset      = 0x182740;   // FUN_00582740
        private const uint transferReleasePlayerOffset    = 0x182870;   // FUN_00582870
        private const uint transferFetchClubOffset        = 0x185B00;   // FUN_00585b00
        private const uint transferReleaseClubOffset      = 0x185C50;   // FUN_00585c50
        private const uint transferBuildContractOffset    = 0x1FAC80;   // FUN_005fac80
        private const uint transferMoveOffset             = 0x1FB480;   // FUN_005fb480
        private const uint transferLoanOffset             = 0x1FB710;   // FUN_005fb710
        private const uint moneyFactorOffset              = 0x38BBB8;   // DAT_0078bbb8 (Int32)
        // A club's squad (working copy): player pointers from +0x990, count Int16 at +0xE6, room
        // for 30 - the per-position counts follow at +0xA08 and FUN_005fbbe0 doesn't check.
        private const uint clubSquadOffset                = 0x990;
        private const uint clubSquadCountOffset           = 0xE6;
        private const int  clubSquadCapacity              = 30;

        // Club records of the league countries (FUN_00585ff0 fetches one, FUN_00586b80 puts it
        // back): record index = leagueBaseTableOffset[league system] + club ID, valid while the ID
        // is below leagueClubCountTableOffset[league system] (both Int16 per system). The season goal
        // the board measures the trainer against (club +0x10C while fetched) is byte +0x101 of the
        // record. A fetched club lives in one of six cache slots (FUN_00584ad0): byte 0 in use,
        // byte 1 country, byte 2 club ID, +8 pointer to the working copy (slot 0 is the club pointer
        // the Trainer reads the own club through), which is written back to the record when put.
        private const uint clubRecordTableOffset        = 0x487CA8;
        private const uint clubRecordStride             = 0x770;
        private const uint clubRecordSeasonGoalOffset   = 0x101;
        private const uint clubSeasonGoalOffset         = 0x10C;
        private const uint clubCacheTableOffset         = 0x400708;
        private const uint clubCacheStride              = 0xC;
        private const int  clubCacheSlots               = 6;
        private const uint clubCachePointerOffset       = 0x8;
        private const uint leagueBaseTableOffset        = 0x2C17F0;
        private const uint leagueClubCountTableOffset   = 0x2C1BE8;

        // Other builds (GOG run.exe, confirmed live on 23.10.2026) keep every fixed address above at
        // a constant distance from the original, but not the same one for all kinds of data:
        // runtime/savegame data (Mitspieler copies, club records and cache, league table, bonus
        // countries, calendar, name pool, offer list) moves like the calibrated persistent anchors
        // (GOG: +0x3140), the static tables in the initialised data section by another distance
        // (GOG: +0x3100), and the code was relinked (GOG: present function +0x4B90, add function
        // +0x22B0, call site +0x39A0). FixedAddresses therefore finds the static tables and the
        // job-offer code by these byte patterns, which must occur exactly once within
        // fixedAddressSearchWindow of the original address. The league bases are game data (first
        // league per league system); the code patterns are the two functions' first instructions
        // and the instructions right after the presentation call.
        private const uint fixedAddressSearchWindow = 0x20000;
        private static readonly byte[] leagueBasePattern       = { 0x00, 0x00, 0x7C, 0x00, 0xC7, 0x00, 0xF5, 0x00, 0x0D, 0x01, 0x27, 0x01 };
        private static readonly byte[] leagueRangePattern      = { 0x00, 0x00, 0x06, 0x00, 0x09, 0x00, 0x0B, 0x00, 0x0C, 0x00, 0x0D, 0x00 };
        private static readonly byte[] jobOfferAddPattern      = { 0x8B, 0x44, 0x24, 0x04, 0x66, 0x3D, 0x01, 0x00, 0x7C, 0x58, 0x66, 0x3D, 0x04, 0x00, 0x7F, 0x52, 0x48, 0x0F, 0xBF, 0xC0 };
        private static readonly byte[] jobOfferPresentPattern  = { 0x81, 0xEC, 0x74, 0x01, 0x00, 0x00, 0x33, 0xC0, 0x53, 0x89, 0x44, 0x24, 0x18, 0x55, 0x89, 0x44, 0x24, 0x20, 0x56, 0x89 };
        private static readonly byte[] jobOfferCallTrailPattern = { 0x6A, 0x02, 0xC7, 0x44, 0x24, 0x14, 0x02, 0x00, 0x00, 0x00 };

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
        // "Stammverein" of a loaned player: country (Byte) at +0x80, club ID (UInt16) at +0x82 (display
        // cache +0x9E/+0xA0). Set by a loan (FUN_005fb710) to the lending club, cleared by a normal
        // transfer; at the season end the player returns to (or is bought from) this club (FUN_00427ad0).
        private const uint playerRecordHomeCountryOffset = 0x80;
        private const uint playerRecordHomeClubOffset = 0x82;
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

        // The remaining Contract fields, taken from the game's own record <-> display-cache
        // converters in the original build (FUN_00582a60 unpacks a record, FUN_00583120 packs it
        // back). GoalsBonus/TransferFee are plain UInt16s right after ShowUpBonus, but two fields
        // aren't stored as shown: the record keeps the contract's END YEAR (display ContractDuration
        // = endYear - current year, clamped at 0) and the year the player JOINED the club (display
        // YearsInClub = current year - joinYear), both UInt16, using the same current year as Age
        // (ageReferenceYearOffset). That's why writing the displayed values there verbatim reverted.
        // ContractDetails directly follows the join year; Career sits further out on its own.
        private const uint playerRecordGoalsBonusOffset     = 0x88;
        private const uint playerRecordTransferFeeOffset    = 0x8A;
        // UInt32 "Kaufpreis": the fee last paid for the player, on a loan with a buy option the price
        // taken up at the season end (set by FUN_005fb710, paid by FUN_004278f0) - in units of the
        // game's money factor x 100 (DAT_0078bbb8), i.e. x100 DM.
        private const uint playerRecordBuyPriceOffset       = 0x8C;
        private const uint playerRecordContractEndYearOffset = 0x90;
        private const uint playerRecordJoinYearOffset       = 0x92;
        private const uint playerRecordContractDetailsOffset = 0x94;
        private const uint playerRecordCareerOffset         = 0xAC;

        // Constitution fields, from the same converters. InjuredDays isn't stored as days but as the
        // injury's END DATE (Int16 year + Int16 day of that year), next to the global-counter value
        // the injury started at; Vulnerable is stored as the global-counter value it ends at (0 =
        // not vulnerable). See GameCalendar for both conversions. The game clears start/end date
        // and playerRecordInjuryClearedFlagOffset together whenever no injury days are left.
        private const uint playerRecordInjuryStartOffset   = 0x2A;
        private const uint playerRecordInjuryEndYearOffset = 0x2C;
        private const uint playerRecordInjuryOffset        = 0x30;
        private const uint playerRecordVulnerableOffset    = 0x32;
        private const uint playerRecordBannedOffset        = 0x34;
        private const uint playerRecordInjuryClearedFlagOffset = 0xAD;

        // Season statistics (bytes), from the same converters. Their meaning comes from the game's
        // statistics screens (FUN_0045ceb0), which sort and show exactly these display-cache fields:
        // "Punkte Rot GelbRot Gelb" (+0x4B/+0x4C/+0x4A), "Eigentore" (+0x6A), Scorer = goals +
        // assists (+0x56 + +0x6B), Joker "Tore Einw." (+0x6C/+0x78) and "Einsätze Einw. Ausw."
        // (+0x77/+0x78/+0x79). The yellow cards (+0x36) are part of the constitution block above.
        private const uint playerRecordRedCardsOffset       = 0x37;
        private const uint playerRecordYellowRedCardsOffset = 0x38;
        private const uint playerRecordGoalsOffset          = 0x42;
        private const uint playerRecordOwnGoalsOffset       = 0x55;
        private const uint playerRecordAssistsOffset        = 0x56;
        private const uint playerRecordJokerGoalsOffset     = 0x57;
        private const uint playerRecordAppearancesOffset    = 0x62;
        private const uint playerRecordSubbedInOffset       = 0x63;
        private const uint playerRecordSubbedOutOffset      = 0x64;
        // League goals counted towards the season-end strength upgrade (display cache +0x166). The
        // game raises it together with the goals (+0x42) but reads only this one for the upgrade.
        private const uint playerRecordUpgradeGoalsOffset   = 0xBA;


        internal static uint NamePoolPointerOffset        { get => namePoolPointerOffset; }
        internal static uint NamePoolEndPointerOffset     { get => namePoolEndPointerOffset; }
        internal static uint NamePoolCountOffset          { get => namePoolCountOffset; }
        internal static uint NamePoolTableOffset          { get => namePoolTableOffset; }
        internal static int  NamePoolCapacity             { get => namePoolCapacity; }
        internal static int  NamePoolTableCapacity        { get => namePoolTableCapacity; }
        internal static uint PlayerRecordTableOffset      { get => playerRecordTableOffset; }
        internal static uint PlayerRecordStride           { get => playerRecordStride; }
        internal static uint PlayerRecordLevelOffset      { get => playerRecordLevelOffset; }
        internal static uint PlayerRecordAgeOffset        { get => playerRecordAgeOffset; }
        internal static uint AgeReferenceYearOffset       { get => ageReferenceYearOffset; }
        internal static uint ActiveTrainerCountOffset     { get => activeTrainerCountOffset; }
        internal static uint CoachSlotTableOffset         { get => coachSlotTableOffset; }
        internal static uint CoachSlotStride              { get => coachSlotStride; }
        internal static uint CoachRecordTableOffset       { get => coachRecordTableOffset; }
        internal static uint CoachRecordStride            { get => coachRecordStride; }
        internal static uint CoachCompetencyRecordOffset  { get => coachCompetencyRecordOffset; }
        internal static uint CoachCompetencyRecordStride  { get => coachCompetencyRecordStride; }
        internal static uint CoachImageRecordOffset       { get => coachImageRecordOffset; }
        internal static uint StartYearOffset              { get => startYearOffset; }
        internal static uint DayCounterOffset             { get => dayCounterOffset; }
        internal static uint DaysPerYearOffset            { get => daysPerYearOffset; }
        internal static uint CalendarTableOffset          { get => calendarTableOffset; }
        internal static uint GlobalCounterOffset          { get => globalCounterOffset; }
        internal static uint LeagueSystemByCountryOffset  { get => leagueSystemByCountryOffset; }
        internal static uint LeagueRangeTableOffset       { get => leagueRangeTableOffset; }
        internal static uint LeagueTableOffset            { get => leagueTableOffset; }
        internal static uint LeagueStride                 { get => leagueStride; }
        internal static uint LeagueClubIdsOffset          { get => leagueClubIdsOffset; }
        internal static uint BonusCountryCountOffset      { get => bonusCountryCountOffset; }
        internal static uint BonusCountryListOffset       { get => bonusCountryListOffset; }
        internal static uint JobOfferPresentCallSiteOffset { get => jobOfferPresentCallSiteOffset; }
        internal static uint JobOfferPresentFunctionOffset { get => jobOfferPresentFunctionOffset; }
        internal static uint JobOfferAddFunctionOffset     { get => jobOfferAddFunctionOffset; }
        internal static uint JobOfferListOffset            { get => jobOfferListOffset; }
        internal static uint[] TransferMarketCallSiteOffsets { get => (uint[])transferMarketCallSiteOffsets.Clone(); }
        internal static uint TransferMarketFunctionOffset  { get => transferMarketFunctionOffset; }
        internal static uint TransferDeadlineCallSiteOffset { get => transferDeadlineCallSiteOffset; }
        internal static uint TransferDeadlineFunctionOffset { get => transferDeadlineFunctionOffset; }
        internal static uint TransferFetchPlayerOffset     { get => transferFetchPlayerOffset; }
        internal static uint TransferReleasePlayerOffset   { get => transferReleasePlayerOffset; }
        internal static uint TransferFetchClubOffset       { get => transferFetchClubOffset; }
        internal static uint TransferReleaseClubOffset     { get => transferReleaseClubOffset; }
        internal static uint TransferBuildContractOffset   { get => transferBuildContractOffset; }
        internal static uint TransferMoveOffset            { get => transferMoveOffset; }
        internal static uint TransferLoanOffset            { get => transferLoanOffset; }
        internal static uint MoneyFactorOffset             { get => moneyFactorOffset; }
        internal static uint ClubSquadOffset               { get => clubSquadOffset; }
        internal static uint ClubSquadCountOffset          { get => clubSquadCountOffset; }
        internal static int  ClubSquadCapacity             { get => clubSquadCapacity; }
        internal static uint ClubRecordTableOffset         { get => clubRecordTableOffset; }
        internal static uint ClubRecordStride              { get => clubRecordStride; }
        internal static uint ClubRecordSeasonGoalOffset    { get => clubRecordSeasonGoalOffset; }
        internal static uint ClubSeasonGoalOffset          { get => clubSeasonGoalOffset; }
        internal static uint ClubCacheTableOffset          { get => clubCacheTableOffset; }
        internal static uint ClubCacheStride               { get => clubCacheStride; }
        internal static int  ClubCacheSlots                { get => clubCacheSlots; }
        internal static uint ClubCachePointerOffset        { get => clubCachePointerOffset; }
        internal static uint LeagueBaseTableOffset         { get => leagueBaseTableOffset; }
        internal static uint LeagueClubCountTableOffset    { get => leagueClubCountTableOffset; }
        internal static uint FixedAddressSearchWindow      { get => fixedAddressSearchWindow; }
        internal static byte[] LeagueBasePattern           { get => (byte[])leagueBasePattern.Clone(); }
        internal static byte[] LeagueRangePattern          { get => (byte[])leagueRangePattern.Clone(); }
        internal static byte[] JobOfferAddPattern          { get => (byte[])jobOfferAddPattern.Clone(); }
        internal static byte[] JobOfferPresentPattern      { get => (byte[])jobOfferPresentPattern.Clone(); }
        internal static byte[] JobOfferCallTrailPattern    { get => (byte[])jobOfferCallTrailPattern.Clone(); }
        internal static uint PlayerRecordPositionOffset   { get => playerRecordPositionOffset; }
        internal static uint PlayerRecordSkillsOffset     { get => playerRecordSkillsOffset; }
        internal static uint PlayerRecordPersonalityOffset { get => playerRecordPersonalityOffset; }
        internal static uint PlayerRecordCharacterOffset    { get => playerRecordCharacterOffset; }
        internal static uint PlayerRecordHealthOffset       { get => playerRecordHealthOffset; }
        internal static uint PlayerRecordSkinColorOffset   { get => playerRecordSkinColorOffset; }
        internal static uint PlayerRecordUnhappyOffset      { get => playerRecordUnhappyOffset; }
        internal static uint PlayerRecordHomeCountryOffset  { get => playerRecordHomeCountryOffset; }
        internal static uint PlayerRecordHomeClubOffset     { get => playerRecordHomeClubOffset; }
        internal static uint PlayerRecordSalaryOffset       { get => playerRecordSalaryOffset; }
        internal static uint PlayerRecordFormOffset         { get => playerRecordFormOffset; }
        internal static uint PlayerRecordConditionOffset    { get => playerRecordConditionOffset; }
        internal static uint PlayerRecordFreshnessOffset    { get => playerRecordFreshnessOffset; }
        internal static uint PlayerRecordNationalityOffset  { get => playerRecordNationalityOffset; }
        internal static uint PlayerRecordGoalsBonusOffset   { get => playerRecordGoalsBonusOffset; }
        internal static uint PlayerRecordTransferFeeOffset  { get => playerRecordTransferFeeOffset; }
        internal static uint PlayerRecordBuyPriceOffset     { get => playerRecordBuyPriceOffset; }
        internal static uint PlayerRecordContractEndYearOffset { get => playerRecordContractEndYearOffset; }
        internal static uint PlayerRecordContractDetailsOffset { get => playerRecordContractDetailsOffset; }
        internal static uint PlayerRecordCareerOffset       { get => playerRecordCareerOffset; }
        internal static uint PlayerRecordInjuryStartOffset  { get => playerRecordInjuryStartOffset; }
        internal static uint PlayerRecordInjuryEndYearOffset { get => playerRecordInjuryEndYearOffset; }
        internal static uint PlayerRecordInjuryOffset       { get => playerRecordInjuryOffset; }
        internal static uint PlayerRecordVulnerableOffset   { get => playerRecordVulnerableOffset; }
        internal static uint PlayerRecordBannedOffset       { get => playerRecordBannedOffset; }
        internal static uint PlayerRecordInjuryClearedFlagOffset { get => playerRecordInjuryClearedFlagOffset; }
        internal static uint PlayerRecordRedCardsOffset     { get => playerRecordRedCardsOffset; }
        internal static uint PlayerRecordYellowRedCardsOffset { get => playerRecordYellowRedCardsOffset; }
        internal static uint PlayerRecordGoalsOffset        { get => playerRecordGoalsOffset; }
        internal static uint PlayerRecordOwnGoalsOffset     { get => playerRecordOwnGoalsOffset; }
        internal static uint PlayerRecordAssistsOffset      { get => playerRecordAssistsOffset; }
        internal static uint PlayerRecordJokerGoalsOffset   { get => playerRecordJokerGoalsOffset; }
        internal static uint PlayerRecordAppearancesOffset  { get => playerRecordAppearancesOffset; }
        internal static uint PlayerRecordSubbedInOffset     { get => playerRecordSubbedInOffset; }
        internal static uint PlayerRecordSubbedOutOffset    { get => playerRecordSubbedOutOffset; }
        internal static uint PlayerRecordUpgradeGoalsOffset { get => playerRecordUpgradeGoalsOffset; }

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
