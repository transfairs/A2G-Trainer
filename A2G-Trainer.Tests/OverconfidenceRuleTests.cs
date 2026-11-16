using System;
using System.Linq;
using System.Runtime.InteropServices;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for OverconfidenceRule: redirecting both calls of the match set-up to the stub, setting
    /// and reading the rules, refusing foreign patches and invalid factors - and running the generated
    /// x86 stub against a stand-in for the game's match set-up.
    /// </summary>
    public class OverconfidenceRuleTests
    {
        private static uint Absolute(FakeModule fake, uint offset) => fake.ModuleBase + offset;

        private static void WriteCall(FakeModule fake, uint siteOffset, uint target)
        {
            int rel = unchecked((int)(target - (Absolute(fake, siteOffset) + 5)));
            fake.WriteModuleBytes(siteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
        }

        // Both calls as in the unpatched game.
        internal static void WriteOriginalCalls(FakeModule fake)
        {
            foreach (uint site in Settings.MatchInitCallSiteOffsets)
                WriteCall(fake, site, Absolute(fake, Settings.MatchInitFunctionOffset));
        }

        private static uint CallTarget(FakeModule fake, uint siteOffset)
        {
            byte[] call = fake.ReadModuleBytes(siteOffset, 5);
            return unchecked((uint)(Absolute(fake, siteOffset) + 5 + BitConverter.ToInt32(call, 1)));
        }

        internal static uint Stub(FakeModule fake) => CallTarget(fake, Settings.MatchInitCallSiteOffsets[0]) - OverconfidenceRule.CodeOffset;

        private static void AssertRule(OverconfidenceRule rule, byte own, byte opponent)
        {
            rule.ReadRule(out byte readOwn, out byte readOpponent);
            Assert.Equal(own, readOwn);
            Assert.Equal(opponent, readOpponent);
        }

        [Fact]
        public void SetRule_RedirectsBothCallsOnce_AndStoresTheFactors()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                OverconfidenceRule rule = new OverconfidenceRule(fake.Memory);
                Assert.True(rule.IsSupported);
                AssertRule(rule, 0, 0);

                Assert.True(rule.SetRule(100, 70));
                uint stub = Stub(fake);
                Assert.Equal(OverconfidenceRule.Signature, fake.Memory.ReadBytesAtAddress(stub, OverconfidenceRule.Signature.Length));
                Assert.Equal(stub + OverconfidenceRule.CodeOffset, CallTarget(fake, Settings.MatchInitCallSiteOffsets[1]));
                Assert.Equal(Absolute(fake, Settings.MatchInitFunctionOffset),
                    BitConverter.ToUInt32(fake.Memory.ReadBytesAtAddress(stub + OverconfidenceRule.FunctionOffset, 4), 0));
                AssertRule(rule, 100, 70);

                // Back to the game's dice: the stub stays and passes everything through.
                Assert.True(rule.SetRule(0, 0));
                AssertRule(rule, 0, 0);
                Assert.Equal(stub, Stub(fake));
                // Again: the same stub, no second redirect.
                Assert.True(new OverconfidenceRule(fake.Memory).SetRule(0, 90));
                Assert.Equal(stub, Stub(fake));
                AssertRule(rule, 0, 90);
            }
        }

        [Fact]
        public void SetRule_ToTheGamesDiceWithoutAStub_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);

                Assert.True(new OverconfidenceRule(fake.Memory).SetRule(0, 0));
                Assert.Equal(Absolute(fake, Settings.MatchInitFunctionOffset), CallTarget(fake, Settings.MatchInitCallSiteOffsets[0]));
            }
        }

        [Theory]
        [InlineData(69, 0)]
        [InlineData(0, 101)]
        [InlineData(1, 100)]
        public void SetRule_WithAFactorOutsideTheGamesRange_Fails(byte own, byte opponent)
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);

                Assert.False(new OverconfidenceRule(fake.Memory).SetRule(own, opponent));
                Assert.Equal(Absolute(fake, Settings.MatchInitFunctionOffset), CallTarget(fake, Settings.MatchInitCallSiteOffsets[0]));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public void SetRule_WithACallSitePatchedElsewhere_Fails(int site)
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                WriteCall(fake, Settings.MatchInitCallSiteOffsets[site], Absolute(fake, 0x10000));
                OverconfidenceRule rule = new OverconfidenceRule(fake.Memory);

                Assert.False(rule.SetRule(80, 0));
                AssertRule(rule, 0, 0);
            }
        }

        [Fact]
        public void SetRule_WithoutACallAtTheCallSite_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.WriteModuleBytes(Settings.MatchInitCallSiteOffsets[0], new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 });

                Assert.False(new OverconfidenceRule(fake.Memory).SetRule(80, 0));
            }
        }

        [Fact]
        public void SetRule_OnABuildWithoutConfirmedAddresses_FailsOnlyForAFactor()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                OverconfidenceRule rule = new OverconfidenceRule(fake.Memory);

                Assert.False(rule.IsSupported);
                Assert.False(rule.SetRule(90, 90));
                Assert.True(rule.SetRule(0, 0));
                AssertRule(rule, 0, 0);
            }
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(1, 100)]
        [InlineData(4, 70)]
        public void RuleNames_MapToTheFactors(int index, byte factor)
        {
            Assert.Equal(OverconfidenceRule.Factors.Length + 1, OverconfidenceRule.RuleNames.Length);
            Assert.Equal(factor, OverconfidenceRule.RuleValue(index));
            Assert.Equal(index, OverconfidenceRule.RuleIndex(factor));
        }

        [Fact]
        public void RuleValue_OutsideTheList_IsTheGamesDice()
        {
            Assert.Equal(0, OverconfidenceRule.RuleValue(-1));
            Assert.Equal(0, OverconfidenceRule.RuleValue(OverconfidenceRule.RuleNames.Length));
            Assert.Equal(0, OverconfidenceRule.RuleIndex(85));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void SetUp(uint match, uint home, uint away);

        // A stand-in for the match set-up (cdecl: match, home club, away club): rolls 80 for the home
        // team and 90 for the away team, and records its arguments and calls.
        private static byte[] StandIn(uint record) => new[]
        {
            new byte[] { 0x8B, 0x44, 0x24, 0x04 },                                                         // mov eax, [esp+4]
            new byte[] { 0x66, 0xC7, 0x80 }, BitConverter.GetBytes(OverconfidenceRule.HomeFactorOffset), new byte[] { 80, 0 },  // mov word [eax+0x70], 80
            new byte[] { 0x66, 0xC7, 0x80 }, BitConverter.GetBytes(OverconfidenceRule.AwayFactorOffset), new byte[] { 90, 0 },  // mov word [eax+0x158], 90
            new byte[] { 0xA3 }, BitConverter.GetBytes(record),                                             // mov [rec], eax
            new byte[] { 0x8B, 0x44, 0x24, 0x08, 0xA3 }, BitConverter.GetBytes(record + 4),                 // mov eax, [esp+8]; mov [rec+4], eax
            new byte[] { 0x8B, 0x44, 0x24, 0x0C, 0xA3 }, BitConverter.GetBytes(record + 8),                 // mov eax, [esp+0xC]; mov [rec+8], eax
            new byte[] { 0xFF, 0x05 }, BitConverter.GetBytes(record + 12),                                  // inc dword [rec+12]
            new byte[] { 0xC3 },                                                                            // ret
        }.SelectMany(p => p).ToArray();

        // Runs the stub as the game calls the match set-up: after the set-up, a human team gets the own
        // rule's factor and the opponent of a human team the opponents' rule's; 0 keeps the dice, and
        // a match between computer teams stays as it was set up.
        [Theory]
        [InlineData(0, 0, true, false, 80, 90)]
        [InlineData(100, 70, true, false, 100, 70)]
        [InlineData(100, 70, false, true, 70, 100)]
        [InlineData(100, 70, false, false, 80, 90)]
        [InlineData(100, 70, true, true, 100, 100)]
        [InlineData(0, 70, true, false, 80, 70)]
        [InlineData(100, 0, false, true, 80, 100)]
        public void BuildStub_SetsTheFactorsAfterTheMatchSetUp(byte own, byte opponent, bool homeHuman, bool awayHuman, short home, short away)
        {
            using (FakeModule fake = new FakeModule())
            {
                uint block = fake.Memory.AllocateExecutable(0x1000).Value;
                uint stub = block;
                uint setUp = block + 0x100;
                uint record = block + 0x180;
                uint match = block + 0x200;
                uint homeClub = block + 0x600;
                uint awayClub = block + 0x800;

                fake.Memory.WriteBytesAtAddress(setUp, StandIn(record));
                fake.Memory.WriteBytesAtAddress(stub, OverconfidenceRule.BuildStub(stub, setUp));
                fake.Memory.WriteBytesAtAddress(stub + OverconfidenceRule.OwnOffset, new[] { own, opponent });
                fake.Memory.WriteBytesAtAddress(homeClub + Settings.ClubHumanOffset, new[] { (byte)(homeHuman ? 1 : 0) });
                fake.Memory.WriteBytesAtAddress(awayClub + Settings.ClubHumanOffset, new[] { (byte)(awayHuman ? 2 : 0) });
                SetUp run = Marshal.GetDelegateForFunctionPointer<SetUp>(new IntPtr(stub + OverconfidenceRule.CodeOffset));

                run(match, homeClub, awayClub);

                short Factor(uint offset) => BitConverter.ToInt16(fake.Memory.ReadBytesAtAddress(match + offset, 2), 0);
                uint Recorded(uint offset) => BitConverter.ToUInt32(fake.Memory.ReadBytesAtAddress(record + offset, 4), 0);
                Assert.Equal(home, Factor(OverconfidenceRule.HomeFactorOffset));
                Assert.Equal(away, Factor(OverconfidenceRule.AwayFactorOffset));
                Assert.Equal(match, Recorded(0));
                Assert.Equal(homeClub, Recorded(4));
                Assert.Equal(awayClub, Recorded(8));
                Assert.Equal(1u, Recorded(12));
            }
        }
    }
}
