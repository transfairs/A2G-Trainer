using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Finds (or confirms) the persistent-data anchors of a game build whose anchors aren't known
    /// for certain (see <see cref="PersistentLayout"/>), by matching what's in memory against the
    /// own roster's display-cache values - which every build already reads correctly.
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>Record table: the module offset where, for every own player, the record at
    /// +PlayerId*stride holds that player's Level/Position. The guessed and the original offset are
    /// tried first; otherwise the whole module image is scanned. Exactly one hit is required.</item>
    /// <item>Savegame year: a UInt16 that decodes every own player's record Age byte back to the
    /// display-cache Age (see Settings.AgeReferenceYearEpoch). Tried at the original offset shifted by
    /// the record table's own shift and by Settings.GogOffset, then searched in a window around both.</item>
    /// <item>Active trainer count: the byte at the same distance from the year as in the original build, if plausible.</item>
    /// <item>Name pool pointer: a pointer through which the record's name indices resolve to the
    /// display-cache names. Same candidates/window as the year.</item>
    /// </list>
    /// Anchors that can't be found stay null, so the matching features keep using the display cache.
    /// The result is logged at Warn level so a user's log file carries the offsets to hardcode.
    /// </remarks>
    public class PersistentLayoutCalibrator
    {
        internal const int MinSamples = 3;
        internal const uint SearchWindow = 0x10000;
        internal const int MaxPoolRead = 0x200000;
        private const double MinMatchRatio = 0.9;
        private const int PageSize = 0x1000;
        private const int MinPlausibleYear = 1990;
        private const int MaxPlausibleYear = 2100;
        private const int MaxNameSamples = 3;
        private const uint MinValidPointer = 0x10000;

        // Covers every record field the checks below look at (Age +0x8, Level +0xA, Position +0xB).
        private static readonly int RecordProbeLength = (int)Math.Max(Settings.PlayerRecordLevelOffset, Math.Max(Settings.PlayerRecordPositionOffset, Settings.PlayerRecordAgeOffset)) + 1;

        private readonly ProcessMemory memory;
        private readonly Encoding encoding = Encoding.GetEncoding("iso-8859-1");

        /// <summary>Creates a calibrator that reads through the given process memory accessor.</summary>
        public PersistentLayoutCalibrator(ProcessMemory memory)
        {
            this.memory = memory;
        }

        /// <summary>
        /// Returns a verified layout for the attached build, or null if the record table can't be
        /// pinned down from these players (too few usable samples, or no unique match) - the caller
        /// then keeps using the display cache and may retry with a later roster.
        /// </summary>
        /// <param name="displayCachePlayers">The own roster, read from the display cache only.</param>
        public PersistentLayout Calibrate(IList<Player> displayCachePlayers)
        {
            List<Player> samples = displayCachePlayers.Where(p => p.Level > 0).ToList();
            if (samples.Count < MinSamples)
            {
                Logger.Warn($"Kalibrierung der dauerhaften Spielerdaten: nur {samples.Count} brauchbare Spieler gelesen - neuer Versuch beim nächsten Laden.");
                return null;
            }

            uint? table = this.FindRecordTable(samples);
            if (!table.HasValue)
            {
                Logger.Warn($"Kalibrierung der dauerhaften Spielerdaten: Spielertabelle nicht gefunden ({this.ModuleName()}, {samples.Count} Spieler, Modulgröße 0x{this.memory.ModuleSize:X}) - es wird nur der Display-Cache verwendet.");
                return null;
            }

            uint tableShift = unchecked(table.Value - Settings.PlayerRecordTableOffset);
            uint? year = this.FindAgeReferenceYear(table.Value, tableShift, samples);
            uint? trainerCount = year.HasValue ? this.CheckActiveTrainerCount(year.Value) : null;
            uint? namePool = this.FindNamePoolPointer(table.Value, tableShift, samples);

            PersistentLayout layout = new PersistentLayout(table, namePool, year, trainerCount, isVerified: true);
            Logger.Warn($"Dauerhafte Adressen für diese Spielversion ermittelt ({this.ModuleName()}): {layout}. Zum Vergleich Originalversion: {PersistentLayout.Original}.");
            return layout;
        }

        #region Record table

        private uint? FindRecordTable(List<Player> samples)
        {
            foreach (uint candidate in Distinct(Settings.PlayerRecordTableOffset + Settings.GogOffset, Settings.PlayerRecordTableOffset))
            {
                if (this.RecordTableMatchesLive(candidate, samples))
                    return candidate;
            }
            return this.ScanForRecordTable(samples);
        }

        private bool RecordTableMatchesLive(uint tableOffset, List<Player> samples)
        {
            int matches = 0;
            foreach (Player p in samples)
            {
                byte[] record = this.memory.ReadBytesAtAddress(this.RecordAddress(tableOffset, p), RecordProbeLength);
                if (record != null && RecordMatches(record, 0, p))
                    matches++;
            }
            return IsEnough(matches, samples.Count);
        }

        private uint? ScanForRecordTable(List<Player> samples)
        {
            byte[] image = this.ReadRange(this.memory.ModuleBase, this.memory.ModuleSize);
            if (image == null)
                return null;

            Player anchor = samples[0];
            long anchorStart = (long)anchor.NameRecordId * Settings.PlayerRecordStride;
            long span = samples.Max(p => (long)p.NameRecordId) * Settings.PlayerRecordStride + RecordProbeLength;

            List<uint> hits = new List<uint>();
            for (long tableOffset = 0; tableOffset + span <= image.Length && hits.Count < 2; tableOffset++)
            {
                // Cheap pre-filter on one player before checking the whole roster.
                if (!RecordMatches(image, tableOffset + anchorStart, anchor))
                    continue;

                int matches = samples.Count(p => RecordMatches(image, tableOffset + (long)p.NameRecordId * Settings.PlayerRecordStride, p));
                if (IsEnough(matches, samples.Count))
                    hits.Add((uint)tableOffset);
            }

            if (hits.Count > 1)
                Logger.Warn($"Kalibrierung: Spielertabelle nicht eindeutig (u.a. 0x{hits[0]:X} und 0x{hits[1]:X}) - verwende keine.");
            return hits.Count == 1 ? hits[0] : (uint?)null;
        }

        private static bool RecordMatches(byte[] data, long recordStart, Player p) =>
            data[recordStart + Settings.PlayerRecordLevelOffset] == p.Level &&
            data[recordStart + Settings.PlayerRecordPositionOffset] == (byte)p.Position;

        #endregion

        #region Savegame year / trainer count

        private uint? FindAgeReferenceYear(uint tableOffset, uint tableShift, List<Player> samples)
        {
            // The record's age byte per player, read once - only the year candidate varies below.
            Dictionary<Player, byte> ageBytes = new Dictionary<Player, byte>();
            foreach (Player p in samples.Where(p => p.Age > 0))
            {
                byte[] b = this.memory.ReadBytesAtAddress(this.RecordAddress(tableOffset, p) + Settings.PlayerRecordAgeOffset, 1);
                if (b != null)
                    ageBytes[p] = b[0];
            }
            if (ageBytes.Count < MinSamples)
                return null;

            uint[] centers = Distinct(unchecked(Settings.AgeReferenceYearOffset + tableShift), Settings.AgeReferenceYearOffset + Settings.GogOffset).ToArray();
            foreach (uint candidate in centers)
            {
                byte[] value = this.memory.ReadBytesAtAddress(this.memory.ModuleBase + candidate, 2);
                if (value != null && YearMatches(BitConverter.ToUInt16(value, 0), ageBytes))
                    return candidate;
            }

            // Window search, 2-byte aligned like the original field. A year value can occur more than
            // once nearby, so prefer hits whose trainer-count neighbour is plausible, then the one
            // closest to where a shifted original offset predicted it.
            List<uint> hits = new List<uint>();
            foreach (uint center in centers)
                hits.AddRange(this.ScanWindow(center, 2, (window, i) => YearMatches(BitConverter.ToUInt16(window, i), ageBytes)));

            return hits
                .Distinct()
                .OrderBy(h => this.CheckActiveTrainerCount(h).HasValue ? 0 : 1)
                .ThenBy(h => centers.Min(c => Math.Abs((long)h - c)))
                .Select(h => (uint?)h)
                .FirstOrDefault();
        }

        private static bool YearMatches(int year, Dictionary<Player, byte> ageBytes)
        {
            if (year < MinPlausibleYear || year > MaxPlausibleYear)
                return false;

            int constant = year - Settings.AgeReferenceYearEpoch;
            int matches = ageBytes.Count(kv => (byte)(constant - kv.Value) == kv.Key.Age);
            return IsEnough(matches, ageBytes.Count);
        }

        private uint? CheckActiveTrainerCount(uint yearOffset)
        {
            uint offset = yearOffset + (Settings.ActiveTrainerCountOffset - Settings.AgeReferenceYearOffset);
            byte[] value = this.memory.ReadBytesAtAddress(this.memory.ModuleBase + offset, 1);
            return value != null && value[0] >= 1 && value[0] <= Coach.MaxTrainers ? offset : (uint?)null;
        }

        #endregion

        #region Name pool

        private uint? FindNamePoolPointer(uint tableOffset, uint tableShift, List<Player> samples)
        {
            // Each sample's [Firstname, Lastname] pool indices, read once from the record table.
            List<KeyValuePair<Player, ushort[]>> nameSamples = new List<KeyValuePair<Player, ushort[]>>();
            foreach (Player p in samples.Where(p => !string.IsNullOrEmpty(p.Firstname) && !string.IsNullOrEmpty(p.Lastname)).Take(MaxNameSamples))
            {
                byte[] indices = this.memory.ReadBytesAtAddress(this.RecordAddress(tableOffset, p), 4);
                if (indices != null)
                    nameSamples.Add(new KeyValuePair<Player, ushort[]>(p, new[] { BitConverter.ToUInt16(indices, 0), BitConverter.ToUInt16(indices, 2) }));
            }
            if (nameSamples.Count < 2)
                return null;

            uint[] centers = Distinct(unchecked(Settings.NamePoolPointerOffset + tableShift), Settings.NamePoolPointerOffset + Settings.GogOffset).ToArray();

            // Window search (the exact candidates are the windows' first, nearest-to-center hits),
            // 4-byte aligned since it's a pointer. Most dwords nearby don't point at a string at
            // all, so filter cheaply first and remember pointer values already rejected.
            HashSet<uint> rejected = new HashSet<uint>();
            foreach (uint center in centers)
            {
                foreach (uint hit in this.ScanWindow(center, 4, (window, i) =>
                {
                    uint pointer = BitConverter.ToUInt32(window, i);
                    if (pointer < MinValidPointer || rejected.Contains(pointer))
                        return false;
                    if (this.PoolMatches(pointer, nameSamples))
                        return true;
                    rejected.Add(pointer);
                    return false;
                }))
                {
                    return hit;
                }
            }
            return null;
        }

        private bool PoolMatches(uint poolBase, List<KeyValuePair<Player, ushort[]>> nameSamples)
        {
            if (!this.LooksLikeStringStart(poolBase))
                return false;

            byte[] pool = this.ReadContiguous(poolBase, MaxPoolRead);
            if (pool == null)
                return false;

            // The display cache truncates names (9/15 bytes), the pool doesn't - hence StartsWith.
            return nameSamples.All(s =>
                this.PoolString(pool, s.Value[0]).StartsWith(s.Key.Firstname, StringComparison.Ordinal) &&
                this.PoolString(pool, s.Value[1]).StartsWith(s.Key.Lastname, StringComparison.Ordinal));
        }

        // Nth null-terminated string in the pool buffer, or "" if it lies beyond the buffer.
        private string PoolString(byte[] pool, int index)
        {
            int pos = 0;
            for (int i = 0; i < index; i++)
            {
                int nul = Array.IndexOf(pool, (byte)0, pos);
                if (nul < 0)
                    return "";
                pos = nul + 1;
            }
            int end = Array.IndexOf(pool, (byte)0, pos);
            return end < 0 ? "" : this.encoding.GetString(pool, pos, end - pos);
        }

        private bool LooksLikeStringStart(uint address)
        {
            byte[] b = this.memory.ReadBytesAtAddress(address, 1);
            return b != null && ((b[0] >= 'A' && b[0] <= 'Z') || (b[0] >= 'a' && b[0] <= 'z') || b[0] >= 0xC0);
        }

        #endregion

        #region Memory helpers

        private uint RecordAddress(uint tableOffset, Player p) =>
            this.memory.ModuleBase + tableOffset + (uint)p.NameRecordId * Settings.PlayerRecordStride;

        // Module offsets within +/-SearchWindow of center (clamped to the module), stepping by
        // alignment, for which match(window, indexInWindow) holds - nearest to center first.
        private IEnumerable<uint> ScanWindow(uint center, int alignment, Func<byte[], int, bool> match)
        {
            uint start = center > SearchWindow ? center - SearchWindow : 0;
            start -= start % (uint)alignment;
            uint end = center + SearchWindow;
            if (end > this.memory.ModuleSize)
                end = this.memory.ModuleSize;
            if (end <= start + (uint)alignment)
                yield break;

            byte[] window = this.ReadRange(this.memory.ModuleBase + start, end - start);
            if (window == null)
                yield break;

            IEnumerable<int> indices = Enumerable.Range(0, (window.Length - alignment) / alignment + 1)
                .Select(k => k * alignment)
                .OrderBy(i => Math.Abs((long)start + i - center));
            foreach (int i in indices)
            {
                if (match(window, i))
                    yield return start + (uint)i;
            }
        }

        // Reads [address, address+length) page by page, so one unreadable page doesn't void the
        // whole range; unreadable pages stay zero. Null if nothing at all was readable.
        private byte[] ReadRange(uint address, uint length)
        {
            if (length == 0)
                return null;

            byte[] result = new byte[length];
            bool anyRead = false;
            for (uint pos = 0; pos < length; pos += PageSize)
            {
                int chunk = (int)Math.Min(PageSize, length - pos);
                byte[] bytes = this.memory.ReadBytesAtAddress(address + pos, chunk);
                if (bytes == null)
                    continue;
                Array.Copy(bytes, 0, result, pos, chunk);
                anyRead = true;
            }
            return anyRead ? result : null;
        }

        // Up to maxLength readable bytes from address: one read if the whole range is readable,
        // otherwise the readable prefix up to the first unreadable page. Null if nothing is readable.
        private byte[] ReadContiguous(uint address, int maxLength)
        {
            byte[] all = this.memory.ReadBytesAtAddress(address, maxLength);
            if (all != null)
                return all;

            List<byte> prefix = new List<byte>();
            for (int pos = 0; pos < maxLength; pos += PageSize)
            {
                byte[] bytes = this.memory.ReadBytesAtAddress(address + (uint)pos, Math.Min(PageSize, maxLength - pos));
                if (bytes == null)
                    break;
                prefix.AddRange(bytes);
            }
            return prefix.Count > 0 ? prefix.ToArray() : null;
        }

        private string ModuleName() => this.memory.mProc.MainModule?.ModuleName ?? "unbekanntes Modul";

        private static bool IsEnough(int matches, int total) =>
            matches >= MinSamples && matches >= Math.Ceiling(total * MinMatchRatio);

        private static IEnumerable<uint> Distinct(params uint[] candidates) => candidates.Distinct();

        #endregion
    }
}
