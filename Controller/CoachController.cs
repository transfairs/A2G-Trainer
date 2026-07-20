using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace A2G_Trainer_XP.Controller
{
    public class CoachController : EntityController<Coach>
    {
        // Distance between one stock slot and the next (Aktie 1 -> Aktie 2 is 34BD8 -> 34BE8).
        private const int StockSlotStride = 0x10;
        // Distance between one player's whole Coach struct and the next (Player 1 -> Player 2
        // Firstname is 857800 -> 859158). Folded into Coach.Offset, which every existing field
        // address already adds in - see GetAddress in EntityController.
        private const int TrainerSlotStride = 0x1958;

        internal Coach Coach { get { return this.coach; } set { this.coach = value; } }
        private Coach coach;

        public CoachController(ProcessMemory memory, bool isGog, PlayerEnums.AddressType type, int trainerIndex = 0) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.ClubAddress;
            this.UpdateBaseAddress(type);
            this.Coach = this.GetEntity((trainerIndex * TrainerSlotStride).ToString("X"), type);
        }

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

                for (int slot = 0; slot < coach.Stocks.Length; slot++)
                {
                    Stock stock = coach.Stocks[slot];
                    stock.Country = (PlayerEnums.Country)this.memory.ReadByte(this.GetStockAddress(coach, CoachEnums.AddressKey.STOCK_COUNTRY, slot));
                    stock.ClubId  = (byte)this.memory.ReadByte(this.GetStockAddress(coach, CoachEnums.AddressKey.STOCK_CLUB, slot));
                    stock.Shares  = ReadUInt16(this.memory.ReadBytes(this.GetStockAddress(coach, CoachEnums.AddressKey.STOCK_SHARES, slot), 2));
                    stock.Price   = ReadUInt16(this.memory.ReadBytes(this.GetStockAddress(coach, CoachEnums.AddressKey.STOCK_PRICE, slot), 2));
                }

                coach.Wealth = ReadInt32(this.memory.ReadBytes(GetAddress(this.memory, coach, coach.Addresses[CoachEnums.AddressKey.WEALTH]), 4));

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

        // An address that fails to resolve (e.g. an unmapped pointer target) must not blow up the
        // whole refresh cycle for every view, the way an unguarded BitConverter call on null would.
        private static ushort ReadUInt16(byte[] bytes) => bytes != null ? BitConverter.ToUInt16(bytes, 0) : (ushort)0;
        private static int ReadInt32(byte[] bytes) => bytes != null ? BitConverter.ToInt32(bytes, 0) : 0;

        // Not every one of the up to Coach.MaxTrainers slots is necessarily used by a given
        // savegame - an empty Firstname marks an unused slot, since a real human manager always has one.
        internal static List<KeyValuePair<int, Coach>> GetActiveTrainers(ProcessMemory memory, bool isGog, PlayerEnums.AddressType type)
        {
            List<KeyValuePair<int, Coach>> trainers = new List<KeyValuePair<int, Coach>>();
            for (int i = 0; i < Coach.MaxTrainers; i++)
            {
                Coach coach = new CoachController(memory, isGog, type, i).Coach;
                if (!string.IsNullOrWhiteSpace(coach.Firstname))
                    trainers.Add(new KeyValuePair<int, Coach>(i, coach));
            }
            return trainers;
        }

        public void Save()
        {
            Logger.Debug($"Saving coach: {this.Coach.Firstname} {this.Coach.Lastname}");

            this.memory.WriteMemory(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.FIRSTNAME]), "string", this.Coach.Firstname.PadRight(9, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));
            this.memory.WriteMemory(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.LAST_NAME]), "string", this.Coach.Lastname.PadRight(15, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));

            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.LEVEL]), new byte[] { this.Coach.Level });
            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.AGE]), new byte[] { this.Coach.Age });

            for (int slot = 0; slot < this.Coach.Stocks.Length; slot++)
            {
                Stock stock = this.Coach.Stocks[slot];
                this.memory.WriteBytes(this.GetStockAddress(this.Coach, CoachEnums.AddressKey.STOCK_COUNTRY, slot), new byte[] { (byte)stock.Country });
                this.memory.WriteBytes(this.GetStockAddress(this.Coach, CoachEnums.AddressKey.STOCK_CLUB, slot), new byte[] { stock.ClubId });
                this.memory.WriteBytes(this.GetStockAddress(this.Coach, CoachEnums.AddressKey.STOCK_SHARES, slot), BitConverter.GetBytes(stock.Shares));
                this.memory.WriteBytes(this.GetStockAddress(this.Coach, CoachEnums.AddressKey.STOCK_PRICE, slot), BitConverter.GetBytes(stock.Price));
            }

            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.WEALTH]), BitConverter.GetBytes(this.Coach.Wealth));
        }
    }
}
