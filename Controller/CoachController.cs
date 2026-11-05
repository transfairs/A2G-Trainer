using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>Reads/writes a single human trainer's (Mitspieler) slot: identity, contract, stocks, and wealth.</summary>
    public class CoachController : EntityController<Coach>
    {
        // Distance between one stock slot and the next (Aktie 1 -> Aktie 2 is 34BD8 -> 34BE8).
        private const int StockSlotStride = 0x10;
        // Distance between one player's whole Coach struct and the next (Player 1 -> Player 2
        // Firstname is 857800 -> 859158). Folded into Coach.Offset, which every existing field
        // address already adds in - see GetAddress in EntityController.
        private const int TrainerSlotStride = 0x1958;

        // Robin found the trainer-competency table directly: starting right after this coach slot's
        // other fields, 16 levels (Coach.MinLevel..MaxLevel) x 6 competencies x 2 bytes (UInt16)
        // each, no padding, one level immediately after the other with no break. Level 0's first
        // competency (Verhandlungsgeschick) is at +36398; everything else is a fixed stride from
        // there. Confirmed per-trainer by the game's own GetMitspieler, which copies exactly these
        // 0xC0 bytes into each Mitspieler's working copy from a separate per-ID record array
        // (Settings.CoachCompetencyRecordOffset).
        private const int CompetencyTableOffset = 0x36398;
        private const int CompetencyEntryStride = 0x2;
        private static readonly CoachEnums.CompetencyKey[] CompetencyOrder = (CoachEnums.CompetencyKey[])Enum.GetValues(typeof(CoachEnums.CompetencyKey));

        // AddressPresets.COACH offsets are relative to the club pointer's target (0x822D00), where
        // the working copy of Mitspieler ID 1 starts at +34B00. Subtracting this turns a preset
        // offset into an offset within one working copy (and, for the fields edited here, within
        // the saved record too - see Settings.CoachSlotTableOffset).
        private const uint PointerRelativeSlotBase = 0x34B00;

        internal Coach Coach { get { return this.coach; } set { this.coach = value; } }
        private Coach coach;

        private readonly int trainerIndex;

        /// <summary>Attaches to the game process and loads the trainer slot at <paramref name="trainerIndex"/>.</summary>
        public CoachController(ProcessMemory memory, bool isGog, PlayerEnums.AddressType type, int trainerIndex = 0) : base(memory)
        {
            this.isGog = isGog;
            this.trainerIndex = trainerIndex;
            this.settings = Settings.ClubAddress;
            this.UpdateBaseAddress(type);
            this.fixedAddresses = FixedAddresses.For(memory);
            this.Coach = this.GetEntity((trainerIndex * TrainerSlotStride).ToString("X"), type);
        }

        /// <summary>Reads this trainer slot's identity, stocks, and wealth from memory.</summary>
        internal override Coach GetEntity(string offset, PlayerEnums.AddressType type)
        {
            Coach coach = new Coach()
            {
                Offset = offset,
                Addresses = AddressPresets.COACH
            };

            if (this.memory.mProc.MainModule != null)
            {
                coach.Initilisation = true;

                coach.Firstname = ReadString(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.FIRSTNAME], 9));
                coach.Lastname  = ReadString(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.LAST_NAME], 15));
                coach.Level     = ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.LEVEL], 1));
                coach.LevelBase = ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.LEVEL_BASE], 1));
                coach.Age       = ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.AGE], 1));
                byte difficulty = ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.DIFFICULTY], 1));
                coach.Difficulty = (CoachEnums.Difficulty)(difficulty & 7);
                coach.DifficultyLocked = (difficulty & 0x80) != 0;
                this.ReadCareer(coach);
                coach.Games    = ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.GAMES], 2));
                coach.Wins     = ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.WINS], 2));

                for (int slot = 0; slot < coach.Stocks.Length; slot++)
                {
                    Stock stock = coach.Stocks[slot];
                    stock.Country = (PlayerEnums.Country)ReadByte(this.ReadField(coach, GetStockOffset(coach, CoachEnums.AddressKey.STOCK_COUNTRY, slot), 1));
                    stock.ClubId  = ReadByte(this.ReadField(coach, GetStockOffset(coach, CoachEnums.AddressKey.STOCK_CLUB, slot), 1));
                    stock.Shares  = ReadInt32(this.ReadField(coach, GetStockOffset(coach, CoachEnums.AddressKey.STOCK_SHARES, slot), 4));
                    stock.Price   = ReadUInt16(this.ReadField(coach, GetStockOffset(coach, CoachEnums.AddressKey.STOCK_PRICE, slot), 2));
                }

                coach.Wealth = ReadInt32(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.WEALTH], 4));

                coach.CurrentCountry = (PlayerEnums.Country)ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.COUNTRY], 1));
                coach.CurrentClubId = ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.CLUB], 1));

                coach.CountryNextSeason = (PlayerEnums.Country)ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.COUNTRY_NEXT_SEASON], 1));
                coach.ClubIdNextSeason = ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.CLUB_NEXT_SEASON], 1));
                coach.ReleaseClause = ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.RELEASE_CLAUSE], 1));
                coach.BoardRelease = ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.BOARD_RELEASE], 1));
                coach.ContractYearsLeft = ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.CONTRACT_DURATION], 2));
                coach.BoardTrustBonus = ReadBoardTrustBonus(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.TRUST_BONUS], 4));
                coach.BoardTrustBase = (short)ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.TRUST], 2));

                coach.Nerves = ClampStanding((short)ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.NERVES], 2)));
                coach.NervesWeeks = (short)ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.NERVES_WEEKS], 2));
                this.loadedNerves = coach.Nerves;
                coach.PublicStanding = ClampStanding((sbyte)ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.PUBLIC_STANDING], 1)));
                coach.FanStanding = ClampStanding((sbyte)ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.FAN_STANDING], 1)));

                coach.Salary = ReadInt32(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.SALARY], 4));
                coach.PointBonus = ReadInt32(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.POINT_BONUS], 4));
                coach.CupRoundBonus = ReadInt32(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.PER_TROPHY_ROUND], 4));
                coach.TitleBonus = ReadInt32(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.PER_TITLE], 4));
                coach.SeasonGoalBonus = ReadInt32(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.ACHIEVED_BONUS], 4));
                coach.SeasonGoal = (CoachEnums.SeasonGoal)ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.SEASON_GOAL], 2));
                coach.SeasonGoal = this.ReadClubSeasonGoal(coach) ?? coach.SeasonGoal;
                coach.ExtensionOption = ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.EXTEND_OPTION], 2));
                coach.Perks = ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.PERKS], 2));

                for (int level = Coach.MinLevel; level <= Coach.MaxLevel; level++)
                {
                    CoachCompetencyLevel competencyLevel = coach.CompetencyLevels[level];
                    for (int i = 0; i < CompetencyOrder.Length; i++)
                    {
                        ushort value = ReadUInt16(this.ReadField(coach, GetCompetencyOffset(level, i), 2));
                        competencyLevel.Set(CompetencyOrder[i], value);
                    }
                }

                coach.MediaBase = (byte)Math.Max(0, (int)(sbyte)ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.MEDIA_STANDING], 1)));
                this.ReadChampionTips(coach);

                byte[] image = this.HasFixedAddresses ? this.memory.ReadBytesAtAddress(this.ImageAddress, 2) : null;
                coach.HasImage = image != null;
                coach.Image = (CoachEnums.ManagerImage)ReadUInt16(image);

                byte[] nationalTeam = this.HasFixedAddresses ? this.memory.ReadBytesAtAddress(this.NationalRecordAddress(Settings.CoachRecordNationalTeamOffset), 2) : null;
                coach.HasNationalTeam = nationalTeam != null;
                coach.NationalTeam = (PlayerEnums.Country)ReadUInt16(nationalTeam);
                coach.NationalSalary = this.HasFixedAddresses ? ReadInt32(this.memory.ReadBytesAtAddress(this.NationalRecordAddress(Settings.CoachRecordNationalSalaryOffset), 4)) : 0;
                coach.NationalRoundBonus = this.HasFixedAddresses ? ReadInt32(this.memory.ReadBytesAtAddress(this.NationalRecordAddress(Settings.CoachRecordNationalBonusOffset), 4)) : 0;
                this.loadedNationalTeam = coach.NationalTeam;
                this.ReadFriendlies(coach);
                // A takeover the game hasn't carried out yet shows as the coach's national team.
                PlayerEnums.Country? pending = this.HasFixedAddresses ? new NationalCoachInjector(this.memory).PendingCountry(this.trainerIndex) : null;
                coach.IsNationalTeamPending = pending.HasValue;
                if (pending.HasValue)
                    coach.NationalTeam = pending.Value;

                coach.Initilisation = false;
            }
            return coach;
        }

        // Stock slots aren't separate dictionary entries - each one is the slot-0 offset (from
        // AddressPresets.COACH) plus slotIndex * StockSlotStride, computed here instead.
        private static string GetStockOffset(Coach coach, CoachEnums.AddressKey key, int slotIndex)
        {
            return Tools.SumHex(new[] { coach.Addresses[key], (slotIndex * StockSlotStride).ToString("X") });
        }

        // Competency slots aren't separate dictionary entries either - level and competency-within-
        // level combine into a single flat byte offset from CompetencyTableOffset, same idea as
        // GetStockOffset above.
        private static string GetCompetencyOffset(int level, int competencyIndex)
        {
            int byteOffset = CompetencyTableOffset + (level * CompetencyOrder.Length + competencyIndex) * CompetencyEntryStride;
            return byteOffset.ToString("X");
        }

        // Mitspieler IDs are 1-based: trainer slot 0 is ID 1 (see Settings.CoachSlotTableOffset).
        private uint MitspielerId => (uint)this.trainerIndex + 1;

        private static uint ToSlotOffset(string presetOffset) => Convert.ToUInt32(presetOffset, 16) - PointerRelativeSlotBase;

        // Resolved once in the constructor (see FixedAddresses.For); null for builds without known
        // fixed addresses, which keep going through the club pointer exactly as before.
        private readonly FixedAddresses fixedAddresses;
        private FixedAddresses Fixed => this.fixedAddresses;
        private bool HasFixedAddresses => this.fixedAddresses != null;

        private uint WorkingCopyAddress(uint slotOffset) =>
            this.Fixed.Runtime(this.memory, Settings.CoachSlotTableOffset) + this.MitspielerId * Settings.CoachSlotStride + slotOffset;

        // Where the saved record keeps the field at slotOffset: the same offset within the
        // Mitspieler's record, except for the competency table, which lives in its own array, and the
        // level/Fortbildung block, which the record keeps further on.
        private uint RecordAddress(uint slotOffset)
        {
            uint competencyStart = (uint)CompetencyTableOffset - PointerRelativeSlotBase;
            if (slotOffset >= competencyStart)
                return this.Fixed.Runtime(this.memory, Settings.CoachCompetencyRecordOffset) + this.MitspielerId * Settings.CoachCompetencyRecordStride + (slotOffset - competencyStart);

            uint record = this.Fixed.Runtime(this.memory, Settings.CoachRecordTableOffset) + this.MitspielerId * Settings.CoachRecordStride;
            if (slotOffset >= Settings.CoachSlotLevelBlockOffset && slotOffset < Settings.CoachSlotLevelBlockOffset + Settings.CoachLevelBlockLength)
                return record + Settings.CoachRecordLevelBlockOffset + (slotOffset - Settings.CoachSlotLevelBlockOffset);
            if (slotOffset >= Settings.CoachSlotFlagsOffset && slotOffset < Settings.CoachSlotFlagsOffset + Settings.CoachFlagsLength)
                return record + Settings.CoachRecordFlagsOffset + (slotOffset - Settings.CoachSlotFlagsOffset);
            return record + slotOffset;
        }

        private uint CoachRecord => this.Fixed.Runtime(this.memory, Settings.CoachRecordTableOffset) + this.MitspielerId * Settings.CoachRecordStride;

        private uint NaturalisationTable => this.Fixed.Runtime(this.memory, Settings.NaturalisationTableOffset) + this.MitspielerId * Settings.NaturalisationTableStride;

        // A working-copy field by its offset within the working copy (no AddressKey of its own).
        private byte[] ReadSlot(Coach coach, uint slotOffset, int length) => this.ReadField(coach, (PointerRelativeSlotBase + slotOffset).ToString("X"), length);

        private void WriteSlot(uint slotOffset, byte[] data) => this.WriteField(this.Coach, (PointerRelativeSlotBase + slotOffset).ToString("X"), data);

        // The level evaluation's values, the event flags, and - with fixed addresses - the sabotage
        // against this manager and his naturalisation table (see CoachLevelState).
        private void ReadLevelState(Coach coach)
        {
            CoachLevelState state = coach.LevelState;
            byte[] block = this.ReadSlot(coach, Settings.CoachSlotLevelBlockOffset, (int)Settings.CoachLevelBlockLength);
            if (block != null)
            {
                state.WinStreak = BitConverter.ToInt16(block, 0x0);
                state.WeeksInOffice = BitConverter.ToInt16(block, 0x2);
                state.Unbeaten = BitConverter.ToInt16(block, 0x4);
                state.LastPosition = block[0x6];
                state.PromotionsInARow = block[0x7];
                state.Marks = BitConverter.ToUInt16(block, 0x8);
                state.BigTitles = BitConverter.ToInt16(block, 0xA);
                state.SeasonSuccess = block[0xC];
                state.Credit = block[0xD];
                state.DemotionReasons = block[0xE];
                state.DismissalsLastSeason = block[0xF];
                state.DismissalsThisSeason = block[0x10];
                state.ResignationsLastSeason = block[0x11];
                state.ResignationsThisSeason = block[0x12];
            }
            state.SeriesPoints = (short)ReadUInt16(this.ReadSlot(coach, Settings.CoachSlotSeriesOffset, 2));
            state.LastChangeHalfWeek = (short)ReadUInt16(this.ReadSlot(coach, Settings.CoachSlotLastLevelChangeOffset, 2));
            state.LastChangeSeason = (short)ReadUInt16(this.ReadSlot(coach, Settings.CoachSlotLastLevelChangeOffset + 2, 2));
            state.RefereeOutbursts = ReadByte(this.ReadSlot(coach, Settings.CoachSlotFlagsOffset, 1));
            byte[] flags = this.ReadSlot(coach, Settings.CoachSlotFlagsOffset + 4, 4);
            state.Flags = flags != null ? BitConverter.ToUInt32(flags, 0) : 0;
            if (!this.HasFixedAddresses)
                return;

            byte[] sabotage = this.memory.ReadBytesAtAddress(this.CoachRecord + Settings.CoachRecordSabotageOffset + 4, Coach.MaxTrainers * 4);
            state.HasSabotage = sabotage != null && Enumerable.Range(0, Coach.MaxTrainers).Any(k => sabotage[k * 4] != 0);
            byte[] naturalisation = this.memory.ReadBytesAtAddress(this.NaturalisationTable, Settings.NaturalisationTableLength);
            if (naturalisation != null)
            {
                state.NaturalisationTalks = BitConverter.ToInt16(naturalisation, 0);
                state.NaturalisationRefusals = Enumerable.Range(0, Settings.NaturalisationEntries)
                    .Count(n => BitConverter.ToInt16(naturalisation, 2 + n * 6) != 0 && (naturalisation[2 + n * 6 + 2] & 4) != 0);
            }
        }

        private void WriteLevelState()
        {
            CoachLevelState state = this.Coach.LevelState;
            byte[] block = new byte[Settings.CoachLevelBlockLength - 1];
            Array.Copy(BitConverter.GetBytes(state.WinStreak), 0, block, 0x0, 2);
            Array.Copy(BitConverter.GetBytes(state.WeeksInOffice), 0, block, 0x2, 2);
            Array.Copy(BitConverter.GetBytes(state.Unbeaten), 0, block, 0x4, 2);
            block[0x6] = state.LastPosition;
            block[0x7] = state.PromotionsInARow;
            Array.Copy(BitConverter.GetBytes(state.Marks), 0, block, 0x8, 2);
            Array.Copy(BitConverter.GetBytes(state.BigTitles), 0, block, 0xA, 2);
            block[0xC] = state.SeasonSuccess;
            block[0xD] = state.Credit;
            block[0xE] = state.DemotionReasons;
            block[0xF] = state.DismissalsLastSeason;
            block[0x10] = state.DismissalsThisSeason;
            block[0x11] = state.ResignationsLastSeason;
            block[0x12] = state.ResignationsThisSeason;
            // Without the Fortbildung result (+0x1833), which WriteCareer writes.
            this.WriteSlot(Settings.CoachSlotLevelBlockOffset, block);
            this.WriteSlot(Settings.CoachSlotSeriesOffset, BitConverter.GetBytes(state.SeriesPoints));
            this.WriteSlot(Settings.CoachSlotLastLevelChangeOffset, BitConverter.GetBytes(state.LastChangeHalfWeek));
            this.WriteSlot(Settings.CoachSlotLastLevelChangeOffset + 2, BitConverter.GetBytes(state.LastChangeSeason));
            this.WriteSlot(Settings.CoachSlotFlagsOffset, new[] { state.RefereeOutbursts });
            this.WriteSlot(Settings.CoachSlotFlagsOffset + 4, BitConverter.GetBytes(state.Flags));
            if (!this.HasFixedAddresses)
                return;

            if (state.StopSabotage)
            {
                this.memory.WriteBytesAtAddress(this.CoachRecord + Settings.CoachRecordSabotageOffset + 4, new byte[Coach.MaxTrainers * 4]);
                state.HasSabotage = false;
                state.StopSabotage = false;
                Logger.Debug($"Coach slot {this.trainerIndex}: stopped every sabotage against this manager.");
            }
            if (state.ResetNaturalisation)
            {
                byte[] naturalisation = this.memory.ReadBytesAtAddress(this.NaturalisationTable, Settings.NaturalisationTableLength);
                if (naturalisation != null)
                {
                    naturalisation[0] = naturalisation[1] = 0;
                    for (int n = 0; n < Settings.NaturalisationEntries; n++)
                        naturalisation[2 + n * 6 + 2] &= 0xFB;
                    this.memory.WriteBytesAtAddress(this.NaturalisationTable, naturalisation);
                    state.NaturalisationTalks = 0;
                    state.NaturalisationRefusals = 0;
                }
                state.ResetNaturalisation = false;
            }
        }

        private uint AchievementsAddress =>
            this.Fixed.Runtime(this.memory, Settings.CoachAchievementsOffset) + this.MitspielerId * Settings.CoachAchievementsStride;

        // The Fortbildung result as read, so a save only moves the level mark when it was changed.
        private CoachEnums.TrainingResult readTrainingResult;

        // Career duration, Fortbildung, and - where the build has fixed
        // addresses - the season to turn the start into a year and the titles.
        private void ReadCareer(Coach coach)
        {
            coach.RetirementAge = (short)ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.RETIREMENT_AGE], 2));
            coach.CareerMode = (CoachEnums.CareerMode)ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.CAREER_MODE], 2));
            coach.CareerStartSeason = (short)ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.CAREER_START], 2));
            coach.TrainingResult = (CoachEnums.TrainingResult)Math.Min(2, (int)ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.TRAINING_RESULT], 1)));
            this.readTrainingResult = coach.TrainingResult;
            this.ReadLevelState(coach);
            if (!this.HasFixedAddresses)
                return;

            coach.CurrentSeason = (short)ReadUInt16(this.memory.ReadBytesAtAddress(this.Fixed.Runtime(this.memory, Settings.SeasonIndexOffset), 2));
            coach.CurrentYear = (short)ReadUInt16(this.memory.ReadBytesAtAddress(this.Fixed.Runtime(this.memory, Settings.AgeReferenceYearOffset), 2));
            byte[] block = this.memory.ReadBytesAtAddress(this.AchievementsAddress, (int)Settings.CoachAchievementsStride);
            if (block == null)
                return;

            CoachAchievements achievements = new CoachAchievements();
            for (int i = 0; i < CoachAchievements.Offsets.Length; i++)
                achievements.Counters[i] = BitConverter.ToInt16(block, (int)CoachAchievements.Offsets[i]);
            coach.Achievements = achievements;
        }

        private void WriteCareer()
        {
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.RETIREMENT_AGE], BitConverter.GetBytes(this.Coach.RetirementAge));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.CAREER_MODE], BitConverter.GetBytes((short)this.Coach.CareerMode));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.CAREER_START], BitConverter.GetBytes(this.Coach.CareerStartSeason));

            // A changed result also sets or clears the mark the level evaluation looks at, as passing
            // the quiz sets it (written with the other marks in WriteLevelState). An unchanged one
            // leaves it alone: the level evaluation uses the mark up, and January clears it.
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.TRAINING_RESULT], new[] { (byte)this.Coach.TrainingResult });
            if (this.Coach.TrainingResult != this.readTrainingResult)
                this.Coach.LevelState.SetMark(0x80, this.Coach.TrainingResult != CoachEnums.TrainingResult.Failed);
            this.readTrainingResult = this.Coach.TrainingResult;
            this.WriteLevelState();

            if (!this.HasFixedAddresses || this.Coach.Achievements == null)
                return;
            for (int i = 0; i < CoachAchievements.Offsets.Length; i++)
                this.memory.WriteBytesAtAddress(this.AchievementsAddress + CoachAchievements.Offsets[i], BitConverter.GetBytes(this.Coach.Achievements.Counters[i]));
        }

        // The image has no working copy - the game reads and assigns it only in the per-ID block
        // (see Settings.CoachImageRecordOffset).
        private uint ImageAddress =>
            this.Fixed.Runtime(this.memory, Settings.CoachImageRecordOffset) + this.MitspielerId * Settings.CoachCompetencyRecordStride;

        // The national coach fields sit past the working copy's end, in the saved record only
        // (except the country, see Settings.CoachSlotNationalTeamOffset).
        private uint NationalRecordAddress(uint recordOffset) =>
            this.Fixed.Runtime(this.memory, Settings.CoachRecordTableOffset) + this.MitspielerId * Settings.CoachRecordStride + recordOffset;

        // The national team as read, so Save knows which team's coach posts to clear on a change.
        private PlayerEnums.Country loadedNationalTeam = PlayerEnums.Country.Sonstige;

        private static uint NationalCoachEntryAddress(ProcessMemory memory, FixedAddresses fixedAddresses, uint mitspielerId) =>
            fixedAddresses.Runtime(memory, Settings.NationalCoachTableOffset) + mitspielerId * Settings.NationalCoachEntryStride;

        /// <summary>
        /// National teams trained by the other active trainers, read from the game's table of who
        /// trains which national team. Empty for builds without known fixed addresses.
        /// </summary>
        internal static List<PlayerEnums.Country> NationalTeamsOfOtherTrainers(ProcessMemory memory, int trainerIndex)
        {
            List<PlayerEnums.Country> teams = new List<PlayerEnums.Country>();
            FixedAddresses fixedAddresses = FixedAddresses.For(memory);
            if (fixedAddresses == null)
                return teams;

            int count = GetActiveTrainerCount(memory) ?? Coach.MaxTrainers;
            for (int i = 0; i < count; i++)
            {
                if (i == trainerIndex)
                    continue;
                byte[] entry = memory.ReadBytesAtAddress(NationalCoachEntryAddress(memory, fixedAddresses, (uint)i + 1), 4);
                if (entry != null && entry[0] != 0 && BitConverter.ToUInt16(entry, 2) == Settings.NationalTeamClubId)
                    teams.Add((PlayerEnums.Country)entry[0]);
            }
            return teams;
        }

        // Puts this Mitspieler's ID into (or, with 0, takes it out of) both coach posts of the
        // national team: its record and every working copy the game has fetched of it.
        private void WriteNationalTeamCoachPosts(PlayerEnums.Country country, byte mitspielerId)
        {
            uint record = this.Fixed.Runtime(this.memory, Settings.NationalTeamRecordTableOffset) + (uint)country * Settings.ClubRecordStride;
            List<uint> copies = new List<uint> { record };
            copies.AddRange(ClubRecords.WorkingCopies(this.memory, this.Fixed, country, Settings.NationalTeamClubId));
            foreach (uint copy in copies)
                this.memory.WriteBytesAtAddress(copy + Settings.NationalTeamCoachPostOffset, new byte[] { mitspielerId, mitspielerId });
        }

        // Whether the game has drawn no friendly opponents for this Mitspieler - as after a takeover
        // the game didn't carry out itself (see NationalCoachInjector).
        private bool HasNoFriendlyCandidates()
        {
            uint block = this.Fixed.Runtime(this.memory, Settings.NationalFriendlyBlockOffset) + this.MitspielerId * Settings.NationalFriendlyBlockStride;
            byte[] candidates = this.memory.ReadBytesAtAddress(block, Settings.NationalFriendlyCandidatesLength);
            return candidates != null && candidates.All(b => b == 0);
        }

        // A change of national team goes through the game's own functions at the next daily
        // processing where the build allows it (NationalCoachInjector) - only they set up line-up,
        // tactics and the friendly opponents. The same repairs a national coach without friendly
        // opponents. Otherwise the same steps as the game's FUN_005cf340 (leave) and FUN_005cf1e0
        // (take over) are written directly: the coach posts of the old and new national team, the
        // table entry, the working copy and the record. A coach without a national team has no
        // salary or bonus either, as when the game ends the job.
        private void WriteNationalTeam()
        {
            if (!this.HasFixedAddresses || !this.Coach.HasNationalTeam)
                return;

            PlayerEnums.Country country = this.Coach.NationalTeam;
            if (!this.Coach.IsNationalCoach)
                this.Coach.NationalSalary = this.Coach.NationalRoundBonus = 0;

            NationalCoachInjector injector = new NationalCoachInjector(this.memory);
            bool needsGame = country != this.loadedNationalTeam || (this.Coach.IsNationalCoach && this.HasNoFriendlyCandidates());
            if (injector.IsSupported && (needsGame || this.Coach.IsNationalTeamPending)
                && injector.Request(this.trainerIndex, country, this.Coach.NationalSalary, this.Coach.NationalRoundBonus))
            {
                this.Coach.IsNationalTeamPending = true;
                this.WriteNationalContract();
                return;
            }

            if (country != this.loadedNationalTeam)
            {
                if (this.loadedNationalTeam != PlayerEnums.Country.Sonstige)
                    this.WriteNationalTeamCoachPosts(this.loadedNationalTeam, 0);

                byte[] entry = new byte[4];
                if (country != PlayerEnums.Country.Sonstige)
                {
                    entry[0] = (byte)country;
                    BitConverter.GetBytes(Settings.NationalTeamClubId).CopyTo(entry, 2);
                    this.WriteNationalTeamCoachPosts(country, (byte)this.MitspielerId);
                }
                this.memory.WriteBytesAtAddress(NationalCoachEntryAddress(this.memory, this.Fixed, this.MitspielerId), entry);
                Logger.Debug($"Coach slot {this.trainerIndex}: national team {this.loadedNationalTeam} -> {country}.");
                this.loadedNationalTeam = country;
            }

            byte[] team = BitConverter.GetBytes((ushort)country);
            this.memory.WriteBytesAtAddress(this.WorkingCopyAddress(Settings.CoachSlotNationalTeamOffset), team);
            this.memory.WriteBytesAtAddress(this.NationalRecordAddress(Settings.CoachRecordNationalTeamOffset), team);
            this.WriteNationalContract();
        }

        private void WriteNationalContract()
        {
            this.memory.WriteBytesAtAddress(this.NationalRecordAddress(Settings.CoachRecordNationalSalaryOffset), BitConverter.GetBytes(this.Coach.NationalSalary));
            this.memory.WriteBytesAtAddress(this.NationalRecordAddress(Settings.CoachRecordNationalBonusOffset), BitConverter.GetBytes(this.Coach.NationalRoundBonus));
        }

        // Builds without known fixed addresses (see FixedAddresses) keep going through the club
        // pointer, exactly as before.
        private byte[] ReadField(Coach coach, string presetOffset, int length)
        {
            if (!this.HasFixedAddresses)
                return this.memory.ReadBytes(GetAddress(this.memory, coach, presetOffset), length);

            return this.memory.ReadBytesAtAddress(this.WorkingCopyAddress(ToSlotOffset(presetOffset)), length);
        }

        // Writes the working copy (what the game shows right now) and, where known, the saved
        // record too, so the edit survives the game re-fetching the trainer before writing back.
        private void WriteField(Coach coach, string presetOffset, byte[] data)
        {
            if (!this.HasFixedAddresses)
            {
                this.memory.WriteBytes(GetAddress(this.memory, coach, presetOffset), data);
                return;
            }

            uint slotOffset = ToSlotOffset(presetOffset);
            this.memory.WriteBytesAtAddress(this.WorkingCopyAddress(slotOffset), data);
            this.memory.WriteBytesAtAddress(this.RecordAddress(slotOffset), data);
        }

        // A club change only takes effect at the season change (FUN_005d47d0), which then makes the
        // NEXT contract (working copy +0x68..+0x87) the current one (+0x48..+0x67). The game fills
        // that block when a contract is signed for next season; a club set through the Trainer comes
        // without one, so the trainer would start at the new club with an all-zero contract (no
        // salary, no term). If the block is still empty, it gets a copy of the current contract.
        private const uint CurrentContractSlotOffset = 0x48;
        private const uint NextContractSlotOffset = 0x68;
        private const int ContractLength = 0x20;

        private void PrepareNextSeasonContract()
        {
            if (!this.HasFixedAddresses || this.Coach.CountryNextSeason == PlayerEnums.Country.Sonstige)
                return;

            byte[] next = this.memory.ReadBytesAtAddress(this.WorkingCopyAddress(NextContractSlotOffset), ContractLength);
            byte[] current = this.memory.ReadBytesAtAddress(this.WorkingCopyAddress(CurrentContractSlotOffset), ContractLength);
            if (next == null || current == null || next.Any(b => b != 0))
                return;

            this.memory.WriteBytesAtAddress(this.WorkingCopyAddress(NextContractSlotOffset), current);
            this.memory.WriteBytesAtAddress(this.RecordAddress(NextContractSlotOffset), current);
            Logger.Debug($"Coach slot {this.trainerIndex}: copied the current contract into the empty next-season contract for the club change to {this.Coach.CountryNextSeason}/{this.Coach.ClubIdNextSeason}.");
        }

        // The season goal the game shows (Karriere) and the board measures the trainer against lives
        // with the CLUB, not in the contract: the club record's byte, and the club's working copy
        // while the game has it fetched (FUN_00543970/FUN_0063dae0 read club +0x10C). Contract +0x5C
        // is only a copy for the season-goal bonus, refreshed from the club whenever the goal is
        // agreed (FUN_0063c960). Only the league countries' clubs have records at a known place
        // (FUN_00585b00 -> FUN_00585ff0, see ClubRecords); for any other club this returns no addresses.
        private List<uint> ClubSeasonGoalAddresses(Coach coach)
        {
            List<uint> addresses = new List<uint>();
            uint? record = ClubRecords.RecordAddress(this.memory, this.Fixed, coach.CurrentCountry, coach.CurrentClubId);
            if (!record.HasValue)
                return addresses;

            // Cached working copies first: they're newer than the record while the game has the club fetched.
            addresses.AddRange(ClubRecords.WorkingCopies(this.memory, this.Fixed, coach.CurrentCountry, coach.CurrentClubId).Select(copy => copy + Settings.ClubSeasonGoalOffset));
            addresses.Add(record.Value + Settings.ClubRecordSeasonGoalOffset);
            return addresses;
        }

        // The club's season goal (see ClubSeasonGoalAddresses), or null if it isn't known for this club.
        private CoachEnums.SeasonGoal? ReadClubSeasonGoal(Coach coach)
        {
            foreach (uint address in this.ClubSeasonGoalAddresses(coach))
            {
                byte[] goal = this.memory.ReadBytesAtAddress(address, 1);
                if (goal != null && goal[0] <= (byte)CoachEnums.SeasonGoal.ZuEndeBringen)
                    return (CoachEnums.SeasonGoal)goal[0];
            }
            return null;
        }

        private void WriteClubSeasonGoal()
        {
            List<uint> addresses = this.ClubSeasonGoalAddresses(this.Coach);
            foreach (uint address in addresses)
                this.memory.WriteBytesAtAddress(address, new byte[] { (byte)this.Coach.SeasonGoal });
            if (addresses.Count > 0)
                Logger.Debug($"Coach slot {this.trainerIndex}: set the season goal of club {this.Coach.CurrentCountry}/{this.Coach.CurrentClubId} to {this.Coach.SeasonGoal} ({addresses.Count} copies).");
        }

        // Mirrors the game's own season-change check (FUN_005d47d0 via FUN_005b37b0/FUN_005a48f0):
        // the club must play in one of that country's leagues, and for a bonus country (main
        // country excluded) only the first league counts - otherwise the game silently drops the
        // change. Returns null when the league tables can't be read or aren't known for the build.
        internal static bool? IsEligibleNextSeasonClub(ProcessMemory memory, PlayerEnums.Country country, byte clubId)
        {
            FixedAddresses fixedAddresses = FixedAddresses.For(memory);
            if (fixedAddresses == null)
                return null;

            byte[] system = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.LeagueSystemByCountryOffset) + (uint)country, 1);
            if (system == null)
                return null;
            byte[] range = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.LeagueRangeTableOffset) + (uint)system[0] * 2, 4);
            if (range == null)
                return null;

            short firstLeague = BitConverter.ToInt16(range, 0);
            short endLeague = BitConverter.ToInt16(range, 2);
            int? tier = null;
            for (short league = firstLeague; league < endLeague && !tier.HasValue; league++)
            {
                uint leagueAddress = fixedAddresses.Runtime(memory, Settings.LeagueTableOffset) + (uint)league * Settings.LeagueStride;
                byte[] count = memory.ReadBytesAtAddress(leagueAddress, 2);
                if (count == null)
                    return null;
                short clubs = BitConverter.ToInt16(count, 0);
                byte[] ids = clubs > 0 ? memory.ReadBytesAtAddress(leagueAddress + Settings.LeagueClubIdsOffset, clubs * 2) : new byte[0];
                if (ids == null)
                    return null;
                for (int i = 0; i < clubs; i++)
                {
                    if (BitConverter.ToInt16(ids, i * 2) == clubId)
                    {
                        tier = league - firstLeague;
                        break;
                    }
                }
            }

            if (!tier.HasValue)
                return false;
            return tier.Value == 0 || !IsBonusCountry(memory, fixedAddresses, country);
        }

        private static bool IsBonusCountry(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country)
        {
            byte[] count = memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.BonusCountryCountOffset), 2);
            if (count == null)
                return false;
            short n = BitConverter.ToInt16(count, 0);
            byte[] list = n > 0 ? memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.BonusCountryListOffset), n * 2) : null;
            if (list == null)
                return false;
            for (int i = 0; i < n; i++)
            {
                if (BitConverter.ToInt16(list, i * 2) == (short)country)
                    return true;
            }
            return false;
        }

        // An address that fails to resolve (e.g. an unmapped pointer target) must not blow up the
        // whole refresh cycle for every view, the way an unguarded BitConverter call on null would.
        private static byte ReadByte(byte[] bytes) => bytes != null ? bytes[0] : (byte)0;
        private static ushort ReadUInt16(byte[] bytes) => bytes != null ? BitConverter.ToUInt16(bytes, 0) : (ushort)0;
        private static int ReadInt32(byte[] bytes) => bytes != null ? BitConverter.ToInt32(bytes, 0) : 0;
        // The nerves as read, so Save only rewrites the season's sum when they were changed.
        private byte loadedNerves;

        private static byte ClampStanding(int value) => (byte)Math.Max(0, Math.Min(Coach.MaxStanding, value));

        // The bet block of a league country: one per league system, in the saved record only.
        private uint? ChampionTipAddress(PlayerEnums.Country country)
        {
            byte[] system = this.memory.ReadBytesAtAddress(this.Fixed.Static(this.memory, Settings.LeagueSystemByCountryOffset) + (uint)country, 1);
            if (system == null)
                return null;
            return this.NationalRecordAddress(Settings.CoachRecordBetOffset + system[0] * Settings.CoachRecordBetStride + Settings.CoachBetChampionOffset);
        }

        private void ReadChampionTips(Coach coach)
        {
            if (!this.HasFixedAddresses)
                return;
            foreach (PlayerEnums.Country country in LeagueSettings.LeagueCountries)
            {
                uint? at = this.ChampionTipAddress(country);
                byte[] tip = at.HasValue ? this.memory.ReadBytesAtAddress(at.Value, 8) : null;
                if (tip == null)
                    continue;
                coach.ChampionTips[country] = new ChampionTip
                {
                    Country = country,
                    Stake = BitConverter.ToInt32(tip, 0),
                    ClubId = BitConverter.ToUInt16(tip, 4),
                    Odds = BitConverter.ToInt16(tip, 6)
                };
            }
        }

        private uint FriendlyBlock => this.Fixed.Runtime(this.memory, Settings.NationalFriendlyBlockOffset) + this.MitspielerId * Settings.NationalFriendlyBlockStride;

        // The three friendlies of a national coach (see NationalFriendly). The game's own dialog only
        // offers the three drawn candidates and locks once a result exists or the match is 4 days
        // away; squads are named 3 days before, so a later change would meet an empty squad.
        private void ReadFriendlies(Coach coach)
        {
            coach.Friendlies.Clear();
            if (!this.HasFixedAddresses || coach.NationalTeam == PlayerEnums.Country.Sonstige)
                return;
            byte[] chosen = this.memory.ReadBytesAtAddress(this.FriendlyBlock + Settings.NationalFriendlyChosenOffset, 3 * 6);
            byte[] results = this.memory.ReadBytesAtAddress(this.FriendlyBlock + Settings.NationalFriendlyResultsOffset, 3 * 2);
            byte[] today = this.memory.ReadBytesAtAddress(this.Fixed.Runtime(this.memory, Settings.GlobalCounterOffset), 2);
            if (chosen == null || results == null || today == null)
                return;
            short day = BitConverter.ToInt16(today, 0);
            for (int n = 0; n < 3; n++)
            {
                coach.Friendlies.Add(new NationalFriendly
                {
                    Index = n,
                    Opponent = (PlayerEnums.Country)chosen[n * 6],
                    AtHome = BitConverter.ToUInt16(chosen, n * 6 + 4) == 0,
                    IsLocked = results[n * 2] != 0xFF || day + 4 >= NationalFriendly.Days[n]
                });
            }
        }

        private void WriteFriendlies()
        {
            if (!this.HasFixedAddresses)
                return;
            foreach (NationalFriendly friendly in this.Coach.Friendlies.Where(f => !f.IsLocked))
            {
                byte[] record = new byte[6];
                record[0] = (byte)friendly.Opponent;
                if (friendly.Opponent != PlayerEnums.Country.Sonstige)
                {
                    Array.Copy(BitConverter.GetBytes((ushort)Settings.NationalTeamClubId), 0, record, 2, 2);
                    Array.Copy(BitConverter.GetBytes((ushort)(friendly.AtHome ? 0 : 1)), 0, record, 4, 2);
                }
                this.memory.WriteBytesAtAddress(this.FriendlyBlock + Settings.NationalFriendlyChosenOffset + (uint)friendly.Index * 6, record);
            }
        }

        private void WriteChampionTips()
        {
            if (!this.HasFixedAddresses)
                return;
            foreach (ChampionTip tip in this.Coach.ChampionTips.Values)
            {
                uint? at = this.ChampionTipAddress(tip.Country);
                if (!at.HasValue)
                    continue;
                byte[] bytes = new byte[8];
                Array.Copy(BitConverter.GetBytes(tip.Stake), 0, bytes, 0, 4);
                Array.Copy(BitConverter.GetBytes(tip.ClubId), 0, bytes, 4, 2);
                Array.Copy(BitConverter.GetBytes(tip.Odds), 0, bytes, 6, 2);
                this.memory.WriteBytesAtAddress(at.Value, bytes);
            }
        }

        // +0x96/+0x97 count as unsigned, +0x98/+0x99 as signed bytes (FUN_00639100).
        private static int ReadBoardTrustBonus(byte[] bytes) => bytes != null ? bytes[0] + bytes[1] + (sbyte)bytes[2] + (sbyte)bytes[3] : 0;
        private static string ReadString(byte[] bytes) => bytes != null ? Encoding.GetEncoding("iso-8859-1").GetString(bytes).Split('\0')[0] : "";

        // Not every one of the up to Coach.MaxTrainers slots is necessarily used by a given
        // savegame. Preferred source: the live count at anstoss2.exe+426666 (see
        // GetActiveTrainerCount) - falls back to the old heuristic (an empty Firstname marks an
        // unused slot, since a real human manager always has one) if that can't be read.
        internal static List<KeyValuePair<int, Coach>> GetActiveTrainers(ProcessMemory memory, bool isGog, PlayerEnums.AddressType type)
        {
            List<KeyValuePair<int, Coach>> trainers = new List<KeyValuePair<int, Coach>>();
            int? liveCount = GetActiveTrainerCount(memory);

            for (int i = 0; i < Coach.MaxTrainers; i++)
            {
                Coach coach = new CoachController(memory, isGog, type, i).Coach;
                bool isActive = liveCount.HasValue ? i < liveCount.Value : !string.IsNullOrWhiteSpace(coach.Firstname);
                if (isActive)
                    trainers.Add(new KeyValuePair<int, Coach>(i, coach));
            }
            return trainers;
        }

        /// <summary>The human manager currently training the given club, or null if none does.</summary>
        internal static CoachController ForClub(ProcessMemory memory, bool isGog, PlayerEnums.Country country, byte clubId)
        {
            foreach (KeyValuePair<int, Coach> trainer in GetActiveTrainers(memory, isGog, PlayerEnums.AddressType.OWN))
            {
                if (trainer.Value.CurrentCountry == country && trainer.Value.CurrentClubId == clubId)
                    return new CoachController(memory, isGog, PlayerEnums.AddressType.OWN, trainer.Key);
            }
            return null;
        }

        /// <summary>Writes only the board's trust (its base value, see Coach.BoardTrust), e.g. from the club view.</summary>
        public void SaveBoardTrust()
        {
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.TRUST], BitConverter.GetBytes(this.Coach.BoardTrustBase));
        }

        // Robin found anstoss2.exe+426666 - right next to the age-reference-year field (see
        // Settings.ActiveTrainerCountOffset) - holding a live byte with the exact number of active
        // human trainers/"Mitspieler" in the current savegame. Authoritative when readable and
        // plausible (0..MaxTrainers); returns null (falling back to the Firstname heuristic above)
        // otherwise, e.g. when no savegame is loaded yet or the offset isn't known/confirmed for the
        // attached build (see PersistentLayout).
        private static int? GetActiveTrainerCount(ProcessMemory memory)
        {
            uint? offset = memory.Layout.ActiveTrainerCountOffset;
            if (!offset.HasValue)
                return null;

            byte[] bytes = memory.ReadBytesAtAddress(memory.ModuleBase + offset.Value, 1);
            if (bytes == null)
                return null;

            int count = bytes[0];
            if (count > Coach.MaxTrainers)
            {
                Logger.Warn($"GetActiveTrainerCount: implausibler Wert {count} bei 0x{offset.Value:X} - falle auf Firstname-Heuristik zurück.");
                return null;
            }
            return count;
        }

        /// <summary>
        /// Makes sure the coach could accept a requested job offer: if neither a release clause, the
        /// board's release nor a running-out contract allows negotiating (see Coach.CanNegotiateOffers),
        /// sets the release clause and writes just that field. Returns whether it had to be set.
        /// </summary>
        public bool EnsureCanNegotiateOffers()
        {
            if (this.Coach.CanNegotiateOffers)
                return false;

            this.Coach.HasReleaseClause = true;
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.RELEASE_CLAUSE], new byte[] { this.Coach.ReleaseClause });
            Logger.Debug($"Coach slot {this.trainerIndex}: set the release clause so a requested job offer can be accepted.");
            return true;
        }

        /// <summary>Writes this trainer slot's identity, stocks, and wealth back to memory (working copy and, on the original build, the saved record).</summary>
        public void Save()
        {
            Logger.Debug($"Saving coach: {this.Coach.Firstname} {this.Coach.Lastname}");
            Encoding latin1 = Encoding.GetEncoding("iso-8859-1");

            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.FIRSTNAME], latin1.GetBytes(this.Coach.Firstname.PadRight(9, '\0')));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.LAST_NAME], latin1.GetBytes(this.Coach.Lastname.PadRight(15, '\0')));

            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.LEVEL], new byte[] { this.Coach.Level });
            this.Coach.LevelBase = this.Coach.Level;
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.LEVEL_BASE], new byte[] { this.Coach.LevelBase });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.AGE], new byte[] { this.Coach.Age });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.DIFFICULTY], new byte[] { this.Coach.DifficultyByte });
            if (this.Coach.UnlockDifficulty)
            {
                this.Coach.DifficultyLocked = false;
                this.Coach.UnlockDifficulty = false;
            }
            this.WriteCareer();
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.GAMES], BitConverter.GetBytes(this.Coach.Games));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.WINS], BitConverter.GetBytes(this.Coach.Wins));

            for (int slot = 0; slot < this.Coach.Stocks.Length; slot++)
            {
                Stock stock = this.Coach.Stocks[slot];
                // The game marks a free slot by country 0 and club 0 and fills the first free one on a
                // purchase (FUN_004035f0, FUN_004029e0); selling the last share clears both again. A
                // new position is therefore just written into a free slot, and one without shares
                // (or without a club) is cleared the same way.
                bool held = stock.HasPosition;
                this.WriteField(this.Coach, GetStockOffset(this.Coach, CoachEnums.AddressKey.STOCK_COUNTRY, slot), new byte[] { held ? (byte)stock.Country : (byte)0 });
                this.WriteField(this.Coach, GetStockOffset(this.Coach, CoachEnums.AddressKey.STOCK_CLUB, slot), BitConverter.GetBytes(held ? (ushort)stock.ClubId : (ushort)0));
                this.WriteField(this.Coach, GetStockOffset(this.Coach, CoachEnums.AddressKey.STOCK_SHARES, slot), BitConverter.GetBytes(held ? stock.Shares : 0));
                this.WriteField(this.Coach, GetStockOffset(this.Coach, CoachEnums.AddressKey.STOCK_PRICE, slot), BitConverter.GetBytes(held ? stock.Price : (ushort)0));
            }

            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.WEALTH], BitConverter.GetBytes(this.Coach.Wealth));

            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.COUNTRY_NEXT_SEASON], new byte[] { (byte)this.Coach.CountryNextSeason });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.CLUB_NEXT_SEASON], new byte[] { this.Coach.ClubIdNextSeason });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.RELEASE_CLAUSE], new byte[] { this.Coach.ReleaseClause });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.BOARD_RELEASE], new byte[] { this.Coach.BoardRelease });
            // Only the base: the bonus bytes stay as the game left them (see Coach.BoardTrust).
            this.SaveBoardTrust();

            // Changed nerves take the season's sum along, so its average (career length) follows too.
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.NERVES], BitConverter.GetBytes((short)this.Coach.Nerves));
            if (this.Coach.Nerves != this.loadedNerves)
                this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.NERVES_SUM], BitConverter.GetBytes((short)Math.Min(short.MaxValue, this.Coach.Nerves * this.Coach.NervesWeeks)));
            this.loadedNerves = this.Coach.Nerves;
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.PUBLIC_STANDING], new byte[] { this.Coach.PublicStanding });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.FAN_STANDING], new byte[] { this.Coach.FanStanding });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.MEDIA_STANDING], new byte[] { this.Coach.MediaBase });
            this.WriteChampionTips();

            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.SALARY], BitConverter.GetBytes(this.Coach.Salary));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.POINT_BONUS], BitConverter.GetBytes(this.Coach.PointBonus));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.PER_TROPHY_ROUND], BitConverter.GetBytes(this.Coach.CupRoundBonus));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.PER_TITLE], BitConverter.GetBytes(this.Coach.TitleBonus));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.ACHIEVED_BONUS], BitConverter.GetBytes(this.Coach.SeasonGoalBonus));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.SEASON_GOAL], BitConverter.GetBytes((ushort)this.Coach.SeasonGoal));
            this.WriteClubSeasonGoal();
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.CONTRACT_DURATION], BitConverter.GetBytes(this.Coach.ContractYearsLeft));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.EXTEND_OPTION], BitConverter.GetBytes(this.Coach.ExtensionOption));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.PERKS], BitConverter.GetBytes(this.Coach.Perks));
            // After the contract fields, so an empty next-season contract gets the edited terms.
            this.PrepareNextSeasonContract();

            for (int level = Coach.MinLevel; level <= Coach.MaxLevel; level++)
            {
                CoachCompetencyLevel competencyLevel = this.Coach.CompetencyLevels[level];
                for (int i = 0; i < CompetencyOrder.Length; i++)
                {
                    ushort value = competencyLevel.Get(CompetencyOrder[i]);
                    this.WriteField(this.Coach, GetCompetencyOffset(level, i), BitConverter.GetBytes(value));
                }
            }

            if (this.HasFixedAddresses && this.Coach.HasImage)
                this.memory.WriteBytesAtAddress(this.ImageAddress, BitConverter.GetBytes((ushort)this.Coach.Image));

            // A changed national team gets new friendlies from the game; only the current one's are written.
            if (this.Coach.NationalTeam == this.loadedNationalTeam)
                this.WriteFriendlies();
            this.WriteNationalTeam();
        }
    }
}
