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

        public static FieldCondition MapToCondition(byte value)
        {
            if (value >= 30)
                return FieldCondition.SpielfeldFraglich;
            if (value >= 20)
                return FieldCondition.Kartoffelacker;
            if (value >= 10)
                return FieldCondition.Holprig;
            if (value >= 5)
                return FieldCondition.Mittelmaessig;
            if (value >= 2)
                return FieldCondition.Ordentlich;
            if (value == 1)
                return FieldCondition.Optimal;

            return FieldCondition.Clean;
        }

        public enum DisplayUnit : byte
        {
            [Description("keine Anzeigetafel")]
            None = 0,
            [Description("Ergebnisanzeige mit Tafeln")]
            ErgebnisAnzeige = 1,
            [Description("kleine LED-Anzeigetafel")]
            KleineLED = 2,
            [Description("große LED-Anzeigetafel")]
            GrosseLED = 3,
            Videowand = 4
        }

        public enum FieldCondition : byte
        {
            [Description("Perfekt")]
            Clean =  0,
            Optimal           =  1, // 1
            Ordentlich        =  3, // 2-4
            [Description("Mittelmäßig")]
            Mittelmaessig =  7, // 5-9
            Holprig           = 15, // 10-19
            Kartoffelacker    = 25, // 20-29
            [Description("Spielfeld ???")]
            SpielfeldFraglich = 30, // > 29
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
