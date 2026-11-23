using A2G_Trainer.View;
using AnstossTrainer.Shared.Tests;
using System.Drawing;
using System.Windows.Forms;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>The buttons with a drawn arrow beside their text (national squad: add, remove).</summary>
    public class ArrowButtonTests
    {
        // The leftmost and rightmost column holding the given colour, or -1 if there is none.
        private static (int First, int Last) Columns(Bitmap image, Color colour)
        {
            int first = -1, last = -1;
            for (int x = 0; x < image.Width; x++)
                for (int y = 0; y < image.Height; y++)
                    if (image.GetPixel(x, y).ToArgb() == colour.ToArgb())
                    {
                        if (first < 0)
                            first = x;
                        last = x;
                        break;
                    }
            return (first, last);
        }

        private static Bitmap Render(ArrowButton button)
        {
            Bitmap image = new Bitmap(button.Width, button.Height);
            button.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
            return image;
        }

        [Fact]
        public void Arrow_StandsBeforeOrAfterTheText_InTheTextColourOrGreyWhenDisabled() => StaThread.Run(() =>
        {
            Color ink = Color.FromArgb(0x12, 0x34, 0x56);
            using (ArrowButton add = new ArrowButton { ArrowLeft = true, Text = "Aufnehmen", Size = new Size(82, 23), ForeColor = ink })
            using (ArrowButton remove = new ArrowButton { Text = "Streichen", Size = new Size(82, 23), Enabled = false })
            {
                Assert.True(add.ArrowLeft);
                Assert.False(remove.ArrowLeft);
                Assert.True(add.Padding.Left > 0 && add.Padding.Right == 0);
                Assert.True(remove.Padding.Right > 0 && remove.Padding.Left == 0);

                using (Bitmap image = Render(add))
                {
                    // Arrow and text are both in the ink colour; the arrow is the leftmost ink, left of the middle.
                    var ink1 = Columns(image, ink);
                    Assert.True(ink1.First >= 0 && ink1.First < add.Width / 4);
                }
                using (Bitmap image = Render(remove))
                {
                    var grey = Columns(image, SystemColors.GrayText);
                    Assert.True(grey.Last > remove.Width * 3 / 4);
                }
            }
        });

        [Fact]
        public void RoomForTheArrow_GrowsWithTheFont() => StaThread.Run(() =>
        {
            using (ArrowButton button = new ArrowButton { ArrowLeft = true })
            using (Font big = new Font(button.Font.FontFamily, button.Font.Size * 2))
            {
                int before = button.Padding.Left;
                button.Font = big;
                Assert.True(button.Padding.Left > before);
            }
        });
    }
}
