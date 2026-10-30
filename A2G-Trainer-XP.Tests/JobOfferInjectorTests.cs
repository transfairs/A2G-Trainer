using System;
using System.Linq;
using System.Runtime.InteropServices;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for JobOfferInjector: redirecting the game's offer-presentation call to the stub, the
    /// request arguments and preconditions, refusing foreign patches - and actually running the
    /// generated x86 stub (this test process is x86 like the game) against stand-ins for the game's
    /// add-offer and present-offers functions.
    /// </summary>
    public class JobOfferInjectorTests
    {
        private static uint CallSite(FakeModule fake) => fake.ModuleBase + Settings.JobOfferPresentCallSiteOffset;
        private static uint PresentFunction(FakeModule fake) => fake.ModuleBase + Settings.JobOfferPresentFunctionOffset;

        // A CALL rel32 to the original presentation function, as in the unpatched game.
        private static void WriteOriginalCall(FakeModule fake)
        {
            int rel = unchecked((int)(PresentFunction(fake) - (CallSite(fake) + 5)));
            fake.WriteModuleBytes(Settings.JobOfferPresentCallSiteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
        }

        private static uint CallTarget(FakeModule fake)
        {
            byte[] call = fake.ReadModuleBytes(Settings.JobOfferPresentCallSiteOffset, 5);
            return unchecked((uint)(CallSite(fake) + 5 + BitConverter.ToInt32(call, 1)));
        }

        private static Coach EmployedCoach() => new Coach { CurrentCountry = PlayerEnums.Country.Deutschland, CurrentClubId = 5 };

        private static byte[] Read(FakeModule fake, uint address, int length) => fake.Memory.ReadBytesAtAddress(address, length);

        [Fact]
        public void Request_RedirectsThePresentationCallAndStoresTheOffer()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCall(fake);
                JobOfferInjector injector = new JobOfferInjector(fake.Memory);

                JobOfferInjector.RequestResult result = injector.Request(EmployedCoach(), trainerIndex: 1, PlayerEnums.Country.England, 7);

                Assert.Equal(JobOfferInjector.RequestResult.Requested, result);
                uint stub = CallTarget(fake) - JobOfferInjector.CodeOffset;
                Assert.Equal(JobOfferInjector.Signature, Read(fake, stub, JobOfferInjector.Signature.Length));
                // Mitspieler ID 2 (slot 1), country, club, offer kind 3.
                uint[] arguments = Enumerable.Range(0, 4).Select(i => BitConverter.ToUInt32(Read(fake, stub + JobOfferInjector.ArgumentsOffset + (uint)i * 4, 4), 0)).ToArray();
                Assert.Equal(new uint[] { 2, (uint)PlayerEnums.Country.England, 7, 3 }, arguments);
                Assert.True(injector.IsRequestPending());
            }
        }

        [Fact]
        public void IsRequestPending_WithAnUnreadableCallSite_IsFalse()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCall(fake);
                fake.MakeModuleRegionUnreadable(Settings.JobOfferPresentCallSiteOffset, 5);

                Assert.False(new JobOfferInjector(fake.Memory).IsRequestPending());
            }
        }

        [Fact]
        public void IsRequestPending_OnABuildWithoutConfirmedAddresses_IsFalse()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCall(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);

                Assert.False(new JobOfferInjector(fake.Memory).IsRequestPending());
            }
        }

        [Fact]
        public void Request_Twice_ReusesTheInstalledStub()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCall(fake);
                JobOfferInjector injector = new JobOfferInjector(fake.Memory);

                injector.Request(EmployedCoach(), 0, PlayerEnums.Country.England, 7);
                uint first = CallTarget(fake);
                injector.Request(EmployedCoach(), 0, PlayerEnums.Country.England, 8);

                Assert.Equal(first, CallTarget(fake));
            }
        }

        [Fact]
        public void Request_ChecksTheGamesPresentationConditionsBeforeTouchingAnything()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCall(fake);
                JobOfferInjector injector = new JobOfferInjector(fake.Memory);

                Assert.Equal(JobOfferInjector.RequestResult.NotEmployed, injector.Request(new Coach(), 0, PlayerEnums.Country.England, 7));
                Coach pending = EmployedCoach();
                pending.CountryNextSeason = PlayerEnums.Country.England;
                Assert.Equal(JobOfferInjector.RequestResult.ChangePending, injector.Request(pending, 0, PlayerEnums.Country.England, 7));
                Assert.Equal(JobOfferInjector.RequestResult.SameClub, injector.Request(EmployedCoach(), 0, PlayerEnums.Country.Deutschland, 5));

                Assert.Equal(PresentFunction(fake), CallTarget(fake));
                Assert.False(injector.IsRequestPending());
            }
        }

        [Fact]
        public void Request_WithoutOriginalBuildAnchors_IsNotSupported()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCall(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);

                Assert.Equal(JobOfferInjector.RequestResult.NotSupported, new JobOfferInjector(fake.Memory).Request(EmployedCoach(), 0, PlayerEnums.Country.England, 7));
                Assert.Equal(PresentFunction(fake), CallTarget(fake));
            }
        }

        [Fact]
        public void Request_WhenTheCallSiteIsNotACall_RefusesToPatch()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(Settings.JobOfferPresentCallSiteOffset, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 });

                Assert.Equal(JobOfferInjector.RequestResult.HookFailed, new JobOfferInjector(fake.Memory).Request(EmployedCoach(), 0, PlayerEnums.Country.England, 7));
                Assert.Equal(new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 }, fake.ReadModuleBytes(Settings.JobOfferPresentCallSiteOffset, 5));
            }
        }

        [Fact]
        public void Request_WhenTheCallIsAlreadyRedirectedByAnotherTool_LeavesItAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                // A CALL into the module that is neither the original target nor our stub.
                int rel = unchecked((int)(fake.ModuleBase + 0x100040 - (CallSite(fake) + 5)));
                byte[] foreign = new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray();
                fake.WriteModuleBytes(Settings.JobOfferPresentCallSiteOffset, foreign);

                Assert.Equal(JobOfferInjector.RequestResult.HookFailed, new JobOfferInjector(fake.Memory).Request(EmployedCoach(), 0, PlayerEnums.Country.England, 7));
                Assert.Equal(foreign, fake.ReadModuleBytes(Settings.JobOfferPresentCallSiteOffset, 5));
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int pid);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        private const uint ProcessVmOperation = 0x0008;
        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessQueryInformation = 0x0400;

        // Runs the request through a handle to this process with only the given rights, as with a
        // game process the trainer may read but not fully modify.
        private static JobOfferInjector.RequestResult RequestWithHandleRights(FakeModule fake, uint access)
        {
            IntPtr full = fake.Memory.mProc.Handle;
            IntPtr limited = OpenProcess(access, false, System.Diagnostics.Process.GetCurrentProcess().Id);
            Assert.NotEqual(IntPtr.Zero, limited);
            try
            {
                fake.Memory.mProc.Handle = limited;
                return new JobOfferInjector(fake.Memory).Request(EmployedCoach(), 0, PlayerEnums.Country.England, 7);
            }
            finally
            {
                fake.Memory.mProc.Handle = full;
                CloseHandle(limited);
            }
        }

        [Fact]
        public void Request_WhenNoStubMemoryCanBeAllocated_FailsAndLeavesTheCallAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCall(fake);

                Assert.Equal(JobOfferInjector.RequestResult.HookFailed, RequestWithHandleRights(fake, ProcessVmRead | ProcessQueryInformation));
                Assert.Equal(PresentFunction(fake), CallTarget(fake));
            }
        }

        [Fact]
        public void Request_WhenTheCallSiteCantBeWritten_FailsAndLeavesTheCallAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCall(fake);

                Assert.Equal(JobOfferInjector.RequestResult.HookFailed, RequestWithHandleRights(fake, ProcessVmOperation | ProcessVmRead | ProcessQueryInformation));
                Assert.Equal(PresentFunction(fake), CallTarget(fake));
            }
        }

        // A stub whose signature ends exactly at a page boundary, with the flag on the next,
        // unreadable page.
        [Fact]
        public void IsRequestPending_WithAnUnreadableFlag_IsFalse()
        {
            using (FakeModule fake = new FakeModule())
            {
                const uint stubOffset = 0x10000 - 8;
                fake.WriteModuleBytes(stubOffset, JobOfferInjector.Signature);
                int rel = unchecked((int)(fake.ModuleBase + stubOffset + JobOfferInjector.CodeOffset - (CallSite(fake) + 5)));
                fake.WriteModuleBytes(Settings.JobOfferPresentCallSiteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
                fake.MakeModuleRegionUnreadable(0x10000, 1);

                Assert.False(new JobOfferInjector(fake.Memory).IsRequestPending());
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StubEntry();

        // Runs the real stub: with a request it must call the add-offer stand-in once with
        // (ECX = offer list; ID, country, club, kind) and clear the flag, then always continue to
        // the present-offers stand-in; without a request it only continues.
        [Fact]
        public void BuildStub_RunsAgainstStandInsForTheGamesFunctions()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint block = fake.Memory.AllocateExecutable(0x200).Value;
                uint stub = block;
                uint record = block + 0x100;           // ID, country, club, kind, ECX, add calls, present calls
                uint addOffer = block + 0x80;
                uint presentOffers = block + 0xE0;
                const uint offerList = 0x12345678;

                byte[] AddressBytes(uint a) => BitConverter.GetBytes(a);
                byte[] addCode = new[]
                {
                    new byte[] { 0x8B, 0x44, 0x24, 0x04, 0xA3 }, AddressBytes(record),          // mov eax,[esp+4]; mov [rec],eax
                    new byte[] { 0x8B, 0x44, 0x24, 0x08, 0xA3 }, AddressBytes(record + 4),
                    new byte[] { 0x8B, 0x44, 0x24, 0x0C, 0xA3 }, AddressBytes(record + 8),
                    new byte[] { 0x8B, 0x44, 0x24, 0x10, 0xA3 }, AddressBytes(record + 12),
                    new byte[] { 0x89, 0x0D }, AddressBytes(record + 16),                       // mov [rec+16],ecx
                    new byte[] { 0xFF, 0x05 }, AddressBytes(record + 20),                       // inc dword [rec+20]
                    new byte[] { 0xC2, 0x10, 0x00 },                                            // ret 0x10
                }.SelectMany(p => p).ToArray();
                byte[] presentCode = new byte[] { 0xFF, 0x05 }.Concat(AddressBytes(record + 24)).Concat(new byte[] { 0xC3 }).ToArray();

                fake.Memory.WriteBytesAtAddress(stub, JobOfferInjector.BuildStub(stub, offerList, addOffer, presentOffers));
                fake.Memory.WriteBytesAtAddress(addOffer, addCode);
                fake.Memory.WriteBytesAtAddress(presentOffers, presentCode);
                fake.Memory.WriteBytesAtAddress(stub + JobOfferInjector.ArgumentsOffset, new uint[] { 2, 14, 55, 3 }.SelectMany(BitConverter.GetBytes).ToArray());
                fake.Memory.WriteBytesAtAddress(stub + JobOfferInjector.FlagOffset, BitConverter.GetBytes(1u));

                StubEntry run = Marshal.GetDelegateForFunctionPointer<StubEntry>(new IntPtr(stub + JobOfferInjector.CodeOffset));
                run();
                run();

                uint[] recorded = Enumerable.Range(0, 7).Select(i => BitConverter.ToUInt32(Read(fake, record + (uint)i * 4, 4), 0)).ToArray();
                Assert.Equal(new uint[] { 2, 14, 55, 3, offerList }, recorded.Take(5));
                Assert.Equal(1u, recorded[5]);   // offer added once - the flag was cleared after the first run
                Assert.Equal(2u, recorded[6]);   // presentation reached both times
                Assert.Equal(0, Read(fake, stub + JobOfferInjector.FlagOffset, 1)[0]);
            }
        }
    }
}
