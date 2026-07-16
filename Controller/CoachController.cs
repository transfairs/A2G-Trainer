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
        internal Coach Coach { get { return this.coach; } set { this.coach = value; } }
        private Coach coach;

        public CoachController(ProcessMemory memory, bool isGog, PlayerEnums.AddressType type) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.ClubAddress;
            this.UpdateBaseAddress(type);
            this.Coach = this.GetEntity("", type);
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

                coach.Initilisation = false;
            }
            return coach;
        }

        public void Save()
        {
            Console.WriteLine($"Save: {this.Coach.Firstname} {this.Coach.Lastname}");

            this.memory.WriteMemory(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.FIRSTNAME]), "string", this.Coach.Firstname.PadRight(9, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));
            this.memory.WriteMemory(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.LAST_NAME]), "string", this.Coach.Lastname.PadRight(15, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));

            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.LEVEL]), new byte[] { this.Coach.Level });
            this.memory.WriteBytes(GetAddress(this.memory, this.Coach, this.Coach.Addresses[CoachEnums.AddressKey.AGE]), new byte[] { this.Coach.Age });
        }
    }
}
