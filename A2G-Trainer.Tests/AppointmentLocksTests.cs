using System;
using System.Linq;
using System.Runtime.InteropServices;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for AppointmentLocks: redirecting the weekly check to the stub and its modes, lifting and
    /// restoring the limit per selling club, refusing code that isn't the game's - and running the
    /// generated x86 stub against a stand-in for the weekly check.
    /// </summary>
    public class AppointmentLocksTests
    {
        // The weekly check as in the game: its first 6 bytes, the pattern, the rest left zero.
        internal static void WriteOriginalCode(FakeModule fake)
        {
            fake.WriteModuleBytes(Settings.WeeklyLimitFunctionOffset, Settings.WeeklyLimitEntry.Concat(Settings.WeeklyLimitPattern).ToArray());
            fake.WriteModuleBytes(Settings.ClubLimitSiteOffset, Settings.ClubLimitPattern.Concat(new byte[] { AppointmentLocks.ClubLimit }).ToArray());
            fake.WriteModuleBytes(Settings.ClubLimitSite2Offset, Settings.ClubLimitPattern2.Concat(new byte[] { AppointmentLocks.ClubLimit }).ToArray());
        }

        private static byte[] Entry(FakeModule fake) => fake.ReadModuleBytes(Settings.WeeklyLimitFunctionOffset, 6);

        private static byte[] Limits(FakeModule fake) => new[]
        {
            fake.ReadModuleBytes(Settings.ClubLimitSiteOffset + (uint)Settings.ClubLimitPattern.Length, 1)[0],
            fake.ReadModuleBytes(Settings.ClubLimitSite2Offset + (uint)Settings.ClubLimitPattern2.Length, 1)[0],
        };

        [Fact]
        public void SetWeeklyMode_InstallsTheStubOnce_AndKeepsTheMode()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCode(fake);
                AppointmentLocks locks = new AppointmentLocks(fake.Memory);
                Assert.True(locks.IsSupported);
                Assert.Equal(0, locks.WeeklyMode);

                Assert.True(locks.SetWeeklyMode(1));
                byte[] jump = Entry(fake);
                Assert.Equal(0xE9, jump[0]);
                Assert.Equal(0x90, jump[5]);
                uint stub = unchecked((uint)(fake.ModuleBase + Settings.WeeklyLimitFunctionOffset + 5 + BitConverter.ToInt32(jump, 1))) - AppointmentLocks.CodeOffset;
                Assert.Equal(AppointmentLocks.Signature, fake.Memory.ReadBytesAtAddress(stub, 8));
                Assert.Equal(fake.ModuleBase + Settings.WeeklyLimitFunctionOffset + 6, BitConverter.ToUInt32(fake.Memory.ReadBytesAtAddress(stub + AppointmentLocks.ContinueOffset, 4), 0));
                Assert.Equal(1, locks.WeeklyMode);

                Assert.True(new AppointmentLocks(fake.Memory).SetWeeklyMode(2));
                Assert.Equal(jump, Entry(fake));
                Assert.Equal(2, locks.WeeklyMode);
                Assert.True(locks.SetWeeklyMode(0));
                Assert.Equal(0, locks.WeeklyMode);
                Assert.Equal(jump, Entry(fake));
                Assert.False(locks.SetWeeklyMode(3));
            }
        }

        [Fact]
        public void SetWeeklyMode_ToTheGamesLimitWithoutAStub_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCode(fake);
                Assert.True(new AppointmentLocks(fake.Memory).SetWeeklyMode(0));
                Assert.Equal(Settings.WeeklyLimitEntry, Entry(fake));
            }
        }

        [Fact]
        public void SetWeeklyMode_WhereTheCheckStartsOtherwise_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                // The original build's addresses, but no code there.
                Assert.False(new AppointmentLocks(fake.Memory).SetWeeklyMode(1));
                Assert.Equal(new byte[6], Entry(fake));
            }
        }

        [Fact]
        public void SetWeeklyMode_WithTheCheckPatchedElsewhere_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCode(fake);
                // Another tool's jump at the start: not ours.
                fake.WriteModuleBytes(Settings.WeeklyLimitFunctionOffset, new byte[] { 0xE9, 0x00, 0x10, 0x00, 0x00, 0x90 });
                AppointmentLocks locks = new AppointmentLocks(fake.Memory);

                Assert.False(locks.SetWeeklyMode(1));
                Assert.Equal(0, locks.WeeklyMode);
                Assert.Equal(new byte[] { 0xE9, 0x00, 0x10, 0x00, 0x00, 0x90 }, Entry(fake));
            }
        }

        [Fact]
        public void Locks_OnABuildWithoutTheCode_AreNotSupported()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCode(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                AppointmentLocks locks = new AppointmentLocks(fake.Memory);
                Assert.False(locks.IsSupported);
                Assert.False(locks.SetWeeklyMode(2));
                Assert.True(locks.SetWeeklyMode(0));
                Assert.Equal(0, locks.WeeklyMode);
                Assert.False(locks.SetClubLimitLifted(true));
                Assert.True(locks.SetClubLimitLifted(false));
                Assert.False(locks.IsClubLimitLifted);
            }
        }

        [Fact]
        public void SetClubLimitLifted_RaisesAndRestoresBothLimits()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCode(fake);
                AppointmentLocks locks = new AppointmentLocks(fake.Memory);
                Assert.False(locks.IsClubLimitLifted);

                Assert.True(locks.SetClubLimitLifted(true));
                Assert.Equal(new byte[] { 0x7F, 0x7F }, Limits(fake));
                Assert.True(locks.IsClubLimitLifted);
                Assert.True(locks.SetClubLimitLifted(true));

                Assert.True(locks.SetClubLimitLifted(false));
                Assert.Equal(new byte[] { 3, 3 }, Limits(fake));
                Assert.False(locks.IsClubLimitLifted);
            }
        }

        [Fact]
        public void SetClubLimitLifted_WithAnotherLimit_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCode(fake);
                fake.WriteModuleBytes(Settings.ClubLimitSite2Offset + (uint)Settings.ClubLimitPattern2.Length, new byte[] { 5 });
                AppointmentLocks locks = new AppointmentLocks(fake.Memory);

                Assert.False(locks.SetClubLimitLifted(true));
                Assert.Equal(new byte[] { 3, 5 }, Limits(fake));
                // Nothing of ours to take back.
                Assert.True(locks.SetClubLimitLifted(false));
                Assert.Equal(new byte[] { 3, 5 }, Limits(fake));
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        private delegate int WeeklyCheck(int day, uint club);

        // Stands in for the rest of the weekly check: undoes the first 6 bytes (pop ebx / add esp, 8)
        // and answers 1 ("3 negotiations"), counting its calls.
        private static byte[] StandIn(uint calls) => new[]
        {
            new byte[] { 0x5B },                                    // pop ebx
            new byte[] { 0x83, 0xC4, 0x08 },                        // add esp, 8
            new byte[] { 0xFF, 0x05 }, BitConverter.GetBytes(calls), // inc dword [calls]
            new byte[] { 0xB8, 0x01, 0x00, 0x00, 0x00 },            // mov eax, 1
            new byte[] { 0xC2, 0x08, 0x00 },                        // ret 8
        }.SelectMany(p => p).ToArray();

        // Mode 0 always asks the game, 2 never; 1 asks it only for a computer club.
        [Theory]
        [InlineData(0, false, 1, 1)]
        [InlineData(0, true, 1, 1)]
        [InlineData(1, false, 1, 1)]
        [InlineData(1, true, 0, 0)]
        [InlineData(2, false, 0, 0)]
        [InlineData(2, true, 0, 0)]
        public void BuildStub_AnswersByMode(byte mode, bool human, int answer, int calls)
        {
            using (FakeModule fake = new FakeModule())
            {
                uint block = fake.Memory.AllocateExecutable(0x1000).Value;
                uint stub = block;
                uint rest = block + 0x100;
                uint counter = block + 0x180;
                uint club = block + 0x200;
                fake.Memory.WriteBytesAtAddress(rest, StandIn(counter));
                fake.Memory.WriteBytesAtAddress(stub, AppointmentLocks.BuildStub(stub, rest));
                fake.Memory.WriteBytesAtAddress(stub + AppointmentLocks.ModeOffset, new[] { mode });
                fake.Memory.WriteBytesAtAddress(club + Settings.ClubHumanOffset, new[] { (byte)(human ? 1 : 0) });
                WeeklyCheck run = Marshal.GetDelegateForFunctionPointer<WeeklyCheck>(new IntPtr(stub + AppointmentLocks.CodeOffset));

                Assert.Equal(answer, run(123, club));
                Assert.Equal(calls, BitConverter.ToInt32(fake.Memory.ReadBytesAtAddress(counter, 4), 0));
            }
        }
    }
}
