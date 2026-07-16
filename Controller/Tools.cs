using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace A2G_Trainer_XP.Model
{
    static class Tools
    {
        internal static bool LimitedStringEquals(string subject, string benchmark, int limitation)
        {
            return ((subject == null && benchmark != null) || (subject != benchmark && (subject.Length < limitation || subject.Substring(0, limitation-1) != benchmark)));
        }

        // Address tokens (Addresses-dictionary literals, entity.Offset, Settings constants) repeat
        // identically thousands of times per refresh; memoize the per-token parse instead of the sum.
        private static readonly Dictionary<string, int> tokenCache = new Dictionary<string, int>();

        internal static string SumHex(string[] hexValues)
        {
            int output = 0;

            foreach (string hex in hexValues)
            {
                if (string.IsNullOrWhiteSpace(hex))
                    continue;

                output += ParseToken(hex);
            }

            return output.ToString("X");
        }

        private static int ParseToken(string hex)
        {
            int cached;
            if (tokenCache.TryGetValue(hex, out cached))
                return cached;

            string trimmed = hex.Trim();

            int sign = 1;

            if (trimmed.StartsWith("-"))
            {
                sign = -1;
                trimmed = trimmed.Substring(1);
            }
            else if (trimmed.StartsWith("+"))
            {
                trimmed = trimmed.Substring(1);
            }

            int value = Convert.ToInt32(trimmed, 16) * sign;
            tokenCache[hex] = value;
            return value;
        }
        internal static string ToMemberName(string name)
        {
            string output = name;
            if (!string.IsNullOrEmpty(name))
            {
                output = char.ToUpper(name[0]) + name.Substring(1);
            }
            return output;
        }
    }
}
