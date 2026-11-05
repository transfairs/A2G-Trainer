using System;
using System.Drawing;
using A2G_Trainer.Controller;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for the quiz help (QuizHelper): finding the dialog object and reading the right answer.</summary>
    public class QuizHelperTests
    {
        // Window handles no real window of the test process has (and no other test writes).
        private const uint Dialog = 0x7A5C3E11;
        private const uint FirstButton = 0x7A5C3E21;
        private const uint SecondButton = 0x7A5C3E31;
        private const uint ObjectOffset = 0x100000;

        private static void WriteObject(FakeModule fake, short question, short right, uint dialog = Dialog)
        {
            fake.WriteModuleBytes(ObjectOffset + 0x1C, BitConverter.GetBytes(dialog));
            fake.WriteModuleBytes(ObjectOffset + 0x41C, BitConverter.GetBytes(FirstButton));
            fake.WriteModuleBytes(ObjectOffset + 0x4E8, BitConverter.GetBytes(SecondButton));
            fake.WriteModuleBytes(ObjectOffset + QuizHelper.QuestionOffset, BitConverter.GetBytes(question));
            fake.WriteModuleBytes(ObjectOffset + QuizHelper.RightAnswerOffset, BitConverter.GetBytes(right));
        }

        [Fact]
        public void FindDialogObject_FindsTheObjectByItsWindowHandles_AndReadsTheRightAnswer()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteObject(fake, 3, 2);

                uint? found = QuizHelper.FindDialogObject(fake.Memory, Dialog, FirstButton, SecondButton, fake.ModuleBase, fake.ModuleBase + fake.Memory.ModuleSize);

                Assert.Equal(fake.ModuleBase + ObjectOffset, found);
                Assert.Equal(2, QuizHelper.RightAnswer(fake.Memory, found.Value));
                Assert.Null(QuizHelper.FindDialogObject(fake.Memory, Dialog, FirstButton, 0x7A5C3E41, fake.ModuleBase, fake.ModuleBase + fake.Memory.ModuleSize));
            }
        }

        [Theory]
        [InlineData((short)0, (short)1)]  // no question yet
        [InlineData((short)7, (short)1)]  // quiz over
        [InlineData((short)2, (short)3)]  // no such button
        public void RightAnswer_OutsideAQuestion_IsUnknown(short question, short right)
        {
            using (FakeModule fake = new FakeModule())
            {
                // Its own dialog handle: the tests run in parallel and the search looks at the whole process.
                WriteObject(fake, question, right, 0x7A5C3E51);

                Assert.Null(QuizHelper.RightAnswer(fake.Memory, fake.ModuleBase + ObjectOffset));
                Assert.Null(QuizHelper.RightAnswer(fake.Memory, 0));
            }
        }

        [Theory]
        [InlineData(30, 22)]  // three quarters of the button's height
        [InlineData(10, 14)]  // at least 14
        [InlineData(60, 28)]  // at most 28
        public void MarkBounds_IsASquareFlushWithTheQuestionBox_CentredVertically(int height, int size)
        {
            Rectangle button = new Rectangle(200, 100, 300, height);

            Rectangle mark = QuizHelper.MarkBounds(button, 218);

            Assert.Equal(new Size(size, size), mark.Size);
            Assert.Equal(218, mark.Left);
            Assert.Equal(button.Top + (height - size) / 2, mark.Top);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void QuizMark_DrawsAGreenTickOrARedCross_OnTheTransparentColour(bool right)
        {
            using (QuizMark mark = new QuizMark(!right) { Size = new Size(24, 24) })
            using (Bitmap image = new Bitmap(24, 24))
            {
                mark.IsRight = right;
                mark.IsRight = right;
                mark.DrawToBitmap(image, new Rectangle(0, 0, 24, 24));

                Assert.Equal(right, mark.IsRight);
                Assert.Equal(mark.TransparencyKey.ToArgb(), image.GetPixel(12, 1).ToArgb());
                Color stroke = right ? Color.FromArgb(0, 170, 0) : Color.FromArgb(220, 0, 0);
                bool drawn = false;
                for (int x = 0; x < 24 && !drawn; x++)
                    for (int y = 0; y < 24 && !drawn; y++)
                        drawn = image.GetPixel(x, y).ToArgb() == stroke.ToArgb();
                Assert.True(drawn);
            }
        }

        [Fact]
        public void Enabled_StartsAndStopsTheTimer_AndATickWithoutAGameDoesNothing()
        {
            using (FakeModule fake = new FakeModule())
            using (QuizHelper helper = new QuizHelper(fake.Memory))
            {
                Assert.False(helper.Enabled);
                helper.Tick();
                helper.Enabled = true;
                helper.Enabled = true;
                Assert.True(helper.Enabled);
                helper.Enabled = false;
                Assert.False(helper.Enabled);
            }
        }
    }
}
