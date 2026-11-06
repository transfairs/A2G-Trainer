using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// The injectors when no stub can be allocated in the game: attached with a handle that may read
    /// the call sites but not allocate (no PROCESS_VM_OPERATION), installing gives nothing and leaves
    /// the calls as they were.
    /// </summary>
    public class InjectorAllocationTests
    {
        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessQueryInformation = 0x0400;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        private static void WriteCall(FakeModule fake, uint siteOffset, uint targetOffset)
        {
            int rel = unchecked((int)(fake.ModuleBase + targetOffset - (fake.ModuleBase + siteOffset + 5)));
            fake.WriteModuleBytes(siteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
        }

        // Runs the check with the memory attached through a read-only handle.
        private static void ReadOnly(FakeModule fake, Action check)
        {
            IntPtr readOnly = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, Process.GetCurrentProcess().Id);
            Assert.NotEqual(IntPtr.Zero, readOnly);
            IntPtr original = fake.Memory.mProc.Handle;
            fake.Memory.mProc.Handle = readOnly;
            try
            {
                Assert.Null(fake.Memory.AllocateExecutable(0x100));
                check();
            }
            finally
            {
                fake.Memory.mProc.Handle = original;
                CloseHandle(readOnly);
            }
        }

        [Fact]
        public void NationalCoachInjector_WithoutAStub_InstallsNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteCall(fake, Settings.OfferGenerateCallSiteOffset, Settings.OfferGenerateFunctionOffset);
                byte[] call = fake.ReadModuleBytes(Settings.OfferGenerateCallSiteOffset, 5);

                ReadOnly(fake, () => Assert.Null(new NationalCoachInjector(fake.Memory).EnsureInstalled()));

                Assert.Equal(call, fake.ReadModuleBytes(Settings.OfferGenerateCallSiteOffset, 5));
            }
        }

        [Fact]
        public void TransferInjector_WithoutAStub_InstallsNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                foreach (uint site in Settings.TransferMarketCallSiteOffsets)
                    WriteCall(fake, site, Settings.TransferMarketFunctionOffset);
                WriteCall(fake, Settings.TransferDeadlineCallSiteOffset, Settings.TransferDeadlineFunctionOffset);
                byte[] deadlineCall = fake.ReadModuleBytes(Settings.TransferDeadlineCallSiteOffset, 5);

                ReadOnly(fake, () =>
                {
                    TransferInjector injector = new TransferInjector(fake.Memory);
                    Assert.True(injector.IsSupported);
                    Assert.Null(injector.EnsureInstalled());
                });

                Assert.Equal(deadlineCall, fake.ReadModuleBytes(Settings.TransferDeadlineCallSiteOffset, 5));
            }
        }
    }
}
