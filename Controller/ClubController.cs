using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
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

                    club.BlockAStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockAStandings])));
                    club.BlockBStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockBStandings])));
                    club.BlockCStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockCStandings])));
                    club.BlockDStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockDStandings])));
                    club.BlockEStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockEStandings])));
                    club.BlockFStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockFStandings])));
                    club.BlockGStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockGStandings])));
                    club.BlockHStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockHStandings])));
                    club.BlockIStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockIStandings])));
                    club.BlockJStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockJStandings])));
                    club.BlockKStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockKStandings])));
                    club.BlockLStandings = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockLStandings])));
                    club.BlockASeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockASeats]))); // -72FF4
                    club.BlockBSeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockBSeats])));
                    club.BlockCSeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockCSeats])));
                    club.BlockDSeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockDSeats])));
                    club.BlockESeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockESeats])));
                    club.BlockFSeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockFSeats])));
                    club.BlockGSeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockGSeats])));
                    club.BlockHSeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockHSeats])));
                    club.BlockISeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockISeats])));
                    club.BlockJSeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockJSeats])));
                    club.BlockKSeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockKSeats])));
                    club.BlockLSeats = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.BlockLSeats])));
                    club.Capacity = this.memory.ReadInt32(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.Capacity]));
                    club.HasRunningTrack = this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.HasRunningTrack])) > 0;
                    club.HasUpperSeats = this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.HasUpperSeats])) > 0;
                    club.GrantPot = this.memory.ReadInt32(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.GrantPot]));
                    club.GrantRate = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.GrantRate])));

                    this.ReadEditorSettings(club);
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
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.HasRunningTrack]), new byte[] { (byte)(this.Club.HasRunningTrack ? 1 : 0) });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.HasUpperSeats]), new byte[] { (byte)(this.Club.HasUpperSeats ? 1 : 0) });
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.GrantPot]), BitConverter.GetBytes(this.Club.GrantPot));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.GrantRate]), BitConverter.GetBytes(this.Club.GrantRate));

            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "854"), BitConverter.GetBytes(this.Club.BlockAStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "856"), BitConverter.GetBytes(this.Club.BlockBStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "858"), BitConverter.GetBytes(this.Club.BlockCStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "85A"), BitConverter.GetBytes(this.Club.BlockDStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "85C"), BitConverter.GetBytes(this.Club.BlockEStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "85E"), BitConverter.GetBytes(this.Club.BlockFStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "860"), BitConverter.GetBytes(this.Club.BlockGStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "862"), BitConverter.GetBytes(this.Club.BlockHStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "864"), BitConverter.GetBytes(this.Club.BlockIStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "866"), BitConverter.GetBytes(this.Club.BlockJStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "868"), BitConverter.GetBytes(this.Club.BlockKStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "86A"), BitConverter.GetBytes(this.Club.BlockLStandings));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "86C"), BitConverter.GetBytes(this.Club.BlockASeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "86E"), BitConverter.GetBytes(this.Club.BlockBSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "870"), BitConverter.GetBytes(this.Club.BlockCSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "872"), BitConverter.GetBytes(this.Club.BlockDSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "874"), BitConverter.GetBytes(this.Club.BlockESeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "876"), BitConverter.GetBytes(this.Club.BlockFSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "878"), BitConverter.GetBytes(this.Club.BlockGSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "87A"), BitConverter.GetBytes(this.Club.BlockHSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "87C"), BitConverter.GetBytes(this.Club.BlockISeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "87E"), BitConverter.GetBytes(this.Club.BlockJSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "880"), BitConverter.GetBytes(this.Club.BlockKSeats));
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, "882"), BitConverter.GetBytes(this.Club.BlockLSeats));

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

            // The game only recounts the capacity when a construction finishes (FUN_006076c0) or at the
            // season change - recount it here, so changed places and construction weeks count right away.
            this.Club.Capacity = this.Club.OpenCapacity();
            this.memory.WriteBytes(GetAddress(this.memory, this.Club, club.Addresses[ClubEnums.AddressKey.Capacity]), BitConverter.GetBytes(this.Club.Capacity));

            this.SaveEditorSettings();

            // this.memory.WriteBytes(GetAddress(this.memory, this.Club, "854"), new byte[] { (byte)player.SkinColor });
        }

        #region Editor settings
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        /// <summary>Clubs of the own club's country with their map positions (for rival, fan friendship and the map); empty if unknown.</summary>
        internal List<ClubRecords.Entry> CountryClubs { get; private set; } = new List<ClubRecords.Entry>();

        private FixedAddresses Fixed => FixedAddresses.For(this.memory);

        // The settings of the game's data editor: read from the working copy the club is shown
        // through, short name, chant and fan name from the club record (they only exist there).
        private void ReadEditorSettings(Club club)
        {
            byte[] jersey = this.memory.ReadBytes(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.Jersey]), Club.JerseyLength);
            if (jersey != null)
                club.Jersey = jersey;
            club.FanTurnout = (ClubEnums.FanTurnout)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.FanTurnout]));
            club.FanType = (ClubEnums.FanType)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.FanType]));
            club.Article = (ClubEnums.Article)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.Article]));
            club.FanFriendshipId = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.FanFriendship]));
            club.ArchRivalId = (byte)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.ArchRival]));
            club.Board = (ClubEnums.Board)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.Board]));
            club.Preposition = (ClubEnums.Preposition)this.memory.ReadByte(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.Preposition]));
            club.Town = this.memory.ReadString(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.Town]), Club.TownLength + 1, Latin1);
            club.MapX = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.MapX])));
            club.MapY = unchecked((short)this.memory.ReadUInt16(GetAddress(this.memory, club, club.Addresses[ClubEnums.AddressKey.MapY])));

            FixedAddresses fixedAddresses = this.Fixed;
            uint? record = ClubRecords.RecordAddress(this.memory, fixedAddresses, club.Country, club.Id);
            club.HasRecord = record.HasValue;
            if (record.HasValue)
            {
                club.ShortName = this.memory.ReadStringAtAddress(record.Value + Settings.ClubRecordShortNameOffset, Club.ShortNameLength + 1, Latin1);
                club.Chant = this.memory.ReadStringAtAddress(record.Value + Settings.ClubRecordChantOffset, Club.FanTextLength + 1, Latin1);
                club.FanName = this.memory.ReadStringAtAddress(record.Value + Settings.ClubRecordFanNameOffset, Club.FanTextLength + 1, Latin1);
            }
            this.CountryClubs = ClubRecords.CountryClubs(this.memory, fixedAddresses, club.Country);
        }

        // The bytes of a mirrored editor setting as the game stores them.
        private byte[] EditorSettingBytes(ClubEnums.AddressKey key)
        {
            switch (key)
            {
                case ClubEnums.AddressKey.Jersey: return this.Club.Jersey;
                case ClubEnums.AddressKey.FanTurnout: return new[] { (byte)this.Club.FanTurnout };
                case ClubEnums.AddressKey.FanType: return new[] { (byte)this.Club.FanType };
                case ClubEnums.AddressKey.Article: return new[] { (byte)this.Club.Article };
                case ClubEnums.AddressKey.FanFriendship: return new[] { this.Club.FanFriendshipId };
                case ClubEnums.AddressKey.ArchRival: return new[] { this.Club.ArchRivalId };
                case ClubEnums.AddressKey.Board: return new[] { (byte)this.Club.Board };
                case ClubEnums.AddressKey.Preposition: return new[] { (byte)this.Club.Preposition };
                case ClubEnums.AddressKey.Town: return Latin1.GetBytes(this.Club.Town.PadRight(Club.TownLength + 1, '\0'));
                case ClubEnums.AddressKey.MapX: return BitConverter.GetBytes(this.Club.MapX);
                case ClubEnums.AddressKey.MapY: return BitConverter.GetBytes(this.Club.MapY);
                default: throw new ArgumentOutOfRangeException(nameof(key), key, null);
            }
        }

        // Writes the editor settings to the working copy shown, every other cached working copy of
        // the club and its record - the game puts a working copy back over the record.
        private void SaveEditorSettings()
        {
            FixedAddresses fixedAddresses = this.Fixed;
            List<uint> copies = ClubRecords.WorkingCopies(this.memory, fixedAddresses, this.Club.Country, this.Club.Id);
            uint? record = ClubRecords.RecordAddress(this.memory, fixedAddresses, this.Club.Country, this.Club.Id);

            foreach (ClubEnums.AddressKey key in Settings.ClubRecordMirrorKeys)
            {
                byte[] data = this.EditorSettingBytes(key);
                string offset = this.Club.Addresses[key];
                this.memory.WriteBytes(GetAddress(this.memory, this.Club, offset), data);
                foreach (uint copy in copies)
                    this.memory.WriteBytesAtAddress(copy + Convert.ToUInt32(offset, 16), data);
                if (record.HasValue)
                    this.memory.WriteBytesAtAddress(record.Value + Settings.ClubRecordMirrorOffset(key), data);
            }

            if (record.HasValue && this.Club.HasRecord)
            {
                this.memory.WriteBytesAtAddress(record.Value + Settings.ClubRecordShortNameOffset, Latin1.GetBytes(this.Club.ShortName.PadRight(Club.ShortNameLength, '-') + "\0"));
                this.memory.WriteBytesAtAddress(record.Value + Settings.ClubRecordChantOffset, Latin1.GetBytes(this.Club.Chant.PadRight(Club.FanTextLength + 1, '\0')));
                this.memory.WriteBytesAtAddress(record.Value + Settings.ClubRecordFanNameOffset, Latin1.GetBytes(this.Club.FanName.PadRight(Club.FanTextLength + 1, '\0')));
            }
            Logger.Debug($"Saved editor settings of {this.Club.Country}/{this.Club.Id} ({copies.Count} cached copies, record {(record.HasValue ? "found" : "unknown")}).");
        }
        #endregion
    }
}
