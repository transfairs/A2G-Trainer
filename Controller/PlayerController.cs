using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Reads/writes a club's player roster. Combines the transient display-cache fields (the
    /// addresses the rest of the project historically used) with the smaller set of fields
    /// confirmed to live in the permanent, PlayerId-keyed record (see ReadPersistentFields/
    /// WritePersistentXxx and PlayerRecordResolver) - the latter are authoritative where mapped.
    /// </summary>
    public class PlayerController : EntityController<Player>
    {
        /// <summary>Roster-start offset of the opponent club's players, resolved once loading the user's own club.</summary>
        public String OpponentOffset { get => this.opponentOffset; set => opponentOffset = value; }
        private String opponentOffset;
        /// <summary>Roster-start offset of the counterpart (opponent/own) club's players, resolved for the dynamic-team lookup.</summary>
        public String OtherOffset { get => this.otherOffset; set => otherOffset = value; }
        private String otherOffset;

        /// <summary>Attaches to the game process and loads the full player roster for the given club.</summary>
        public PlayerController(ProcessMemory memory, Club club, bool isGog, PlayerEnums.AddressType type) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.PlayerAddress;
            this.UpdateBaseAddress(type);
            this.RefreshPlayerList(club);
        }

        /// <summary>Reloads the full player list for the given club and, for OWN/OPPONENT, refreshes the counterpart-roster offsets.</summary>
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

            if (this.Type == PlayerEnums.AddressType.OWN || this.Type == PlayerEnums.AddressType.OPPONENT)
            {
                this.InitOffsets(offset);
            }
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
            player.Condition   = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CONDITION]));
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
            player.Vulnerable           = this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.VULNERABLE])) != 0;
            player.RedCardBannedMatches = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.BANNED_MATCHES]));
            player.Doped                = this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.DOPED])) != 0;
            player.YellowCardsSeason    = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.YELLOW_CARDS]));
            #endregion

            #region Contract
            player.Salary           = this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SALARY]));
            player.ShowUpBonus      = this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SHOWUP]));
            player.GoalsBonus       = this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.GOALS_BONUS]));
            player.TransferFee      = this.memory.ReadUInt16(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.TRANSFER_FEE]));
            player.ContractDuration = (byte)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CONTRACT_DURATION]));
            player.ContractDetails  = (PlayerEnums.Contract)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CONTRACT_DETAILS]));
            player.YearsInClub      = (byte)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.YEARS_IN_CLUB]));
            player.Career           = (PlayerEnums.Career)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CAREER]));
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
            player.Jersey                = this.memory.ReadByte(GetAddress(this.memory, player, "3A"));
            player.RedCardsSeason        = (byte) this.memory.ReadByte(GetAddress(this.memory, player, "4B"));
            player.YellowRedCardsSeason  = (byte) this.memory.ReadByte(GetAddress(this.memory, player, "4C"));
            player.Goals                 = this.memory.ReadByte(GetAddress(this.memory, player, "56"));
            player.GoalsCupNational      = this.memory.ReadByte(GetAddress(this.memory, player, "57"));
            player.GoalsCupInternational = this.memory.ReadByte(GetAddress(this.memory, player, "58"));
            player.Goals1stLeague        = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, "5A"), 2), 0);
            player.Assists               = this.memory.ReadByte(GetAddress(this.memory, player, "6B"));
            player.JokerGoals            = this.memory.ReadByte(GetAddress(this.memory, player, "6C"));
            player.PenaltiesSeason       = this.memory.ReadByte(GetAddress(this.memory, player, "6F"));
            player.Penalties             = this.memory.ReadByte(GetAddress(this.memory, player, "70"));
            player.FreekicksSeason       = this.memory.ReadByte(GetAddress(this.memory, player, "71"));
            player.Freekicks             = this.memory.ReadByte(GetAddress(this.memory, player, "72"));
            player.AppearancesSeason     = this.memory.ReadByte(GetAddress(this.memory, player, "77"));
            player.JokerAppearances      = this.memory.ReadByte(GetAddress(this.memory, player, "78"));
            player.Appearances1stLeague  = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, "7A"), 2), 0);
            */
            #endregion

            // Overrides the fields above with their persistent, PlayerId-keyed values where known
            // (see WritePersistentLevel/Age/Position/Skills/Personality/Mood/Salary) - falls back to
            // whatever the display-cache reads above already set if the record can't be read. This
            // was the missing half of the picture: we were writing these fields persistently for
            // Jugendspieler, but still always DISPLAYING/using the transient, shared "Dynamische
            // Mannschaft" cache value - which for Jugendspieler can be stale or from the wrong
            // context (see HelpView.cs). That mismatch is why the trainer could show a different Age
            // than the game itself.
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
                : this.memory.ReadString(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.FIRSTNAME]), length: 9, stringEncoding: encoding);

            uint? lastnameAddress = namePool.ResolveLastnameAddress(player.NameRecordId);
            player.Lastname = lastnameAddress.HasValue
                ? this.memory.ReadStringAtAddress(lastnameAddress.Value, 32, encoding)
                : this.memory.ReadString(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.LASTNAME]), length: 15, stringEncoding: encoding);

            Logger.Debug($"ReadPersistentName: NameRecordId={player.NameRecordId} (Id={player.Id}) -> Firstname=\"{player.Firstname}\" (addr={(firstnameAddress.HasValue ? "0x" + firstnameAddress.Value.ToString("X") : "fallback")}), Lastname=\"{player.Lastname}\" (addr={(lastnameAddress.HasValue ? "0x" + lastnameAddress.Value.ToString("X") : "fallback")}).");
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
            uint baseAddress = recordResolver.GetFieldAddress(player.NameRecordId, 0);
            int? ageEncodingConstant = this.GetAgeEncodingConstant();

            const int mainChunkLength = 0x26;
            byte[] chunk = this.memory.ReadBytesAtAddress(baseAddress, mainChunkLength);
            if (chunk != null)
            {
                player.SkinColor = (PlayerEnums.SkinColor)chunk[Settings.PlayerRecordSkinColorOffset];
                player.HairColor = (PlayerEnums.HairColor)chunk[Settings.PlayerRecordSkinColorOffset + 1];
                if (ageEncodingConstant.HasValue)
                    player.Age = (byte)(ageEncodingConstant.Value - chunk[Settings.PlayerRecordAgeOffset]);
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

            byte[] contractChunk = this.memory.ReadBytesAtAddress(baseAddress + Settings.PlayerRecordSalaryOffset, 4);
            if (contractChunk != null)
            {
                player.Salary = BitConverter.ToUInt16(contractChunk, 0);
                player.ShowUpBonus = BitConverter.ToUInt16(contractChunk, 2);
            }
        }

        // Writes Firstname/Lastname to the permanent name pool in addition to the display cache
        // below, so a rename actually survives a save/reload instead of being reset to the old value
        // (see NamePoolResolver's header comment for why the display cache alone isn't enough).
        //
        // The pool is tightly packed with no padding between variable-length strings, so a
        // replacement with a DIFFERENT byte length than the current value would shift every later
        // name in the pool. We only write when the length matches exactly; otherwise we log and skip
        // the persistent write (still returning a warning the caller can show the user) - the display
        // cache write below still makes the change visible in-game until the next load, matching
        // docs/manual-rename-workaround.md.
        private List<string> WritePersistentName(Player player)
        {
            Encoding encoding = Encoding.GetEncoding("iso-8859-1");
            NamePoolResolver namePool = new NamePoolResolver(this.memory);
            List<string> warnings = new List<string>();

            string firstnameWarning = this.TryWritePersistentName(namePool.ResolveFirstnameAddress(player.NameRecordId), player.Firstname, encoding, "Vorname", player);
            if (firstnameWarning != null) warnings.Add(firstnameWarning);

            string lastnameWarning = this.TryWritePersistentName(namePool.ResolveLastnameAddress(player.NameRecordId), player.Lastname, encoding, "Nachname", player);
            if (lastnameWarning != null) warnings.Add(lastnameWarning);

            return warnings;
        }

        // Returns null on success, or a user-facing (German) warning describing why the permanent
        // write was skipped. Every skip is still logged via Logger.Warn for troubleshooting - the
        // returned string is a friendlier, player-identified version for the UI.
        private string TryWritePersistentName(uint? address, string newValue, Encoding encoding, string fieldLabel, Player player)
        {
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

            byte[] newBytes = encoding.GetBytes(newValue);
            if (currentLength != newBytes.Length)
            {
                Logger.Warn($"Skipping persistent {fieldLabel} write for player {player.NameRecordId}: length changed ({currentLength} -> {newBytes.Length} bytes) - would shift every later name in the pool. Use a same-length replacement for now.");
                return $"{playerLabel}: {fieldLabel}, Länge falsch (vorher: {currentLength}, nachher: {newBytes.Length}), nicht übernommen.";
            }

            byte[] toWrite = new byte[newBytes.Length + 1];
            Array.Copy(newBytes, toWrite, newBytes.Length);
            toWrite[newBytes.Length] = 0;
            this.memory.WriteBytesAtAddress(address.Value, toWrite);
            Logger.Debug($"Wrote persistent {fieldLabel} for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address.Value:X}.");
            return null;
        }

        // Writes Level ("Stärke") to the same persistent per-player record as the name pool anchor
        // (anstoss2.exe+516678 + PlayerId*0xC8, see PlayerRecordResolver), in addition to the
        // display-cache write above. For club/dynamic-team players the display cache alone already
        // persists correctly, so this is a harmless redundant write there - but for Jugendspieler,
        // whose display cache is a transient, shared "Dynamische Mannschaft" buffer the game
        // silently discards edits to (see HelpView.cs), this is the only write that actually
        // sticks. Confirmed live: changing this byte survived both a Tagesabschluss and a full
        // save-to-file + reload.
        private void WritePersistentLevel(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordLevelOffset);
            this.memory.WriteBytesAtAddress(address, new byte[] { player.Level });
            Logger.Debug($"Wrote persistent Level for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Level}.");
        }

        // Same rationale as WritePersistentLevel above, for Age. Unlike Level, the byte here isn't
        // the age itself - it counts DOWN as age goes up, relative to the CURRENT SAVEGAME's
        // in-game year (see GetAgeEncodingConstant/Settings.AgeReferenceYearOffset for why this
        // isn't a fixed constant). Skipped entirely if that year can't be read, rather than writing
        // using a guessed/stale constant.
        private void WritePersistentAge(Player player)
        {
            int? encodingConstant = this.GetAgeEncodingConstant();
            if (!encodingConstant.HasValue)
            {
                Logger.Warn($"Skipping persistent Age write for player NameRecordId={player.NameRecordId} (Id={player.Id}): couldn't read the savegame's current year at 0x{Settings.AgeReferenceYearOffset:X}.");
                return;
            }

            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordAgeOffset);
            byte encodedAge = (byte)(encodingConstant.Value - player.Age);
            this.memory.WriteBytesAtAddress(address, new byte[] { encodedAge });
            Logger.Debug($"Wrote persistent Age for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Age} (encoded 0x{encodedAge:X2}, constant={encodingConstant.Value}).");
        }

        // Age isn't stored directly in the per-player record - it's encoded relative to the
        // savegame's own in-game year, read live from anstoss2.exe+426662 (a flat, per-savegame
        // value - stable across restarts/reinstalls, but genuinely different between savegames, so
        // this must never be hardcoded). Returns null if that year can't be read right now.
        private int? GetAgeEncodingConstant()
        {
            byte[] yearBytes = this.memory.ReadBytesAtAddress(this.memory.ModuleBase + Settings.AgeReferenceYearOffset, 2);
            if (yearBytes == null)
                return null;

            int currentYear = BitConverter.ToUInt16(yearBytes, 0);
            return currentYear - Settings.AgeReferenceYearEpoch;
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

        // Robin found this one directly, isolated from the group above (+0x1E) - Form had been
        // assumed omitted like the other club-context fields (ClubId/ClubCountry), but it turns out
        // it isn't. Written on its own since it's 3 bytes past Happy (+0x1A/+0x1B end) with unknown
        // content in between that this shouldn't touch.
        private void WritePersistentForm(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordFormOffset);
            this.memory.WriteBytesAtAddress(address, new byte[] { player.Form });
            Logger.Debug($"Wrote persistent Form for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Form}.");
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

        // Same rationale as WritePersistentLevel above, for Salary/ShowUpBonus - contiguous UInt16
        // pair, written in one call.
        //
        // The remaining Contract-tab fields (GoalsBonus/TransferFee/ContractDuration/
        // ContractDetails/YearsInClub/Career, +0x88 onward) were tried here too - Robin confirmed
        // only Salary/ShowUpBonus actually work, the rest reverted back out (see Settings.cs/docs).
        private void WritePersistentSalary(Player player)
        {
            PlayerRecordResolver recordResolver = new PlayerRecordResolver(this.memory);
            uint address = recordResolver.GetFieldAddress(player.NameRecordId, Settings.PlayerRecordSalaryOffset);
            byte[] toWrite = new byte[4];
            Array.Copy(BitConverter.GetBytes(player.Salary), 0, toWrite, 0, 2);
            Array.Copy(BitConverter.GetBytes(player.ShowUpBonus), 0, toWrite, 2, 2);
            this.memory.WriteBytesAtAddress(address, toWrite);
            Logger.Debug($"Wrote persistent Salary/ShowUpBonus for player NameRecordId={player.NameRecordId} (Id={player.Id}) at 0x{address:X}: {player.Salary}/{player.ShowUpBonus}.");
        }

        // Returns one warning per Firstname/Lastname that couldn't be permanently saved (see
        // WritePersistentName) - empty if everything went through.
        //
        // The three persistent, PlayerId-keyed writes below (Name/Level/Age) always run, for every
        // player type - they don't depend on which context is currently loaded. Everything else
        // writes through the transient "display cache" address space instead, which for TRAINEE
        // (Jugendspieler) is a shared buffer the game itself dynamically refills on navigation (see
        // HelpView.cs): writing there can silently do nothing, or - if the buffer's since been
        // reused for a different context - land in the wrong slot ("unvorhersehbares Verhalten").
        // So for TRAINEE we skip that whole block and rely only on the persistent writes.
        /// <summary>Writes a player's persistent fields (name/level/age/etc.) and, outside TRAINEE context, its full display-cache fields too. Returns any name-save warnings to surface to the user.</summary>
        public List<string> Save(Player player)
        {
            List<string> nameWarnings = this.WritePersistentName(player);
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

            if (this.Type != PlayerEnums.AddressType.TRAINEE)
            {
                #region Overview
                this.memory.WriteMemory(GetAddress(this.memory, player,player.Addresses[PlayerEnums.AddressKey.FIRSTNAME]), "string", player.Firstname.PadRight(9, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));
                this.memory.WriteMemory(GetAddress(this.memory, player,player.Addresses[PlayerEnums.AddressKey.LASTNAME]), "string", player.Lastname.PadRight(15, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));

                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SKIN]), new byte[] { (byte) player.SkinColor });
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.HAIR]), new byte[] { (byte) player.HairColor });

                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.AGE]), new byte[] { player.Age });
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.LEVEL]), new byte[] { player.Level });
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.FORM]), new byte[] { player.Form });
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CONDITION]), new byte[] { player.Condition });
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.FRESHNESS]), new byte[] { player.Freshness });

                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.NATIONALITY]), new byte[] { (byte)player.Nationality });
                #endregion

                #region Positions
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.POSITION]), new byte[] { (byte)player.Position });
                if (player.SecondaryPositions.Count > 0)
                {
                    this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SECONDARY_1]), new byte[] { (byte)player.SecondaryPositions[0] });
                    if (player.SecondaryPositions.Count > 1)
                    {
                        this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SECONDARY_2]), new byte[] { (byte)player.SecondaryPositions[1] });
                    }
                }
                #endregion

                #region Skills
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SKILLS]), BitConverter.GetBytes((ushort)player.Skills));
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.NEG_SKILLS]), BitConverter.GetBytes((ushort)player.NegativeSkills));
                #endregion

                #region Character
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.PERSONALITY]), BitConverter.GetBytes((ushort)player.Personality));
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CHARACTER]), new byte[] { (byte)player.Character });
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.HEALTH]), new byte[] { (byte)player.Health });
                #endregion

                #region Constitution
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.UNHAPPY]), BitConverter.GetBytes((ushort)player.Unhappy));
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.HAPPY]), BitConverter.GetBytes((byte)player.Happy));

                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.INJURED_DAYS]), BitConverter.GetBytes(player.InjuredDays));
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.VULNERABLE]), BitConverter.GetBytes(player.Vulnerable));

                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.BANNED_MATCHES]), new byte[] { (byte)player.RedCardBannedMatches });
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.DOPED]), BitConverter.GetBytes(player.Doped));
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.YELLOW_CARDS]), new byte[] { (byte)player.YellowCardsSeason });
                #endregion

                #region Contract
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SALARY]), BitConverter.GetBytes(player.Salary));
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SHOWUP]), BitConverter.GetBytes(player.ShowUpBonus));
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.GOALS_BONUS]), BitConverter.GetBytes(player.GoalsBonus));
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.TRANSFER_FEE]), BitConverter.GetBytes(player.TransferFee));
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CONTRACT_DURATION]), new byte[] { (byte)player.ContractDuration });
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CONTRACT_DETAILS]), BitConverter.GetBytes((byte)player.ContractDetails));
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.YEARS_IN_CLUB]), new byte[] { (byte)player.YearsInClub });
                this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CAREER]), BitConverter.GetBytes((byte)player.Career));
                #endregion

                #region Other
                #endregion

                #region NotImplemented
                #endregion
            }

            return nameWarnings;
        }

        /// <summary>Writes only the given fields via the transient display-cache addresses (no persistent-record writes).</summary>
        public void Save (Player player, List<PlayerEnums.AddressKey> fieldsToSave)
        {
            foreach (var field in fieldsToSave) {
                // Get the memory address for this specific property dynamically
                string address = GetAddress(this.memory, player, player.Addresses[field]);

                switch (field) {
                    case PlayerEnums.AddressKey.CONDITION:
                        this.memory.WriteBytes(address, new byte[] { player.Condition });
                        break;

                    case PlayerEnums.AddressKey.FRESHNESS:
                        this.memory.WriteBytes(address, new byte[] { player.Freshness });
                        break;
                }
            }
        }

        private void InitOffsets(string offset)
        {
            this.OtherOffset = offset;

            PlayerEnums.AddressType counterpartType = this.Type == PlayerEnums.AddressType.OWN
                ? PlayerEnums.AddressType.OPPONENT
                : PlayerEnums.AddressType.OWN;

            ClubController counterpart = new ClubController(this.memory, this.isGog, counterpartType);

            int count = counterpart.Club.PlayerCount + counterpart.Club.AmateurPlayerCount;

            for (int i = 0; i < count; i++)
            {
                this.OtherOffset = Tools.SumHex(
                    new[] { this.OtherOffset, Settings.PlayerOffset }
                );
            }

            if (this.Type == PlayerEnums.AddressType.OWN)
            {
                this.OpponentOffset = offset;
                AddressPresets.InitOpponent(this.OpponentOffset);
            }

            AddressPresets.InitDynamicTeam(this.OtherOffset);
        }
    }
}
