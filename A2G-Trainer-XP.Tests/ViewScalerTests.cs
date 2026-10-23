using System.Drawing;
using System.Windows.Forms;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for ViewScaler's zooming of an absolutely laid-out view.</summary>
    public class ViewScalerTests
    {
        private static UserControl NewRoot() => new UserControl
        {
            Size = new Size(200, 100),
            Font = new Font("Microsoft Sans Serif", 8f),
            AutoScaleMode = AutoScaleMode.Font
        };

        [Fact]
        public void ScaleTo_StretchesBoundsPerAxisAndResizesTheRoot() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                GroupBox box = new GroupBox { Bounds = new Rectangle(10, 10, 100, 50) };
                Button button = new Button { Bounds = new Rectangle(5, 20, 40, 20) };
                box.Controls.Add(button);
                root.Controls.Add(box);

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(400, 300));

                    Assert.Equal(new Size(400, 300), root.Size);
                    Assert.Equal(new Rectangle(20, 30, 200, 150), box.Bounds);
                    Assert.Equal(new Rectangle(10, 60, 80, 60), button.Bounds);
                    Assert.Equal(2f, scaler.ScaleX);
                    Assert.Equal(3f, scaler.ScaleY);
                    Assert.Equal(2f, scaler.FontScale);
                }
            }
        });

        [Fact]
        public void ScaleTo_SwitchesOffTheRootsOwnFontAutoScaling() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            using (new ViewScaler(root))
            {
                Assert.Equal(AutoScaleMode.None, root.AutoScaleMode);
            }
        });

        [Fact]
        public void ScaleTo_ScalesInheritedAndExplicitFontsByTheSmallerFactor() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                Label inherited = new Label();
                Label bold = new Label { Font = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold) };
                root.Controls.Add(inherited);
                root.Controls.Add(bold);

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(300, 400));

                    Assert.Equal(12f, root.Font.Size);
                    Assert.Equal(12f, inherited.Font.Size);
                    Assert.Equal(15f, bold.Font.Size);
                    Assert.Equal(FontStyle.Bold, bold.Font.Style);
                }
            }
        });

        [Fact]
        public void ScaleTo_BackToTheDesignSize_RestoresTheOriginalBoundsAndFonts() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                Font designFont = root.Font;
                TextBox input = new TextBox { Multiline = true, Bounds = new Rectangle(7, 9, 33, 21) };
                root.Controls.Add(input);

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(333, 177));
                    scaler.ScaleTo(new Size(200, 100));

                    Assert.Equal(new Rectangle(7, 9, 33, 21), input.Bounds);
                    Assert.Same(designFont, root.Font);
                }
            }
        });

        [Fact]
        public void ScaleTo_ReusesTheScaledFontForAnUnchangedFontScale() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            using (ViewScaler scaler = new ViewScaler(root))
            {
                scaler.ScaleTo(new Size(400, 200));
                Font first = root.Font;
                scaler.ScaleTo(new Size(400, 250));

                Assert.Same(first, root.Font);
            }
        });

        [Fact]
        public void ScaleTo_WithAnEmptyTargetOrDesignSize_DoesNothing() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            using (UserControl empty = new UserControl { Size = Size.Empty })
            using (ViewScaler scaler = new ViewScaler(root))
            using (ViewScaler emptyScaler = new ViewScaler(empty))
            {
                scaler.ScaleTo(new Size(0, 50));
                emptyScaler.ScaleTo(new Size(100, 50));

                Assert.Equal(new Size(200, 100), root.Size);
                Assert.Equal(1f, scaler.ScaleX);
                Assert.Equal(1f, emptyScaler.ScaleX);
            }
        });

        [Fact]
        public void ScaleTo_LeavesDockedControlsAndTabPagesToTheirParentsLayout() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                TabControl tabs = new TabControl { Bounds = new Rectangle(0, 0, 100, 80) };
                TabPage page = new TabPage();
                tabs.TabPages.Add(page);
                Panel docked = new Panel { Dock = DockStyle.Fill };
                Label inner = new Label { Bounds = new Rectangle(4, 4, 20, 10) };
                docked.Controls.Add(inner);
                page.Controls.Add(docked);
                root.Controls.Add(tabs);

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(400, 200));

                    Assert.Equal(new Rectangle(0, 0, 200, 160), tabs.Bounds);
                    Assert.Equal(page.ClientRectangle, docked.Bounds);
                    Assert.Equal(new Point(8, 8), inner.Location);
                }
            }
        });

        [Fact]
        public void ScaleTo_StretchesListViewColumnsHorizontally() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                ListView list = new ListView { View = System.Windows.Forms.View.Details, Bounds = new Rectangle(0, 0, 100, 100) };
                list.Columns.Add("Name", 60);
                root.Controls.Add(list);

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(300, 100));

                    Assert.Equal(90, list.Columns[0].Width);
                }
            }
        });

        [Fact]
        public void ScaleTo_ZoomsRichTextBoxesWithoutFlatteningTheirFormatting() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                RichTextBox help = new RichTextBox { Bounds = new Rectangle(0, 0, 200, 100) };
                root.Controls.Add(help);
                root.CreateControl();
                help.Rtf = @"{\rtf1\ansi{\fonttbl{\f0 Arial;}}\fs20 normal {\fs32\b gross}\par}";

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(400, 200));
                    scaler.ScaleTo(new Size(400, 250));

                    Assert.Equal(2f, help.ZoomFactor, 3);
                    help.Select(help.Text.IndexOf("gross"), 5);
                    Assert.True(help.SelectionFont.Bold);
                    Assert.Equal(16f, help.SelectionFont.Size);
                }
            }
        });

        [Fact]
        public void ScaleTo_InAScrolledDownPanel_KeepsTheContentInPlaceAndTheScrollOffsetProportional() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                Panel scrolling = new Panel { AutoScroll = true, Bounds = new Rectangle(0, 0, 100, 100) };
                Label below = new Label { Bounds = new Rectangle(0, 150, 20, 10) };
                scrolling.Controls.Add(below);
                root.Controls.Add(scrolling);
                root.CreateControl();

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scrolling.AutoScrollPosition = new Point(0, 50);
                    scaler.ScaleTo(new Size(400, 200));

                    Assert.Equal(new Point(0, -100), scrolling.AutoScrollPosition);
                    Assert.Equal(300 - 100, below.Top);

                    // A control added later is placed in design content coordinates, so it has to
                    // land where its content position currently scrolls to, not at the visible top.
                    Label late = new Label { Location = new Point(0, 10) };
                    scrolling.Controls.Add(late);

                    Assert.Equal(new Point(0, 10 * 2 - 100), late.Location);
                }
            }
        });

        [Fact]
        public void ControlAddedLater_IsScaledFromDesignCoordinatesStraightAway() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                GroupBox box = new GroupBox { Bounds = new Rectangle(0, 0, 100, 50) };
                root.Controls.Add(box);

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(400, 200));
                    Label late = new Label { Bounds = new Rectangle(10, 5, 30, 10), Font = new Font("Arial", 9f) };
                    box.Controls.Add(late);
                    RichTextBox lateText = new RichTextBox { Bounds = new Rectangle(0, 0, 10, 10) };
                    root.Controls.Add(lateText);

                    Assert.Equal(new Rectangle(20, 10, 60, 20), late.Bounds);
                    Assert.Equal(18f, late.Font.Size);
                    Assert.Equal(2f, lateText.ZoomFactor, 3);
                }
            }
        });

        [Fact]
        public void ContainerAddedLater_HasItsChildrenScaledToo() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            using (ViewScaler scaler = new ViewScaler(root))
            {
                scaler.ScaleTo(new Size(400, 200));
                GroupBox box = new GroupBox { Bounds = new Rectangle(0, 0, 100, 50) };
                Label inside = new Label { Bounds = new Rectangle(10, 5, 30, 10) };
                box.Controls.Add(inside);

                root.Controls.Add(box);

                Assert.Equal(new Rectangle(20, 10, 60, 20), inside.Bounds);
            }
        });

        // A scale whose font size rounds down to nothing keeps the design font rather than creating
        // an invalid zero-size one.
        [Fact]
        public void ScaleTo_ATinySize_KeepsTheDesignFonts() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                Font design = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold);
                Label bold = new Label { Font = design };
                root.Controls.Add(bold);

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(1, 1));

                    Assert.Same(design, bold.Font);
                }
            }
        });

        [Fact]
        public void ControlRemoved_IsNoLongerScaled() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            using (ListView list = new ListView { Bounds = new Rectangle(0, 0, 50, 50) })
            {
                list.Columns.Add("Name", 40);
                root.Controls.Add(list);

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    root.Controls.Remove(list);
                    scaler.ScaleTo(new Size(400, 200));

                    Assert.Equal(new Rectangle(0, 0, 50, 50), list.Bounds);
                    Assert.Equal(40, list.Columns[0].Width);
                }
            }
        });

        [Fact]
        public void Dispose_StopsFollowingTheControlTree() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                ViewScaler scaler = new ViewScaler(root);
                scaler.ScaleTo(new Size(400, 200));
                scaler.Dispose();

                Label late = new Label { Bounds = new Rectangle(10, 5, 30, 10) };
                root.Controls.Add(late);

                Assert.Equal(new Rectangle(10, 5, 30, 10), late.Bounds);
            }
        });

        [Fact]
        public void ScaleTo_WithACreatedHandle_StillAppliesTheLayout() => StaThread.Run(() =>
        {
            using (UserControl root = NewRoot())
            {
                Button button = new Button { Bounds = new Rectangle(10, 10, 20, 20) };
                root.Controls.Add(button);
                root.CreateControl();

                using (ViewScaler scaler = new ViewScaler(root))
                {
                    scaler.ScaleTo(new Size(400, 200));

                    Assert.True(root.IsHandleCreated);
                    Assert.Equal(new Rectangle(20, 20, 40, 40), button.Bounds);
                }
            }
        });
    }
}
