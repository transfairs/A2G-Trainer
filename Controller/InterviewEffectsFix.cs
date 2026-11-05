using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Makes interview answers take effect. The game stores the chosen answer's two effect pairs
    /// (code, value) in the interview engine's heap copy of the context, but applies them from the
    /// original context, whose pairs are never written - so every answer applies "fans +0" twice
    /// (see Settings). The fix redirects that call to a stub which first copies the pairs over, and
    /// maps the two codes whose meaning the texts and the game's cases disagree on: 10 (team morale)
    /// goes to case 12, the game's morale change; 12 (a fine in DM) is marked and booked by a second
    /// entry the call in case 12 is redirected to.
    /// </summary>
    /// <remarks>
    /// The redirects live in the running process only and stay installed; switching the fix off
    /// clears the stub's flag, and the stub then zeroes the pairs as the unpatched game leaves them.
    ///
    /// Stub layout: +0x00 signature, +0x08 flag, +0x20 apply entry, +0xC0 morale entry.
    /// Apply entry (cdecl, the context still on the stack):
    /// <code>
    ///   cmp  byte [flag], 0 / je zero
    ///   mov  eax, [engine] / test / je zero
    ///   mov  eax, [eax+0x11C] / test / je zero    ; the engine's context copy
    ///   per pair: ecx = code, edx = value
    ///             code 12: edx = (edx and 0xFFFFFF) or 0x40000000   ; marked fine
    ///             code 10: ecx = 12                                  ; morale
    ///             store both into the context
    ///   jmp  done
    /// zero:
    ///   xor  ecx, ecx; mov [context+0x14..0x20], ecx
    /// done:
    ///   mov  eax, apply / jmp eax
    /// </code>
    /// Morale entry (cdecl: club, value): a marked value adds DM * [moneyFactor] / 100 to the club's
    /// actual other costs, as the game books its own fines; any other value goes on to the morale change.
    /// </remarks>
    internal sealed class InterviewEffectsFix
    {
        internal static readonly byte[] Signature = Encoding.ASCII.GetBytes("A2GINT02");
        internal const int FlagOffset = 0x08;
        internal const int CodeOffset = 0x20;
        internal const int MoraleCodeOffset = 0xC0;
        internal const int StubSize = 0x100;
        // Within the interview engine: its context copy. Within a context: the two effect pairs.
        internal const uint EngineContextOffset = 0x11C;
        internal const uint EffectsOffset = 0x14;
        // The texts' codes the stub maps (see the class summary), and the game's morale case.
        internal const int TeamMoraleCode = 10;
        internal const int FineCode = 12;
        internal const int MoraleCase = 12;
        // A fine's value in the morale case: the DM in the low 24 bits, this in the high byte.
        internal const uint FineMarker = 0x40000000;

        private readonly ProcessMemory memory;

        /// <summary>Creates the fix for the attached game; it changes nothing until <see cref="SetEnabled"/>.</summary>
        internal InterviewEffectsFix(ProcessMemory memory) => this.memory = memory;

        private FixedAddresses Addresses => FixedAddresses.For(this.memory);

        /// <summary>Whether the fix is available for the attached build.</summary>
        internal bool IsSupported => this.Addresses?.Interview != null;

        /// <summary>Switches the fix on (installing the stub if needed) or off. Returns whether that succeeded.</summary>
        internal bool SetEnabled(bool enabled)
        {
            uint? stub = enabled ? this.EnsureInstalled() : this.FindInstalledStub();
            if (!stub.HasValue)
                return !enabled;
            this.memory.WriteBytesAtAddress(stub.Value + FlagOffset, BitConverter.GetBytes(enabled ? 1u : 0u));
            Logger.Debug($"InterviewEffectsFix: {(enabled ? "on" : "off")} (stub 0x{stub.Value:X}).");
            return true;
        }

        private uint ApplyCallSite => this.memory.ModuleBase + this.Addresses.Interview.ApplyCallSite;
        private uint ApplyFunction => this.memory.ModuleBase + this.Addresses.Interview.ApplyFunction;
        private uint MoraleCallSite => this.memory.ModuleBase + this.Addresses.Interview.MoraleCallSite;
        private uint MoraleFunction => this.memory.ModuleBase + this.Addresses.Interview.MoraleFunction;

        /// <summary>Returns the installed stub, installing it first if both call sites are still original; null if neither applies.</summary>
        internal uint? EnsureInstalled()
        {
            uint? applyTarget = this.ReadCallTarget(() => this.ApplyCallSite);
            uint? moraleTarget = this.ReadCallTarget(() => this.MoraleCallSite);
            if (!applyTarget.HasValue || !moraleTarget.HasValue)
                return null;
            if (applyTarget.Value != this.ApplyFunction)
                return this.StubAt(applyTarget.Value - CodeOffset);
            if (moraleTarget.Value != this.MoraleFunction)
                return null;

            uint? stub = this.memory.AllocateExecutable(StubSize);
            if (!stub.HasValue)
                return null;

            FixedAddresses addresses = this.Addresses;
            byte[] code = BuildStub(stub.Value,
                addresses.Runtime(this.memory, Settings.InterviewEngineOffset),
                addresses.Runtime(this.memory, Settings.InterviewContextOffset),
                this.ApplyFunction,
                addresses.Runtime(this.memory, Settings.MoneyFactorOffset),
                this.MoraleFunction);
            this.memory.WriteBytesAtAddress(stub.Value, code);

            // The morale entry first: it passes everything unmarked on, so it's harmless on its own.
            if (!this.Redirect(this.MoraleCallSite, stub.Value + MoraleCodeOffset) || !this.Redirect(this.ApplyCallSite, stub.Value + CodeOffset))
                return null;

            Logger.Debug($"InterviewEffectsFix: redirected 0x{this.ApplyCallSite:X} and 0x{this.MoraleCallSite:X} to a stub at 0x{stub.Value:X}.");
            return stub;
        }

        private bool Redirect(uint callSite, uint target) =>
            this.memory.WriteCode(callSite + 1, BitConverter.GetBytes(unchecked((int)(target - (callSite + 5)))));

        private uint? FindInstalledStub()
        {
            uint? target = this.ReadCallTarget(() => this.ApplyCallSite);
            return target.HasValue && target.Value != this.ApplyFunction ? this.StubAt(target.Value - CodeOffset) : null;
        }

        // The call site must still be a CALL rel32; returns where it currently goes.
        private uint? ReadCallTarget(Func<uint> callSite)
        {
            if (!this.IsSupported)
                return null;
            uint site = callSite();
            byte[] call = this.memory.ReadBytesAtAddress(site, 5);
            if (call == null || call[0] != 0xE8)
                return null;
            return unchecked((uint)(site + 5 + BitConverter.ToInt32(call, 1)));
        }

        // Accepts a redirected call only if it lands on our own stub (signature check), never on some
        // other tool's patch.
        private uint? StubAt(uint stub)
        {
            byte[] signature = this.memory.ReadBytesAtAddress(stub, Signature.Length);
            return signature != null && signature.SequenceEqual(Signature) ? stub : (uint?)null;
        }

        // x86 code with short forward jumps to labels.
        private sealed class Assembler
        {
            private readonly List<byte> code = new List<byte>();
            private readonly Dictionary<string, int> labels = new Dictionary<string, int>();
            private readonly List<KeyValuePair<int, string>> jumps = new List<KeyValuePair<int, string>>();

            public void Emit(params byte[] bytes) => this.code.AddRange(bytes);
            public void Dword(uint value) => this.code.AddRange(BitConverter.GetBytes(value));
            public void Jump(byte opcode, string label)
            {
                this.Emit(opcode, 0x00);
                this.jumps.Add(new KeyValuePair<int, string>(this.code.Count - 1, label));
            }
            public void Mark(string label) => this.labels[label] = this.code.Count;

            public byte[] ToArray()
            {
                foreach (KeyValuePair<int, string> jump in this.jumps)
                    this.code[jump.Key] = (byte)(this.labels[jump.Value] - (jump.Key + 1));
                return this.code.ToArray();
            }
        }

        /// <summary>Builds the complete stub (data + code) for the given absolute addresses - see the class remarks.</summary>
        internal static byte[] BuildStub(uint stubBase, uint engine, uint context, uint apply, uint moneyFactor, uint morale)
        {
            byte[] stub = new byte[StubSize];
            Array.Copy(Signature, stub, Signature.Length);

            Assembler a = new Assembler();
            a.Emit(0x80, 0x3D); a.Dword(stubBase + FlagOffset); a.Emit(0x00);      // cmp byte [flag], 0
            a.Jump(0x74, "zero");                                                  // je zero
            a.Emit(0xA1); a.Dword(engine);                                         // mov eax, [engine]
            a.Emit(0x85, 0xC0);                                                    // test eax, eax
            a.Jump(0x74, "zero");                                                  // je zero
            a.Emit(0x8B, 0x80); a.Dword(EngineContextOffset);                      // mov eax, [eax+0x11C]
            a.Emit(0x85, 0xC0);                                                    // test eax, eax
            a.Jump(0x74, "zero");                                                  // je zero
            for (byte field = (byte)EffectsOffset; field < EffectsOffset + 0x10; field += 8)
            {
                string store = "store" + field, teamMorale = "teamMorale" + field;
                a.Emit(0x8B, 0x48, field);                                         // mov ecx, [eax+code]
                a.Emit(0x8B, 0x50, (byte)(field + 4));                             // mov edx, [eax+value]
                a.Emit(0x83, 0xF9, FineCode);                                      // cmp ecx, 12
                a.Jump(0x75, teamMorale);                                          // jne teamMorale
                a.Emit(0x81, 0xE2); a.Dword(0x00FFFFFFu);                          // and edx, 0xFFFFFF
                a.Emit(0x81, 0xCA); a.Dword(FineMarker);                           // or edx, marker
                a.Jump(0xEB, store);                                               // jmp store
                a.Mark(teamMorale);
                a.Emit(0x83, 0xF9, TeamMoraleCode);                                // cmp ecx, 10
                a.Jump(0x75, store);                                               // jne store
                a.Emit(0xB1, MoraleCase);                                          // mov cl, 12
                a.Mark(store);
                a.Emit(0x89, 0x0D); a.Dword(context + field);                      // mov [context+code], ecx
                a.Emit(0x89, 0x15); a.Dword(context + field + 4u);                 // mov [context+value], edx
            }
            a.Jump(0xEB, "done");                                                  // jmp done
            a.Mark("zero");
            a.Emit(0x31, 0xC9);                                                    // xor ecx, ecx
            for (uint field = EffectsOffset; field < EffectsOffset + 0x10; field += 4)
            {
                a.Emit(0x89, 0x0D); a.Dword(context + field);                      // mov [context+field], ecx
            }
            a.Mark("done");
            a.Emit(0xB8); a.Dword(apply);                                          // mov eax, apply
            a.Emit(0xFF, 0xE0);                                                    // jmp eax
            byte[] applyCode = a.ToArray();
            if (CodeOffset + applyCode.Length > MoraleCodeOffset)
                throw new InvalidOperationException("InterviewEffectsFix: apply entry too long.");
            applyCode.CopyTo(stub, CodeOffset);

            Assembler m = new Assembler();
            m.Emit(0x80, 0x7C, 0x24, 0x0B, (byte)(FineMarker >> 24));             // cmp byte [esp+0xB], 0x40
            m.Jump(0x75, "morale");                                                // jne morale
            m.Emit(0x8B, 0x44, 0x24, 0x08);                                        // mov eax, [esp+8]
            m.Emit(0x25); m.Dword(0x00FFFFFFu);                                    // and eax, 0xFFFFFF
            m.Emit(0x0F, 0xAF, 0x05); m.Dword(moneyFactor);                        // imul eax, [moneyFactor]
            m.Emit(0x99);                                                          // cdq
            m.Emit(0xB9); m.Dword(100u);                                           // mov ecx, 100
            m.Emit(0xF7, 0xF9);                                                    // idiv ecx
            m.Emit(0x8B, 0x54, 0x24, 0x04);                                        // mov edx, [esp+4]  (club)
            m.Emit(0x85, 0xD2);                                                    // test edx, edx
            m.Jump(0x74, "return");                                                // je return
            m.Emit(0x01, 0x82); m.Dword(Settings.ClubOtherCostsOffset);            // add [edx+0x38C], eax
            m.Mark("return");
            m.Emit(0xC3);                                                          // ret
            m.Mark("morale");
            m.Emit(0xB8); m.Dword(morale);                                         // mov eax, morale
            m.Emit(0xFF, 0xE0);                                                    // jmp eax
            byte[] moraleCode = m.ToArray();
            moraleCode.CopyTo(stub, MoraleCodeOffset);
            return stub;
        }
    }
}
