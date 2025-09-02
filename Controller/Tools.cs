using System;
using System.Windows.Forms;

namespace A2G_Trainer_XP.Model
{
    static class Tools
    {
        internal static bool LimitedStringEquals(string subject, string benchmark, int limitation)
        {
            return ((subject == null && benchmark != null) || (subject != benchmark && (subject.Length < limitation || subject.Substring(0, limitation-1) != benchmark)));
        }
        internal static string SumHex(string[] hexValues)
        {
            int output = 0;
            /*
            foreach (string hex in hexValues)
            {
                if (string.IsNullOrEmpty(hex)) continue;
                output += Convert.ToInt32(hex, 16);
            }
            */
            foreach (string hex in hexValues)
            {
                if (string.IsNullOrWhiteSpace(hex))
                    continue;

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
                output += value;
            }

            return output.ToString("X");
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
