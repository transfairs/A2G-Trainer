using System;
using System.Collections.Concurrent;
using System.Windows.Forms;

namespace A2G_Trainer_XP.Model
{
    /// <summary>Shared helpers for hex-address arithmetic and string handling used across the controllers.</summary>
    static class Tools
    {
        /// <summary>
        /// True when <paramref name="subject"/> differs from <paramref name="benchmark"/> in a way
        /// that matters given the field's byte-length limit (used to gate property setters so a
        /// value truncated by the length limit doesn't compare as different from itself).
        /// </summary>
        internal static bool LimitedStringEquals(string subject, string benchmark, int limitation)
        {
            return ((subject == null && benchmark != null) || (subject != benchmark && (subject.Length < limitation || subject.Substring(0, limitation-1) != benchmark)));
        }

        // Address tokens (Addresses-dictionary literals, entity.Offset, Settings constants) repeat
        // identically thousands of times per refresh; memoize the per-token parse instead of the sum.
        // Concurrent because SumHex is called from both the UI thread and ProcessController's
        // background Observe() thread.
        private static readonly ConcurrentDictionary<string, int> tokenCache = new ConcurrentDictionary<string, int>();

        /// <summary>Sums a list of signed hex offset strings (each optionally prefixed +/-) and returns the result as hex.</summary>
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

        private static int ParseToken(string hex) => tokenCache.GetOrAdd(hex, ParseTokenUncached);

        private static int ParseTokenUncached(string hex)
        {
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

            return Convert.ToInt32(trimmed, 16) * sign;
        }
        /// <summary>Capitalizes the first letter of an identifier (e.g. a field name to its property name).</summary>
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
