using A2G_Trainer_XP.Model;
using Memory;
using System;

namespace A2G_Trainer_XP.Controller
{
    class ClubController : EntityController<Club>
    {
        internal Club Club { get { return this.club; } set { this.club = value; } }
        private Club club;

        public string temp;
        public ClubController(Mem memory, bool isGog) : base(memory)
        {
            this.baseAddress = isGog ? Settings.ClubAddressGog : Settings.ClubAddress;
            this.GetEntity();
        }

        internal override Club GetEntity(string offset = "")
        {
            this.club = new Club();
            if (this.memory.mProc.MainModule != null)
            {
                this.club.PlayerCount = (ushort) this.memory.ReadByte($"{this.memory.mProc.MainModule.ModuleName}+{this.baseAddress},{Tools.SumHex(new string[] { "E6", offset })}");
            }

            return this.club;
        }
    }
}
