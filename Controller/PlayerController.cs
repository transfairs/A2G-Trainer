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
        public PlayerController(Mem memory, Club club, bool isGog) : base(memory)
        {
            this.baseAddress = isGog ? Settings.PlayerAddressGog : Settings.PlayerAddress;
            RefreshPlayerList(club);
        }

        internal void RefreshPlayerList(Club club)
        {
            this.EntityList = new BindingList<Player>();

            Console.WriteLine($"{club.PlayerCount} Players found.");

            for(int i=0; i < club.PlayerCount; i++)
            {
                string offset = string.Empty;
                if (i > 0)
                {
                    offset = Tools.SumHex(new string[] { this.EntityList.Last().Offset, Settings.PlayerOffset.ToString() });
                }
                this.EntityList.Add(this.GetEntity(offset));
            }
        }

        internal void SaveEntityList()
        {
            foreach (Player p in this.EntityList)
            {
                this.Save(p);
            }
        }

        internal override Player GetEntity(string offset)
        {
            Player player = new Player
            {
                Offset = offset
            };
            #region Overview
            player.Id          = (uint) this.memory.ReadByte(GetAddress(this.memory, player, ""));
            player.Firstname   = this.memory.ReadString(GetAddress(this.memory, player, "2"), length: 9, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
            player.Lastname    = this.memory.ReadString(GetAddress(this.memory, player, "C"), length: 15, stringEncoding: Encoding.GetEncoding("iso-8859-1"));
            player.SkinColor   = (PlayerEnums.SkinColor) this.memory.ReadByte(GetAddress(this.memory, player, "1C"));
            player.HairColor   = (PlayerEnums.HairColor) this.memory.ReadByte(GetAddress(this.memory, player, "1D"));
            player.Age         = (byte) this.memory.ReadByte(GetAddress(this.memory, player, "1E"));
            player.Level       = (byte) this.memory.ReadByte(GetAddress(this.memory, player, "1F"));
            player.Form        = (byte) this.memory.ReadByte(GetAddress(this.memory, player, "25"));
            player.Condition   = (byte) this.memory.ReadByte(GetAddress(this.memory, player, "30"));
            player.Freshness   = (byte) this.memory.ReadByte(GetAddress(this.memory, player, "31"));
            player.Nationality = (PlayerEnums.Country)this.memory.ReadByte(GetAddress(this.memory, player, "38"));
            #endregion

            #region Position
            player.Position           = (PlayerEnums.Position)this.memory.ReadByte(GetAddress(this.memory, player, "20"));
            player.SecondaryPositions = new List<PlayerEnums.Position>
            {
                (PlayerEnums.Position)this.memory.ReadByte(GetAddress(this.memory, player, "21")),
                (PlayerEnums.Position)this.memory.ReadByte(GetAddress(this.memory, player, "22"))
            };
            #endregion

            #region Skills
            player.Skills         = (PlayerEnums.Skills)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, "28"), 2), 0);
            player.NegativeSkills = (PlayerEnums.Skills)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, "2A"), 2), 0);
            #endregion

            #region Character
            player.Personality = (PlayerEnums.Personality)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, "2C"), 2), 0);
            player.Character   = (PlayerEnums.Character)this.memory.ReadByte(GetAddress(this.memory, player, "2E"));
            player.Health      = (PlayerEnums.Health)this.memory.ReadByte(GetAddress(this.memory, player, "2F"));
            #endregion

            #region Constitution
            player.Unhappy              = (PlayerEnums.Unhappy)BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, "34"), 2), 0);
            player.Happy                = (PlayerEnums.Happy)this.memory.ReadByte(GetAddress(this.memory, player, "36"));
            player.InjuredDays          = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, "44"), 2), 0);
            player.Injury               = (byte) this.memory.ReadByte(GetAddress(this.memory, player, "46"));
            player.Vulnerable           = this.memory.ReadByte(GetAddress(this.memory, player, "47")) != 0;
            player.RedCardBannedMatches = (byte) this.memory.ReadByte(GetAddress(this.memory, player, "48"));
            player.Doped                = this.memory.ReadByte(GetAddress(this.memory, player, "49")) != 0;
            player.YellowCardsSeason    = (byte) this.memory.ReadByte(GetAddress(this.memory, player, "4A"));
            #endregion

            #region Other
            /* Unknowm values */
            // 1, 8, 18, 27, 32, 33, 37, 39, 3B-43, 4D-55, 59, 5C - 6A, 6D, 6E, 73-76, 79
            #endregion

            #region NotImplemented
            /* Mainly statistical values with no benefit changing. */
            /*
            player.TeamId                = BitConverter.ToUInt16(this.memory.ReadBytes(GetAddress(this.memory, player, "23"), 2), 0);
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

            //Console.WriteLine($"{player.ToString()}: Unhappy: {player.Unhappy} | Happy: {player.Happy}");

            return player;
        }

        public void Save(Player player)
        {
            Console.WriteLine($"Save: {player}");
            #region Overview
            this.memory.WriteMemory(GetAddress(this.memory, player, "2"), "string", player.Firstname.PadRight(9, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));
            this.memory.WriteMemory(GetAddress(this.memory, player, "C"), "string", player.Lastname.PadRight(15, '\0'), stringEncoding: Encoding.GetEncoding("iso-8859-1"));

            this.memory.WriteBytes(GetAddress(this.memory, player, "1C"), new byte[] { (byte) player.SkinColor });
            this.memory.WriteBytes(GetAddress(this.memory, player, "1D"), new byte[] { (byte) player.HairColor });

            this.memory.WriteBytes(GetAddress(this.memory, player, "1E"), new byte[] { player.Age });
            this.memory.WriteBytes(GetAddress(this.memory, player, "1F"), new byte[] { player.Level });
            this.memory.WriteBytes(GetAddress(this.memory, player, "25"), new byte[] { player.Form });
            this.memory.WriteBytes(GetAddress(this.memory, player, "30"), new byte[] { player.Condition });
            this.memory.WriteBytes(GetAddress(this.memory, player, "31"), new byte[] { player.Freshness });

            this.memory.WriteBytes(GetAddress(this.memory, player, "38"), new byte[] { (byte)player.Nationality });
            #endregion

            #region Positions
            this.memory.WriteBytes(GetAddress(this.memory, player, "20"), new byte[] { (byte)player.Position });
            if (player.SecondaryPositions.Count > 0)
            {
                this.memory.WriteBytes(GetAddress(this.memory, player, "21"), new byte[] { (byte)player.SecondaryPositions[0] });
                if (player.SecondaryPositions.Count > 1)
                {
                    this.memory.WriteBytes(GetAddress(this.memory, player, "22"), new byte[] { (byte)player.SecondaryPositions[1] });
                }
            }
            #endregion

            #region Skills
            this.memory.WriteBytes(GetAddress(this.memory, player, "28"), BitConverter.GetBytes((ushort)player.Skills));
            this.memory.WriteBytes(GetAddress(this.memory, player, "2A"), BitConverter.GetBytes((ushort)player.NegativeSkills));
            #endregion

            #region Character
            this.memory.WriteBytes(GetAddress(this.memory, player, "2C"), BitConverter.GetBytes((ushort)player.Personality));
            this.memory.WriteBytes(GetAddress(this.memory, player, "2E"), new byte[] { (byte)player.Character });
            this.memory.WriteBytes(GetAddress(this.memory, player, "2F"), new byte[] { (byte)player.Health });
            #endregion

            #region Constitution
            this.memory.WriteBytes(GetAddress(this.memory, player, "34"), BitConverter.GetBytes((ushort)player.Unhappy));
            this.memory.WriteBytes(GetAddress(this.memory, player, "36"), BitConverter.GetBytes((ushort)player.Happy));

            this.memory.WriteBytes(GetAddress(this.memory, player, "44"), BitConverter.GetBytes(player.InjuredDays));
            this.memory.WriteBytes(GetAddress(this.memory, player, "47"), BitConverter.GetBytes(player.Vulnerable));

            this.memory.WriteBytes(GetAddress(this.memory, player, "48"), new byte[] { (byte)player.RedCardBannedMatches });
            this.memory.WriteBytes(GetAddress(this.memory, player, "49"), BitConverter.GetBytes(player.Doped));
            this.memory.WriteBytes(GetAddress(this.memory, player, "4A"), new byte[] { (byte)player.YellowCardsSeason });
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
    }
}
