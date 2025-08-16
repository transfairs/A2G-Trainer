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
                this.Club.Initilisation = true;
                this.Club.PlayerCount = (ushort) this.memory.ReadByte($"{this.memory.mProc.MainModule.ModuleName}+{this.baseAddress},{Tools.SumHex(new string[] { "E6", offset })}");

                this.Club.ClubName = this.memory.ReadString(GetAddress(this.memory, this.Club, "0"), length: 19, stringEncoding: Encoding.GetEncoding("iso-8859-1"));

                this.Club.EarningsLeagueGames = BitConverter.ToInt32(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "290"), 4), 0);
                this.Club.EarningsFriendlyGames = BitConverter.ToInt32(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "2B8"), 4), 0);
                this.Club.EarningsAds = BitConverter.ToInt32(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "470"), 4), 0);

                this.Club.StadiumName = this.memory.ReadString(GetAddress(this.memory, this.Club, "81C"), length: 28, stringEncoding: Encoding.GetEncoding("iso-8859-1"));

                this.Club.Roof = (ClubEnums.Roof)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "890"), 2), 0);
                this.Club.DisplayUnit = (ClubEnums.DisplayUnit)(this.memory.ReadByte(GetAddress(this.memory, this.Club, "896")));
                this.Club.HasFloodLight = this.memory.ReadByte(GetAddress(this.memory, this.Club, "897")) > 0;
                this.Club.HasGrassHeating = this.memory.ReadByte(GetAddress(this.memory, this.Club, "898")) > 0;
                this.Club.FieldCondition = ClubEnums.MapToCondition((byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "89B")));

                this.Club.BlockAWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "884"));
                this.Club.BlockBWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "885"));
                this.Club.BlockCWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "886"));
                this.Club.BlockDWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "887"));
                this.Club.BlockEWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "888"));
                this.Club.BlockFWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "889"));
                this.Club.BlockGWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "88A"));
                this.Club.BlockHWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "88B"));
                this.Club.BlockIWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "88C"));
                this.Club.BlockJWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "88D"));
                this.Club.BlockKWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "88E"));
                this.Club.BlockLWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, this.Club, "88F"));

                this.Club.BlockAStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "854"), 2), 0);
                this.Club.BlockBStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "856"), 2), 0);
                this.Club.BlockCStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "858"), 2), 0);
                this.Club.BlockDStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "85A"), 2), 0);
                this.Club.BlockEStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "85C"), 2), 0);
                this.Club.BlockFStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "85E"), 2), 0);
                this.Club.BlockGStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "860"), 2), 0);
                this.Club.BlockHStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "862"), 2), 0);
                this.Club.BlockIStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "864"), 2), 0);
                this.Club.BlockJStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "866"), 2), 0);
                this.Club.BlockKStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "868"), 2), 0);
                this.Club.BlockLStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "86A"), 2), 0);
                this.Club.BlockASeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "86C"), 2), 0); // -72FF4
                this.Club.BlockBSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "86E"), 2), 0);
                this.Club.BlockCSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "870"), 2), 0);
                this.Club.BlockDSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "872"), 2), 0);
                this.Club.BlockESeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "874"), 2), 0);
                this.Club.BlockFSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "876"), 2), 0);
                this.Club.BlockGSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "878"), 2), 0);
                this.Club.BlockHSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "87A"), 2), 0);
                this.Club.BlockISeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "87C"), 2), 0);
                this.Club.BlockJSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "87E"), 2), 0);
                this.Club.BlockKSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "880"), 2), 0);
                this.Club.BlockLSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, this.Club, "882"), 2), 0);
            }
            this.Club.Initilisation = false;

            return this.Club;
        }
        public void Save()
        {
            Console.WriteLine($"Save: {this.Club.ClubName}");

            this.memory.WriteMemory(GetAddress(this.memory, this.Club, "0"), "string", this.Club.ClubName.PadRight(19, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));
            this.memory.WriteMemory(GetAddress(this.memory, this.Club, "81C"), "string", this.Club.StadiumName.PadRight(28, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));

            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "290"), BitConverter.GetBytes((int)this.Club.EarningsLeagueGames));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "2B8"), BitConverter.GetBytes((int)this.Club.EarningsFriendlyGames));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "470"), BitConverter.GetBytes((int)this.Club.EarningsAds));


            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "890"), BitConverter.GetBytes((ushort)this.Club.Roof));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "896"), new byte[] { (byte)this.Club.DisplayUnit });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "897"), new byte[] { this.Club.HasFloodLight ? (byte)1 : (byte)0 });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "898"), new byte[] { (byte)(this.Club.HasGrassHeating ? 1 : 0) });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "89B"), BitConverter.GetBytes((byte)this.Club.FieldCondition));

            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "854"), BitConverter.GetBytes((ushort)this.Club.BlockAStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "856"), BitConverter.GetBytes((ushort)this.Club.BlockBStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "858"), BitConverter.GetBytes((ushort)this.Club.BlockCStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "85A"), BitConverter.GetBytes((ushort)this.Club.BlockDStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "85C"), BitConverter.GetBytes((ushort)this.Club.BlockEStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "85E"), BitConverter.GetBytes((ushort)this.Club.BlockFStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "860"), BitConverter.GetBytes((ushort)this.Club.BlockGStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "862"), BitConverter.GetBytes((ushort)this.Club.BlockHStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "864"), BitConverter.GetBytes((ushort)this.Club.BlockIStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "866"), BitConverter.GetBytes((ushort)this.Club.BlockJStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "868"), BitConverter.GetBytes((ushort)this.Club.BlockKStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "86A"), BitConverter.GetBytes((ushort)this.Club.BlockLStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "86C"), BitConverter.GetBytes((ushort)this.Club.BlockASeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "86E"), BitConverter.GetBytes((ushort)this.Club.BlockBSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "870"), BitConverter.GetBytes((ushort)this.Club.BlockCSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "872"), BitConverter.GetBytes((ushort)this.Club.BlockDSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "874"), BitConverter.GetBytes((ushort)this.Club.BlockESeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "876"), BitConverter.GetBytes((ushort)this.Club.BlockFSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "878"), BitConverter.GetBytes((ushort)this.Club.BlockGSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "87A"), BitConverter.GetBytes((ushort)this.Club.BlockHSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "87C"), BitConverter.GetBytes((ushort)this.Club.BlockISeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "87E"), BitConverter.GetBytes((ushort)this.Club.BlockJSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "880"), BitConverter.GetBytes((ushort)this.Club.BlockKSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "882"), BitConverter.GetBytes((ushort)this.Club.BlockLSeats));

            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "884"), new byte[] { this.Club.BlockAWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "885"), new byte[] { this.Club.BlockBWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "886"), new byte[] { this.Club.BlockCWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "887"), new byte[] { this.Club.BlockDWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "888"), new byte[] { this.Club.BlockEWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "889"), new byte[] { this.Club.BlockFWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "88A"), new byte[] { this.Club.BlockGWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "88B"), new byte[] { this.Club.BlockHWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "88C"), new byte[] { this.Club.BlockIWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "88D"), new byte[] { this.Club.BlockJWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "88E"), new byte[] { this.Club.BlockKWeeks });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "88F"), new byte[] { this.Club.BlockLWeeks });

            // this.memory.WriteBytes(GetAddress(this.memory, this.Club, "854"), new byte[] { (byte)player.SkinColor });
        }
    }
}
