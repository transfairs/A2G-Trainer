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
    /// Reads/writes a club's player roster. Combines the transient display-cache fields (the
    /// addresses the rest of the project historically used) with the smaller set of fields
    /// confirmed to live in the permanent, PlayerId-keyed record (see ReadPersistentFields/
    /// WritePersistentXxx and PlayerRecordResolver) - the latter are authoritative where mapped.
    /// The squad or youth of any club (CLUB, TRAINEE) is listed from the records alone, by the
    /// player IDs ClubSquads finds; a player the game holds a working copy of gets that written too.
    /// </summary>
    public class PlayerController : EntityController<Player>
    {
        /// <summary>Roster-start offset of the opponent club's players, resolved once loading the user's own club.</summary>
        public String OpponentOffset { get => this.opponentOffset; set => opponentOffset = value; }
        private String opponentOffset;

        /// <summary>The club whose squad or youth is listed from the records, or null for the own/opponent roster.</summary>
        internal ClubSquads.Entry RecordClub { get; }

        /// <summary>Attaches to the game process and loads the full player roster for the given club.</summary>
        public PlayerController(ProcessMemory memory, Club club, bool isGog, PlayerEnums.AddressType type) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.PlayerAddress;
            this.UpdateBaseAddress(type);
            this.RefreshPlayerList(club);
        }

        /// <summary>Lists the squad and amateurs (CLUB) or the youth players (TRAINEE) of <paramref name="club"/> from the player records; nothing without a club.</summary>
        internal PlayerController(ProcessMemory memory, ClubSquads.Entry club, bool isGog, PlayerEnums.AddressType type) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.PlayerAddress;
            this.UpdateBaseAddress(type);
            this.RecordClub = club;
            this.EntityList = new BindingList<Player>();

            // Without the record table there is nothing to read the players from.
            FixedAddresses fixedAddresses = FixedAddresses.For(memory);
            if (club == null || fixedAddresses == null || !new PlayerRecordResolver(memory).IsAvailable)
                return;

            List<ushort> ids = type == PlayerEnums.AddressType.TRAINEE
                ? ClubSquads.YouthIds(memory, fixedAddresses, club)
                : ClubSquads.SquadIds(memory, fixedAddresses, club);
            Dictionary<ushort, uint> workingCopies = ClubSquads.PlayerWorkingCopies(memory, fixedAddresses);
            foreach (ushort id in ids)
                this.EntityList.Add(this.GetRecordEntity(id, workingCopies.TryGetValue(id, out uint copy) ? copy : (uint?)null));
            Logger.Debug($"{club.Name} ({club.Country}/{club.Id}): {this.EntityList.Count} players from the records ({type}).");
        }

        private Player GetRecordEntity(ushort id, uint? workingCopy)
        {
            Player player = new Player
            {
                Addresses = AddressPresets.OWN_PLAYERS,
                Id = id,
                NameRecordId = id,
                ClubId = this.RecordClub.Id,
                ClubCountry = this.RecordClub.Country,
                WorkingCopy = workingCopy
            };
            this.ReadPersistentName(player);
            this.ReadPersistentFields(player);
            return player;
        }

        /// <summary>Reloads the full player list for the given club and, for OWN, refreshes the opponent's roster offsets.</summary>
        internal void RefreshPlayerList(Club club)
        {
            this.EntityList = new BindingList<Player>();

            Logger.Debug($"{club.PlayerCount} Players found.");

            string offset = string.Empty;
            for (int i=0; i < club.PlayerCount + club.AmateurPlayerCount; i++)
            {
                if (i > 0)
                {
                    offset = Tools.SumHex(new string[] { this.EntityList.Last().Offset, Settings.PlayerOffset });
                }
                this.EntityList.Add(this.GetEntity(offset, this.Type));
            }

            // While the layout is unverified, GetEntity's persistent reads were skipped, so the list
            // holds pure display-cache values - exactly the reference the calibrator needs. Only the
            // own roster is trusted for that. On success, re-read the persistent values the list is
            // still missing.
            if (this.Type == PlayerEnums.AddressType.OWN && !this.memory.Layout.IsVerified && this.TryCalibrateLayout())
            {
                foreach (Player player in this.EntityList)
                {
                    this.ReadPersistentName(player);
                    this.ReadPersistentFields(player);
                }
            }

            if (this.Type == PlayerEnums.AddressType.OWN)
            {
                this.InitOffsets(offset);
            }
        }

        // Runs PersistentLayoutCalibrator against the (display-cache-only) own roster and installs
        // the result on success. A failed attempt is remembered by roster fingerprint, so the full
        // module scan isn't repeated on every refresh until the roster actually changes (e.g. after
        // a savegame load or Tagesabschluss).
        private bool TryCalibrateLayout()
        {
            PersistentLayout current = this.memory.Layout;
            string signature = string.Join(",", this.EntityList.Select(p => $"{p.NameRecordId}:{p.Level}:{(byte)p.Position}"));
            if (signature == current.FailedCalibrationSignature)
                return false;

            PersistentLayout calibrated = new PersistentLayoutCalibrator(this.memory).Calibrate(this.EntityList.ToList());
            if (calibrated == null)
            {
                current.FailedCalibrationSignature = signature;
                return false;
            }

            this.memory.Layout = calibrated;
            return true;
        }

        // Returns one human-readable warning per Firstname/Lastname that couldn't be permanently
        // saved (see Save(Player) / WritePersistentName below) - empty if everything went through.
        // Callers (PlayerView) surface these to the user instead of leaving a silent, log-only trail.
        internal List<string> SaveEntityList()
        {
            Logger.Debug($"Saving {this.EntityList.Count} players");
            List<string> warnings = new List<string>();
            foreach (Player p in this.EntityList)
            {
                warnings.AddRange(this.Save(p));
            }
            return warnings;
        }

        internal override Player GetEntity(string offset, PlayerEnums.AddressType type)
        {
            Player player = new Player
            {
                Offset = offset,
                Addresses = AddressPresets.From(type, false)
            };
            #region Overview
            player.Id          = (uint) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.ID]));

            byte[] nameRecordIdBytes = this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.ID]), 2);
            player.NameRecordId = nameRecordIdBytes != null ? BitConverter.ToUInt16(nameRecordIdBytes, 0) : (ushort)0;

            this.ReadPersistentName(player);
            player.ClubId      = (ushort) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CLUB_ID]));
            player.ClubCountry = (PlayerEnums.Country) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CLUB_COUNTRY]));
            player.SkinColor   = (PlayerEnums.SkinColor) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SKIN]));
            player.HairColor   = (PlayerEnums.HairColor) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.HAIR]));
            player.Age         = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.AGE]));
            player.Level       = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.LEVEL]));
            player.Form        = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.FORM]));
            player.Jersey      = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.JERSEY]));
            player.Condition  = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CONDITION]));
            player.Freshness   = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.FRESHNESS]));
            player.Nationality = (PlayerEnums.Country)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.NATIONALITY]));
            #endregion

            #region Position
            player.Position           = (PlayerEnums.Position)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.POSITION]));
            player.SecondaryPositions = new List<PlayerEnums.Position>
            {
                (PlayerEnums.Position)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SECONDARY_1])),
                (PlayerEnums.Position)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SECONDARY_2]))
            };
            #endregion

            #region Skills
            player.Skills = (PlayerEnums.Skills)this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SKILLS]));
            player.NegativeSkills = (PlayerEnums.Skills)this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.NEG_SKILLS]));
            #endregion

            #region Character
            player.Personality = (PlayerEnums.Personality)this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.PERSONALITY]));
            player.Character   = (PlayerEnums.Character)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CHARACTER]));
            player.Health      = (PlayerEnums.Health)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.HEALTH]));
            #endregion

            #region Constitution
            player.Unhappy              = (PlayerEnums.Unhappy)this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.UNHAPPY]));
            player.Happy                = (PlayerEnums.Happy)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.HAPPY]));
            player.InjuredDays          = this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.INJURED_DAYS]));
            player.Injury               = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.INJURY]));
            player.VulnerableTicks      = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.VULNERABLE]));
            player.Vulnerable           = player.VulnerableTicks != 0;
            player.SetLeagueBan(
                (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.LEAGUE_BAN])),
                (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.LEAGUE_BAN_REASON])),
                this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.LEAGUE_BAN_END])),
                (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.BANNED_MATCHES])));
            player.DopingState          = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.DOPED]));
            player.Doped                = IsDoped(player.DopingState);
            player.YellowCardsSeason    = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.YELLOW_CARDS]));
            #endregion

            #region Contract
            player.Salary           = this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SALARY]));
            player.ShowUpBonus      = this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SHOWUP]));
            player.GoalsBonus       = this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.GOALS_BONUS]));
            player.TransferFee      = this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.TRANSFER_FEE]));
            player.BuyPrice         = (uint)this.memory.ReadInt32(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.BUY_PRICE]));
            player.HomeCountry      = (PlayerEnums.Country)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.HOME_COUNTRY]));
            player.HomeClubId       = this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.HOME_CLUB]));
            player.ContractDuration = (byte)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CONTRACT_DURATION]));
            player.ContractDetails  = (PlayerEnums.Contract)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CONTRACT_DETAILS]));
            player.YearsInClub      = (byte)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.YEARS_IN_CLUB]));
            player.Career           = (PlayerEnums.Career)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CAREER]));
            player.WasTransferListed = player.IsTransferListed;
            #endregion

            #region Statistics
            foreach (StatisticField field in StatisticFields)
                field.Set(player, (byte)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[field.Key])));
            #endregion

            #region Other
            /* Unknowm values */
            // 1, 8, 18, 27, 32, 33, 37, 39, 3B-43, 4D-55, 59, 5C - 6A, 6D, 6E, 73-76, 79
            #endregion

            #region NotImplemented
            /* Mainly statistical values with no benefit changing. */
            /*
            player.ClubId                = this.memory.ReadByte(GetAddress(this.memory, player, "23"));
            player.PreviousForm          = this.memory.ReadByte(GetAddress(this.memory, player, "26"));
            player.GoalsCupNational      = this.memory.ReadByte(GetAddress(this.memory, player, "57"));
            player.GoalsCupInternational = this.memory.ReadByte(GetAddress(this.memory, player, "58"));
            player.Goals1stLeague        = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, "5A"), 2), 0);
            player.PenaltiesSeason       = this.memory.ReadByte(GetAddress(this.memory, player, "6F"));
            player.Penalties             = this.memory.ReadByte(GetAddress(this.memory, player, "70"));
            player.FreekicksSeason       = this.memory.ReadByte(GetAddress(this.memory, player, "71"));
            player.Freekicks             = this.memory.ReadByte(GetAddress(this.memory, player, "72"));
            player.Appearances1stLeague  = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, "7A"), 2), 0);
            */
            #endregion

            // Overrides the fields above with their persistent, PlayerId-keyed values where known
            // (see WritePersistentLevel/Age/Position/Skills/Personality/Mood/Salary) - falls back to
            // whatever the display-cache reads above already set if the record can't be read. A
            // display cache that is stale or from another context once made the trainer show a
            // different Age than the game itself.
            this.ReadPersistentFields(player);

            return player;
        }

        // Reads Firstname/Lastname from their true, save-persistent pool address (see
        // NamePoolResolver) instead of the transient display cache the rest of this file's addresses
        // point at. Falls back to the old display-cache read if the pool can't be resolved (e.g. no
        // savegame loaded yet), so this never leaves the fields blank.
        private void ReadPersistentName(Player player)
        {
            Encoding encoding = Encoding.GetEncoding("iso-8859-1");
            NamePoolResolver namePool = new NamePoolResolver(this.memory);

            uint? firstnameAddress = namePool.ResolveFirstnameAddress(player.NameRecordId);
            player.Firstname = firstnameAddress.HasValue
                ? this.memory.ReadStringAtAddress(firstnameAddress.Value, 32, encoding)
                : this.ReadCacheString(player, PlayerEnums.AddressKey.FIRSTNAME, 9, encoding);

            uint? lastnameAddress = namePool.ResolveLastnameAddress(player.NameRecordId);
            player.Lastname = lastnameAddress.HasValue
                ? this.memory.ReadStringAtAddress(lastnameAddress.Value, 32, encoding)
                : this.ReadCacheString(player, PlayerEnums.AddressKey.LASTNAME, 15, encoding);

            Logger.Debug($"ReadPersistentName: NameRecordId={player.NameRecordId} (Id={player.Id}) -> Firstname=\"{player.Firstname}\" (addr={(firstnameAddress.HasValue ? "0x" + firstnameAddress.Value.ToString("X") : "fallback")}), Lastname=\"{player.Lastname}\" (addr={(lastnameAddress.HasValue ? "0x" + lastnameAddress.Value.ToString("X") : "fallback")}).");
        }

        // A display-cache string field: from the player's working copy, from the roster slot of the
        // own/opponent list, or "" for a listed-from-the-records player the game holds no copy of.
        private string ReadCacheString(Player player, PlayerEnums.AddressKey key, int length, Encoding encoding)
        {
            if (player.WorkingCopy.HasValue)
                return this.memory.ReadStringAtAddress(player.WorkingCopy.Value + CacheOffset(player, key), length, encoding);
            return this.RecordClub == null
                ? this.memory.ReadString(GetAddress(this.memory, player, player.Addresses[key]), length: length, stringEncoding: encoding)
                : string.Empty;
        }

        // Writes a display-cache field into the player's working copy or roster slot.
        private void WriteCache(Player player, PlayerEnums.AddressKey key, byte[] data)
        {
            if (player.WorkingCopy.HasValue)
                this.memory.WriteBytesAtAddress(player.WorkingCopy.Value + CacheOffset(player, key), data);
            else
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[key]), data);
        }

        private static uint CacheOffset(Player player, PlayerEnums.AddressKey key) => Convert.ToUInt32(player.Addresses[key], 16);

        // Whether the player has display-cache fields to write: a working copy for a player listed
        // from the records, the roster slot for the own/opponent roster.
        private bool HasCacheCopy(Player player) => this.RecordClub == null || player.WorkingCopy.HasValue;

        // The game may have fetched or freed the player since the list was read.
        private void RefreshWorkingCopy(Player player)
        {
            if (this.RecordClub != null)
                player.WorkingCopy = ClubSquads.PlayerWorkingCopy(this.memory, FixedAddresses.For(this.memory), player.NameRecordId);
        }

        // Counterpart to the WritePersistentXxx methods below: reads the same fields back out of
        // the persistent, PlayerId-keyed record instead of leaving them at whatever the transient
        // display-cache reads earlier in GetEntity produced. Overwrites the Player properties only
        // if the record can actually be read - otherwise leaves the display-cache values in place,
        // same fallback behavior as ReadPersistentName above.
        //
        // Two reads: the confirmed fields from SkinColor (+0x6) through Nationality (+0x25) are
        // packed into one call (with real gaps in between now - see Settings.cs, the "no gaps"
        // pattern didn't hold this far out); Salary/ShowUpBonus (+0x84) are far enough away to
        // need a second, separate one.
        private void ReadPersistentFields(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            // Record table not known/confirmed for this build (see PersistentLayout) - the display
            // cache values already read in GetEntity are the best there is.
            if (!recordResolver.IsAvailable)
                return;

            uint baseAddress = recordResolver.GetFieldAddress(player.NameRecordId, 0);
            int? currentYear = this.GetCurrentYear();

            const int mainChunkLength = 0x26;
            byte[] chunk = this.memory.ReadBytesAtAddress(baseAddress, mainChunkLength);
            if (chunk != null)
            {
                player.SkinColor = (PlayerEnums.SkinColor)chunk[Settings.PlayerRecordSkinColorOffset];
                player.HairColor = (PlayerEnums.HairColor)chunk[Settings.PlayerRecordSkinColorOffset + 1];
                if (currentYear.HasValue)
                    player.Age = (byte)(currentYear.Value - BitConverter.ToUInt16(chunk, (int)Settings.PlayerRecordAgeOffset));
                player.Level = chunk[Settings.PlayerRecordLevelOffset];
                player.Position = (PlayerEnums.Position)chunk[Settings.PlayerRecordPositionOffset];
                player.SecondaryPositions = new List<PlayerEnums.Position>
                {
                    (PlayerEnums.Position)chunk[Settings.PlayerRecordPositionOffset + 1],
                    (PlayerEnums.Position)chunk[Settings.PlayerRecordPositionOffset + 2]
                };
                player.Skills = (PlayerEnums.Skills)BitConverter.ToUInt16(chunk, (int)Settings.PlayerRecordSkillsOffset);
                player.NegativeSkills = (PlayerEnums.Skills)BitConverter.ToUInt16(chunk, (int)Settings.PlayerRecordSkillsOffset + 2);
                player.Personality = (PlayerEnums.Personality)BitConverter.ToUInt16(chunk, (int)Settings.PlayerRecordPersonalityOffset);
                player.Character = (PlayerEnums.Character)chunk[Settings.PlayerRecordCharacterOffset];
                player.Health = (PlayerEnums.Health)chunk[Settings.PlayerRecordHealthOffset];
                player.Unhappy = (PlayerEnums.Unhappy)BitConverter.ToUInt16(chunk, (int)Settings.PlayerRecordUnhappyOffset);
                player.Happy = (PlayerEnums.Happy)chunk[Settings.PlayerRecordUnhappyOffset + 2];
                player.Form = chunk[Settings.PlayerRecordFormOffset];
                player.Jersey = chunk[Settings.PlayerRecordJerseyOffset];
                player.Condition = chunk[Settings.PlayerRecordConditionOffset];
                player.Freshness = chunk[Settings.PlayerRecordFreshnessOffset];
                // The Country ID only ever occupies the low 7 bits here - Robin found a Russian
                // (Country=54=0x36) player encoded as 0xB6 (0x36 | 0x80). Masking off bit 7 handles
                // both that case and the plain, no-flag case (0x36) at once - see
                // Settings.PlayerRecordNationalityOffset and WritePersistentNationality below for
                // what happens to that bit on write.
                player.Nationality = (PlayerEnums.Country)(chunk[Settings.PlayerRecordNationalityOffset] & 0x7F);

                Logger.Debug($"ReadPersistentFields: NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{baseAddress:X} -> Age={player.Age}, Level={player.Level}, Position={player.Position}.");
            }
            else
            {
                Logger.Warn($"ReadPersistentFields: couldn't read record for NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{baseAddress:X} - keeping display-cache values.");
            }

            byte[] contractChunk = this.memory.ReadBytesAtAddress(baseAddress + Settings.PlayerRecordSalaryOffset, ContractChunkLength);
            if (contractChunk != null)
            {
                player.Salary = BitConverter.ToUInt16(contractChunk, 0);
                player.ShowUpBonus = BitConverter.ToUInt16(contractChunk, 2);
                player.GoalsBonus = BitConverter.ToUInt16(contractChunk, (int)(Settings.PlayerRecordGoalsBonusOffset - Settings.PlayerRecordSalaryOffset));
                player.TransferFee = BitConverter.ToUInt16(contractChunk, (int)(Settings.PlayerRecordTransferFeeOffset - Settings.PlayerRecordSalaryOffset));
                player.BuyPrice = BitConverter.ToUInt32(contractChunk, (int)(Settings.PlayerRecordBuyPriceOffset - Settings.PlayerRecordSalaryOffset));
            }

            byte[] homeChunk = this.memory.ReadBytesAtAddress(baseAddress + Settings.PlayerRecordHomeCountryOffset, HomeChunkLength);
            if (homeChunk != null)
            {
                player.HomeCountry = (PlayerEnums.Country)homeChunk[0];
                player.HomeClubId = BitConverter.ToUInt16(homeChunk, (int)(Settings.PlayerRecordHomeClubOffset - Settings.PlayerRecordHomeCountryOffset));
            }

            // End year/join year/details are contiguous; the two years only turn into the displayed
            // values with the savegame's current year, exactly as the game's own unpacking does it.
            byte[] termChunk = this.memory.ReadBytesAtAddress(baseAddress + Settings.PlayerRecordContractEndYearOffset, 5);
            if (termChunk != null)
            {
                if (currentYear.HasValue)
                {
                    int endYear = BitConverter.ToUInt16(termChunk, 0);
                    int joinYear = BitConverter.ToUInt16(termChunk, 2);
                    player.ContractDuration = (byte)Math.Max(0, endYear - currentYear.Value);
                    player.YearsInClub = (byte)(currentYear.Value - joinYear);
                }
                player.ContractDetails = (PlayerEnums.Contract)termChunk[Settings.PlayerRecordContractDetailsOffset - Settings.PlayerRecordContractEndYearOffset];
            }

            byte[] careerByte = this.memory.ReadBytesAtAddress(baseAddress + Settings.PlayerRecordCareerOffset, 1);
            if (careerByte != null)
                player.Career = (PlayerEnums.Career)careerByte[0];
            player.WasTransferListed = player.IsTransferListed;

            this.ReadPersistentConstitution(player, baseAddress);
            this.ReadPersistentStatistics(player, baseAddress);
        }

        // Constitution block, +0x2A through YellowCardsSeason (+0x36). Injury type, suspension,
        // doping and yellow cards are plain bytes; InjuredDays and Vulnerable only turn into the
        // displayed values with the savegame's calendar (see GameCalendar), so they keep their
        // display-cache values where that isn't available.
        private const int ConstitutionChunkLength = 0xD;

        private void ReadPersistentConstitution(Player player, uint baseAddress)
        {
            byte[] chunk = this.memory.ReadBytesAtAddress(baseAddress + Settings.PlayerRecordInjuryStartOffset, ConstitutionChunkLength);
            if (chunk == null)
                return;

            int At(uint recordOffset) => (int)(recordOffset - Settings.PlayerRecordInjuryStartOffset);
            player.Injury = chunk[At(Settings.PlayerRecordInjuryOffset)];
            byte[] ban = this.memory.ReadBytesAtAddress(baseAddress + Settings.PlayerRecordLeagueBanOffset, 2);
            byte[] banEnd = this.memory.ReadBytesAtAddress(baseAddress + Settings.PlayerRecordLeagueBanEndOffset, 2);
            if (ban != null && banEnd != null)
                player.SetLeagueBan(ban[0], ban[1], BitConverter.ToUInt16(banEnd, 0), chunk[At(Settings.PlayerRecordBannedOffset)]);
            player.DopingState = chunk[At(Settings.PlayerRecordBannedOffset + 1)];
            player.Doped = IsDoped(player.DopingState);
            player.YellowCardsSeason = chunk[At(Settings.PlayerRecordBannedOffset + 2)];

            GameCalendar calendar = GameCalendar.Read(this.memory);
            if (calendar == null)
                return;

            player.InjuredDays = calendar.InjuredDaysFromEndDate(
                BitConverter.ToInt16(chunk, At(Settings.PlayerRecordInjuryEndYearOffset)),
                BitConverter.ToInt16(chunk, At(Settings.PlayerRecordInjuryEndYearOffset + 2)));
            player.VulnerableTicks = calendar.VulnerableTicksFromEndValue(BitConverter.ToUInt16(chunk, At(Settings.PlayerRecordVulnerableOffset)));
            player.Vulnerable = player.VulnerableTicks != 0;
        }

        // Doping is active while the low nibble counts down; the upper bits are unrelated markers.
        private static bool IsDoped(byte dopingState) => (dopingState & 0x0F) != 0;

        // Makes a Firstname/Lastname change permanent in addition to the display cache write below,
        // so a rename actually survives a save/reload instead of being reset to the old value (see
        // NamePoolResolver's header comment for why the display cache alone isn't enough).
        //
        // The pool is deduplicated - all players named "Jakob" share ONE pool entry - so blindly
        // overwriting an entry's bytes turned every Jakob in the game into a Josef. In order:
        //   1. If the pool already holds the new name, only this player's index is repointed at it
        //      (affects nobody else, works for any length).
        //   2. Otherwise the entry is overwritten in place, but only if the new name has the same
        //      byte length (a different one would shift every later name in the tightly packed
        //      pool) AND no other player references the entry (NamePoolResolver.CountReferences).
        //      That covers fixing up a unique surname like AuLecht -> Aulecht.
        //   Original build: instead of 2., a name that isn't in the pool yet is appended to it the way
        //   the game itself adds names (NamePoolResolver.AppendName) and the player repointed at it -
        //   any length, nobody else affected. (An earlier prototype that moved the pool crashed the
        //   game because the game's own pointer table wasn't updated - see
        //   docs/manual-rename-workaround.md; appending leaves every existing entry where it is.)
        //   3. Anything else can't be made permanent; we log and return a warning the caller can show
        //      the user, and the display cache write below still shows the change until the next load.
        private List<string> WritePersistentName(Player player)
        {
            Encoding encoding = Encoding.GetEncoding("iso-8859-1");
            NamePoolResolver namePool = new NamePoolResolver(this.memory);
            List<string> warnings = new List<string>();

            // No name pool known/confirmed for this build at all (see PersistentLayout): that's a
            // property of the build, not of this player, so don't raise the per-name warning below
            // for every single player on every save - names then only go to the display cache, as
            // they did before the pool was found. SaveEntityList reports the build-level gap once.
            // (Save only calls this with a usable record table, so the pool pointer is all that's left to check.)
            if (!this.memory.Layout.NamePoolPointerOffset.HasValue)
                return warnings;

            string firstnameWarning = this.TryWritePersistentName(namePool, namePool.ResolveFirstnameAddress(player.NameRecordId), player.Firstname, encoding, isLastname: false, player);
            if (firstnameWarning != null) warnings.Add(firstnameWarning);

            string lastnameWarning = this.TryWritePersistentName(namePool, namePool.ResolveLastnameAddress(player.NameRecordId), player.Lastname, encoding, isLastname: true, player);
            if (lastnameWarning != null) warnings.Add(lastnameWarning);

            return warnings;
        }

        // Returns null on success (or when nothing changed), or a user-facing (German) warning
        // describing why the permanent write was skipped. Every skip is still logged via Logger.Warn
        // for troubleshooting - the returned string is a friendlier, player-identified version for the UI.
        private string TryWritePersistentName(NamePoolResolver namePool, uint? address, string newValue, Encoding encoding, bool isLastname, Player player)
        {
            string fieldLabel = isLastname ? "Nachname" : "Vorname";
            if (newValue == null)
                return null;

            string playerLabel = $"{player.Firstname} {player.Lastname}".Trim();

            if (!address.HasValue)
            {
                Logger.Warn($"Skipping persistent {fieldLabel} write for player NameRecordId={player.NameRecordId} (Id={player.Id}): could not resolve a pool address (NamePoolResolver returned null).");
                return $"{playerLabel}: {fieldLabel} - Adresse nicht auflösbar.";
            }

            const int maxScan = 40;
            byte[] currentBytes = this.memory.ReadBytesAtAddress(address.Value, maxScan);
            if (currentBytes == null)
            {
                Logger.Warn($"Skipping persistent {fieldLabel} write for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address.Value:X}: ReadBytesAtAddress returned null.");
                return $"{playerLabel}: {fieldLabel} - Speicher nicht lesbar.";
            }

            int currentLength = Array.IndexOf(currentBytes, (byte)0);
            string currentString = currentLength >= 0 ? encoding.GetString(currentBytes, 0, currentLength) : null;
            Logger.Debug($"Persistent {fieldLabel} for player NameRecordId={player.NameRecordId} (Id={player.Id}): address=0x{address.Value:X}, current=\"{currentString}\" ({currentLength} bytes), new=\"{newValue}\".");

            if (currentLength < 0)
            {
                Logger.Warn($"Skipping persistent {fieldLabel} write for player {player.NameRecordId}: couldn't find end of the current string within {maxScan} bytes.");
                return $"{playerLabel}: {fieldLabel} - Stringende nicht gefunden.";
            }

            if (currentString == newValue)
                return null;

            ushort? newIndex = namePool.FindIndexOf(newValue, encoding);
            if (!newIndex.HasValue && namePool.CanAppend)
            {
                // Not in the pool yet: add it the way the game adds names, then repoint below. Any
                // length (within the 9/15 the model already enforces), and nobody else is touched.
                newIndex = namePool.AppendName(newValue, encoding);
                if (!newIndex.HasValue)
                {
                    Logger.Warn($"Skipping persistent {fieldLabel} write for player NameRecordId={player.NameRecordId} (Id={player.Id}): \"{newValue}\" couldn't be added to the name pool.");
                    return $"{playerLabel}: {fieldLabel} \"{newValue}\" konnte nicht in den Namensbestand des Spielstands aufgenommen werden, nur bis zum nächsten Laden gespeichert.";
                }
            }
            if (newIndex.HasValue)
            {
                if (isLastname)
                    namePool.RepointLastname(player.NameRecordId, newIndex.Value);
                else
                    namePool.RepointFirstname(player.NameRecordId, newIndex.Value);
                Logger.Debug($"Repointed persistent {fieldLabel} for player NameRecordId={player.NameRecordId} (Id={player.Id}) from \"{currentString}\" to pool index {newIndex.Value} (\"{newValue}\").");
                return null;
            }

            byte[] newBytes = encoding.GetBytes(newValue);
            if (currentLength != newBytes.Length)
            {
                Logger.Warn($"Skipping persistent {fieldLabel} write for player NameRecordId={player.NameRecordId} (Id={player.Id}): \"{newValue}\" isn't in the name pool, and its length differs from \"{currentString}\" ({currentLength} -> {newBytes.Length} bytes) - would shift every later name in the pool.");
                return $"{playerLabel}: {fieldLabel} \"{newValue}\" kommt im Spielstand nicht vor und ist nicht genauso lang wie \"{currentString}\", nur bis zum nächsten Laden gespeichert.";
            }

            ushort? currentIndex = isLastname ? namePool.ReadLastnameIndex(player.NameRecordId) : namePool.ReadFirstnameIndex(player.NameRecordId);
            int? sharers = currentIndex.HasValue ? namePool.CountReferences(currentIndex.Value) : null;
            if (sharers != 1)
            {
                Logger.Warn($"Skipping persistent {fieldLabel} write for player NameRecordId={player.NameRecordId} (Id={player.Id}): pool entry {currentIndex} (\"{currentString}\") has {(sharers.HasValue ? sharers.ToString() : "an unknown number of")} references - overwriting it would rename every player sharing it.");
                return $"{playerLabel}: {fieldLabel} \"{newValue}\" kommt im Spielstand nicht vor, und \"{currentString}\" wird {(sharers.HasValue ? $"von {sharers} Spielern" : "möglicherweise von weiteren Spielern")} geteilt, nur bis zum nächsten Laden gespeichert.";
            }

            // Same length and this player is the entry's only user: overwriting it in place renames
            // nobody else and shifts nothing.
            byte[] toWrite = new byte[newBytes.Length + 1];
            Array.Copy(newBytes, toWrite, newBytes.Length);
            this.memory.WriteBytesAtAddress(address.Value, toWrite);
            Logger.Debug($"Overwrote unshared persistent {fieldLabel} for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address.Value:X}: \"{currentString}\" -> \"{newValue}\".");
            return null;
        }

        // Writes Level ("Stärke") to the same persistent per-player record as the name pool anchor
        // (anstoss2.exe+516678 + PlayerId*0xC8, see PlayerRecordResolver), in addition to the
        // display-cache write. The game packs a loaded player's working copy back over the record,
        // so for the own/opponent roster both are needed; for a player the game holds no copy of
        // (most youth players, other clubs) the record is the only write that sticks. Confirmed
        // live: changing this byte survived both a Tagesabschluss and a full save-to-file + reload.
        private void WritePersistentLevel(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordLevelOffset);
            this.memory.WriteBytesAtAddress(address, new byte[] { player.Level });
            Logger.Debug($"Wrote persistent Level for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Level}.");
        }

        // Same rationale as WritePersistentLevel above, for Age. The record holds the birth year
        // (UInt16), not the age, so this needs the CURRENT SAVEGAME's in-game year (see
        // GetCurrentYear) - skipped entirely if that can't be read, rather than guessing one.
        private void WritePersistentAge(Player player)
        {
            int? currentYear = this.GetCurrentYear();
            if (!currentYear.HasValue)
            {
                Logger.Warn($"Skipping persistent Age write for player NameRecordId={player.NameRecordId} (Id={player.Id}): couldn't read the savegame's current year (layout: {this.memory.Layout}).");
                return;
            }

            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordAgeOffset);
            ushort birthYear = (ushort)(currentYear.Value - player.Age);
            this.memory.WriteBytesAtAddress(address, BitConverter.GetBytes(birthYear));
            Logger.Debug($"Wrote persistent Age for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Age} (birth year {birthYear}).");
        }

        // The savegame's current in-game year, read live from anstoss2.exe+426662 (per savegame -
        // stable across restarts, but different between savegames, so never hardcoded). Several
        // record fields are stored as absolute years relative to it (birth year, contract end year,
        // club join year). Returns null if it can't be read right now, or its offset isn't
        // known/confirmed for the attached build (see PersistentLayout).
        private int? GetCurrentYear()
        {
            uint? yearOffset = this.memory.Layout.AgeReferenceYearOffset;
            if (!yearOffset.HasValue)
                return null;

            byte[] yearBytes = this.memory.ReadBytesAtAddress(this.memory.ModuleBase + yearOffset.Value, 2);
            if (yearBytes == null)
                return null;

            return BitConverter.ToUInt16(yearBytes, 0);
        }

        // Same rationale as WritePersistentLevel above, for Position. The main position and both
        // secondary positions are three contiguous bytes starting at Settings.PlayerRecordPositionOffset
        // (found directly by Robin), so all three go out in a single write. Missing secondary slots
        // are written as Position.None (0) rather than left untouched, so this record always reflects
        // exactly what's set in the UI.
        private void WritePersistentPosition(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordPositionOffset);
            byte secondary1 = player.SecondaryPositions.Count > 0 ? (byte)player.SecondaryPositions[0] : (byte)PlayerEnums.Position.None;
            byte secondary2 = player.SecondaryPositions.Count > 1 ? (byte)player.SecondaryPositions[1] : (byte)PlayerEnums.Position.None;
            this.memory.WriteBytesAtAddress(address, new byte[] { (byte)player.Position, secondary1, secondary2 });
            Logger.Debug($"Wrote persistent Position for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Position}/{secondary1}/{secondary2}.");
        }

        // Same rationale as WritePersistentLevel above, for Skills. NegativeSkills is a second
        // UInt16 immediately following Skills in the record, so both go out as one 4-byte write.
        private void WritePersistentSkills(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordSkillsOffset);
            byte[] toWrite = new byte[4];
            Array.Copy(BitConverter.GetBytes((ushort)player.Skills), 0, toWrite, 0, 2);
            Array.Copy(BitConverter.GetBytes((ushort)player.NegativeSkills), 0, toWrite, 2, 2);
            this.memory.WriteBytesAtAddress(address, toWrite);
            Logger.Debug($"Wrote persistent Skills for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: Skills={player.Skills}, NegativeSkills={player.NegativeSkills}.");
        }

        // Same rationale as WritePersistentLevel above, for Personality/Character/Health - three
        // contiguous fields (UInt16 + byte + byte, matching the display-cache "Character" region),
        // written in one call.
        private void WritePersistentPersonality(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordPersonalityOffset);
            byte[] toWrite = new byte[4];
            Array.Copy(BitConverter.GetBytes((ushort)player.Personality), 0, toWrite, 0, 2);
            toWrite[2] = (byte)player.Character;
            toWrite[3] = (byte)player.Health;
            this.memory.WriteBytesAtAddress(address, toWrite);
            Logger.Debug($"Wrote persistent Personality/Character/Health for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Personality}/{player.Character}/{player.Health}.");
        }

        // Same rationale as WritePersistentLevel above, for SkinColor/HairColor. Contiguous bytes,
        // so both go out in one write.
        private void WritePersistentAppearance(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordSkinColorOffset);
            this.memory.WriteBytesAtAddress(address, new byte[] { (byte)player.SkinColor, (byte)player.HairColor });
            Logger.Debug($"Wrote persistent SkinColor/HairColor for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.SkinColor}/{player.HairColor}.");
        }

        // Same rationale as WritePersistentLevel above, for Unhappy/Happy - contiguous UInt16 +
        // byte, written in one call.
        private void WritePersistentMood(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordUnhappyOffset);
            byte[] toWrite = new byte[3];
            Array.Copy(BitConverter.GetBytes((ushort)player.Unhappy), 0, toWrite, 0, 2);
            toWrite[2] = (byte)player.Happy;
            this.memory.WriteBytesAtAddress(address, toWrite);
            Logger.Debug($"Wrote persistent Unhappy/Happy for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Unhappy}/{player.Happy}.");
        }

        /// <summary>
        /// Why the shirt numbers of <paramref name="squad"/> can't be saved as they are - a number
        /// above 99 or one given twice, as the game's own "Rückennummern" dialog refuses - or null
        /// if they're fine. 0 (no number yet, e.g. right after a transfer) is left for the game to
        /// fill in. Youth players aren't in a squad and have no number to check.
        /// </summary>
        internal static string JerseyProblem(IEnumerable<Player> squad, PlayerEnums.AddressType type)
        {
            if (type == PlayerEnums.AddressType.TRAINEE)
                return null;
            List<Player> players = squad.Where(p => p.Jersey != 0).ToList();
            Player outOfRange = players.FirstOrDefault(p => p.Jersey > 99);
            if (outOfRange != null)
                return $"Rückennummern gehen von 1 bis 99 ({outOfRange}: {outOfRange.Jersey}).";
            IGrouping<byte, Player> twice = players.GroupBy(p => p.Jersey).FirstOrDefault(g => g.Count() > 1);
            if (twice != null)
                return $"Bitte jede Rückennummer nur einmal vergeben! (Nr. {twice.Key}: {string.Join(", ", twice)})";
            return null;
        }

        // Robin found this one directly, isolated from the group above (+0x1E) - Form had been
        // assumed omitted like the other club-context fields (ClubId/ClubCountry), but it turns out
        // it isn't. The shirt number follows right after it (+0x1F, FUN_00582a60/FUN_00583120), so
        // both go in one write; the 3 bytes between Happy (+0x1A/+0x1B end) and Form stay untouched.
        private void WritePersistentForm(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordFormOffset);
            this.memory.WriteBytesAtAddress(address, new byte[] { player.Form, player.Jersey });
            Logger.Debug($"Wrote persistent Form/Jersey for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Form}/{player.Jersey}.");
        }

        // Condition/Freshness, found directly by Robin at +0x21/+0x22 - contiguous with each other
        // (unlike Form above), so written together, but 2 bytes past Form with unknown content in
        // between that this shouldn't touch either.
        private void WritePersistentConditionFreshness(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordConditionOffset);
            this.memory.WriteBytesAtAddress(address, new byte[] { player.Condition, player.Freshness });
            Logger.Debug($"Wrote persistent Condition/Freshness for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Condition}/{player.Freshness}.");
        }

        // Nationality/"Land", found directly by Robin at +0x25 - isolated write, 2 bytes past
        // Freshness with unknown content in between.
        //
        // Bit 7 of this byte isn't part of the Country ID - Robin found a Russian player
        // (Country=54=0x36) encoded as 0xB6 (0x36 | 0x80), and confirmed writing the plain 0x36
        // still displays correctly in-game. What that bit actually means is unknown ("evtl. ist da
        // noch was anderes codiert"), so rather than guess at it (always clear it, or always set
        // it), this reads the byte currently there first and preserves whatever it already has -
        // only the low 7 bits (the Country ID) get replaced.
        private void WritePersistentNationality(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordNationalityOffset);

            byte[] currentByte = this.memory.ReadBytesAtAddress(address, 1);
            byte preservedHighBit = currentByte != null ? (byte)(currentByte[0] & 0x80) : (byte)0;
            byte newByte = (byte)(preservedHighBit | ((byte)player.Nationality & 0x7F));

            this.memory.WriteBytesAtAddress(address, new byte[] { newByte });
            Logger.Debug($"Wrote persistent Nationality for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Nationality} (byte 0x{newByte:X2}, preserved high bit={(preservedHighBit != 0)}).");
        }

        // "Stammverein": country byte, one byte the game copies along but never reads, club UInt16.
        private const int HomeChunkLength = 4;

        // Country and club separately, so the byte between them stays as it is.
        private void WritePersistentHomeClub(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            this.memory.WriteBytesAtAddress(recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordHomeCountryOffset), new byte[] { (byte)player.HomeCountry });
            this.memory.WriteBytesAtAddress(recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordHomeClubOffset), BitConverter.GetBytes(player.HomeClubId));
            Logger.Debug($"Wrote persistent home club for player NameRecordId={player.NameRecordId} (Id={player.Id}): {player.HomeCountry}/{player.HomeClubId}.");
        }

        // Salary/ShowUpBonus/GoalsBonus/TransferFee (four contiguous UInt16s) and BuyPrice (UInt32) -
        // read and written as one block.
        private const int ContractChunkLength = 0xC;

        // Same rationale as WritePersistentLevel above, for the contract block - written in one call.
        private void WritePersistentSalary(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordSalaryOffset);
            byte[] toWrite = new byte[ContractChunkLength];
            Array.Copy(BitConverter.GetBytes(player.Salary), 0, toWrite, 0, 2);
            Array.Copy(BitConverter.GetBytes(player.ShowUpBonus), 0, toWrite, 2, 2);
            Array.Copy(BitConverter.GetBytes(player.GoalsBonus), 0, toWrite, Settings.PlayerRecordGoalsBonusOffset - Settings.PlayerRecordSalaryOffset, 2);
            Array.Copy(BitConverter.GetBytes(player.TransferFee), 0, toWrite, Settings.PlayerRecordTransferFeeOffset - Settings.PlayerRecordSalaryOffset, 2);
            Array.Copy(BitConverter.GetBytes(player.BuyPrice), 0, toWrite, Settings.PlayerRecordBuyPriceOffset - Settings.PlayerRecordSalaryOffset, 4);
            this.memory.WriteBytesAtAddress(address, toWrite);
            Logger.Debug($"Wrote persistent Salary/ShowUpBonus/GoalsBonus/TransferFee/BuyPrice for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Salary}/{player.ShowUpBonus}/{player.GoalsBonus}/{player.TransferFee}/{player.BuyPrice}.");
        }

        // Constitution: injury type, red-card suspension, doping and yellow cards as plain bytes,
        // plus the two calendar-based fields, packed exactly as the game does it (FUN_00583120):
        // no injury days left -> start, end date and the "cleared" flag at +0xAD all go to 0;
        // otherwise the end date comes from the game's calendar table, and a NEW injury (start still
        // 0) gets today's counter as its start, like the game's own injury setter (FUN_006337e0).
        // Vulnerable is written as the counter value it ends at. The two calendar fields are skipped
        // if the calendar isn't available, rather than written with a guessed date.
        private void WritePersistentConstitution(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint Address(uint recordOffset) => recordResolver.GetFieldAddress(player.NameRecordId, recordOffset);

            this.memory.WriteBytesAtAddress(Address(Settings.PlayerRecordInjuryOffset), new byte[] { player.Injury });
            this.memory.WriteBytesAtAddress(Address(Settings.PlayerRecordBannedOffset), new byte[] { player.NextMatchBan, player.DopingStateToWrite, player.YellowCardsSeason });
            this.memory.WriteBytesAtAddress(Address(Settings.PlayerRecordLeagueBanOffset), new byte[] { player.LeagueBanState, player.LeagueBanReason });
            this.memory.WriteBytesAtAddress(Address(Settings.PlayerRecordLeagueBanEndOffset), BitConverter.GetBytes(player.LeagueBanEndDay));

            GameCalendar calendar = GameCalendar.Read(this.memory);
            if (calendar == null)
            {
                Logger.Warn($"Skipping persistent InjuredDays/Vulnerable write for player NameRecordId={player.NameRecordId} (Id={player.Id}): savegame calendar not available (layout: {this.memory.Layout}).");
                return;
            }

            if (player.InjuredDays < 1)
            {
                this.memory.WriteBytesAtAddress(Address(Settings.PlayerRecordInjuryStartOffset), new byte[6]);
                this.memory.WriteBytesAtAddress(Address(Settings.PlayerRecordInjuryClearedFlagOffset), new byte[] { 0 });
            }
            else
            {
                Tuple<short, short> endDate = calendar.EndDateForInjuredDays(player.InjuredDays);
                byte[] startBytes = this.memory.ReadBytesAtAddress(Address(Settings.PlayerRecordInjuryStartOffset), 2);
                if (endDate == null || startBytes == null)
                {
                    Logger.Warn($"Skipping persistent InjuredDays write for player NameRecordId={player.NameRecordId} (Id={player.Id}): {player.InjuredDays} days lie outside the game's calendar.");
                }
                else
                {
                    ushort start = BitConverter.ToUInt16(startBytes, 0);
                    if (start == 0)
                        start = (ushort)calendar.GlobalCounter;

                    byte[] injury = new byte[6];
                    Array.Copy(BitConverter.GetBytes(start), 0, injury, 0, 2);
                    Array.Copy(BitConverter.GetBytes(endDate.Item1), 0, injury, 2, 2);
                    Array.Copy(BitConverter.GetBytes(endDate.Item2), 0, injury, 4, 2);
                    this.memory.WriteBytesAtAddress(Address(Settings.PlayerRecordInjuryStartOffset), injury);
                }
            }

            this.memory.WriteBytesAtAddress(Address(Settings.PlayerRecordVulnerableOffset), BitConverter.GetBytes(calendar.VulnerableEndValueForTicks(player.VulnerableTicksToWrite)));
            Logger.Debug($"Wrote persistent constitution for player NameRecordId={player.NameRecordId} (Id={player.Id}): Injury={player.Injury}, Days={player.InjuredDays}, Vulnerable={player.VulnerableTicksToWrite}, Ban=0x{player.LeagueBanState:X2}/0x{player.LeagueBanReason:X2} until day {player.LeagueBanEndDay}, NextMatch=0x{player.NextMatchBan:X2}, Doping=0x{player.DopingStateToWrite:X2}, Yellow={player.YellowCardsSeason}.");
        }

        // ContractDuration/YearsInClub/ContractDetails and Career. The record stores the contract's
        // end year and the club join year rather than the displayed counts (see Settings.cs), so
        // both are converted with the savegame's current year - the same way the game packs them.
        // Like Age, the two years are skipped entirely if that year can't be read; ContractDetails
        // and Career don't depend on it.
        private void WritePersistentContractTerms(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint termsAddress = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordContractEndYearOffset);
            uint detailsAddress = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordContractDetailsOffset);

            int? currentYear = this.GetCurrentYear();
            if (currentYear.HasValue)
            {
                byte[] years = new byte[4];
                Array.Copy(BitConverter.GetBytes((ushort)(currentYear.Value + player.ContractDuration)), 0, years, 0, 2);
                Array.Copy(BitConverter.GetBytes((ushort)(currentYear.Value - player.YearsInClub)), 0, years, 2, 2);
                this.memory.WriteBytesAtAddress(termsAddress, years);
            }
            else
            {
                Logger.Warn($"Skipping persistent ContractDuration/YearsInClub write for player NameRecordId={player.NameRecordId} (Id={player.Id}): couldn't read the savegame's current year (layout: {this.memory.Layout}).");
            }

            this.memory.WriteBytesAtAddress(detailsAddress, new byte[] { (byte)player.ContractDetails });
            this.memory.WriteBytesAtAddress(recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordCareerOffset), new byte[] { (byte)player.Career });
            Logger.Debug($"Wrote persistent contract terms for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{termsAddress:X}: Duration={player.ContractDuration}, YearsInClub={player.YearsInClub}, Details={player.ContractDetails}, Career={player.Career}.");
        }

        // A ticked box puts the player on the transfer list (TransferList.Add, like the manager does
        // in the game), an unticked one takes the player off (FUN_0062c0f0). Career bit 4 then goes
        // out with every other save, as the game's FUN_0062c820/FUN_0062c910 set and clear it. If the
        // list can't take the player (full, unreadable), the box is unticked again.
        private void ApplyTransferListChange(Player player)
        {
            FixedAddresses fixedAddresses = FixedAddresses.For(this.memory);
            if (player.IsTransferListed && !player.WasTransferListed && !TransferList.Add(this.memory, fixedAddresses, player))
                player.IsTransferListed = false;
            else if (player.WasTransferListed && !player.IsTransferListed)
                TransferList.Remove(this.memory, fixedAddresses, player.NameRecordId);
            player.WasTransferListed = player.IsTransferListed;
        }

        // The season statistics (see Settings.PlayerRecordRedCardsOffset): one byte each, with its
        // display-cache key and its record offset.
        private sealed class StatisticField
        {
            public PlayerEnums.AddressKey Key;
            public uint RecordOffset;
            public Func<Player, byte> Get;
            public Action<Player, byte> Set;
        }

        private static readonly StatisticField[] StatisticFields =
        {
            new StatisticField { Key = PlayerEnums.AddressKey.RED_CARDS, RecordOffset = Settings.PlayerRecordRedCardsOffset, Get = p => p.RedCardsSeason, Set = (p, v) => p.RedCardsSeason = v },
            new StatisticField { Key = PlayerEnums.AddressKey.YELLOW_RED_CARDS, RecordOffset = Settings.PlayerRecordYellowRedCardsOffset, Get = p => p.YellowRedCardsSeason, Set = (p, v) => p.YellowRedCardsSeason = v },
            new StatisticField { Key = PlayerEnums.AddressKey.GOALS, RecordOffset = Settings.PlayerRecordGoalsOffset, Get = p => p.Goals, Set = (p, v) => p.Goals = v },
            new StatisticField { Key = PlayerEnums.AddressKey.OWN_GOALS, RecordOffset = Settings.PlayerRecordOwnGoalsOffset, Get = p => p.OwnGoals, Set = (p, v) => p.OwnGoals = v },
            new StatisticField { Key = PlayerEnums.AddressKey.ASSISTS, RecordOffset = Settings.PlayerRecordAssistsOffset, Get = p => p.Assists, Set = (p, v) => p.Assists = v },
            new StatisticField { Key = PlayerEnums.AddressKey.JOKER_GOALS, RecordOffset = Settings.PlayerRecordJokerGoalsOffset, Get = p => p.JokerGoals, Set = (p, v) => p.JokerGoals = v },
            new StatisticField { Key = PlayerEnums.AddressKey.APPEARANCES, RecordOffset = Settings.PlayerRecordAppearancesOffset, Get = p => p.Appearances, Set = (p, v) => p.Appearances = v },
            new StatisticField { Key = PlayerEnums.AddressKey.SUBBED_IN, RecordOffset = Settings.PlayerRecordSubbedInOffset, Get = p => p.SubbedIn, Set = (p, v) => p.SubbedIn = v },
            new StatisticField { Key = PlayerEnums.AddressKey.SUBBED_OUT, RecordOffset = Settings.PlayerRecordSubbedOutOffset, Get = p => p.SubbedOut, Set = (p, v) => p.SubbedOut = v },
            new StatisticField { Key = PlayerEnums.AddressKey.UPGRADE_GOALS, RecordOffset = Settings.PlayerRecordUpgradeGoalsOffset, Get = p => p.UpgradeGoals, Set = (p, v) => p.UpgradeGoals = v },
            new StatisticField { Key = PlayerEnums.AddressKey.CAPS, RecordOffset = Settings.PlayerRecordCapsOffset, Get = p => p.Caps, Set = (p, v) => p.Caps = v },
        };

        // All statistics lie between the red cards and the upgrade goals - read in one call.
        private static readonly int StatisticsChunkLength = (int)(Settings.PlayerRecordUpgradeGoalsOffset - Settings.PlayerRecordRedCardsOffset + 1);

        private void ReadPersistentStatistics(Player player, uint baseAddress)
        {
            byte[] chunk = this.memory.ReadBytesAtAddress(baseAddress + Settings.PlayerRecordRedCardsOffset, StatisticsChunkLength);
            if (chunk == null)
                return;

            foreach (StatisticField field in StatisticFields)
                field.Set(player, chunk[field.RecordOffset - Settings.PlayerRecordRedCardsOffset]);
        }

        // Byte by byte, so the bytes in between (other statistics the Trainer doesn't edit) stay as they are.
        private void WritePersistentStatistics(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            foreach (StatisticField field in StatisticFields)
                this.memory.WriteBytesAtAddress(recordResolver.GetFieldAddress(player.NameRecordId, field.RecordOffset), new byte[] { field.Get(player) });
            Logger.Debug($"Wrote persistent statistics for player NameRecordId={player.NameRecordId} (Id={player.Id}): Goals={player.Goals}, Assists={player.Assists}, Appearances={player.Appearances}.");
        }

        // An edited ban count becomes the game's ban bytes (see Player.ApplyLeagueBanChange). A new
        // red-card ban needs today's season day for its end; without the calendar the ban stays as it
        // was rather than being written without an end day, which the game would drop on loading.
        private void ApplyLeagueBanChange(Player player)
        {
            if (player.RedCardBannedMatches == (player.LeagueBanState & Player.LeagueBanCountMask))
                return;

            byte requested = player.RedCardBannedMatches;
            short? today = GameCalendar.Read(this.memory)?.GlobalCounter;
            if (player.ApplyLeagueBanChange(today))
            {
                Logger.Debug($"League ban for player NameRecordId={player.NameRecordId} (Id={player.Id}) set to {requested} match(es): 0x{player.LeagueBanState:X2}/0x{player.LeagueBanReason:X2} until day {player.LeagueBanEndDay}.");
            }
            else
            {
                Logger.Warn($"League ban for player NameRecordId={player.NameRecordId} (Id={player.Id}) left at {player.LeagueBanState & Player.LeagueBanCountMask} match(es): the season day for its end isn't available (layout: {this.memory.Layout}).");
                player.RedCardBannedMatches = (byte)(player.LeagueBanState & Player.LeagueBanCountMask);
            }
        }

        // Returns one warning per Firstname/Lastname that couldn't be permanently saved (see
        // WritePersistentName) - empty if everything went through.
        //
        // The persistent, PlayerId-keyed writes below always run - they don't depend on which
        // context is currently loaded. The display-cache fields go to the own/opponent roster slot,
        // or for a player listed from the records to the working copy the game holds of him right
        // now (looked up again, the game fetches and frees them all the time) - without one the
        // record is all there is. The game packs a working copy back over the record, so a player
        // it holds one of must get both.
        /// <summary>Writes a player's persistent fields (name/level/age/etc.) and, where the game holds one, its display-cache copy too. Returns any name-save warnings to surface to the user.</summary>
        public List<string> Save(Player player)
        {
            List<string> nameWarnings = new List<string>();
            this.RefreshWorkingCopy(player);
            int? previousAge = this.RecordAge(player);

            // Never write through record-table anchors that aren't known/confirmed for the attached
            // build (see PersistentLayout) - a wrong guess would land in unrelated game memory.
            this.ApplyTransferListChange(player);
            this.ApplyLeagueBanChange(player);

            if (new PlayerRecordResolver(this.memory).IsAvailable)
            {
                nameWarnings = this.WritePersistentName(player);
                this.WritePersistentAge(player);
                this.WritePersistentLevel(player);
                this.WritePersistentPosition(player);
                this.WritePersistentSkills(player);
                this.WritePersistentPersonality(player);
                this.WritePersistentAppearance(player);
                this.WritePersistentMood(player);
                this.WritePersistentForm(player);
                this.WritePersistentConditionFreshness(player);
                this.WritePersistentNationality(player);
                this.WritePersistentSalary(player);
                this.WritePersistentHomeClub(player);
                this.WritePersistentContractTerms(player);
                this.WritePersistentConstitution(player);
                this.WritePersistentStatistics(player);
                if (this.Type == PlayerEnums.AddressType.TRAINEE && this.RecordClub != null && previousAge.HasValue)
                    nameWarnings.AddRange(this.FileYouthByAge(player, previousAge.Value != player.Age));
            }

            if (this.HasCacheCopy(player))
            {
                nameWarnings.AddRange(this.GetDisplayCacheOnlyRenameWarnings(player));

                #region Overview
                Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
                this.WriteCache(player, PlayerEnums.AddressKey.FIRSTNAME, latin1.GetBytes(player.Firstname.PadRight(9, '\0')));
                this.WriteCache(player, PlayerEnums.AddressKey.LASTNAME, latin1.GetBytes(player.Lastname.PadRight(15, '\0')));

                this.WriteCache(player, PlayerEnums.AddressKey.SKIN, new byte[] { (byte) player.SkinColor });
                this.WriteCache(player, PlayerEnums.AddressKey.HAIR, new byte[] { (byte) player.HairColor });

                this.WriteCache(player, PlayerEnums.AddressKey.AGE, new byte[] { player.Age });
                this.WriteCache(player, PlayerEnums.AddressKey.LEVEL, new byte[] { player.Level });
                this.WriteCache(player, PlayerEnums.AddressKey.FORM, new byte[] { player.Form });
                this.WriteCache(player, PlayerEnums.AddressKey.JERSEY, new byte[] { player.Jersey });
                this.WriteCache(player, PlayerEnums.AddressKey.CONDITION, new byte[] { player.Condition });
                this.WriteCache(player, PlayerEnums.AddressKey.FRESHNESS, new byte[] { player.Freshness });

                this.WriteCache(player, PlayerEnums.AddressKey.NATIONALITY, new byte[] { (byte)player.Nationality });
                #endregion

                #region Positions
                this.WriteCache(player, PlayerEnums.AddressKey.POSITION, new byte[] { (byte)player.Position });
                if (player.SecondaryPositions.Count > 0)
                {
                    this.WriteCache(player, PlayerEnums.AddressKey.SECONDARY_1, new byte[] { (byte)player.SecondaryPositions[0] });
                    if (player.SecondaryPositions.Count > 1)
                    {
                        this.WriteCache(player, PlayerEnums.AddressKey.SECONDARY_2, new byte[] { (byte)player.SecondaryPositions[1] });
                    }
                }
                #endregion

                #region Skills
                this.WriteCache(player, PlayerEnums.AddressKey.SKILLS, BitConverter.GetBytes((ushort)player.Skills));
                this.WriteCache(player, PlayerEnums.AddressKey.NEG_SKILLS, BitConverter.GetBytes((ushort)player.NegativeSkills));
                #endregion

                #region Character
                this.WriteCache(player, PlayerEnums.AddressKey.PERSONALITY, BitConverter.GetBytes((ushort)player.Personality));
                this.WriteCache(player, PlayerEnums.AddressKey.CHARACTER, new byte[] { (byte)player.Character });
                this.WriteCache(player, PlayerEnums.AddressKey.HEALTH, new byte[] { (byte)player.Health });
                #endregion

                #region Constitution
                this.WriteCache(player, PlayerEnums.AddressKey.UNHAPPY, BitConverter.GetBytes((ushort)player.Unhappy));
                this.WriteCache(player, PlayerEnums.AddressKey.HAPPY, BitConverter.GetBytes((byte)player.Happy));

                // The kind goes to the working copy too: the game shows it from there and writes it
                // back over the record (FUN_00583120), so a record-only write was lost.
                this.WriteCache(player, PlayerEnums.AddressKey.INJURY, new byte[] { player.Injury });
                this.WriteCache(player, PlayerEnums.AddressKey.INJURED_DAYS, BitConverter.GetBytes(player.InjuredDays));
                this.WriteCache(player, PlayerEnums.AddressKey.VULNERABLE, new byte[] { player.VulnerableTicksToWrite });

                this.WriteCache(player, PlayerEnums.AddressKey.BANNED_MATCHES, new byte[] { player.NextMatchBan });
                this.WriteCache(player, PlayerEnums.AddressKey.LEAGUE_BAN, new byte[] { player.LeagueBanState, player.LeagueBanReason });
                this.WriteCache(player, PlayerEnums.AddressKey.LEAGUE_BAN_END, BitConverter.GetBytes(player.LeagueBanEndDay));
                this.WriteCache(player, PlayerEnums.AddressKey.DOPED, new byte[] { player.DopingStateToWrite });
                this.WriteCache(player, PlayerEnums.AddressKey.YELLOW_CARDS, new byte[] { (byte)player.YellowCardsSeason });
                #endregion

                #region Contract
                this.WriteCache(player, PlayerEnums.AddressKey.SALARY, BitConverter.GetBytes(player.Salary));
                this.WriteCache(player, PlayerEnums.AddressKey.SHOWUP, BitConverter.GetBytes(player.ShowUpBonus));
                this.WriteCache(player, PlayerEnums.AddressKey.GOALS_BONUS, BitConverter.GetBytes(player.GoalsBonus));
                this.WriteCache(player, PlayerEnums.AddressKey.TRANSFER_FEE, BitConverter.GetBytes(player.TransferFee));
                this.WriteCache(player, PlayerEnums.AddressKey.BUY_PRICE, BitConverter.GetBytes(player.BuyPrice));
                this.WriteCache(player, PlayerEnums.AddressKey.HOME_COUNTRY, new byte[] { (byte)player.HomeCountry });
                this.WriteCache(player, PlayerEnums.AddressKey.HOME_CLUB, BitConverter.GetBytes(player.HomeClubId));
                this.WriteCache(player, PlayerEnums.AddressKey.CONTRACT_DURATION, new byte[] { (byte)player.ContractDuration });
                this.WriteCache(player, PlayerEnums.AddressKey.CONTRACT_DETAILS, BitConverter.GetBytes((byte)player.ContractDetails));
                this.WriteCache(player, PlayerEnums.AddressKey.YEARS_IN_CLUB, new byte[] { (byte)player.YearsInClub });
                this.WriteCache(player, PlayerEnums.AddressKey.CAREER, BitConverter.GetBytes((byte)player.Career));
                #endregion

                #region Statistics
                foreach (StatisticField field in StatisticFields)
                    this.WriteCache(player, field.Key, new byte[] { field.Get(player) });
                #endregion

                #region Other
                #endregion

                #region NotImplemented
                #endregion
            }

            return nameWarnings;
        }

        // Age as the record holds it now (birth year against the current year), or null if unknown.
        private int? RecordAge(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            int? currentYear = this.GetCurrentYear();
            if (!recordResolver.IsAvailable || !currentYear.HasValue)
                return null;
            byte[] birthYear = this.memory.ReadBytesAtAddress(recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordAgeOffset), 2);
            return birthYear == null ? (int?)null : currentYear.Value - BitConverter.ToUInt16(birthYear, 0);
        }

        // A youth player made older or younger has to change his youth group as well (see
        // ClubSquads.MoveYouthToAgeGroup) - only from the A youth can he be signed by appointment.
        private IEnumerable<string> FileYouthByAge(Player player, bool ageChanged)
        {
            ClubSquads.YouthMove move = ClubSquads.MoveYouthToAgeGroup(this.memory, FixedAddresses.For(this.memory), this.RecordClub, player.NameRecordId, player.Age, ageChanged, out int from, out int to);
            Logger.Debug($"FileYouthByAge: player {player.NameRecordId}, age {player.Age}: {move} ({ClubSquads.YouthGroupNames[from]} -> {ClubSquads.YouthGroupNames[to]}).");
            if (move == ClubSquads.YouthMove.GroupFull)
                yield return $"{player}: Die {ClubSquads.YouthGroupNames[to]} ist voll, er bleibt in der {ClubSquads.YouthGroupNames[from]}."
                    + (to == 0 ? " Verpflichten lässt er sich per Termin erst aus der A-Jugend." : "");
        }

        // Without a name pool for this build (see PersistentLayout), WritePersistentName skips every
        // player silently - but an actual rename then only reaches the display cache and is lost on
        // the next load, which the user must hear about. Only warns for names that really changed
        // (compared against what the display cache still holds), so an untouched roster saves quietly.
        private List<string> GetDisplayCacheOnlyRenameWarnings(Player player)
        {
            List<string> warnings = new List<string>();
            if (this.memory.Layout.NamePoolPointerOffset.HasValue && this.memory.Layout.PlayerRecordTableOffset.HasValue)
                return warnings;

            Encoding encoding = Encoding.GetEncoding("iso-8859-1");
            string playerLabel = $"{player.Firstname} {player.Lastname}".Trim();
            string currentFirstname = this.ReadCacheString(player, PlayerEnums.AddressKey.FIRSTNAME, 9, encoding);
            string currentLastname = this.ReadCacheString(player, PlayerEnums.AddressKey.LASTNAME, 15, encoding);

            if (player.Firstname != null && player.Firstname != currentFirstname)
                warnings.Add($"{playerLabel}: Vorname - für diese Spielversion nur bis zum nächsten Laden gespeichert.");
            if (player.Lastname != null && player.Lastname != currentLastname)
                warnings.Add($"{playerLabel}: Nachname - für diese Spielversion nur bis zum nächsten Laden gespeichert.");
            return warnings;
        }

        /// <summary>
        /// Writes only the given fields (condition, freshness) via the display-cache addresses; for a
        /// player listed from the records into his record and, if the game holds one, his working copy.
        /// </summary>
        public void Save (Player player, List<PlayerEnums.AddressKey> fieldsToSave)
        {
            this.RefreshWorkingCopy(player);
            if (this.RecordClub != null && new PlayerRecordResolver(this.memory).IsAvailable)
                this.WritePersistentConditionFreshness(player);
            if (!this.HasCacheCopy(player))
                return;

            foreach (var field in fieldsToSave) {
                switch (field) {
                    case PlayerEnums.AddressKey.CONDITION:
                        this.WriteCache(player, field, new byte[] { player.Condition });
                        break;

                    case PlayerEnums.AddressKey.FRESHNESS:
                        this.WriteCache(player, field, new byte[] { player.Freshness });
                        break;
                }
            }
        }

        /// <summary>True while the display-cache slot this player was read from still holds the same player; always for a player listed from the records.</summary>
        // The game reuses the OWN/OPPONENT slots for other clubs during a Wochenabschluss or a match
        // and packs the slot back into each player's record afterwards (FUN_00583120), so a write
        // into a slot that now holds someone else ends up permanently in that player's record.
        // A record, and the working copy looked up by the player's ID, always belong to that player.
        internal bool IsStillLoaded(Player player)
        {
            if (this.RecordClub != null)
                return true;
            byte[] nameRecordIdBytes = this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.ID]), 2);
            return nameRecordIdBytes != null && BitConverter.ToUInt16(nameRecordIdBytes, 0) == player.NameRecordId;
        }

        // The opponent's players follow the own club's in the roster slots.
        private void InitOffsets(string offset)
        {
            this.OpponentOffset = offset;
            AddressPresets.InitOpponent(this.OpponentOffset);
        }
    }
}
