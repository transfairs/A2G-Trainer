using System;
using System.Linq;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// The instructions behind the match engine's goal limits and how they are rewritten (see
    /// Settings and GoalLimitPatch), on plain bytes. The standalone A2G-Patch makes the same change
    /// in the exe on disk or by its msvfw32.dll; keep its copy in step when this changes.
    /// <code>
    ///   limit:  setg r8 / dec r32 / and r, -4 / add r, 10   ->  setg r8 / mov r32, 15 / nop...
    ///   brakes: jle (7E)                                     ->  jmp (EB)
    /// </code>
    /// </summary>
    internal static class GoalLimitBytes
    {
        /// <summary>The limit the patch sets: the most goals the game stores per team (a nibble).</summary>
        internal const short MaxGoals = 15;
        internal const byte Jle = 0x7E;
        internal const byte Jmp = 0xEB;
        private const byte Nop = 0x90;

        // The instructions right before each rewritten byte (cmp word [clock+8], 45 ... setg; cmp word
        // [esi+0xE], 6 / mov [esp+0x30], eax; mov dx, [esi+0x10] / add esp, 4 / cmp dx, 6 / mov ecx, eax).
        // They end before the patched bytes, so they're found patched or not.
        internal static readonly byte[] LimitPattern     = { 0x66, 0x83, 0x7F, 0x08, 0x2D, 0x57, 0x6A, 0x01, 0x0F, 0x9F };
        internal static readonly byte[] HomeBrakePattern = { 0x66, 0x83, 0x7E, 0x0E, 0x06, 0x89, 0x44, 0x24, 0x30 };
        internal static readonly byte[] AwayBrakePattern = { 0x66, 0x8B, 0x56, 0x10, 0x83, 0xC4, 0x04, 0x66, 0x83, 0xFA, 0x06, 0x8B, 0xC8 };

        /// <summary>How many bytes <see cref="Limit"/> needs after the limit pattern.</summary>
        internal const int LimitWindow = 8;

        /// <summary>One rewritable place: where it starts relative to the bytes read, and both versions of its bytes.</summary>
        internal sealed class Site
        {
            public int Offset;
            public byte[] OriginalBytes, PatchedBytes;
            public bool Patched;
        }

        /// <summary>The brake jump in <paramref name="jump"/> (the byte after its pattern), or null if it's neither jle nor jmp.</summary>
        internal static Site Jump(byte jump) =>
            jump != Jle && jump != Jmp ? null : new Site { Offset = 0, OriginalBytes = new[] { Jle }, PatchedBytes = new[] { Jmp }, Patched = jump == Jmp };

        /// <summary>
        /// The limit in <paramref name="bytes"/> (the <see cref="LimitWindow"/> bytes after its pattern,
        /// starting with setg's ModR/M byte C0+r; the rewritten instructions follow it), or null if
        /// they're neither the original nor the patched instructions.
        /// </summary>
        internal static Site Limit(byte[] bytes)
        {
            if (bytes == null || bytes.Length < LimitWindow || bytes[0] < 0xC0 || bytes[0] > 0xC3)
                return null;
            int register = bytes[0] - 0xC0;
            foreach (byte[] original in LimitOriginals(register))
            {
                byte[] patched = LimitPatched(register, original.Length);
                foreach (byte[] candidate in new[] { original, patched })
                {
                    if (bytes.Skip(1).Take(candidate.Length).SequenceEqual(candidate))
                        return new Site { Offset = 1, OriginalBytes = original, PatchedBytes = patched, Patched = candidate == patched };
                }
            }
            return null;
        }

        // dec r32 / and r, -4 / add r, 10 - with eax, the compiler may use the short "and al, -4". The
        // long form comes first: patched, the short form is a prefix of it.
        internal static byte[][] LimitOriginals(int register)
        {
            byte[] add = { 0x83, (byte)(0xC0 + register), 0x0A };
            byte[] longForm = new byte[] { (byte)(0x48 + register), 0x83, (byte)(0xE0 + register), 0xFC }.Concat(add).ToArray();
            return register == 0
                ? new[] { longForm, new byte[] { 0x48, 0x24, 0xFC }.Concat(add).ToArray() }
                : new[] { longForm };
        }

        // mov r32, 15, padded with nops to the original's length.
        internal static byte[] LimitPatched(int register, int length) =>
            new[] { (byte)(0xB8 + register) }.Concat(BitConverter.GetBytes((int)MaxGoals)).Concat(Enumerable.Repeat(Nop, length - 5)).ToArray();
    }
}
