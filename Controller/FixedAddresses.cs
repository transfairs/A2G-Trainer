using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Where the fixed addresses found by statically analysing the original build (Mitspieler copies,
    /// club records, league tables, calendar, name pool, job-offer code - see Settings) sit in the
    /// attached build.
    /// </summary>
    /// <remarks>
    /// The original build uses them as they are. Any other supported build (GOG, the 2007 CD release)
    /// has them at constant distances, found here instead of listed per build:
    /// <list type="bullet">
    /// <item>runtime data moves like the persistent anchors PersistentLayoutCalibrator confirmed
    /// against the display cache - all calibrated anchors must agree on one distance;</item>
    /// <item>the static league tables and the job-offer code are searched for by byte pattern near
    /// their original address, and each pattern must occur there exactly once.</item>
    /// </list>
    /// Nothing is used before the layout is calibrated, so an unexpected build is never written into.
    /// </remarks>
    internal sealed class FixedAddresses
    {
        /// <summary>Distance of runtime/savegame data from the original build's addresses.</summary>
        public uint RuntimeShift { get; }
        /// <summary>Distance of the static tables in the initialised data section from the original build's addresses.</summary>
        public uint StaticShift { get; }
        /// <summary>The job-offer code in this build, or null if it couldn't be found.</summary>
        public JobOfferCode JobOffer { get; }

        /// <summary>Module-relative addresses of the code JobOfferInjector hooks.</summary>
        internal sealed class JobOfferCode
        {
            /// <summary>Call site of the job-offer presentation (E8 rel32).</summary>
            public uint PresentCallSite { get; }
            /// <summary>The job-offer presentation function.</summary>
            public uint PresentFunction { get; }
            /// <summary>The function that adds a job offer to an offer list.</summary>
            public uint AddFunction { get; }

            internal JobOfferCode(uint presentCallSite, uint presentFunction, uint addFunction)
            {
                this.PresentCallSite = presentCallSite;
                this.PresentFunction = presentFunction;
                this.AddFunction = addFunction;
            }
        }

        internal FixedAddresses(uint runtimeShift, uint staticShift, JobOfferCode jobOffer)
        {
            this.RuntimeShift = runtimeShift;
            this.StaticShift = staticShift;
            this.JobOffer = jobOffer;
        }

        /// <summary>The original build's addresses.</summary>
        public static FixedAddresses Original { get; } = new FixedAddresses(0, 0,
            new JobOfferCode(Settings.JobOfferPresentCallSiteOffset, Settings.JobOfferPresentFunctionOffset, Settings.JobOfferAddFunctionOffset));

        /// <summary>Absolute address of runtime data at the original build's module-relative <paramref name="offset"/>.</summary>
        public uint Runtime(ProcessMemory memory, uint offset) => unchecked(memory.ModuleBase + offset + this.RuntimeShift);

        /// <summary>Absolute address of a static table at the original build's module-relative <paramref name="offset"/>.</summary>
        public uint Static(ProcessMemory memory, uint offset) => unchecked(memory.ModuleBase + offset + this.StaticShift);

        /// <summary>
        /// How far runtime data is moved in the build <paramref name="memory"/> is attached to, judged by
        /// <paramref name="layout"/>: 0 for the original build, the calibrated anchors' common distance
        /// for another supported build, or null while that isn't confirmed.
        /// </summary>
        public static uint? RuntimeShiftFor(ProcessMemory memory, PersistentLayout layout)
        {
            if (layout.HasOriginalBuildAnchors)
                return 0;
            if (!layout.IsVerified || memory.Build == null || !memory.Build.IsSupported || !layout.PlayerRecordTableOffset.HasValue)
                return null;

            uint shift = unchecked(layout.PlayerRecordTableOffset.Value - Settings.PlayerRecordTableOffset);
            bool consistent = SameShift(layout.NamePoolPointerOffset, Settings.NamePoolPointerOffset, shift)
                && SameShift(layout.AgeReferenceYearOffset, Settings.AgeReferenceYearOffset, shift)
                && SameShift(layout.ActiveTrainerCountOffset, Settings.ActiveTrainerCountOffset, shift);
            return consistent ? shift : (uint?)null;
        }

        private static bool SameShift(uint? anchor, uint original, uint shift) => !anchor.HasValue || unchecked(anchor.Value - original) == shift;

        // One search per calibrated layout (a new layout comes with every attach/calibration).
        private static readonly ConditionalWeakTable<PersistentLayout, Holder> Discovered = new ConditionalWeakTable<PersistentLayout, Holder>();
        private sealed class Holder { public FixedAddresses Value; }

        /// <summary>The fixed addresses for the build <paramref name="memory"/> is attached to, or null if they can't be used (yet).</summary>
        public static FixedAddresses For(ProcessMemory memory)
        {
            PersistentLayout layout = memory.Layout;
            if (layout.HasOriginalBuildAnchors)
                return Original;

            uint? runtimeShift = RuntimeShiftFor(memory, layout);
            if (!runtimeShift.HasValue)
                return null;
            return Discovered.GetValue(layout, _ => new Holder { Value = Discover(memory, runtimeShift.Value) }).Value;
        }

        private static FixedAddresses Discover(ProcessMemory memory, uint runtimeShift)
        {
            uint? leagueBases = FindUnique(memory, Settings.LeagueBaseTableOffset, Settings.LeagueBasePattern);
            if (!leagueBases.HasValue)
            {
                Logger.Warn($"FixedAddresses: Ligatabellen in {memory.Build?.Name} nicht eindeutig gefunden - Trainer-Funktionen mit festen Adressen bleiben aus.");
                return null;
            }
            uint staticShift = unchecked(leagueBases.Value - Settings.LeagueBaseTableOffset);
            byte[] ranges = memory.ReadBytesAtAddress(unchecked(memory.ModuleBase + Settings.LeagueRangeTableOffset + staticShift), Settings.LeagueRangePattern.Length);
            if (ranges == null || !Matches(ranges, 0, Settings.LeagueRangePattern))
            {
                Logger.Warn($"FixedAddresses: Ligabereiche in {memory.Build?.Name} passen nicht zur Verschiebung 0x{staticShift:X} - Trainer-Funktionen mit festen Adressen bleiben aus.");
                return null;
            }

            JobOfferCode jobOffer = DiscoverJobOffer(memory);
            Logger.Warn($"FixedAddresses: {memory.Build?.Name}: Laufzeitdaten +0x{runtimeShift:X}, statische Tabellen +0x{staticShift:X}, " +
                (jobOffer != null
                    ? $"Jobangebot: Aufruf 0x{jobOffer.PresentCallSite:X}, Anzeige 0x{jobOffer.PresentFunction:X}, Eintragen 0x{jobOffer.AddFunction:X}."
                    : "Jobangebot-Code nicht eindeutig gefunden."));
            return new FixedAddresses(runtimeShift, staticShift, jobOffer);
        }

        private static JobOfferCode DiscoverJobOffer(ProcessMemory memory)
        {
            uint? add = FindUnique(memory, Settings.JobOfferAddFunctionOffset, Settings.JobOfferAddPattern);
            uint? present = FindUnique(memory, Settings.JobOfferPresentFunctionOffset, Settings.JobOfferPresentPattern);
            if (!add.HasValue || !present.HasValue)
                return null;

            // The presentation call: a CALL rel32 to the present function, followed by the original's
            // next instructions - exactly once near the original call site.
            byte[] trail = Settings.JobOfferCallTrailPattern;
            uint windowStart;
            byte[] window = ReadWindow(memory, Settings.JobOfferPresentCallSiteOffset, out windowStart);
            if (window == null)
                return null;
            uint? callSite = null;
            for (int i = 0; i + 5 + trail.Length <= window.Length; i++)
            {
                if (window[i] != 0xE8 || !Matches(window, i + 5, trail))
                    continue;
                uint target = unchecked((uint)(windowStart + i + 5 + BitConverter.ToInt32(window, i + 1)));
                if (target != present.Value)
                    continue;
                if (callSite.HasValue)
                    return null;
                callSite = windowStart + (uint)i;
            }
            return callSite.HasValue ? new JobOfferCode(callSite.Value, present.Value, add.Value) : null;
        }

        // Module-relative offset of the only occurrence of pattern within the search window around the
        // original module-relative offset, or null if there's none or more than one.
        private static uint? FindUnique(ProcessMemory memory, uint originalOffset, byte[] pattern)
        {
            uint windowStart;
            byte[] window = ReadWindow(memory, originalOffset, out windowStart);
            if (window == null)
                return null;

            uint? found = null;
            for (int i = 0; i + pattern.Length <= window.Length; i++)
            {
                if (!Matches(window, i, pattern))
                    continue;
                if (found.HasValue)
                    return null;
                found = windowStart + (uint)i;
            }
            return found;
        }

        // The module bytes from originalOffset - window to originalOffset + window (clamped to the
        // image), with windowStart set to the module-relative offset of byte 0. Unreadable pages stay zero.
        private static byte[] ReadWindow(ProcessMemory memory, uint originalOffset, out uint windowStart)
        {
            const int Chunk = 0x1000;
            uint window = Settings.FixedAddressSearchWindow;
            windowStart = originalOffset > window ? originalOffset - window : 0;
            uint end = Math.Min(originalOffset + window, memory.ModuleSize);
            if (end <= windowStart)
                return null;

            byte[] bytes = new byte[end - windowStart];
            bool any = false;
            for (uint offset = 0; offset < bytes.Length; offset += Chunk)
            {
                int length = (int)Math.Min(Chunk, bytes.Length - offset);
                byte[] chunk = memory.ReadBytesAtAddress(memory.ModuleBase + windowStart + offset, length);
                if (chunk == null)
                    continue;
                Buffer.BlockCopy(chunk, 0, bytes, (int)offset, chunk.Length);
                any = true;
            }
            return any ? bytes : null;
        }

        private static bool Matches(byte[] data, int at, IList<byte> pattern)
        {
            if (at < 0 || at + pattern.Count > data.Length)
                return false;
            for (int j = 0; j < pattern.Count; j++)
            {
                if (data[at + j] != pattern[j])
                    return false;
            }
            return true;
        }
    }
}
