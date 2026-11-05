using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Design;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Windows.Forms.Design;

namespace A2G_Trainer.View
{
    /// <summary>The members of an enum with their [Description] texts, as the savegame editor offers them.</summary>
    internal static class EnumMembers
    {
        /// <summary>One named value of an enum.</summary>
        internal sealed class Member
        {
            public ulong Value;
            public string Text;
        }

        /// <summary>An enum value as raw bits (two's complement for negative values).</summary>
        internal static ulong Bits(object value)
        {
            Type underlying = Enum.GetUnderlyingType(value.GetType());
            bool signed = underlying == typeof(sbyte) || underlying == typeof(short) || underlying == typeof(int) || underlying == typeof(long);
            return signed ? unchecked((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)) : Convert.ToUInt64(value, CultureInfo.InvariantCulture);
        }

        /// <summary>The enum value of <paramref name="type"/> with the given raw bits.</summary>
        internal static object ToEnum(Type type, ulong bits) => Enum.ToObject(type, unchecked((long)bits));

        private static string TextOf(FieldInfo field)
        {
            DescriptionAttribute description = (DescriptionAttribute)field.GetCustomAttributes(typeof(DescriptionAttribute), false).FirstOrDefault();
            return description != null ? description.Description : field.Name;
        }

        /// <summary>All named values in declaration order.</summary>
        internal static List<Member> All(Type type) =>
            type.GetFields(BindingFlags.Public | BindingFlags.Static)
                .Select(f => new Member { Value = Bits(f.GetValue(null)), Text = TextOf(f) })
                .ToList();

        /// <summary>
        /// The single bits of a bitmask enum. Where two groups of names share the same bits (the
        /// goalkeeper and outfield skills), the group that fits <paramref name="instance"/> is used.
        /// </summary>
        internal static List<Member> Flags(Type type, object instance)
        {
            List<Member> bits = All(type).Where(m => m.Value != 0 && (m.Value & (m.Value - 1)) == 0).ToList();
            int repeat = bits.FindIndex(m => bits.TakeWhile(other => other != m).Any(other => other.Value == m.Value));
            if (repeat < 0)
                return bits;
            bool firstGroup = instance is SaveGamePlayer player && player.Position == PlayerEnums.Position.TO;
            return firstGroup ? bits.Take(repeat).ToList() : bits.Skip(repeat).ToList();
        }

        /// <summary>Text of a single value: its description, or the number for a value the enum doesn't name.</summary>
        internal static string Text(Type type, object value)
        {
            ulong number = Bits(value);
            Member member = All(type).FirstOrDefault(m => m.Value == number);
            return member != null ? member.Text : Convert.ToString(value, CultureInfo.CurrentCulture);
        }
    }

    /// <summary>Shows enum values by their [Description] and offers them in declaration order.</summary>
    public sealed class DescriptionEnumConverter : EnumConverter
    {
        /// <summary>Creates the converter for <paramref name="type"/>.</summary>
        public DescriptionEnumConverter(Type type) : base(type)
        {
        }

        /// <inheritdoc/>
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value != null && value.GetType() == this.EnumType)
                return EnumMembers.Text(this.EnumType, value);
            return base.ConvertTo(context, culture, value, destinationType);
        }

        /// <inheritdoc/>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string text)
            {
                EnumMembers.Member member = EnumMembers.All(this.EnumType).FirstOrDefault(m => m.Text == text);
                if (member != null)
                    return EnumMembers.ToEnum(this.EnumType, member.Value);
                if (ulong.TryParse(text, NumberStyles.Integer, culture ?? CultureInfo.CurrentCulture, out ulong number))
                    return Enum.ToObject(this.EnumType, number);
            }
            return base.ConvertFrom(context, culture, value);
        }

        /// <inheritdoc/>
        public override StandardValuesCollection GetStandardValues(ITypeDescriptorContext context) =>
            new StandardValuesCollection(EnumMembers.All(this.EnumType).Select(m => EnumMembers.ToEnum(this.EnumType, m.Value)).ToList());

        /// <inheritdoc/>
        public override bool GetStandardValuesExclusive(ITypeDescriptorContext context) => true;
    }

    /// <summary>Shows a bitmask enum as the list of its set bits; edited with <see cref="FlagsEnumEditor"/>.</summary>
    public sealed class FlagsEnumConverter : EnumConverter
    {
        /// <summary>Creates the converter for <paramref name="type"/>.</summary>
        public FlagsEnumConverter(Type type) : base(type)
        {
        }

        /// <inheritdoc/>
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType) => false;

        /// <inheritdoc/>
        public override bool GetStandardValuesSupported(ITypeDescriptorContext context) => false;

        /// <inheritdoc/>
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value != null && value.GetType() == this.EnumType)
            {
                ulong bits = EnumMembers.Bits(value);
                List<string> names = EnumMembers.Flags(this.EnumType, context?.Instance).Where(m => (bits & m.Value) != 0).Select(m => m.Text).ToList();
                return names.Count == 0 ? "keine" : string.Join(", ", names);
            }
            return base.ConvertTo(context, culture, value, destinationType);
        }
    }

    /// <summary>Edits a bitmask enum in the property grid with a drop-down list of check boxes.</summary>
    public sealed class FlagsEnumEditor : UITypeEditor
    {
        /// <inheritdoc/>
        public override UITypeEditorEditStyle GetEditStyle(ITypeDescriptorContext context) => UITypeEditorEditStyle.DropDown;

        /// <inheritdoc/>
        public override object EditValue(ITypeDescriptorContext context, IServiceProvider provider, object value)
        {
            IWindowsFormsEditorService service = provider?.GetService(typeof(IWindowsFormsEditorService)) as IWindowsFormsEditorService;
            if (service == null || value == null)
                return value;

            Type type = value.GetType();
            List<EnumMembers.Member> members = EnumMembers.Flags(type, context?.Instance);
            ulong bits = EnumMembers.Bits(value);
            using (CheckedListBox list = new CheckedListBox { CheckOnClick = true, BorderStyle = BorderStyle.None, IntegralHeight = false })
            {
                foreach (EnumMembers.Member member in members)
                    list.Items.Add(member.Text, (bits & member.Value) != 0);
                list.Height = Math.Min(members.Count, 16) * list.ItemHeight + 4;
                service.DropDownControl(list);

                ulong result = bits & ~members.Aggregate(0UL, (all, m) => all | m.Value);
                for (int i = 0; i < members.Count; i++)
                {
                    if (list.GetItemChecked(i))
                        result |= members[i].Value;
                }
                return EnumMembers.ToEnum(type, result);
            }
        }
    }

    /// <summary>Shows yes/no values as "Ja"/"Nein".</summary>
    public sealed class YesNoConverter : BooleanConverter
    {
        /// <inheritdoc/>
        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (destinationType == typeof(string) && value is bool flag)
                return flag ? "Ja" : "Nein";
            return base.ConvertTo(context, culture, value, destinationType);
        }

        /// <inheritdoc/>
        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            if (value is string text)
            {
                if (string.Equals(text.Trim(), "Ja", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (string.Equals(text.Trim(), "Nein", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return base.ConvertFrom(context, culture, value);
        }
    }

    /// <summary>
    /// A property-grid category that keeps the savegame editor's own order instead of the grid's
    /// alphabetical one: the grid sorts by the category text, so earlier categories get more
    /// leading tabs, which it doesn't draw.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SaveGameCategoryAttribute : CategoryAttribute
    {
        private static readonly string[] Order =
        {
            "Spielstand", "Allgemein", "Person", "Verein", "Fähigkeiten", "Verfassung", "Stimmung", "Vertrag", "Statistik",
            "Mannschaft", "Taktik", "Fans", "Trikots", "Finanzen", "Stadion", "Kompetenzen (aktuelle Stufe)", "Aktien"
        };

        /// <summary>Creates the category <paramref name="name"/>.</summary>
        public SaveGameCategoryAttribute(string name) : base(name)
        {
        }

        /// <inheritdoc/>
        protected override string GetLocalizedString(string value)
        {
            int index = Array.IndexOf(Order, value);
            return index < 0 ? value : new string('\t', Order.Length - index) + value;
        }
    }
}
