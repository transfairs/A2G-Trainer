using System;
using System.Linq;
using System.Text;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for InterviewEffectsDisplay: the effect labels, labelling the answers of the game's
    /// question list (a stand-in built in the fake module) and putting the original texts back.
    /// </summary>
    public class InterviewEffectsDisplayTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        // Where the stand-in engine, its list node, the question and its answer texts sit in the fake module.
        private const uint EngineObject = 0x500000;
        private const uint Node = 0x500100;
        private const uint Question = 0x500200;
        private const uint Texts = 0x500400;

        [Theory]
        [InlineData(new[] { 0, 2, 5, -1 }, " [Fans +2, Vorstand -1]")]
        [InlineData(new[] { 1, -3, -1, -1 }, " [Öffentlichkeit -3]")]
        [InlineData(new[] { 2, -5, 3, -2 }, " [Medien -5, Reporter -2]")]
        [InlineData(new[] { 6, 1, 11, -5 }, " [Schiri, Form -1]")]
        [InlineData(new[] { 3, -2, 13, 0 }, " [Reporter -2, Abbruch]")]
        [InlineData(new[] { 14, 0, 5, 0 }, " [Abbruch, Vorstand 0]")]
        [InlineData(new[] { 10, -20, 16, -2 }, " [Moral -2,0]")]
        [InlineData(new[] { 11, -3, 12, 20000 }, " [Form -1, Strafe 20.000 DM]")]
        [InlineData(new[] { 12, 100000, 8, 24 }, " [Strafe 100.000 DM]")]
        [InlineData(new[] { 4, 3, 7, 15 }, " [keine Wirkung]")]
        [InlineData(new[] { 10, -25, 10, 0 }, " [Moral -2,5, Moral 0]")]
        public void Describe_NamesWhatTheGameApplies(int[] effects, string expected)
        {
            Assert.Equal(expected, InterviewEffectsDisplay.Describe(effects));
        }

        private static uint WriteCString(FakeModule fake, uint offset, string text)
        {
            byte[] bytes = Latin1.GetBytes(text);
            fake.WriteModuleBytes(offset, new[] { 1, bytes.Length, bytes.Length }.SelectMany(BitConverter.GetBytes).Concat(bytes).Concat(new byte[] { 0 }).ToArray());
            return fake.ModuleBase + offset + 12;
        }

        // The engine with one question whose four answers carry the given effects; returns the answer texts' pointers.
        private static uint[] PlantQuestionList(FakeModule fake, bool loaded = true)
        {
            fake.WriteModuleBytes(Settings.InterviewEngineOffset, BitConverter.GetBytes(fake.ModuleBase + EngineObject));
            fake.WriteModuleBytes(EngineObject + InterviewEffectsDisplay.EngineLoadedOffset, new byte[] { (byte)(loaded ? 1 : 0) });
            fake.WriteModuleBytes(EngineObject + InterviewEffectsDisplay.EngineQuestionsHeadOffset, BitConverter.GetBytes(fake.ModuleBase + Node));
            fake.WriteModuleBytes(Node, new[] { 0u, 0u, fake.ModuleBase + Question }.SelectMany(BitConverter.GetBytes).ToArray());

            uint[] texts = Enumerable.Range(0, 4).Select(i => WriteCString(fake, Texts + (uint)i * 0x40, $"Antwort {i + 1}")).ToArray();
            fake.WriteModuleBytes(Question + InterviewEffectsDisplay.AnswerTextsOffset, texts.SelectMany(BitConverter.GetBytes).ToArray());
            int[][] effects = { new[] { 0, -3, 5, -3 }, new[] { 10, 1, -1, -1 }, new[] { 2, 4, -1, -1 }, new[] { 13, 0, 3, -2 } };
            fake.WriteModuleBytes(Question + InterviewEffectsDisplay.AnswerEffectsOffset, effects.SelectMany(e => e).SelectMany(BitConverter.GetBytes).ToArray());
            return texts;
        }

        private static uint Slot(FakeModule fake, int answer) =>
            BitConverter.ToUInt32(fake.ReadModuleBytes(Question + InterviewEffectsDisplay.AnswerTextsOffset + (uint)answer * 4, 4), 0);

        private static string Text(FakeModule fake, uint text)
        {
            int length = BitConverter.ToInt32(fake.Memory.ReadBytesAtAddress(text - 8, 4), 0);
            return Latin1.GetString(fake.Memory.ReadBytesAtAddress(text, length + 1)).TrimEnd('\0');
        }

        [Fact]
        public void Tick_LabelsEveryAnswer_AndSwitchingOffPutsTheOriginalsBack()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint[] originals = PlantQuestionList(fake);
                using (InterviewEffectsDisplay display = new InterviewEffectsDisplay(fake.Memory))
                {
                    display.Tick();

                    Assert.Equal(
                        new[] { "Antwort 1 [Fans -3, Vorstand -3]", "Antwort 2 [Moral +0,1]", "Antwort 3 [Medien +4]", "Antwort 4 [Abbruch, Reporter -2]" },
                        Enumerable.Range(0, 4).Select(i => Text(fake, Slot(fake, i))));
                    uint labelled = Slot(fake, 0);
                    display.Tick();
                    Assert.Equal(labelled, Slot(fake, 0));

                    display.Enabled = true;
                    display.Enabled = false;
                    Assert.Equal(originals, Enumerable.Range(0, 4).Select(i => Slot(fake, i)));

                    // Back on: the strings already written are used again.
                    display.Tick();
                    Assert.Equal(labelled, Slot(fake, 0));
                }
            }
        }

        [Fact]
        public void Enabled_LabelsThroughItsTimer_AndATickQueuedAfterSwitchingOffDoesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint[] originals = PlantQuestionList(fake);
                using (InterviewEffectsDisplay display = new InterviewEffectsDisplay(fake.Memory))
                {
                    display.Enabled = true;
                    DateTime deadline = DateTime.UtcNow.AddSeconds(5);
                    while (Slot(fake, 0) == originals[0] && DateTime.UtcNow < deadline)
                        System.Threading.Thread.Sleep(20);
                    Assert.Equal("Antwort 1 [Fans -3, Vorstand -3]", Text(fake, Slot(fake, 0)));

                    display.Enabled = false;
                    typeof(InterviewEffectsDisplay).GetMethod("TimerTick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .Invoke(display, null);

                    Assert.Equal(originals, Enumerable.Range(0, 4).Select(i => Slot(fake, i)));
                }
            }
        }

        [Fact]
        public void Tick_AfterATrainerRestart_KeepsTheLabelsAndCanStillPutTheOriginalsBack()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint[] originals = PlantQuestionList(fake);
                new InterviewEffectsDisplay(fake.Memory).Tick();
                uint labelled = Slot(fake, 0);

                InterviewEffectsDisplay restarted = new InterviewEffectsDisplay(fake.Memory) { Enabled = true };
                restarted.Tick();
                Assert.Equal(labelled, Slot(fake, 0));
                Assert.Equal("Antwort 1 [Fans -3, Vorstand -3]", Text(fake, Slot(fake, 0)));

                restarted.Dispose();
                Assert.Equal(originals, Enumerable.Range(0, 4).Select(i => Slot(fake, i)));
            }
        }

        [Fact]
        public void Tick_LeavesATextTheGameChangedSinceAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                PlantQuestionList(fake);
                InterviewEffectsDisplay display = new InterviewEffectsDisplay(fake.Memory) { Enabled = true };
                display.Tick();
                uint other = WriteCString(fake, 0x500800, "Neu");
                fake.WriteModuleBytes(Question + InterviewEffectsDisplay.AnswerTextsOffset, BitConverter.GetBytes(other));

                display.Enabled = false;

                Assert.Equal(other, Slot(fake, 0));
            }
        }

        [Fact]
        public void Tick_BeforeTheListIsLoaded_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint[] originals = PlantQuestionList(fake, loaded: false);

                new InterviewEffectsDisplay(fake.Memory).Tick();

                Assert.Equal(originals, Enumerable.Range(0, 4).Select(i => Slot(fake, i)));
            }
        }

        [Fact]
        public void Tick_WithoutAnEngineOrConfirmedAddresses_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                new InterviewEffectsDisplay(fake.Memory).Tick();
                uint[] originals = PlantQuestionList(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);

                new InterviewEffectsDisplay(fake.Memory).Tick();

                Assert.Equal(originals, Enumerable.Range(0, 4).Select(i => Slot(fake, i)));
            }
        }

        [Fact]
        public void Tick_SkipsAnswersWithoutReadableText()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint[] originals = PlantQuestionList(fake);
                // Answer 2 has no text, answer 3 an implausible length.
                fake.WriteModuleBytes(Question + InterviewEffectsDisplay.AnswerTextsOffset + 4, BitConverter.GetBytes(0u));
                fake.WriteModuleBytes(Texts + 2 * 0x40 + 4, BitConverter.GetBytes(5000));

                new InterviewEffectsDisplay(fake.Memory).Tick();

                Assert.Equal(0u, Slot(fake, 1));
                Assert.Equal(originals[2], Slot(fake, 2));
                Assert.Equal("Antwort 4 [Abbruch, Reporter -2]", Text(fake, Slot(fake, 3)));
            }
        }
    }
}
