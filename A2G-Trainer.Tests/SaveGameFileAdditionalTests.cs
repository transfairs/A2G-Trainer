using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Further tests for the savegame file: damaged blocks and files, the optional entries after
    /// the flag/count pair, raw blocks being written back, and a name pool that is full or that
    /// lacks its last terminator.
    /// </summary>
    public class SaveGameFileAdditionalTests
    {
        private const uint CalendarBlock = 0x826620;
        // A plain (unpacked) block of 0x50 bytes.
        private const uint RawBlock = 0x700C28;
        // The packed block right before the flag/count pair of FUN_005fd3b0.
        private const uint BlockBeforeFlag = 0x872100;

        private static object Field(object owner, string name) =>
            owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).GetValue(owner);

        private static void SetField(object owner, string name, object value) =>
            owner.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).SetValue(owner, value);

        // Replaces a block's data the way a changed block is written back.
        private static void ReplaceBlock(SaveGameFile file, string block, byte[] data)
        {
            object entry = Field(file, block);
            SetField(entry, "Data", data);
            SetField(entry, "Changed", true);
        }

        [Fact]
        public void Codec_TooShortBlocks_Throw()
        {
            Assert.Throws<InvalidDataException>(() => SaveGameCodec.Decompress(null));
            Assert.Throws<InvalidDataException>(() => SaveGameCodec.Decompress(new byte[3]));
            // Declared as 10 unpacked bytes, but only 2 follow.
            byte[] raw = BitConverter.GetBytes(-10).Concat(new byte[2]).ToArray();
            Assert.Throws<InvalidDataException>(() => SaveGameCodec.Decompress(raw));
            // A bit stream for 10 bytes that has no bits at all.
            Assert.Throws<InvalidDataException>(() => SaveGameCodec.Decompress(BitConverter.GetBytes(10)));
        }

        [Fact]
        public void Parse_BlockLengthBelowFour_Throws()
        {
            Exception error = Assert.Throws<InvalidDataException>(() => SaveGameFile.Parse(new byte[8]));
            Assert.Contains("kein Spielstand", error.Message);
        }

        [Fact]
        public void Parse_BlockOfTheWrongSize_Throws()
        {
            // The first block must hold 0x34 bytes; this one declares 5.
            byte[] content = BitConverter.GetBytes(8).Concat(BitConverter.GetBytes(5)).Concat(new byte[4]).ToArray();

            Assert.Throws<InvalidDataException>(() => SaveGameFile.Parse(content));
        }

        [Fact]
        public void Parse_TruncatedRawBlock_Throws()
        {
            byte[] content = SaveGameFile.CreateBlank().Serialize();
            // The file ends with a plain block; one byte short of it is not a savegame.
            byte[] truncated = content.Take(content.Length - 1).ToArray();

            Exception error = Assert.Throws<InvalidDataException>(() => SaveGameFile.Parse(truncated));
            Assert.Equal("Die Datei ist kein Spielstand von Anstoss 2 Gold.", error.Message);
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(250001)]
        public void Parse_DamagedNamePoolLength_Throws(int length)
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            ReplaceBlock(file, "nameLengthBlock", BitConverter.GetBytes(length));

            Exception error = Assert.Throws<InvalidDataException>(() => SaveGameFile.Parse(file.Serialize()));
            Assert.Contains("Namensbestand", error.Message);
        }

        [Fact]
        public void Parse_FlagSet_ReadsCountPlusOneEntries()
        {
            SaveGameFile blank = SaveGameFile.CreateBlank();
            byte[] content = blank.Serialize();
            // Offset of the flag: right after the block before it.
            int offset = 0;
            foreach (object block in (IList)Field(blank, "blocks"))
            {
                offset += (Field(block, "Kind").ToString() == "Packed" ? 4 : 0) + ((byte[])Field(block, "Stored")).Length;
                if ((uint)Field(block, "Address") == BlockBeforeFlag)
                    break;
            }
            Assert.Equal(new byte[4], content.Skip(offset).Take(4).ToArray());

            // Flag 1, count 1: two entries of 0xE10 bytes follow.
            List<byte> withEntries = content.Take(offset).ToList();
            withEntries.AddRange(new byte[] { 1, 0, 1, 0 });
            withEntries.AddRange(Enumerable.Repeat((byte)0x5A, 2 * 0xE10));
            withEntries.AddRange(content.Skip(offset + 4));
            byte[] changed = withEntries.ToArray();

            SaveGameFile parsed = SaveGameFile.Parse(changed);

            Assert.Equal(blank.BlockCount + 2, parsed.BlockCount);
            Assert.Equal(changed, parsed.Serialize());
        }

        [Fact]
        public void Serialize_ChangedRawBlock_IsWrittenPlain()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            int before = file.Serialize().Length;

            file.WriteInt32(RawBlock + 0x10, 0x11223344);
            byte[] content = file.Serialize();

            // A plain block keeps its size, and the bytes appear as they are.
            Assert.Equal(before, content.Length);
            Assert.True(Enumerable.Range(0, content.Length - 3).Any(i => BitConverter.ToInt32(content, i) == 0x11223344));
            Assert.Equal(0x11223344, SaveGameFile.Parse(content).ReadInt32(RawBlock + 0x10));
        }

        [Fact]
        public void ReadString_WithoutTerminator_TakesTheWholeField()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            file.Write(CalendarBlock, Encoding.ASCII.GetBytes("ABCD"));

            Assert.Equal("ABCD", file.ReadString(CalendarBlock, 4));
        }

        [Fact]
        public void WriteString_Null_ClearsTheField()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            file.WriteString(CalendarBlock, "Kiel", 5);

            file.WriteString(CalendarBlock, null, 5);

            Assert.Equal(string.Empty, file.ReadString(CalendarBlock, 5));
            Assert.Equal(new byte[5], file.Read(CalendarBlock, 5));
        }

        [Fact]
        public void GetName_NegativeIndex_IsEmpty()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            file.AddName("Basler");

            Assert.Equal(string.Empty, file.GetName(-1));
            Assert.Equal("Basler", file.GetName(0));
        }

        [Fact]
        public void NamePool_WithoutLastTerminator_EndsAtThePoolsEnd()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            file.AddName("Abc");
            ReplaceBlock(file, "namePoolBlock", Encoding.ASCII.GetBytes("Abc"));
            ReplaceBlock(file, "nameLengthBlock", BitConverter.GetBytes(3));

            SaveGameFile parsed = SaveGameFile.Parse(file.Serialize());

            Assert.Equal(1, parsed.NameCount);
            Assert.Equal("Abc", parsed.GetName(0));
            Assert.Equal(0, parsed.FindName("Abc"));
        }

        [Fact]
        public void AddName_FullTable_IsRefused()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            List<int> offsets = (List<int>)Field(file, "nameOffsets");
            offsets.AddRange(Enumerable.Repeat(0, Settings.NamePoolTableCapacity));

            Assert.Equal(-1, file.AddName("Neu"));
            Assert.False(file.IsModified);
            Assert.Equal(-1, file.FindName("Neu"));
        }

        [Fact]
        public void AddName_FullPool_IsRefused()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();

            Assert.Equal(-1, file.AddName(new string('x', Settings.NamePoolCapacity)));
            Assert.Equal(0, file.NameCount);
            Assert.Equal(0, file.AddName("Kurz"));
        }
    }
}
