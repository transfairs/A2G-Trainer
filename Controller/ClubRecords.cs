using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer_XP.Controller
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
