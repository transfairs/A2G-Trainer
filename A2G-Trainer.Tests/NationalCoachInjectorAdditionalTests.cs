using System;
using System.Linq;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// NationalCoachInjector edge cases: a stub whose request slots or header can't be read, and a
    /// call site whose code can't be rewritten.
    /// </summary>
    public class NationalCoachInjectorAdditionalTests
    {
        // A fake stub in the free end of the FakeModule: header readable, request slots on the next page.
        private const uint EdgePage = 0x51B000;
        private const uint EdgeStub = EdgePage - NationalCoachInjector.SlotsOffset;

        private static byte[] Call(uint site, uint target) =>
            new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(unchecked((int)(target - (site + 5))))).ToArray();

        [Fact]
        public void PendingCountry_OfAStubWhoseSlotsCantBeRead_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(EdgeStub, NationalCoachInjector.Signature.Concat(BitConverter.GetBytes(fake.ModuleBase + Settings.OfferGenerateFunctionOffset)).ToArray());
                fake.WriteModuleBytes(EdgePage + NationalCoachInjector.FlagOffset, BitConverter.GetBytes(1u));
                fake.WriteModuleBytes(EdgePage + NationalCoachInjector.CountryOffset, BitConverter.GetBytes((uint)PlayerEnums.Country.Italien));
                uint site = fake.ModuleBase + Settings.OfferGenerateCallSiteOffset;
                fake.WriteModuleBytes(Settings.OfferGenerateCallSiteOffset, Call(site, fake.ModuleBase + EdgeStub + NationalCoachInjector.CodeOffset));
                NationalCoachInjector injector = new NationalCoachInjector(fake.Memory);
                Assert.Equal(PlayerEnums.Country.Italien, injector.PendingCountry(0));

                fake.MakeModuleRegionUnreadable(EdgePage, 1);

                Assert.Null(injector.PendingCountry(0));
                Assert.Equal(fake.ModuleBase + EdgeStub, injector.EnsureInstalled());
            }
        }

        [Fact]
        public void ContinueOf_AnUnreadableAddress_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint code = fake.ModuleBase + EdgePage + NationalCoachInjector.CodeOffset;
                fake.WriteModuleBytes(EdgePage, NationalCoachInjector.Signature.Concat(BitConverter.GetBytes(0x401000u)).ToArray());
                Assert.Equal(0x401000u, NationalCoachInjector.ContinueOf(fake.Memory, code));

                fake.MakeModuleRegionUnreadable(EdgePage, 1);

                Assert.Null(NationalCoachInjector.ContinueOf(fake.Memory, code));
            }
        }

        [Fact]
        public void Request_WhenTheCallSiteCantBeRewritten_Fails()
        {
            using (FakeModule fake = new FakeModule())
            using (NonExecutablePage page = new NonExecutablePage())
            {
                FixedAddresses.NationalCoachCode original = FixedAddresses.Original.NationalCoach;
                FixedAddresses.NationalCoachCode code = new FixedAddresses.NationalCoachCode
                {
                    CallSite = unchecked(page.Address - fake.ModuleBase),
                    Continue = original.Continue,
                    Take = original.Take,
                    Leave = original.Leave,
                    Candidates = original.Candidates,
                };
                byte[] call = Call(page.Address, fake.ModuleBase + original.Continue);
                page.Write(0, call);
                InjectedFixedAddresses.Use(fake, new FixedAddresses(0, 0, null, null, code));
                NationalCoachInjector injector = new NationalCoachInjector(fake.Memory);
                Assert.True(injector.IsSupported);

                Assert.False(injector.Request(0, PlayerEnums.Country.Italien, 1000, 100));

                Assert.Equal(call, fake.Memory.ReadBytesAtAddress(page.Address, 5));
                Assert.Null(injector.PendingCountry(0));
            }
        }
    }
}
