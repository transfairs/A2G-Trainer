using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
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
        // race another test (see A2G-Trainer.Tests/AssemblyInfo.cs).
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
        //     reads and edits. The 0x857800 found for slot 0 is exactly this for ID 1.
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

        // National coach (original build, confirmed live on GOG). The saved record keeps it at its
        // end, past the working copy's size: +0x20F0 the national team's country (UInt16, 0 = none),
        // +0x20F8 the annual salary (Int32, a twelfth paid monthly, FUN_00642e20) and +0x20FC the
        // bonus paid for every knockout round reached at a EM/WM (Int32, FUN_005abcf0). The working
        // copy only mirrors the country, at +0x1896 (FUN_005bab80). Two further copies of who
        // trains a national team: one 8-byte entry per Mitspieler ID (country byte, UInt16 200 = the
        // national team's "club ID"), and the national team's own record (club-record layout,
        // country * clubRecordStride) with the Mitspieler ID in both coach posts. The game sets all
        // of them in FUN_005cf1e0, clears them in FUN_005cf340 and rebuilds the latter two from the
        // record's country when it loads a savegame (FUN_005ba7c0).
        private const uint coachRecordNationalTeamOffset   = 0x20F0;
        private const uint coachRecordNationalSalaryOffset = 0x20F8;
        private const uint coachRecordNationalBonusOffset  = 0x20FC;
        // Wettbüro: one 0x20-byte bet block per league system in the saved record only (FUN_005badb0 /
        // FUN_005bae10), +0x18 champion bet stake (Int32), +0x1C club (UInt16), +0x1E odds (Int16).
        private const uint coachRecordBetOffset            = 0x1A74;
        private const uint coachRecordBetStride            = 0x20;
        private const uint coachBetChampionOffset          = 0x18;
        // The level and Fortbildung block: working copy +0x1820..+0x1833, record +0x1B14.. (FUN_005b9df0).
        private const uint coachSlotLevelBlockOffset       = 0x1820;
        private const uint coachRecordLevelBlockOffset     = 0x1B14;
        private const uint coachLevelBlockLength           = 0x14;
        // Event flags: working copy +0x1888 (byte, referee outbursts) and +0x188C (UInt32 bits), record
        // +0x20B8/+0x20BC. Series in points +0xB4, last level change +0xAA (half-week) / +0xAC (season);
        // same offsets in both copies (FUN_005b9df0, FUN_00628420).
        private const uint coachSlotFlagsOffset            = 0x1888;
        private const uint coachRecordFlagsOffset          = 0x20B8;
        private const uint coachFlagsLength                = 8;
        private const uint coachSlotSeriesOffset           = 0xB4;
        private const uint coachSlotLastLevelChangeOffset  = 0xAA;
        // Sabotage against a manager: record only, +0x1A60 + perpetrator ID * 4 (byte kind, byte weeks
        // left, Int16 player ID; FUN_004ea350, FUN_00417510 counts down, FUN_005ee810 carries it out).
        private const uint coachRecordSabotageOffset       = 0x1A60;
        // Naturalisation talks per manager (FUN_004cc220, savegame block 0x7DCFD0): 0x7DCF56 + ID * 0x7A,
        // Int16 talks this season, then 20 x (Int16 player ID, Int16 flags, Int16 day); flag bit 2 refused.
        private const uint naturalisationTableOffset       = 0x3DCF56;
        private const uint naturalisationTableStride       = 0x7A;
        private const int  naturalisationEntries           = 20;
        // "Stationen und Erfolge": one block per Mitspieler ID, saved record only (FUN_0057d390), with
        // the title counters (see CoachAchievements); the stations behind them (+0x1A8 count, from
        // +0x1AC) are documented in docs/pointer-investigation-guide.md.
        private const uint coachAchievementsOffset         = 0x3DB180;
        private const uint coachAchievementsStride         = 0x610;
        // The season index (Int16, 1 = the savegame's first season), right before the current year.
        private const uint seasonIndexOffset               = 0x426660;
        private const uint coachSlotNationalTeamOffset     = 0x1896;
        private const uint nationalCoachTableOffset        = 0x81DAB0;
        private const uint nationalCoachEntryStride        = 8;
        private const uint nationalTeamRecordTableOffset   = 0x3DD1C0;
        private const uint nationalTeamCoachPostOffset     = 0x86;
        private const ushort nationalTeamClubId            = 200;
        // The national team's squad uses the club record's squad fields (count, player IDs), but
        // holds at most 22 players (FUN_004c88d0). The game keeps it sorted by position and, for a
        // national team without a human coach, picks it anew in every week with a national match
        // (FUN_005d1650 -> FUN_005d0f20); a human national coach's squad is left alone.
        private const int  nationalTeamSquadCapacity       = 22;
        // Number of player records: the savegame load numbers them up to 0xBF3124 (FUN_005af600);
        // unused ones have no name indices.
        private const int  playerRecordCapacity            = 14990;

        // Calendar values of the savegame (same meta block as ageReferenceYearOffset, plus one table
        // loaded with the savegame), which the player record stores injuries against - from the
        // original build's FUN_00582a60/FUN_00583120 and confirmed against a live savegame:
        //   startYearOffset: the current calendar year (Int16), not the year the savegame started in:
        //   FUN_005a3c20 counts it up when December turns into January and resets dayCounterOffset.
        //   dayCounterOffset: days since 1 January of that year (Int16) - "today".
        //   daysPerYearOffset: 365 (Int16), used when turning an injury end date back into days.
        //   calendarTableOffset: one entry per day counter value: Int16 year, Int16 day of that year.
        //   globalCounterOffset: the day of the season (FUN_005901d0 counts it up, the season change
        //   resets it to 0); "verletzungsanfällig" is stored as an end value of it, the transfer list
        //   dates its entries with it.
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
        // Whether the club is listed on the stock market: working copy +0x115, set by its IPO
        // (FUN_004015f0), stored in the record at +0x10A (FUN_00586b80). Only listed clubs
        // have shares (FUN_00401aa0).
        private const uint clubRecordListedOffset       = 0x10A;
        private const uint clubListedOffset             = 0x115;
        // The club's finance block (working copy +0x154..+0x49C, record +0x50 further on): the
        // "Kalkulation" (FUN_004b65e0 tickets, FUN_004b21b0 law firm and departments), the slush fund
        // just before it (record +0x138), the loans (8 x 0x14) and the investments (6 x 0x10).
        private const uint clubSeasonTicketDiscountOffset = 0x270;
        private const uint clubSeasonTicketQuotaOffset  = 0x271;
        private const uint clubStandingPriceOffset      = 0x272;
        private const uint clubSeatPriceOffset          = 0x274;
        private const uint clubSecurityOffset           = 0x2AC;
        private const uint clubPlannedLawFirmOffset     = 0x370;
        private const uint clubPlannedStatisticsOffset  = 0x371;
        private const uint clubPlannedComputerOffset    = 0x372;
        private const uint clubLawFirmOffset            = 0x373;
        private const uint clubStatisticsOffset         = 0x374;
        private const uint clubComputerOffset           = 0x375;
        private const uint clubSlushFundOffset          = 0x144;
        private const uint clubSlushFundRateOffset      = 0x148;
        private const uint clubLoansOffset              = 0x15C;
        private const uint clubLoanStride               = 0x14;
        private const int  clubLoanSlots                = 8;
        private const uint clubInvestmentsOffset        = 0x1FC;
        private const uint clubInvestmentStride         = 0x10;
        private const int  clubInvestmentSlots          = 6;
        // Savegame-wide interest rates in basis points (Int16, block 0x826620): over the overdraft
        // limit, on a positive balance, overdraft, loan base, Tagesgeld (FUN_0058c030, FUN_0058ba80).
        private const uint interestOverLimitOffset      = 0x426704;
        private const uint interestDepositOffset        = 0x426706;
        private const uint interestOverdraftOffset      = 0x426708;
        private const uint interestLoanOffset           = 0x42670A;
        private const uint interestCallMoneyOffset      = 0x42670E;
        // Index of each kind of investment (Int32 x 5, start 1000, FUN_0058c1d0) and the corporation
        // tax in percent (byte; the byte behind it keeps last year's rate).
        private const uint investmentIndexOffset        = 0x426710;
        private const uint corporationTaxOffset         = 0x427BB1;
        // Calendar of the game state block: Int16 day of the month (hi word of 0x826670) and month.
        private const uint dayOfMonthOffset             = 0x426672;
        private const uint monthOffset                  = 0x426674;
        // More of the finance block: legal fees booked this season (Int32) and the overdraft limit
        // (Int16, units of 100,000, set at the season change by FUN_0063e220).
        private const uint clubLegalCostsOffset         = 0x384;
        private const uint clubOverdraftLimitOffset     = 0x39C;
        // The club's image with the public (byte 10..50, record +0x103, FUN_005b7950).
        private const uint clubPublicImageOffset        = 0x10E;
        // Licence procedure (record +0xF5): bit 7 licence refused (FUN_005b52a0 sets |= 0x90, a granted
        // licence clears bit 7). Association requirements (record +0xF6), set anew at every season end
        // from the club's wealth (FUN_005b5820), each bit a requirement - see Club.Requirements. Their
        // limits sit in the finance block: +0x494 debt (byte, x 1,000,000), +0x495 spectators (byte, x 1,000),
        // +0x496 salaries (UInt16, x 100,000), +0x498 transfers (Int16, x 100,000; FUN_005b5b40 checks them;
        // units read live: 0x78BB48 = 1,000,000, 0x78BB10 = 100,000 DM).
        private const uint clubLicenceOffset            = 0xFF;
        private const uint clubRequirementsOffset       = 0x100;
        private const uint clubDebtLimitOffset          = 0x494;
        private const uint clubSpectatorLimitOffset     = 0x495;
        private const uint clubSalaryLimitOffset        = 0x496;
        private const uint clubTransferLimitOffset      = 0x498;
        // Locks. Bribery (record +0x13E): bit 7 referee tried, bit 5 opponent asked, bit 4 opponent paid;
        // the game clears bits 4-7 after every league match (FUN_005f5850). Short training camp before
        // the next match (UInt32, record +0x6FC): bit 0x800 booked, bits 8-9 camps this season
        // (FUN_0052d8f0, FUN_0052dba0). Requests to the sponsor (byte, only the first one pays,
        // FUN_0050b7e0; kept with the human manager's club data, not in the record). Scouting spent
        // this season (Int32, finance block; from 2 Mio. DM no more, FUN_004855a0).
        private const uint clubBriberyOffset            = 0x14A;
        private const uint clubShortCampOffset          = 0x1074;
        private const uint clubSponsorRequestsOffset    = 0xFC;
        private const uint clubScoutSpendingOffset      = 0x2FC;
        // The running matches (see LiveMatch): table 0x82E460, 0x26C per match, the clock 0xC24E10
        // (+2 Int16 number of matches, +8 Int16 minute). Line-up (11 x Int16) and bench (7 x Int16)
        // in the club's working copy, record +0x148/+0x15E. A player's match flags in his working
        // copy +0x84 (1 starting, 3 came on, 4 off, 8/0x10/0x20 cards).
        private const uint matchTableOffset             = 0x42E460;
        private const uint matchStride                  = 0x26C;
        private const uint matchClockOffset             = 0x824E10;
        private const uint clubLineUpOffset             = 0x8FC;
        private const uint clubBenchOffset              = 0x912;
        private const uint playerMatchFlagsOffset       = 0x84;
        // The club's stock block, in the record only (FUN_0057d4b0): +0 shares issued (Int32),
        // +4 price (Int16), +0x18 ten weekly prices for the chart.
        private const uint clubRecordStockOffset        = 0x10C;
        // Static tables in the writable data section (module-relative, original build): law firm fees
        // per case, 5 x {red card, other} Int16 in thousands of DM; department costs, statistics
        // Int16[3] in units of 10,000 and computer Int16[2] (at +8) in units of 100,000.
        private const uint lawFirmFeeTableOffset        = 0x2C1F10;
        private const uint departmentCostTableOffset    = 0x2C1EE0;
        // The editor's club settings (loaded from LandXxxx.sav by FUN_005b0d60, stadium from
        // StadXxxx.sav by FUN_005b25d0). Short name, chant and fan name exist only in the record and
        // are read from there (FUN_00589050, FUN_00589210); the other fields are copied to the working copy at
        // the AddressPresets.OWN_CLUB offsets and back, so both are written.
        private const uint clubRecordShortNameOffset    = 0x14;
        private const uint clubRecordChantOffset        = 0x18;
        private const uint clubRecordFanNameOffset      = 0x4A;
        private static readonly Dictionary<ClubEnums.AddressKey, uint> clubRecordMirrorOffsets = new Dictionary<ClubEnums.AddressKey, uint>
        {
            { ClubEnums.AddressKey.Jersey,        0xE0 },
            { ClubEnums.AddressKey.FanTurnout,    0xE6 },
            { ClubEnums.AddressKey.FanType,       0xE7 },
            { ClubEnums.AddressKey.Article,       0xEA },
            { ClubEnums.AddressKey.FanFriendship, 0xEB },
            { ClubEnums.AddressKey.ArchRival,     0xEC },
            { ClubEnums.AddressKey.Board,         0xED },
            { ClubEnums.AddressKey.Preposition,   0xF0 },
            { ClubEnums.AddressKey.Town,          0x509 },
            { ClubEnums.AddressKey.MapX,          0x702 },
            { ClubEnums.AddressKey.MapY,          0x704 },
            { ClubEnums.AddressKey.Association,   0xF7 },
        };
        private const uint clubCacheTableOffset         = 0x400708;
        private const uint clubCacheStride              = 0xC;
        private const int  clubCacheSlots               = 6;
        private const uint clubCachePointerOffset       = 0x8;
        // The parts fetched into that working copy (UInt16 mask of the fetch flags, FUN_00585ff0).
        private const uint clubCachePartsOffset         = 0x4;
        private const uint leagueBaseTableOffset        = 0x2C17F0;
        private const uint leagueClubCountTableOffset   = 0x2C1BE8;

        // Who plays for a club, from the original build's fetch FUN_00585ff0 -> FUN_00581b10 and put
        // FUN_00581e60: the record keeps the squad as player IDs (Int16 count +0xDE, IDs from +0x570)
        // and the amateurs (byte count +0x5BD, IDs from +0x5BE, room for 5); a fetched club's working
        // copy keeps pointers to the players' working copies instead (squad above, amateurs: byte
        // count +0xA19, pointers from +0xA1C). Club ID and country are bytes +0xF4/+0xF8 of the record.
        private const uint clubRecordSquadCountOffset   = 0xDE;
        private const uint clubRecordSquadOffset        = 0x570;
        private const uint clubRecordAmateurCountOffset = 0x5BD;
        private const uint clubRecordAmateursOffset     = 0x5BE;
        private const uint clubRecordIdOffset           = 0xF4;
        private const uint clubRecordCountryOffset      = 0xF8;
        private const uint clubAmateurCountOffset       = 0xA19;
        private const uint clubAmateursOffset           = 0xA1C;
        private const int  clubAmateurCapacity          = 5;
        // Youth players (FUN_00584780 fetches, FUN_005848b0 puts back): one 0x1A-byte entry per club
        // record index; bytes +1/+2/+3 count the A/B/C youth, their player IDs follow from +4 (4),
        // +0xC (4) and +0x14 (3).
        private const uint youthTableOffset             = 0x476FB0;
        private const uint youthStride                  = 0x1A;
        private static readonly uint[] youthIdOffsets   = { 0x4, 0xC, 0x14 };
        private static readonly int[] youthCapacities   = { 4, 4, 3 };
        // The players' working copies (FUN_005818b0 hands one out, FUN_00581a00 frees it): 300 slots
        // of 0x178 bytes, byte 0 in use, the working copy from +4 starting with the player ID. Its
        // layout is AddressPresets.OWN_PLAYERS; the game packs it back into the record (FUN_00582a30).
        private const uint playerCacheTableOffset       = 0x3AFD08;
        private const uint playerCacheStride            = 0x178;
        private const int  playerCacheSlots             = 300;
        private const uint playerCacheCopyOffset        = 0x4;

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

        // Interviews (original build). The interview engine (pointer DAT_0077f898, its question list
        // loaded from TEXTE\inttext.crn by FUN_005983b0) works on a heap copy of the interview context
        // 0x77F960 (FUN_005995b0) and stores the two effect pairs of the chosen answer there
        // (FUN_00599040, copy +0x14..+0x20). FUN_004f8a90 then applies the pairs with FUN_004f8a00 -
        // but from 0x77F960 itself, whose pairs stay 0, so no answer has any effect (2007 CD checked
        // live). InterviewEffectsFix redirects that call (push 0x77F960, E8 rel32) to a stub that copies
        // the pairs over first. The pattern is FUN_004f8a00's first instructions (GOG/2007: +0x2230).
        // The pairs' codes don't all match FUN_004f87f0's cases: the texts use 10 for the team's
        // morale (no case) and 12 for a fine in DM (20000/100000; case 12 is the morale, FUN_005ffa50:
        // club +0x9C += value, 0..200). The fix sends 10 to case 12 and redirects case 12's call
        // (GOG/2007: call +0x2230, function +0x4960) to book a marked fine as the game books its own:
        // added to the club's actual other costs (+0x38C, money units = DM * moneyFactor / 100).
        private const uint interviewEngineOffset        = 0x37F898;   // DAT_0077f898
        private const uint interviewContextOffset       = 0x37F960;   // DAT_0077f960
        private const uint interviewApplyCallSiteOffset = 0x0F8AB0;   // E8 rel32 -> FUN_004f8a00 in FUN_004f8a90
        private const uint interviewApplyFunctionOffset = 0x0F8A00;   // FUN_004f8a00
        private const uint interviewMoraleCallSiteOffset = 0x0F899C;  // E8 rel32 -> FUN_005ffa50 in FUN_004f87f0, case 12
        private const uint interviewMoraleFunctionOffset = 0x1FFA50;  // FUN_005ffa50
        private const uint clubOtherCostsOffset         = 0x38C;      // club working copy, Int32 money units
        private static readonly byte[] interviewApplyPattern = { 0x53, 0x55, 0x56, 0x8B, 0x74, 0x24, 0x10, 0x57, 0x66, 0x8B, 0x46, 0x74, 0x50, 0xE8 };
        private static readonly byte[] interviewMoralePattern = { 0x8B, 0x4C, 0x24, 0x04, 0x8B, 0x44, 0x24, 0x08, 0x66, 0x0F, 0xB6, 0x91, 0x9C, 0x00, 0x00, 0x00, 0x03, 0xC2, 0x66, 0x3D, 0xC8, 0x00 };
        private static readonly byte[] interviewMoraleCallLeadPattern = { 0x8B, 0x4C, 0x24, 0x50, 0x8B, 0x54, 0x24, 0x5C, 0x51, 0x52 };
        private static readonly byte[] interviewMoraleCallTrailPattern = { 0x83, 0xC4, 0x08, 0x5F, 0x5E, 0x83, 0xC4, 0x40, 0xC3 };

        // Goal limits of the match engine (original build). FUN_005f2850 plays one chance per team and
        // minute and lets a team score only while it has fewer than 6 goals up to minute 45 and fewer
        // than 10 after: cmp word [clock+8], 45 / setg r8 / dec r32 / and r, -4 / add r, 10. From 7
        // goals on, it also doubles the random number the chance must beat (home: cmp word [esi+0xE], 6
        // / jle, away: cmp dx, 6 / jle), halving the team's chances. GoalLimitPatch writes
        // "mov r32, 15" (the most a result nibble holds) over the limit and turns both jle into jmp. The patterns are the
        // instructions right before each patched byte; the 2007 CD run.exe (packed on disk, read live
        // on 04.11.2026) and GOG have them 0x4968..0x496A further on, with ecx instead of eax for the limit.
        private const uint goalLimitSiteOffset     = 0x1F2BCD;   // cmp word [edi+8], 0x2D ... setg
        private const uint goalHomeBrakeSiteOffset = 0x1F2C0C;   // cmp word [esi+0xE], 6 ... jle
        private const uint goalAwayBrakeSiteOffset = 0x1F2E6C;   // mov dx, [esi+0x10] ... cmp dx, 6 ... jle
        // The patterns themselves live in GoalLimitBytes.
        private static readonly byte[] goalLimitPattern     = GoalLimitBytes.LimitPattern;
        private static readonly byte[] goalHomeBrakePattern = GoalLimitBytes.HomeBrakePattern;
        private static readonly byte[] goalAwayBrakePattern = GoalLimitBytes.AwayBrakePattern;

        // National coach (original build). The game's own takeover FUN_005cf1e0(ID, country), leave
        // FUN_005cf340(ID) and friendly candidates FUN_005cf3e0() (all cdecl) also set up the national
        // team's line-up and tactics and the three friendly dates' opponents to choose from, which the
        // game otherwise only does at the season change - NationalCoachInjector has the game call them
        // from the daily processing, at the call of the offer generation FUN_00627390 right before the
        // job-offer presentation call. The friendlies live in one block per Mitspieler ID: 3 dates x 3
        // candidates (6 bytes each: country, 0, UInt16 200, UInt16 home/away) from +0, then the chosen
        // opponents. Found in other builds like the job-offer code (2007 CD: +0x3740); the leave
        // pattern ends in the absolute address of the national coach table, which moves with the
        // runtime data.
        private const uint nationalTakeFunctionOffset       = 0x1CF1E0;
        private const uint nationalLeaveFunctionOffset      = 0x1CF340;
        private const uint nationalCandidatesFunctionOffset = 0x1CF3E0;
        private const uint offerGenerateCallSiteOffset      = 0x1E5FE4;   // E8 rel32 -> FUN_00627390
        private const uint offerGenerateFunctionOffset      = 0x227390;
        private const uint nationalFriendlyBlockOffset      = 0x3D3AA8;
        private const uint nationalFriendlyBlockStride      = 0x208;
        private const int  nationalFriendlyCandidatesLength = 0x36;
        // Behind the candidates: the chosen opponent of each friendly (3 x 6 bytes: country, 0, UInt16
        // 200, UInt16 home/away with 0 = at home), then each result (UInt16, low byte 0xFF = not played).
        private const uint nationalFriendlyChosenOffset     = 0x36;
        private const uint nationalFriendlyResultsOffset    = 0x48;
        private static readonly byte[] nationalTakePattern       = { 0x83, 0xEC, 0x64, 0x53, 0x8B, 0x5C, 0x24, 0x70, 0x66, 0x83, 0xFB, 0x01, 0x0F, 0x8C, 0x49, 0x01, 0x00, 0x00, 0x66, 0x83, 0xFB, 0x4B, 0x0F, 0x8F, 0x3F, 0x01, 0x00, 0x00 };
        private static readonly byte[] nationalLeavePatternHead  = { 0x8B, 0x54, 0x24, 0x04, 0x53, 0x0F, 0xBF, 0xCA, 0xC1, 0xE1, 0x03, 0x33, 0xDB, 0x56, 0x8A, 0x81 };
        private static readonly byte[] nationalCandidatesPattern = { 0x81, 0xEC, 0xA4, 0x02, 0x00, 0x00, 0x53, 0x55, 0x56, 0x57, 0xE8 };

        // Co-trainer and goalkeeper trainer (original build). The club keeps their IDs at +0x82/+0x84
        // (0 = none). A co-trainer is an entry of the trainer pool (getter FUN_0057d620, 1000 entries),
        // a goalkeeper trainer one of the goalkeeper trainer table (getter FUN_0057d680, 101 entries),
        // 32 bytes each: +0/+2 name pool indices, +4 competence (the co-trainer salary table
        // DAT_006ab748 ends at 15), +6 reputation, +7 age, +8 contract years left (counted down at the
        // season change, 0 lets him go), +0xC salary per year. Other builds: both getters - the only
        // two with this body near the original - are found by pattern and the table addresses read
        // from their code (2007 CD: both +0x3140, unlike the club data).
        private const uint coachPoolTableOffset           = 0x47FFA8;
        private const uint goalkeeperCoachTableOffset     = 0x7F31E8;
        private const uint coachPoolGetterOffset          = 0x17D625;   // body after the leading call
        private const uint goalkeeperCoachGetterOffset    = 0x17D685;
        private const int  coachPoolCapacity              = 1000;
        private const int  goalkeeperCoachCapacity        = 101;
        private const uint staffEntryStride               = 0x20;
        private const uint staffCompetenceOffset          = 0x04;
        private const uint staffContractOffset            = 0x08;
        private const uint staffSalaryOffset              = 0x0C;
        private const byte maxStaffCompetence             = 15;
        // test eax,eax / je / push edi / mov edi,[esp+0C] / push esi / mov ecx,8 / movsx esi,[esp+0C] /
        // shl esi,5 / add esi, <table>
        private static readonly byte[] staffGetterPattern = { 0x85, 0xC0, 0x74, 0x1D, 0x57, 0x8B, 0x7C, 0x24, 0x0C, 0x56, 0xB9, 0x08, 0x00, 0x00, 0x00, 0x0F, 0xBF, 0x74, 0x24, 0x0C, 0xC1, 0xE6, 0x05, 0x81, 0xC6 };

        // The transfer code, found the same way (GOG and the 2007 CD, confirmed on 01.11.2026: fetch
        // player/club, release player/club +0x22C0/+0x22A0, contract, move and loan +0x4960, deadline
        // check +0x4B80, market +0x4B40, market calls +0x22A0). The patterns are the functions' first
        // instructions up to the first operand that differs between builds. The deadline check and the
        // market function read the global counter (+0 / +2) through an absolute operand, which moves
        // with the runtime data - their patterns are completed with that address during the search
        // (head, address, tail). The market calls are "push ebp; call" followed by the same read; the
        // deadline call is the move function's first call, which gives the move function as well.
        private static readonly byte[] transferFetchPlayerPattern    = { 0x56, 0x57, 0x8B, 0x7C, 0x24, 0x0C, 0x66, 0x85, 0xFF, 0x74, 0x57, 0x57 };
        // Ends in a call of the function right behind it, which moves along with it.
        private static readonly byte[] transferReleasePlayerPattern  = { 0x8B, 0x44, 0x24, 0x04, 0x66, 0x8B, 0x08, 0x51, 0x50, 0xE8, 0x12, 0x00, 0x00, 0x00, 0x83, 0xC4, 0x08, 0xC3 };
        private static readonly byte[] transferFetchClubPattern      = { 0x53, 0x56, 0x57, 0x8B, 0x7C, 0x24, 0x14, 0x66, 0x81, 0xFF, 0xC8, 0x00, 0x75, 0x16, 0x8B, 0x44, 0x24, 0x18, 0x8B, 0x4C, 0x24, 0x10, 0x50, 0x51 };
        private static readonly byte[] transferReleaseClubPattern    = { 0x8B, 0x44, 0x24, 0x04, 0x68, 0xFF, 0xFF, 0x00, 0x00, 0x66, 0x0F, 0xB6, 0x88, 0x02, 0x01, 0x00, 0x00, 0x66, 0x0F, 0xB6, 0x90, 0x03, 0x01, 0x00, 0x00, 0x51, 0x52, 0x50 };
        private static readonly byte[] transferBuildContractPattern  = { 0x55, 0x56, 0x57, 0x8B, 0x7C, 0x24, 0x14, 0x66, 0x0F, 0xB6, 0x47, 0x24, 0x66, 0x0F, 0xB6, 0x4F, 0x23, 0x50, 0x51 };
        private static readonly byte[] transferLoanPattern           = { 0x53, 0x55, 0x8B, 0x6C, 0x24, 0x18, 0x8B, 0x5C, 0x24, 0x10, 0x56, 0x8B, 0x74, 0x24, 0x10, 0x57, 0x8B, 0x7C, 0x24, 0x1C, 0x55, 0x53, 0x8A, 0x87 };
        // xor eax, eax; cmp word [global counter], 217 / setge al; ret
        private static readonly byte[] transferDeadlinePatternHead   = { 0x33, 0xC0, 0x66, 0x81, 0x3D };
        private static readonly byte[] transferDeadlinePatternTail   = { 0xD9, 0x00, 0x0F, 0x9D, 0xC0, 0xC3 };
        // mov ax, [global counter + 2] / sub esp, 0x64; ...
        private static readonly byte[] transferMarketPatternHead     = { 0x66, 0xA1 };
        private static readonly byte[] transferMarketPatternTail     = { 0x83, 0xEC, 0x64, 0x8D, 0x4C, 0x24, 0x1C, 0x56, 0x66, 0x8B, 0x74, 0x24, 0x6C, 0x66, 0x03, 0xC6, 0x50, 0x51 };
        private static readonly byte[] transferMarketCallLeadPattern = { 0x55 };
        // after "mov ax, [global counter + 2]"
        private static readonly byte[] transferMarketCallTrailTail   = { 0x83, 0xC4, 0x04, 0x66, 0x03, 0xC5, 0x50 };
        private static readonly byte[] transferDeadlineCallLeadPattern  = { 0x83, 0xEC, 0x2C, 0x53, 0x55, 0x56, 0x57 };
        private static readonly byte[] transferDeadlineCallTrailPattern = { 0x85, 0xC0, 0x0F, 0x84, 0xBE, 0x00, 0x00, 0x00, 0x8B, 0x7C, 0x24, 0x40 };

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
        // Shirt number (display +0x3A). The game keeps the numbers in use in 100-entry tables
        // (FUN_005e54d0/FUN_005e56c0), so only 1-99 are valid; its own "Rückennummern" dialog
        // refuses 0 and duplicates within a squad (FUN_004e0b50).
        private const uint playerRecordJerseyOffset      = 0x1F;
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
        // The league ban itself (FUN_00582a60 unpacks, FUN_00583120 packs): +0x39 matches left in
        // bits 0-4 plus flag bits 5-7, +0x3A the reason (bit 7 red card, bit 6 second yellow,
        // bit 5 yellow cards, bits 0-4 offence index), +0xBE the season day the ban ends. +0x34
        // ("banned for the next match") is only derived from them before each match (FUN_0057c600).
        private const uint playerRecordLeagueBanOffset     = 0x39;
        private const uint playerRecordLeagueBanEndOffset  = 0xBE;
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
        // National team caps (working copy +0x8E), raised after every international match played
        // (FUN_005ce720); the naturalisation list (FUN_004cbe30) only shows players without any.
        private const uint playerRecordCapsOffset           = 0x7B;
        // The transfer list: a global list, not a record field (FUN_0062be30 adds, FUN_0062c0f0
        // removes). Int16 count at +0, then up to 250 entries of 0x1C bytes from +8, each starting
        // with the player ID (Int16). Career bit 4 mirrors it per player.
        private const uint transferListOffset               = 0x8A2280;
        private const uint transferListEntriesOffset        = 8;
        private const uint transferListEntryStride          = 0x1C;
        private const int  transferListCapacity             = 250;


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
        internal static uint CoachRecordNationalTeamOffset   { get => coachRecordNationalTeamOffset; }
        internal static uint CoachRecordNationalSalaryOffset { get => coachRecordNationalSalaryOffset; }
        internal static uint CoachRecordNationalBonusOffset  { get => coachRecordNationalBonusOffset; }
        internal static uint CoachRecordBetOffset            { get => coachRecordBetOffset; }
        internal static uint CoachRecordBetStride            { get => coachRecordBetStride; }
        internal static uint CoachBetChampionOffset          { get => coachBetChampionOffset; }
        internal static uint CoachSlotLevelBlockOffset       { get => coachSlotLevelBlockOffset; }
        internal static uint CoachRecordLevelBlockOffset     { get => coachRecordLevelBlockOffset; }
        internal static uint CoachLevelBlockLength           { get => coachLevelBlockLength; }
        internal static uint CoachSlotFlagsOffset            { get => coachSlotFlagsOffset; }
        internal static uint CoachRecordFlagsOffset          { get => coachRecordFlagsOffset; }
        internal static uint CoachFlagsLength                { get => coachFlagsLength; }
        internal static uint CoachSlotSeriesOffset           { get => coachSlotSeriesOffset; }
        internal static uint CoachSlotLastLevelChangeOffset  { get => coachSlotLastLevelChangeOffset; }
        internal static uint CoachRecordSabotageOffset       { get => coachRecordSabotageOffset; }
        internal static uint NaturalisationTableOffset       { get => naturalisationTableOffset; }
        internal static uint NaturalisationTableStride       { get => naturalisationTableStride; }
        internal static int  NaturalisationEntries           { get => naturalisationEntries; }
        internal static int  NaturalisationTableLength       { get => 2 + naturalisationEntries * 6; }
        internal static uint CoachAchievementsOffset         { get => coachAchievementsOffset; }
        internal static uint CoachAchievementsStride         { get => coachAchievementsStride; }
        internal static uint SeasonIndexOffset               { get => seasonIndexOffset; }
        internal static uint CoachSlotNationalTeamOffset     { get => coachSlotNationalTeamOffset; }
        internal static uint NationalCoachTableOffset        { get => nationalCoachTableOffset; }
        internal static uint NationalCoachEntryStride        { get => nationalCoachEntryStride; }
        internal static uint NationalTeamRecordTableOffset   { get => nationalTeamRecordTableOffset; }
        internal static uint NationalTeamCoachPostOffset     { get => nationalTeamCoachPostOffset; }
        internal static ushort NationalTeamClubId            { get => nationalTeamClubId; }
        internal static int  NationalTeamSquadCapacity       { get => nationalTeamSquadCapacity; }
        internal static int  PlayerRecordCapacity            { get => playerRecordCapacity; }
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
        internal static uint InterviewEngineOffset         { get => interviewEngineOffset; }
        internal static uint InterviewContextOffset        { get => interviewContextOffset; }
        internal static uint InterviewApplyCallSiteOffset  { get => interviewApplyCallSiteOffset; }
        internal static uint InterviewApplyFunctionOffset  { get => interviewApplyFunctionOffset; }
        internal static byte[] InterviewApplyPattern       { get => (byte[])interviewApplyPattern.Clone(); }
        internal static uint InterviewMoraleCallSiteOffset { get => interviewMoraleCallSiteOffset; }
        internal static uint InterviewMoraleFunctionOffset { get => interviewMoraleFunctionOffset; }
        internal static uint ClubOtherCostsOffset          { get => clubOtherCostsOffset; }
        internal static byte[] InterviewMoralePattern      { get => (byte[])interviewMoralePattern.Clone(); }
        internal static byte[] InterviewMoraleCallLeadPattern  { get => (byte[])interviewMoraleCallLeadPattern.Clone(); }
        internal static byte[] InterviewMoraleCallTrailPattern { get => (byte[])interviewMoraleCallTrailPattern.Clone(); }
        internal static uint GoalLimitSiteOffset           { get => goalLimitSiteOffset; }
        internal static uint GoalHomeBrakeSiteOffset       { get => goalHomeBrakeSiteOffset; }
        internal static uint GoalAwayBrakeSiteOffset       { get => goalAwayBrakeSiteOffset; }
        internal static byte[] GoalLimitPattern            { get => (byte[])goalLimitPattern.Clone(); }
        internal static byte[] GoalHomeBrakePattern        { get => (byte[])goalHomeBrakePattern.Clone(); }
        internal static byte[] GoalAwayBrakePattern        { get => (byte[])goalAwayBrakePattern.Clone(); }
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
        internal static uint ClubRecordListedOffset        { get => clubRecordListedOffset; }
        internal static uint ClubListedOffset              { get => clubListedOffset; }
        internal static uint ClubSeasonTicketDiscountOffset { get => clubSeasonTicketDiscountOffset; }
        internal static uint ClubSeasonTicketQuotaOffset   { get => clubSeasonTicketQuotaOffset; }
        internal static uint ClubStandingPriceOffset       { get => clubStandingPriceOffset; }
        internal static uint ClubSeatPriceOffset           { get => clubSeatPriceOffset; }
        internal static uint ClubSecurityOffset            { get => clubSecurityOffset; }
        internal static uint ClubPlannedLawFirmOffset      { get => clubPlannedLawFirmOffset; }
        internal static uint ClubPlannedStatisticsOffset   { get => clubPlannedStatisticsOffset; }
        internal static uint ClubPlannedComputerOffset     { get => clubPlannedComputerOffset; }
        internal static uint ClubLawFirmOffset             { get => clubLawFirmOffset; }
        internal static uint ClubStatisticsOffset          { get => clubStatisticsOffset; }
        internal static uint ClubComputerOffset            { get => clubComputerOffset; }
        internal static uint ClubSlushFundOffset           { get => clubSlushFundOffset; }
        internal static uint ClubSlushFundRateOffset       { get => clubSlushFundRateOffset; }
        internal static uint ClubLoansOffset               { get => clubLoansOffset; }
        internal static uint ClubLoanStride                { get => clubLoanStride; }
        internal static int  ClubLoanSlots                 { get => clubLoanSlots; }
        internal static uint ClubInvestmentsOffset         { get => clubInvestmentsOffset; }
        internal static uint ClubInvestmentStride          { get => clubInvestmentStride; }
        internal static int  ClubInvestmentSlots           { get => clubInvestmentSlots; }
        internal static uint InterestOverLimitOffset       { get => interestOverLimitOffset; }
        internal static uint InterestDepositOffset         { get => interestDepositOffset; }
        internal static uint InterestOverdraftOffset       { get => interestOverdraftOffset; }
        internal static uint InterestLoanOffset            { get => interestLoanOffset; }
        internal static uint InterestCallMoneyOffset       { get => interestCallMoneyOffset; }
        internal static uint InvestmentIndexOffset         { get => investmentIndexOffset; }
        internal static uint CorporationTaxOffset          { get => corporationTaxOffset; }
        internal static uint DayOfMonthOffset              { get => dayOfMonthOffset; }
        internal static uint MonthOffset                   { get => monthOffset; }
        internal static uint ClubLegalCostsOffset          { get => clubLegalCostsOffset; }
        internal static uint ClubOverdraftLimitOffset      { get => clubOverdraftLimitOffset; }
        internal static uint ClubPublicImageOffset         { get => clubPublicImageOffset; }
        internal static uint ClubLicenceOffset             { get => clubLicenceOffset; }
        internal static uint ClubRequirementsOffset        { get => clubRequirementsOffset; }
        internal static uint ClubDebtLimitOffset           { get => clubDebtLimitOffset; }
        internal static uint ClubSpectatorLimitOffset      { get => clubSpectatorLimitOffset; }
        internal static uint ClubSalaryLimitOffset         { get => clubSalaryLimitOffset; }
        internal static uint ClubTransferLimitOffset       { get => clubTransferLimitOffset; }
        internal static uint ClubBriberyOffset             { get => clubBriberyOffset; }
        internal static uint ClubShortCampOffset           { get => clubShortCampOffset; }
        internal static uint ClubSponsorRequestsOffset     { get => clubSponsorRequestsOffset; }
        internal static uint ClubScoutSpendingOffset       { get => clubScoutSpendingOffset; }
        internal static uint MatchTableOffset              { get => matchTableOffset; }
        internal static uint MatchStride                   { get => matchStride; }
        internal static uint MatchClockOffset              { get => matchClockOffset; }
        internal static uint ClubLineUpOffset              { get => clubLineUpOffset; }
        internal static uint ClubBenchOffset               { get => clubBenchOffset; }
        internal static uint PlayerMatchFlagsOffset        { get => playerMatchFlagsOffset; }
        internal static uint ClubRecordStockOffset         { get => clubRecordStockOffset; }
        internal static uint LawFirmFeeTableOffset         { get => lawFirmFeeTableOffset; }
        internal static uint DepartmentCostTableOffset     { get => departmentCostTableOffset; }
        internal static uint ClubRecordShortNameOffset     { get => clubRecordShortNameOffset; }
        internal static uint ClubRecordChantOffset         { get => clubRecordChantOffset; }
        internal static uint ClubRecordFanNameOffset       { get => clubRecordFanNameOffset; }
        internal static uint ClubRecordMirrorOffset(ClubEnums.AddressKey key) => clubRecordMirrorOffsets[key];
        internal static IEnumerable<ClubEnums.AddressKey> ClubRecordMirrorKeys { get => clubRecordMirrorOffsets.Keys; }
        internal static uint ClubCacheTableOffset          { get => clubCacheTableOffset; }
        internal static uint ClubCacheStride               { get => clubCacheStride; }
        internal static int  ClubCacheSlots                { get => clubCacheSlots; }
        internal static uint ClubCachePointerOffset        { get => clubCachePointerOffset; }
        internal static uint ClubCachePartsOffset          { get => clubCachePartsOffset; }
        internal static uint LeagueBaseTableOffset         { get => leagueBaseTableOffset; }
        internal static uint LeagueClubCountTableOffset    { get => leagueClubCountTableOffset; }
        // As many records as the club scan (ClubController.GetEntityList) walks.
        internal static int  ClubRecordCount               { get => clubCount + 1; }
        internal static uint ClubRecordSquadCountOffset    { get => clubRecordSquadCountOffset; }
        internal static uint ClubRecordSquadOffset         { get => clubRecordSquadOffset; }
        internal static uint ClubRecordAmateurCountOffset  { get => clubRecordAmateurCountOffset; }
        internal static uint ClubRecordAmateursOffset      { get => clubRecordAmateursOffset; }
        internal static uint ClubRecordIdOffset            { get => clubRecordIdOffset; }
        internal static uint ClubRecordCountryOffset       { get => clubRecordCountryOffset; }
        internal static uint ClubAmateurCountOffset        { get => clubAmateurCountOffset; }
        internal static uint ClubAmateursOffset            { get => clubAmateursOffset; }
        internal static int  ClubAmateurCapacity           { get => clubAmateurCapacity; }
        internal static uint YouthTableOffset              { get => youthTableOffset; }
        internal static uint YouthStride                   { get => youthStride; }
        internal static uint[] YouthIdOffsets              { get => (uint[])youthIdOffsets.Clone(); }
        internal static int[] YouthCapacities              { get => (int[])youthCapacities.Clone(); }
        internal static uint PlayerCacheTableOffset        { get => playerCacheTableOffset; }
        internal static uint PlayerCacheStride             { get => playerCacheStride; }
        internal static int  PlayerCacheSlots              { get => playerCacheSlots; }
        internal static uint PlayerCacheCopyOffset         { get => playerCacheCopyOffset; }
        internal static uint FixedAddressSearchWindow      { get => fixedAddressSearchWindow; }
        internal static byte[] LeagueBasePattern           { get => (byte[])leagueBasePattern.Clone(); }
        internal static byte[] LeagueRangePattern          { get => (byte[])leagueRangePattern.Clone(); }
        internal static byte[] JobOfferAddPattern          { get => (byte[])jobOfferAddPattern.Clone(); }
        internal static uint NationalTakeFunctionOffset       { get => nationalTakeFunctionOffset; }
        internal static uint NationalLeaveFunctionOffset      { get => nationalLeaveFunctionOffset; }
        internal static uint NationalCandidatesFunctionOffset { get => nationalCandidatesFunctionOffset; }
        internal static uint OfferGenerateCallSiteOffset      { get => offerGenerateCallSiteOffset; }
        internal static uint OfferGenerateFunctionOffset      { get => offerGenerateFunctionOffset; }
        internal static uint NationalFriendlyBlockOffset      { get => nationalFriendlyBlockOffset; }
        internal static uint NationalFriendlyBlockStride      { get => nationalFriendlyBlockStride; }
        internal static int  NationalFriendlyCandidatesLength { get => nationalFriendlyCandidatesLength; }
        internal static uint NationalFriendlyChosenOffset     { get => nationalFriendlyChosenOffset; }
        internal static uint NationalFriendlyResultsOffset    { get => nationalFriendlyResultsOffset; }
        internal static byte[] NationalTakePattern            { get => (byte[])nationalTakePattern.Clone(); }
        internal static byte[] NationalLeavePatternHead       { get => (byte[])nationalLeavePatternHead.Clone(); }
        internal static byte[] NationalCandidatesPattern      { get => (byte[])nationalCandidatesPattern.Clone(); }
        internal static uint CoachPoolTableOffset             { get => coachPoolTableOffset; }
        internal static uint GoalkeeperCoachTableOffset       { get => goalkeeperCoachTableOffset; }
        internal static uint CoachPoolGetterOffset            { get => coachPoolGetterOffset; }
        internal static uint GoalkeeperCoachGetterOffset      { get => goalkeeperCoachGetterOffset; }
        internal static int  CoachPoolCapacity                { get => coachPoolCapacity; }
        internal static int  GoalkeeperCoachCapacity          { get => goalkeeperCoachCapacity; }
        internal static uint StaffEntryStride                 { get => staffEntryStride; }
        internal static uint StaffCompetenceOffset            { get => staffCompetenceOffset; }
        internal static uint StaffContractOffset              { get => staffContractOffset; }
        internal static uint StaffSalaryOffset                { get => staffSalaryOffset; }
        internal static byte MaxStaffCompetence               { get => maxStaffCompetence; }
        internal static byte[] StaffGetterPattern             { get => (byte[])staffGetterPattern.Clone(); }
        internal static byte[] JobOfferPresentPattern      { get => (byte[])jobOfferPresentPattern.Clone(); }
        internal static byte[] JobOfferCallTrailPattern    { get => (byte[])jobOfferCallTrailPattern.Clone(); }
        internal static byte[] TransferFetchPlayerPattern   { get => (byte[])transferFetchPlayerPattern.Clone(); }
        internal static byte[] TransferReleasePlayerPattern { get => (byte[])transferReleasePlayerPattern.Clone(); }
        internal static byte[] TransferFetchClubPattern     { get => (byte[])transferFetchClubPattern.Clone(); }
        internal static byte[] TransferReleaseClubPattern   { get => (byte[])transferReleaseClubPattern.Clone(); }
        internal static byte[] TransferBuildContractPattern { get => (byte[])transferBuildContractPattern.Clone(); }
        internal static byte[] TransferLoanPattern          { get => (byte[])transferLoanPattern.Clone(); }
        internal static byte[] TransferDeadlinePatternHead  { get => (byte[])transferDeadlinePatternHead.Clone(); }
        internal static byte[] TransferDeadlinePatternTail  { get => (byte[])transferDeadlinePatternTail.Clone(); }
        internal static byte[] TransferMarketPatternHead    { get => (byte[])transferMarketPatternHead.Clone(); }
        internal static byte[] TransferMarketPatternTail    { get => (byte[])transferMarketPatternTail.Clone(); }
        internal static byte[] TransferMarketCallLeadPattern { get => (byte[])transferMarketCallLeadPattern.Clone(); }
        internal static byte[] TransferMarketCallTrailTail  { get => (byte[])transferMarketCallTrailTail.Clone(); }
        internal static byte[] TransferDeadlineCallLeadPattern  { get => (byte[])transferDeadlineCallLeadPattern.Clone(); }
        internal static byte[] TransferDeadlineCallTrailPattern { get => (byte[])transferDeadlineCallTrailPattern.Clone(); }
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
        internal static uint PlayerRecordJerseyOffset       { get => playerRecordJerseyOffset; }
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
        internal static uint PlayerRecordLeagueBanOffset    { get => playerRecordLeagueBanOffset; }
        internal static uint PlayerRecordLeagueBanEndOffset { get => playerRecordLeagueBanEndOffset; }
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
        internal static uint PlayerRecordCapsOffset { get => playerRecordCapsOffset; }
        internal static uint TransferListOffset { get => transferListOffset; }
        internal static uint TransferListEntriesOffset { get => transferListEntriesOffset; }
        internal static uint TransferListEntryStride { get => transferListEntryStride; }
        internal static int  TransferListCapacity { get => transferListCapacity; }

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
