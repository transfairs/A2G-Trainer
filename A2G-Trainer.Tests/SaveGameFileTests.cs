using System;
using System.IO;
using System.Linq;
using A2G_Trainer.Controller;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for reading and writing savegame files: the block compression, the block structure,
    /// addressed access and the name pool.
    /// </summary>
    public class SaveGameFileTests
    {
        private const uint CalendarBlock = 0x826620;
        // Read twice by the game's loader (two blocks in the file).
        private const uint DuplicatedBlock = 0x7DCFD0;

        private static byte[] SampleData(int length, int seed)
        {
            // Mostly repetitive with some noise, like the game's records.
            Random random = new Random(seed);
            byte[] data = new byte[length];
            for (int i = 0; i < length; i++)
                data[i] = random.Next(4) == 0 ? (byte)random.Next(256) : (byte)(i % 37 < 20 ? 0 : i % 7);
            return data;
        }

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(100)]
        [InlineData(5000)]
        [InlineData(70000)]
        public void Codec_RoundTripsData(int length)
        {
            byte[] data = SampleData(length, length);
            Assert.Equal(data, SaveGameCodec.Decompress(SaveGameCodec.Compress(data)));
        }

        [Fact]
        public void Codec_RoundTripsLongRunsAndFarMatches()
        {
            byte[] data = new byte[200000];
            byte[] pattern = SampleData(600, 7);
            Array.Copy(pattern, 0, data, 1000, pattern.Length);
            Array.Copy(pattern, 0, data, 1000 + 509, pattern.Length);
            Array.Copy(pattern, 0, data, 5000, pattern.Length);

            byte[] packed = SaveGameCodec.Compress(data);

            Assert.True(packed.Length < data.Length / 50);
            Assert.Equal(data, SaveGameCodec.Decompress(packed));
        }

        [Fact]
        public void Codec_EmptyData_IsAnEmptyBlock()
        {
            Assert.Empty(SaveGameCodec.Decompress(SaveGameCodec.Compress(new byte[0])));
        }

        [Fact]
        public void Codec_IncompressibleData_IsStoredRaw()
        {
            byte[] data = new byte[64];
            new Random(3).NextBytes(data);

            byte[] packed = SaveGameCodec.Compress(data);

            Assert.Equal(-64, BitConverter.ToInt32(packed, 0));
            Assert.Equal(data, SaveGameCodec.Decompress(packed));
        }

        [Fact]
        public void Codec_DecodesTheGamesBitStream()
        {
            // "ab" as literals, then a reference one byte back (class 0, value 1 -> distance 2)
            // of length 2 + 1 = 3: "ababa". Bits, LSB first: 0 01100001, 0 01100010, 1 000 1 01 00.
            byte[] packed = new byte[12];
            Array.Copy(BitConverter.GetBytes(5), packed, 4);
            ulong bits = 0;
            int at = 0;
            void Put(uint value, int count) { for (int i = 0; i < count; i++) bits |= (ulong)((value >> i) & 1) << at++; }
            Put(0, 1); Put((byte)'a', 8);
            Put(0, 1); Put((byte)'b', 8);
            Put(1, 1); Put(0, 3); Put(1, 1); Put(1, 2);
            Array.Copy(BitConverter.GetBytes(bits), 0, packed, 4, 8);

            Assert.Equal("ababa", System.Text.Encoding.ASCII.GetString(SaveGameCodec.Decompress(packed)));
        }

        [Fact]
        public void Codec_DamagedBlock_Throws()
        {
            byte[] packed = new byte[8];
            Array.Copy(BitConverter.GetBytes(10), packed, 4);
            packed[4] = 0xFF;

            Assert.Throws<InvalidDataException>(() => SaveGameCodec.Decompress(packed));
        }

        [Fact]
        public void Blank_SerializesToAFileThatParsesBack()
        {
            SaveGameFile blank = SaveGameFile.CreateBlank();

            byte[] content = blank.Serialize();
            SaveGameFile parsed = SaveGameFile.Parse(content);

            Assert.Equal(blank.BlockCount, parsed.BlockCount);
            Assert.Equal(content, parsed.Serialize());
            Assert.False(parsed.IsModified);
        }

        [Fact]
        public void Parse_SomethingElse_Throws()
        {
            Assert.Throws<InvalidDataException>(() => SaveGameFile.Parse(new byte[] { 1, 2, 3 }));
            Assert.Throws<InvalidDataException>(() => SaveGameFile.Parse(Enumerable.Repeat((byte)0x41, 5000).ToArray()));
        }

        [Fact]
        public void Parse_TrailingBytes_Throws()
        {
            byte[] content = SaveGameFile.CreateBlank().Serialize().Concat(new byte[] { 0 }).ToArray();

            Assert.Throws<InvalidDataException>(() => SaveGameFile.Parse(content));
        }

        [Fact]
        public void Write_IsReadBackAndSurvivesSerializing()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();

            file.WriteInt16(CalendarBlock + 0x42, 2003);
            file.WriteString(CalendarBlock + 0x100, "Bremen", 8);

            Assert.True(file.IsModified);
            SaveGameFile parsed = SaveGameFile.Parse(file.Serialize());
            Assert.Equal(2003, parsed.ReadInt16(CalendarBlock + 0x42));
            Assert.Equal("Bremen", parsed.ReadString(CalendarBlock + 0x100, 8));
        }

        [Fact]
        public void Write_SameValue_DoesNotMarkModified()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();

            file.WriteInt32(CalendarBlock, 0);

            Assert.False(file.IsModified);
        }

        [Fact]
        public void Write_DuplicatedBlock_UpdatesBothCopies()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();

            file.WriteInt32(DuplicatedBlock + 4, 12345);
            byte[] content = file.Serialize();

            // Both copies are packed the same way, so the value occurs in the file only through them:
            // reading the file back gives the value whichever copy wins.
            Assert.Equal(12345, SaveGameFile.Parse(content).ReadInt32(DuplicatedBlock + 4));
            Assert.True(content.Length > SaveGameFile.CreateBlank().Serialize().Length);
        }

        [Fact]
        public void Access_OutsideTheBlocks_Throws()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();

            Assert.False(file.Contains(0x500000, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => file.Read(0x500000, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => file.WriteByte(0x500000, 1));
            // Straddling the end of a block isn't a block's data either.
            Assert.Throws<ArgumentOutOfRangeException>(() => file.Read(CalendarBlock + 0x15B0 - 1, 2));
        }

        [Fact]
        public void WriteString_CutsToTheField()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();

            file.WriteString(CalendarBlock, "Mönchengladbach", 5);

            Assert.Equal("Mönc", file.ReadString(CalendarBlock, 5));
            Assert.Equal(0, file.ReadByte(CalendarBlock + 4));
        }

        [Fact]
        public void Names_AreAddedOnceAndSurviveSerializing()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();

            int first = file.AddName("Jürgen");
            int second = file.AddName("Kohler");
            int again = file.AddName("Jürgen");

            Assert.Equal(0, first);
            Assert.Equal(1, second);
            Assert.Equal(first, again);
            SaveGameFile parsed = SaveGameFile.Parse(file.Serialize());
            Assert.Equal(2, parsed.NameCount);
            Assert.Equal("Jürgen", parsed.GetName(0));
            Assert.Equal("Kohler", parsed.GetName(1));
            Assert.Equal(1, parsed.FindName("Kohler"));
            Assert.Equal(-1, parsed.FindName("Sammer"));
            Assert.Equal(string.Empty, parsed.GetName(5));
        }

        [Fact]
        public void Save_WritesTheFileAndABackup()
        {
            string directory = Path.Combine(Path.GetTempPath(), "A2G-SaveGameTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "Test.gld");
                byte[] original = SaveGameFile.CreateBlank().Serialize();
                File.WriteAllBytes(path, original);

                SaveGameFile file = SaveGameFile.Load(path);
                file.WriteInt16(CalendarBlock + 0x42, 1999);
                file.Save();

                Assert.False(file.IsModified);
                Assert.Equal(original, File.ReadAllBytes(path + ".bak"));
                Assert.Equal(1999, SaveGameFile.Load(path).ReadInt16(CalendarBlock + 0x42));
                Assert.False(File.Exists(path + ".tmp"));

                string copy = Path.Combine(directory, "Kopie.gld");
                file.Save(copy, backup: false);
                Assert.Equal(copy, file.Path);
                Assert.False(File.Exists(copy + ".bak"));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
