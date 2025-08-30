using System;
using System.Collections.Generic;
using static A2G_Trainer_XP.Model.PlayerEnums;

namespace A2G_Trainer_XP.Model
{
    public sealed class PlayerAddresses
    {
        private readonly Dictionary<PlayerEnums.AddressKey, string> map;

        private PlayerAddresses(Dictionary<PlayerEnums.AddressKey, string> map)
        {
            this.map = new Dictionary<PlayerEnums.AddressKey, string>(map);
        }

        public string this[AddressKey key] { get { return this.map[key]; } }

        public static PlayerAddresses Create(params KeyValuePair<PlayerEnums.AddressKey, string>[] pairs)
        {
            var dict = new Dictionary<PlayerEnums.AddressKey, string>();
            foreach (var kv in pairs) dict[kv.Key] = kv.Value ?? "";
            return new PlayerAddresses(dict);
        }
        public static PlayerAddresses From(PlayerEnums.PlayerAddressType type)
        {
            switch(type)
            {
                case PlayerEnums.PlayerAddressType.OWN: return PlayerAddresses.OWN;
                case PlayerEnums.PlayerAddressType.OTHER: return PlayerAddresses.OTHER;
                default: throw new ArgumentOutOfRangeException("type");
            }
        }

        public static readonly PlayerAddresses OWN = PlayerAddresses.Create(
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.ID, "0"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.AGE, "1E"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.FIRSTNAME, "2"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.LASTNAME, "C"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.LEVEL, "1F"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.FORM, "35"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SKIN, "1C"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.HAIR, "1D"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.CONDITION, "30"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.FRESHNESS, "31"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.NATIONALITY, "38"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.POSITION, "20"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SECONDARY_1, "21"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SECONDARY_2, "22"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SKILLS, "28"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.NEG_SKILLS, "2A"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.PERSONALITY, "2C"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.CHARACTER, "2E"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.HEALTH, "2F"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.UNHAPPY, "34"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.HAPPY, "36"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.INJURED_DAYS, "44"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.INJURY, "46"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.VULNERABLE, "47"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.BANNED_MATCHES, "48"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.DOPED, "49"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.YELLOW_CARDS, "4A"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SALARY, "B4"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SHOWUP, "B6"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.GOALS_BONUS, "B8"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.TRANSFER_FEE, "BA"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.CONTRACT_DURATION, "C0"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.CONTRACT_DETAILS, "C1"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.YEARS_IN_CLUB, "C2"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.CAREER, "D7")
        );

        public static readonly PlayerAddresses OTHER = PlayerAddresses.Create(
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.ID, "5240"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.AGE, "525E"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.FIRSTNAME, "5242"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.LASTNAME, "524C"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.LEVEL, "525F"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.FORM, "5275"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SKIN, "525C"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.HAIR, "525D"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.CONDITION, "5270"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.FRESHNESS, "5271"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.NATIONALITY, "5278"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.POSITION, "5260"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SECONDARY_1, "5261"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SECONDARY_2, "5262"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SKILLS, "5268"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.NEG_SKILLS, "526A"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.PERSONALITY, "526C"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.CHARACTER, "526E"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.HEALTH, "526F"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.UNHAPPY, "5274"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.HAPPY, "5276"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.INJURED_DAYS, "5284"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.INJURY, "5286"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.VULNERABLE, "5287"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.BANNED_MATCHES, "5288"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.DOPED, "5289"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.YELLOW_CARDS, "528A"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SALARY, "52F4"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.SHOWUP, "52F6"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.GOALS_BONUS, "52F8"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.TRANSFER_FEE, "52FA"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.CONTRACT_DURATION, "5300"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.CONTRACT_DETAILS, "5301"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.YEARS_IN_CLUB, "5302"),
            new KeyValuePair<PlayerEnums.AddressKey, string>(PlayerEnums.AddressKey.CAREER, "5317")
        );
    }
}
