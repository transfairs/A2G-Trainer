using A2G_Trainer.Model;
using System;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Sets the overconfidence of every match instead of the game's dice: the overall factor (team
    /// block +0x1C, 100 = full strength) that the match set-up lowers to 90/80/70 by chance for a team
    /// much stronger than its opponent. One rule for the human teams, one for their opponents; matches
    /// between computer teams stay as the game sets them up.
    /// </summary>
    /// <remarks>
    /// Both calls of the match set-up go to a stub that calls it and then writes the rule's factor
    /// into each team block - for a human team (club +0x86 set) the own rule, for the opponent of a
    /// human team the opponents' rule; 0 leaves the game's factor. The redirects live in the running
    /// process only and stay installed; switching both rules back to 0 makes the stub a plain pass-through.
    ///
    /// Stub layout: +0x00 signature, +0x08 own factor (byte), +0x09 opponents' factor (byte),
    /// +0x0C the match set-up (absolute), +0x20 code (cdecl: match, home club, away club):
    /// <code>
    ///   push [esp+0xC] x3 / call [setUp] / add esp, 0xC
    ///   per team block (home: own if the home club is human, else opponents' if the away club is;
    ///                   away the other way round):
    ///     movzx eax, byte [rule] / test eax, eax / je next / mov ecx, [esp+4] / mov [ecx+factor], ax
    ///   ret
    /// </code>
    /// </remarks>
    internal sealed class OverconfidenceRule
    {
        internal static readonly byte[] Signature = Encoding.ASCII.GetBytes("A2GOVC01");
        internal const int OwnOffset = 0x08;
        internal const int OpponentOffset = 0x09;
        internal const int FunctionOffset = 0x0C;
        internal const int CodeOffset = 0x20;
        internal const int StubSize = 0x100;
        // Within a match: the home team block +0x54, the away one +0x13C.
        internal const uint HomeFactorOffset = 0x54 + 0x1C;
        internal const uint AwayFactorOffset = 0x13C + 0x1C;

        /// <summary>The factors the rules offer: none, 10, 20 or 30 % overconfidence.</summary>
        internal static readonly byte[] Factors = { 100, 90, 80, 70 };
        /// <summary>The names of <see cref="Factors"/>: how much strength the overconfidence costs.</summary>
        internal static readonly string[] FactorNames = { "Keine", "–10 %", "–20 %", "–30 %" };
        /// <summary>The choices of a rule: the game's dice (0), then <see cref="FactorNames"/>.</summary>
        internal static readonly string[] RuleNames = new[] { "Wie im Spiel" }.Concat(FactorNames).ToArray();

        /// <summary>The rule value of choice <paramref name="index"/> in <see cref="RuleNames"/>.</summary>
        internal static byte RuleValue(int index) => index <= 0 || index > Factors.Length ? (byte)0 : Factors[index - 1];

        /// <summary>The index in <see cref="RuleNames"/> of the rule value <paramref name="factor"/> (0 for the game's dice or an unknown value).</summary>
        internal static int RuleIndex(byte factor) => Array.IndexOf(Factors, factor) + 1;

        private readonly ProcessMemory memory;

        /// <summary>Creates the rule for the attached game; it changes nothing until <see cref="SetRule"/>.</summary>
        internal OverconfidenceRule(ProcessMemory memory) => this.memory = memory;

        private FixedAddresses Addresses => FixedAddresses.For(this.memory);

        /// <summary>Whether the rule is available for the attached build.</summary>
        internal bool IsSupported => this.Addresses?.MatchInit != null;

        private uint Function => this.memory.ModuleBase + this.Addresses.MatchInit.Function;

        /// <summary>
        /// Sets the factor for the human teams and for their opponents (70..100, 0 = the game's dice),
        /// installing the stub if a factor is set. Returns whether that succeeded.
        /// </summary>
        internal bool SetRule(byte own, byte opponent)
        {
            if (!Valid(own) || !Valid(opponent))
                return false;
            bool any = own != 0 || opponent != 0;
            uint? stub = any ? this.EnsureInstalled() : this.FindInstalledStub();
            if (!stub.HasValue)
                return !any;
            this.memory.WriteBytesAtAddress(stub.Value + OwnOffset, new[] { own, opponent });
            Logger.Debug($"OverconfidenceRule: own {own}, opponents {opponent} (stub 0x{stub.Value:X}).");
            return true;
        }

        private static bool Valid(byte factor) => factor == 0 || (factor >= 70 && factor <= 100);

        /// <summary>The rule set in the game (0 = the game's dice); both 0 without a stub.</summary>
        internal void ReadRule(out byte own, out byte opponent)
        {
            uint? stub = this.FindInstalledStub();
            byte[] rule = stub.HasValue ? this.memory.ReadBytesAtAddress(stub.Value + OwnOffset, 2) : null;
            own = rule?[0] ?? 0;
            opponent = rule?[1] ?? 0;
        }

        /// <summary>Returns the installed stub, installing it first if both call sites still go to the match set-up; null if that isn't possible.</summary>
        internal uint? EnsureInstalled()
        {
            uint? existing = this.FindInstalledStub();
            if (existing.HasValue)
                return existing;
            if (!this.IsSupported)
                return null;
            uint[] sites = this.Addresses.MatchInit.CallSites.Select(s => this.memory.ModuleBase + s).ToArray();
            if (sites.Any(s => this.ReadCallTarget(s) != this.Function))
                return null;

            uint? stub = this.memory.AllocateExecutable(StubSize);
            if (!stub.HasValue)
                return null;
            this.memory.WriteBytesAtAddress(stub.Value, BuildStub(stub.Value, this.Function));
            foreach (uint site in sites)
            {
                if (!this.memory.WriteCode(site + 1, BitConverter.GetBytes(unchecked((int)(stub.Value + CodeOffset - (site + 5))))))
                    return null;
            }
            Logger.Debug($"OverconfidenceRule: redirected {string.Join(", ", sites.Select(s => $"0x{s:X}").ToArray())} to a stub at 0x{stub.Value:X}.");
            return stub;
        }

        // The stub the first call site goes to, if it is ours (signature check) - never another tool's patch.
        private uint? FindInstalledStub()
        {
            if (!this.IsSupported)
                return null;
            uint? target = this.ReadCallTarget(this.memory.ModuleBase + this.Addresses.MatchInit.CallSites[0]);
            if (!target.HasValue || target.Value == this.Function)
                return null;
            uint stub = target.Value - CodeOffset;
            byte[] signature = this.memory.ReadBytesAtAddress(stub, Signature.Length);
            return signature != null && signature.SequenceEqual(Signature) ? stub : (uint?)null;
        }

        // Where the CALL rel32 at site goes, or null if there is none.
        private uint? ReadCallTarget(uint site)
        {
            byte[] call = this.memory.ReadBytesAtAddress(site, 5);
            if (call == null || call[0] != 0xE8)
                return null;
            return unchecked((uint)(site + 5 + BitConverter.ToInt32(call, 1)));
        }

        /// <summary>Builds the complete stub (data + code) for the given absolute addresses - see the class remarks.</summary>
        internal static byte[] BuildStub(uint stubBase, uint function)
        {
            byte[] stub = new byte[StubSize];
            Array.Copy(Signature, stub, Signature.Length);
            BitConverter.GetBytes(function).CopyTo(stub, FunctionOffset);

            X86Assembler a = new X86Assembler();
            for (int i = 0; i < 3; i++)
                a.Emit(0xFF, 0x74, 0x24, 0x0C);                                    // push dword [esp+0xC]
            a.Emit(0xFF, 0x15); a.Dword(stubBase + FunctionOffset);                // call [setUp]
            a.Emit(0x83, 0xC4, 0x0C);                                              // add esp, 0xC
            // Home block: the home club at [esp+8], the away club at [esp+0xC]; away block the other way round.
            Team(a, stubBase, "home", 0x08, 0x0C, HomeFactorOffset);
            Team(a, stubBase, "away", 0x0C, 0x08, AwayFactorOffset);
            a.Emit(0xC3);                                                          // ret
            a.ToArray().CopyTo(stub, CodeOffset);
            return stub;
        }

        private static void Team(X86Assembler a, uint stubBase, string name, byte club, byte opponent, uint factor)
        {
            string own = name + "Own", set = name + "Set", done = name + "Done";
            uint human = Settings.ClubHumanOffset;
            a.Emit(0x8B, 0x44, 0x24, club);                                        // mov eax, [esp+club]
            a.Emit(0x80, 0xB8); a.Dword(human); a.Emit(0x00);                      // cmp byte [eax+0x86], 0
            a.Jump(0x75, own);                                                     // jne own
            a.Emit(0x8B, 0x44, 0x24, opponent);                                    // mov eax, [esp+opponent]
            a.Emit(0x80, 0xB8); a.Dword(human); a.Emit(0x00);                      // cmp byte [eax+0x86], 0
            a.Jump(0x74, done);                                                    // je done
            a.Emit(0x0F, 0xB6, 0x05); a.Dword(stubBase + OpponentOffset);          // movzx eax, byte [opponents' rule]
            a.Jump(0xEB, set);                                                     // jmp set
            a.Mark(own);
            a.Emit(0x0F, 0xB6, 0x05); a.Dword(stubBase + OwnOffset);               // movzx eax, byte [own rule]
            a.Mark(set);
            a.Emit(0x85, 0xC0);                                                    // test eax, eax
            a.Jump(0x74, done);                                                    // je done
            a.Emit(0x8B, 0x4C, 0x24, 0x04);                                        // mov ecx, [esp+4]
            a.Emit(0x66, 0x89, 0x81); a.Dword(factor);                             // mov [ecx+factor], ax
            a.Mark(done);
        }
    }
}
