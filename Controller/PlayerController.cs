using A2G_Trainer_XP.Model;
using Memory;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace A2G_Trainer_XP.Controller
{
    public class PlayerController : EntityController<Player>
    {
        public String OpponentOffset { get => this.opponentOffset; set => opponentOffset = value; }
        private String opponentOffset;
        public String OtherOffset { get => this.otherOffset; set => otherOffset = value; }
        private String otherOffset;

        public PlayerController(Mem memory, Club club, bool isGog, PlayerEnums.AddressType type) : base(memory)
        {
            this.isGog = isGog;
            this.settings = Settings.PlayerAddress;
            this.UpdateBaseAddress(type);
            this.RefreshPlayerList(club);
        }

        internal void RefreshPlayerList(Club club)
        {
            this.EntityList = new BindingList<Player>();

            Console.WriteLine($"{club.PlayerCount} Players found.");

            string offset = string.Empty;
            for (int i=0; i < club.PlayerCount + club.AmateurPlayerCount; i++)
            {
                if (i > 0)
                {
                    offset = Tools.SumHex(new string[] { this.EntityList.Last().Offset, Settings.PlayerOffset.ToString() });
                }
                this.EntityList.Add(this.GetEntity(offset, this.Type));
            }

            if (this.Type == PlayerEnums.AddressType.OWN || this.Type == PlayerEnums.AddressType.OPPONENT)
            {
                this.InitOffsets(offset);
            }
        }

        internal void SaveEntityList()
        {
            foreach (Player p in this.EntityList)
            {
                this.Save(p);
            }
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
            player.Firstname   = this.memory.ReadString(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.FIRSTNAME]), length: 9, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
            player.Lastname    = this.memory.ReadString(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.LASTNAME]), length: 15, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
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
            player.Skills = (PlayerEnums.Skills)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SKILLS]), 2), 0);
            player.NegativeSkills = (PlayerEnums.Skills)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.NEG_SKILLS]), 2), 0);
            #endregion

            #region Character
            player.Personality = (PlayerEnums.Personality)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.PERSONALITY]), 2), 0);
            player.Character   = (PlayerEnums.Character)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.CHARACTER]));
            player.Health      = (PlayerEnums.Health)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.HEALTH]));
            #endregion

            #region Constitution
            player.Unhappy              = (PlayerEnums.Unhappy)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.UNHAPPY]), 2), 0);
            player.Happy                = (PlayerEnums.Happy)this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.HAPPY]));
            player.InjuredDays          = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.INJURED_DAYS]), 2), 0);
            player.Injury               = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.INJURY]));
            player.Vulnerable           = this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.VULNERABLE])) != 0;
            player.RedCardBannedMatches = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.BANNED_MATCHES]));
            player.Doped                = this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.DOPED])) != 0;
            player.YellowCardsSeason    = (byte) this.memory.ReadByte(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.YELLOW_CARDS]));
            #endregion

            #region Contract
            player.Salary           = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SALARY]), 2), 0);
            player.ShowUpBonus      = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.SHOWUP]), 2), 0);
            player.GoalsBonus       = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.GOALS_BONUS]), 2), 0);
            player.TransferFee      = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.TRANSFER_FEE]), 2), 0);
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

            return player;
        }

        public void Save(Player player)
        {
            Console.WriteLine($"Save: {player}");
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
            // this.memory.WriteBytes(GetAddress(this.memory, player, player.Addresses[PlayerEnums.AddressKey.INJURY]), BitConverter.GetBytes(player.Injury));
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
            /* Unknowm values */
            // 1, 8, 18, 27, 32, 33, 37, 39, 3B-43, 4D-55, 59, 5C - 6A, 6D, 6E, 73-76, 79
            #endregion

            #region NotImplemented
            /* Mainly statistical values with no benefit changing. */
            /*
            this.memory.WriteBytes(GetAddress(this.memory, player, "23"), BitConverter.GetBytes((ushort)player.TeamId));
            this.memory.WriteBytes(GetAddress(this.memory, player, "26"), new byte[] { (byte)player.PreviousForm });
            this.memory.WriteBytes(GetAddress(this.memory, player, "3A"), new byte[] { (byte)player.Jersey });
            this.memory.WriteBytes(GetAddress(this.memory, player, "4B"), new byte[] { (byte)player.RedCardsSeason });
            this.memory.WriteBytes(GetAddress(this.memory, player, "4C"), new byte[] { (byte)player.YellowRedCardsSeason });
            this.memory.WriteBytes(GetAddress(this.memory, player, "56"), new byte[] { (byte)player.Goals });
            this.memory.WriteBytes(GetAddress(this.memory, player, "57"), new byte[] { (byte)player.GoalsCupNational });
            this.memory.WriteBytes(GetAddress(this.memory, player, "58"), new byte[] { (byte)player.GoalsCupInternational });
            this.memory.WriteBytes(GetAddress(this.memory, player, "5A"), new byte[] { (byte)player.Goals1stLeague });
            this.memory.WriteBytes(GetAddress(this.memory, player, "6B"), new byte[] { (byte)player.Assists });
            this.memory.WriteBytes(GetAddress(this.memory, player, "6C"), new byte[] { (byte)player.JokerGoals });
            this.memory.WriteBytes(GetAddress(this.memory, player, "6F"), new byte[] { (byte)player.PenaltiesSeason });
            this.memory.WriteBytes(GetAddress(this.memory, player, "70"), new byte[] { (byte)player.Penalties });
            this.memory.WriteBytes(GetAddress(this.memory, player, "71"), new byte[] { (byte)player.FreekicksSeason });
            this.memory.WriteBytes(GetAddress(this.memory, player, "72"), new byte[] { (byte)player.Freekicks });
            this.memory.WriteBytes(GetAddress(this.memory, player, "77"), new byte[] { (byte)player.AppearancesSeason });
            this.memory.WriteBytes(GetAddress(this.memory, player, "78"), new byte[] { (byte)player.JokerAppearances });
            this.memory.WriteBytes(GetAddress(this.memory, player, "7A"), new byte[] { (byte)player.Appearances1stLeague });
            */
            #endregion
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
                    new[] { this.OtherOffset, Settings.PlayerOffset.ToString() }
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
