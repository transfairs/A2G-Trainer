using System;
using System.Linq;
using System.Collections.Generic;

namespace A2G_Trainer_XP.Model
{
    /// <summary>
    /// Immutable map from an entity's field-key enum to its hex memory offset string.
    /// Instances are built once from a preset and can be re-based to a new instance's
    /// slot via <see cref="WithOffset"/> instead of hand-recomputing every field.
    /// </summary>
    public sealed class Addresses
    {
        private readonly Dictionary<Enum, string> map;

        private Addresses(Dictionary<Enum, string> map)
        {
            this.map = new Dictionary<Enum, string>(map);
        }

        /// <summary>Hex offset string for the given field key.</summary>
        public string this[Enum key] { get { return this.map[key]; } }

        /// <summary>Builds an <see cref="Addresses"/> map from key/offset pairs.</summary>
        public static Addresses Create(params KeyValuePair<Enum, string>[] pairs)
        {
            Dictionary<Enum, string> dict = new Dictionary<Enum, string>();
            foreach (KeyValuePair<Enum, string> kv in pairs) dict[kv.Key] = kv.Value ?? "";
            return new Addresses(dict);
        }
        /// <summary>
        /// Returns a copy with every offset shifted by <paramref name="offset"/> (hex-added),
        /// used to re-base a shared preset onto a specific slot/instance's base address.
        /// </summary>
        public Addresses WithOffset(string offset)
        {
            Dictionary<Enum, string> transformed = this.map.ToDictionary(
                kv => kv.Key,
                kv => Tools.SumHex(new String[] { kv.Value, offset})
            );

            return new Addresses(transformed);
        }

        /// <summary>Whether an offset is defined for the given field key.</summary>
        public bool ContainsKey(Enum key)
        {
            return this.map.ContainsKey(key);
        }
    }
}
