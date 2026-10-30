using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer_XP.Controller
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
                coach.Age       = ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.AGE], 1));
                coach.Difficulty = (CoachEnums.Difficulty)ReadByte(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.DIFFICULTY], 1));
                coach.Games    = ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.GAMES], 2));
                coach.Wins     = ReadUInt16(this.ReadField(coach, coach.Addresses[CoachEnums.AddressKey.WINS], 2));

                for (int slot = 0; slot < coach.Stocks.Length; slot++)
                {
                    Stock stock = coach.Stocks[slot];
                    stock.Country = (PlayerEnums.Country)ReadByte(this.ReadField(coach, GetStockOffset(coach, CoachEnums.AddressKey.STOCK_COUNTRY, slot), 1));
                    stock.ClubId  = ReadByte(this.ReadField(coach, GetStockOffset(coach, CoachEnums.AddressKey.STOCK_CLUB, slot), 1));
                    stock.Shares  = ReadUInt16(this.ReadField(coach, GetStockOffset(coach, CoachEnums.AddressKey.STOCK_SHARES, slot), 2));
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
        // Mitspieler's record, except for the competency table, which lives in its own array.
        private uint RecordAddress(uint slotOffset)
        {
            uint competencyStart = (uint)CompetencyTableOffset - PointerRelativeSlotBase;
            if (slotOffset >= competencyStart)
                return this.Fixed.Runtime(this.memory, Settings.CoachCompetencyRecordOffset) + this.MitspielerId * Settings.CoachCompetencyRecordStride + (slotOffset - competencyStart);

            return this.Fixed.Runtime(this.memory, Settings.CoachRecordTableOffset) + this.MitspielerId * Settings.CoachRecordStride + slotOffset;
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
        // (FUN_00585b00 -> FUN_00585ff0); for any other club this returns no addresses.
        private static readonly PlayerEnums.Country[] LeagueRecordCountries =
            { PlayerEnums.Country.Deutschland, PlayerEnums.Country.England, PlayerEnums.Country.Frankreich, PlayerEnums.Country.Italien, PlayerEnums.Country.Spanien };

        private List<uint> ClubSeasonGoalAddresses(Coach coach)
        {
            List<uint> addresses = new List<uint>();
            if (!this.HasFixedAddresses || !LeagueRecordCountries.Contains(coach.CurrentCountry))
                return addresses;

            byte[] system = this.memory.ReadBytesAtAddress(this.Fixed.Static(this.memory, Settings.LeagueSystemByCountryOffset) + (uint)coach.CurrentCountry, 1);
            if (system == null)
                return addresses;
            byte[] count = this.memory.ReadBytesAtAddress(this.Fixed.Static(this.memory, Settings.LeagueClubCountTableOffset) + (uint)system[0] * 2, 2);
            byte[] first = this.memory.ReadBytesAtAddress(this.Fixed.Static(this.memory, Settings.LeagueBaseTableOffset) + (uint)system[0] * 2, 2);
            if (count == null || first == null || coach.CurrentClubId >= BitConverter.ToInt16(count, 0))
                return addresses;

            // Cached working copies first: they're newer than the record while the game has the club fetched.
            uint cache = this.Fixed.Runtime(this.memory, Settings.ClubCacheTableOffset);
            for (int slot = 0; slot < Settings.ClubCacheSlots; slot++)
            {
                byte[] entry = this.memory.ReadBytesAtAddress(cache + (uint)slot * Settings.ClubCacheStride, (int)Settings.ClubCacheStride);
                if (entry == null || entry[0] == 0 || entry[1] != (byte)coach.CurrentCountry || entry[2] != coach.CurrentClubId)
                    continue;
                uint workingCopy = BitConverter.ToUInt32(entry, (int)Settings.ClubCachePointerOffset);
                if (workingCopy != 0)
                    addresses.Add(workingCopy + Settings.ClubSeasonGoalOffset);
            }

            uint record = (uint)(BitConverter.ToInt16(first, 0) + coach.CurrentClubId);
            addresses.Add(this.Fixed.Runtime(this.memory, Settings.ClubRecordTableOffset) + record * Settings.ClubRecordStride + Settings.ClubRecordSeasonGoalOffset);
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
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.AGE], new byte[] { this.Coach.Age });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.DIFFICULTY], new byte[] { (byte)this.Coach.Difficulty });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.GAMES], BitConverter.GetBytes(this.Coach.Games));
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.WINS], BitConverter.GetBytes(this.Coach.Wins));

            for (int slot = 0; slot < this.Coach.Stocks.Length; slot++)
            {
                Stock stock = this.Coach.Stocks[slot];
                this.WriteField(this.Coach, GetStockOffset(this.Coach, CoachEnums.AddressKey.STOCK_COUNTRY, slot), new byte[] { (byte)stock.Country });
                this.WriteField(this.Coach, GetStockOffset(this.Coach, CoachEnums.AddressKey.STOCK_CLUB, slot), new byte[] { stock.ClubId });
                this.WriteField(this.Coach, GetStockOffset(this.Coach, CoachEnums.AddressKey.STOCK_SHARES, slot), BitConverter.GetBytes(stock.Shares));
                this.WriteField(this.Coach, GetStockOffset(this.Coach, CoachEnums.AddressKey.STOCK_PRICE, slot), BitConverter.GetBytes(stock.Price));
            }

            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.WEALTH], BitConverter.GetBytes(this.Coach.Wealth));

            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.COUNTRY_NEXT_SEASON], new byte[] { (byte)this.Coach.CountryNextSeason });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.CLUB_NEXT_SEASON], new byte[] { this.Coach.ClubIdNextSeason });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.RELEASE_CLAUSE], new byte[] { this.Coach.ReleaseClause });
            this.WriteField(this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.BOARD_RELEASE], new byte[] { this.Coach.BoardRelease });

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
        }
    }
}
