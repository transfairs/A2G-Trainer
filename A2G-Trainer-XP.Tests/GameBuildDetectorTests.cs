using System;
using A2G_Trainer_XP.Controller;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for GameBuildDetector's PE-header identification and signature fallback, against a
    /// FakeModule whose zero-initialized module block stands in for the game's mapped image.
    /// </summary>
    public class GameBuildDetectorTests
    {
        private const uint UnknownStamp = 0x12345678;
        private const uint SmallImage = 0x30000;

        [Fact]
        public void Identify_WithOriginalHeader_ReturnsOriginalWithOriginalAddressingOnly()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader();

                GameBuild build = GameBuildDetector.Identify(fake.Memory);

                Assert.Same(GameBuild.Original, build);
                Assert.True(build.IsKnown);
                Assert.Equal(new[] { false }, build.CandidateAddressings);
            }
        }

        public static TheoryData<string> GogAddressedBuilds => new TheoryData<string> { nameof(GameBuild.Gog), nameof(GameBuild.Cd2007) };

        [Theory]
        [MemberData(nameof(GogAddressedBuilds))]
        public void Identify_WithGogAddressedBuildHeader_ReturnsThatBuildWithGogAddressingOnly(string buildField)
        {
            GameBuild expected = (GameBuild)typeof(GameBuild).GetField(buildField).GetValue(null);
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader(expected.TimeDateStamp, expected.SizeOfImage);

                GameBuild build = GameBuildDetector.Identify(fake.Memory);

                Assert.Same(expected, build);
                Assert.True(build.IsKnown);
                Assert.Equal(new[] { true }, build.CandidateAddressings);
            }
        }

        [Fact]
        public void Identify_WithVerlaengerungHeader_ReturnsUnsupportedBuild()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader(GameBuild.Verlaengerung.TimeDateStamp, GameBuild.Verlaengerung.SizeOfImage);

                GameBuild build = GameBuildDetector.Identify(fake.Memory);

                Assert.Same(GameBuild.Verlaengerung, build);
                Assert.False(build.IsSupported);
            }
        }

        [Fact]
        public void Identify_WithOriginalStampButDifferentImageSize_IsNotTakenForTheOriginal()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader(sizeOfImage: SmallImage);

                Assert.Null(GameBuildDetector.Identify(fake.Memory));
            }
        }

        [Fact]
        public void Identify_WithUnknownHeaderAndSignature_ReturnsUnidentifiedTryingGogFirst()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader(UnknownStamp, SmallImage);
                fake.WriteModuleBytes(0x1234, GameBuildDetector.Signature);

                GameBuild build = GameBuildDetector.Identify(fake.Memory);

                Assert.NotNull(build);
                Assert.False(build.IsKnown);
                Assert.True(build.IsSupported);
                Assert.Equal(new[] { true, false }, build.CandidateAddressings);
                Assert.Contains("0x12345678", build.Name);
            }
        }

        [Fact]
        public void Identify_WithUnknownHeaderAndNoSignature_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader(UnknownStamp, SmallImage);

                Assert.Null(GameBuildDetector.Identify(fake.Memory));
            }
        }

        [Fact]
        public void Identify_WithSignatureBeyondSizeOfImage_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader(UnknownStamp, SmallImage);
                fake.WriteModuleBytes(SmallImage + 0x100, GameBuildDetector.Signature);

                Assert.Null(GameBuildDetector.Identify(fake.Memory));
            }
        }

        [Fact]
        public void Identify_WithoutMzHeader_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                Assert.Null(GameBuildDetector.Identify(fake.Memory));
            }
        }

        [Fact]
        public void Identify_WithUnreadableModuleBase_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader();
                fake.MakeModuleRegionUnreadable(0, 0x40);

                Assert.Null(GameBuildDetector.Identify(fake.Memory));
            }
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(0x2000u)]
        public void TryReadPeHeader_WithImplausibleNtOffset_ReturnsFalse(uint ntOffset)
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader();
                fake.WriteModuleBytes(0x3C, BitConverter.GetBytes(ntOffset));

                Assert.False(GameBuildDetector.TryReadPeHeader(fake.Memory, out _, out _));
            }
        }

        [Fact]
        public void TryReadPeHeader_WithoutPeSignature_ReturnsFalse()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader();
                fake.WriteModuleBytes(0x80, new byte[] { (byte)'P', (byte)'X' });

                Assert.False(GameBuildDetector.TryReadPeHeader(fake.Memory, out _, out _));
            }
        }

        [Fact]
        public void TryReadPeHeader_WithUnreadableNtHeaders_ReturnsFalse()
        {
            using (FakeModule fake = new FakeModule())
            {
                // NT headers at 0xFC0..0x1018 straddle into the page made unreadable below.
                fake.WritePeHeader(ntOffset: 0xFC0);
                fake.MakeModuleRegionUnreadable(0x1000, 0x10);

                Assert.False(GameBuildDetector.TryReadPeHeader(fake.Memory, out _, out _));
            }
        }

        [Fact]
        public void TryReadPeHeader_WithPe32PlusMagic_ReturnsFalse()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader(magic: 0x20B);

                Assert.False(GameBuildDetector.TryReadPeHeader(fake.Memory, out _, out _));
            }
        }

        [Fact]
        public void TryReadPeHeader_ReadsStampAndImageSize()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WritePeHeader(UnknownStamp, SmallImage);

                Assert.True(GameBuildDetector.TryReadPeHeader(fake.Memory, out uint stamp, out uint size));
                Assert.Equal(UnknownStamp, stamp);
                Assert.Equal(SmallImage, size);
            }
        }

        [Fact]
        public void ContainsSignature_FindsAMatchStraddlingAChunkBoundary()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(0x10000 - 5, GameBuildDetector.Signature);

                Assert.True(GameBuildDetector.ContainsSignature(fake.Memory, fake.ModuleBase, SmallImage));
            }
        }

        [Fact]
        public void ContainsSignature_SkipsUnreadableChunks()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.MakeModuleRegionUnreadable(0, 0x10000);
                fake.WriteModuleBytes(0x20000, GameBuildDetector.Signature);

                Assert.True(GameBuildDetector.ContainsSignature(fake.Memory, fake.ModuleBase, SmallImage));
            }
        }

        [Fact]
        public void IndexOf_ReturnsFirstMatchOrMinusOne()
        {
            byte[] haystack = { 1, 2, 3, 2, 3 };

            Assert.Equal(1, GameBuildDetector.IndexOf(haystack, new byte[] { 2, 3 }));
            Assert.Equal(-1, GameBuildDetector.IndexOf(haystack, new byte[] { 3, 4 }));
            Assert.Equal(-1, GameBuildDetector.IndexOf(new byte[] { 1 }, new byte[] { 1, 2 }));
        }
    }
}
