using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Failure paths of the code the trainer puts into the game: OverconfidenceRule without memory for
    /// its stub or with a call site that can't be patched, InterviewEffectsFix's switch in a stub
    /// whose flag can't be read, TransferInjector with an unreadable call site, X86Assembler's short
    /// jump backwards out of range, and a live match whose player copies become unreadable.
    /// </summary>
    public class StubInstallGapTests
    {
        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessQueryInformation = 0x0400;
        private const int Page = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        private static uint CallTarget(FakeModule fake, uint siteOffset)
        {
            byte[] call = fake.ReadModuleBytes(siteOffset, 5);
            return unchecked((uint)(fake.ModuleBase + siteOffset + 5 + BitConverter.ToInt32(call, 1)));
        }

        private static void WriteCall(FakeModule fake, uint siteOffset, uint target)
        {
            int rel = unchecked((int)(target - (fake.ModuleBase + siteOffset + 5)));
            fake.WriteModuleBytes(siteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
        }

        // ---- OverconfidenceRule ---------------------------------------------------------------

        [Fact]
        public void OverconfidenceRule_WithoutMemoryForTheStub_InstallsNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                OverconfidenceRuleTests.WriteOriginalCalls(fake);
                IntPtr full = fake.Memory.mProc.Handle;
                // A handle that may read the game but not allocate in it.
                IntPtr readOnly = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, Process.GetCurrentProcess().Id);
                Assert.NotEqual(IntPtr.Zero, readOnly);
                try
                {
                    fake.Memory.mProc.Handle = readOnly;
                    OverconfidenceRule rule = new OverconfidenceRule(fake.Memory);

                    Assert.True(rule.IsSupported);
                    Assert.Null(rule.EnsureInstalled());
                    Assert.False(rule.SetRule(100, 0));
                }
                finally
                {
                    fake.Memory.mProc.Handle = full;
                    CloseHandle(readOnly);
                }
                foreach (uint site in Settings.MatchInitCallSiteOffsets)
                    Assert.Equal(fake.ModuleBase + Settings.MatchInitFunctionOffset, CallTarget(fake, site));
            }
        }

        // The second call site lies 0x11CC bytes after the first.
        [Theory]
        [InlineData(false)] // both call sites read-only: the first can't be redirected
        [InlineData(true)]  // the first call site writable, the second just past it read-only
        public void OverconfidenceRule_WhenACallSiteCantBePatched_InstallsNothing(bool firstWritable)
        {
            using (SplitCodeMemory split = new SplitCodeMemory(readOnlyFirst: false))
            {
                uint[] offsets = Settings.MatchInitCallSiteOffsets;
                uint first = firstWritable ? split.ReadOnly - 0x100 : split.ReadOnly + 0x100;
                uint moduleBase = first - offsets[0];
                uint second = moduleBase + offsets[1];
                Assert.True(second >= split.ReadOnly);
                split.SetModuleBase(moduleBase);
                uint function = moduleBase + Settings.MatchInitFunctionOffset;
                split.WriteCall(first, function);
                split.WriteCall(second, function);
                OverconfidenceRule rule = new OverconfidenceRule(split.Memory);

                Assert.True(rule.IsSupported);
                Assert.Null(rule.EnsureInstalled());

                Assert.Equal(function, split.CallTarget(second));
                Assert.Equal(firstWritable, split.CallTarget(first) != function);
            }
        }

        // ---- InterviewEffectsFix --------------------------------------------------------------

        // A stub of the fix at a module offset whose signature is the last 8 bytes of a readable page.
        private const uint StubOffset = 0x500000 - 8;

        private static void PlantInterviewStub(FakeModule fake, byte flag)
        {
            InterviewEffectsFixTests.WriteOriginalCalls(fake);
            WriteCall(fake, Settings.InterviewApplyCallSiteOffset, fake.ModuleBase + StubOffset + InterviewEffectsFix.CodeOffset);
            fake.WriteModuleBytes(StubOffset, InterviewEffectsFix.Signature);
            fake.WriteModuleBytes(StubOffset + InterviewEffectsFix.FlagOffset, new[] { flag });
        }

        [Theory]
        [InlineData(1, true)]
        [InlineData(0, false)]
        public void InterviewEffectsFix_IsEnabled_FollowsTheStubsFlag(byte flag, bool enabled)
        {
            using (FakeModule fake = new FakeModule())
            {
                PlantInterviewStub(fake, flag);

                Assert.Equal(enabled, new InterviewEffectsFix(fake.Memory).IsEnabled);
            }
        }

        [Fact]
        public void InterviewEffectsFix_WithAFlagThatCantBeRead_IsOff()
        {
            using (FakeModule fake = new FakeModule())
            {
                PlantInterviewStub(fake, 1);
                // The flag is the first byte of the next page.
                fake.MakeModuleRegionUnreadable(StubOffset + InterviewEffectsFix.FlagOffset, 1);
                Assert.Equal(InterviewEffectsFix.Signature, fake.ReadModuleBytes(StubOffset, InterviewEffectsFix.Signature.Length));

                Assert.False(new InterviewEffectsFix(fake.Memory).IsEnabled);
            }
        }

        // ---- TransferInjector -----------------------------------------------------------------

        [Fact]
        public void TransferInjector_WithACallSiteThatCantBeRead_InstallsNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                foreach (uint site in Settings.TransferMarketCallSiteOffsets)
                    WriteCall(fake, site, fake.ModuleBase + Settings.TransferMarketFunctionOffset);
                WriteCall(fake, Settings.TransferDeadlineCallSiteOffset, fake.ModuleBase + Settings.TransferDeadlineFunctionOffset);
                fake.MakeModuleRegionUnreadable(Settings.TransferMarketCallSiteOffsets[0], 5);
                TransferInjector injector = new TransferInjector(fake.Memory);

                Assert.True(injector.IsSupported);
                Assert.Null(injector.EnsureInstalled());
            }
        }

        // ---- X86Assembler ---------------------------------------------------------------------

        [Fact]
        public void X86Assembler_ShortJumpTooFarBack_Throws()
        {
            X86Assembler a = new X86Assembler();
            a.Mark("back");
            a.Emit(new byte[200]);
            a.Jump(0xEB, "back");

            Assert.Throws<InvalidOperationException>(() => a.ToArray());
        }

        // ---- LiveMatch ------------------------------------------------------------------------

        // The squad is read lazily: before it hands out the player its copy's page is made unreadable,
        // after the working copies were collected. A copy whose flags can't be read counts as unused.
        [Fact]
        public void LiveMatch_BenchCandidates_WithAPlayerCopyThatCantBeRead_TakesHimAsUnused()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                LiveMatchTests.SeedMatch(fake);
                // Slot 0 of the player cache: player 140, sent off.
                uint copy = Settings.PlayerCacheTableOffset;
                fake.WriteModuleBytes(copy, new byte[] { 1 });
                fake.WriteModuleBytes(copy + Settings.PlayerCacheCopyOffset, BitConverter.GetBytes((ushort)140));
                fake.WriteModuleBytes(copy + Settings.PlayerCacheCopyOffset + Settings.PlayerMatchFlagsOffset, new byte[] { 0x21 });
                LiveMatch match = LiveMatch.Find(fake.Memory, FixedAddresses.For(fake.Memory), ClubEditorSettingsTests.Country, ClubEditorSettingsTests.OwnId,
                    fake.ModuleBase + 0x51A000, fake.ModuleBase + 0x51F000);
                Assert.NotNull(match);
                Assert.Empty(match.BenchCandidates(new ushort[] { 140 }));

                IEnumerable<ushort> Squad()
                {
                    fake.MakeModuleRegionUnreadable(copy + Settings.PlayerCacheCopyOffset + Settings.PlayerMatchFlagsOffset, 1);
                    yield return 140;
                }

                Assert.Equal(new ushort[] { 140 }, match.BenchCandidates(Squad()));
            }
        }
    }
}
