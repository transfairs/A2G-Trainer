using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Moves a player to another club at the next daily processing - as a transfer, a loan or a loan
    /// with buy option - through the game's own transfer functions, so squads, line-ups, money,
    /// transfer history, news and the loan's home club come out exactly as for a move the game made.
    /// </summary>
    /// <remarks>
    /// Same approach as JobOfferInjector: a stub is placed in the game process and both calls of the
    /// daily transfer-market resolution (Settings.TransferMarketCallSiteOffsets) are redirected to
    /// it. With a request pending, the stub runs the game's own sequence (see Settings) and then
    /// jumps on to the original market resolution; without one it only jumps on. The stub refuses
    /// the move if the player isn't in the old club's squad any more or the new squad is full (30,
    /// which the game's move function doesn't check itself) and leaves the outcome for the Trainer.
    ///
    /// "sofort": after day 216 the game's move function only books a transfer for the season end.
    /// Its one call of the deadline check (Settings.TransferDeadlineCallSiteOffset) is redirected
    /// to a second entry of the stub, which answers "before the deadline" while the stub's own
    /// immediate move runs and otherwise just continues to the original check. Loans have no
    /// deadline. Original build only; the redirects live in the running process only.
    ///
    /// Stub layout: +0x00 signature, +0x08 request flag, +0x0C outcome, +0x10 arguments (each a
    /// dword: player ID, old country, old club, new country, new club, kind, fee, buy price,
    /// immediate), +0x34 "immediate move running", +0x38 contract (0x10 bytes), +0x50 deadline
    /// entry, +0x70 market entry.
    /// </remarks>
    public sealed class TransferInjector
    {
        /// <summary>How the player moves; the values are what the stub passes on to the game.</summary>
        public enum Kind
        {
            /// <summary>A normal transfer (FUN_005fb480).</summary>
            Transfer = 0,
            /// <summary>A loan without buy option (FUN_005fb710).</summary>
            Loan = 1,
            /// <summary>A loan with buy option (FUN_005fb710, kind 3).</summary>
            LoanWithBuyOption = 3
        }

        /// <summary>Outcome of <see cref="Request"/>.</summary>
        public enum RequestResult
        {
            /// <summary>The move will be made at the next daily processing.</summary>
            Requested,
            /// <summary>Only available for the original build.</summary>
            NotSupported,
            /// <summary>The player has no club to leave.</summary>
            NoClub,
            /// <summary>The target is the player's current club.</summary>
            SameClub,
            /// <summary>The redirects couldn't be set up (unexpected code at a call site, or memory not writable).</summary>
            HookFailed
        }

        /// <summary>What the stub did with the last request, as left in the game process.</summary>
        public enum Outcome
        {
            /// <summary>Nothing to report (no request run yet, or already taken).</summary>
            None = 0,
            /// <summary>The player was moved (or, after the deadline without "sofort", booked for the season end).</summary>
            Done = 1,
            /// <summary>The player wasn't in the old club's squad any more.</summary>
            NotInClub = 2,
            /// <summary>The new club's squad was full.</summary>
            SquadFull = 3
        }

        internal static readonly byte[] Signature = Encoding.ASCII.GetBytes("A2GXFR01");
        internal const int FlagOffset = 0x08;
        internal const int OutcomeOffset = 0x0C;
        internal const int ArgumentsOffset = 0x10;
        internal const int ImmediateActiveOffset = 0x34;
        internal const int ContractOffset = 0x38;
        internal const int DeadlineCodeOffset = 0x50;
        internal const int CodeOffset = 0x70;
        internal const int StubSize = 0x200;

        // Argument slots (dwords from ArgumentsOffset).
        internal const int ArgPlayer = 0, ArgOldCountry = 1, ArgOldClub = 2, ArgNewCountry = 3, ArgNewClub = 4,
            ArgKind = 5, ArgFee = 6, ArgBuyPrice = 7, ArgImmediate = 8;
        private const int ArgumentCount = 9;
        // Contract byte the game raises to at least 2 (years) for its own transfers.
        private const int ContractYearsOffset = 0xC;

        private readonly ProcessMemory memory;

        /// <summary>Creates an injector for the given attached game process.</summary>
        public TransferInjector(ProcessMemory memory)
        {
            this.memory = memory;
        }

        /// <summary>Whether moves can be requested for the attached build (original build only).</summary>
        public bool IsSupported => this.memory.Layout.HasOriginalBuildAnchors;

        private uint Module(uint offset) => this.memory.ModuleBase + offset;
        private uint MarketFunction => this.Module(Settings.TransferMarketFunctionOffset);
        private uint DeadlineFunction => this.Module(Settings.TransferDeadlineFunctionOffset);

        /// <summary>
        /// Requests moving <paramref name="player"/> from his current club to <paramref name="clubId"/>
        /// in <paramref name="country"/>. Fee and buy price are in x100 DM like everywhere in the
        /// Trainer; the buy price only counts for a loan with buy option. <paramref name="immediate"/>
        /// makes a transfer happen right away even after the deadline. A new request replaces one
        /// that hasn't run yet.
        /// </summary>
        public RequestResult Request(Player player, PlayerEnums.Country country, byte clubId, Kind kind, uint fee, uint buyPrice, bool immediate)
        {
            if (!this.IsSupported)
                return RequestResult.NotSupported;
            if (player.ClubCountry == PlayerEnums.Country.Sonstige)
                return RequestResult.NoClub;
            if (player.ClubCountry == country && player.ClubId == clubId)
                return RequestResult.SameClub;

            uint? stub = this.EnsureInstalled();
            if (!stub.HasValue)
                return RequestResult.HookFailed;

            uint factor = this.ReadMoneyFactor();
            uint[] arguments =
            {
                player.NameRecordId, (uint)player.ClubCountry, player.ClubId, (uint)country, clubId,
                (uint)kind, ToMoney(fee, factor), kind == Kind.LoanWithBuyOption ? ToMoney(buyPrice, factor) : 0,
                immediate && kind == Kind.Transfer ? 1u : 0u
            };
            // Flag cleared first so the stub can't pick up half-written arguments of a replaced request.
            this.memory.WriteBytesAtAddress(stub.Value + FlagOffset, BitConverter.GetBytes(0u));
            this.memory.WriteBytesAtAddress(stub.Value + OutcomeOffset, BitConverter.GetBytes(0u));
            this.memory.WriteBytesAtAddress(stub.Value + ArgumentsOffset, arguments.SelectMany(BitConverter.GetBytes).ToArray());
            this.memory.WriteBytesAtAddress(stub.Value + FlagOffset, BitConverter.GetBytes(1u));
            Logger.Debug($"TransferInjector: {kind} of player {player.NameRecordId} from {player.ClubCountry}/{player.ClubId} to {country}/{clubId} requested (fee {fee}, buy price {buyPrice}, immediate {immediate}, stub 0x{stub.Value:X}).");
            return RequestResult.Requested;
        }

        // The game's fees are money units; the Trainer shows x100 DM, i.e. units of DAT_0078bbb8.
        private uint ReadMoneyFactor()
        {
            byte[] factor = this.memory.ReadBytesAtAddress(this.Module(Settings.MoneyFactorOffset), 4);
            int value = factor != null ? BitConverter.ToInt32(factor, 0) : 0;
            return value > 0 ? (uint)value : 100u;
        }

        internal static uint ToMoney(uint hundreds, uint factor) => (uint)Math.Min((ulong)hundreds * factor, int.MaxValue);

        /// <summary>Whether a requested move hasn't been made yet (the next daily processing hasn't run).</summary>
        public bool IsRequestPending()
        {
            uint? stub = this.FindInstalledStub();
            if (!stub.HasValue)
                return false;
            byte[] flag = this.memory.ReadBytesAtAddress(stub.Value + FlagOffset, 1);
            return flag != null && flag[0] != 0;
        }

        /// <summary>Returns what the last request came to and clears it, so each outcome is reported once.</summary>
        public Outcome TakeOutcome()
        {
            uint? stub = this.FindInstalledStub();
            if (!stub.HasValue || this.IsRequestPending())
                return Outcome.None;
            byte[] outcome = this.memory.ReadBytesAtAddress(stub.Value + OutcomeOffset, 4);
            if (outcome == null || BitConverter.ToUInt32(outcome, 0) == 0)
                return Outcome.None;
            this.memory.WriteBytesAtAddress(stub.Value + OutcomeOffset, BitConverter.GetBytes(0u));
            return (Outcome)BitConverter.ToUInt32(outcome, 0);
        }

        /// <summary>Returns the installed stub, installing it (or finishing a partial install) first; null if a call site isn't as expected.</summary>
        internal uint? EnsureInstalled()
        {
            if (!this.IsSupported)
                return null;

            uint[] marketSites = Settings.TransferMarketCallSiteOffsets.Select(this.Module).ToArray();
            uint deadlineSite = this.Module(Settings.TransferDeadlineCallSiteOffset);
            uint?[] marketTargets = marketSites.Select(this.ReadCallTarget).ToArray();
            uint? deadlineTarget = this.ReadCallTarget(deadlineSite);
            if (marketTargets.Any(t => !t.HasValue) || !deadlineTarget.HasValue)
                return null;

            // Every site must go either to the original function or into one and the same stub of ours.
            uint? stub = null;
            foreach (uint target in marketTargets.Select(t => t.Value).Where(t => t != this.MarketFunction))
            {
                uint? found = this.StubAt(target - CodeOffset);
                if (!found.HasValue || (stub.HasValue && stub != found))
                    return null;
                stub = found;
            }
            if (deadlineTarget.Value != this.DeadlineFunction)
            {
                uint? found = this.StubAt(deadlineTarget.Value - DeadlineCodeOffset);
                if (!found.HasValue || (stub.HasValue && stub != found))
                    return null;
                stub = found;
            }

            if (!stub.HasValue)
            {
                stub = this.memory.AllocateExecutable(StubSize);
                if (!stub.HasValue)
                    return null;
                this.memory.WriteBytesAtAddress(stub.Value, BuildStub(stub.Value, this.memory.ModuleBase));
            }

            // Deadline first: it only matters once the stub runs, which the market redirects start.
            if (deadlineTarget.Value == this.DeadlineFunction && !this.Redirect(deadlineSite, stub.Value + DeadlineCodeOffset))
                return null;
            for (int i = 0; i < marketSites.Length; i++)
            {
                if (marketTargets[i].Value == this.MarketFunction && !this.Redirect(marketSites[i], stub.Value + CodeOffset))
                    return null;
            }
            Logger.Debug($"TransferInjector: transfer stub at 0x{stub.Value:X}.");
            return stub;
        }

        private bool Redirect(uint callSite, uint target)
        {
            int rel = unchecked((int)(target - (callSite + 5)));
            bool written = this.memory.WriteCode(callSite + 1, BitConverter.GetBytes(rel));
            if (written)
                Logger.Debug($"TransferInjector: redirected the call at 0x{callSite:X} to 0x{target:X}.");
            return written;
        }

        private uint? FindInstalledStub()
        {
            if (!this.IsSupported)
                return null;
            uint? target = this.ReadCallTarget(this.Module(Settings.TransferMarketCallSiteOffsets[0]));
            return target.HasValue && target.Value != this.MarketFunction ? this.StubAt(target.Value - CodeOffset) : null;
        }

        // The call site must still be a CALL rel32; returns where it currently goes.
        private uint? ReadCallTarget(uint callSite)
        {
            byte[] call = this.memory.ReadBytesAtAddress(callSite, 5);
            if (call == null || call[0] != 0xE8)
                return null;
            return unchecked((uint)(callSite + 5 + BitConverter.ToInt32(call, 1)));
        }

        // Accepts a redirected call only if it lands in our own stub (signature check), never in some other tool's patch.
        private uint? StubAt(uint stub)
        {
            byte[] signature = this.memory.ReadBytesAtAddress(stub, Signature.Length);
            return signature != null && signature.SequenceEqual(Signature) ? stub : (uint?)null;
        }

        /// <summary>Builds the complete stub (data + code) for the game module at <paramref name="moduleBase"/> - see the class remarks.</summary>
        internal static byte[] BuildStub(uint stubBase, uint moduleBase) =>
            BuildStub(stubBase, new StubTargets
            {
                Market = moduleBase + Settings.TransferMarketFunctionOffset,
                Deadline = moduleBase + Settings.TransferDeadlineFunctionOffset,
                FetchPlayer = moduleBase + Settings.TransferFetchPlayerOffset,
                ReleasePlayer = moduleBase + Settings.TransferReleasePlayerOffset,
                FetchClub = moduleBase + Settings.TransferFetchClubOffset,
                ReleaseClub = moduleBase + Settings.TransferReleaseClubOffset,
                BuildContract = moduleBase + Settings.TransferBuildContractOffset,
                Move = moduleBase + Settings.TransferMoveOffset,
                Loan = moduleBase + Settings.TransferLoanOffset,
            });

        /// <summary>Absolute addresses of the game functions the stub calls.</summary>
        internal sealed class StubTargets
        {
            public uint Market, Deadline, FetchPlayer, ReleasePlayer, FetchClub, ReleaseClub, BuildContract, Move, Loan;
        }

        internal static byte[] BuildStub(uint stubBase, StubTargets t)
        {
            byte[] stub = new byte[StubSize];
            Array.Copy(Signature, stub, Signature.Length);

            uint flag = stubBase + FlagOffset;
            uint outcome = stubBase + OutcomeOffset;
            uint active = stubBase + ImmediateActiveOffset;
            uint contract = stubBase + ContractOffset;
            uint Arg(int slot) => stubBase + ArgumentsOffset + (uint)slot * 4;

            // Deadline entry: "before the deadline" (0) while our immediate move runs, else the original check.
            X86 d = new X86();
            d.Op(0x80, 0x3D).U32(active).Op(0x00);                    // cmp byte [active], 0
            d.Jcc(0x85, "zero");                                      // jne zero
            d.Op(0xB8).U32(t.Deadline).Op(0xFF, 0xE0);                // mov eax, deadline; jmp eax
            d.Label("zero").Op(0x31, 0xC0, 0xC3);                     // xor eax, eax; ret
            byte[] deadlineCode = d.Build();

            X86 c = new X86();
            c.Op(0x80, 0x3D).U32(flag).Op(0x00);                      // cmp byte [flag], 0
            c.Jcc(0x84, "done");                                      // je done
            c.Op(0x60);                                               // pushad
            c.Op(0xC6, 0x05).U32(flag).Op(0x00);                      // mov byte [flag], 0
            c.Op(0xC7, 0x05).U32(outcome).U32((uint)Outcome.NotInClub);

            // esi = old club, edi = new club (FUN_00585b00(country, club, 0xFFFF))
            c.Op(0x68).U32(0xFFFF).Op(0xFF, 0x35).U32(Arg(ArgOldClub)).Op(0xFF, 0x35).U32(Arg(ArgOldCountry));
            c.Call(t.FetchClub, 3).Op(0x89, 0xC6);                    // mov esi, eax
            c.Op(0x68).U32(0xFFFF).Op(0xFF, 0x35).U32(Arg(ArgNewClub)).Op(0xFF, 0x35).U32(Arg(ArgNewCountry));
            c.Call(t.FetchClub, 3).Op(0x89, 0xC7);                    // mov edi, eax

            // Is the player in the old club's squad? (ebx = found)
            c.Op(0x0F, 0xBF, 0x8E).U32(Settings.ClubSquadCountOffset); // movsx ecx, word [esi+count]
            c.Op(0x8B, 0x15).U32(Arg(ArgPlayer));                     // mov edx, [player]
            c.Op(0x31, 0xDB);                                         // xor ebx, ebx
            c.Label("search").Op(0x85, 0xC9);                         // test ecx, ecx
            c.Jcc(0x8E, "searched");                                  // jle searched
            c.Op(0x49);                                               // dec ecx
            c.Op(0x8B, 0x84, 0x8E).U32(Settings.ClubSquadOffset);     // mov eax, [esi+ecx*4+squad]
            c.Op(0x66, 0x39, 0x10);                                   // cmp [eax], dx
            c.Jcc(0x85, "search");                                    // jne search
            c.Op(0x43);                                               // inc ebx
            c.Label("searched").Op(0x85, 0xDB);                       // test ebx, ebx
            c.Jcc(0x84, "releaseClubs");                              // je releaseClubs
            c.Op(0xC7, 0x05).U32(outcome).U32((uint)Outcome.SquadFull);
            c.Op(0x66, 0x81, 0xBF).U32(Settings.ClubSquadCountOffset).U16((ushort)Settings.ClubSquadCapacity); // cmp word [edi+count], 30
            c.Jcc(0x8D, "releaseClubs");                              // jge releaseClubs

            // ebx = player (FUN_00582740(id)); contract built by the game for the new club
            c.Op(0xFF, 0x35).U32(Arg(ArgPlayer)).Call(t.FetchPlayer, 1).Op(0x89, 0xC3);
            c.Op(0x31, 0xC0);                                         // xor eax, eax
            for (uint i = 0; i < 0x10; i += 4)
                c.Op(0xA3).U32(contract + i);                         // mov [contract+i], eax
            c.Op(0x57, 0x53, 0x68).U32(contract);                     // push edi; push ebx; push contract
            c.Call(t.BuildContract, 3);
            c.Op(0x80, 0x3D).U32(contract + ContractYearsOffset).Op(0x02); // cmp byte [years], 2
            c.Jcc(0x8D, "yearsOk");                                   // jge yearsOk
            c.Op(0xC6, 0x05).U32(contract + ContractYearsOffset).Op(0x02);
            c.Label("yearsOk");
            c.Op(0xA1).U32(Arg(ArgImmediate)).Op(0xA3).U32(active);   // [active] = immediate

            c.Op(0x83, 0x3D).U32(Arg(ArgKind)).Op(0x00);              // cmp dword [kind], 0
            c.Jcc(0x85, "loan");                                      // jne loan
            // FUN_005fb480(player, new, old, fee, &contract)
            c.Op(0x68).U32(contract).Op(0xFF, 0x35).U32(Arg(ArgFee)).Op(0x56, 0x57, 0x53);
            c.Call(t.Move, 5);
            c.Jmp("moved");
            // FUN_005fb710(player, new, old, fee, buy price, &contract, kind)
            c.Label("loan").Op(0xFF, 0x35).U32(Arg(ArgKind)).Op(0x68).U32(contract);
            c.Op(0xFF, 0x35).U32(Arg(ArgBuyPrice)).Op(0xFF, 0x35).U32(Arg(ArgFee)).Op(0x56, 0x57, 0x53);
            c.Call(t.Loan, 7);
            c.Label("moved").Op(0x31, 0xC0).Op(0xA3).U32(active);    // [active] = 0
            c.Op(0xC7, 0x05).U32(outcome).U32((uint)Outcome.Done);
            c.Op(0x53).Call(t.ReleasePlayer, 1);                      // FUN_00582870(player)

            c.Label("releaseClubs").Op(0x57).Call(t.ReleaseClub, 1);  // FUN_00585c50(new)
            c.Op(0x56).Call(t.ReleaseClub, 1);                        // FUN_00585c50(old)
            c.Op(0x61);                                               // popad
            c.Label("done").Op(0xB8).U32(t.Market).Op(0xFF, 0xE0);    // mov eax, market; jmp eax
            byte[] code = c.Build();

            if (DeadlineCodeOffset + deadlineCode.Length > CodeOffset || CodeOffset + code.Length > StubSize)
                throw new InvalidOperationException("Transfer stub doesn't fit.");
            Array.Copy(deadlineCode, 0, stub, DeadlineCodeOffset, deadlineCode.Length);
            Array.Copy(code, 0, stub, CodeOffset, code.Length);
            return stub;
        }

        // Minimal x86 emitter: raw bytes, labels and rel32 jumps resolved at Build.
        private sealed class X86
        {
            private readonly List<byte> bytes = new List<byte>();
            private readonly Dictionary<string, int> labels = new Dictionary<string, int>();
            private readonly List<KeyValuePair<int, string>> fixups = new List<KeyValuePair<int, string>>();

            public X86 Op(params byte[] op) { this.bytes.AddRange(op); return this; }
            public X86 U32(uint value) { this.bytes.AddRange(BitConverter.GetBytes(value)); return this; }
            public X86 U16(ushort value) { this.bytes.AddRange(BitConverter.GetBytes(value)); return this; }
            public X86 Label(string name) { this.labels[name] = this.bytes.Count; return this; }

            // Jcc rel32 (0F cc).
            public X86 Jcc(byte condition, string label) => this.Op(0x0F, condition).Rel(label);
            public X86 Jmp(string label) => this.Op(0xE9).Rel(label);

            // mov eax, target; call eax; add esp, 4 * arguments (cdecl).
            public X86 Call(uint target, int arguments) => this.Op(0xB8).U32(target).Op(0xFF, 0xD0, 0x83, 0xC4, (byte)(arguments * 4));

            private X86 Rel(string label)
            {
                this.fixups.Add(new KeyValuePair<int, string>(this.bytes.Count, label));
                return this.U32(0);
            }

            public byte[] Build()
            {
                byte[] code = this.bytes.ToArray();
                foreach (KeyValuePair<int, string> fixup in this.fixups)
                {
                    int rel = this.labels[fixup.Value] - (fixup.Key + 4);
                    Array.Copy(BitConverter.GetBytes(rel), 0, code, fixup.Key, 4);
                }
                return code;
            }
        }
    }
}
