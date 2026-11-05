using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// The club records of the five league countries and the working copies the game has fetched
    /// of them (see Settings: record table, cache slots). A persistent club field is changed by
    /// writing the record and every cached working copy of the club - the game writes a working
    /// copy back over the record when it puts the club away.
    /// </summary>
    internal static class ClubRecords
    {
        /// <summary>Countries whose clubs have records (the countries with a league table).</summary>
        internal static readonly PlayerEnums.Country[] Countries =
            { PlayerEnums.Country.Deutschland, PlayerEnums.Country.England, PlayerEnums.Country.Frankreich, PlayerEnums.Country.Italien, PlayerEnums.Country.Spanien };

        /// <summary>One club of a country as listed by <see cref="CountryClubs"/>.</summary>
        internal sealed class Entry
        {
            /// <summary>Club ID within its country.</summary>
            public byte Id { get; set; }
            /// <summary>Club name from the record.</summary>
            public string Name { get; set; }
            /// <summary>Position on the editor's map (record +0x702).</summary>
            public short MapX { get; set; }
            /// <summary>Position on the editor's map (record +0x704).</summary>
            public short MapY { get; set; }
        }

        // First record and club count of the country's league system; false if unknown or unreadable.
        private static bool CountryRange(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, out short first, out short count)
        {
            first = count = 0;
            if (fixedAddresses == null || !Countries.Contains(country))
                return false;

            byte[] system = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.LeagueSystemByCountryOffset) + (uint)country, 1);
            if (system == null)
                return false;
            byte[] counts = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.LeagueClubCountTableOffset) + (uint)system[0] * 2, 2);
            byte[] bases = memory.ReadBytesAtAddress(fixedAddresses.Static(memory, Settings.LeagueBaseTableOffset) + (uint)system[0] * 2, 2);
            if (counts == null || bases == null)
                return false;

            first = BitConverter.ToInt16(bases, 0);
            count = BitConverter.ToInt16(counts, 0);
            return true;
        }

        /// <summary>Address of the record of the country's club <paramref name="clubId"/>, or null if it has none.</summary>
        internal static uint? RecordAddress(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, int clubId)
        {
            if (!CountryRange(memory, fixedAddresses, country, out short first, out short count) || clubId >= count)
                return null;
            return fixedAddresses.Runtime(memory, Settings.ClubRecordTableOffset) + (uint)(first + clubId) * Settings.ClubRecordStride;
        }

        /// <summary>Addresses of every working copy of the club in the game's club cache.</summary>
        internal static List<uint> WorkingCopies(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, int clubId)
        {
            List<uint> copies = new List<uint>();
            if (fixedAddresses == null)
                return copies;

            uint cache = fixedAddresses.Runtime(memory, Settings.ClubCacheTableOffset);
            for (int slot = 0; slot < Settings.ClubCacheSlots; slot++)
            {
                byte[] entry = memory.ReadBytesAtAddress(cache + (uint)slot * Settings.ClubCacheStride, (int)Settings.ClubCacheStride);
                if (entry == null || entry[0] == 0 || entry[1] != (byte)country || entry[2] != clubId)
                    continue;
                uint workingCopy = BitConverter.ToUInt32(entry, (int)Settings.ClubCachePointerOffset);
                if (workingCopy != 0)
                    copies.Add(workingCopy);
            }
            return copies;
        }

        /// <summary>A stretch of the working copy the game fills from the record when it fetches a club.</summary>
        internal sealed class Segment
        {
            /// <summary>Offset in the working copy (AddressPresets.OWN_CLUB layout).</summary>
            public uint Working { get; }
            /// <summary>Offset in the record.</summary>
            public uint Record { get; }
            /// <summary>Length in bytes.</summary>
            public int Length { get; }
            /// <summary>The part of the club it belongs to (fetch flag, kept in the cache slot's mask); 0 if always fetched.</summary>
            public ushort Part { get; }

            internal Segment(uint working, uint record, int length, ushort part)
            {
                this.Working = working;
                this.Record = record;
                this.Length = length;
                this.Part = part;
            }
        }

        /// <summary>Size of a working copy (FUN_00584ad0 allocates 0x14E0 bytes).</summary>
        internal const int WorkingCopyLength = 0x14E0;

        // From the fetch FUN_00585ff0, which copies record -> working copy by part: 0x200 the name,
        // 0x100 co-/goalkeeper trainer, morale, tactics, fans, editor settings, finances, team cohesion, tickets and map position,
        // 0x80 the stadium (with the town). Squad count, amateurs, ID and country always.
        internal static readonly Segment[] Segments =
        {
            new Segment(0x000, 0x000, 0x14, 0x200),
            new Segment(0x082, 0x082, 4, 0x100),
            new Segment(0x08E, 0x094, 2, 0x100),
            new Segment(0x09A, 0x0A0, 2, 0x100),
            new Segment(0x09C, 0x0A2, 9, 0x100),
            new Segment(0x0E6, 0x0DE, 2, 0),
            new Segment(0x0E8, 0x0E0, 0x12, 0x100),
            new Segment(0x102, 0x0F4, 1, 0),
            new Segment(0x0FF, 0x0F5, 3, 0x100),
            new Segment(0x10E, 0x103, 1, 0x100),
            new Segment(0x103, 0x0F8, 1, 0),
            new Segment(0x144, 0x138, 0x10, 0x100),
            new Segment(0x154, 0x1A4, 0x348, 0x100),
            new Segment(0x814, 0x5D4, 4, 0x100),
            new Segment(0x81C, 0x4EC, 0x84, 0x80),
            new Segment(0x8FC, 0x148, 0x5C, 0x100),
            new Segment(0xA19, 0x5BD, 1, 0),
            new Segment(0x1074, 0x6FC, 4, 0x100),
            new Segment(0x1438, 0x702, 4, 0x100),
        };

        /// <summary>Record address of the working-copy field at <paramref name="working"/>, or null if the record doesn't keep it.</summary>
        internal static uint? RecordField(uint record, uint working, int length)
        {
            Segment segment = Segments.FirstOrDefault(s => working >= s.Working && working + length <= s.Working + s.Length);
            return segment == null ? (uint?)null : record + segment.Record + (working - segment.Working);
        }

        /// <summary>
        /// The club as the game sees it, in the working copy's layout: each part from the cached working
        /// copy that has it fetched (the game works on that one), the rest from the record. Fields the
        /// record doesn't keep stay 0.
        /// </summary>
        internal static byte[] Image(ProcessMemory memory, FixedAddresses fixedAddresses, int index, PlayerEnums.Country country, int clubId)
        {
            byte[] image = new byte[WorkingCopyLength];
            if (fixedAddresses == null)
                return image;

            uint record = fixedAddresses.Runtime(memory, Settings.ClubRecordTableOffset) + (uint)index * Settings.ClubRecordStride;
            uint copy = 0;
            ushort parts = 0;
            uint cache = fixedAddresses.Runtime(memory, Settings.ClubCacheTableOffset);
            for (int slot = 0; slot < Settings.ClubCacheSlots && copy == 0; slot++)
            {
                byte[] entry = memory.ReadBytesAtAddress(cache + (uint)slot * Settings.ClubCacheStride, (int)Settings.ClubCacheStride);
                if (entry == null || entry[0] == 0 || entry[1] != (byte)country || entry[2] != clubId)
                    continue;
                copy = BitConverter.ToUInt32(entry, (int)Settings.ClubCachePointerOffset);
                parts = BitConverter.ToUInt16(entry, (int)Settings.ClubCachePartsOffset);
            }

            foreach (Segment segment in Segments)
            {
                bool fromCopy = copy != 0 && segment.Part != 0 && (parts & segment.Part) == segment.Part;
                byte[] bytes = memory.ReadBytesAtAddress(fromCopy ? copy + segment.Working : record + segment.Record, segment.Length);
                if (bytes != null)
                    Array.Copy(bytes, 0, image, segment.Working, segment.Length);
            }
            return image;
        }

        /// <summary>
        /// Whether the club is listed on the stock market, i.e. has shares: from a working copy in the
        /// game's cache if there is one (it may be newer), else from the record. Null if unknown.
        /// </summary>
        internal static bool? IsListed(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, int clubId)
        {
            uint? record = RecordAddress(memory, fixedAddresses, country, clubId);
            if (record == null)
                return null;
            foreach (uint workingCopy in WorkingCopies(memory, fixedAddresses, country, clubId))
            {
                byte[] working = memory.ReadBytesAtAddress(workingCopy + Settings.ClubListedOffset, 1);
                if (working != null)
                    return working[0] != 0;
            }
            byte[] listed = memory.ReadBytesAtAddress(record.Value + Settings.ClubRecordListedOffset, 1);
            return listed == null ? (bool?)null : listed[0] != 0;
        }

        /// <summary>All clubs of the country with their map positions, by ID; empty if the country has no records.</summary>
        internal static List<Entry> CountryClubs(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country)
        {
            List<Entry> clubs = new List<Entry>();
            if (!CountryRange(memory, fixedAddresses, country, out short first, out short count))
                return clubs;

            Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
            uint table = fixedAddresses.Runtime(memory, Settings.ClubRecordTableOffset);
            for (int id = 0; id < count && id <= byte.MaxValue; id++)
            {
                uint record = table + (uint)(first + id) * Settings.ClubRecordStride;
                byte[] name = memory.ReadBytesAtAddress(record, 19);
                byte[] mapX = memory.ReadBytesAtAddress(record + Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.MapX), 2);
                byte[] mapY = memory.ReadBytesAtAddress(record + Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.MapY), 2);
                if (name == null || mapX == null || mapY == null)
                    continue;
                clubs.Add(new Entry
                {
                    Id = (byte)id,
                    Name = latin1.GetString(name).Split('\0')[0],
                    MapX = BitConverter.ToInt16(mapX, 0),
                    MapY = BitConverter.ToInt16(mapY, 0)
                });
            }
            return clubs;
        }
    }
}
