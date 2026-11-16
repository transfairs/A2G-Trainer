using System;
using System.Linq;
using A2G_Trainer.Controller;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for PreMatchSpeeches (reading the game's speech table, the text in front of each speech)
    /// and for the X86Assembler the stubs are built with.
    /// </summary>
    public class PreMatchSpeechesTests
    {
        private static byte[] Entry(sbyte defence, sbyte attack, byte hardness, byte maxUses, int textId = 6290, uint flags = 0)
        {
            byte[] entry = new byte[PreMatchSpeeches.Stride];
            BitConverter.GetBytes(textId).CopyTo(entry, 8);
            entry[0xC] = unchecked((byte)defence);
            entry[0xD] = unchecked((byte)attack);
            entry[0xE] = hardness;
            entry[0xF] = maxUses;
            BitConverter.GetBytes(flags).CopyTo(entry, 0x10);
            return entry;
        }

        [Fact]
        public void Parse_ReadsSpeeches1To43()
        {
            byte[] table = new byte[PreMatchSpeeches.Count * PreMatchSpeeches.Stride];
            Entry(-10, 10, 4, 2, 6295, 0x8001).CopyTo(table, 7 * PreMatchSpeeches.Stride);

            var speeches = PreMatchSpeeches.Parse(table);

            Assert.Equal(Enumerable.Range(1, 43), speeches.Select(s => s.Number));
            PreMatchSpeeches.Speech seven = speeches[6];
            Assert.Equal(6295, seven.TextId);
            Assert.Equal(-10, seven.Defence);
            Assert.Equal(10, seven.Attack);
            Assert.Equal(4, seven.Hardness);
            Assert.Equal(2, seven.MaxUses);
            Assert.Equal(0x8001u, seven.EffectFlags);
        }

        [Fact]
        public void Parse_OfAShortTable_StopsAtItsEnd()
        {
            Assert.Equal(2, PreMatchSpeeches.Parse(new byte[3 * PreMatchSpeeches.Stride]).Count);
        }

        [Theory]
        [InlineData(3, 3, PreMatchSpeeches.NoHardness, 2, 1, "+3% 1x ")]
        [InlineData(-10, 10, PreMatchSpeeches.NoHardness, PreMatchSpeeches.Unlimited, 5, "-10/+10 ")]
        [InlineData(0, 0, 4, 1, 0, "0% sanft 0x ")]
        [InlineData(2, 0, 0, 1, 1, "+2/0 hart 1x ")]
        [InlineData(1, 1, 2, PreMatchSpeeches.Unlimited, 0, "+1% normal ")]
        [InlineData(1, 1, PreMatchSpeeches.NoHardness, 3, -1, "+1% 0x ")]
        public void Prefix_ShowsEffectHardnessAndUsesLeft(int defence, int attack, byte hardness, byte maxUses, short usesLeft, string prefix)
        {
            byte[] table = new byte[2 * PreMatchSpeeches.Stride];
            Entry((sbyte)defence, (sbyte)attack, hardness, maxUses).CopyTo(table, PreMatchSpeeches.Stride);

            Assert.Equal(prefix, PreMatchSpeeches.Prefix(PreMatchSpeeches.Parse(table)[0], usesLeft));
        }

        [Fact]
        public void X86Assembler_ResolvesShortAndNearJumpsBothWays()
        {
            X86Assembler a = new X86Assembler();
            a.Mark("start");
            a.Jump(0xEB, "end");                    // 0: EB rel8
            a.JumpNear("start", 0x0F, 0x84);        // 2: 0F 84 rel32
            a.PadTo(0x10);
            a.Mark("end");
            Assert.Equal(0x10, a.Position);
            a.JumpNear("start", 0xE9);              // 0x10: E9 rel32

            byte[] code = a.ToArray();

            Assert.Equal(new byte[] { 0xEB, 0x0E }, code.Take(2));
            Assert.Equal(new byte[] { 0x0F, 0x84 }, code.Skip(2).Take(2));
            Assert.Equal(-8, BitConverter.ToInt32(code, 4));
            Assert.All(code.Skip(8).Take(8), b => Assert.Equal(0xCC, b));
            Assert.Equal(0xE9, code[0x10]);
            Assert.Equal(-0x15, BitConverter.ToInt32(code, 0x11));
        }

        [Fact]
        public void X86Assembler_ShortJumpOutOfReach_Throws()
        {
            X86Assembler a = new X86Assembler();
            a.Jump(0xEB, "far");
            a.PadTo(0x100);
            a.Mark("far");

            Assert.Throws<InvalidOperationException>(() => a.ToArray());
        }

        [Fact]
        public void X86Assembler_PadToBehindTheCode_Throws()
        {
            X86Assembler a = new X86Assembler();
            a.Emit(0x90, 0x90);

            Assert.Throws<InvalidOperationException>(() => a.PadTo(1));
        }
    }
}
