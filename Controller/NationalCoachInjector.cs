using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Has the game itself make a Mitspieler national coach (or end the job) at the next daily
    /// processing, with its own functions: besides the coach posts these set up the national team's
    /// line-up and tactics, and draw the friendly opponents to choose from - which the game otherwise
    /// only does at the season change, so without them the friendly dates offer no opponents.
    /// </summary>
    /// <remarks>
    /// Same technique as JobOfferInjector, at another call site: the daily call of the offer
    /// generation (see Settings.OfferGenerateCallSiteOffset) is redirected to a stub in the game
    /// process. For every Mitspieler with a pending request the stub ends the current job
    /// (FUN_005cf340, which also clears salary and bonus), takes over the requested national team
    /// (FUN_005cf1e0) and draws the friendly candidates (FUN_005cf3e0), then writes the requested
    /// salary and bonus into the record. Then it jumps on to the offer generation. Country 0 only
    /// ends the job. The redirect lives in the running process only and is set up again after the
    /// game restarts.
    ///
    /// Stub layout: +0x00 signature, +0x08 continue address, from +0x10 one slot per Mitspieler ID
    /// 1-4 (SlotSize bytes: flag, country, salary, bonus, address of the record's salary - 32 bits
    /// each), code from +0x80. Per slot:
    /// <code>
    ///   cmp  byte [flag], 0
    ///   je   next
    ///   mov  byte [flag], 0
    ///   push id / mov eax, leave / call eax / add esp, 4
    ///   mov  eax, [country]
    ///   test eax, eax
    ///   jz   pay
    ///   push eax / push id / mov eax, take / call eax / add esp, 8
    ///   mov  eax, candidates / call eax
    /// pay:
    ///   mov  ecx, [salaryAddress] / mov eax, [salary] / mov [ecx], eax / mov eax, [bonus] / mov [ecx+4], eax
    /// next:
    /// </code>
    /// and after the last slot <c>mov eax, [continue] / jmp eax</c>.
    /// </remarks>
    public sealed class NationalCoachInjector
    {
        internal static readonly byte[] Signature = Encoding.ASCII.GetBytes("A2GNAT01");
        internal const int ContinueOffset = 0x08;
        internal const int SlotsOffset = 0x10;
        internal const int SlotSize = 0x18;
        internal const int FlagOffset = 0x00;
        internal const int CountryOffset = 0x04;
        internal const int SalaryOffset = 0x08;
        internal const int BonusOffset = 0x0C;
        internal const int SalaryAddressOffset = 0x10;
        internal const int CodeOffset = 0x80;
        internal const int StubSize = 0x200;

        private readonly ProcessMemory memory;
        private readonly FixedAddresses fixedAddresses;
        private readonly FixedAddresses.NationalCoachCode code;

        /// <summary>Creates an injector for the given attached game process.</summary>
        public NationalCoachInjector(ProcessMemory memory)
        {
            this.memory = memory;
            this.fixedAddresses = FixedAddresses.For(memory);
            this.code = this.fixedAddresses?.NationalCoach;
        }

        /// <summary>Whether the game's national coach functions can be used for the attached build.</summary>
        public bool IsSupported => this.code != null;

        private uint CallSite => this.memory.ModuleBase + this.code.CallSite;
        private uint ContinueFunction => this.memory.ModuleBase + this.code.Continue;

        private static uint SlotOffset(int trainerIndex) => (uint)(SlotsOffset + trainerIndex * SlotSize);

        /// <summary>
        /// Requests that the trainer in slot <paramref name="trainerIndex"/> becomes national coach of
        /// <paramref name="country"/> (or, with Sonstige, ends the job) at the next daily processing,
        /// with the given contract. Returns false if the redirect couldn't be set up.
        /// </summary>
        public bool Request(int trainerIndex, PlayerEnums.Country country, int salary, int bonus)
        {
            if (!this.IsSupported || trainerIndex < 0 || trainerIndex >= Coach.MaxTrainers)
                return false;
            uint? stub = this.EnsureInstalled();
            if (!stub.HasValue)
                return false;

            uint id = (uint)trainerIndex + 1;
            uint salaryAddress = this.fixedAddresses.Runtime(this.memory, Settings.CoachRecordTableOffset) + id * Settings.CoachRecordStride + Settings.CoachRecordNationalSalaryOffset;
            uint slot = stub.Value + SlotOffset(trainerIndex);
            uint[] values = { (uint)country, unchecked((uint)salary), unchecked((uint)bonus), salaryAddress };
            this.memory.WriteBytesAtAddress(slot + CountryOffset, values.SelectMany(BitConverter.GetBytes).ToArray());
            // Flag last: the stub reads the slot only once it sees the flag.
            this.memory.WriteBytesAtAddress(slot + FlagOffset, BitConverter.GetBytes(1u));
            Logger.Debug($"NationalCoachInjector: trainer slot {trainerIndex} -> national team {country} at the next daily processing (stub 0x{stub.Value:X}).");
            return true;
        }

        /// <summary>The national team requested for the trainer that the game hasn't taken over yet, or null if there's none.</summary>
        public PlayerEnums.Country? PendingCountry(int trainerIndex)
        {
            uint? stub = this.FindInstalledStub();
            if (!stub.HasValue || trainerIndex < 0 || trainerIndex >= Coach.MaxTrainers)
                return null;
            byte[] slot = this.memory.ReadBytesAtAddress(stub.Value + SlotOffset(trainerIndex), 8);
            if (slot == null || slot[FlagOffset] == 0)
                return null;
            return (PlayerEnums.Country)BitConverter.ToUInt32(slot, CountryOffset);
        }

        /// <summary>Where an installed stub whose code starts at <paramref name="codeAddress"/> continues to, or null if there's no stub of ours.</summary>
        internal static uint? ContinueOf(ProcessMemory memory, uint codeAddress)
        {
            uint stub = codeAddress - CodeOffset;
            byte[] header = memory.ReadBytesAtAddress(stub, ContinueOffset + 4);
            if (header == null || !header.Take(Signature.Length).SequenceEqual(Signature))
                return null;
            return BitConverter.ToUInt32(header, ContinueOffset);
        }

        /// <summary>Returns the installed stub, installing it first if the call site still goes to the offer generation; null if neither applies.</summary>
        internal uint? EnsureInstalled()
        {
            uint? target = this.ReadCallTarget();
            if (!target.HasValue)
                return null;
            if (target.Value != this.ContinueFunction)
                return ContinueOf(this.memory, target.Value).HasValue ? target.Value - CodeOffset : (uint?)null;

            uint? stub = this.memory.AllocateExecutable(StubSize);
            if (!stub.HasValue)
                return null;

            this.memory.WriteBytesAtAddress(stub.Value, BuildStub(stub.Value, this.ContinueFunction,
                this.memory.ModuleBase + this.code.Leave, this.memory.ModuleBase + this.code.Take, this.memory.ModuleBase + this.code.Candidates));

            int rel = unchecked((int)(stub.Value + CodeOffset - (this.CallSite + 5)));
            if (!this.memory.WriteCode(this.CallSite + 1, BitConverter.GetBytes(rel)))
                return null;

            Logger.Debug($"NationalCoachInjector: redirected the offer generation at 0x{this.CallSite:X} to a stub at 0x{stub.Value:X}.");
            return stub;
        }

        private uint? FindInstalledStub()
        {
            uint? target = this.ReadCallTarget();
            return target.HasValue && ContinueOf(this.memory, target.Value).HasValue ? target.Value - CodeOffset : (uint?)null;
        }

        // The call site must still be a CALL rel32; returns where it currently goes.
        private uint? ReadCallTarget()
        {
            if (!this.IsSupported)
                return null;
            byte[] call = this.memory.ReadBytesAtAddress(this.CallSite, 5);
            if (call == null || call[0] != 0xE8)
                return null;
            return unchecked((uint)(this.CallSite + 5 + BitConverter.ToInt32(call, 1)));
        }

        /// <summary>Builds the complete stub (data + code) for the given absolute addresses - see the class remarks.</summary>
        internal static byte[] BuildStub(uint stubBase, uint continueTo, uint leave, uint take, uint candidates)
        {
            byte[] stub = new byte[StubSize];
            Array.Copy(Signature, stub, Signature.Length);
            BitConverter.GetBytes(continueTo).CopyTo(stub, ContinueOffset);

            List<byte> code = new List<byte>();
            for (int index = 0; index < Coach.MaxTrainers; index++)
            {
                uint slot = stubBase + SlotOffset(index);
                byte[] id = BitConverter.GetBytes((uint)index + 1);

                code.AddRange(Join(new byte[] { 0x80, 0x3D }, BitConverter.GetBytes(slot + FlagOffset), new byte[] { 0x00 }));   // cmp byte [flag], 0
                int skip = Jump(code, 0x74);                                                                                       // je next
                code.AddRange(Join(new byte[] { 0xC6, 0x05 }, BitConverter.GetBytes(slot + FlagOffset), new byte[] { 0x00 }));   // mov byte [flag], 0
                code.AddRange(Join(new byte[] { 0x68 }, id));                                                                     // push id
                code.AddRange(Join(new byte[] { 0xB8 }, BitConverter.GetBytes(leave), new byte[] { 0xFF, 0xD0, 0x83, 0xC4, 0x04 })); // call leave; add esp, 4
                code.AddRange(Join(new byte[] { 0xA1 }, BitConverter.GetBytes(slot + CountryOffset), new byte[] { 0x85, 0xC0 })); // mov eax, [country]; test eax, eax
                int pay = Jump(code, 0x74);                                                                                        // jz pay
                code.AddRange(Join(new byte[] { 0x50, 0x68 }, id));                                                               // push eax; push id
                code.AddRange(Join(new byte[] { 0xB8 }, BitConverter.GetBytes(take), new byte[] { 0xFF, 0xD0, 0x83, 0xC4, 0x08 })); // call take; add esp, 8
                code.AddRange(Join(new byte[] { 0xB8 }, BitConverter.GetBytes(candidates), new byte[] { 0xFF, 0xD0 }));          // call candidates
                Land(code, pay);
                code.AddRange(Join(new byte[] { 0x8B, 0x0D }, BitConverter.GetBytes(slot + SalaryAddressOffset)));              // mov ecx, [salaryAddress]
                code.AddRange(Join(new byte[] { 0xA1 }, BitConverter.GetBytes(slot + SalaryOffset), new byte[] { 0x89, 0x01 })); // mov eax, [salary]; mov [ecx], eax
                code.AddRange(Join(new byte[] { 0xA1 }, BitConverter.GetBytes(slot + BonusOffset), new byte[] { 0x89, 0x41, 0x04 })); // mov eax, [bonus]; mov [ecx+4], eax
                Land(code, skip);
            }
            code.AddRange(Join(new byte[] { 0xA1 }, BitConverter.GetBytes(stubBase + ContinueOffset), new byte[] { 0xFF, 0xE0 })); // mov eax, [continue]; jmp eax

            code.CopyTo(stub, CodeOffset);
            return stub;
        }

        private static byte[] Join(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

        // A short conditional jump with its distance still open; returns where the distance goes.
        private static int Jump(List<byte> code, byte opcode)
        {
            code.Add(opcode);
            code.Add(0);
            return code.Count - 1;
        }

        // Points the jump whose distance is at displacement here.
        private static void Land(List<byte> code, int displacement) => code[displacement] = checked((byte)(code.Count - displacement - 1));
    }
}
