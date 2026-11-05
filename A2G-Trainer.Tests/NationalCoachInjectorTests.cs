using System;
using System.Linq;
using System.Runtime.InteropServices;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for NationalCoachInjector: redirecting the daily offer generation to the stub, the
    /// per-trainer requests, refusing foreign patches, running the generated x86 stub against
    /// stand-ins for the game's functions, and CoachController/CoachView using it.
    /// </summary>
    public class NationalCoachInjectorTests
    {
        private const int ModuleSize = 0x820000;

        private static uint CallSite(FakeModule fake) => fake.ModuleBase + Settings.OfferGenerateCallSiteOffset;
        private static uint ContinueFunction(FakeModule fake) => fake.ModuleBase + Settings.OfferGenerateFunctionOffset;

        // A CALL rel32 to the offer generation, as in the unpatched game.
        private static void WriteOriginalCall(FakeModule fake)
        {
            int rel = unchecked((int)(ContinueFunction(fake) - (CallSite(fake) + 5)));
            fake.WriteModuleBytes(Settings.OfferGenerateCallSiteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
        }

        private static uint CallTarget(FakeModule fake)
        {
            byte[] call = fake.ReadModuleBytes(Settings.OfferGenerateCallSiteOffset, 5);
            return unchecked((uint)(CallSite(fake) + 5 + BitConverter.ToInt32(call, 1)));
        }

        private static uint UInt32At(FakeModule fake, uint address) => BitConverter.ToUInt32(fake.Memory.ReadBytesAtAddress(address, 4), 0);

        [Fact]
        public void Original_KnowsTheNationalCoachCode()
        {
            FixedAddresses.NationalCoachCode code = FixedAddresses.Original.NationalCoach;

            Assert.Equal(0x1E5FE4u, code.CallSite);
            Assert.Equal(0x227390u, code.Continue);
            Assert.Equal(0x1CF1E0u, code.Take);
            Assert.Equal(0x1CF340u, code.Leave);
            Assert.Equal(0x1CF3E0u, code.Candidates);
        }

        [Fact]
        public void Request_RedirectsTheCallAndStoresThePerTrainerRequest()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCall(fake);
                NationalCoachInjector injector = new NationalCoachInjector(fake.Memory);

                Assert.True(injector.Request(1, PlayerEnums.Country.Deutschland, 1000000, 250000));
                Assert.True(injector.Request(1, PlayerEnums.Country.Italien, 2000000, 300000));

                uint stub = CallTarget(fake) - NationalCoachInjector.CodeOffset;
                Assert.Equal(NationalCoachInjector.Signature, fake.Memory.ReadBytesAtAddress(stub, 8));
                Assert.Equal(ContinueFunction(fake), NationalCoachInjector.ContinueOf(fake.Memory, CallTarget(fake)));
                uint slot = stub + NationalCoachInjector.SlotsOffset + NationalCoachInjector.SlotSize;
                uint salaryAddress = fake.ModuleBase + Settings.CoachRecordTableOffset + 2 * Settings.CoachRecordStride + Settings.CoachRecordNationalSalaryOffset;
                Assert.Equal(new uint[] { 1, 28, 2000000, 300000, salaryAddress }, Enumerable.Range(0, 5).Select(i => UInt32At(fake, slot + (uint)i * 4)));
                Assert.Equal(PlayerEnums.Country.Italien, injector.PendingCountry(1));
                Assert.Null(injector.PendingCountry(0));
                Assert.Null(injector.PendingCountry(Coach.MaxTrainers));
                Assert.False(injector.Request(-1, PlayerEnums.Country.Italien, 0, 0));
            }
        }

        [Fact]
        public void Request_LeavesAForeignPatchAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                int rel = unchecked((int)(fake.ModuleBase + 0x10000 - (CallSite(fake) + 5)));
                fake.WriteModuleBytes(Settings.OfferGenerateCallSiteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
                NationalCoachInjector injector = new NationalCoachInjector(fake.Memory);

                Assert.False(injector.Request(0, PlayerEnums.Country.Italien, 0, 0));
                Assert.Null(injector.PendingCountry(0));
                Assert.Equal(fake.ModuleBase + 0x10000, CallTarget(fake));
            }
        }

        [Fact]
        public void WithoutTheCode_NothingIsRequested()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCall(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                NationalCoachInjector injector = new NationalCoachInjector(fake.Memory);

                Assert.False(injector.IsSupported);
                Assert.False(injector.Request(0, PlayerEnums.Country.Italien, 0, 0));
                Assert.Null(injector.PendingCountry(0));
            }
        }

        [Fact]
        public void Request_WithoutACallAtTheCallSite_Fails()
        {
            using (FakeModule fake = new FakeModule())
                Assert.False(new NationalCoachInjector(fake.Memory).Request(0, PlayerEnums.Country.Italien, 0, 0));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StubEntry();

        // Runs the real stub: slot 2 takes over Germany, slot 4 only leaves; a second run finds no
        // request any more and only continues.
        [Fact]
        public void BuildStub_RunsAgainstStandInsForTheGamesFunctions()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint block = fake.Memory.AllocateExecutable(0x400).Value;
                uint stub = block;
                uint leave = block + 0x200, take = block + 0x240, candidates = block + 0x280, next = block + 0x2A0;
                uint log = block + 0x300;   // last leave ID, leave calls, take ID, take country, take calls, candidate calls, continue calls
                byte[] A(uint a) => BitConverter.GetBytes(a);
                byte[] Inc(uint a) => new byte[] { 0xFF, 0x05 }.Concat(A(a)).ToArray();
                byte[] leaveCode = new[] { new byte[] { 0x8B, 0x44, 0x24, 0x04, 0xA3 }, A(log), Inc(log + 4), new byte[] { 0xC3 } }.SelectMany(p => p).ToArray();
                byte[] takeCode = new[]
                {
                    new byte[] { 0x8B, 0x44, 0x24, 0x04, 0xA3 }, A(log + 8),
                    new byte[] { 0x8B, 0x44, 0x24, 0x08, 0xA3 }, A(log + 12),
                    Inc(log + 16), new byte[] { 0xC3 },
                }.SelectMany(p => p).ToArray();
                fake.Memory.WriteBytesAtAddress(stub, NationalCoachInjector.BuildStub(stub, next, leave, take, candidates));
                fake.Memory.WriteBytesAtAddress(leave, leaveCode);
                fake.Memory.WriteBytesAtAddress(take, takeCode);
                fake.Memory.WriteBytesAtAddress(candidates, Inc(log + 20).Concat(new byte[] { 0xC3 }).ToArray());
                fake.Memory.WriteBytesAtAddress(next, Inc(log + 24).Concat(new byte[] { 0xC3 }).ToArray());
                fake.Memory.WriteBytesAtAddress(log + 0x48, new byte[8] { 9, 9, 9, 9, 9, 9, 9, 9 });

                uint slot2 = stub + NationalCoachInjector.SlotsOffset + NationalCoachInjector.SlotSize;
                uint slot4 = stub + NationalCoachInjector.SlotsOffset + 3 * NationalCoachInjector.SlotSize;
                fake.Memory.WriteBytesAtAddress(slot2, new uint[] { 1, 14, 1000, 50, log + 0x40 }.SelectMany(BitConverter.GetBytes).ToArray());
                fake.Memory.WriteBytesAtAddress(slot4, new uint[] { 1, 0, 0, 0, log + 0x48 }.SelectMany(BitConverter.GetBytes).ToArray());

                StubEntry run = Marshal.GetDelegateForFunctionPointer<StubEntry>(new IntPtr(stub + NationalCoachInjector.CodeOffset));
                run();
                run();

                uint[] recorded = Enumerable.Range(0, 7).Select(i => UInt32At(fake, log + (uint)i * 4)).ToArray();
                Assert.Equal(new uint[] { 4, 2, 2, 14, 1, 1, 2 }, recorded);
                Assert.Equal(new uint[] { 1000, 50, 0, 0 }, Enumerable.Range(0, 4).Select(i => UInt32At(fake, log + 0x40 + (uint)i * 4)));
                Assert.Equal(0u, UInt32At(fake, slot2));
                Assert.Equal(0u, UInt32At(fake, slot4));
            }
        }

        private static uint RecordOffset(int trainerSlot, uint fieldOffset) =>
            Settings.CoachRecordTableOffset + (uint)(trainerSlot + 1) * Settings.CoachRecordStride + fieldOffset;

        [Fact]
        public void Save_ChangingTheNationalTeam_LeavesItToTheGame()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            {
                WriteOriginalCall(fake);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                controller.Coach.NationalTeam = PlayerEnums.Country.Italien;
                controller.Coach.NationalSalary = 1000000;

                controller.Save();

                Assert.True(controller.Coach.IsNationalTeamPending);
                // Posts, table and team are the game's job now; only the contract is written.
                Assert.Equal((ushort)0, BitConverter.ToUInt16(fake.ReadModuleBytes(RecordOffset(0, Settings.CoachRecordNationalTeamOffset), 2), 0));
                Assert.Equal(1000000, BitConverter.ToInt32(fake.ReadModuleBytes(RecordOffset(0, Settings.CoachRecordNationalSalaryOffset), 4), 0));

                Coach reread = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;
                Assert.True(reread.IsNationalTeamPending);
                Assert.Equal(PlayerEnums.Country.Italien, reread.NationalTeam);
                Assert.Equal("Das Spiel übernimmt Italien beim nächsten Tagesabschluss.", CoachView.NationalCoachMessage(reread));
            }
        }

        [Fact]
        public void Save_ANationalCoachWithoutFriendlyOpponents_IsSetUpAgainByTheGame()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            {
                WriteOriginalCall(fake);
                fake.WriteModuleBytes(RecordOffset(0, Settings.CoachRecordNationalTeamOffset), BitConverter.GetBytes((ushort)PlayerEnums.Country.Deutschland));
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

                controller.Save();
                Assert.Equal(PlayerEnums.Country.Deutschland, new NationalCoachInjector(fake.Memory).PendingCountry(0));
            }
        }

        [Fact]
        public void Save_ANationalCoachWithFriendlyOpponents_RequestsNothing()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            {
                WriteOriginalCall(fake);
                fake.WriteModuleBytes(RecordOffset(0, Settings.CoachRecordNationalTeamOffset), BitConverter.GetBytes((ushort)PlayerEnums.Country.Deutschland));
                fake.WriteModuleBytes(Settings.NationalFriendlyBlockOffset + Settings.NationalFriendlyBlockStride, new byte[] { 28, 0, 200, 0 });
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

                controller.Save();

                Assert.False(controller.Coach.IsNationalTeamPending);
                Assert.Equal(ContinueFunction(fake), CallTarget(fake));
            }
        }

        [Fact]
        public void NationalCoachMessage_DescribesAPendingChange()
        {
            Assert.Equal("", CoachView.NationalCoachMessage(null));
            Assert.Equal("", CoachView.NationalCoachMessage(new Coach { NationalTeam = PlayerEnums.Country.Italien }));
            Assert.Equal("Das Spiel beendet das Amt beim nächsten Tagesabschluss.", CoachView.NationalCoachMessage(new Coach { IsNationalTeamPending = true }));
        }
    }
}
