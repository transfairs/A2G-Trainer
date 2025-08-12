using A2G_Trainer_XP.Model;
using Memory;
using System;
using System.Text;

namespace A2G_Trainer_XP.Controller
{
    public class ClubController : EntityController<Club>
    {
        internal Club Club { get { return this.club; } set { this.club = value; } }
        private Club club;

        public ClubController(Mem memory, bool isGog) : base(memory)
        {
            this.baseAddress = isGog ? Settings.ClubAddressGog : Settings.ClubAddress;
            this.GetEntity();
        }

        internal override Club GetEntity(string offset = "")
        {
            this.Club = new Club();
            if (this.memory.mProc.MainModule != null)
            {
                this.Club.PlayerCount = (ushort) this.memory.ReadByte($"{this.memory.mProc.MainModule.ModuleName}+{this.baseAddress},{Tools.SumHex(new string[] { "E6", offset })}");

                this.Club.ClubName = this.memory.ReadString(GetAddress(this.memory, this.Club, "0"), length: 19, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
                this.Club.StadiumName = this.memory.ReadString(GetAddress(this.memory, this.Club, "81C"), length: 28, stringEncoding: Encoding.GetEncoding("iso-8859-1"));

                this.Club.Roof = (ClubEnums.Roof)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "890"), 2), 0);
                this.Club.BlockAStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "854"), 2), 0);
                this.Club.BlockASeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "86C"), 2), 0); // -72FF4
            }

            return this.Club;
        }
    }
}
