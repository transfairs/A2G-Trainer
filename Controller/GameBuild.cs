using System;
using System.Linq;
using System.Text;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// One executable build of the game, told apart by its PE header's link timestamp and image
    /// size - both differ per build, unlike the window title ("ANSTOSS 2 ...") or the window class
    /// (MFC's dynamic "Afx:..." names, see FUN_0066a7ae in the decompile), which every build shares.
    /// </summary>
    internal sealed class GameBuild
    {
        /// <summary>Human-readable build name, for the log.</summary>
        public string Name { get; }

        /// <summary>PE header TimeDateStamp of the main module, or null for a build not in the known table.</summary>
        public uint? TimeDateStamp { get; }

        /// <summary>PE header SizeOfImage of the main module, or null for a build not in the known table.</summary>
        public uint? SizeOfImage { get; }

        /// <summary>
        /// The memory addressing(s) to try, in order - true = GOG-style addressing (see Settings.GogOffset).
        /// A known build has exactly one; an unidentified build tries GOG first (GOG and the 2007 CD
        /// both use it, and the original build is always identified exactly) and the original second.
        /// Empty for a build the trainer doesn't support.
        /// </summary>
        public bool[] CandidateAddressings { get; }

        /// <summary>Whether the trainer can attach to this build at all.</summary>
        public bool IsSupported => this.CandidateAddressings.Length > 0;

        /// <summary>Whether this build came from the known-build table rather than the signature fallback.</summary>
        public bool IsKnown => this.TimeDateStamp.HasValue;

        private GameBuild(string name, uint? timeDateStamp, uint? sizeOfImage, params bool[] candidateAddressings)
        {
            this.Name = name;
            this.TimeDateStamp = timeDateStamp;
            this.SizeOfImage = sizeOfImage;
            this.CandidateAddressings = candidateAddressings;
        }

        /// <summary>Original German anstoss2.exe of 08.07.1999 (SHA-256 fe4b1e37...).</summary>
        public static readonly GameBuild Original = new GameBuild("Original (anstoss2.exe 1999)", 0x377CACB9, 0x8B2000, false);

        // verl.exe from the same install: an earlier Anstoss 2 build (opens "A2-Verlängerung (*.ver)"
        // files) that also titles its window "ANSTOSS 2 - ..." but has its own, unmapped memory layout.
        /// <summary>The Verlängerung executable verl.exe - recognised so it's rejected instead of attached with wrong addresses.</summary>
        public static readonly GameBuild Verlaengerung = new GameBuild("Verlängerung (verl.exe)", 0x377C6FD9, 0x87A000);

        // GOG ships its build with the link timestamp zeroed out, so only SizeOfImage tells it apart
        // here - confirmed live on 23.10.2026 (run.exe, SHA-256 1739471B..., signature string at RVA
        // 0x2AE9B4, own club readable through the GOG club pointer slot only).
        /// <summary>GOG release, run.exe.</summary>
        public static readonly GameBuild Gog = new GameBuild("GOG (run.exe)", 0x00000000, 0x8BD000, true);

        // The 2007 CD release also runs as run.exe, but is a build of its own: packed on disk (1.9 MB,
        // signature string only appears once unpacked in memory, at the same RVA 0x2AE9B4 as GOG).
        // Confirmed live on 23.10.2026 (SHA-256 836AEF55..., own club readable through the GOG club
        // pointer slot only).
        /// <summary>2007 CD release, run.exe - uses GOG's addressing.</summary>
        public static readonly GameBuild Cd2007 = new GameBuild("CD 2007 (run.exe)", 0x35FE228B, 0x8BA000, true);

        // Any other build carrying the signature string is still picked up by the fallback in
        // GameBuildDetector and gets Unidentified's GOG-then-original probing.
        internal static readonly GameBuild[] Known = { Original, Gog, Cd2007, Verlaengerung };

        /// <summary>A build that isn't in <see cref="Known"/> but carries the game's signature string.</summary>
        public static GameBuild Unidentified(uint timeDateStamp, uint sizeOfImage) =>
            new GameBuild($"unbekannter Build (TimeDateStamp 0x{timeDateStamp:X8}, SizeOfImage 0x{sizeOfImage:X})", null, null, true, false);
    }

    /// <summary>Identifies which <see cref="GameBuild"/> the process attached to a <see cref="ProcessMemory"/> runs, read straight from its mapped main module.</summary>
    internal static class GameBuildDetector
    {
        // Registry key the game reads its settings from (FUN_0066d60c, s_SOFTWARE_ASCARON_ANSTOSS_2_1_00).
        // Every game build contains it; the Dateneditor (Editor.exe) doesn't.
        internal static readonly byte[] Signature = Encoding.ASCII.GetBytes(@"SOFTWARE\ASCARON\ANSTOSS 2");

        private const int ScanChunkSize = 0x10000;
        private const uint MaxHeaderOffset = 0x1000;

        // Offsets inside the PE headers (IMAGE_NT_HEADERS32).
        private const int NtTimeDateStamp = 0x08;
        private const int NtOptionalMagic = 0x18;
        private const int NtSizeOfImage = 0x50;
        private const int NtHeaderReadLength = 0x58;
        private const ushort Pe32Magic = 0x10B;

        /// <summary>
        /// Returns the attached process's build: a <see cref="GameBuild.Known"/> entry when the PE
        /// header matches one, <see cref="GameBuild.Unidentified"/> when it doesn't but the image
        /// contains the game's <see cref="Signature"/>, or null when it's no Anstoss 2 build at all
        /// (or its header can't be read).
        /// </summary>
        public static GameBuild Identify(ProcessMemory memory)
        {
            if (!TryReadPeHeader(memory, out uint timeDateStamp, out uint sizeOfImage))
                return null;

            GameBuild known = GameBuild.Known.FirstOrDefault(b => b.TimeDateStamp == timeDateStamp && b.SizeOfImage == sizeOfImage);
            if (known != null)
                return known;

            return ContainsSignature(memory, memory.ModuleBase, sizeOfImage)
                ? GameBuild.Unidentified(timeDateStamp, sizeOfImage)
                : null;
        }

        internal static bool TryReadPeHeader(ProcessMemory memory, out uint timeDateStamp, out uint sizeOfImage)
        {
            timeDateStamp = 0;
            sizeOfImage = 0;

            uint moduleBase = memory.ModuleBase;
            byte[] dos = memory.ReadBytesAtAddress(moduleBase, 0x40);
            if (dos == null || dos[0] != 'M' || dos[1] != 'Z')
                return false;

            uint ntOffset = BitConverter.ToUInt32(dos, 0x3C);
            if (ntOffset == 0 || ntOffset > MaxHeaderOffset)
                return false;

            byte[] nt = memory.ReadBytesAtAddress(moduleBase + ntOffset, NtHeaderReadLength);
            if (nt == null || nt[0] != 'P' || nt[1] != 'E' || nt[2] != 0 || nt[3] != 0)
                return false;
            if (BitConverter.ToUInt16(nt, NtOptionalMagic) != Pe32Magic)
                return false;

            timeDateStamp = BitConverter.ToUInt32(nt, NtTimeDateStamp);
            sizeOfImage = BitConverter.ToUInt32(nt, NtSizeOfImage);
            return true;
        }

        // Scans the mapped image chunk by chunk; consecutive chunks overlap by the signature's length
        // so a match straddling a chunk boundary isn't missed. Unreadable chunks are skipped.
        internal static bool ContainsSignature(ProcessMemory memory, uint start, uint length)
        {
            int overlap = Signature.Length - 1;
            for (long offset = 0; offset < length; offset += ScanChunkSize)
            {
                int chunkLength = (int)Math.Min(ScanChunkSize + overlap, length - offset);
                byte[] chunk = memory.ReadBytesAtAddress((uint)(start + offset), chunkLength);
                if (chunk != null && IndexOf(chunk, Signature) >= 0)
                    return true;
            }
            return false;
        }

        internal static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                int j = 0;
                while (j < needle.Length && haystack[i + j] == needle[j])
                    j++;
                if (j == needle.Length)
                    return i;
            }
            return -1;
        }
    }
}
