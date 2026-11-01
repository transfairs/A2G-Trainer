using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    /// <summary>
    /// The two jersey sets like the data editor's "Trikotsätze" tab: a player in each set, the
    /// first on the left. Clicking the shirt, the shorts or the socks switches to the next colour
    /// (right click: the previous one), in the editor's order. The "Streifenmuster" box in the
    /// middle has one column of patterns per set. The pattern is drawn in the shorts' colour,
    /// as in the editor ("Ärmel w. H." = sleeves like the shorts).
    /// </summary>
    public class JerseyView : Control
    {
        /// <summary>The parts of a jersey set.</summary>
        internal enum Garment { Shirt, Shorts, Socks }

        /// <summary>Colours of <see cref="ClubEnums.JerseyColor"/> as the editor shows them.</summary>
        internal static readonly Color[] Colors =
        {
            Color.FromArgb(240, 242, 244),  // weiß
            Color.FromArgb(185, 12, 24),    // rot
            Color.FromArgb(244, 230, 20),   // gelb
            Color.FromArgb(10, 20, 160),    // blau
            Color.FromArgb(90, 206, 30),    // hellgrün
            Color.FromArgb(0, 120, 20),     // grün
            Color.FromArgb(0, 158, 142),    // türkis
            Color.FromArgb(150, 220, 240),  // hellblau
            Color.FromArgb(140, 66, 24),    // braun
            Color.FromArgb(160, 30, 170),   // lila
            Color.FromArgb(255, 133, 16),   // orange
            Color.FromArgb(40, 40, 40),     // schwarz
            Color.FromArgb(140, 25, 75),    // weinrot
        };

        // The editor's short names of the patterns, in ClubEnums.ShirtPattern order.
        private static readonly string[] PatternLabels = { "neutral", "längs", "quer", "Halb/Halb", "Schulter", "Brustring", "Ärmel w. H." };

        private static readonly Color Skin = Color.FromArgb(232, 184, 150);
        private static readonly Color Hair = Color.FromArgb(70, 45, 25);
        private static readonly Color Shoe = Color.FromArgb(25, 25, 25);
        private const float FigureWidth = 140, FigureHeight = 300;

        private readonly GroupBox patternBox;
        private readonly RadioButton[][] patternButtons = new RadioButton[2][];
        private Club club;
        private bool showing;

        /// <summary>Raised when a click or a pattern changes the club's jerseys.</summary>
        public event EventHandler JerseyChanged;

        /// <summary>Creates the view with the pattern box.</summary>
        public JerseyView()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);

            this.patternBox = new GroupBox { Text = "Streifenmuster", BackColor = Color.Transparent, Size = new Size(110, 150), Name = "PatternBox" };
            ClubEnums.ShirtPattern[] patterns = Enum.GetValues(typeof(ClubEnums.ShirtPattern)).Cast<ClubEnums.ShirtPattern>().ToArray();
            for (int set = 0; set < 2; set++)
            {
                Panel column = new Panel { BackColor = Color.Transparent, Location = new Point(set == 0 ? 6 : 88, 16), Size = new Size(16, patterns.Length * 18), Name = $"PatternColumn{set + 1}" };
                this.patternButtons[set] = new RadioButton[patterns.Length];
                for (int i = 0; i < patterns.Length; i++)
                {
                    int s = set;
                    ClubEnums.ShirtPattern pattern = patterns[i];
                    RadioButton button = new RadioButton { Location = new Point(0, i * 18), Size = new Size(16, 16), BackColor = Color.Transparent, Tag = pattern, Name = $"Pattern{set + 1}{pattern}" };
                    button.CheckedChanged += (o, e) => { if (button.Checked) this.SetPattern(s, pattern); };
                    this.patternButtons[set][i] = button;
                    column.Controls.Add(button);
                }
                this.patternBox.Controls.Add(column);
            }
            for (int i = 0; i < patterns.Length; i++)
                this.patternBox.Controls.Add(new Label { Text = PatternLabels[i], Location = new Point(22, 17 + i * 18), Size = new Size(66, 16), TextAlign = ContentAlignment.MiddleCenter, BackColor = Color.Transparent });
            this.Controls.Add(this.patternBox);
            this.BackColor = Color.FromArgb(222, 178, 70);
        }

        /// <summary>The club whose jerseys are shown and edited; null shows white jerseys.</summary>
        public Club Club
        {
            get => this.club;
            set
            {
                this.club = value;
                this.showing = true;
                try
                {
                    for (int set = 0; set < 2; set++)
                        foreach (RadioButton button in this.patternButtons[set])
                            button.Checked = value != null && (ClubEnums.ShirtPattern)button.Tag == value.GetShirtPattern(set);
                }
                finally
                {
                    this.showing = false;
                }
                this.Invalidate();
            }
        }

        /// <summary>The pattern radio button of a set (0 or 1).</summary>
        internal RadioButton PatternButton(int set, ClubEnums.ShirtPattern pattern) => this.patternButtons[set].First(b => (ClubEnums.ShirtPattern)b.Tag == pattern);

        private void SetPattern(int set, ClubEnums.ShirtPattern pattern)
        {
            if (this.showing || this.club == null || this.club.GetShirtPattern(set) == pattern)
                return;
            this.club.SetShirtPattern(set, pattern);
            this.Invalidate();
            this.JerseyChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <inheritdoc/>
        protected override void OnLayout(LayoutEventArgs levent)
        {
            base.OnLayout(levent);
            this.patternBox.Location = new Point((this.Width - this.patternBox.Width) / 2, this.Height - this.patternBox.Height - 8);
        }

        #region Figure
        // Scale and top-left corner of a set's figure for the current size.
        private float FigureScale => Math.Max(0.1f, Math.Min((this.Height - 16) / FigureHeight, this.Width / 3f / FigureWidth));

        private PointF FigureOrigin(int set)
        {
            float scale = this.FigureScale;
            float centre = this.Width * (set == 0 ? 0.22f : 0.78f);
            return new PointF(centre - FigureWidth * scale / 2, (this.Height - FigureHeight * scale) / 2);
        }

        // The figure's parts in figure units (140 x 300).
        private static GraphicsPath Torso() => Polygon(38, 52, 102, 52, 106, 138, 34, 138);
        private static GraphicsPath LeftSleeve() => Polygon(38, 52, 22, 62, 10, 100, 28, 106, 36, 80);
        private static GraphicsPath RightSleeve() => Polygon(102, 52, 118, 62, 130, 100, 112, 106, 104, 80);

        private static GraphicsPath Shirt()
        {
            GraphicsPath path = new GraphicsPath(FillMode.Winding);
            path.AddPath(Torso(), false);
            path.AddPath(LeftSleeve(), false);
            path.AddPath(RightSleeve(), false);
            return path;
        }

        private static GraphicsPath Shorts() => Polygon(34, 136, 106, 136, 112, 190, 74, 194, 70, 168, 66, 194, 28, 190);

        private static GraphicsPath Socks()
        {
            GraphicsPath path = new GraphicsPath(FillMode.Winding);
            path.AddPath(Polygon(38, 238, 60, 238, 59, 280, 39, 280), false);
            path.AddPath(Polygon(80, 238, 102, 238, 101, 280, 81, 280), false);
            return path;
        }

        private static GraphicsPath Polygon(params float[] xy)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddPolygon(Enumerable.Range(0, xy.Length / 2).Select(i => new PointF(xy[2 * i], xy[2 * i + 1])).ToArray());
            return path;
        }

        private Matrix FigureTransform(int set)
        {
            PointF origin = this.FigureOrigin(set);
            float scale = this.FigureScale;
            Matrix matrix = new Matrix();
            matrix.Translate(origin.X, origin.Y);
            matrix.Scale(scale, scale);
            return matrix;
        }

        /// <summary>The set (0 or 1) and garment under a point, or null.</summary>
        internal Tuple<int, Garment> GarmentAt(Point point)
        {
            for (int set = 0; set < 2; set++)
                using (Matrix matrix = this.FigureTransform(set))
                {
                    matrix.Invert();
                    PointF[] p = { point };
                    matrix.TransformPoints(p);
                    foreach (Garment garment in new[] { Garment.Shirt, Garment.Shorts, Garment.Socks })
                        using (GraphicsPath path = garment == Garment.Shirt ? Shirt() : garment == Garment.Shorts ? Shorts() : Socks())
                            if (path.IsVisible(p[0]))
                                return Tuple.Create(set, garment);
                }
            return null;
        }

        /// <summary>The point in the middle of a garment of a set, for the current size.</summary>
        internal Point GarmentCentre(int set, Garment garment)
        {
            PointF[] p = { garment == Garment.Shirt ? new PointF(70, 95) : garment == Garment.Shorts ? new PointF(50, 160) : new PointF(49, 258) };
            using (Matrix matrix = this.FigureTransform(set))
                matrix.TransformPoints(p);
            return Point.Round(p[0]);
        }

        /// <summary>Switches a garment's colour by <paramref name="step"/> places in the editor's order.</summary>
        internal void Cycle(int set, Garment garment, int step)
        {
            if (this.club == null)
                return;
            int count = Colors.Length;
            switch (garment)
            {
                case Garment.Shirt:
                    this.club.SetShirtColor(set, (ClubEnums.JerseyColor)((((int)this.club.GetShirtColor(set) + step) % count + count) % count));
                    break;
                case Garment.Shorts:
                    this.club.SetShortsColor(set, (ClubEnums.JerseyColor)((((int)this.club.GetShortsColor(set) + step) % count + count) % count));
                    break;
                default:
                    this.club.SetSocksColor(set, (ClubEnums.JerseyColor)((((int)this.club.GetSocksColor(set) + step) % count + count) % count));
                    break;
            }
            this.Invalidate();
            this.JerseyChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Screen colour of a stored colour; unknown values show grey.</summary>
        internal static Color ToColor(ClubEnums.JerseyColor color) => (int)color < Colors.Length ? Colors[(int)color] : Color.Gray;

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Tuple<int, Garment> hit = this.GarmentAt(e.Location);
            if (hit != null && (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right))
                this.Cycle(hit.Item1, hit.Item2, e.Button == MouseButtons.Left ? 1 : -1);
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            this.Cursor = this.GarmentAt(e.Location) != null ? Cursors.Hand : Cursors.Default;
        }
        #endregion

        #region Painting
        /// <inheritdoc/>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using (LinearGradientBrush brush = new LinearGradientBrush(this.ClientRectangle, Color.FromArgb(246, 214, 110), Color.FromArgb(196, 140, 40), LinearGradientMode.ForwardDiagonal))
                e.Graphics.FillRectangle(brush, this.ClientRectangle);
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            for (int set = 0; set < 2; set++)
            {
                GraphicsState state = e.Graphics.Save();
                using (Matrix matrix = this.FigureTransform(set))
                    e.Graphics.MultiplyTransform(matrix);
                this.PaintFigure(e.Graphics, set);
                e.Graphics.Restore(state);
            }
        }

        private void PaintFigure(Graphics g, int set)
        {
            Color shirt = ToColor(this.club?.GetShirtColor(set) ?? ClubEnums.JerseyColor.Weiss);
            Color shorts = ToColor(this.club?.GetShortsColor(set) ?? ClubEnums.JerseyColor.Weiss);
            Color socks = ToColor(this.club?.GetSocksColor(set) ?? ClubEnums.JerseyColor.Weiss);
            ClubEnums.ShirtPattern pattern = this.club?.GetShirtPattern(set) ?? ClubEnums.ShirtPattern.Neutral;

            using (Pen outline = new Pen(Color.FromArgb(120, 0, 0, 0), 1.2f) { LineJoin = LineJoin.Round })
            using (Brush skin = new SolidBrush(Skin))
            {
                // Arms, legs, head.
                g.FillPolygon(skin, new[] { new PointF(10, 100), new PointF(28, 106), new PointF(24, 150), new PointF(14, 150) });
                g.FillPolygon(skin, new[] { new PointF(130, 100), new PointF(112, 106), new PointF(116, 150), new PointF(126, 150) });
                g.FillEllipse(skin, 11, 146, 16, 16);
                g.FillEllipse(skin, 113, 146, 16, 16);
                g.FillPolygon(skin, new[] { new PointF(36, 188), new PointF(64, 190), new PointF(60, 240), new PointF(38, 240) });
                g.FillPolygon(skin, new[] { new PointF(76, 190), new PointF(104, 188), new PointF(102, 240), new PointF(80, 240) });
                g.FillRectangle(skin, 62, 40, 16, 16);
                g.FillEllipse(skin, 52, 6, 36, 42);
                using (Brush hair = new SolidBrush(Hair))
                    g.FillPie(hair, 50, 3, 40, 30, 180, 180);

                // Shirt with its pattern in the shorts' colour.
                using (GraphicsPath shirtPath = Shirt())
                {
                    using (Brush brush = new SolidBrush(shirt))
                        g.FillPath(brush, shirtPath);
                    if (pattern != ClubEnums.ShirtPattern.Neutral)
                    {
                        Region clip = g.Clip;
                        g.SetClip(shirtPath, CombineMode.Intersect);
                        using (Brush brush = new SolidBrush(shorts))
                            foreach (RectangleF band in PatternBands(pattern))
                                g.FillRectangle(brush, band);
                        if (pattern == ClubEnums.ShirtPattern.AermelWieHose)
                            using (Brush brush = new SolidBrush(shorts))
                            using (GraphicsPath left = LeftSleeve())
                            using (GraphicsPath right = RightSleeve())
                            {
                                g.FillPath(brush, left);
                                g.FillPath(brush, right);
                            }
                        g.Clip = clip;
                    }
                    g.DrawPath(outline, shirtPath);
                }
                using (GraphicsPath path = Shorts())
                using (Brush brush = new SolidBrush(shorts))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(outline, path);
                }
                using (GraphicsPath path = Socks())
                using (Brush brush = new SolidBrush(socks))
                {
                    g.FillPath(brush, path);
                    g.DrawPath(outline, path);
                }
                using (Brush shoe = new SolidBrush(Shoe))
                {
                    g.FillEllipse(shoe, 30, 276, 32, 14);
                    g.FillEllipse(shoe, 78, 276, 32, 14);
                }
            }
        }

        // The areas of the shirt a pattern paints in the second colour (clipped to the shirt).
        internal static IEnumerable<RectangleF> PatternBands(ClubEnums.ShirtPattern pattern)
        {
            switch (pattern)
            {
                case ClubEnums.ShirtPattern.Laengs:
                    for (float x = 42; x < 106; x += 16)
                        yield return new RectangleF(x, 40, 8, 110);
                    break;
                case ClubEnums.ShirtPattern.Quer:
                    for (float y = 60; y < 140; y += 18)
                        yield return new RectangleF(0, y, 140, 9);
                    break;
                case ClubEnums.ShirtPattern.HalbHalb:
                    yield return new RectangleF(0, 40, 70, 110);
                    break;
                case ClubEnums.ShirtPattern.Schulter:
                    yield return new RectangleF(0, 40, 140, 30);
                    break;
                case ClubEnums.ShirtPattern.Brustring:
                    yield return new RectangleF(0, 84, 140, 16);
                    break;
            }
        }
        #endregion
    }
}
