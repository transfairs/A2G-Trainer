using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using AnstossTrainer.Shared;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// More tests for InterviewEffectsFix: installing the stub fails cleanly when the game's memory
    /// can't be had or one of the call sites can't be patched.
    /// </summary>
    public class InterviewEffectsFixAdditionalTests
    {
        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessQueryInformation = 0x0400;
        private const uint PageReadWrite = 0x04;
        private const uint PageNoAccess = 0x01;
        private const uint MemCommit = 0x1000;
        private const uint MemReserve = 0x2000;
        private const uint MemRelease = 0x8000;
        private const uint FileMapWrite = 0x02;
        private const uint FileMapRead = 0x04;
        private const int Half = 0x10000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAlloc(IntPtr address, UIntPtr size, uint type, uint protect);
        [DllImport("kernel32.dll")]
        private static extern bool VirtualFree(IntPtr address, UIntPtr size, uint type);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFileMapping(IntPtr file, IntPtr attributes, uint protect, uint sizeHigh, uint sizeLow, string name);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFileEx(IntPtr mapping, uint access, uint offsetHigh, uint offsetLow, UIntPtr size, IntPtr address);
        [DllImport("kernel32.dll")]
        private static extern bool UnmapViewOfFile(IntPtr address);

        private static uint CallTarget(ProcessMemory memory, uint site)
        {
            byte[] call = memory.ReadBytesAtAddress(site, 5);
            return unchecked((uint)(site + 5 + BitConverter.ToInt32(call, 1)));
        }

        private static byte[] Call(uint site, uint target) =>
            new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(unchecked((int)(target - (site + 5))))).ToArray();

        [Fact]
        public void SetEnabled_WithoutMemoryForTheStub_FailsAndLeavesTheCallsAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                InterviewEffectsFixTests.WriteOriginalCalls(fake);
                IntPtr full = fake.Memory.mProc.Handle;
                // A handle that may read the game but not allocate in it.
                IntPtr readOnly = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, Process.GetCurrentProcess().Id);
                Assert.NotEqual(IntPtr.Zero, readOnly);
                try
                {
                    fake.Memory.mProc.Handle = readOnly;
                    InterviewEffectsFix fix = new InterviewEffectsFix(fake.Memory);

                    Assert.Null(fix.EnsureInstalled());
                    Assert.False(fix.SetEnabled(true));
                    Assert.Equal(fake.ModuleBase + Settings.InterviewApplyFunctionOffset, CallTarget(fake.Memory, fake.ModuleBase + Settings.InterviewApplyCallSiteOffset));
                    Assert.Equal(fake.ModuleBase + Settings.InterviewMoraleFunctionOffset, CallTarget(fake.Memory, fake.ModuleBase + Settings.InterviewMoraleCallSiteOffset));
                }
                finally
                {
                    fake.Memory.mProc.Handle = full;
                    CloseHandle(readOnly);
                }
            }
        }

        // Two adjacent 64 KB blocks of this process: a writable one, then a read-only view of a file
        // mapping, whose protection can't be lifted - so code there can be read but not patched.
        private sealed class SplitMemory : IDisposable
        {
            public uint Writable { get; }
            public uint ReadOnly => this.Writable + Half;
            private readonly IntPtr mapping;
            private readonly IntPtr writableView;
            private readonly IntPtr readOnlyView;
            private readonly IntPtr low;

            public SplitMemory()
            {
                this.mapping = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, PageReadWrite, 0, Half, null);
                Assert.NotEqual(IntPtr.Zero, this.mapping);
                this.writableView = MapViewOfFileEx(this.mapping, FileMapWrite, 0, 0, (UIntPtr)Half, IntPtr.Zero);
                Assert.NotEqual(IntPtr.Zero, this.writableView);
                for (int attempt = 0; attempt < 10 && this.readOnlyView == IntPtr.Zero; attempt++)
                {
                    IntPtr reserved = VirtualAlloc(IntPtr.Zero, (UIntPtr)(2 * Half), MemReserve, PageNoAccess);
                    VirtualFree(reserved, UIntPtr.Zero, MemRelease);
                    this.low = VirtualAlloc(reserved, (UIntPtr)Half, MemCommit | MemReserve, PageReadWrite);
                    if (this.low == IntPtr.Zero)
                        continue;
                    this.readOnlyView = MapViewOfFileEx(this.mapping, FileMapRead, 0, 0, (UIntPtr)Half, reserved + Half);
                    if (this.readOnlyView == IntPtr.Zero)
                    {
                        VirtualFree(this.low, UIntPtr.Zero, MemRelease);
                        this.low = IntPtr.Zero;
                    }
                }
                Assert.NotEqual(IntPtr.Zero, this.readOnlyView);
                this.Writable = (uint)this.low.ToInt64();
            }

            public void Write(uint address, byte[] data)
            {
                if (address >= this.ReadOnly)
                    Marshal.Copy(data, 0, this.writableView + (int)(address - this.ReadOnly), data.Length);
                else
                    Marshal.Copy(data, 0, new IntPtr((int)address), data.Length);
            }

            public void Dispose()
            {
                UnmapViewOfFile(this.readOnlyView);
                UnmapViewOfFile(this.writableView);
                CloseHandle(this.mapping);
                VirtualFree(this.low, UIntPtr.Zero, MemRelease);
            }
        }

        // Puts the module base where the morale call site lands at moraleSite, and writes both calls as in the unpatched game.
        private static uint PlaceCalls(ProcessMemory memory, SplitMemory split, uint moraleSite)
        {
            uint moduleBase = moraleSite - Settings.InterviewMoraleCallSiteOffset;
            memory.mProc.ModuleBaseAddress = new IntPtr(unchecked((int)moduleBase));
            uint applySite = moduleBase + Settings.InterviewApplyCallSiteOffset;
            split.Write(moraleSite, Call(moraleSite, moduleBase + Settings.InterviewMoraleFunctionOffset));
            split.Write(applySite, Call(applySite, moduleBase + Settings.InterviewApplyFunctionOffset));
            return moduleBase;
        }

        [Theory]
        [InlineData(false)] // both call sites read-only: the morale call can't be redirected
        [InlineData(true)]  // the morale call site writable, the apply call site just past it read-only
        public void SetEnabled_WhenACallSiteCantBePatched_Fails(bool moraleWritable)
        {
            using (SplitMemory split = new SplitMemory())
            {
                ProcessMemory memory = new ProcessMemory();
                Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));
                // The apply call site lies 0x114 bytes after the morale one.
                uint moraleSite = moraleWritable ? split.ReadOnly - 0x100 : split.ReadOnly + 0x100;
                uint moduleBase = PlaceCalls(memory, split, moraleSite);
                uint applySite = moduleBase + Settings.InterviewApplyCallSiteOffset;
                InterviewEffectsFix fix = new InterviewEffectsFix(memory);

                Assert.True(fix.IsSupported);
                Assert.Null(fix.EnsureInstalled());

                Assert.Equal(moduleBase + Settings.InterviewApplyFunctionOffset, CallTarget(memory, applySite));
                uint moraleTarget = CallTarget(memory, moraleSite);
                if (moraleWritable)
                {
                    // The morale entry is already in place; it passes everything unmarked on, so that's harmless.
                    Assert.NotEqual(moduleBase + Settings.InterviewMoraleFunctionOffset, moraleTarget);
                    Assert.Equal(InterviewEffectsFix.Signature, memory.ReadBytesAtAddress(moraleTarget - InterviewEffectsFix.MoraleCodeOffset, InterviewEffectsFix.Signature.Length));
                }
                else
                    Assert.Equal(moduleBase + Settings.InterviewMoraleFunctionOffset, moraleTarget);
            }
        }
    }
}
