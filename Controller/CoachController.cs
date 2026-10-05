using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>Reads/writes a single human trainer's (Mitspieler) slot: identity, stocks, and wealth.</summary>
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
        // there. This is per-trainer-slot data (folded into Coach.Offset via GetAddress, same as
        // Level/Age/Wealth above) - an assumption, not yet live-confirmed, since each human trainer
        // presumably has their own competency history.
        private const int CompetencyTableOffset = 0x36398;
        private const int CompetencyEntryStride = 0x2;
        private static readonly CoachEnums.CompetencyKey[] CompetencyOrder = (CoachEnums.CompetencyKey[])Enum.GetValues(typeof(CoachEnums.CompetencyKey));

        internal Coach Coach { get { return this.coach; } set { this.coach = value; } }
        private Coach coach;

        /// <summary>Attaches to the game process and loads the trainer slot at <paramref name="trainerIndex"/>.</summary>
        public CoachController(ProcessMemory memory, bool isGog, PlayerEnums.AddressType type, int trainerIndex = 0) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.ClubAddress;
            this.UpdateBaseAddress(type);
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

                coach.Firstname = this.memory.ReadString(GetAddress(this.memory, coach, coach.Addresses[CoachEnums.AddressKey.FIRSTNAME]), length: 9, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
                coach.Lastname  = this.memory.ReadString(GetAddress(this.memory, coach, coach.Addresses[CoachEnums.AddressKey.LAST_NAME]), length: 15, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
                coach.Level     = (byte)this.memory.ReadByte(GetAddress(this.memory, coach, coach.Addresses[CoachEnums.AddressKey.LEVEL]));
                coach.Age       = (byte)this.memory.ReadByte(GetAddress(this.memory, coach, coach.Addresses[CoachEnums.AddressKey.AGE]));
                coach.Difficulty = (CoachEnums.Difficulty)this.memory.ReadByte(GetAddress(this.memory, coach, coach.Addresses[CoachEnums.AddressKey.DIFFICULTY]));
                coach.Games    = ReadUInt16(this.memory.ReadBytes(GetAddress(this.memory, coach, coach.Addresses[CoachEnums.AddressKey.GAMES]), 2));
                coach.Wins     = ReadUInt16(this.memory.ReadBytes(GetAddress(this.memory, coach, coach.Addresses[CoachEnums.AddressKey.WINS]), 2));

                for (int slot = 0; slot < coach.Stocks.Length; slot++)
                {
                    Stock stock = coach.Stocks[slot];
                    stock.Country = (PlayerEnums.Country)this.memory.ReadByte(this.GetStockAddress(coach, CoachEnums.AddressKey.STOCK_COUNTRY, slot));
                    stock.ClubId  = (byte)this.memory.ReadByte(this.GetStockAddress(coach, CoachEnums.AddressKey.STOCK_CLUB, slot));
                    stock.Shares  = ReadUInt16(this.memory.ReadBytes(this.GetStockAddress(coach, CoachEnums.AddressKey.STOCK_SHARES, slot), 2));
                    stock.Price   = ReadUInt16(this.memory.ReadBytes(this.GetStockAddress(coach, CoachEnums.AddressKey.STOCK_PRICE, slot), 2));
                }

                coach.Wealth = ReadInt32(this.memory.ReadBytes(GetAddress(this.memory, coach, coach.Addresses[CoachEnums.AddressKey.WEALTH]), 4));

                for (int level = Coach.MinLevel; level <= Coach.MaxLevel; level++)
                {
                    CoachCompetencyLevel competencyLevel = coach.CompetencyLevels[level];
                    for (int i = 0; i < CompetencyOrder.Length; i++)
                    {
                        ushort value = ReadUInt16(this.memory.ReadBytes(this.GetCompetencyAddress(coach, level, i), 2));
                        competencyLevel.Set(CompetencyOrder[i], value);
                    }
                }

                coach.Initilisation = false;
            }
            return coach;
        }

        // Stock slots aren't separate dictionary entries - each one is the slot-0 offset (from
        // AddressPresets.COACH) plus slotIndex * StockSlotStride, computed here instead.
        private string GetStockAddress(Coach coach, CoachEnums.AddressKey key, int slotIndex)
        {
            string slotOffset = Tools.SumHex(new[] { coach.Addresses[key], (slotIndex * StockSlotStride).ToString("X") });
            return GetAddress(this.memory, coach, slotOffset);
        }

        // Competency slots aren't separate dictionary entries either - level and competency-within-
        // level combine into a single flat byte offset from CompetencyTableOffset, same idea as
        // GetStockAddress above.
        private string GetCompetencyAddress(Coach coach, int level, int competencyIndex)
        {
            int byteOffset = CompetencyTableOffset + (level * CompetencyOrder.Length + competencyIndex) * CompetencyEntryStride;
            return GetAddress(this.memory, coach, byteOffset.ToString("X"));
        }

        // An address that fails to resolve (e.g. an unmapped pointer target) must not blow up the
        // whole refresh cycle for every view, the way an unguarded BitConverter call on null would.
        private static ushort ReadUInt16(byte[] bytes) => bytes != null ? BitConverter.ToUInt16(bytes, 0) : (ushort)0;
        private static int ReadInt32(byte[] bytes) => bytes != null ? BitConverter.ToInt32(bytes, 0) : 0;

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

        /// <summary>Writes this trainer slot's identity, stocks, and wealth back to memory.</summary>
        public void Save()
        {
            Logger.Debug($"Saving coach: {this.Coach.Firstname} {this.Coach.Lastname}");

            this.memory.WriteMemory(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.FIRSTNAME]), "string", this.Coach.Firstname.PadRight(9, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));
            this.memory.WriteMemory(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.LAST_NAME]), "string", this.Coach.Lastname.PadRight(15, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));

            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.LEVEL]), new byte[] { this.Coach.Level });
            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.AGE]), new byte[] { this.Coach.Age });
            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.DIFFICULTY]), new byte[] { (byte)this.Coach.Difficulty });
            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.GAMES]), BitConverter.GetBytes(this.Coach.Games));
            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.WINS]), BitConverter.GetBytes(this.Coach.Wins));

            for (int slot = 0; slot < this.Coach.Stocks.Length; slot++)
            {
                Stock stock = this.Coach.Stocks[slot];
                this.memory.WriteBytes(this.GetStockAddress(this.Coach, CoachEnums.AddressKey.STOCK_COUNTRY, slot), new byte[] { (byte)stock.Country });
                this.memory.WriteBytes(this.GetStockAddress(this.Coach, CoachEnums.AddressKey.STOCK_CLUB, slot), new byte[] { stock.ClubId });
                this.memory.WriteBytes(this.GetStockAddress(this.Coach, CoachEnums.AddressKey.STOCK_SHARES, slot), BitConverter.GetBytes(stock.Shares));
                this.memory.WriteBytes(this.GetStockAddress(this.Coach, CoachEnums.AddressKey.STOCK_PRICE, slot), BitConverter.GetBytes(stock.Price));
            }

            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.WEALTH]), BitConverter.GetBytes(this.Coach.Wealth));

            for (int level = Coach.MinLevel; level <= Coach.MaxLevel; level++)
            {
                CoachCompetencyLevel competencyLevel = this.Coach.CompetencyLevels[level];
                for (int i = 0; i < CompetencyOrder.Length; i++)
                {
                    ushort value = competencyLevel.Get(CompetencyOrder[i]);
                    this.memory.WriteBytes(this.GetCompetencyAddress(this.Coach, level, i), BitConverter.GetBytes(value));
                }
            }
        }
    }
}
