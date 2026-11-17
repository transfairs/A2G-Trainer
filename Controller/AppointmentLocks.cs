using A2G_Trainer.Model;
using System;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Lifts two limits the game sets on arranging negotiations (see docs/savegame-format.md,
    /// "Terminplaner"): a club takes part in at most 3 negotiations a week (FUN_00634720, text 2570),
    /// and a manager gets at most 3 negotiation dates with the players of one club a season (club
    /// +0x1081 + manager, text 2626 "... unter keinen Umständen freigeben").
    /// </summary>
    /// <remarks>
    /// The weekly check is entered through a stub (first 6 bytes replaced by JMP rel32 + NOP):
    /// <code>
    ///   +0x00 signature, +0x08 mode (0 the game's limit, 1 not for human clubs, 2 for no club),
    ///   +0x0C where the check goes on after its first 6 bytes (absolute), +0x20 code:
    ///   cmp byte [mode], 0 / je game / cmp byte [mode], 2 / je free
    ///   mov eax, [esp+8] (club) / cmp byte [eax+0x86], 0 / jne free
    ///   game: sub esp, 8 / push ebx / mov ebx, ecx / jmp [continue]
    ///   free: xor eax, eax / ret 8
    /// </code>
    /// It stays installed; mode 0 makes it a pass-through. The limit per selling club is the
    /// immediate of "cmp byte [...], 3" at two places, set to 0x7F (the counter stops at 10). Both
    /// changes live in the running process only.
    /// </remarks>
    internal sealed class AppointmentLocks
    {
        internal static readonly byte[] Signature = Encoding.ASCII.GetBytes("A2GTRM01");
        internal const int ModeOffset = 0x08;
        internal const int ContinueOffset = 0x0C;
        internal const int CodeOffset = 0x20;
        internal const int StubSize = 0x80;
        /// <summary>The game's limit per selling club.</summary>
        internal const byte ClubLimit = 3;
        /// <summary>The limit per selling club while lifted (more than the counter reaches).</summary>
        internal const byte ClubLimitLifted = 0x7F;

        /// <summary>The weekly limit's choices; the index is the stub's mode.</summary>
        internal static readonly string[] WeeklyModeNames = { "Wie im Spiel", "Für eigene Vereine aufheben", "Für alle Vereine aufheben" };

        private readonly ProcessMemory memory;

        /// <summary>Creates the locks for the attached game; nothing changes until a Set call.</summary>
        internal AppointmentLocks(ProcessMemory memory) => this.memory = memory;

        private FixedAddresses.AppointmentLockCode Code => FixedAddresses.For(this.memory)?.AppointmentLock;

        /// <summary>Whether the locks can be changed in the attached build.</summary>
        internal bool IsSupported => this.Code != null;

        private uint Function => this.memory.ModuleBase + this.Code.WeeklyFunction;

        /// <summary>The weekly limit's mode in the game (0 without the stub).</summary>
        internal byte WeeklyMode
        {
            get
            {
                uint? stub = this.FindInstalledStub();
                byte[] mode = stub.HasValue ? this.memory.ReadBytesAtAddress(stub.Value + ModeOffset, 1) : null;
                return mode?[0] ?? 0;
            }
        }

        /// <summary>Sets the weekly limit's mode (index in <see cref="WeeklyModeNames"/>), installing the stub for 1 or 2. Returns whether that succeeded.</summary>
        internal bool SetWeeklyMode(byte mode)
        {
            if (mode >= WeeklyModeNames.Length)
                return false;
            uint? stub = mode != 0 ? this.EnsureInstalled() : this.FindInstalledStub();
            if (!stub.HasValue)
                return mode == 0;
            this.memory.WriteBytesAtAddress(stub.Value + ModeOffset, new[] { mode });
            Logger.Debug($"AppointmentLocks: weekly mode {mode} (stub 0x{stub.Value:X}).");
            return true;
        }

        /// <summary>Returns the installed stub, installing it if the check still starts as in the game; null if that isn't possible.</summary>
        internal uint? EnsureInstalled()
        {
            uint? existing = this.FindInstalledStub();
            if (existing.HasValue)
                return existing;
            if (!this.IsSupported)
                return null;
            byte[] entry = this.memory.ReadBytesAtAddress(this.Function, Settings.WeeklyLimitEntry.Length);
            if (entry == null || !entry.SequenceEqual(Settings.WeeklyLimitEntry))
            {
                Logger.Warn("AppointmentLocks: Wochenprüfung beginnt nicht wie erwartet - nichts geändert.");
                return null;
            }

            uint? stub = this.memory.AllocateExecutable(StubSize);
            if (!stub.HasValue)
                return null;
            this.memory.WriteBytesAtAddress(stub.Value, BuildStub(stub.Value, this.Function + (uint)Settings.WeeklyLimitEntry.Length));
            byte[] jump = new byte[] { 0xE9 }
                .Concat(BitConverter.GetBytes(unchecked((int)(stub.Value + CodeOffset - (this.Function + 5)))))
                .Concat(new byte[] { 0x90 })
                .ToArray();
            if (!this.memory.WriteCode(this.Function, jump))
                return null;
            Logger.Debug($"AppointmentLocks: weekly check 0x{this.Function:X} redirected to a stub at 0x{stub.Value:X}.");
            return stub;
        }

        // The stub the weekly check jumps to, if it is ours (signature check) - never another tool's patch.
        private uint? FindInstalledStub()
        {
            if (!this.IsSupported)
                return null;
            byte[] jump = this.memory.ReadBytesAtAddress(this.Function, 5);
            if (jump == null || jump[0] != 0xE9)
                return null;
            uint stub = unchecked((uint)(this.Function + 5 + BitConverter.ToInt32(jump, 1)) - CodeOffset);
            byte[] signature = this.memory.ReadBytesAtAddress(stub, Signature.Length);
            return signature != null && signature.SequenceEqual(Signature) ? stub : (uint?)null;
        }

        /// <summary>Builds the stub (data + code) for the given absolute addresses - see the class remarks.</summary>
        internal static byte[] BuildStub(uint stubBase, uint resume)
        {
            byte[] stub = new byte[StubSize];
            Array.Copy(Signature, stub, Signature.Length);
            BitConverter.GetBytes(resume).CopyTo(stub, ContinueOffset);

            X86Assembler a = new X86Assembler();
            a.Emit(0x80, 0x3D); a.Dword(stubBase + ModeOffset); a.Emit(0x00);      // cmp byte [mode], 0
            a.Jump(0x74, "game");                                                  // je game
            a.Emit(0x80, 0x3D); a.Dword(stubBase + ModeOffset); a.Emit(0x02);      // cmp byte [mode], 2
            a.Jump(0x74, "free");                                                  // je free
            a.Emit(0x8B, 0x44, 0x24, 0x08);                                        // mov eax, [esp+8]
            a.Emit(0x80, 0xB8); a.Dword(Settings.ClubHumanOffset); a.Emit(0x00);   // cmp byte [eax+0x86], 0
            a.Jump(0x75, "free");                                                  // jne free
            a.Mark("game");
            a.Emit(Settings.WeeklyLimitEntry);                                     // sub esp, 8 / push ebx / mov ebx, ecx
            a.Emit(0xFF, 0x25); a.Dword(stubBase + ContinueOffset);                // jmp [continue]
            a.Mark("free");
            a.Emit(0x33, 0xC0);                                                    // xor eax, eax
            a.Emit(0xC2, 0x08, 0x00);                                              // ret 8
            a.ToArray().CopyTo(stub, CodeOffset);
            return stub;
        }

        /// <summary>Whether the limit per selling club is lifted in the game right now.</summary>
        internal bool IsClubLimitLifted => this.ReadClubLimits()?.All(l => l == ClubLimitLifted) == true;

        /// <summary>Lifts the limit per selling club or restores it. Returns whether that succeeded.</summary>
        internal bool SetClubLimitLifted(bool lifted)
        {
            byte[] limits = this.ReadClubLimits();
            if (limits == null)
                return !lifted;
            uint[] sites = this.Code.ClubLimitSites;
            byte value = lifted ? ClubLimitLifted : ClubLimit;
            for (int i = 0; i < sites.Length; i++)
            {
                if (limits[i] != value && !this.memory.WriteCode(this.memory.ModuleBase + sites[i], new[] { value }))
                    return false;
            }
            Logger.Debug($"AppointmentLocks: club limit {(lifted ? "lifted" : "restored")}.");
            return true;
        }

        // The limit byte at both sites, or null if a site can't be read or holds something else.
        private byte[] ReadClubLimits()
        {
            FixedAddresses.AppointmentLockCode code = this.Code;
            if (code == null)
                return null;
            byte[] limits = code.ClubLimitSites.Select(s => this.memory.ReadBytesAtAddress(this.memory.ModuleBase + s, 1)?[0] ?? (byte)0).ToArray();
            if (limits.Any(l => l != ClubLimit && l != ClubLimitLifted))
            {
                Logger.Warn("AppointmentLocks: Grenze je Verein nicht wie erwartet - nichts geändert.");
                return null;
            }
            return limits;
        }
    }
}
