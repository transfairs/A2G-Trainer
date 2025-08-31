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

        private readonly bool showLog = false;

        public ClubController(Mem memory, bool isGog, PlayerEnums.AddressType type) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.ClubAddress;
            this.UpdateBaseAddress(type);
            this.showLog = true;
            this.Club = this.GetEntity(type == PlayerEnums.AddressType.OTHER ? Settings.AllClubOffset : "", type);
            this.showLog = false;
            // Console.WriteLine($"{club.ClubName}, {type}: {club.PlayerCount} ({club.AmateurPlayerCount})");
            this.EntityList = this.GetEntityList();
        }

        public BindingList<Club> GetEntityList()
        {
            // Console.WriteLine($"{club.PlayerCount} Players found.");

            int end = 294;

            BindingList<Club> output = new BindingList<Club>();

            for (int i = 0; i <= end; i++)
            {
                string offset = Settings.AllClubOffset;
                if (i > 0)
                {
                    offset = Tools.SumHex(new string[] { output.Last().Offset, Settings.ClubOffset.ToString() });
                }
                output.Add(this.GetEntity(offset, PlayerEnums.AddressType.OTHER));
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

        internal override Club GetEntity(string offset, PlayerEnums.AddressType type)
        {
            Club club = new Club()
            {
                Offset = offset,
                Addresses = AddressPresets.From(type, true)
            };



            if (this.memory.mProc.MainModule != null)
            {
                club.Initilisation = true;
                club.PlayerCount = (ushort) this.memory.ReadByte($"{this.memory.mProc.MainModule.ModuleName}+{this.baseAddress},{Tools.SumHex(new string[] { club.Addresses[ClubEnums.AddressKey.PLAYER_COUNT], offset })}");
                club.AmateurPlayerCount = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT]));

                club.ClubName = this.memory.ReadString(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.NAME]), length: 19, stringEncoding: Encoding.GetEncoding("iso-8859-1"));

                if (this.showLog)
                    Console.WriteLine($"{club.ClubName}: {club.PlayerCount} ({club.AmateurPlayerCount})");


                if (type == PlayerEnums.AddressType.OWN)
                {
                    club.EarningsLeagueGames = BitConverter.ToInt32(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.EarningsLeagueGames]), 4), 0);
                    club.EarningsFriendlyGames = BitConverter.ToInt32(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.EarningsFriendlyGames]), 4), 0);
                    club.EarningsAds = BitConverter.ToInt32(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.EarningsAds]), 4), 0);

                    club.StadiumName = this.memory.ReadString(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.StadiumName]), length: 28, stringEncoding: Encoding.GetEncoding("iso-8859-1"));

                    club.Roof = (ClubEnums.Roof)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.ROOF]), 2), 0);
                    club.DisplayUnit = (ClubEnums.DisplayUnit)(this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.DisplayUnit])));
                    club.HasFloodLight = this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.HasFloodLight])) > 0;
                    club.HasGrassHeating = this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.HasGrassHeating])) > 0;
                    club.FieldCondition = ClubEnums.MapToCondition((byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.FieldCondition])));

                    club.BlockAWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockAWeeks]));
                    club.BlockBWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockBWeeks]));
                    club.BlockCWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockCWeeks]));
                    club.BlockDWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockDWeeks]));
                    club.BlockEWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockEWeeks]));
                    club.BlockFWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockFWeeks]));
                    club.BlockGWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockGWeeks]));
                    club.BlockHWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockHWeeks]));
                    club.BlockIWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockIWeeks]));
                    club.BlockJWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockJWeeks]));
                    club.BlockKWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockKWeeks]));
                    club.BlockLWeeks = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockLWeeks]));

                    club.BlockAStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockAStandings]), 2), 0);
                    club.BlockBStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockBStandings]), 2), 0);
                    club.BlockCStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockCStandings]), 2), 0);
                    club.BlockDStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockDStandings]), 2), 0);
                    club.BlockEStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockEStandings]), 2), 0);
                    club.BlockFStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockFStandings]), 2), 0);
                    club.BlockGStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockGStandings]), 2), 0);
                    club.BlockHStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockHStandings]), 2), 0);
                    club.BlockIStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockIStandings]), 2), 0);
                    club.BlockJStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockJStandings]), 2), 0);
                    club.BlockKStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockKStandings]), 2), 0);
                    club.BlockLStandings = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockLStandings]), 2), 0);
                    club.BlockASeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockASeats]), 2), 0); // -72FF4
                    club.BlockBSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockBSeats]), 2), 0);
                    club.BlockCSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockCSeats]), 2), 0);
                    club.BlockDSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockDSeats]), 2), 0);
                    club.BlockESeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockESeats]), 2), 0);
                    club.BlockFSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockFSeats]), 2), 0);
                    club.BlockGSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockGSeats]), 2), 0);
                    club.BlockHSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockHSeats]), 2), 0);
                    club.BlockISeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockISeats]), 2), 0);
                    club.BlockJSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockJSeats]), 2), 0);
                    club.BlockKSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockKSeats]), 2), 0);
                    club.BlockLSeats = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockLSeats]), 2), 0);
                }
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
