using System;
using A2G_Trainer_XP.Controller;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for PersistentLayout and how the resolvers honour an unverified/partial layout.</summary>
    public class PersistentLayoutTests
    {
        [Fact]
        public void Original_IsVerifiedAndExposesTheSettingsAnchors()
        {
            PersistentLayout original = PersistentLayout.Original;

            Assert.True(original.IsVerified);
            Assert.Equal(Settings.PlayerRecordTableOffset, original.PlayerRecordTableOffset);
            Assert.Equal(Settings.NamePoolPointerOffset, original.NamePoolPointerOffset);
            Assert.Equal(Settings.AgeReferenceYearOffset, original.AgeReferenceYearOffset);
            Assert.Equal(Settings.ActiveTrainerCountOffset, original.ActiveTrainerCountOffset);
        }

        [Fact]
        public void GogGuess_IsUnverifiedAndHidesEveryAnchor()
        {
            PersistentLayout guess = PersistentLayout.CreateGogGuess();

            Assert.False(guess.IsVerified);
            Assert.Null(guess.PlayerRecordTableOffset);
            Assert.Null(guess.NamePoolPointerOffset);
            Assert.Null(guess.AgeReferenceYearOffset);
            Assert.Null(guess.ActiveTrainerCountOffset);
            Assert.Contains($"PlayerRecordTable=0x{Settings.PlayerRecordTableOffset + Settings.GogOffset:X}", guess.ToString());
        }

        [Fact]
        public void For_PicksOriginalOrAFreshGogGuess()
        {
            Assert.Same(PersistentLayout.Original, PersistentLayout.For(isGog: false));

            PersistentLayout first = PersistentLayout.For(isGog: true);
            Assert.False(first.IsVerified);
            Assert.NotSame(first, PersistentLayout.For(isGog: true));
        }

        [Fact]
        public void GogOffset_MatchesTheDisplayCacheShift()
        {
            Assert.Equal(0x3140u, Settings.GogOffset);
        }

        [Fact]
        public void ToString_MarksUnknownAnchors()
        {
            string text = new PersistentLayout(0x10, null, null, null, isVerified: true).ToString();

            Assert.Contains("PlayerRecordTable=0x10", text);
            Assert.Contains("NamePoolPointer=unbekannt", text);
            Assert.Contains("Verified=True", text);
        }

        [Fact]
        public void ProcessMemory_DefaultsToTheOriginalLayout()
        {
            Assert.Same(PersistentLayout.Original, new ProcessMemory().Layout);
        }

        [Fact]
        public void PlayerRecordResolver_WithUnverifiedLayout_IsUnavailableAndRefusesAddresses()
        {
            ProcessMemory memory = new ProcessMemory { Layout = PersistentLayout.CreateGogGuess() };
            PlayerRecordResolver resolver = new PlayerRecordResolver(memory);

            Assert.False(resolver.IsAvailable);
            Assert.Throws<InvalidOperationException>(() => resolver.GetFieldAddress(0, 0));
        }

        [Fact]
        public void PlayerRecordResolver_WithExplicitLayout_UsesItsTableOffset()
        {
            ProcessMemory memory = new ProcessMemory();
            PlayerRecordResolver resolver = new PlayerRecordResolver(memory, new PersistentLayout(0x1000, null, null, null, isVerified: true));

            Assert.True(resolver.IsAvailable);
            Assert.Equal(0x1000u + 2u * Settings.PlayerRecordStride + 5u, resolver.GetFieldAddress(2, 5));
        }

        [Fact]
        public void NamePoolResolver_WithUnverifiedLayout_ReturnsNullWithoutReading()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: 0x800000))
            {
                fake.Memory.Layout = PersistentLayout.CreateGogGuess();
                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);

                Assert.Null(resolver.ResolveFirstnameAddress(0));
                Assert.Null(resolver.ResolveLastnameAddress(0));
            }
        }

        [Fact]
        public void NamePoolResolver_WithTableButNoPoolPointer_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(moduleBlockSize: 0x800000))
            {
                fake.Memory.Layout = new PersistentLayout(Settings.PlayerRecordTableOffset, null, null, null, isVerified: true);

                Assert.Null(new NamePoolResolver(fake.Memory).ResolveFirstnameAddress(0));
            }
        }
    }
}
