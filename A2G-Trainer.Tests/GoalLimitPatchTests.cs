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
    /// Tests for GoalLimitPatch: lifting and restoring the match engine's goal limit and the halved
    /// chances from 7 goals, in both encodings of the limit, and refusing code it doesn't know.
    /// </summary>
    public class GoalLimitPatchTests
    {
        // The original build's instructions after each pattern (eax, short "and al, -4").
        internal static readonly byte[] OriginalLimit = { 0xC0, 0x48, 0x24, 0xFC, 0x83, 0xC0, 0x0A, 0x89, 0x44 };
        // GOG and the 2007 CD (ecx, long "and ecx, -4").
        internal static readonly byte[] GogLimit = { 0xC1, 0x49, 0x83, 0xE1, 0xFC, 0x83, 0xC1, 0x0A, 0x89 };

        internal static void PlantGoalLimitCode(FakeModule fake, uint limitSite, uint homeSite, uint awaySite, byte[] limit)
        {
            fake.WriteModuleBytes(limitSite, Settings.GoalLimitPattern.Concat(limit).ToArray());
            fake.WriteModuleBytes(homeSite, Settings.GoalHomeBrakePattern.Concat(new byte[] { 0x7E, 0x09, 0x8B, 0xD0 }).ToArray());
            fake.WriteModuleBytes(awaySite, Settings.GoalAwayBrakePattern.Concat(new byte[] { 0x7E, 0x02, 0x03, 0xC9 }).ToArray());
        }

        private static void PlantOriginal(FakeModule fake, byte[] limit = null) =>
            PlantGoalLimitCode(fake, Settings.GoalLimitSiteOffset, Settings.GoalHomeBrakeSiteOffset, Settings.GoalAwayBrakeSiteOffset, limit ?? OriginalLimit);

        private static byte[] Limit(FakeModule fake, uint site = 0) =>
            fake.ReadModuleBytes((site == 0 ? Settings.GoalLimitSiteOffset : site) + (uint)Settings.GoalLimitPattern.Length, 9);

        private static byte HomeJump(FakeModule fake) => fake.ReadModuleBytes(Settings.GoalHomeBrakeSiteOffset + (uint)Settings.GoalHomeBrakePattern.Length, 1)[0];

        private static byte AwayJump(FakeModule fake) => fake.ReadModuleBytes(Settings.GoalAwayBrakeSiteOffset + (uint)Settings.GoalAwayBrakePattern.Length, 1)[0];

        [Fact]
        public void SetEnabled_LiftsTheLimitAndTheBrakes_AndRestoresThem()
        {
            using (FakeModule fake = new FakeModule())
            {
                PlantOriginal(fake);
                GoalLimitPatch patch = new GoalLimitPatch(fake.Memory);

                Assert.True(patch.IsSupported);
                Assert.False(patch.IsEnabled);
                Assert.True(patch.SetEnabled(true));
                // mov eax, 15 / nop, then the next instruction untouched.
                Assert.Equal(new byte[] { 0xC0, 0xB8, 0x0F, 0x00, 0x00, 0x00, 0x90, 0x89, 0x44 }, Limit(fake));
                Assert.Equal(0xEB, HomeJump(fake));
                Assert.Equal(0xEB, AwayJump(fake));
                Assert.True(new GoalLimitPatch(fake.Memory).IsEnabled);
                // On again changes nothing.
                Assert.True(patch.SetEnabled(true));

                Assert.True(patch.SetEnabled(false));
                Assert.Equal(OriginalLimit, Limit(fake));
                Assert.Equal(0x7E, HomeJump(fake));
                Assert.Equal(0x7E, AwayJump(fake));
                Assert.False(patch.IsEnabled);
            }
        }

        [Fact]
        public void SetEnabled_WithTheLongEncoding_RestoresIt()
        {
            using (FakeModule fake = new FakeModule())
            {
                PlantOriginal(fake, GogLimit);
                GoalLimitPatch patch = new GoalLimitPatch(fake.Memory);

                Assert.True(patch.SetEnabled(true));
                Assert.Equal(new byte[] { 0xC1, 0xB9, 0x0F, 0x00, 0x00, 0x00, 0x90, 0x90, 0x89 }, Limit(fake));
                Assert.True(patch.SetEnabled(false));
                Assert.Equal(GogLimit, Limit(fake));
            }
        }

        [Fact]
        public void SetEnabled_WithEaxInTheLongEncoding_RestoresTheLongEncoding()
        {
            byte[] longEax = { 0xC0, 0x48, 0x83, 0xE0, 0xFC, 0x83, 0xC0, 0x0A, 0x89 };
            using (FakeModule fake = new FakeModule())
            {
                PlantOriginal(fake, longEax);
                GoalLimitPatch patch = new GoalLimitPatch(fake.Memory);

                Assert.True(patch.SetEnabled(true));
                Assert.True(patch.IsEnabled);
                Assert.True(patch.SetEnabled(false));
                Assert.Equal(longEax, Limit(fake));
            }
        }

        [Fact]
        public void SetEnabled_OffWhenPartlyPatched_RestoresEverything()
        {
            using (FakeModule fake = new FakeModule())
            {
                PlantOriginal(fake);
                fake.WriteModuleBytes(Settings.GoalHomeBrakeSiteOffset + (uint)Settings.GoalHomeBrakePattern.Length, new byte[] { 0xEB });
                GoalLimitPatch patch = new GoalLimitPatch(fake.Memory);

                Assert.False(patch.IsEnabled);
                Assert.True(patch.SetEnabled(false));
                Assert.Equal(0x7E, HomeJump(fake));
            }
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public void SetEnabled_OnUnknownCode_FailsWithoutWriting(int site)
        {
            using (FakeModule fake = new FakeModule())
            {
                PlantOriginal(fake, site == 0 ? new byte[] { 0xC0, 0x48, 0x24, 0xF8, 0x83, 0xC0, 0x0A, 0x89, 0x44 } : null);
                if (site == 1)
                    fake.WriteModuleBytes(Settings.GoalHomeBrakeSiteOffset + (uint)Settings.GoalHomeBrakePattern.Length, new byte[] { 0x7F });
                if (site == 2)
                    fake.WriteModuleBytes(Settings.GoalAwayBrakeSiteOffset + (uint)Settings.GoalAwayBrakePattern.Length, new byte[] { 0x90 });
                GoalLimitPatch patch = new GoalLimitPatch(fake.Memory);

                Assert.False(patch.SetEnabled(true));
                Assert.False(patch.IsEnabled);
                Assert.True(patch.SetEnabled(false));
                if (site != 1)
                    Assert.Equal(0x7E, HomeJump(fake));
                if (site != 2)
                    Assert.Equal(0x7E, AwayJump(fake));
            }
        }

        [DllImport("kernel32.dll", EntryPoint = "OpenProcess")]
        private static extern IntPtr OpenProcessNative(uint access, bool inherit, int processId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [Fact]
        public void SetEnabled_WhenTheCodeCannotBeWritten_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                PlantOriginal(fake);
                IntPtr full = fake.Memory.mProc.Handle;
                // PROCESS_VM_READ | PROCESS_QUERY_INFORMATION: the sites read fine, but nothing can be written.
                IntPtr readOnly = OpenProcessNative(0x0410, false, Process.GetCurrentProcess().Id);
                Assert.NotEqual(IntPtr.Zero, readOnly);
                try
                {
                    fake.Memory.mProc.Handle = readOnly;
                    GoalLimitPatch patch = new GoalLimitPatch(fake.Memory);

                    Assert.True(patch.IsSupported);
                    Assert.False(patch.SetEnabled(true));
                    Assert.False(patch.IsEnabled);
                    Assert.Equal(OriginalLimit, Limit(fake));
                }
                finally
                {
                    fake.Memory.mProc.Handle = full;
                    CloseHandle(readOnly);
                }
            }
        }

        [Fact]
        public void SetEnabled_WithASetgOnAnotherRegister_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                PlantOriginal(fake, new byte[] { 0xC5, 0x4D, 0x83, 0xE5, 0xFC, 0x83, 0xC5, 0x0A, 0x89 });

                Assert.False(new GoalLimitPatch(fake.Memory).SetEnabled(true));
            }
        }

        [Fact]
        public void SetEnabled_OnABuildWithoutConfirmedAddresses_FailsOnlyForOn()
        {
            using (FakeModule fake = new FakeModule())
            {
                PlantOriginal(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                GoalLimitPatch patch = new GoalLimitPatch(fake.Memory);

                Assert.False(patch.IsSupported);
                Assert.False(patch.IsEnabled);
                Assert.False(patch.SetEnabled(true));
                Assert.True(patch.SetEnabled(false));
                Assert.Equal(OriginalLimit, Limit(fake));
            }
        }

        [Fact]
        public void LimitPatched_IsMovOfTheLimitPaddedToTheOriginalsLength()
        {
            foreach (int register in Enumerable.Range(0, 4))
            {
                foreach (byte[] original in GoalLimitBytes.LimitOriginals(register))
                {
                    byte[] patched = GoalLimitBytes.LimitPatched(register, original.Length);
                    Assert.Equal(original.Length, patched.Length);
                    Assert.Equal(0xB8 + register, patched[0]);
                    Assert.Equal(GoalLimitBytes.MaxGoals, System.BitConverter.ToInt32(patched, 1));
                }
            }
            Assert.Equal(2, GoalLimitBytes.LimitOriginals(0).Length);
            Assert.Single(GoalLimitBytes.LimitOriginals(1));
        }
    }
}
