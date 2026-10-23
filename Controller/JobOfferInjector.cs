using A2G_Trainer_XP.Model;
using System;
using System.Linq;
using System.Text;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// Makes the game offer a human trainer a job at a chosen club at the next daily processing,
    /// as a regular in-game job offer the player then accepts ("Sofort" or "Saisonende") or declines -
    /// the game itself carries out the club change, exactly as for an offer it made on its own.
    /// </summary>
    /// <remarks>
    /// The game generates and presents offers back to back inside one daily-processing call (see
    /// Settings.JobOfferPresentCallSiteOffset) and empties its offer lists first, so there is no
    /// moment where writing an entry from outside would survive. Instead a small stub is placed in
    /// the game process and the presentation call is redirected to it: if a request is pending, the
    /// stub adds one offer through the game's own add-offer function, clears the request, and then
    /// jumps on to the original presentation. Without a request it only jumps on. The redirect
    /// lives in the running process only (nothing on disk changes) and is set up again after the
    /// game restarts. Original build only.
    ///
    /// Stub layout: +0x00 signature, +0x08 request flag, +0x0C Mitspieler ID, +0x10 country,
    /// +0x14 club ID, +0x18 offer kind (each a 32-bit stack argument), +0x20 code:
    /// <code>
    ///   cmp  byte [flag], 0
    ///   je   done
    ///   mov  byte [flag], 0
    ///   push dword [kind] / [club] / [country] / [id]
    ///   mov  ecx, offerList
    ///   mov  eax, addOffer
    ///   call eax                  ; __thiscall, pops its 4 arguments (RET 0x10)
    /// done:
    ///   mov  eax, presentOffers
    ///   jmp  eax                  ; returns straight to the original caller
    /// </code>
    /// </remarks>
    public sealed class JobOfferInjector
    {
        /// <summary>Outcome of <see cref="Request"/>.</summary>
        public enum RequestResult
        {
            /// <summary>The offer will be made at the next daily processing.</summary>
            Requested,
            /// <summary>Only available for the original build.</summary>
            NotSupported,
            /// <summary>The trainer has no club right now - the game only presents offers to employed trainers.</summary>
            NotEmployed,
            /// <summary>A club change for next season is already set - the game doesn't present offers then.</summary>
            ChangePending,
            /// <summary>The requested club is the trainer's current club.</summary>
            SameClub,
            /// <summary>The redirect couldn't be set up (unexpected code at the call site, or memory not writable).</summary>
            HookFailed
        }

        internal static readonly byte[] Signature = Encoding.ASCII.GetBytes("A2GJOB01");
        internal const int FlagOffset = 0x08;
        internal const int ArgumentsOffset = 0x0C;
        internal const int CodeOffset = 0x20;
        internal const int StubSize = 0x60;
        // Kind the game passes for every job offer it generates (FUN_00625dc0/FUN_00626090).
        private const uint OfferKind = 3;

        private readonly ProcessMemory memory;
        // The attached build's addresses (see FixedAddresses); either is null if not known.
        private readonly FixedAddresses fixedAddresses;
        private readonly FixedAddresses.JobOfferCode code;

        /// <summary>Creates an injector for the given attached game process.</summary>
        public JobOfferInjector(ProcessMemory memory)
        {
            this.memory = memory;
            this.fixedAddresses = FixedAddresses.For(memory);
            this.code = this.fixedAddresses?.JobOffer;
        }

        /// <summary>Whether job offers can be requested for the attached build.</summary>
        public bool IsSupported => this.code != null;

        private uint CallSite => this.memory.ModuleBase + this.code.PresentCallSite;
        private uint PresentFunction => this.memory.ModuleBase + this.code.PresentFunction;

        /// <summary>
        /// Requests a job offer from <paramref name="clubId"/> in <paramref name="country"/> for the
        /// trainer in slot <paramref name="trainerIndex"/> (Mitspieler ID = slot + 1), checked against
        /// the conditions under which the game presents offers at all.
        /// </summary>
        public RequestResult Request(Coach coach, int trainerIndex, PlayerEnums.Country country, byte clubId)
        {
            if (!this.IsSupported)
                return RequestResult.NotSupported;
            if (coach.CurrentCountry == PlayerEnums.Country.Sonstige)
                return RequestResult.NotEmployed;
            if (coach.CountryNextSeason != PlayerEnums.Country.Sonstige)
                return RequestResult.ChangePending;
            if (coach.CurrentCountry == country && coach.CurrentClubId == clubId)
                return RequestResult.SameClub;

            uint? stub = this.EnsureInstalled();
            if (!stub.HasValue)
                return RequestResult.HookFailed;

            uint[] arguments = { (uint)trainerIndex + 1, (uint)country, clubId, OfferKind };
            this.memory.WriteBytesAtAddress(stub.Value + ArgumentsOffset, arguments.SelectMany(BitConverter.GetBytes).ToArray());
            // Flag last: the stub reads the arguments only once it sees the flag.
            this.memory.WriteBytesAtAddress(stub.Value + FlagOffset, BitConverter.GetBytes(1u));
            Logger.Debug($"JobOfferInjector: offer requested for trainer slot {trainerIndex} from {country}/{clubId} (stub 0x{stub.Value:X}).");
            return RequestResult.Requested;
        }

        /// <summary>Whether a requested offer hasn't been made yet (the next daily processing hasn't run).</summary>
        public bool IsRequestPending()
        {
            uint? stub = this.FindInstalledStub();
            if (!stub.HasValue)
                return false;
            byte[] flag = this.memory.ReadBytesAtAddress(stub.Value + FlagOffset, 1);
            return flag != null && flag[0] != 0;
        }

        /// <summary>Returns the installed stub, installing it first if the call site is still original; null if neither applies.</summary>
        internal uint? EnsureInstalled()
        {
            uint? target = this.ReadCallTarget();
            if (!target.HasValue)
                return null;
            if (target.Value != this.PresentFunction)
                return this.StubAt(target.Value);

            uint? stub = this.memory.AllocateExecutable(StubSize);
            if (!stub.HasValue)
                return null;

            byte[] code = BuildStub(stub.Value,
                this.fixedAddresses.Runtime(this.memory, Settings.JobOfferListOffset),
                this.memory.ModuleBase + this.code.AddFunction,
                this.PresentFunction);
            this.memory.WriteBytesAtAddress(stub.Value, code);

            int rel = unchecked((int)(stub.Value + CodeOffset - (this.CallSite + 5)));
            if (!this.memory.WriteCode(this.CallSite + 1, BitConverter.GetBytes(rel)))
                return null;

            Logger.Debug($"JobOfferInjector: redirected the offer presentation at 0x{this.CallSite:X} to a stub at 0x{stub.Value:X}.");
            return stub;
        }

        private uint? FindInstalledStub()
        {
            uint? target = this.ReadCallTarget();
            return target.HasValue && target.Value != this.PresentFunction ? this.StubAt(target.Value) : null;
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

        // Accepts a redirected call only if it lands on our own stub's code (signature check), never
        // on some other tool's patch.
        private uint? StubAt(uint codeAddress)
        {
            uint stub = codeAddress - CodeOffset;
            byte[] signature = this.memory.ReadBytesAtAddress(stub, Signature.Length);
            return signature != null && signature.SequenceEqual(Signature) ? stub : (uint?)null;
        }

        /// <summary>Builds the complete stub (data + code) for the given absolute addresses - see the class remarks.</summary>
        internal static byte[] BuildStub(uint stubBase, uint offerList, uint addOffer, uint presentOffers)
        {
            byte[] stub = new byte[StubSize];
            Array.Copy(Signature, stub, Signature.Length);

            uint flag = stubBase + FlagOffset;
            byte[] code = new[]
            {
                new byte[] { 0x80, 0x3D }, BitConverter.GetBytes(flag), new byte[] { 0x00 },       // cmp byte [flag], 0
                new byte[] { 0x74, 0x2B },                                                          // je done (+43)
                new byte[] { 0xC6, 0x05 }, BitConverter.GetBytes(flag), new byte[] { 0x00 },       // mov byte [flag], 0
                new byte[] { 0xFF, 0x35 }, BitConverter.GetBytes(stubBase + ArgumentsOffset + 12),  // push dword [kind]
                new byte[] { 0xFF, 0x35 }, BitConverter.GetBytes(stubBase + ArgumentsOffset + 8),   // push dword [club]
                new byte[] { 0xFF, 0x35 }, BitConverter.GetBytes(stubBase + ArgumentsOffset + 4),   // push dword [country]
                new byte[] { 0xFF, 0x35 }, BitConverter.GetBytes(stubBase + ArgumentsOffset),       // push dword [id]
                new byte[] { 0xB9 }, BitConverter.GetBytes(offerList),                              // mov ecx, offerList
                new byte[] { 0xB8 }, BitConverter.GetBytes(addOffer),                               // mov eax, addOffer
                new byte[] { 0xFF, 0xD0 },                                                          // call eax
                new byte[] { 0xB8 }, BitConverter.GetBytes(presentOffers),                          // done: mov eax, presentOffers
                new byte[] { 0xFF, 0xE0 },                                                          // jmp eax
            }.SelectMany(part => part).ToArray();

            Array.Copy(code, 0, stub, CodeOffset, code.Length);
            return stub;
        }
    }
}
