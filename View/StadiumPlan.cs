using A2G_Trainer.Model;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// Top view of the stadium like the game's "Stadionausbau" screen: the pitch with the twelve
    /// blocks around it (F E D above, B to the right, J K L below, H to the left, the corners G, C, I
    /// and A). Each block fills from the pitch outwards by its share of <see cref="FullBlock"/>:
    /// standing places (block K: boxes) first, the seats behind them, in the game's legend colours,
    /// darker with a roof. Negative places fill in from the outer edge in <see cref="Negative"/>.
    /// Blocks under construction are hatched. Clicking a block selects it.
    /// </summary>
    public class StadiumPlan : Control
    {
        /// <summary>Legend colours of the game's stadium screen.</summary>
        public static readonly Color StandingOpen = Color.FromArgb(240, 140, 140);
        /// <summary>Standing places with a roof.</summary>
        public static readonly Color StandingRoofed = Color.FromArgb(225, 30, 30);
        /// <summary>Seats without a roof.</summary>
        public static readonly Color SeatsOpen = Color.FromArgb(140, 140, 255);
        /// <summary>Seats with a roof.</summary>
        public static readonly Color SeatsRoofed = Color.FromArgb(30, 30, 220);
        /// <summary>Boxes (block K's standing places).</summary>
        public static readonly Color Boxes = Color.FromArgb(150, 110, 135);
        /// <summary>Negative places (the "negative places" trick), filled in from the outer edge.</summary>
        public static readonly Color Negative = Color.FromArgb(240, 200, 0);
        /// <summary>Places that fill a block completely (the Int16 limit of a block's fields).</summary>
        public const int FullBlock = short.MaxValue;
        private static readonly Color EmptyBlock = Color.FromArgb(205, 205, 205);
        private static readonly Color Pitch = Color.FromArgb(40, 150, 40);

        private Club club;
        private int selectedBlock;

        /// <summary>Raised when a click selects another block.</summary>
        public event EventHandler SelectedBlockChanged;

        /// <summary>Creates an empty plan.</summary>
        public StadiumPlan()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            this.BackColor = Color.FromArgb(190, 190, 190);
        }

        /// <summary>The club whose stadium is shown; null shows an empty plan.</summary>
        public Club Club
        {
            get => this.club;
            set { this.club = value; this.Invalidate(); }
        }

        /// <summary>Index of the selected block (0 = A ... 11 = L).</summary>
        public int SelectedBlock
        {
            get => this.selectedBlock;
            set
            {
                int block = Math.Max(0, Math.Min(Club.BlockCount - 1, value));
                if (block == this.selectedBlock)
                    return;
                this.selectedBlock = block;
                this.Invalidate();
                this.SelectedBlockChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        // The plan's geometry for the current size: outer edge, inner edge of the stands, pitch.
        private RectangleF Outer => RectangleF.Inflate(this.ClientRectangle, -4, -4);

        private RectangleF Inner
        {
            get
            {
                RectangleF o = this.Outer;
                float depthX = o.Width * 0.2f, depthY = o.Height * 0.2f;
                return new RectangleF(o.Left + depthX, o.Top + depthY, o.Width - 2 * depthX, o.Height - 2 * depthY);
            }
        }

        /// <summary>Outline of a block (0 = A ... 11 = L) for the current size.</summary>
        internal PointF[] BlockOutline(int block)
        {
            RectangleF o = this.Outer, i = this.Inner;
            float third = i.Width / 3;
            float c = Math.Min(i.Left - o.Left, i.Top - o.Top) * 0.8f;   // chamfer of the outer corners
            switch (block)
            {
                case 0: return new[] { new PointF(i.Right, i.Bottom), new PointF(o.Right, i.Bottom), new PointF(o.Right, o.Bottom - c), new PointF(o.Right - c, o.Bottom), new PointF(i.Right, o.Bottom) };
                case 1: return Box(i.Right, i.Top, o.Right, i.Bottom);
                case 2: return new[] { new PointF(i.Right, i.Top), new PointF(i.Right, o.Top), new PointF(o.Right - c, o.Top), new PointF(o.Right, o.Top + c), new PointF(o.Right, i.Top) };
                case 3: return Box(i.Left + 2 * third, o.Top, i.Right, i.Top);
                case 4: return Box(i.Left + third, o.Top, i.Left + 2 * third, i.Top);
                case 5: return Box(i.Left, o.Top, i.Left + third, i.Top);
                case 6: return new[] { new PointF(i.Left, i.Top), new PointF(o.Left, i.Top), new PointF(o.Left, o.Top + c), new PointF(o.Left + c, o.Top), new PointF(i.Left, o.Top) };
                case 7: return Box(o.Left, i.Top, i.Left, i.Bottom);
                case 8: return new[] { new PointF(i.Left, i.Bottom), new PointF(i.Left, o.Bottom), new PointF(o.Left + c, o.Bottom), new PointF(o.Left, o.Bottom - c), new PointF(o.Left, i.Bottom) };
                case 9: return Box(i.Left, i.Bottom, i.Left + third, o.Bottom);
                case 10: return Box(i.Left + third, i.Bottom, i.Left + 2 * third, o.Bottom);
                default: return Box(i.Left + 2 * third, i.Bottom, i.Right, o.Bottom);
            }
        }

        private static PointF[] Box(float left, float top, float right, float bottom) =>
            new[] { new PointF(left, top), new PointF(right, top), new PointF(right, bottom), new PointF(left, bottom) };

        // The part of a block next to the pitch that holds a share of its depth: standing places
        // (block K: boxes) sit there, the seats behind them.
        private RectangleF InnerBand(int block, float share)
        {
            RectangleF o = this.Outer, i = this.Inner;
            float dx = (i.Left - o.Left) * share, dy = (i.Top - o.Top) * share;
            float big = Math.Max(o.Width, o.Height);
            switch (block)
            {
                case 0: return new RectangleF(i.Right, i.Bottom, dx, dy);
                case 1: return new RectangleF(i.Right, o.Top, dx, big);
                case 2: return new RectangleF(i.Right, i.Top - dy, dx, dy);
                case 3: case 4: case 5: return new RectangleF(o.Left, i.Top - dy, big, dy);
                case 6: return new RectangleF(i.Left - dx, i.Top - dy, dx, dy);
                case 7: return new RectangleF(i.Left - dx, o.Top, dx, big);
                case 8: return new RectangleF(i.Left - dx, i.Bottom, dx, dy);
                default: return new RectangleF(o.Left, i.Bottom, big, dy);
            }
        }

        /// <summary>The block at a point of the control, or -1.</summary>
        public int BlockAt(Point point)
        {
            for (int block = 0; block < Club.BlockCount; block++)
            {
                using (GraphicsPath path = new GraphicsPath())
                {
                    path.AddPolygon(this.BlockOutline(block));
                    if (path.IsVisible(point))
                        return block;
                }
            }
            return -1;
        }

        /// <inheritdoc/>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            int block = this.BlockAt(e.Location);
            if (block >= 0)
                this.SelectedBlock = block;
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            this.PaintPitch(g);
            for (int block = 0; block < Club.BlockCount; block++)
                this.PaintBlock(g, block);
            // The selection on top, so its frame isn't covered by a neighbour.
            using (Pen pen = new Pen(Color.Orange, 3))
                g.DrawPolygon(pen, this.BlockOutline(this.selectedBlock));
        }

        private void PaintPitch(Graphics g)
        {
            RectangleF i = this.Inner;
            RectangleF pitch = RectangleF.Inflate(i, -i.Width * 0.04f, -i.Height * 0.06f);
            using (Brush brush = new SolidBrush(Pitch))
                g.FillRectangle(brush, i);
            using (Pen line = new Pen(Color.White, 1))
            {
                g.DrawRectangle(line, pitch.X, pitch.Y, pitch.Width, pitch.Height);
                float midX = pitch.Left + pitch.Width / 2, midY = pitch.Top + pitch.Height / 2;
                g.DrawLine(line, midX, pitch.Top, midX, pitch.Bottom);
                float r = pitch.Height * 0.15f;
                g.DrawEllipse(line, midX - r, midY - r, 2 * r, 2 * r);
                float boxW = pitch.Width * 0.14f, boxH = pitch.Height * 0.5f;
                g.DrawRectangle(line, pitch.Left, midY - boxH / 2, boxW, boxH);
                g.DrawRectangle(line, pitch.Right - boxW, midY - boxH / 2, boxW, boxH);
            }
        }

        private void PaintBlock(Graphics g, int block)
        {
            PointF[] outline = this.BlockOutline(block);
            int standing = 0, seats = 0, negative = 0;
            bool roofed = false;
            byte weeks = 0;
            if (this.club != null)
            {
                short s = this.club.GetStandings(block), t = this.club.GetSeats(block);
                standing = Math.Max((short)0, s);
                seats = Math.Max((short)0, t);
                negative = -Math.Min(0, (int)s) - Math.Min(0, (int)t);
                roofed = this.club.HasRoof(block);
                weeks = this.club.GetWeeks(block);
            }

            Color standingColor = block == Club.BoxBlock ? Boxes : roofed ? StandingRoofed : StandingOpen;
            Color seatsColor = roofed ? SeatsRoofed : SeatsOpen;

            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddPolygon(outline);
                Region clip = g.Clip;
                g.SetClip(path, CombineMode.Intersect);
                // Painted from the outside in, each band covering the one before: negative places at
                // the outer edge, the empty rest, then the seats and the standing places at the pitch.
                using (Brush brush = new SolidBrush(negative > 0 ? Negative : EmptyBlock))
                    g.FillPath(brush, path);
                if (negative > 0)
                    this.FillBand(g, block, 1 - Share(negative), EmptyBlock);
                this.FillBand(g, block, Share(standing + seats), seatsColor);
                this.FillBand(g, block, Share(standing), standingColor);
                g.Clip = clip;
                if (weeks > 0)
                {
                    using (Brush hatch = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(160, Color.Black), Color.Transparent))
                        g.FillPath(hatch, path);
                }
                using (Pen pen = new Pen(Color.Black, 1))
                    g.DrawPath(pen, path);
            }

            RectangleF bounds = OutlineBounds(outline);
            bool hasPlaces = this.club != null && this.club.GetStandings(block) + this.club.GetSeats(block) != 0;
            string text = Club.BlockLetters[block] + (hasPlaces ? "\n" + this.PlacesText(block) : "") + (weeks > 0 ? $"\n{weeks} Wo." : "");
            using (Font font = new Font(this.Font, FontStyle.Bold))
            using (StringFormat format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (Brush shadow = new SolidBrush(Color.FromArgb(200, Color.White)))
            {
                RectangleF shifted = bounds;
                shifted.Offset(1, 1);
                g.DrawString(text, font, shadow, shifted, format);
                g.DrawString(text, font, Brushes.Black, bounds, format);
            }
        }

        private void FillBand(Graphics g, int block, float share, Color color)
        {
            if (share <= 0)
                return;
            using (Brush brush = new SolidBrush(color))
                g.FillRectangle(brush, this.InnerBand(block, share));
        }

        // A number of places as a share of a full block's depth.
        private static float Share(int places) => Math.Min(1f, (float)places / FullBlock);

        // Signed, so the "negative places" trick shows as such.
        private string PlacesText(int block) => (this.club.GetStandings(block) + this.club.GetSeats(block)).ToString("N0");

        private static RectangleF OutlineBounds(PointF[] points)
        {
            float left = float.MaxValue, top = float.MaxValue, right = float.MinValue, bottom = float.MinValue;
            foreach (PointF p in points)
            {
                left = Math.Min(left, p.X); top = Math.Min(top, p.Y);
                right = Math.Max(right, p.X); bottom = Math.Max(bottom, p.Y);
            }
            return RectangleF.FromLTRB(left, top, right, bottom);
        }
    }
}
