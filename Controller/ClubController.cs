using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>Reads/writes a club's identity, stadium, and finances, and can scan every club in the league.</summary>
    public class ClubController : EntityController<Club>
    {
        internal Club Club { get { return this.club; } set { this.club = value; } }
        private Club club;

        /// <summary>Attaches to the game process and loads the club matching <paramref name="type"/>, optionally scanning the full club list too.</summary>
        public ClubController(ProcessMemory memory, bool isGog, PlayerEnums.AddressType type, bool loadFullList = false) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.ClubAddress;
            this.UpdateBaseAddress(type);
            this.Club = this.GetEntity(type == PlayerEnums.AddressType.ALL ? Settings.AllClubInitialOffset.Key : "", type);

            // Scanning every club is only needed for DYNAMIC (club-membership lookup) and
            // TRAINEE/ALL (trainee counts / club picker) - skip it for the common OWN/OPPONENT path.
            if (loadFullList)
            {
                this.EntityList = this.GetEntityList();

                if (this.EntityList != null)
                {
                    Club clubInList = this.EntityList.FirstOrDefault(c => c.Id == this.Club.Id && c.Country == this.Club.Country);
                    if (clubInList != null)
                    {
                        this.Club.TraineeACount = clubInList.TraineeACount;
                        this.Club.TraineeBCount = clubInList.TraineeBCount;
                        this.Club.TraineeCCount = clubInList.TraineeCCount;
                    }
                }
            }
        }

        /// <summary>Scans every playable and non-playable club, sorted by name, with trainee counts resolved for the ALL region.</summary>
        public BindingList<Club> GetEntityList()
        {
            BindingList<Club> output = new BindingList<Club>();

            Dictionary<string, Tuple<ushort, PlayerEnums.AddressType>> clubRegions = new Dictionary<string, Tuple<ushort, PlayerEnums.AddressType>>
            {
                { Settings.AllClubInitialOffset.Key, Settings.AllClubInitialOffset.Value },
                { Settings.NonPlayableInitialOffset.Key, Settings.NonPlayableInitialOffset.Value }
            };

            foreach (KeyValuePair<string, Tuple<ushort, PlayerEnums.AddressType>> clubRegion in clubRegions)
            {
                string lastTraineeOffset = clubRegion.Key;
                for (ushort i = 0; i <= clubRegion.Value.Item1; i++)
                {
                    // One bad slot (e.g. an unmapped pointer target) must not abort the whole
                    // 294+100-entry scan - skip it and keep going.
                    try
                    {
                        string offset = clubRegion.Key;
                        if (i > 0)
                        {
                            offset = Tools.SumHex(new string[] { output.Last().Offset, clubRegion.Value.Item2 == PlayerEnums.AddressType.ALL ? Settings.ClubOffset : Settings.NonPlayableOffset });
                        }
                        Club c = this.GetEntity(offset, clubRegion.Value.Item2);

                        if (clubRegion.Value.Item2 == PlayerEnums.AddressType.ALL)
                        {
                            string offsetBackup = c.Offset;
                            if (i > 0)
                            {
                                lastTraineeOffset = Tools.SumHex(new string[] { lastTraineeOffset, Settings.AllTraineeOffset });
                            }
                            c.Offset = lastTraineeOffset;
                            this.GetTraineeCount(c);
                            lastTraineeOffset = c.Offset;
                            c.Offset = offsetBackup;
                        }

                        output.Add(c);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Club scan: skipping slot i={i} in region {clubRegion.Value.Item2}", ex);
                    }
                }
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

        private void GetTraineeCount(Club club)
        {
            if (club != null)
            {
                club.TraineeACount = 0;
                club.TraineeBCount = 0;
                club.TraineeCCount = 0;
                if (club.Addresses.ContainsKey(ClubEnums.AddressKey.TRAINEE))
                {
                    club.TraineeACount = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.TRAINEE_A]));
                    club.TraineeBCount = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.TRAINEE_B]));
                    club.TraineeCCount = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.TRAINEE_C]));
                }
            }
        }

        /// <summary>Reads a club's identity fields, and its stadium/finances too when it's the user's own club (type == OWN).</summary>
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
                club.AmateurPlayerCount = (byte) (club.Addresses[ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT].StartsWith("-") ? 0 : this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT])));

                club.ClubName = this.memory.ReadString(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.NAME]), length: 19, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
                club.Id = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.ID]));
                club.Country = (PlayerEnums.Country) this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.COUNTRY]));

                Logger.Debug($"{club.ClubName}: {club.Id} ({club.Country}), {club.PlayerCount} ({club.AmateurPlayerCount}), ({club.TraineeACount}, {club.TraineeBCount}, {club.TraineeCCount})");


                if (type == PlayerEnums.AddressType.OWN)
                {
                    club.Wealth = this.memory.ReadInt32(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.Wealth]));
                    club.EarningsLeagueGames = this.memory.ReadInt32(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.EarningsLeagueGames]));
                    club.EarningsFriendlyGames = this.memory.ReadInt32(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.EarningsFriendlyGames]));
                    club.EarningsAds = this.memory.ReadInt32(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.EarningsAds]));
                    club.SponsorCash = this.memory.ReadInt32(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.SponsorCash]));
                    club.SponsorPeriod = (byte) this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.SponsorPeriod]));

                    club.Respect = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.Respect]));
                    club.Spirit = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.Spirit]));
                    club.Will2Win = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.Will2Win]));
                    club.TeamCohesion = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.TeamCohesion]));

                    club.FreeTickets = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.FreeTickets]));
                    club.RoadGameSupport = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.RoadGameSupport]));

                    club.StadiumName = this.memory.ReadString(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.StadiumName]), length: 28, stringEncoding: Encoding.GetEncoding("iso-8859-1"));

                    club.Roof = (ClubEnums.Roof)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.ROOF]));
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

                    club.BlockAStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockAStandings]));
                    club.BlockBStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockBStandings]));
                    club.BlockCStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockCStandings]));
                    club.BlockDStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockDStandings]));
                    club.BlockEStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockEStandings]));
                    club.BlockFStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockFStandings]));
                    club.BlockGStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockGStandings]));
                    club.BlockHStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockHStandings]));
                    club.BlockIStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockIStandings]));
                    club.BlockJStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockJStandings]));
                    club.BlockKStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockKStandings]));
                    club.BlockLStandings = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockLStandings]));
                    club.BlockASeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockASeats])); // -72FF4
                    club.BlockBSeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockBSeats]));
                    club.BlockCSeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockCSeats]));
                    club.BlockDSeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockDSeats]));
                    club.BlockESeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockESeats]));
                    club.BlockFSeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockFSeats]));
                    club.BlockGSeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockGSeats]));
                    club.BlockHSeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockHSeats]));
                    club.BlockISeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockISeats]));
                    club.BlockJSeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockJSeats]));
                    club.BlockKSeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockKSeats]));
                    club.BlockLSeats = this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockLSeats]));
                }
            }
            club.Initilisation = false;

            return club;
        }
        /// <summary>Writes the user's own club (identity, stadium, finances) back to memory.</summary>
        public void Save()
        {
            Logger.Debug($"Saving club: {this.Club.ClubName}");

            this.memory.WriteMemory(GetAddress(this.memory, this.Club, "0"), "string", this.Club.ClubName.PadRight(19, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));
            this.memory.WriteMemory(GetAddress(this.memory, this.Club, "81C"), "string", this.Club.StadiumName.PadRight(28, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));

            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.Wealth]), BitConverter.GetBytes(this.Club.Wealth));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "290"), BitConverter.GetBytes(this.Club.EarningsLeagueGames));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "2B8"), BitConverter.GetBytes(this.Club.EarningsFriendlyGames));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "470"), BitConverter.GetBytes(this.Club.EarningsAds));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.SponsorCash]), BitConverter.GetBytes(this.Club.SponsorCash));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.SponsorPeriod]), BitConverter.GetBytes(this.Club.SponsorPeriod));

            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.Respect]), BitConverter.GetBytes(this.Club.Respect));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.Spirit]), BitConverter.GetBytes(this.Club.Spirit));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.Will2Win]), BitConverter.GetBytes(this.Club.Will2Win));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.TeamCohesion]), BitConverter.GetBytes(this.Club.TeamCohesion));

            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.FreeTickets]), BitConverter.GetBytes(this.Club.FreeTickets));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.RoadGameSupport]), BitConverter.GetBytes(this.Club.RoadGameSupport));

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
