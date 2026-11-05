using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// A savegame file (*.gld) of Anstoss 2 Gold, read into memory and written back the way the
    /// game does it. The file is a sequence of blocks the game's loader (FUN_005a61f0) unpacks to
    /// fixed addresses and its writer (FUN_005ef380) packs from there, so every block is kept with
    /// the address it belongs to: <see cref="Read"/>/<see cref="Write"/> take the original build's
    /// addresses (0x400000 + the module offsets in <see cref="Settings"/>), whichever build wrote
    /// the file - the file format is the same for all of them.
    /// </summary>
    /// <remarks>
    /// Packed blocks are [Int32 length][data, see <see cref="SaveGameCodec"/>]; a few small blocks
    /// are stored as plain bytes (FUN_00657170/FUN_00657590). Blocks nobody changed are written
    /// back byte for byte as they were read. The name pool is a block of its own (count, byte
    /// length, then the NUL-terminated names), see <see cref="GetName"/>/<see cref="AddName"/>.
    /// </remarks>
    internal sealed class SaveGameFile
    {
        /// <summary>Address the original build's module is loaded at; the <see cref="Settings"/> offsets are relative to it.</summary>
        public const uint ModuleBase = 0x400000;

        // The name pool the game allocates (FUN_005af480) and its pointer table (see Settings.NamePoolCapacity).
        private static readonly int NamePoolCapacity = Settings.NamePoolCapacity;
        private static readonly int NameTableCapacity = Settings.NamePoolTableCapacity;

        private enum BlockKind { Packed, Raw }

        private sealed class Block
        {
            public BlockKind Kind;
            public uint Address;
            public byte[] Data;
            public byte[] Stored;
            public bool Changed;
        }

        private readonly List<Block> blocks = new List<Block>();
        private Block nameCountBlock;
        private Block nameLengthBlock;
        private Block namePoolBlock;
        private readonly List<int> nameOffsets = new List<int>();
        private readonly Dictionary<string, int> nameIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        private SaveGameFile()
        {
        }

        /// <summary>Path the file was read from (and is saved to by default).</summary>
        public string Path { get; private set; }

        /// <summary>True once anything has been changed since the file was read or last saved.</summary>
        public bool IsModified { get; private set; }

        /// <summary>Number of blocks in the file.</summary>
        public int BlockCount => this.blocks.Count;

        /// <summary>Reads a savegame file; throws <see cref="InvalidDataException"/> if it isn't one of Anstoss 2 Gold.</summary>
        public static SaveGameFile Load(string path)
        {
            SaveGameFile file = Parse(File.ReadAllBytes(path));
            file.Path = path;
            return file;
        }

        /// <summary>Reads a savegame from its bytes; throws <see cref="InvalidDataException"/> if it isn't one of Anstoss 2 Gold.</summary>
        public static SaveGameFile Parse(byte[] content)
        {
            SaveGameFile file = new SaveGameFile();
            new Parser(file, content).Run();
            file.IndexNames();
            return file;
        }

        /// <summary>A savegame with every block present and zero-filled (and an empty name pool), e.g. for tests.</summary>
        internal static SaveGameFile CreateBlank()
        {
            SaveGameFile file = new SaveGameFile();
            new Parser(file, null).Run();
            file.IndexNames();
            return file;
        }

        #region Addressed access
        /// <summary>True if the file holds <paramref name="length"/> bytes at the original build's <paramref name="address"/>.</summary>
        public bool Contains(uint address, int length) => this.FindBlock(address, length) != null;

        /// <summary>Reads bytes at an original-build address; throws if the file has no block there.</summary>
        public byte[] Read(uint address, int length)
        {
            Block block = this.FindBlock(address, length) ?? throw new ArgumentOutOfRangeException(nameof(address), $"Der Spielstand enthält keine Daten bei 0x{address:X} ({length} Byte).");
            byte[] result = new byte[length];
            Array.Copy(block.Data, address - block.Address, result, 0, length);
            return result;
        }

        /// <summary>Writes bytes at an original-build address into every block holding it; throws if the file has no block there.</summary>
        public void Write(uint address, byte[] data)
        {
            bool written = false;
            foreach (Block block in this.blocks)
            {
                if (!Covers(block, address, data.Length))
                    continue;
                int offset = (int)(address - block.Address);
                bool same = true;
                for (int i = 0; i < data.Length && same; i++)
                    same = block.Data[offset + i] == data[i];
                written = true;
                if (same)
                    continue;
                Array.Copy(data, 0, block.Data, offset, data.Length);
                block.Changed = true;
                this.IsModified = true;
            }
            if (!written)
                throw new ArgumentOutOfRangeException(nameof(address), $"Der Spielstand enthält keine Daten bei 0x{address:X} ({data.Length} Byte).");
        }

        /// <summary>Reads a byte at an original-build address.</summary>
        public byte ReadByte(uint address) => this.Read(address, 1)[0];
        /// <summary>Reads an Int16 at an original-build address.</summary>
        public short ReadInt16(uint address) => BitConverter.ToInt16(this.Read(address, 2), 0);
        /// <summary>Reads a UInt16 at an original-build address.</summary>
        public ushort ReadUInt16(uint address) => BitConverter.ToUInt16(this.Read(address, 2), 0);
        /// <summary>Reads an Int32 at an original-build address.</summary>
        public int ReadInt32(uint address) => BitConverter.ToInt32(this.Read(address, 4), 0);
        /// <summary>Reads a UInt32 at an original-build address.</summary>
        public uint ReadUInt32(uint address) => BitConverter.ToUInt32(this.Read(address, 4), 0);

        /// <summary>Reads a NUL-terminated Latin-1 text of at most <paramref name="length"/> bytes.</summary>
        public string ReadString(uint address, int length)
        {
            byte[] bytes = this.Read(address, length);
            int end = Array.IndexOf(bytes, (byte)0);
            return Latin1.GetString(bytes, 0, end < 0 ? length : end);
        }

        /// <summary>Writes a byte.</summary>
        public void WriteByte(uint address, byte value) => this.Write(address, new[] { value });
        /// <summary>Writes an Int16.</summary>
        public void WriteInt16(uint address, short value) => this.Write(address, BitConverter.GetBytes(value));
        /// <summary>Writes a UInt16.</summary>
        public void WriteUInt16(uint address, ushort value) => this.Write(address, BitConverter.GetBytes(value));
        /// <summary>Writes an Int32.</summary>
        public void WriteInt32(uint address, int value) => this.Write(address, BitConverter.GetBytes(value));
        /// <summary>Writes a UInt32.</summary>
        public void WriteUInt32(uint address, uint value) => this.Write(address, BitConverter.GetBytes(value));

        /// <summary>
        /// Writes a Latin-1 text into a field of <paramref name="fieldLength"/> bytes, cut to leave
        /// room for the terminating NUL and padded with NULs.
        /// </summary>
        public void WriteString(uint address, string value, int fieldLength)
        {
            byte[] field = new byte[fieldLength];
            byte[] text = Latin1.GetBytes(value ?? string.Empty);
            Array.Copy(text, field, Math.Min(text.Length, fieldLength - 1));
            this.Write(address, field);
        }

        // The last block holding the address wins, as in the game: one block (0x7DCFD0) is read twice.
        private Block FindBlock(uint address, int length)
        {
            for (int i = this.blocks.Count - 1; i >= 0; i--)
            {
                if (Covers(this.blocks[i], address, length))
                    return this.blocks[i];
            }
            return null;
        }

        private static bool Covers(Block block, uint address, int length) =>
            block.Address != 0 && address >= block.Address && (long)address + length <= (long)block.Address + block.Data.Length;
        #endregion

        #region Name pool
        /// <summary>Number of names in the pool.</summary>
        public int NameCount => this.nameOffsets.Count;

        /// <summary>The name with the given pool index, or "" if there is none.</summary>
        public string GetName(int index)
        {
            if (index < 0 || index >= this.nameOffsets.Count)
                return string.Empty;
            byte[] pool = this.namePoolBlock.Data;
            int start = this.nameOffsets[index];
            int end = Array.IndexOf(pool, (byte)0, start);
            return Latin1.GetString(pool, start, (end < 0 ? pool.Length : end) - start);
        }

        /// <summary>Pool index of the first name equal to <paramref name="name"/>, or -1.</summary>
        public int FindName(string name) => this.nameIndex.TryGetValue(name, out int index) ? index : -1;

        /// <summary>
        /// Index of <paramref name="name"/> in the pool, adding it at the end the way the game adds
        /// names (FUN_005b3380) if it isn't there yet; -1 if the pool is full.
        /// </summary>
        public int AddName(string name)
        {
            int existing = this.FindName(name);
            if (existing >= 0)
                return existing;

            byte[] text = Latin1.GetBytes(name);
            byte[] pool = this.namePoolBlock.Data;
            if (this.nameOffsets.Count >= NameTableCapacity || this.nameOffsets.Count >= ushort.MaxValue || pool.Length + text.Length + 1 > NamePoolCapacity)
                return -1;

            byte[] grown = new byte[pool.Length + text.Length + 1];
            Array.Copy(pool, grown, pool.Length);
            Array.Copy(text, 0, grown, pool.Length, text.Length);
            this.namePoolBlock.Data = grown;
            this.namePoolBlock.Changed = true;

            int index = this.nameOffsets.Count;
            this.nameOffsets.Add(pool.Length);
            this.nameIndex[name] = index;
            this.SetBlockData(this.nameCountBlock, BitConverter.GetBytes(this.nameOffsets.Count));
            this.SetBlockData(this.nameLengthBlock, BitConverter.GetBytes(grown.Length));
            this.IsModified = true;
            return index;
        }

        private void SetBlockData(Block block, byte[] data)
        {
            block.Data = data;
            block.Changed = true;
        }

        private void IndexNames()
        {
            int count = BitConverter.ToInt32(this.nameCountBlock.Data, 0);
            byte[] pool = this.namePoolBlock.Data;
            int position = 0;
            for (int i = 0; i < count && position < pool.Length; i++)
            {
                this.nameOffsets.Add(position);
                int end = Array.IndexOf(pool, (byte)0, position);
                if (end < 0)
                    end = pool.Length;
                string name = Latin1.GetString(pool, position, end - position);
                if (!this.nameIndex.ContainsKey(name))
                    this.nameIndex[name] = i;
                position = end + 1;
            }
        }
        #endregion

        #region Writing
        /// <summary>The file's bytes with every change applied.</summary>
        public byte[] Serialize()
        {
            using (MemoryStream stream = new MemoryStream())
            {
                foreach (Block block in this.blocks)
                {
                    if (block.Changed)
                    {
                        block.Stored = block.Kind == BlockKind.Packed ? SaveGameCodec.Compress(block.Data) : (byte[])block.Data.Clone();
                        block.Changed = false;
                    }
                    if (block.Kind == BlockKind.Packed)
                        stream.Write(BitConverter.GetBytes(block.Stored.Length), 0, 4);
                    stream.Write(block.Stored, 0, block.Stored.Length);
                }
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Writes the savegame to <paramref name="path"/> (default: where it was read from). An
        /// existing file is first copied to "&lt;name&gt;.bak" when <paramref name="backup"/> is set;
        /// the new content goes to a temporary file first, so a failed write leaves the old one intact.
        /// </summary>
        public void Save(string path = null, bool backup = true)
        {
            path = path ?? this.Path;
            byte[] content = this.Serialize();
            string temporary = path + ".tmp";
            File.WriteAllBytes(temporary, content);
            if (File.Exists(path))
            {
                if (backup)
                    File.Copy(path, path + ".bak", overwrite: true);
                File.Delete(path);
            }
            File.Move(temporary, path);
            this.Path = path;
            this.IsModified = false;
        }
        #endregion

        /// <summary>Reads the blocks in the order and with the sizes the game's loader expects.</summary>
        private sealed class Parser
        {
            private readonly SaveGameFile file;
            private readonly byte[] content;
            private int position;

            public Parser(SaveGameFile file, byte[] content)
            {
                this.file = file;
                this.content = content;
            }

            public void Run()
            {
                this.Packed(0xC1FAA8, 0x34);
                this.Packed(0xCA43D0, 0x154);
                this.Packed(0xC1FB28, 0x90);
                this.Packed(0xC1FB0C, 2);
                this.Packed(0xC1FB10, 10);
                this.Packed(0x826620, 0x15B0);
                this.Packed(0x860688, 0x116F0);
                this.Packed(0x846880, 0x10F7C);
                this.Packed(0x6C18C0, 4);
                this.Packed(0x6C18C4, 4);
                this.Packed(0x827FE8, 0x3C);
                this.Packed(0x846878, 2);
                this.Packed(0x860260, 2);
                this.Packed(0x6AC290, 0x2A);
                this.Packed(0x6AC2D0, 0x2A);
                this.Packed(0x85F340, 0xC);
                this.Packed(0x8265D0, 0x4C);
                this.Packed(0x6C18C8, 0x4C);
                this.Packed(0x8264B8, 0x4C);
                this.Packed(0x827C38, 0x4C);
                this.Packed(0x6C1918, 0x4C);
                this.Packed(0x916678, 0x2DCB70);
                this.Packed(0x887CA8, 0x8B740);
                this.Packed(0xBF3E90, 0x9E70);
                this.Packed(0x914F58, 0x1720);
                this.Packed(0x7CBBE8, 0x80C8);
                this.Packed(0x78BE58, 0x8192);
                this.Packed(0x7A4830, 0x8400);
                this.Packed(0x7DB790, 0x1840);
                this.Packed(0x793FF0, 0x10840);
                this.Packed(0xCAD2C0, 0x288);
                this.Packed(0x7000C0, 0x510);
                this.Packed(0x878F18, 24000);
                this.Packed(0x8726B0, 0x4682);
                this.Packed(0x82CBF8, 0xF0);
                this.Packed(0x827BD0, 100);
                this.Packed(0x87FFA8, 32000);
                this.Packed(0xBF31E8, 0xCA0);
                this.Packed(0x6C1C78, 10);
                this.Packed(0x700AA0, 0xA0);
                this.Packed(0x700BA8, 0x80);
                this.Packed(0x700C78, 0x1E0);
                this.Packed(0x702F18, 0xF0);
                this.Packed(0x703068, 0x4C);
                this.Packed(0x702A00, 0x4C);
                this.Packed(0x703010, 0x4C);
                this.Packed(0x7008D0, 0x130);
                this.Packed(0x700630, 0x14);
                // Two objects that load themselves (FUN_005a6100, address only known at runtime).
                for (int i = 0; i < 2; i++)
                {
                    this.Packed(0, 0x834);
                    this.Packed(0, 0x1B0);
                    this.Packed(0, 0x2D0);
                    this.Raw(0, 12);
                }
                this.Packed(0x7027F8, 0x1B2);
                this.Packed(0x702A50, 0x4C2);
                this.Packed(0x700648, 0x284);
                this.Packed(0x700E58, 0xE);
                this.Packed(0x7030B8, 0xE);
                this.Packed(0xC1FD70, 0x113A);
                this.Packed(0xC1FC08, 0x15E);
                this.Packed(0xC20EB0, 0x1A4);
                this.Packed(0x828028, 0x4B00);
                this.Packed(0x85DD60, 0x15E0);
                this.Packed(0x82CCE8, 6000);
                this.Packed(0xC22340, 0x1A4);
                this.Packed(0xC22198, 0x1A4);
                this.Packed(0x9133E8, 0x96A);
                this.Packed(0x85FF18, 0x348);
                this.Packed(0x876D38, 400);
                this.Packed(0x876FB0, 0x1E78);
                this.Packed(0xCA2280, 0x1B60);
                this.Packed(0xCA96C8, 0x3844);
                this.Packed(0xCACF10, 0x7A);
                this.Packed(0x77D970, 0xC80);
                this.file.nameCountBlock = this.Packed(0, 4);
                this.file.nameLengthBlock = this.Packed(0, 4);
                int poolLength = BitConverter.ToInt32(this.file.nameLengthBlock.Data, 0);
                if (poolLength < 0 || poolLength > NamePoolCapacity)
                    throw new InvalidDataException("Der Namensbestand des Spielstands ist beschädigt.");
                this.file.namePoolBlock = this.Packed(0, poolLength);
                this.Packed(0xC1BAB0, 0x152);
                this.Packed(0x782F18, 0x11A);
                this.Packed(0x783038, 0x11A);
                this.Packed(0x782B28, 0x3EC);
                this.Packed(0x872100, 0x10);
                // FUN_005fd3b0: flag, count, then count + 1 entries of 0xE10 bytes if the flag is set.
                short flag = BitConverter.ToInt16(this.Raw(0, 2).Data, 0);
                short entries = BitConverter.ToInt16(this.Raw(0, 2).Data, 0);
                if (flag != 0)
                {
                    for (int i = 0; i <= entries; i++)
                        this.Raw(0, 0xE10);
                }
                this.Packed(0x7DA7D0, 4000);
                this.Packed(0x913E00, 0x1146);
                this.Packed(0x7CB5A8, 0x640);
                this.Packed(0xC9DCB8, 0x348);
                this.Packed(0x827FD8, 4);
                this.Packed(0x7828C8, 0x23C);
                this.Packed(0xC21B70, 0x294);
                this.Packed(0x878E30, 0xE2);
                this.Packed(0x876EC8, 0xE2);
                this.Packed(0x7AE160, 0x1B80);
                this.Packed(0x7DD1C0, 0x23540);
                this.Packed(0x7D3CB0, 0x820);
                this.Packed(0x7DCFD0, 0x1E8);
                this.Packed(0xC1DAB8, 0x20);
                this.Packed(0x87FD20, 0x124);
                this.Packed(0x87F7B8, 0x490);
                this.Packed(0x87FC48, 0xD2);
                this.Packed(0x6FC910, 300);
                this.Packed(0x7DCFD0, 0x1E8);
                this.Packed(0x87FF70, 0x32);
                this.Packed(0x807E20, 0x118D8);
                this.Raw(0xC21060, 10);
                this.Packed(0x8256C0, 0x2F0);
                this.Packed(0xC1BD08, 0x1DB0);
                this.Packed(0x800750, 30000);
                this.Packed(0x819700, 0x9600);
                this.Raw(0, 2);
                this.Raw(0x700C28, 0x50);
                this.Raw(0x785968, 0x1B90);
                this.Raw(0x7ACC30, 0x580);
                this.Packed(0x703D78, 0x1F6);
                this.Raw(0, 0xA0);
                this.Raw(0xC9E030, 0xE1A);
                this.Raw(0xC9F138, 0xE1A);
                this.Raw(0xC9EE50, 0x2D2);

                if (this.content != null && this.position != this.content.Length)
                    throw new InvalidDataException("Die Datei ist kein Spielstand von Anstoss 2 Gold (unerwartetes Dateiende).");
            }

            // Without content (CreateBlank) every block is created zero-filled instead of read.
            private Block Packed(uint address, int size)
            {
                if (this.content == null)
                {
                    Block blank = this.Add(BlockKind.Packed, address, new byte[size], null);
                    blank.Changed = true;
                    return blank;
                }
                if (this.position + 4 > this.content.Length)
                    throw NotASavegame();
                int length = BitConverter.ToInt32(this.content, this.position);
                if (length < 4 || this.position + 4 + length > this.content.Length)
                    throw NotASavegame();
                byte[] stored = new byte[length];
                Array.Copy(this.content, this.position + 4, stored, 0, length);
                this.position += 4 + length;

                int declared = BitConverter.ToInt32(stored, 0);
                if (declared != size && declared != -size)
                    throw NotASavegame();
                byte[] data = SaveGameCodec.Decompress(stored);
                if (data.Length != size)
                    throw NotASavegame();
                return this.Add(BlockKind.Packed, address, data, stored);
            }

            private Block Raw(uint address, int size)
            {
                if (this.content == null)
                    return this.Add(BlockKind.Raw, address, new byte[size], new byte[size]);
                if (this.position + size > this.content.Length)
                    throw NotASavegame();
                byte[] data = new byte[size];
                Array.Copy(this.content, this.position, data, 0, size);
                this.position += size;
                return this.Add(BlockKind.Raw, address, data, (byte[])data.Clone());
            }

            private Block Add(BlockKind kind, uint address, byte[] data, byte[] stored)
            {
                Block block = new Block { Kind = kind, Address = address, Data = data, Stored = stored };
                this.file.blocks.Add(block);
                return block;
            }

            private static InvalidDataException NotASavegame() =>
                new InvalidDataException("Die Datei ist kein Spielstand von Anstoss 2 Gold.");
        }
    }
}
