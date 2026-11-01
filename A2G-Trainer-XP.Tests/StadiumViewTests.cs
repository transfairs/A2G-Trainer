using AnstossTrainer.Shared.Tests;
using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for the stadium tab: the plan (StadiumPlan), editing one block at a time, the legend
    /// and capacity, signed places, and the new stadium fields in ClubController.
    /// </summary>
    public class StadiumViewTests
    {
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        private static Addresses Own => AddressPresets.OWN_CLUB;

        private static void WriteBlock(FakeModule fake, char letter, short standings, short seats, byte weeks = 0)
        {
            fake.WriteDisplayCacheBytes(Own[(ClubEnums.AddressKey)Enum.Parse(typeof(ClubEnums.AddressKey), $"Block{letter}Standings")], BitConverter.GetBytes(standings));
            fake.WriteDisplayCacheBytes(Own[(ClubEnums.AddressKey)Enum.Parse(typeof(ClubEnums.AddressKey), $"Block{letter}Seats")], BitConverter.GetBytes(seats));
            fake.WriteDisplayCacheBytes(Own[(ClubEnums.AddressKey)Enum.Parse(typeof(ClubEnums.AddressKey), $"Block{letter}Weeks")], new[] { weeks });
        }

        // The own club with A: 1,000 standing / 500 seats, K: 20 boxes / 700 seats (roofed), L under construction.
        private static void WithStadiumTab(Action<FakeModule, ClubView> test) => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 15 });
                WriteBlock(fake, 'A', 1000, 500);
                WriteBlock(fake, 'K', 20, 700);
                WriteBlock(fake, 'L', 3000, 0, weeks: 4);
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.ROOF], BitConverter.GetBytes((ushort)ClubEnums.Roof.BlockK));
                trainer.ClubView.RefreshValues(PlayerEnums.AddressType.OWN);
                trainer.ClubView.InitClubTabControl();
                test(fake, trainer.ClubView);
            }
        });

        [Fact]
        public void SelectingABlock_ShowsItsValues_AndBlockKHasBoxes() => WithStadiumTab((fake, view) =>
        {
            StadiumPlan plan = Field<StadiumPlan>(view, "StadiumPlanView");
            Assert.Equal("Block A", Field<GroupBox>(view, "BlockBox").Text);
            Assert.Equal("1000", Field<TextBox>(view, "BlockStandingsInput").Text);
            Assert.Equal("500", Field<TextBox>(view, "BlockSeatsInput").Text);
            Assert.Equal("Stehplätze", Field<Label>(view, "BlockStandingsLabel").Text);

            plan.SelectedBlock = Club.BoxBlock;

            Assert.Equal("Block K", Field<GroupBox>(view, "BlockBox").Text);
            Assert.Equal("Logen", Field<Label>(view, "BlockStandingsLabel").Text);
            Assert.Equal("20", Field<TextBox>(view, "BlockStandingsInput").Text);
            Assert.True(Field<CheckBox>(view, "BlockRoofInput").Checked);
        });

        [Fact]
        public void Legend_SumsThePlacesByKind_AndTheCapacityLeavesOutBlocksUnderConstruction() => WithStadiumTab((fake, view) =>
        {
            Assert.Equal(4000.ToString("N0"), Field<Label>(view, "LegendStandingOpenValue").Text);   // A + L
            Assert.Equal("0", Field<Label>(view, "LegendStandingRoofedValue").Text);
            Assert.Equal("500", Field<Label>(view, "LegendSeatsOpenValue").Text);
            Assert.Equal("700", Field<Label>(view, "LegendSeatsRoofedValue").Text);
            Assert.Equal("20", Field<Label>(view, "LegendBoxesValue").Text);
            Assert.Equal(2220.ToString("N0"), Field<Label>(view, "CapacityValue").Text);              // L closed
        });

        [Fact]
        public void EditingTheBlock_WritesTheClub_AndUpdatesTheLegend() => WithStadiumTab((fake, view) =>
        {
            Club club = Field<Club>(view, "club");
            Field<StadiumPlan>(view, "StadiumPlanView").SelectedBlock = 11;   // L

            Field<TextBox>(view, "BlockWeeksInput").Text = "0";
            Field<TextBox>(view, "BlockSeatsInput").Text = "-200";
            Field<CheckBox>(view, "BlockRoofInput").Checked = true;

            Assert.Equal(0, club.BlockLWeeks);
            Assert.Equal(-200, club.BlockLSeats);
            Assert.True(club.IsBlockLRoof);
            Assert.Equal((2220 + 3000 - 200).ToString("N0"), Field<Label>(view, "CapacityValue").Text);
            Assert.Equal(3000.ToString("N0"), Field<Label>(view, "LegendStandingRoofedValue").Text);
        });

        [Fact]
        public void Saving_KeepsTheStadium_AndWritesSignedPlaces() => WithStadiumTab((fake, view) =>
        {
            Field<TextBox>(view, "BlockSeatsInput").Text = "-20";

            Invoke(view, "SaveBtn_Click", null, EventArgs.Empty);

            Assert.Equal((short)-20, BitConverter.ToInt16(fake.ReadDisplayCacheBytes(Own[ClubEnums.AddressKey.BlockASeats], 2), 0));
            Assert.Equal((short)1000, BitConverter.ToInt16(fake.ReadDisplayCacheBytes(Own[ClubEnums.AddressKey.BlockAStandings], 2), 0));
            Assert.Equal("-20", Field<TextBox>(view, "BlockSeatsInput").Text);
            Assert.Equal(-20, Field<Club>(view, "club").BlockASeats);
        });

        [Theory]
        [InlineData("40000", "32767")]
        [InlineData("-40000", "-32768")]
        [InlineData("-", "-")]
        [InlineData("-32768", "-32768")]
        public void PlacesInput_StaysWithinInt16(string typed, string shown) => WithStadiumTab((fake, view) =>
        {
            TextBox input = Field<TextBox>(view, "BlockStandingsInput");
            input.Text = typed;
            Assert.Equal(shown, input.Text);
        });

        [Theory]
        [InlineData("", 0, '-', false)]
        [InlineData("12", 0, '-', false)]
        [InlineData("12", 1, '-', true)]
        [InlineData("-12", 0, '-', true)]
        [InlineData("", 0, '5', false)]
        [InlineData("", 0, 'x', true)]
        public void SignedNumericKeyPress_AllowsOneLeadingMinus(string text, int caret, char key, bool blocked) => WithStadiumTab((fake, view) =>
        {
            TextBox input = Field<TextBox>(view, "BlockSeatsInput");
            input.Text = text;
            input.SelectionStart = caret;
            KeyPressEventArgs e = new KeyPressEventArgs(key);

            Invoke(view, "SignedNumeric_KeyPress", input, e);

            Assert.Equal(blocked, e.Handled);
        });

        [Fact]
        public void Refresh_DoesNotZeroTheBlockOfTheClubStillShown() => WithStadiumTab((fake, view) =>
        {
            Club club = Field<Club>(view, "club");

            view.RefreshValues(PlayerEnums.AddressType.OWN, club);

            Assert.Equal(1000, club.BlockAStandings);
            Assert.Equal("1000", Field<TextBox>(view, "BlockStandingsInput").Text);
        });

        [Fact]
        public void ParseInt16_FallsBackToZero()
        {
            Assert.Equal(-5, ClubView.ParseInt16("-5"));
            Assert.Equal(0, ClubView.ParseInt16(""));
            Assert.Equal(0, ClubView.ParseInt16("-"));
        }

        [Fact]
        public void Plan_HitsEveryBlockAtItsCentre_AndPaints()
        {
            using (StadiumPlan plan = new StadiumPlan { Size = new Size(330, 334) })
            {
                for (int block = 0; block < Club.BlockCount; block++)
                {
                    PointF[] outline = plan.BlockOutline(block);
                    // Inside every block: halfway between the first point and the centroid.
                    float cx = outline.Average(p => p.X), cy = outline.Average(p => p.Y);
                    Point inside = Point.Round(new PointF((cx * 3 + outline[0].X) / 4, (cy * 3 + outline[0].Y) / 4));
                    Assert.Equal(block, plan.BlockAt(inside));
                }
                Assert.Equal(-1, plan.BlockAt(new Point(165, 167)));   // the pitch

                Club club = new Club { BlockAStandings = 100, BlockASeats = 50, BlockKStandings = 10, BlockLSeats = -5, BlockBWeeks = 2 };
                club.IsBlockKRoof = true;
                plan.Club = club;
                using (Bitmap bitmap = new Bitmap(plan.Width, plan.Height))
                    plan.DrawToBitmap(bitmap, new Rectangle(Point.Empty, plan.Size));
                plan.Club = null;
                using (Bitmap bitmap = new Bitmap(plan.Width, plan.Height))
                    plan.DrawToBitmap(bitmap, new Rectangle(Point.Empty, plan.Size));
            }
        }

        [Fact]
        public void Plan_SelectsByClick_ClampsAndReportsChanges()
        {
            using (StadiumPlan plan = new StadiumPlan { Size = new Size(330, 334) })
            {
                int changes = 0;
                plan.SelectedBlockChanged += (s, e) => changes++;

                plan.SelectedBlock = 0;
                Assert.Equal(0, changes);
                plan.SelectedBlock = 99;
                Assert.Equal(Club.BlockCount - 1, plan.SelectedBlock);

                PointF[] h = plan.BlockOutline(7);
                Point inH = Point.Round(new PointF(h.Average(p => p.X), h.Average(p => p.Y)));
                Invoke(plan, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, inH.X, inH.Y, 0));
                Assert.Equal(7, plan.SelectedBlock);
                Invoke(plan, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, 165, 167, 0));
                Assert.Equal(7, plan.SelectedBlock);
                Assert.Equal(2, changes);
            }
        }

        [Fact]
        public void Controller_ReadsAndWritesTheNewStadiumFields()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 15 });
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.HasRunningTrack], new byte[] { 1 });
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.GrantPot], BitConverter.GetBytes(750000));
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.GrantRate], BitConverter.GetBytes((short)15));
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.BlockCSeats], BitConverter.GetBytes((short)-300));

                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;
                Assert.True(club.HasRunningTrack);
                Assert.False(club.HasUpperSeats);
                Assert.Equal(750000, club.GrantPot);
                Assert.Equal(15, club.GrantRate);
                Assert.Equal(-300, club.BlockCSeats);

                club.ClubName = "X";
                club.StadiumName = "Y";
                club.HasRunningTrack = false;
                club.HasUpperSeats = true;
                club.GrantPot = 123;
                club.GrantRate = 25;
                controller.Save();

                Assert.Equal(0, fake.ReadDisplayCacheBytes(Own[ClubEnums.AddressKey.HasRunningTrack], 1)[0]);
                Assert.Equal(1, fake.ReadDisplayCacheBytes(Own[ClubEnums.AddressKey.HasUpperSeats], 1)[0]);
                Assert.Equal(123, BitConverter.ToInt32(fake.ReadDisplayCacheBytes(Own[ClubEnums.AddressKey.GrantPot], 4), 0));
                Assert.Equal((short)25, BitConverter.ToInt16(fake.ReadDisplayCacheBytes(Own[ClubEnums.AddressKey.GrantRate], 2), 0));
                Assert.Equal(-300, BitConverter.ToInt32(fake.ReadDisplayCacheBytes(Own[ClubEnums.AddressKey.Capacity], 4), 0));
            }
        }

        [Fact]
        public void NewProperties_RaisePropertyChanged()
        {
            Club club = new Club();
            var changed = new System.Collections.Generic.List<string>();
            club.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            club.HasRunningTrack = true;
            club.HasUpperSeats = true;
            club.GrantPot = 1;
            club.GrantRate = 2;
            club.Capacity = 3;

            Assert.Equal(new[] { nameof(Club.HasRunningTrack), nameof(Club.HasUpperSeats), nameof(Club.GrantPot), nameof(Club.GrantRate), nameof(Club.Capacity) }, changed);
        }

        [Fact]
        public void BlockAccessors_ReachEveryBlock()
        {
            Club club = new Club();
            for (int block = 0; block < Club.BlockCount; block++)
            {
                club.SetStandings(block, (short)(block + 1));
                club.SetSeats(block, (short)-(block + 1));
                club.SetWeeks(block, (byte)block);
                club.SetRoof(block, block % 2 == 0);
            }

            Assert.Equal(12, club.BlockLStandings);
            Assert.Equal(-1, club.BlockASeats);
            Assert.Equal(11, club.BlockLWeeks);
            Assert.True(club.IsBlockCRoof);
            Assert.False(club.IsBlockDRoof);
            Assert.Equal((ClubEnums.Roof)0x555, club.Roof);
            Assert.Equal(club.BlockKStandings, club.GetStandings(Club.BoxBlock));
        }
    }
}
