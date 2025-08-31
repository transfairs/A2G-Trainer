using System;
using System.Linq;
using System.Collections.Generic;

namespace A2G_Trainer_XP.Model
{
    public sealed class Addresses
    {
        private readonly Dictionary<Enum, string> map;

        private Addresses(Dictionary<Enum, string> map)
        {
            this.map = new Dictionary<Enum, string>(map);
        }

        public string this[Enum key] { get { return this.map[key]; } }

        public static Addresses Create(params KeyValuePair<Enum, string>[] pairs)
        {
            Dictionary<Enum, string> dict = new Dictionary<Enum, string>();
            foreach (KeyValuePair<Enum, string> kv in pairs) dict[kv.Key] = kv.Value ?? "";
            return new Addresses(dict);
        }
        public Addresses WithOffset(string offset)
        {
            Dictionary<Enum, string> transformed = this.map.ToDictionary(
                kv => kv.Key,
                kv => Tools.SumHex(new String[] { kv.Value, offset})
            );

            return new Addresses(transformed);
        }
    }
}
