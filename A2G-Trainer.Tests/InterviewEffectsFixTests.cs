using System;
using System.Linq;
using System.Runtime.InteropServices;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for InterviewEffectsFix: redirecting the game's effect-application and morale calls to
    /// the stub, switching it on and off, refusing foreign patches - and running the generated x86
    /// stub against stand-ins for the game's effect application and morale change.
    /// </summary>
    public class InterviewEffectsFixTests
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
            WriteCall(fake, Settings.InterviewApplyCallSiteOffset, Absolute(fake, Settings.InterviewApplyFunctionOffset));
            WriteCall(fake, Settings.InterviewMoraleCallSiteOffset, Absolute(fake, Settings.InterviewMoraleFunctionOffset));
        }

        private static uint CallTarget(FakeModule fake, uint siteOffset)
        {
            byte[] call = fake.ReadModuleBytes(siteOffset, 5);
            return unchecked((uint)(Absolute(fake, siteOffset) + 5 + BitConverter.ToInt32(call, 1)));
        }

        private static uint Stub(FakeModule fake) => CallTarget(fake, Settings.InterviewApplyCallSiteOffset) - InterviewEffectsFix.CodeOffset;

        private static byte Flag(FakeModule fake) => fake.Memory.ReadBytesAtAddress(Stub(fake) + InterviewEffectsFix.FlagOffset, 1)[0];

        [Fact]
        public void SetEnabled_RedirectsBothCallsOnce_AndSwitchesTheFlag()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                InterviewEffectsFix fix = new InterviewEffectsFix(fake.Memory);

                Assert.True(fix.IsSupported);
                Assert.True(fix.SetEnabled(true));
                uint stub = Stub(fake);
                Assert.Equal(InterviewEffectsFix.Signature, fake.Memory.ReadBytesAtAddress(stub, InterviewEffectsFix.Signature.Length));
                Assert.Equal(stub + InterviewEffectsFix.MoraleCodeOffset, CallTarget(fake, Settings.InterviewMoraleCallSiteOffset));
                Assert.Equal(1, Flag(fake));

                Assert.True(fix.SetEnabled(false));
                Assert.Equal(0, Flag(fake));
                // On again: the same stub, no second redirect.
                Assert.True(new InterviewEffectsFix(fake.Memory).SetEnabled(true));
                Assert.Equal(stub, Stub(fake));
                Assert.Equal(1, Flag(fake));
            }
        }

        [Fact]
        public void SetEnabled_OffWithoutAStub_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);

                Assert.True(new InterviewEffectsFix(fake.Memory).SetEnabled(false));
                Assert.Equal(Absolute(fake, Settings.InterviewApplyFunctionOffset), CallTarget(fake, Settings.InterviewApplyCallSiteOffset));
            }
        }

        [Fact]
        public void SetEnabled_OnAForeignPatch_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                WriteCall(fake, Settings.InterviewApplyCallSiteOffset, Absolute(fake, 0x10000));

                Assert.False(new InterviewEffectsFix(fake.Memory).SetEnabled(true));
            }
        }

        [Fact]
        public void SetEnabled_WithTheMoraleCallPatchedElsewhere_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                WriteCall(fake, Settings.InterviewMoraleCallSiteOffset, Absolute(fake, 0x10000));

                Assert.False(new InterviewEffectsFix(fake.Memory).SetEnabled(true));
                Assert.Equal(Absolute(fake, Settings.InterviewApplyFunctionOffset), CallTarget(fake, Settings.InterviewApplyCallSiteOffset));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SetEnabled_WithoutACallAtACallSite_Fails(bool atApply)
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.WriteModuleBytes(atApply ? Settings.InterviewApplyCallSiteOffset : Settings.InterviewMoraleCallSiteOffset, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 });

                Assert.False(new InterviewEffectsFix(fake.Memory).SetEnabled(true));
            }
        }

        [Fact]
        public void SetEnabled_OnABuildWithoutConfirmedAddresses_FailsOnlyForOn()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                InterviewEffectsFix fix = new InterviewEffectsFix(fake.Memory);

                Assert.False(fix.IsSupported);
                Assert.False(fix.SetEnabled(true));
                Assert.True(fix.SetEnabled(false));
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ApplyEntry(uint context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void MoraleEntry(uint club, int value);

        // A stand-in that records its two stack arguments and counts its calls (cdecl).
        private static byte[] Recorder(uint record) => new[]
        {
            new byte[] { 0x8B, 0x44, 0x24, 0x04, 0xA3 }, BitConverter.GetBytes(record),         // mov eax,[esp+4]; mov [rec],eax
            new byte[] { 0x8B, 0x44, 0x24, 0x08, 0xA3 }, BitConverter.GetBytes(record + 4),     // mov eax,[esp+8]; mov [rec+4],eax
            new byte[] { 0xFF, 0x05 }, BitConverter.GetBytes(record + 8),                       // inc dword [rec+8]
            new byte[] { 0xC3 },                                                                // ret
        }.SelectMany(p => p).ToArray();

        private static int Int(FakeModule fake, uint address) => BitConverter.ToInt32(fake.Memory.ReadBytesAtAddress(address, 4), 0);

        // Runs the apply entry as the game calls it (cdecl, the context on the stack): switched on it
        // copies the engine copy's pairs into the context, sending 10 to the morale case and marking a
        // fine; switched off - or without an engine or its copy - it zeroes them; either way it then
        // continues to the effect application.
        [Fact]
        public void BuildStub_ApplyEntry_RunsAgainstAStandInForTheEffectApplication()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint block = fake.Memory.AllocateExecutable(0x400).Value;
                uint stub = block;
                uint apply = block + 0x100;
                uint record = block + 0x140;
                uint engine = block + 0x180;
                uint engineObject = block + 0x200;
                uint copy = block + 0x340;
                uint context = block + 0x380;

                fake.Memory.WriteBytesAtAddress(apply, Recorder(record));
                fake.Memory.WriteBytesAtAddress(stub, InterviewEffectsFix.BuildStub(stub, engine, context, apply, 0, 0));
                fake.Memory.WriteBytesAtAddress(engine, BitConverter.GetBytes(engineObject));
                fake.Memory.WriteBytesAtAddress(engineObject + InterviewEffectsFix.EngineContextOffset, BitConverter.GetBytes(copy));

                void Pairs(params int[] values) => fake.Memory.WriteBytesAtAddress(copy + InterviewEffectsFix.EffectsOffset, values.SelectMany(BitConverter.GetBytes).ToArray());
                int[] Effects() => Enumerable.Range(0, 4).Select(i => Int(fake, context + InterviewEffectsFix.EffectsOffset + (uint)i * 4)).ToArray();
                ApplyEntry run = Marshal.GetDelegateForFunctionPointer<ApplyEntry>(new IntPtr(stub + InterviewEffectsFix.CodeOffset));

                fake.Memory.WriteBytesAtAddress(stub + InterviewEffectsFix.FlagOffset, BitConverter.GetBytes(1u));
                Pairs(11, -3, 12, 20000);
                run(context);
                Assert.Equal(new[] { 11, -3, 12, 0x40000000 | 20000 }, Effects());

                Pairs(12, 100000, 10, -5);
                run(context);
                Assert.Equal(new[] { 12, 0x40000000 | 100000, 12, -5 }, Effects());

                Pairs(5, -2, 1, 4);
                run(context);
                Assert.Equal(new[] { 5, -2, 1, 4 }, Effects());

                fake.Memory.WriteBytesAtAddress(engineObject + InterviewEffectsFix.EngineContextOffset, BitConverter.GetBytes(0u));
                run(context);
                Assert.Equal(new int[4], Effects());

                fake.Memory.WriteBytesAtAddress(engineObject + InterviewEffectsFix.EngineContextOffset, BitConverter.GetBytes(copy));
                run(context);
                fake.Memory.WriteBytesAtAddress(engine, BitConverter.GetBytes(0u));
                run(context);
                Assert.Equal(new int[4], Effects());

                fake.Memory.WriteBytesAtAddress(engine, BitConverter.GetBytes(engineObject));
                run(context);
                fake.Memory.WriteBytesAtAddress(stub + InterviewEffectsFix.FlagOffset, BitConverter.GetBytes(0u));
                run(context);
                Assert.Equal(new int[4], Effects());

                Assert.Equal((int)context, Int(fake, record));
                Assert.Equal(8, Int(fake, record + 8));
            }
        }

        // Runs the morale entry as case 12 calls it (cdecl: club, value): a marked fine is booked on the
        // club's other costs in money units (DM * factor / 100), anything else goes on to the morale change.
        [Fact]
        public void BuildStub_MoraleEntry_BooksFinesAndPassesTheRestOn()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint block = fake.Memory.AllocateExecutable(0x1000).Value;
                uint stub = block;
                uint morale = block + 0x100;
                uint record = block + 0x140;
                uint moneyFactor = block + 0x180;
                uint club = block + 0x200;

                fake.Memory.WriteBytesAtAddress(morale, Recorder(record));
                fake.Memory.WriteBytesAtAddress(moneyFactor, BitConverter.GetBytes(1000));
                fake.Memory.WriteBytesAtAddress(club + Settings.ClubOtherCostsOffset, BitConverter.GetBytes(5000));
                fake.Memory.WriteBytesAtAddress(stub, InterviewEffectsFix.BuildStub(stub, 0, 0, 0, moneyFactor, morale));
                MoraleEntry run = Marshal.GetDelegateForFunctionPointer<MoraleEntry>(new IntPtr(stub + InterviewEffectsFix.MoraleCodeOffset));

                run(club, 0x40000000 | 20000);
                Assert.Equal(5000 + 200000, Int(fake, club + Settings.ClubOtherCostsOffset));
                run(0, 0x40000000 | 100000);
                Assert.Equal(0, Int(fake, record + 8));

                run(club, -5);
                Assert.Equal((int)club, Int(fake, record));
                Assert.Equal(-5, Int(fake, record + 4));
                run(club, 7);
                Assert.Equal(7, Int(fake, record + 4));
                Assert.Equal(2, Int(fake, record + 8));
                Assert.Equal(205000, Int(fake, club + Settings.ClubOtherCostsOffset));
            }
        }
    }
}
