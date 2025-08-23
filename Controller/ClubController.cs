using A2G_Trainer_XP.Model;
using Memory;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
            this.Club = this.GetEntity();
            this.EntityList = this.GetEntityList();
        }

        public BindingList<Club> GetEntityList()
        {
            // Console.WriteLine($"{club.PlayerCount} Players found.");

            int end = 294;

            BindingList<Club> output = new BindingList<Club>();

            for (int i = 0; i <= end; i++)
            {
                string offset = "64FA8";
                if (i > 0)
                {
                    offset = Tools.SumHex(new string[] { output.Last().Offset, Settings.ClubOffset.ToString() });
                }
                output.Add(this.GetEntity(offset));
            }

            List<Club> sorted = output.OrderBy(c => c.ClubName).ToList();

            output.RaiseListChangedEvents = false;
            output.Clear();
            foreach (Club c in sorted)
                output.Add(c);
            output.RaiseListChangedEvents = true;
            output.ResetBindings();

            return output;
        }

        internal override Club GetEntity(string offset = "")
        {
            Club club = new Club()
            {
                Offset = offset
            };
            if (this.memory.mProc.MainModule != null)
            {
                club.Initilisation = true;
                club.PlayerCount = (ushort) this.memory.ReadByte($"{this.memory.mProc.MainModule.ModuleName}+{this.baseAddress},{Tools.SumHex(new string[] { "E6", offset })}");
                
                club.ClubName = this.memory.ReadString(GetAddress(this.memory, club, "0"), length: 19, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
                
                club.EarningsLeagueGames = BitConverter.ToInt32(this.memory.ReadBytes(GetAddress(this.memory, club, "290"), 4), 0);
                club.EarningsFriendlyGames = BitConverter.ToInt32(this.memory.ReadBytes(GetAddress(this.memory, club, "2B8"), 4), 0);
                club.EarningsAds = BitConverter.ToInt32(this.memory.ReadBytes(GetAddress(this.memory, club, "470"), 4), 0);
                
                club.StadiumName = this.memory.ReadString(GetAddress(this.memory, club, "81C"), length: 28, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
                
                club.Roof = (ClubEnums.Roof)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "890"), 2), 0);
                club.DisplayUnit = (ClubEnums.DisplayUnit)(this.memory.ReadByte(GetAddress(this.memory, club, "896")));
                club.HasFloodLight = this.memory.ReadByte(GetAddress(this.memory, club, "897")) > 0;
                club.HasGrassHeating = this.memory.ReadByte(GetAddress(this.memory, club, "898")) > 0;
                club.FieldCondition = ClubEnums.MapToCondition((byte)this.memory.ReadByte(GetAddress(this.memory, club, "89B")));
                
                club.BlockAWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "884"));
                club.BlockBWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "885"));
                club.BlockCWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "886"));
                club.BlockDWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "887"));
                club.BlockEWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "888"));
                club.BlockFWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "889"));
                club.BlockGWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "88A"));
                club.BlockHWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "88B"));
                club.BlockIWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "88C"));
                club.BlockJWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "88D"));
                club.BlockKWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "88E"));
                club.BlockLWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, "88F"));
                
                club.BlockAStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "854"), 2), 0);
                club.BlockBStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "856"), 2), 0);
                club.BlockCStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "858"), 2), 0);
                club.BlockDStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "85A"), 2), 0);
                club.BlockEStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "85C"), 2), 0);
                club.BlockFStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "85E"), 2), 0);
                club.BlockGStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "860"), 2), 0);
                club.BlockHStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "862"), 2), 0);
                club.BlockIStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "864"), 2), 0);
                club.BlockJStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "866"), 2), 0);
                club.BlockKStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "868"), 2), 0);
                club.BlockLStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "86A"), 2), 0);
                club.BlockASeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "86C"), 2), 0); // -72FF4
                club.BlockBSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "86E"), 2), 0);
                club.BlockCSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "870"), 2), 0);
                club.BlockDSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "872"), 2), 0);
                club.BlockESeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "874"), 2), 0);
                club.BlockFSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "876"), 2), 0);
                club.BlockGSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "878"), 2), 0);
                club.BlockHSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "87A"), 2), 0);
                club.BlockISeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "87C"), 2), 0);
                club.BlockJSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "87E"), 2), 0);
                club.BlockKSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "880"), 2), 0);
                club.BlockLSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, "882"), 2), 0);
            }
            club.Initilisation = false;

            return club;
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
