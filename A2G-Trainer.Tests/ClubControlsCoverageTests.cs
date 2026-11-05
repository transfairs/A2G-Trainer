using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// The club tab's own controls on their own: StadiumPlan, ClubMap, JerseyView, CountryMap and
    /// ClubSelection - every block's band, a map with a single layer, clicks and edits nobody listens to.
    /// </summary>
    public class ClubControlsCoverageTests
    {
        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        #region StadiumPlan
        [Fact]
        public void StadiumPlan_EveryBlocksBand_GrowsFromThePitchOutwards()
        {
            using (StadiumPlan plan = new StadiumPlan { Size = new Size(330, 334) })
            {
                PointF pitchCentre = new PointF(165, 167);
                Func<PointF, PointF, float> distance = (a, b) => (float)Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));
                for (int block = 0; block < Club.BlockCount; block++)
                {
                    PointF[] outline = plan.BlockOutline(block);
                    PointF centroid = new PointF(outline.Average(p => p.X), outline.Average(p => p.Y));
                    Func<PointF, PointF> nudged = p =>
                    {
                        float d = distance(p, centroid);
                        return new PointF(p.X + (centroid.X - p.X) * 2 / d, p.Y + (centroid.Y - p.Y) * 2 / d);
                    };
                    PointF nearest = outline.OrderBy(p => distance(p, pitchCentre)).First();
                    PointF farthest = outline.OrderBy(p => distance(p, pitchCentre)).Last();
                    RectangleF band = (RectangleF)Invoke(plan, "InnerBand", block, 0.5f);

                    Assert.True(band.Contains(nudged(nearest)), $"Block {block}: the band starts at the pitch");
                    Assert.False(band.Contains(nudged(farthest)), $"Block {block}: half a block doesn't reach the outer edge");
                }
            }
        }

        [Fact]
        public void StadiumPlan_PaintsEveryBlock_RoofedStandingPlacesDarker()
        {
            using (StadiumPlan plan = new StadiumPlan { Size = new Size(330, 334) })
            using (Bitmap bitmap = new Bitmap(330, 334))
            {
                Club club = new Club();
                for (int block = 0; block < Club.BlockCount; block++)
                    club.SetStandings(block, (short)(StadiumPlan.FullBlock / 2));
                club.SetRoof(4, true);   // E, above the pitch's middle
                plan.Club = club;
                Assert.Same(club, plan.Club);

                plan.DrawToBitmap(bitmap, new Rectangle(Point.Empty, plan.Size));

                // E spans x 133..197 above the pitch (top edge y 69): its standing half next to the pitch.
                Assert.Equal(StadiumPlan.StandingRoofed.ToArgb(), bitmap.GetPixel(140, 62).ToArgb());
                // D, to its right, is open.
                Assert.Equal(StadiumPlan.StandingOpen.ToArgb(), bitmap.GetPixel(205, 62).ToArgb());
            }
        }

        [Fact]
        public void StadiumPlan_SelectingWithoutAListener_StillSelects()
        {
            using (StadiumPlan plan = new StadiumPlan { Size = new Size(330, 334) })
            {
                plan.SelectedBlock = 5;
                Assert.Equal(5, plan.SelectedBlock);

                int changes = 0;
                plan.SelectedBlockChanged += (s, e) => changes++;
                plan.SelectedBlock = 6;
                Assert.Equal(1, changes);
            }
        }
        #endregion

        #region ClubMap
        // A 100 x 80 map with nothing but the country's land, the triangle (0,0) (100,0) (0,100).
        private static CountryMap LandOnly()
        {
            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("A2GM"));
                writer.Write((byte)1);
                writer.Write((short)100);
                writer.Write((short)80);
                writer.Write((byte)1);
                writer.Write((byte)CountryMap.Layer.Land);
                writer.Write(1);
                writer.Write((ushort)3);
                foreach (short[] p in new[] { new short[] { 0, 0 }, new short[] { 1600, 0 }, new short[] { 0, 1600 } })
                {
                    writer.Write(p[0]);
                    writer.Write(p[1]);
                }
                writer.Flush();
                stream.Position = 0;
                return CountryMap.Read(new MemoryStream(stream.ToArray()));
            }
        }

        [Fact]
        public void ClubMap_DrawsOnlyTheLayersTheMapHas()
        {
            using (ClubMap map = new ClubMap { Size = new Size(200, 160) })
            using (Bitmap bitmap = new Bitmap(200, 160))
            {
                CountryMap land = LandOnly();
                Assert.False(land.TryGetLayer(CountryMap.Layer.Outline, out _));
                map.Map = land;

                map.DrawToBitmap(bitmap, new Rectangle(Point.Empty, map.Size));

                Assert.Equal(Color.FromArgb(246, 236, 207).ToArgb(), bitmap.GetPixel(30, 30).ToArgb());   // land
                Assert.Equal(Color.FromArgb(198, 220, 235).ToArgb(), bitmap.GetPixel(190, 150).ToArgb()); // sea
            }
        }

        [Fact]
        public void ClubMap_LeftClickMovesTheClubWithoutAListener_RightClickDoesNot()
        {
            using (ClubMap map = new ClubMap { Size = new Size(200, 160) })
            {
                Club club = new Club { Country = PlayerEnums.Country.Sonstige, MapX = 10, MapY = 10 };
                map.Clubs = new List<ClubRecords.Entry> { new ClubRecords.Entry { Id = 5, MapX = 0, MapY = 0 }, new ClubRecords.Entry { Id = 6, MapX = 100, MapY = 80 } };
                map.Club = club;
                Assert.Same(club, map.Club);
                Point target = Point.Round(map.ToScreen(50, 40));

                Invoke(map, "OnMouseClick", new MouseEventArgs(MouseButtons.Right, 1, target.X, target.Y, 0));
                Assert.Equal((short)10, club.MapX);

                Invoke(map, "OnMouseClick", new MouseEventArgs(MouseButtons.Left, 1, target.X, target.Y, 0));
                Assert.Equal((short)50, club.MapX);
                Assert.Equal((short)40, club.MapY);

                int moves = 0;
                map.PositionChanged += (s, e) => moves++;
                Invoke(map, "OnMouseClick", new MouseEventArgs(MouseButtons.Left, 1, target.X + 20, target.Y, 0));
                Assert.Equal(1, moves);
                Assert.NotEqual((short)50, club.MapX);

                // Without a club a click moves nothing.
                map.Club = null;
                Invoke(map, "OnMouseClick", new MouseEventArgs(MouseButtons.Left, 1, target.X, target.Y, 0));
                Assert.Equal(1, moves);
            }
        }
        #endregion

        #region JerseyView
        [Fact]
        public void JerseyView_EditsWithoutAListener_AndNoClubClearsThePatterns() => StaThread.Run(() =>
        {
            using (JerseyView view = new JerseyView { Size = new Size(500, 330) })
            {
                Club club = new Club();
                view.Club = club;
                Assert.True(view.PatternButton(0, ClubEnums.ShirtPattern.Neutral).Checked);

                view.PatternButton(1, ClubEnums.ShirtPattern.Quer).Checked = true;
                Assert.Equal(ClubEnums.ShirtPattern.Quer, club.GetShirtPattern(1));
                view.Cycle(0, JerseyView.Garment.Shirt, -1);
                Assert.Equal((ClubEnums.JerseyColor)(JerseyView.Colors.Length - 1), club.GetShirtColor(0));

                int changes = 0;
                view.JerseyChanged += (s, e) => changes++;
                view.PatternButton(0, ClubEnums.ShirtPattern.Laengs).Checked = true;
                view.Cycle(1, JerseyView.Garment.Socks, 1);
                Assert.Equal(2, changes);

                view.Club = null;
                Assert.False(view.PatternButton(0, ClubEnums.ShirtPattern.Neutral).Checked);
                Assert.False(view.PatternButton(1, ClubEnums.ShirtPattern.Quer).Checked);
            }
        });
        #endregion

        #region CountryMap
        [Fact]
        public void CountryMap_WithoutItsEmbeddedFile_IsNull()
        {
            PlayerEnums.Country country = (PlayerEnums.Country)0xFE;
            FieldInfo filesField = typeof(CountryMap).GetField("Files", BindingFlags.NonPublic | BindingFlags.Static);
            FieldInfo loadedField = typeof(CountryMap).GetField("Loaded", BindingFlags.NonPublic | BindingFlags.Static);
            Dictionary<PlayerEnums.Country, string> files = (Dictionary<PlayerEnums.Country, string>)filesField.GetValue(null);
            Dictionary<PlayerEnums.Country, CountryMap> loaded = (Dictionary<PlayerEnums.Country, CountryMap>)loadedField.GetValue(null);
            // Both tables are only used under the lock on Loaded.
            lock (loaded)
            {
                files[country] = "MISSING";
                loaded.Remove(country);
            }
            try
            {
                Assert.Null(CountryMap.For(country));
            }
            finally
            {
                lock (loaded)
                {
                    files.Remove(country);
                    loaded.Remove(country);
                }
            }
        }
        #endregion

        #region ClubSelection
        [Fact]
        public void ClubSelection_PickingWithoutAListener_JustSelects() => StaThread.Run(() =>
        {
            using (ComboBox countries = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList })
            using (ComboBox clubs = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList })
            using (Form host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
            {
                host.Controls.Add(countries);
                host.Controls.Add(clubs);
                ClubSquads.Entry a = new ClubSquads.Entry { Country = PlayerEnums.Country.Deutschland, Id = 1, Name = "A" };
                ClubSquads.Entry b = new ClubSquads.Entry { Country = PlayerEnums.Country.Deutschland, Id = 2, Name = "B" };
                ClubSquads.Entry c = new ClubSquads.Entry { Country = PlayerEnums.Country.Deutschland, Id = 3, Name = "C" };
                ClubSelection selection = new ClubSelection(countries, clubs) { Clubs = new List<ClubSquads.Entry> { a, b, c } };
                selection.Show(a);

                clubs.SelectedItem = b;
                Assert.Same(b, clubs.SelectedItem);

                List<ClubSquads.Entry> picked = new List<ClubSquads.Entry>();
                selection.Picked += picked.Add;
                clubs.SelectedItem = c;
                Assert.Equal(new[] { c }, picked);
            }
        });
        #endregion
    }
}
