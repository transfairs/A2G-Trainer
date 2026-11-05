using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// The editor's map ("Landkarte"): the country with its neighbours and inner borders
    /// (<see cref="CountryMap"/>, in the editor's pixel grid) and every club of the country as a
    /// dot at its map position. The own club is marked, together with its arch rival and fan
    /// friendship, and rings show the distances at which the game's share of away fans changes
    /// (FUN_006489e0). Clicking moves the own club there. Without a map (another country) the
    /// clubs' extent is shown.
    /// </summary>
    public class ClubMap : Control
    {
        private static readonly Color Sea = Color.FromArgb(198, 220, 235);
        private static readonly Dictionary<CountryMap.Layer, Color> LayerColors = new Dictionary<CountryMap.Layer, Color>
        {
            { CountryMap.Layer.NeighbourLand, Color.FromArgb(230, 228, 220) },
            { CountryMap.Layer.NeighbourBorder, Color.FromArgb(160, 160, 155) },
            { CountryMap.Layer.Land, Color.FromArgb(246, 236, 207) },
            { CountryMap.Layer.Subregion, Color.FromArgb(214, 196, 160) },
            { CountryMap.Layer.Region, Color.FromArgb(160, 128, 85) },
            { CountryMap.Layer.Outline, Color.FromArgb(110, 80, 50) },
        };
        // Line widths in screen pixels.
        private static readonly Dictionary<CountryMap.Layer, float> LayerWidths = new Dictionary<CountryMap.Layer, float>
        {
            { CountryMap.Layer.NeighbourBorder, 0.8f },
            { CountryMap.Layer.Subregion, 0.6f },
            { CountryMap.Layer.Region, 1.1f },
            { CountryMap.Layer.Outline, 1.5f },
        };

        /// <summary>Distances where the game's away-fan factor changes (FUN_006489e0: 3, 10, 25, 50, 100, 250); the small ones aren't drawn.</summary>
        internal static readonly int[] DistanceRings = { 25, 50, 100, 250 };

        private static readonly Color OtherClub = Color.FromArgb(120, 120, 120);
        private static readonly Color OwnClub = Color.FromArgb(30, 30, 220);
        private static readonly Color Rival = Color.FromArgb(225, 30, 30);
        private static readonly Color Friend = Color.FromArgb(40, 150, 40);
        private const float EdgeMargin = 12;

        private List<ClubRecords.Entry> clubs = new List<ClubRecords.Entry>();
        private Club club;
        private CountryMap map;
        private ClubRecords.Entry hovered;

        /// <summary>Raised when a click moves the own club.</summary>
        public event EventHandler PositionChanged;

        /// <summary>Creates an empty map.</summary>
        public ClubMap()
        {
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            this.BackColor = Color.FromArgb(235, 235, 225);
        }

        /// <summary>All clubs of the country (the own club's entry included or not).</summary>
        internal List<ClubRecords.Entry> Clubs
        {
            get => this.clubs;
            set { this.clubs = value ?? new List<ClubRecords.Entry>(); this.hovered = null; this.Invalidate(); }
        }

        /// <summary>The own club; its position, rival and fan friendship are taken from it, the map from its country.</summary>
        public Club Club
        {
            get => this.club;
            set { this.club = value; this.map = value != null ? CountryMap.For(value.Country) : null; this.Invalidate(); }
        }

        /// <summary>The map drawn behind the clubs (set with <see cref="Club"/>), or null.</summary>
        internal CountryMap Map
        {
            get => this.map;
            set { this.map = value; this.Invalidate(); }
        }

        // The other clubs: the own club is drawn from its (possibly edited) position instead.
        private IEnumerable<ClubRecords.Entry> Others => this.clubs.Where(c => this.club == null || c.Id != this.club.Id);

        // Map units shown: the editor's whole map, or without one the other clubs' extent (so the
        // frame doesn't move while the own club is placed), grown to include the own club.
        private RectangleF MapBounds
        {
            get
            {
                if (this.map != null)
                    return new RectangleF(0, 0, this.map.Width, this.map.Height);
                List<PointF> points = this.Others.Select(c => new PointF(c.MapX, c.MapY)).ToList();
                if (this.club != null)
                    points.Add(new PointF(this.club.MapX, this.club.MapY));
                if (points.Count == 0)
                    return new RectangleF(0, 0, 100, 100);
                float left = points.Min(p => p.X), top = points.Min(p => p.Y);
                float right = points.Max(p => p.X), bottom = points.Max(p => p.Y);
                return new RectangleF(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
            }
        }

        // Pixels per map unit, the same both ways so distances stay round.
        private float PixelsPerUnit
        {
            get
            {
                RectangleF b = this.MapBounds;
                float margin = this.map != null ? 0 : EdgeMargin;
                return Math.Max(0.01f, Math.Min((this.Width - 2 * margin) / b.Width, (this.Height - 2 * margin) / b.Height));
            }
        }

        /// <summary>Screen point of a map position for the current size.</summary>
        internal PointF ToScreen(float x, float y)
        {
            RectangleF b = this.MapBounds;
            float scale = this.PixelsPerUnit;
            float offsetX = (this.Width - b.Width * scale) / 2, offsetY = (this.Height - b.Height * scale) / 2;
            return new PointF(offsetX + (x - b.Left) * scale, offsetY + (y - b.Top) * scale);
        }

        /// <summary>Map position of a screen point for the current size, rounded to whole units.</summary>
        internal Point ToMap(Point screen)
        {
            RectangleF b = this.MapBounds;
            float scale = this.PixelsPerUnit;
            float offsetX = (this.Width - b.Width * scale) / 2, offsetY = (this.Height - b.Height * scale) / 2;
            int x = (int)Math.Round(b.Left + (screen.X - offsetX) / scale);
            int y = (int)Math.Round(b.Top + (screen.Y - offsetY) / scale);
            return new Point(Math.Max(short.MinValue, Math.Min(short.MaxValue, x)), Math.Max(short.MinValue, Math.Min(short.MaxValue, y)));
        }

        /// <inheritdoc/>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            this.PaintMap(g);

            using (Brush other = new SolidBrush(OtherClub))
            {
                foreach (ClubRecords.Entry entry in this.Others)
                {
                    PointF p = this.ToScreen(entry.MapX, entry.MapY);
                    g.FillEllipse(other, p.X - 2.5f, p.Y - 2.5f, 5, 5);
                }
            }
            if (this.club == null)
                return;

            PointF own = this.ToScreen(this.club.MapX, this.club.MapY);
            using (Pen ring = new Pen(Color.FromArgb(110, OwnClub)) { DashStyle = DashStyle.Dash })
            {
                foreach (int distance in DistanceRings)
                {
                    float r = distance * this.PixelsPerUnit;
                    g.DrawEllipse(ring, own.X - r, own.Y - r, 2 * r, 2 * r);
                }
            }

            this.Mark(g, this.club.FanFriendshipId, Friend);
            this.Mark(g, this.club.ArchRivalId, Rival);

            using (Brush brush = new SolidBrush(OwnClub))
                g.FillEllipse(brush, own.X - 5, own.Y - 5, 10, 10);
            this.Label(g, this.club.ClubName, own, OwnClub);

            if (this.hovered != null && this.hovered.Id != this.club.ArchRivalId && this.hovered.Id != this.club.FanFriendshipId)
                this.Label(g, this.hovered.Name, this.ToScreen(this.hovered.MapX, this.hovered.MapY), OtherClub);
        }

        // Sea, neighbours, the country and its borders, drawn in map pixels.
        private void PaintMap(Graphics g)
        {
            if (this.map == null)
                return;
            g.Clear(Sea);
            float scale = this.PixelsPerUnit;
            PointF origin = this.ToScreen(0, 0);
            GraphicsState state = g.Save();
            try
            {
                g.TranslateTransform(origin.X, origin.Y);
                g.ScaleTransform(scale, scale);
                foreach (CountryMap.Layer layer in new[] { CountryMap.Layer.NeighbourLand, CountryMap.Layer.NeighbourBorder, CountryMap.Layer.Land, CountryMap.Layer.Subregion, CountryMap.Layer.Region, CountryMap.Layer.Outline })
                {
                    if (!this.map.TryGetLayer(layer, out List<PointF[]> shapes))
                        continue;
                    if (LayerWidths.TryGetValue(layer, out float width))
                    {
                        using (Pen pen = new Pen(LayerColors[layer], width / scale) { LineJoin = LineJoin.Round })
                            foreach (PointF[] line in shapes.Where(s => s.Length >= 2))
                                g.DrawLines(pen, line);
                    }
                    else
                    {
                        using (GraphicsPath path = new GraphicsPath(FillMode.Winding))
                        using (Brush brush = new SolidBrush(LayerColors[layer]))
                        {
                            foreach (PointF[] ring in shapes.Where(s => s.Length >= 3))
                                path.AddPolygon(ring);
                            g.FillPath(brush, path);
                        }
                    }
                }
            }
            finally
            {
                g.Restore(state);
            }
        }

        // Rings the club with the given ID (rival, fan friendship) and names it.
        private void Mark(Graphics g, byte id, Color color)
        {
            ClubRecords.Entry entry = this.Others.FirstOrDefault(c => c.Id == id);
            if (id == Club.NoClub || entry == null)
                return;
            PointF p = this.ToScreen(entry.MapX, entry.MapY);
            using (Pen pen = new Pen(color, 2))
                g.DrawEllipse(pen, p.X - 5, p.Y - 5, 10, 10);
            this.Label(g, entry.Name, p, color);
        }

        // A club name next to its dot, kept inside the control.
        private void Label(Graphics g, string text, PointF at, Color color)
        {
            SizeF size = g.MeasureString(text, this.Font);
            float x = Math.Min(at.X + 7, this.Width - size.Width - 2);
            float y = Math.Max(2, Math.Min(at.Y - size.Height / 2, this.Height - size.Height - 2));
            using (Brush back = new SolidBrush(Color.FromArgb(200, this.BackColor)))
                g.FillRectangle(back, x, y, size.Width, size.Height);
            using (Brush brush = new SolidBrush(color))
                g.DrawString(text, this.Font, brush, x, y);
        }

        /// <summary>The club whose dot is nearest to the screen point, within a few pixels; null if none.</summary>
        internal ClubRecords.Entry ClubAt(Point screen)
        {
            ClubRecords.Entry nearest = null;
            float best = 6 * 6;
            foreach (ClubRecords.Entry entry in this.Others)
            {
                PointF p = this.ToScreen(entry.MapX, entry.MapY);
                float d = (p.X - screen.X) * (p.X - screen.X) + (p.Y - screen.Y) * (p.Y - screen.Y);
                if (d <= best)
                {
                    best = d;
                    nearest = entry;
                }
            }
            return nearest;
        }

        /// <inheritdoc/>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            ClubRecords.Entry entry = this.ClubAt(e.Location);
            if (entry != this.hovered)
            {
                this.hovered = entry;
                this.Invalidate();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (this.hovered != null)
            {
                this.hovered = null;
                this.Invalidate();
            }
        }

        /// <inheritdoc/>
        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (this.club == null || e.Button != MouseButtons.Left)
                return;
            Point position = this.ToMap(e.Location);
            this.club.MapX = (short)position.X;
            this.club.MapY = (short)position.Y;
            this.Invalidate();
            this.PositionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
