using System;
using System.ComponentModel;

namespace A2G_Trainer_XP.Model
{
    public static class ClubEnums
    {
        public static string GetDescription(Enum value)
        {
            var f = value.GetType().GetField(value.ToString());
            var attributes = (DescriptionAttribute[])f.GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : value.ToString();
        }

        public enum Roof : ushort
        {
            None   = 0,
            BlockA = 1 <<  0,
            BlockB = 1 <<  1,
            BlockC = 1 <<  2,
            BlockD = 1 <<  3,
            BlockE = 1 <<  4,
            BlockF = 1 <<  5,
            BlockG = 1 <<  6,
            BlockH = 1 <<  7,
            BlockI = 1 <<  8,
            BlockJ = 1 <<  9,
            BlockK = 1 << 10,
            BlockL = 1 << 11
        }
    }
}
