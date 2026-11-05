using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Reads/writes a club's identity, stadium, and finances, and can scan every club in the league.
    /// The own club is read through the club pointer; any club (CLUB) is put together from its record
    /// and the parts a cached working copy holds (ClubRecords.Image). Every field is written to the
    /// club pointer's copy (own club), every cached working copy of the club and its record.
    /// </summary>
    public class ClubController : EntityController<Club>
    {
        internal Club Club { get { return this.club; } set { this.club = value; } }
        private Club club;

        /// <summary>The club read from its record (CLUB), or null for the club pointer and the club scans.</summary>
        internal ClubSquads.Entry RecordClub { get; }
        // The record club in the working copy's layout; null when reading through the club pointer.
        private readonly byte[] image;

        /// <summary>Reads <paramref name="club"/> from its record and any working copy the game holds of it.</summary>
        internal ClubController(ProcessMemory memory, bool isGog, ClubSquads.Entry club) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.ClubAddress;
            this.UpdateBaseAddress(PlayerEnums.AddressType.CLUB);
            this.RecordClub = club;
            this.image = ClubRecords.Image(memory, FixedAddresses.For(memory), club.Index, club.Country, club.Id);
            this.Club = this.GetEntity("", PlayerEnums.AddressType.CLUB);
        }

        /// <summary>Attaches to the game process and loads the club matching <paramref name="type"/>, optionally scanning the full club list too.</summary>
        public ClubController(ProcessMemory memory, bool isGog, PlayerEnums.AddressType type, bool loadFullList = false) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.ClubAddress;
            this.UpdateBaseAddress(type);
            this.Club = this.GetEntity(type == PlayerEnums.AddressType.ALL ? Settings.AllClubInitialOffset.Key : "", type);

            // Scanning every club is only needed for the club lists (Stammverein, Wechsel) - skip
            // it for the common OWN/OPPONENT path.
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
                    club.TraineeACount = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.TRAINEE_A]);
                    club.TraineeBCount = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.TRAINEE_B]);
                    club.TraineeCCount = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.TRAINEE_C]);
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
                club.PlayerCount = (ushort) this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.PLAYER_COUNT]);
                club.AmateurPlayerCount = (byte) (club.Addresses[ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT].StartsWith("-") ? 0 : this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT]));

                club.ClubName = this.FieldText(club, club.Addresses[ClubEnums.AddressKey.NAME], 19);
                club.Id = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.ID]);
                club.Country = (PlayerEnums.Country) this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.COUNTRY]);

                Logger.Debug($"{club.ClubName}: {club.Id} ({club.Country}), {club.PlayerCount} ({club.AmateurPlayerCount}), ({club.TraineeACount}, {club.TraineeBCount}, {club.TraineeCCount})");


                if (type == PlayerEnums.AddressType.OWN || type == PlayerEnums.AddressType.CLUB)
                {
                    club.Wealth = this.FieldI32(club, club.Addresses[ClubEnums.AddressKey.Wealth]);
                    club.EarningsLeagueGames = this.FieldI32(club, club.Addresses[ClubEnums.AddressKey.EarningsLeagueGames]);
                    club.EarningsFriendlyGames = this.FieldI32(club, club.Addresses[ClubEnums.AddressKey.EarningsFriendlyGames]);
                    club.EarningsAds = this.FieldI32(club, club.Addresses[ClubEnums.AddressKey.EarningsAds]);
                    club.SponsorCash = this.FieldI32(club, club.Addresses[ClubEnums.AddressKey.SponsorCash]);
                    club.SponsorPeriod = (byte) this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.SponsorPeriod]);

                    club.Respect = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.Respect]);
                    club.Spirit = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.Spirit]);
                    club.Will2Win = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.Will2Win]);
                    club.TeamCohesion = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.TeamCohesion]);
                    club.Pressing = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.Pressing]);
                    club.OffsideTrap = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.OffsideTrap]);
                    club.AntiOffsideTrap = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.AntiOffsideTrap]);
                    club.TeamSkills = this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.TeamSkills]);
                    this.ReadStaff(club);

                    club.FreeTickets = this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.FreeTickets]);
                    club.RoadGameSupport = this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.RoadGameSupport]);

                    club.StadiumName = this.FieldText(club, club.Addresses[ClubEnums.AddressKey.StadiumName], 28);

                    club.Roof = (ClubEnums.Roof)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.ROOF]);
                    club.DisplayUnit = (ClubEnums.DisplayUnit)(this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.DisplayUnit]));
                    club.HasFloodLight = this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.HasFloodLight]) > 0;
                    club.HasGrassHeating = this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.HasGrassHeating]) > 0;
                    club.FieldCondition = ClubEnums.MapToCondition((byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.FieldCondition]));

                    club.BlockAWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockAWeeks]);
                    club.BlockBWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockBWeeks]);
                    club.BlockCWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockCWeeks]);
                    club.BlockDWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockDWeeks]);
                    club.BlockEWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockEWeeks]);
                    club.BlockFWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockFWeeks]);
                    club.BlockGWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockGWeeks]);
                    club.BlockHWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockHWeeks]);
                    club.BlockIWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockIWeeks]);
                    club.BlockJWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockJWeeks]);
                    club.BlockKWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockKWeeks]);
                    club.BlockLWeeks = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.BlockLWeeks]);

                    club.BlockAStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockAStandings]));
                    club.BlockBStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockBStandings]));
                    club.BlockCStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockCStandings]));
                    club.BlockDStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockDStandings]));
                    club.BlockEStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockEStandings]));
                    club.BlockFStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockFStandings]));
                    club.BlockGStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockGStandings]));
                    club.BlockHStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockHStandings]));
                    club.BlockIStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockIStandings]));
                    club.BlockJStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockJStandings]));
                    club.BlockKStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockKStandings]));
                    club.BlockLStandings = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockLStandings]));
                    club.BlockASeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockASeats])); // -72FF4
                    club.BlockBSeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockBSeats]));
                    club.BlockCSeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockCSeats]));
                    club.BlockDSeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockDSeats]));
                    club.BlockESeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockESeats]));
                    club.BlockFSeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockFSeats]));
                    club.BlockGSeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockGSeats]));
                    club.BlockHSeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockHSeats]));
                    club.BlockISeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockISeats]));
                    club.BlockJSeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockJSeats]));
                    club.BlockKSeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockKSeats]));
                    club.BlockLSeats = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.BlockLSeats]));
                    club.Capacity = this.FieldI32(club, club.Addresses[ClubEnums.AddressKey.Capacity]);
                    club.HasRunningTrack = this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.HasRunningTrack]) > 0;
                    club.HasUpperSeats = this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.HasUpperSeats]) > 0;
                    club.GrantPot = this.FieldI32(club, club.Addresses[ClubEnums.AddressKey.GrantPot]);
                    club.GrantRate = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.GrantRate]));

                    this.ReadEditorSettings(club);
                    this.ReadBudget(club);
                }
            }
            club.Initilisation = false;

            return club;
        }
        /// <summary>Writes the club (identity, stadium, finances) back to memory: its working copies and its record.</summary>
        public void Save()
        {
            Logger.Debug($"Saving club: {this.Club.ClubName}");
            this.ResolveWriteTargets();

            this.WriteField("0", Latin1.GetBytes(this.Club.ClubName.PadRight(19, '\0')));
            this.WriteField("81C", Latin1.GetBytes(this.Club.StadiumName.PadRight(28, '\0')));

            this.WriteField(club.Addresses[ClubEnums.AddressKey.Wealth], BitConverter.GetBytes(this.Club.Wealth));
            this.WriteField("290", BitConverter.GetBytes(this.Club.EarningsLeagueGames));
            this.WriteField("2B8", BitConverter.GetBytes(this.Club.EarningsFriendlyGames));
            this.WriteField("470", BitConverter.GetBytes(this.Club.EarningsAds));
            this.WriteField(club.Addresses[ClubEnums.AddressKey.SponsorCash], BitConverter.GetBytes(this.Club.SponsorCash));
            this.WriteField(club.Addresses[ClubEnums.AddressKey.SponsorPeriod], new byte[] { this.Club.SponsorPeriod });

            this.WriteField(club.Addresses[ClubEnums.AddressKey.Respect], BitConverter.GetBytes(this.Club.Respect));
            // Single bytes: BitConverter.GetBytes(byte) would pick the short overload and clear the next field.
            this.WriteField(club.Addresses[ClubEnums.AddressKey.Spirit], new byte[] { this.Club.Spirit });
            this.WriteField(club.Addresses[ClubEnums.AddressKey.Will2Win], BitConverter.GetBytes(this.Club.Will2Win));
            this.WriteField(club.Addresses[ClubEnums.AddressKey.TeamCohesion], new byte[] { this.Club.TeamCohesion });
            this.WriteField(club.Addresses[ClubEnums.AddressKey.Pressing], new byte[] { this.Club.Pressing });
            this.WriteField(club.Addresses[ClubEnums.AddressKey.OffsideTrap], new byte[] { this.Club.OffsideTrap });
            this.WriteField(club.Addresses[ClubEnums.AddressKey.AntiOffsideTrap], new byte[] { this.Club.AntiOffsideTrap });
            this.WriteField(club.Addresses[ClubEnums.AddressKey.TeamSkills], BitConverter.GetBytes(this.Club.TeamSkills));

            this.WriteField(club.Addresses[ClubEnums.AddressKey.FreeTickets], BitConverter.GetBytes(this.Club.FreeTickets));
            this.WriteField(club.Addresses[ClubEnums.AddressKey.RoadGameSupport], BitConverter.GetBytes(this.Club.RoadGameSupport));

            this.WriteField("890", BitConverter.GetBytes((ushort)this.Club.Roof));
            this.WriteField("896", new byte[] { (byte)this.Club.DisplayUnit });
            this.WriteField("897", new byte[] { this.Club.HasFloodLight ? (byte)1 : (byte)0 });
            this.WriteField("898", new byte[] { (byte)(this.Club.HasGrassHeating ? 1 : 0) });
            this.WriteField("89B", BitConverter.GetBytes((byte)this.Club.FieldCondition));
            this.WriteField(club.Addresses[ClubEnums.AddressKey.HasRunningTrack], new byte[] { (byte)(this.Club.HasRunningTrack ? 1 : 0) });
            this.WriteField(club.Addresses[ClubEnums.AddressKey.HasUpperSeats], new byte[] { (byte)(this.Club.HasUpperSeats ? 1 : 0) });
            this.WriteField(club.Addresses[ClubEnums.AddressKey.GrantPot], BitConverter.GetBytes(this.Club.GrantPot));
            this.WriteField(club.Addresses[ClubEnums.AddressKey.GrantRate], BitConverter.GetBytes(this.Club.GrantRate));

            this.WriteField("854", BitConverter.GetBytes(this.Club.BlockAStandings));
            this.WriteField("856", BitConverter.GetBytes(this.Club.BlockBStandings));
            this.WriteField("858", BitConverter.GetBytes(this.Club.BlockCStandings));
            this.WriteField("85A", BitConverter.GetBytes(this.Club.BlockDStandings));
            this.WriteField("85C", BitConverter.GetBytes(this.Club.BlockEStandings));
            this.WriteField("85E", BitConverter.GetBytes(this.Club.BlockFStandings));
            this.WriteField("860", BitConverter.GetBytes(this.Club.BlockGStandings));
            this.WriteField("862", BitConverter.GetBytes(this.Club.BlockHStandings));
            this.WriteField("864", BitConverter.GetBytes(this.Club.BlockIStandings));
            this.WriteField("866", BitConverter.GetBytes(this.Club.BlockJStandings));
            this.WriteField("868", BitConverter.GetBytes(this.Club.BlockKStandings));
            this.WriteField("86A", BitConverter.GetBytes(this.Club.BlockLStandings));
            this.WriteField("86C", BitConverter.GetBytes(this.Club.BlockASeats));
            this.WriteField("86E", BitConverter.GetBytes(this.Club.BlockBSeats));
            this.WriteField("870", BitConverter.GetBytes(this.Club.BlockCSeats));
            this.WriteField("872", BitConverter.GetBytes(this.Club.BlockDSeats));
            this.WriteField("874", BitConverter.GetBytes(this.Club.BlockESeats));
            this.WriteField("876", BitConverter.GetBytes(this.Club.BlockFSeats));
            this.WriteField("878", BitConverter.GetBytes(this.Club.BlockGSeats));
            this.WriteField("87A", BitConverter.GetBytes(this.Club.BlockHSeats));
            this.WriteField("87C", BitConverter.GetBytes(this.Club.BlockISeats));
            this.WriteField("87E", BitConverter.GetBytes(this.Club.BlockJSeats));
            this.WriteField("880", BitConverter.GetBytes(this.Club.BlockKSeats));
            this.WriteField("882", BitConverter.GetBytes(this.Club.BlockLSeats));

            this.WriteField("884", new byte[] { this.Club.BlockAWeeks });
            this.WriteField("885", new byte[] { this.Club.BlockBWeeks });
            this.WriteField("886", new byte[] { this.Club.BlockCWeeks });
            this.WriteField("887", new byte[] { this.Club.BlockDWeeks });
            this.WriteField("888", new byte[] { this.Club.BlockEWeeks });
            this.WriteField("889", new byte[] { this.Club.BlockFWeeks });
            this.WriteField("88A", new byte[] { this.Club.BlockGWeeks });
            this.WriteField("88B", new byte[] { this.Club.BlockHWeeks });
            this.WriteField("88C", new byte[] { this.Club.BlockIWeeks });
            this.WriteField("88D", new byte[] { this.Club.BlockJWeeks });
            this.WriteField("88E", new byte[] { this.Club.BlockKWeeks });
            this.WriteField("88F", new byte[] { this.Club.BlockLWeeks });

            // The game only recounts the capacity when a construction finishes (FUN_006076c0) or at the
            // season change - recount it here, so changed places and construction weeks count right away.
            this.Club.Capacity = this.Club.OpenCapacity();
            this.WriteField(club.Addresses[ClubEnums.AddressKey.Capacity], BitConverter.GetBytes(this.Club.Capacity));

            this.SaveEditorSettings();
            this.SaveStaff();
            this.SaveBudget();

            // this.WriteField("854", new byte[] { (byte)player.SkinColor });
        }

        #region Coaching staff
        // Address of a co-trainer's (trainer pool) or goalkeeper trainer's entry; null without the table or a valid ID.
        private uint? StaffEntry(bool goalkeeper, ushort id)
        {
            FixedAddresses fixedAddresses = this.Fixed;
            uint? table = goalkeeper ? fixedAddresses?.GoalkeeperCoachTable : fixedAddresses?.CoachPoolTable;
            int capacity = goalkeeper ? Settings.GoalkeeperCoachCapacity : Settings.CoachPoolCapacity;
            if (!table.HasValue || id == 0 || id >= capacity)
                return null;
            return this.memory.ModuleBase + table.Value + id * Settings.StaffEntryStride;
        }


        // The game shows the name as "<+0> <+2>" from the name pool (FUN_00583a40); "" if unreadable.
        private static string StaffNamePart(NamePoolResolver names, byte[] entry, int offset) =>
            names.ReadName(BitConverter.ToUInt16(entry, offset), Latin1) ?? "";

        private void ReadStaff(Club club)
        {
            NamePoolResolver names = new NamePoolResolver(this.memory);
            club.CoTrainerId = this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.CoTrainer]);
            club.GoalkeeperCoachId = this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.GoalkeeperCoach]);

            uint? co = this.StaffEntry(false, club.CoTrainerId);
            byte[] entry = co.HasValue ? this.memory.ReadBytesAtAddress(co.Value, (int)Settings.StaffEntryStride) : null;
            club.CoTrainerFirstname = entry == null ? StaffNone(club.CoTrainerId) : StaffNamePart(names, entry, 0);
            club.CoTrainerLastname = entry == null ? "" : StaffNamePart(names, entry, 2);
            club.CoTrainerCompetence = entry?[Settings.StaffCompetenceOffset] ?? 0;
            club.CoTrainerContract = entry?[Settings.StaffContractOffset] ?? 0;
            club.CoTrainerSalary = entry == null ? 0 : BitConverter.ToInt32(entry, (int)Settings.StaffSalaryOffset);

            uint? goalkeeper = this.StaffEntry(true, club.GoalkeeperCoachId);
            entry = goalkeeper.HasValue ? this.memory.ReadBytesAtAddress(goalkeeper.Value, (int)Settings.StaffEntryStride) : null;
            club.GoalkeeperCoachFirstname = entry == null ? StaffNone(club.GoalkeeperCoachId) : StaffNamePart(names, entry, 0);
            club.GoalkeeperCoachLastname = entry == null ? "" : StaffNamePart(names, entry, 2);
            club.GoalkeeperCoachCompetence = entry?[Settings.StaffCompetenceOffset] ?? 0;
            club.GoalkeeperCoachContract = entry?[Settings.StaffContractOffset] ?? 0;
            club.GoalkeeperCoachSalary = entry == null ? 0 : BitConverter.ToInt32(entry, (int)Settings.StaffSalaryOffset);
        }

        private static string StaffNone(ushort id) => id == 0 ? "keiner" : $"Nr. {id} (nicht lesbar)";

        // Only the person's entry changes; who the club employs stays as the game set it.
        private void SaveStaff()
        {
            NamePoolResolver names = new NamePoolResolver(this.memory);
            uint? co = this.StaffEntry(false, this.Club.CoTrainerId);
            if (co.HasValue)
            {
                this.WriteStaff(co.Value, this.Club.CoTrainerCompetence, this.Club.CoTrainerContract, this.Club.CoTrainerSalary);
                this.WriteStaffName(names, co.Value, 0, this.Club.CoTrainerFirstname);
                this.WriteStaffName(names, co.Value, 2, this.Club.CoTrainerLastname);
            }
            uint? goalkeeper = this.StaffEntry(true, this.Club.GoalkeeperCoachId);
            if (goalkeeper.HasValue)
            {
                this.WriteStaff(goalkeeper.Value, this.Club.GoalkeeperCoachCompetence, this.Club.GoalkeeperCoachContract, this.Club.GoalkeeperCoachSalary);
                this.WriteStaffName(names, goalkeeper.Value, 0, this.Club.GoalkeeperCoachFirstname);
                this.WriteStaffName(names, goalkeeper.Value, 2, this.Club.GoalkeeperCoachLastname);
            }
        }

        private void WriteStaff(uint entry, byte competence, byte contract, int salary)
        {
            this.memory.WriteBytesAtAddress(entry + Settings.StaffCompetenceOffset, new byte[] { Math.Min(competence, Settings.MaxStaffCompetence) });
            this.memory.WriteBytesAtAddress(entry + Settings.StaffContractOffset, new byte[] { contract });
            this.memory.WriteBytesAtAddress(entry + Settings.StaffSalaryOffset, BitConverter.GetBytes(salary));
        }

        // A new name is a pool entry like a player's (shared entries are never overwritten): an existing
        // one with that text, else appended like the game does; the person's entry is pointed at it.
        // Empty stays as it is (also a name that couldn't be read and is shown empty).
        private void WriteStaffName(NamePoolResolver names, uint entry, uint offset, string value)
        {
            string name = NamePoolResolver.NormalizeLikeGame(value);
            if (name.Length > Club.StaffNameLength)
                name = name.Substring(0, Club.StaffNameLength).TrimEnd(' ');
            byte[] current = this.memory.ReadBytesAtAddress(entry, (int)Settings.StaffEntryStride);
            if (name.Length == 0 || current == null || name == StaffNamePart(names, current, (int)offset))
                return;
            ushort? index = names.FindIndexOf(name, Latin1) ?? names.AppendName(name, Latin1);
            if (!index.HasValue)
            {
                Logger.Warn($"Trainerstab: \"{name}\" konnte nicht in den Namensbestand aufgenommen werden - Name bleibt.");
                return;
            }
            this.memory.WriteBytesAtAddress(entry + offset, BitConverter.GetBytes(index.Value));
        }
        #endregion

        #region Editor settings
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        /// <summary>Clubs of the own club's country with their map positions (for rival, fan friendship and the map); empty if unknown.</summary>
        internal List<ClubRecords.Entry> CountryClubs { get; private set; } = new List<ClubRecords.Entry>();

        private FixedAddresses Fixed => FixedAddresses.For(this.memory);

        // The settings of the game's data editor: read from the working copy the club is shown
        // through, short name, chant and fan name from the club record (they only exist there).
        private void ReadEditorSettings(Club club)
        {
            byte[] jersey = this.FieldBytes(club, club.Addresses[ClubEnums.AddressKey.Jersey], Club.JerseyLength);
            if (jersey != null)
                club.Jersey = jersey;
            club.FanTurnout = (ClubEnums.FanTurnout)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.FanTurnout]);
            club.FanType = (ClubEnums.FanType)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.FanType]);
            club.Article = (ClubEnums.Article)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.Article]);
            club.FanFriendshipId = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.FanFriendship]);
            club.ArchRivalId = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.ArchRival]);
            club.Board = (ClubEnums.Board)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.Board]);
            club.Association = (byte)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.Association]);
            club.Preposition = (ClubEnums.Preposition)this.FieldByte(club, club.Addresses[ClubEnums.AddressKey.Preposition]);
            club.Town = this.FieldText(club, club.Addresses[ClubEnums.AddressKey.Town], Club.TownLength + 1);
            club.MapX = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.MapX]));
            club.MapY = unchecked((short)this.FieldU16(club, club.Addresses[ClubEnums.AddressKey.MapY]));

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

        private static string Hex(uint offset) => offset.ToString("X");


        // The "Kalkulation", the slush fund and the active loans and investments - all in the finance
        // block, which the record keeps 0x50 further on (see ClubRecords.Segments).
        private void ReadBudget(Club club)
        {
            club.SeasonTicketDiscount = (byte)this.FieldByte(club, Hex(Settings.ClubSeasonTicketDiscountOffset));
            club.SeasonTicketQuota = (byte)this.FieldByte(club, Hex(Settings.ClubSeasonTicketQuotaOffset));
            club.StandingPrice = unchecked((short)this.FieldU16(club, Hex(Settings.ClubStandingPriceOffset)));
            club.SeatPrice = unchecked((short)this.FieldU16(club, Hex(Settings.ClubSeatPriceOffset)));
            club.Security = unchecked((short)this.FieldU16(club, Hex(Settings.ClubSecurityOffset)));
            club.LawFirm = (byte)this.FieldByte(club, Hex(Settings.ClubLawFirmOffset));
            club.StatisticsDepartment = (byte)this.FieldByte(club, Hex(Settings.ClubStatisticsOffset));
            club.HasComputerDepartment = this.FieldByte(club, Hex(Settings.ClubComputerOffset)) != 0;
            club.SlushFund = this.FieldI32(club, Hex(Settings.ClubSlushFundOffset));
            club.SlushFundRate = unchecked((short)this.FieldU16(club, Hex(Settings.ClubSlushFundRateOffset)));

            club.Loans.Clear();
            this.loadedLoanSlots.Clear();
            byte[] loans = this.FieldBytes(club, Hex(Settings.ClubLoansOffset), Settings.ClubLoanSlots * (int)Settings.ClubLoanStride);
            for (int slot = 0; loans != null && slot < Settings.ClubLoanSlots; slot++)
            {
                int at = slot * (int)Settings.ClubLoanStride;
                if ((loans[at + 0xB] & 1) == 0)
                    continue;
                ClubLoan loan = new ClubLoan
                {
                    Slot = slot,
                    Amount = BitConverter.ToInt32(loans, at),
                    Term = loans[at + 0xA],
                    Cancellable = (loans[at + 0xB] & 2) != 0
                };
                for (int term = 0; term < 3; term++)
                    loan.Rates[term] = BitConverter.ToInt16(loans, at + 4 + term * 2);
                club.Loans.Add(loan);
                this.loadedLoanSlots.Add(slot);
            }

            club.LegalCosts = this.FieldI32(club, Hex(Settings.ClubLegalCostsOffset));
            club.OverdraftLimit = unchecked((short)this.FieldU16(club, Hex(Settings.ClubOverdraftLimitOffset)));
            club.PublicImage = (byte)this.FieldByte(club, Hex(Settings.ClubPublicImageOffset));
            club.Licence = (byte)this.FieldByte(club, Hex(Settings.ClubLicenceOffset));
            club.Requirements = (byte)this.FieldByte(club, Hex(Settings.ClubRequirementsOffset));
            club.DebtLimit = (byte)this.FieldByte(club, Hex(Settings.ClubDebtLimitOffset));
            club.SpectatorLimit = (byte)this.FieldByte(club, Hex(Settings.ClubSpectatorLimitOffset));
            club.SalaryLimit = this.FieldU16(club, Hex(Settings.ClubSalaryLimitOffset));
            club.TransferLimit = unchecked((short)this.FieldU16(club, Hex(Settings.ClubTransferLimitOffset)));
            club.Bribery = (byte)this.FieldByte(club, Hex(Settings.ClubBriberyOffset));
            byte[] camp = this.FieldBytes(club, Hex(Settings.ClubShortCampOffset), 4);
            club.ShortCamp = camp != null ? BitConverter.ToUInt32(camp, 0) : 0;
            club.SponsorRequests = (byte)this.FieldByte(club, Hex(Settings.ClubSponsorRequestsOffset));
            club.ScoutSpending = this.FieldI32(club, Hex(Settings.ClubScoutSpendingOffset));

            club.Investments.Clear();
            FixedAddresses fixedAddresses = this.Fixed;
            club.InvestmentIndices = Enumerable.Range(0, ClubInvestment.KindNames.Length).Select(kind => GameRates.InvestmentIndex(this.memory, fixedAddresses, kind)).ToArray();
            this.loadedInvestmentSlots.Clear();
            byte[] investments = this.FieldBytes(club, Hex(Settings.ClubInvestmentsOffset), Settings.ClubInvestmentSlots * (int)Settings.ClubInvestmentStride);
            for (int slot = 0; investments != null && slot < Settings.ClubInvestmentSlots; slot++)
            {
                int at = slot * (int)Settings.ClubInvestmentStride;
                if ((investments[at + 0xB] & 1) == 0)
                    continue;
                ClubInvestment investment = ClubInvestment.New(investments[at + 0xA], BitConverter.ToInt32(investments, at), club.InvestmentIndices);
                investment.Slot = slot;
                investment.IndexAtPurchase = BitConverter.ToInt16(investments, at + 8);
                investment.Base = BitConverter.ToInt32(investments, at + 4);
                club.Investments.Add(investment);
                this.loadedInvestmentSlots.Add(slot);
            }

            // The share price lives in the club record only (FUN_0057d4b0).
            uint? record = this.RecordOf(club);
            club.IsListed = ClubRecords.IsListed(this.memory, fixedAddresses, club.Country, club.Id) == true;
            byte[] stock = record.HasValue ? this.memory.ReadBytesAtAddress(record.Value + Settings.ClubRecordStockOffset, 6) : null;
            club.IssuedShares = stock != null ? BitConverter.ToInt32(stock, 0) : 0;
            club.SharePrice = stock != null ? BitConverter.ToInt16(stock, 4) : (short)0;
        }

        // Loan slots active when read: one the user repaid gets its active bit cleared.
        private readonly HashSet<int> loadedLoanSlots = new HashSet<int>();

        // The first slot of count that's neither kept nor active when read, or -1.
        private static int FreeSlot(int count, HashSet<int> used, HashSet<int> loaded) =>
            Enumerable.Range(0, count).Where(s => !used.Contains(s) && !loaded.Contains(s)).DefaultIfEmpty(-1).First();

        // Loans as the game keeps them (FUN_00550c80): kept ones rewritten from amount to flags, new
        // ones in the first free slot with the billing day of the quarter (FUN_005a4760), no days
        // accrued yet and the maturity years * 1000 + season day; repaid ones inactive. The game
        // counts loans as available money, so a new or bigger loan is cash right away.
        private void SaveLoans()
        {
            HashSet<int> used = new HashSet<int>(this.Club.Loans.Where(l => l.Slot >= 0).Select(l => l.Slot));
            foreach (int slot in this.loadedLoanSlots.Where(s => !used.Contains(s)).ToList())
            {
                this.WriteField(Hex(Settings.ClubLoansOffset + (uint)slot * Settings.ClubLoanStride + 0xB), new byte[] { 0 });
                this.loadedLoanSlots.Remove(slot);
            }
            foreach (ClubLoan loan in this.Club.Loans)
            {
                bool isNew = loan.Slot < 0;
                byte[] slot = new byte[isNew ? (int)Settings.ClubLoanStride : 0xC];
                Array.Copy(BitConverter.GetBytes(loan.Amount), 0, slot, 0, 4);
                for (int term = 0; term < 3; term++)
                    Array.Copy(BitConverter.GetBytes(loan.Rates[term]), 0, slot, 4 + term * 2, 2);
                slot[0xA] = loan.Term;
                slot[0xB] = (byte)(loan.Cancellable ? 3 : 1);
                if (isNew)
                {
                    int free = FreeSlot(Settings.ClubLoanSlots, used, this.loadedLoanSlots);
                    if (free < 0)
                        continue;
                    loan.Slot = free;
                    used.Add(free);
                    this.loadedLoanSlots.Add(free);
                    short today = (short)this.GameShort(Settings.GlobalCounterOffset);
                    Array.Copy(BitConverter.GetBytes(this.DayOfQuarter()), 0, slot, 0xE, 2);
                    Array.Copy(BitConverter.GetBytes((short)(ClubLoan.TermYears[loan.Term] * 1000 + today)), 0, slot, 0x10, 2);
                }
                this.WriteField(Hex(Settings.ClubLoansOffset + (uint)loan.Slot * Settings.ClubLoanStride), slot);
            }
        }

        // An Int16 of the game's state block (calendar, rates), 0 if unreadable.
        private int GameShort(uint offset)
        {
            byte[] bytes = this.Fixed == null ? null : this.memory.ReadBytesAtAddress(this.Fixed.Runtime(this.memory, offset), 2);
            return bytes == null ? 0 : BitConverter.ToInt16(bytes, 0);
        }

        // Day within the quarter as the game counts it (FUN_005a4760): day of month (max. 30) - 30 + (month % 3) * 30.
        private short DayOfQuarter() =>
            (short)(Math.Min(this.GameShort(Settings.DayOfMonthOffset), 30) - 30 + (this.GameShort(Settings.MonthOffset) % 3) * 30);

        // Investment slots active when read: one the user deleted gets its active bit cleared.
        private readonly HashSet<int> loadedInvestmentSlots = new HashSet<int>();

        private uint? RecordOf(Club club) => this.RecordClub != null && this.Fixed != null
            ? this.Fixed.Runtime(this.memory, Settings.ClubRecordTableOffset) + (uint)this.RecordClub.Index * Settings.ClubRecordStride
            : ClubRecords.RecordAddress(this.memory, this.Fixed, club.Country, club.Id);

        // Investments as the game keeps them: kept slots rewritten in full, new ones in the first free
        // slot like the game's own (purchase day 0, as after a season change), deleted ones inactive.
        private void SaveInvestments()
        {
            HashSet<int> used = new HashSet<int>(this.Club.Investments.Where(i => i.Slot >= 0).Select(i => i.Slot));
            foreach (int slot in this.loadedInvestmentSlots.Where(s => !used.Contains(s)).ToList())
            {
                this.WriteField(Hex(Settings.ClubInvestmentsOffset + (uint)slot * Settings.ClubInvestmentStride + 0xB), new byte[] { 0 });
                this.loadedInvestmentSlots.Remove(slot);
            }
            foreach (ClubInvestment investment in this.Club.Investments)
            {
                bool isNew = investment.Slot < 0;
                if (isNew)
                {
                    int free = FreeSlot(Settings.ClubInvestmentSlots, used, this.loadedInvestmentSlots);
                    if (free < 0)
                        continue;
                    investment.Slot = free;
                    used.Add(free);
                    this.loadedInvestmentSlots.Add(free);
                }
                byte[] slot = new byte[isNew ? 0x10 : 0xC];
                Array.Copy(BitConverter.GetBytes(investment.Principal), 0, slot, 0, 4);
                Array.Copy(BitConverter.GetBytes(investment.Base), 0, slot, 4, 4);
                Array.Copy(BitConverter.GetBytes(investment.IndexAtPurchase), 0, slot, 8, 2);
                slot[0xA] = investment.Kind;
                slot[0xB] = 1;
                this.WriteField(Hex(Settings.ClubInvestmentsOffset + (uint)investment.Slot * Settings.ClubInvestmentStride), slot);
            }
        }

        // Law firm and departments go to the planned and the current byte alike, so the next
        // Kalkulation starts from them too.
        private void SaveBudget()
        {
            this.WriteField(Hex(Settings.ClubSeasonTicketDiscountOffset), new[] { this.Club.SeasonTicketDiscount, this.Club.SeasonTicketQuota });
            this.WriteField(Hex(Settings.ClubStandingPriceOffset), BitConverter.GetBytes(this.Club.StandingPrice));
            this.WriteField(Hex(Settings.ClubSeatPriceOffset), BitConverter.GetBytes(this.Club.SeatPrice));
            this.WriteField(Hex(Settings.ClubSecurityOffset), BitConverter.GetBytes(this.Club.Security));
            byte computer = (byte)(this.Club.HasComputerDepartment ? 1 : 0);
            this.WriteField(Hex(Settings.ClubPlannedLawFirmOffset), new[] { this.Club.LawFirm, this.Club.StatisticsDepartment, computer, this.Club.LawFirm, this.Club.StatisticsDepartment, computer });
            this.WriteField(Hex(Settings.ClubSlushFundOffset), BitConverter.GetBytes(this.Club.SlushFund));
            this.WriteField(Hex(Settings.ClubSlushFundRateOffset), BitConverter.GetBytes(this.Club.SlushFundRate));
            this.SaveLoans();
            this.SaveInvestments();
            this.WriteField(Hex(Settings.ClubLegalCostsOffset), BitConverter.GetBytes(this.Club.LegalCosts));
            this.WriteField(Hex(Settings.ClubOverdraftLimitOffset), BitConverter.GetBytes(this.Club.OverdraftLimit));
            this.WriteField(Hex(Settings.ClubPublicImageOffset), new[] { this.Club.PublicImage });
            this.SaveRequirements();

            // Price and the ten weekly prices of the chart, so the chart doesn't jump.
            if (this.Club.IsListed && this.record.HasValue)
            {
                this.memory.WriteBytesAtAddress(this.record.Value + Settings.ClubRecordStockOffset + 4, BitConverter.GetBytes(this.Club.SharePrice));
                byte[] history = new byte[20];
                for (int week = 0; week < 10; week++)
                    Array.Copy(BitConverter.GetBytes(this.Club.SharePrice), 0, history, week * 2, 2);
                this.memory.WriteBytesAtAddress(this.record.Value + Settings.ClubRecordStockOffset + 0x18, history);
            }
        }

        // Licence and requirements. A lifted requirement takes its limit along where the game checks
        // the limit alone (salaries: FUN_005b5b40 counts +0x496 whenever it isn't 0).
        private void SaveRequirements()
        {
            if (!this.Club.HasRequirement(ClubEnums.Requirement.SalaryLimit))
                this.Club.SalaryLimit = 0;
            this.WriteField(Hex(Settings.ClubLicenceOffset), new[] { this.Club.Licence, this.Club.Requirements });
            this.WriteField(Hex(Settings.ClubDebtLimitOffset), new[] { this.Club.DebtLimit, this.Club.SpectatorLimit });
            this.WriteField(Hex(Settings.ClubSalaryLimitOffset), BitConverter.GetBytes(this.Club.SalaryLimit));
            this.WriteField(Hex(Settings.ClubTransferLimitOffset), BitConverter.GetBytes(this.Club.TransferLimit));
            this.WriteField(Hex(Settings.ClubBriberyOffset), new[] { this.Club.Bribery });
            this.WriteField(Hex(Settings.ClubShortCampOffset), BitConverter.GetBytes(this.Club.ShortCamp));
            this.WriteField(Hex(Settings.ClubSponsorRequestsOffset), new[] { this.Club.SponsorRequests });
            this.WriteField(Hex(Settings.ClubScoutSpendingOffset), BitConverter.GetBytes(this.Club.ScoutSpending));
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
                case ClubEnums.AddressKey.Association: return new[] { this.Club.Association };
                case ClubEnums.AddressKey.Preposition: return new[] { (byte)this.Club.Preposition };
                case ClubEnums.AddressKey.Town: return Latin1.GetBytes(this.Club.Town.PadRight(Club.TownLength + 1, '\0'));
                case ClubEnums.AddressKey.MapX: return BitConverter.GetBytes(this.Club.MapX);
                case ClubEnums.AddressKey.MapY: return BitConverter.GetBytes(this.Club.MapY);
                default: throw new ArgumentOutOfRangeException(nameof(key), key, null);
            }
        }

        // The editor settings go everywhere like every other field (see WriteField); short name,
        // chant and fan name only exist in the record.
        private void SaveEditorSettings()
        {
            foreach (ClubEnums.AddressKey key in Settings.ClubRecordMirrorKeys)
                this.WriteField(this.Club.Addresses[key], this.EditorSettingBytes(key));

            if (this.record.HasValue && this.Club.HasRecord)
            {
                this.memory.WriteBytesAtAddress(this.record.Value + Settings.ClubRecordShortNameOffset, Latin1.GetBytes(this.Club.ShortName.PadRight(Club.ShortNameLength, '-') + "\0"));
                this.memory.WriteBytesAtAddress(this.record.Value + Settings.ClubRecordChantOffset, Latin1.GetBytes(this.Club.Chant.PadRight(Club.FanTextLength + 1, '\0')));
                this.memory.WriteBytesAtAddress(this.record.Value + Settings.ClubRecordFanNameOffset, Latin1.GetBytes(this.Club.FanName.PadRight(Club.FanTextLength + 1, '\0')));
            }
            Logger.Debug($"Saved {this.Club.Country}/{this.Club.Id} ({this.copies.Count} cached copies, record {(this.record.HasValue ? "found" : "unknown")}).");
        }
        #endregion

        #region Field access
        // Where Save writes to, resolved once per save: the club's cached working copies and its record.
        private List<uint> copies = new List<uint>();
        private uint? record;

        private void ResolveWriteTargets()
        {
            FixedAddresses fixedAddresses = this.Fixed;
            this.copies = ClubRecords.WorkingCopies(this.memory, fixedAddresses, this.Club.Country, this.Club.Id);
            this.record = this.RecordClub != null && fixedAddresses != null
                ? fixedAddresses.Runtime(this.memory, Settings.ClubRecordTableOffset) + (uint)this.RecordClub.Index * Settings.ClubRecordStride
                : ClubRecords.RecordAddress(this.memory, fixedAddresses, this.Club.Country, this.Club.Id);
        }

        // A field at its working-copy offset goes to the copy behind the club pointer (own club),
        // every cached working copy of the club and the record - the game puts a working copy back
        // over the record and fetches a new one from it.
        private void WriteField(string offset, byte[] data)
        {
            if (this.RecordClub == null)
                this.memory.WriteBytes(GetAddress(this.memory, this.Club, offset), data);
            uint at = Convert.ToUInt32(offset, 16);
            foreach (uint copy in this.copies)
                this.memory.WriteBytesAtAddress(copy + at, data);
            uint? field = this.record.HasValue ? ClubRecords.RecordField(this.record.Value, at, data.Length) : null;
            if (field.HasValue)
                this.memory.WriteBytesAtAddress(field.Value, data);
        }

        // A field at its working-copy offset: from the record club's image, or through the club pointer.
        private byte[] FieldBytes(Club club, string offset, int length)
        {
            if (this.image == null)
                return this.memory.ReadBytes(GetAddress(this.memory, club, offset), length);
            uint at = Convert.ToUInt32(offset, 16);
            byte[] bytes = new byte[length];
            if (at + length <= this.image.Length)
                Array.Copy(this.image, at, bytes, 0, length);
            return bytes;
        }

        private int FieldByte(Club club, string offset) => this.FieldBytes(club, offset, 1)?[0] ?? 0;

        private ushort FieldU16(Club club, string offset)
        {
            byte[] bytes = this.FieldBytes(club, offset, 2);
            return bytes != null ? BitConverter.ToUInt16(bytes, 0) : (ushort)0;
        }

        private int FieldI32(Club club, string offset)
        {
            byte[] bytes = this.FieldBytes(club, offset, 4);
            return bytes != null ? BitConverter.ToInt32(bytes, 0) : 0;
        }

        private string FieldText(Club club, string offset, int length)
        {
            byte[] bytes = this.FieldBytes(club, offset, length);
            return bytes != null ? Latin1.GetString(bytes).Split('\0')[0] : "";
        }
        #endregion
    }
}
