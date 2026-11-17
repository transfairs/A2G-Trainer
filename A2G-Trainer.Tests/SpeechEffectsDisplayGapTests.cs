using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// More tests for SpeechEffectsDisplay: installing fails cleanly without memory for the stub or
    /// where a call can't be redirected, switching on twice, the timer's tick, a tick without the
    /// build's addresses, without or with failing human clubs, and speeches outside the table.
    /// </summary>
    public class SpeechEffectsDisplayGapTests
    {
        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessQueryInformation = 0x0400;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        private static uint CallTarget(FakeModule fake, uint siteOffset)
        {
            byte[] call = fake.ReadModuleBytes(siteOffset, 5);
            return unchecked((uint)(fake.ModuleBase + siteOffset + 5 + BitConverter.ToInt32(call, 1)));
        }

        private static uint Dword(FakeModule fake, uint address) => BitConverter.ToUInt32(fake.Memory.ReadBytesAtAddress(address, 4), 0);

        [Fact]
        public void EnsureInstalled_WithoutMemoryForTheStub_FailsAndLeavesTheCallsAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                SpeechEffectsDisplayTests.WriteOriginalCalls(fake);
                IntPtr full = fake.Memory.mProc.Handle;
                // A handle that may read the game but not allocate in it.
                IntPtr readOnly = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, Process.GetCurrentProcess().Id);
                Assert.NotEqual(IntPtr.Zero, readOnly);
                try
                {
                    fake.Memory.mProc.Handle = readOnly;
                    SpeechEffectsDisplay display = new SpeechEffectsDisplay(fake.Memory, null);

                    Assert.True(display.IsSupported);
                    Assert.Null(display.EnsureInstalled());
                    Assert.False(display.SetEnabled(true));
                    Assert.False(display.Enabled);
                }
                finally
                {
                    fake.Memory.mProc.Handle = full;
                    CloseHandle(readOnly);
                }
                Assert.Equal(fake.ModuleBase + Settings.SpeechListFunctionOffset, CallTarget(fake, Settings.SpeechListCallSiteOffset));
                Assert.Equal(fake.ModuleBase + Settings.SpeechCheckReleaseFunctionOffset, CallTarget(fake, Settings.SpeechCheckReleaseSiteOffset));
            }
        }

        // The release call lies 0x1290 bytes after the list call. Read-only first: with the list call
        // just below the writable block the release call is in it; further down both are read-only.
        [Theory]
        [InlineData(false)] // both calls read-only: the release call (redirected first) can't be patched
        [InlineData(true)]  // the release call writable, the list call read-only
        public void EnsureInstalled_WhenACallCantBeRedirected_Fails(bool releaseWritable)
        {
            using (SplitCodeMemory split = new SplitCodeMemory(readOnlyFirst: true))
            {
                uint listSite = releaseWritable ? split.ReadOnly + SplitCodeMemory.Half - 0x100 : split.ReadOnly + 0x100;
                uint moduleBase = listSite - Settings.SpeechListCallSiteOffset;
                uint releaseSite = moduleBase + Settings.SpeechCheckReleaseSiteOffset;
                Assert.Equal(releaseWritable, releaseSite >= split.Writable);
                split.SetModuleBase(moduleBase);
                split.WriteCall(listSite, moduleBase + Settings.SpeechListFunctionOffset);
                split.WriteCall(releaseSite, moduleBase + Settings.SpeechCheckReleaseFunctionOffset);
                SpeechEffectsDisplay display = new SpeechEffectsDisplay(split.Memory, null);

                Assert.True(display.IsSupported);
                Assert.Null(display.EnsureInstalled());

                Assert.Equal(moduleBase + Settings.SpeechListFunctionOffset, split.CallTarget(listSite));
                uint releaseTarget = split.CallTarget(releaseSite);
                if (releaseWritable)
                {
                    // The release entry is in place; without the list call no check runs from it, so that's harmless.
                    Assert.Equal(SpeechEffectsDisplay.Signature, split.Memory.ReadBytesAtAddress(releaseTarget - SpeechEffectsDisplay.ReleaseCodeOffset, SpeechEffectsDisplay.Signature.Length));
                }
                else
                    Assert.Equal(moduleBase + Settings.SpeechCheckReleaseFunctionOffset, releaseTarget);
            }
        }

        [Fact]
        public void SetEnabled_OnTwice_KeepsTheOneTimer()
        {
            using (FakeModule fake = new FakeModule())
            {
                SpeechEffectsDisplayTests.WriteOriginalCalls(fake);
                using (SpeechEffectsDisplay display = new SpeechEffectsDisplay(fake.Memory, null))
                {
                    Assert.True(display.SetEnabled(true));
                    object timer = typeof(SpeechEffectsDisplay).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(display);

                    Assert.True(display.SetEnabled(true));

                    Assert.Same(timer, typeof(SpeechEffectsDisplay).GetField("timer", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(display));
                    Assert.True(display.Enabled);
                }
            }
        }

        [Fact]
        public void StubFunctions_OfAnAddressThatCantBeRead_AreNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                // Below the lowest address ProcessMemory reads.
                Assert.Null(SpeechEffectsDisplay.ListFunctionOf(fake.Memory, 0x100));
                Assert.Null(SpeechEffectsDisplay.ReleaseFunctionOf(fake.Memory, 0x100));
            }
        }

        // The timer's tick: only while switched on (a tick queued before switching off writes nothing).
        [Fact]
        public void TimerTick_FillsTheTablesOnlyWhileSwitchedOn()
        {
            using (FakeModule fake = new FakeModule())
            {
                SpeechEffectsDisplayTests.WriteOriginalCalls(fake);
                MethodInfo timerTick = typeof(SpeechEffectsDisplay).GetMethod("TimerTick", BindingFlags.NonPublic | BindingFlags.Instance);
                KeyValuePair<PlayerEnums.Country, ushort>[] clubs = { new KeyValuePair<PlayerEnums.Country, ushort>(PlayerEnums.Country.Deutschland, 1) };
                int asked = 0;
                using (SpeechEffectsDisplay display = new SpeechEffectsDisplay(fake.Memory, () => { asked++; return clubs; }))
                {
                    Assert.NotNull(display.EnsureInstalled());
                    timerTick.Invoke(display, null);
                    Assert.Equal(0, asked);

                    Assert.True(display.SetEnabled(true));
                    int before = asked;
                    timerTick.Invoke(display, null);
                    Assert.True(asked > before);
                }
            }
        }

        [Fact]
        public void Tick_WithoutTheBuildsAddresses_DoesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                SpeechEffectsDisplayTests.WriteOriginalCalls(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                bool asked = false;
                SpeechEffectsDisplay display = new SpeechEffectsDisplay(fake.Memory, () => { asked = true; return null; });

                display.Tick();

                Assert.False(asked);
            }
        }

        // Without a club list, with one that gives nothing and with one that fails: every table free, the failure only logged.
        [Fact]
        public void Tick_WithoutHumanClubs_FreesEveryTable_AndSurvivesAFailingList()
        {
            using (FakeModule fake = new FakeModule())
            {
                SpeechEffectsDisplayTests.WriteOriginalCalls(fake);
                Func<IEnumerable<KeyValuePair<PlayerEnums.Country, ushort>>>[] lists =
                {
                    null,
                    () => null,
                    () => throw new InvalidOperationException("no clubs"),
                };
                foreach (Func<IEnumerable<KeyValuePair<PlayerEnums.Country, ushort>>> list in lists)
                {
                    SpeechEffectsDisplay display = new SpeechEffectsDisplay(fake.Memory, list);
                    uint stub = display.EnsureInstalled().Value;
                    uint tables = stub + SpeechEffectsDisplay.TablesOffset;
                    fake.Memory.WriteBytesAtAddress(tables, BitConverter.GetBytes(0x1234u));

                    display.Tick();

                    Assert.Equal(list == lists[2] ? 0x1234u : SpeechEffectsDisplay.FreeKey, Dword(fake, tables));
                }
            }
        }

        [Fact]
        public void BuildTable_LeavesOutSpeechesOutsideTheTable()
        {
            PreMatchSpeeches.Speech[] speeches =
            {
                new PreMatchSpeeches.Speech { Number = 0, Defence = 5 },
                new PreMatchSpeeches.Speech { Number = PreMatchSpeeches.Count, Defence = 5 },
            };

            byte[] table = SpeechEffectsDisplay.BuildTable(PlayerEnums.Country.Deutschland, 1, speeches, new short[PreMatchSpeeches.Count]);

            Assert.Equal(SpeechEffectsDisplay.TableSize, table.Length);
            Assert.All(table.Skip(4), b => Assert.Equal(0, b));
        }
    }
}
