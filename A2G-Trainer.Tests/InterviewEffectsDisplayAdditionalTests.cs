using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using AnstossTrainer.Shared;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// More tests for InterviewEffectsDisplay: question lists and texts it can't read, strings that only
    /// look like its own labels, no memory for the labels, and failures while putting the originals back.
    /// </summary>
    public class InterviewEffectsDisplayAdditionalTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        private const uint EngineObject = 0x500000;
        private const uint Node = 0x500100;
        private const uint Question = 0x500200;
        private const uint Texts = 0x500400;
        // An address below the lowest one ProcessMemory reads.
        private const uint Unreadable = 0x2000;

        private const uint ProcessVmRead = 0x0010;
        private const uint ProcessQueryInformation = 0x0400;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inherit, int processId);
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        // A CString at offset, preceded by the dword before (as the display stores an original pointer there); returns the text's address.
        private static uint WriteCString(FakeModule fake, uint offset, string text, uint before = 0, int? length = null)
        {
            byte[] bytes = Latin1.GetBytes(text);
            int stored = length ?? bytes.Length;
            fake.WriteModuleBytes(offset - 4, new[] { (int)before, 1, stored, bytes.Length }.SelectMany(BitConverter.GetBytes).Concat(bytes).Concat(new byte[] { 0 }).ToArray());
            return fake.ModuleBase + offset + 12;
        }

        private static void PlantEngine(FakeModule fake, uint engine, byte loaded = 1, uint? head = null)
        {
            fake.WriteModuleBytes(Settings.InterviewEngineOffset, BitConverter.GetBytes(engine));
            if (engine == fake.ModuleBase + EngineObject)
            {
                fake.WriteModuleBytes(EngineObject + InterviewEffectsDisplay.EngineLoadedOffset, new[] { loaded });
                fake.WriteModuleBytes(EngineObject + InterviewEffectsDisplay.EngineQuestionsHeadOffset, BitConverter.GetBytes(head ?? fake.ModuleBase + Node));
            }
        }

        // One question with the given answer texts, each with effects " [Fans +1]".
        private static void PlantQuestion(FakeModule fake, params uint[] texts)
        {
            PlantEngine(fake, fake.ModuleBase + EngineObject);
            fake.WriteModuleBytes(Node, new[] { 0u, 0u, fake.ModuleBase + Question }.SelectMany(BitConverter.GetBytes).ToArray());
            fake.WriteModuleBytes(Question + InterviewEffectsDisplay.AnswerTextsOffset, texts.Concat(new uint[4 - texts.Length]).SelectMany(BitConverter.GetBytes).ToArray());
            int[] effects = Enumerable.Range(0, 4).SelectMany(_ => new[] { 0, 1, -1, -1 }).ToArray();
            fake.WriteModuleBytes(Question + InterviewEffectsDisplay.AnswerEffectsOffset, effects.SelectMany(BitConverter.GetBytes).ToArray());
        }

        private static uint Slot(FakeModule fake, int answer) =>
            BitConverter.ToUInt32(fake.ReadModuleBytes(Question + InterviewEffectsDisplay.AnswerTextsOffset + (uint)answer * 4, 4), 0);

        private static string Text(FakeModule fake, uint text)
        {
            int length = BitConverter.ToInt32(fake.Memory.ReadBytesAtAddress(text - 8, 4), 0);
            return Latin1.GetString(fake.Memory.ReadBytesAtAddress(text, length));
        }

        [Fact]
        public void Tick_WithAnUnreadableEngineSlot_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint text = WriteCString(fake, Texts, "Antwort");
                PlantQuestion(fake, text);
                fake.MakeModuleRegionUnreadable(Settings.InterviewEngineOffset, 4);

                new InterviewEffectsDisplay(fake.Memory).Tick();

                Assert.Equal(text, Slot(fake, 0));
            }
        }

        [Fact]
        public void Tick_WithAnUnreadableEngine_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint text = WriteCString(fake, Texts, "Antwort");
                PlantQuestion(fake, text);
                PlantEngine(fake, Unreadable);

                new InterviewEffectsDisplay(fake.Memory).Tick();

                Assert.Equal(text, Slot(fake, 0));
            }
        }

        [Fact]
        public void Tick_SkipsEmptyAndUnreadableNodesAndQuestions_AndLabelsTheRest()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint text = WriteCString(fake, Texts, "Antwort");
                PlantQuestion(fake, text);
                // The list: a node without a question, one with an unreadable question, the real one, then a node that can't be read.
                const uint first = 0x500040, second = 0x500060;
                PlantEngine(fake, fake.ModuleBase + EngineObject, head: fake.ModuleBase + first);
                fake.WriteModuleBytes(first, new[] { fake.ModuleBase + second, 0u, 0u }.SelectMany(BitConverter.GetBytes).ToArray());
                fake.WriteModuleBytes(second, new[] { fake.ModuleBase + Node, 0u, Unreadable }.SelectMany(BitConverter.GetBytes).ToArray());
                fake.WriteModuleBytes(Node, BitConverter.GetBytes(Unreadable));

                new InterviewEffectsDisplay(fake.Memory).Tick();

                Assert.Equal("Antwort [Fans +1]", Text(fake, Slot(fake, 0)));
            }
        }

        [Fact]
        public void Tick_LabelsTextsThatOnlyLookLikeItsOwnLabels_AndAnEmptyOriginalWithTheLabelAsItsOwn()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint plain = WriteCString(fake, Texts, "Antwort");
                uint broken = WriteCString(fake, Texts + 0x100, "kaputt", length: 5000);
                uint empty = WriteCString(fake, Texts + 0x200, "");
                uint[] texts =
                {
                    // Before it a pointer to a string with an implausible length.
                    WriteCString(fake, Texts + 0x300, "Antwort [Fans +1]", broken),
                    // Before it a pointer to a text it doesn't start with.
                    WriteCString(fake, Texts + 0x400, "Frage [Fans +1]", WriteCString(fake, Texts + 0x500, "Antwort")),
                    // Before it a pointer to a text of the right length it doesn't start with.
                    WriteCString(fake, Texts + 0x600, "Antwort [Fans +1]", WriteCString(fake, Texts + 0x700, "Antwurt")),
                    // Before it the pointer to an empty text, and the text is the label alone: one of ours.
                    WriteCString(fake, Texts + 0x800, " [Fans +1]", empty),
                };
                PlantQuestion(fake, texts);
                InterviewEffectsDisplay display = new InterviewEffectsDisplay(fake.Memory) { Enabled = true };

                display.Tick();

                Assert.Equal("Antwort [Fans +1] [Fans +1]", Text(fake, Slot(fake, 0)));
                Assert.Equal("Frage [Fans +1] [Fans +1]", Text(fake, Slot(fake, 1)));
                Assert.Equal("Antwort [Fans +1] [Fans +1]", Text(fake, Slot(fake, 2)));
                Assert.Equal(texts[3], Slot(fake, 3));

                display.Enabled = false;
                Assert.Equal(new[] { texts[0], texts[1], texts[2], empty }, Enumerable.Range(0, 4).Select(i => Slot(fake, i)));
                Assert.NotEqual(0u, plain);
            }
        }

        [Fact]
        public void Tick_WithAnOriginalPointerBeforeAnUnreadableText_SkipsIt()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint original = WriteCString(fake, Texts, "Antwort");
                // Our label's look, but its own length is negative.
                uint text = WriteCString(fake, Texts + 0x100, "Antwort [Fans +1]", original, length: -1);
                PlantQuestion(fake, text);

                new InterviewEffectsDisplay(fake.Memory).Tick();

                Assert.Equal(text, Slot(fake, 0));
            }
        }

        [Fact]
        public void Tick_WithATextRightAtTheLowestReadableAddress_SkipsIt()
        {
            using (FakeModule fake = new FakeModule())
            {
                // Neither its header nor the dword before it can be read.
                PlantQuestion(fake, 0x10008);

                new InterviewEffectsDisplay(fake.Memory).Tick();

                Assert.Equal(0x10008u, Slot(fake, 0));
            }
        }

        [Fact]
        public void Tick_WithoutMemoryForTheLabels_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint[] texts = { WriteCString(fake, Texts, "Eins"), WriteCString(fake, Texts + 0x100, "Zwei") };
                PlantQuestion(fake, texts);
                IntPtr full = fake.Memory.mProc.Handle;
                // A handle that may read the game but not allocate in it.
                IntPtr readOnly = OpenProcess(ProcessVmRead | ProcessQueryInformation, false, Process.GetCurrentProcess().Id);
                Assert.NotEqual(IntPtr.Zero, readOnly);
                try
                {
                    fake.Memory.mProc.Handle = readOnly;
                    InterviewEffectsDisplay display = new InterviewEffectsDisplay(fake.Memory);

                    display.Tick();

                    Assert.Equal(texts, new[] { Slot(fake, 0), Slot(fake, 1) });
                }
                finally
                {
                    fake.Memory.mProc.Handle = full;
                    CloseHandle(readOnly);
                }
            }
        }

        // The memory the display works on, swapped out to make every access fail.
        private static readonly FieldInfo MemoryField = typeof(InterviewEffectsDisplay).GetField("memory", BindingFlags.Instance | BindingFlags.NonPublic);

        [Fact]
        public void TickAndSwitchingOff_SurviveAFailingMemoryAccess_AndForgetTheLabels()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint text = WriteCString(fake, Texts, "Antwort");
                PlantQuestion(fake, text);
                InterviewEffectsDisplay display = new InterviewEffectsDisplay(fake.Memory) { Enabled = true };
                display.Tick();
                uint labelled = Slot(fake, 0);
                Assert.NotEqual(text, labelled);

                MemoryField.SetValue(display, null);
                display.Tick();
                display.Enabled = false;

                Assert.False(display.Enabled);
                Assert.Equal(labelled, Slot(fake, 0));
                // The labels are forgotten all the same.
                System.Collections.ICollection known = (System.Collections.ICollection)typeof(InterviewEffectsDisplay)
                    .GetField("labelled", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(display);
                Assert.Empty(known);
            }
        }
    }
}
