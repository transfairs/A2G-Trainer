using System;
using System.Collections.Generic;
using System.Text;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// The pre-match speeches (Mannschaftssitzung) as the game's speech table has them, and the short
    /// text the trainer puts in front of each in the game's list: its effect and the uses left.
    /// </summary>
    /// <remarks>
    /// The game checks the speech as soon as the dialog is confirmed: a speech without uses left this
    /// season, or one that doesn't fit, does nothing at all - but the use is gone either way. One that
    /// fits (and, from 28 on, passes a roll against the manager's motivation skill) raises defence and
    /// attack by its percentages, may set the hardness, and sets the team's overall factor back to 100,
    /// lifting any overconfidence (checked live 16.11.2026).
    /// </remarks>
    internal static class PreMatchSpeeches
    {
        /// <summary>Entries of the game's speech table (0 is empty, 1..43 are the speeches).</summary>
        internal const int Count = 44;
        /// <summary>Size of a table entry.</summary>
        internal const int Stride = 0x14;
        /// <summary>A maximum of this many uses means "any number".</summary>
        internal const byte Unlimited = 99;
        /// <summary>Hardness byte meaning "unchanged".</summary>
        internal const byte NoHardness = 99;
        /// <summary>From this speech on, a fitting speech also needs a roll against the manager's motivation skill.</summary>
        internal const int FirstMotivationSpeech = 28;

        /// <summary>
        /// The chance in percent that a fitting motivation speech works: the game rolls 0..99 and the
        /// roll must not exceed skill * 6 + 10 (0x4C4A19).
        /// </summary>
        internal static int MotivationChance(int skill) => Math.Max(0, Math.Min(100, skill * 6 + 11));

        /// <summary>One speech as the game's table has it (FUN_004c4bd0 applies it).</summary>
        internal sealed class Speech
        {
            /// <summary>Number 1..43.</summary>
            public int Number { get; set; }
            /// <summary>Text ID in RESDLL.dll (6284..6326).</summary>
            public int TextId { get; set; }
            /// <summary>Change of the team's defence in percent (+0x0C).</summary>
            public sbyte Defence { get; set; }
            /// <summary>Change of the team's attack in percent (+0x0D).</summary>
            public sbyte Attack { get; set; }
            /// <summary>Hardness the team plays with, or <see cref="NoHardness"/> (+0x0E).</summary>
            public byte Hardness { get; set; }
            /// <summary>Uses per season, or <see cref="Unlimited"/> (+0x0F).</summary>
            public byte MaxUses { get; set; }
            /// <summary>Bit 0: only while not behind; bit 15: until the first defeat (+0x10).</summary>
            public uint EffectFlags { get; set; }
        }

        /// <summary>Reads the speeches from the game's table bytes (<see cref="Count"/> x <see cref="Stride"/>).</summary>
        internal static List<Speech> Parse(byte[] table)
        {
            List<Speech> speeches = new List<Speech>();
            for (int number = 1; number < Count && (number + 1) * Stride <= table.Length; number++)
            {
                int at = number * Stride;
                speeches.Add(new Speech
                {
                    Number = number,
                    TextId = BitConverter.ToInt32(table, at + 8),
                    Defence = unchecked((sbyte)table[at + 0xC]),
                    Attack = unchecked((sbyte)table[at + 0xD]),
                    Hardness = table[at + 0xE],
                    MaxUses = table[at + 0xF],
                    EffectFlags = BitConverter.ToUInt32(table, at + 0x10),
                });
            }
            return speeches;
        }

        /// <summary>
        /// The text put in front of the speech in the game's list: its effect and, if limited, the uses
        /// left, e.g. "+3% 2x ", "-10/+10 ", "+0% sanft 1x "; Latin-1, as the game shows it. Whether it
        /// fits is shown by the colour.
        /// </summary>
        internal static string Prefix(Speech speech, short usesLeft)
        {
            StringBuilder text = new StringBuilder(speech.Defence == speech.Attack
                ? Percent(speech.Defence)
                : $"{Signed(speech.Defence)}/{Signed(speech.Attack)}");
            if (speech.Hardness != NoHardness)
                text.Append(' ').Append(speech.Hardness < Normal ? "hart" : speech.Hardness > Normal ? "sanft" : "normal");
            if (speech.MaxUses != Unlimited)
                text.Append(' ').Append(Math.Max((short)0, usesLeft)).Append('x');
            return text.Append(' ').ToString();
        }

        // The hardness byte of normal play (LiveMatch.HardnessNames).
        private const byte Normal = 2;

        private static string Signed(sbyte value) => value.ToString("+0;-0;0");

        private static string Percent(sbyte value) => Signed(value) + "%";
    }
}
