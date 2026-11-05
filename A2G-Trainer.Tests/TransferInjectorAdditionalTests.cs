using System;
using System.Linq;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// TransferInjector edge cases: call sites that aren't calls, calls into two different stubs,
    /// a stub whose data can't be read, an unreadable money factor and call sites whose code can't
    /// be rewritten.
    /// </summary>
    public class TransferInjectorAdditionalTests
    {
        // A fake stub in the free end of the FakeModule whose signature ends right at a page boundary.
        private const uint EdgeStub = 0x51AFF8;
        private const uint EdgePage = 0x51B000;

        private static byte[] Call(uint site, uint target) =>
            new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(unchecked((int)(target - (site + 5))))).ToArray();

        private static void WriteCall(FakeModule fake, uint siteOffset, uint targetOffset) =>
            fake.WriteModuleBytes(siteOffset, Call(fake.ModuleBase + siteOffset, fake.ModuleBase + targetOffset));

        private static void WriteOriginalCalls(FakeModule fake)
        {
            foreach (uint site in Settings.TransferMarketCallSiteOffsets)
                WriteCall(fake, site, Settings.TransferMarketFunctionOffset);
            WriteCall(fake, Settings.TransferDeadlineCallSiteOffset, Settings.TransferDeadlineFunctionOffset);
        }

        private static uint CallTarget(FakeModule fake, uint site)
        {
            byte[] call = fake.Memory.ReadBytesAtAddress(site, 5);
            return unchecked((uint)(site + 5 + BitConverter.ToInt32(call, 1)));
        }

        private static Player BayernPlayer() => new Player { NameRecordId = 0x1234, ClubCountry = PlayerEnums.Country.Deutschland, ClubId = 5 };

        private static TransferInjector.RequestResult RequestMove(TransferInjector injector, uint fee = 0) =>
            injector.Request(BayernPlayer(), PlayerEnums.Country.England, 7, TransferInjector.Kind.Transfer, fee, 0, false);

        private static FixedAddresses.TransferCode CodeWith(uint[] marketSites, uint deadlineSite)
        {
            FixedAddresses.TransferCode original = FixedAddresses.Original.Transfer;
            return new FixedAddresses.TransferCode
            {
                MarketCallSites = marketSites,
                MarketFunction = original.MarketFunction,
                DeadlineCallSite = deadlineSite,
                DeadlineFunction = original.DeadlineFunction,
                FetchPlayer = original.FetchPlayer,
                ReleasePlayer = original.ReleasePlayer,
                FetchClub = original.FetchClub,
                ReleaseClub = original.ReleaseClub,
                BuildContract = original.BuildContract,
                Move = original.Move,
                Loan = original.Loan,
            };
        }

        [Fact]
        public void Request_WhenAMarketCallSiteIsNotACall_RefusesToPatch()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.WriteModuleBytes(Settings.TransferMarketCallSiteOffsets[0], new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 });
                TransferInjector injector = new TransferInjector(fake.Memory);

                Assert.Equal(TransferInjector.RequestResult.HookFailed, RequestMove(injector));
                Assert.Null(injector.EnsureInstalled());
                Assert.Equal(fake.ModuleBase + Settings.TransferDeadlineFunctionOffset, CallTarget(fake, fake.ModuleBase + Settings.TransferDeadlineCallSiteOffset));
            }
        }

        [Fact]
        public void Request_WhenTheMarketCallsGoIntoTwoDifferentStubs_LeavesEverythingAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                // Two stubs of ours (signature checked), each with one of the market calls.
                const uint first = 0x51A000, second = 0x51A400;
                fake.WriteModuleBytes(first, TransferInjector.Signature);
                fake.WriteModuleBytes(second, TransferInjector.Signature);
                WriteCall(fake, Settings.TransferMarketCallSiteOffsets[0], first + TransferInjector.CodeOffset);
                WriteCall(fake, Settings.TransferMarketCallSiteOffsets[1], second + TransferInjector.CodeOffset);

                Assert.Equal(TransferInjector.RequestResult.HookFailed, RequestMove(new TransferInjector(fake.Memory)));
                Assert.Equal(fake.ModuleBase + first + TransferInjector.CodeOffset, CallTarget(fake, fake.ModuleBase + Settings.TransferMarketCallSiteOffsets[0]));
                Assert.Equal(fake.ModuleBase + second + TransferInjector.CodeOffset, CallTarget(fake, fake.ModuleBase + Settings.TransferMarketCallSiteOffsets[1]));
                Assert.Equal(fake.ModuleBase + Settings.TransferDeadlineFunctionOffset, CallTarget(fake, fake.ModuleBase + Settings.TransferDeadlineCallSiteOffset));
            }
        }

        [Fact]
        public void PendingAndOutcome_OfAStubWhoseDataCantBeRead_AreNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.WriteModuleBytes(EdgeStub, TransferInjector.Signature);
                // Flag 1 and an outcome: readable they'd count as a pending request and an outcome.
                fake.WriteModuleBytes(EdgeStub + TransferInjector.FlagOffset, BitConverter.GetBytes(0u));
                fake.WriteModuleBytes(EdgeStub + TransferInjector.OutcomeOffset, BitConverter.GetBytes((uint)TransferInjector.Outcome.Done));
                WriteCall(fake, Settings.TransferMarketCallSiteOffsets[0], EdgeStub + TransferInjector.CodeOffset);
                TransferInjector injector = new TransferInjector(fake.Memory);
                Assert.False(injector.IsRequestPending());
                fake.WriteModuleBytes(EdgeStub + TransferInjector.FlagOffset, BitConverter.GetBytes(1u));
                Assert.True(injector.IsRequestPending());
                fake.WriteModuleBytes(EdgeStub + TransferInjector.FlagOffset, BitConverter.GetBytes(0u));

                fake.MakeModuleRegionUnreadable(EdgePage, 1);

                Assert.False(injector.IsRequestPending());
                Assert.Equal(TransferInjector.Outcome.None, injector.TakeOutcome());
            }
        }

        [Fact]
        public void Request_WithAnUnreadableMoneyFactor_UsesTheGamesDefault()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.WriteModuleBytes(Settings.MoneyFactorOffset, BitConverter.GetBytes(1000));
                fake.MakeModuleRegionUnreadable(Settings.MoneyFactorOffset, 4);
                TransferInjector injector = new TransferInjector(fake.Memory);

                Assert.Equal(TransferInjector.RequestResult.Requested, RequestMove(injector, fee: 3));

                uint stub = CallTarget(fake, fake.ModuleBase + Settings.TransferMarketCallSiteOffsets[0]) - TransferInjector.CodeOffset;
                uint fee = BitConverter.ToUInt32(fake.Memory.ReadBytesAtAddress(stub + TransferInjector.ArgumentsOffset + TransferInjector.ArgFee * 4, 4), 0);
                Assert.Equal(300u, fee);
            }
        }

        [Fact]
        public void Request_WhenTheDeadlineCallCantBeRewritten_FailsBeforeTouchingTheMarketCalls()
        {
            using (FakeModule fake = new FakeModule())
            using (NonExecutablePage page = new NonExecutablePage())
            {
                WriteOriginalCalls(fake);
                uint deadlineSite = unchecked(page.Address - fake.ModuleBase);
                page.Write(0, Call(page.Address, fake.ModuleBase + Settings.TransferDeadlineFunctionOffset));
                InjectedFixedAddresses.Use(fake, new FixedAddresses(0, 0, null, CodeWith(Settings.TransferMarketCallSiteOffsets, deadlineSite)));
                TransferInjector injector = new TransferInjector(fake.Memory);
                Assert.True(injector.IsSupported);

                Assert.Equal(TransferInjector.RequestResult.HookFailed, RequestMove(injector));

                Assert.Equal(fake.ModuleBase + Settings.TransferDeadlineFunctionOffset, CallTarget(fake, page.Address));
                foreach (uint site in Settings.TransferMarketCallSiteOffsets)
                    Assert.Equal(fake.ModuleBase + Settings.TransferMarketFunctionOffset, CallTarget(fake, fake.ModuleBase + site));
            }
        }

        [Fact]
        public void Request_WhenAMarketCallCantBeRewritten_Fails()
        {
            using (FakeModule fake = new FakeModule())
            using (NonExecutablePage page = new NonExecutablePage())
            {
                WriteOriginalCalls(fake);
                uint marketSite = unchecked(page.Address - fake.ModuleBase);
                page.Write(0, Call(page.Address, fake.ModuleBase + Settings.TransferMarketFunctionOffset));
                uint[] sites = { Settings.TransferMarketCallSiteOffsets[0], marketSite };
                InjectedFixedAddresses.Use(fake, new FixedAddresses(0, 0, null, CodeWith(sites, Settings.TransferDeadlineCallSiteOffset)));
                TransferInjector injector = new TransferInjector(fake.Memory);

                Assert.Equal(TransferInjector.RequestResult.HookFailed, RequestMove(injector));

                // The deadline goes first and the first market call is redirected, the second stays.
                uint stub = CallTarget(fake, fake.ModuleBase + Settings.TransferMarketCallSiteOffsets[0]) - TransferInjector.CodeOffset;
                Assert.Equal(TransferInjector.Signature, fake.Memory.ReadBytesAtAddress(stub, TransferInjector.Signature.Length));
                Assert.Equal(stub + TransferInjector.DeadlineCodeOffset, CallTarget(fake, fake.ModuleBase + Settings.TransferDeadlineCallSiteOffset));
                Assert.Equal(fake.ModuleBase + Settings.TransferMarketFunctionOffset, CallTarget(fake, page.Address));
            }
        }
    }
}
