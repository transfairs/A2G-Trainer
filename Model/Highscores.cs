using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;

namespace A2G_Trainer.Model
{
    /// <summary>
    /// One place of a highscore list (0x30 bytes, FUN_00594ee0): name, duration, points, level,
    /// the two title counters and the date it was entered. Edits go straight into the file image.
    /// </summary>
    public class HighscoreEntry
    {
        /// <summary>Size of an entry in the file.</summary>
        public const int Size = 0x30;
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        private readonly byte[] data;
        private readonly int at;

        internal HighscoreEntry(byte[] data, int at, int rank)
        {
            this.data = data;
            this.at = at;
            this.Rank = rank;
        }

        /// <summary>Place in the list, from 1.</summary>
        public int Rank { get; }

        /// <summary>First name of the manager (9 characters).</summary>
        public string Firstname { get => this.Text(0x00, 10); set => this.SetText(0x00, 10, value); }
        /// <summary>Last name of the manager (15 characters).</summary>
        public string Lastname { get => this.Text(0x0A, 16); set => this.SetText(0x0A, 16, value); }
        /// <summary>Fixed duration: the years played; career: the manager's age.</summary>
        public short Years { get => BitConverter.ToInt16(this.data, this.at + 0x1A); set => this.Set(0x1A, BitConverter.GetBytes(value)); }
        /// <summary>The divisor for points per year: the years (fixed duration) or the seasons (career).</summary>
        public int Seasons { get => BitConverter.ToInt32(this.data, this.at + 0x1C); set => this.Set(0x1C, BitConverter.GetBytes(value)); }
        /// <summary>The score (FUN_005e8290); 0 hides the entry.</summary>
        public int Points { get => BitConverter.ToInt32(this.data, this.at + 0x20); set => this.Set(0x20, BitConverter.GetBytes(value)); }
        /// <summary>The manager's level (+0x21).</summary>
        public byte Level { get => this.data[this.at + 0x24]; set { this.data[this.at + 0x24] = value; this.data[this.at + 0x25] = value; } }
        /// <summary>First title counter (manager +0xC8); the list shows the sum with <see cref="TitlesB"/>.</summary>
        public short TitlesA { get => BitConverter.ToInt16(this.data, this.at + 0x26); set => this.Set(0x26, BitConverter.GetBytes(value)); }
        /// <summary>Second title counter (manager +0xCA).</summary>
        public short TitlesB { get => BitConverter.ToInt16(this.data, this.at + 0x28); set => this.Set(0x28, BitConverter.GetBytes(value)); }
        /// <summary>The date the entry was made (day, month, year), empty for none.</summary>
        public string Date
        {
            get => HighscoreFiles.FormatDate(this.data, this.at + 0x2A);
            set => HighscoreFiles.ParseDate(value, this.data, this.at + 0x2A);
        }

        private string Text(int offset, int length) => Latin1.GetString(this.data, this.at + offset, length).Split('\0')[0];

        private void SetText(int offset, int length, string value)
        {
            byte[] text = new byte[length];
            byte[] bytes = Latin1.GetBytes(value ?? "");
            Array.Copy(bytes, text, Math.Min(bytes.Length, length - 1));
            this.Set(offset, text);
        }

        private void Set(int offset, byte[] bytes) => Array.Copy(bytes, 0, this.data, this.at + offset, bytes.Length);
    }

    /// <summary>
    /// One all-time season record of alltimes.saq (FUN_005e00e0/FUN_005e0130): the shared head (date,
    /// club, the manager's initial and last name) and one or two values at a place that depends on
    /// the record.
    /// </summary>
    public class SeasonRecord
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        private readonly byte[] data;
        private readonly int at;
        private readonly int valueOffset, valueSize, secondOffset, secondSize;

        internal SeasonRecord(byte[] data, int at, string name, int valueOffset, int valueSize, int secondOffset = 0, int secondSize = 0)
        {
            this.data = data;
            this.at = at;
            this.Name = name;
            this.valueOffset = valueOffset;
            this.valueSize = valueSize;
            this.secondOffset = secondOffset;
            this.secondSize = secondSize;
        }

        /// <summary>The record as the game names it (texts 2319-2341).</summary>
        public string Name { get; }
        /// <summary>The value (goals, points, minutes, DM, spectators ...); 0 means no record yet.</summary>
        public int Value { get => this.Read(this.valueOffset, this.valueSize); set => this.Write(this.valueOffset, this.valueSize, value); }
        /// <summary>Whether the record has a second value (goals against, matches).</summary>
        public bool HasSecondValue => this.secondSize > 0;
        /// <summary>The second value: goals against for the results, matches for the series.</summary>
        public int SecondValue { get => this.HasSecondValue ? this.Read(this.secondOffset, this.secondSize) : 0; set { if (this.HasSecondValue) this.Write(this.secondOffset, this.secondSize, value); } }
        /// <summary>The manager's last name.</summary>
        public string Manager
        {
            get => Latin1.GetString(this.data, this.at + 0x1C, 16).Split('\0')[0];
            set
            {
                byte[] text = new byte[16];
                byte[] bytes = Latin1.GetBytes(value ?? "");
                Array.Copy(bytes, text, Math.Min(bytes.Length, 15));
                Array.Copy(text, 0, this.data, this.at + 0x1C, 16);
            }
        }
        /// <summary>The date of the record (day, month, year), empty for none.</summary>
        public string Date
        {
            get => HighscoreFiles.FormatDate(this.data, this.at);
            set => HighscoreFiles.ParseDate(value, this.data, this.at);
        }

        private int Read(int offset, int size)
        {
            int i = this.at + offset;
            return size == 1 ? this.data[i] : size == 2 ? BitConverter.ToInt16(this.data, i) : BitConverter.ToInt32(this.data, i);
        }

        private void Write(int offset, int size, int value)
        {
            int i = this.at + offset;
            if (size == 1)
                this.data[i] = (byte)Math.Max(0, Math.Min(255, value));
            else if (size == 2)
                Array.Copy(BitConverter.GetBytes((short)Math.Max(short.MinValue, Math.Min(short.MaxValue, value))), 0, this.data, i, 2);
            else
                Array.Copy(BitConverter.GetBytes(value), 0, this.data, i, 4);
        }
    }

    /// <summary>
    /// The highscore files in the game folder: high00..high09, one per game mode (FUN_00594dd0/
    /// FUN_00594e50; 1 entry each for the fixed durations, 5 for the career), and alltimes.saq, the
    /// all-time season records (0x420 bytes, a copy of 0x860268). Plain images without header or
    /// checksum. The game rewrites a highscore list whenever a manager's game ends, and alltimes.saq
    /// when it exits.
    /// </summary>
    public class HighscoreFiles
    {
        /// <summary>Name of the season records file.</summary>
        public const string RecordsFileName = "alltimes.saq";
        /// <summary>Size of the season records file.</summary>
        public const int RecordsFileSize = 0x420;
        /// <summary>The game mode of each highscore list (high00..high09), as the game's start screen names it.</summary>
        public static readonly string[] ListNames = { "1 Jahr", "2 Jahre", "3 Jahre", "4 Jahre", "5 Jahre", "10 Jahre", "20 Jahre", "50 Jahre", "100 Jahre", "Karriere" };
        /// <summary>Entries of each list (table 0x6C2100).</summary>
        public static readonly int[] ListSizes = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 5 };

        // The 21 records: offset, name, value (offset, size) and an optional second value.
        private static readonly string[] RecordNames =
        {
            "Höchster Heimsieg", "Höchster Auswärtssieg", "Höchste Heimniederlage", "Höchste Auswärtsniederlage",
            "Beste Serie", "Schlechteste Serie", "Meiste Punkte", "Meiste Tore Saison", "Meiste Gegentore Saison",
            "Minuten ohne Gegentor", "Tore Saison", "Tore Spieltag", "Meiste Spiele", "Teuerster Einkauf",
            "Teuerster Verkauf", "Höchste Werbeeinnahme", "Höchste Durchschnittsform", "Meiste Zuschauer",
            "Höchster Zuschauerschnitt", "Meiste Platzverweise", "Übelster Treter"
        };
        // Per record: offset, value offset and size, second value offset and size (0 = none).
        private static readonly int[][] RecordLayouts =
        {
            new[] { 0x000, 0x30, 2, 0x32, 2 }, new[] { 0x03A, 0x30, 2, 0x32, 2 }, new[] { 0x074, 0x30, 2, 0x32, 2 },
            new[] { 0x0AE, 0x30, 2, 0x32, 2 }, new[] { 0x0E8, 0x2C, 1, 0x2D, 1 }, new[] { 0x116, 0x2C, 1, 0x2D, 1 },
            new[] { 0x144, 0x2C, 2, 0, 0 }, new[] { 0x172, 0x2C, 2, 0, 0 }, new[] { 0x1A0, 0x2C, 2, 0, 0 },
            new[] { 0x1CE, 0x2C, 2, 0, 0 }, new[] { 0x1FC, 0x2C, 2, 0, 0 }, new[] { 0x22E, 0x2C, 2, 0, 0 },
            new[] { 0x260, 0x2C, 2, 0, 0 }, new[] { 0x294, 0x30, 4, 0, 0 }, new[] { 0x2C8, 0x30, 4, 0, 0 },
            new[] { 0x2FC, 0x2C, 4, 0, 0 }, new[] { 0x32C, 0x30, 2, 0, 0 }, new[] { 0x360, 0x2C, 4, 0, 0 },
            new[] { 0x390, 0x2C, 4, 0, 0 }, new[] { 0x3C0, 0x2C, 2, 0, 0 }, new[] { 0x3EE, 0x30, 2, 0, 0 }
        };

        private readonly byte[][] lists = new byte[ListNames.Length][];
        private byte[] records;

        /// <summary>The game folder the files are read from and written to.</summary>
        public string Folder { get; }

        private HighscoreFiles(string folder) => this.Folder = folder;

        /// <summary>File name of highscore list <paramref name="index"/>.</summary>
        public static string ListFileName(int index) => $"high{index:00}";

        /// <summary>Reads every file of <paramref name="folder"/>; a missing one counts as empty, as in the game.</summary>
        public static HighscoreFiles Load(string folder)
        {
            HighscoreFiles files = new HighscoreFiles(folder);
            for (int i = 0; i < ListNames.Length; i++)
                files.lists[i] = ReadFixed(Path.Combine(folder, ListFileName(i)), ListSizes[i] * HighscoreEntry.Size);
            files.records = ReadFixed(Path.Combine(folder, RecordsFileName), RecordsFileSize);
            return files;
        }

        private static byte[] ReadFixed(string path, int size)
        {
            byte[] data = new byte[size];
            if (File.Exists(path))
            {
                byte[] file = File.ReadAllBytes(path);
                Array.Copy(file, data, Math.Min(file.Length, size));
            }
            return data;
        }

        /// <summary>Whether <paramref name="folder"/> looks like the game folder (it has the texts the quiz and the events read).</summary>
        public static bool IsGameFolder(string folder) => !string.IsNullOrEmpty(folder) && Directory.Exists(Path.Combine(folder, "texte"));

        /// <summary>The entries of highscore list <paramref name="index"/>.</summary>
        public List<HighscoreEntry> Entries(int index) =>
            Enumerable.Range(0, ListSizes[index]).Select(n => new HighscoreEntry(this.lists[index], n * HighscoreEntry.Size, n + 1)).ToList();

        /// <summary>The all-time season records.</summary>
        public List<SeasonRecord> SeasonRecords() =>
            RecordLayouts.Select((r, i) => new SeasonRecord(this.records, r[0], RecordNames[i], r[1], r[2], r[3], r[4])).ToList();

        /// <summary>Writes every file back, keeping the previous version of a changed one as .bak.</summary>
        public void Save()
        {
            for (int i = 0; i < ListNames.Length; i++)
                Write(Path.Combine(this.Folder, ListFileName(i)), this.lists[i]);
            Write(Path.Combine(this.Folder, RecordsFileName), this.records);
        }

        private static void Write(string path, byte[] data)
        {
            bool exists = File.Exists(path);
            if (exists && File.ReadAllBytes(path).SequenceEqual(data))
                return;
            if (!exists && data.All(b => b == 0))
                return;
            if (exists)
                File.Copy(path, path + ".bak", true);
            File.WriteAllBytes(path, data);
        }

        internal static string FormatDate(byte[] data, int at)
        {
            short year = BitConverter.ToInt16(data, at + 2);
            return year > 0 ? $"{data[at]:00}.{data[at + 1]:00}.{year}" : "";
        }

        internal static void ParseDate(string text, byte[] data, int at)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                Array.Clear(data, at, 4);
                return;
            }
            if (!DateTime.TryParseExact(text.Trim(), "d.M.yyyy", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime date))
                return;
            data[at] = (byte)date.Day;
            data[at + 1] = (byte)date.Month;
            Array.Copy(BitConverter.GetBytes((short)date.Year), 0, data, at + 2, 2);
        }
    }
}
