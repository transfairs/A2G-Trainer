using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using A2G_Trainer.View;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for the data editor's club settings: short name, chant, fan name, town, article,
    /// preposition, fan turnout, board, arch rival, fan friendship, jerseys and map position -
    /// read from the working copy and the club record, written to every copy (ClubController,
    /// ClubRecords), shown on the "Identität" and "Landkarte" tabs (ClubView, ClubMap).
    /// </summary>
    public class ClubEditorSettingsTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");
        private static Addresses Own => AddressPresets.OWN_CLUB;

        // Germany is league system 0 with three clubs from record 0; the own club is ID 1.
        private const PlayerEnums.Country Country = PlayerEnums.Country.Deutschland;
        private const byte OwnId = 1;
        private const uint OtherCopy = 0x510000;

        private static uint Record(byte id) => Settings.ClubRecordTableOffset + id * Settings.ClubRecordStride;

        private static void WriteRecordClub(FakeModule fake, byte id, string name, short x, short y)
        {
            fake.WriteModuleBytes(Record(id), Latin1.GetBytes(name + "\0"));
            fake.WriteModuleBytes(Record(id) + Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.MapX), BitConverter.GetBytes(x));
            fake.WriteModuleBytes(Record(id) + Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.MapY), BitConverter.GetBytes(y));
        }

        private static void CacheSlot(FakeModule fake, int slot, byte id, uint? pointer = null)
        {
            byte[] entry = new byte[Settings.ClubCacheStride];
            entry[0] = 1;
            entry[1] = (byte)Country;
            entry[2] = id;
            fake.WriteModuleBytes(Settings.ClubCacheTableOffset + (uint)slot * Settings.ClubCacheStride, entry.Take(8).ToArray());
            if (pointer.HasValue)
                fake.WriteModuleBytes(Settings.ClubCacheTableOffset + (uint)slot * Settings.ClubCacheStride + Settings.ClubCachePointerOffset, BitConverter.GetBytes(pointer.Value));
        }

        // The own club (Braunschweig-like values) in cache slot 0 and slot 3, plus two other clubs.
        private static void SeedOwnClub(FakeModule fake, bool withRecord = true)
        {
            fake.RoutePointerSlot(Settings.ClubAddress[0]);
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.NAME], Latin1.GetBytes("Eintr. Braunschweig\0"));
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.ID], new[] { OwnId });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.COUNTRY], new[] { (byte)Country });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 15 });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.Jersey], new byte[] { 0x42, 3, 2, 3, 2, 3 });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.FanTurnout], new byte[] { 3 });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.FanType], new byte[] { 5 });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.Article], new byte[] { 2 });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.FanFriendship], new byte[] { 0xFF });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.ArchRival], new byte[] { 2 });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.Board], new byte[] { 3 });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.Preposition], new byte[] { 2 });
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.Town], Latin1.GetBytes("Braunschweig\0"));
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.MapX], BitConverter.GetBytes((short)294));
            fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.MapY], BitConverter.GetBytes((short)111));

            // Slot 0's pointer is the club pointer the trainer reads through; slot 3 holds another copy.
            CacheSlot(fake, 0, OwnId);
            CacheSlot(fake, 3, OwnId, fake.ModuleBase + OtherCopy);
            CacheSlot(fake, 4, 2, fake.ModuleBase + OtherCopy + 0x2000);

            if (!withRecord)
                return;
            fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + (uint)Country, new byte[] { 0 });
            fake.WriteModuleBytes(Settings.LeagueClubCountTableOffset, BitConverter.GetBytes((short)3));
            fake.WriteModuleBytes(Settings.LeagueBaseTableOffset, BitConverter.GetBytes((short)0));
            WriteRecordClub(fake, 0, "VfL Osnabrück", 233, 112);
            WriteRecordClub(fake, OwnId, "Eintr. Braunschweig", 294, 111);
            WriteRecordClub(fake, 2, "FC Hannover 66", 279, 105);
            fake.WriteModuleBytes(Record(OwnId) + Settings.ClubRecordShortNameOffset, Latin1.GetBytes("EBS\0"));
            fake.WriteModuleBytes(Record(OwnId) + Settings.ClubRecordChantOffset, Latin1.GetBytes("Löwen\0"));
            fake.WriteModuleBytes(Record(OwnId) + Settings.ClubRecordFanNameOffset, Latin1.GetBytes("Braunschweiger\0"));
        }

        [Fact]
        public void GetEntity_ReadsTheEditorSettingsFromTheWorkingCopyAndTheRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedOwnClub(fake);

                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;

                Assert.True(club.HasRecord);
                Assert.Equal("EBS", club.ShortName);
                Assert.Equal("Löwen", club.Chant);
                Assert.Equal("Braunschweiger", club.FanName);
                Assert.Equal("Braunschweig", club.Town);
                Assert.Equal(ClubEnums.Article.Ohne, club.Article);
                Assert.Equal(ClubEnums.Preposition.Von, club.Preposition);
                Assert.Equal(ClubEnums.FanTurnout.Durchschnitt, club.FanTurnout);
                Assert.Equal(ClubEnums.FanType.Erfolgshungrig, club.FanType);
                Assert.Equal(ClubEnums.Board.Normal, club.Board);
                Assert.Equal((byte)2, club.ArchRivalId);
                Assert.Equal(Club.NoClub, club.FanFriendshipId);
                Assert.Equal(new byte[] { 0x42, 3, 2, 3, 2, 3 }, club.Jersey);
                Assert.Equal((short)294, club.MapX);
                Assert.Equal((short)111, club.MapY);

                Assert.Equal(new[] { "VfL Osnabrück", "Eintr. Braunschweig", "FC Hannover 66" }, controller.CountryClubs.Select(c => c.Name));
                Assert.Equal(new byte[] { 0, 1, 2 }, controller.CountryClubs.Select(c => c.Id));
                Assert.Equal((short)279, controller.CountryClubs[2].MapX);
                Assert.Equal((short)105, controller.CountryClubs[2].MapY);
            }
        }

        [Fact]
        public void Save_WritesTheWorkingCopyEveryOtherCachedCopyAndTheRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedOwnClub(fake);
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;

                club.ShortName = "BTS";
                club.Chant = "Eintracht";
                club.FanName = "Löwenfans";
                club.Town = "Wolfenbüttel";
                club.Article = ClubEnums.Article.Die;
                club.Preposition = ClubEnums.Preposition.VonDer;
                club.FanTurnout = ClubEnums.FanTurnout.WahreHorden;
                club.FanType = ClubEnums.FanType.Frustriert;
                club.Board = ClubEnums.Board.Treu;
                club.ArchRivalId = 0;
                club.FanFriendshipId = 2;
                club.SetShirtPattern(0, ClubEnums.ShirtPattern.Laengs);
                club.SetSocksColor(1, ClubEnums.JerseyColor.Weinrot);
                club.MapX = 300;
                club.MapY = -5;
                controller.Save();

                byte[] expectedJersey = { 0x12, 3, 2, 3, 2, 12 };
                foreach (Func<string, int, byte[]> copy in new Func<string, int, byte[]>[]
                {
                    (offset, length) => fake.ReadDisplayCacheBytes(offset, length),
                    (offset, length) => fake.ReadModuleBytes(OtherCopy + Convert.ToUInt32(offset, 16), length),
                })
                {
                    Assert.Equal(expectedJersey, copy(Own[ClubEnums.AddressKey.Jersey], 6));
                    Assert.Equal(new byte[] { 0, 6 }, copy(Own[ClubEnums.AddressKey.FanTurnout], 2));
                    Assert.Equal(new byte[] { 1 }, copy(Own[ClubEnums.AddressKey.Article], 1));
                    Assert.Equal(new byte[] { 2 }, copy(Own[ClubEnums.AddressKey.FanFriendship], 1));
                    Assert.Equal(new byte[] { 0 }, copy(Own[ClubEnums.AddressKey.ArchRival], 1));
                    Assert.Equal(new byte[] { 5 }, copy(Own[ClubEnums.AddressKey.Board], 1));
                    Assert.Equal(new byte[] { 3 }, copy(Own[ClubEnums.AddressKey.Preposition], 1));
                    Assert.Equal("Wolfenbüttel\0", Latin1.GetString(copy(Own[ClubEnums.AddressKey.Town], 13)));
                    Assert.Equal((short)300, BitConverter.ToInt16(copy(Own[ClubEnums.AddressKey.MapX], 2), 0));
                    Assert.Equal((short)-5, BitConverter.ToInt16(copy(Own[ClubEnums.AddressKey.MapY], 2), 0));
                }

                // The other club's cached copy is left alone.
                Assert.Equal(new byte[6], fake.ReadModuleBytes(OtherCopy + 0x2000 + 0xE8, 6));

                uint record = Record(OwnId);
                Assert.Equal(expectedJersey, fake.ReadModuleBytes(record + 0xE0, 6));
                Assert.Equal(new byte[] { 0, 6 }, fake.ReadModuleBytes(record + 0xE6, 2));
                Assert.Equal(new byte[] { 1, 2, 0, 5 }, fake.ReadModuleBytes(record + 0xEA, 4));
                Assert.Equal(new byte[] { 3 }, fake.ReadModuleBytes(record + 0xF0, 1));
                Assert.Equal("Wolfenbüttel\0", Latin1.GetString(fake.ReadModuleBytes(record + 0x509, 13)));
                Assert.Equal((short)300, BitConverter.ToInt16(fake.ReadModuleBytes(record + 0x702, 2), 0));
                Assert.Equal((short)-5, BitConverter.ToInt16(fake.ReadModuleBytes(record + 0x704, 2), 0));
                Assert.Equal("BTS\0", Latin1.GetString(fake.ReadModuleBytes(record + 0x14, 4)));
                Assert.Equal("Eintracht\0", Latin1.GetString(fake.ReadModuleBytes(record + 0x18, 10)));
                Assert.Equal("Löwenfans\0", Latin1.GetString(fake.ReadModuleBytes(record + 0x4A, 10)));

                Club reread = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN).Club;
                Assert.Equal("BTS", reread.ShortName);
                Assert.Equal("Eintracht", reread.Chant);
                Assert.Equal("Löwenfans", reread.FanName);
            }
        }

        [Fact]
        public void Save_PadsAShortShortNameWithDashes_AndOverwritesLongerOldTexts()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedOwnClub(fake);
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                controller.Club.ShortName = "E";
                controller.Club.Chant = "Ei";
                controller.Club.Town = "BS";
                controller.Save();

                Assert.Equal("E--\0", Latin1.GetString(fake.ReadModuleBytes(Record(OwnId) + 0x14, 4)));
                Assert.Equal(new byte[Club.FanTextLength - 1], fake.ReadModuleBytes(Record(OwnId) + 0x18 + 2, Club.FanTextLength - 1));
                Assert.Equal(new byte[Club.TownLength - 1], fake.ReadDisplayCacheBytes("83B", Club.TownLength - 1));
            }
        }

        [Fact]
        public void WithoutAClubRecord_TheRecordTextsAreUnknownAndOnlyTheWorkingCopiesAreWritten()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedOwnClub(fake, withRecord: false);
                fake.MakeModuleRegionUnreadable(Settings.LeagueSystemByCountryOffset, 1);
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);

                Assert.False(controller.Club.HasRecord);
                Assert.Equal("", controller.Club.ShortName);
                Assert.Empty(controller.CountryClubs);

                controller.Club.Board = ClubEnums.Board.Pulverfass;
                controller.Club.Chant = "Nicht schreiben";
                controller.Save();

                Assert.Equal(new byte[] { 0 }, fake.ReadDisplayCacheBytes(Own[ClubEnums.AddressKey.Board], 1));
                Assert.Equal(new byte[] { 0 }, fake.ReadModuleBytes(OtherCopy + 0xF5, 1));
                Assert.Equal(new byte[4], fake.ReadModuleBytes(Record(OwnId) + 0x14, 4));
                Assert.Equal(new byte[4], fake.ReadModuleBytes(Record(OwnId) + 0x18, 4));
            }
        }

        [Fact]
        public void ClubRecords_AClubIdBeyondTheLeagueSystemsCountHasNoRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedOwnClub(fake);
                Assert.Null(ClubRecords.RecordAddress(fake.Memory, FixedAddresses.For(fake.Memory), Country, 3));
                Assert.Null(ClubRecords.RecordAddress(fake.Memory, FixedAddresses.For(fake.Memory), PlayerEnums.Country.Belgien, 0));
                Assert.Null(ClubRecords.RecordAddress(fake.Memory, null, Country, 0));
                Assert.Empty(ClubRecords.WorkingCopies(fake.Memory, null, Country, OwnId));
                Assert.Equal(fake.ModuleBase + Record(2), ClubRecords.RecordAddress(fake.Memory, FixedAddresses.For(fake.Memory), Country, 2));
            }
        }

        [Fact]
        public void ClubRecords_SkipsAnUnreadableRecordInTheCountryList()
        {
            using (FakeModule fake = new FakeModule())
            {
                // Ten clubs; the page with record 9 can't be read (it also holds the end of record 8).
                SeedOwnClub(fake);
                fake.WriteModuleBytes(Settings.LeagueClubCountTableOffset, BitConverter.GetBytes((short)10));
                fake.MakeModuleRegionUnreadable(Record(9), 19);

                List<ClubRecords.Entry> clubs = ClubRecords.CountryClubs(fake.Memory, FixedAddresses.For(fake.Memory), Country);

                Assert.Equal(new byte[] { 0, 1, 2 }, clubs.Take(3).Select(c => c.Id));
                Assert.DoesNotContain(clubs, c => c.Id == 9);
            }
        }

        [Fact]
        public void Jersey_ShirtByteHoldsColourAndPattern_AndSettersKeepTheOtherHalf()
        {
            Club club = new Club { Jersey = new byte[] { 0x42, 11, 2, 11, 2, 11 } };

            Assert.Equal(ClubEnums.JerseyColor.Gelb, club.GetShirtColor(0));
            Assert.Equal(ClubEnums.ShirtPattern.Schulter, club.GetShirtPattern(0));
            Assert.Equal(ClubEnums.JerseyColor.Schwarz, club.GetShortsColor(0));
            Assert.Equal(ClubEnums.JerseyColor.Gelb, club.GetSocksColor(0));
            Assert.Equal(ClubEnums.JerseyColor.Schwarz, club.GetShirtColor(1));
            Assert.Equal(ClubEnums.ShirtPattern.Neutral, club.GetShirtPattern(1));

            club.SetShirtColor(0, ClubEnums.JerseyColor.Weinrot);
            club.SetShirtPattern(1, ClubEnums.ShirtPattern.AermelWieHose);
            club.SetShortsColor(1, ClubEnums.JerseyColor.Weiss);

            Assert.Equal(new byte[] { 0x4C, 11, 2, 0x6B, 0, 11 }, club.Jersey);
            Assert.Throws<ArgumentException>(() => club.Jersey = new byte[5]);
            Assert.Throws<ArgumentException>(() => club.Jersey = null);
        }

        [Fact]
        public void Texts_AreCutToTheGamesLimits()
        {
            Club club = new Club
            {
                ShortName = "ABCD",
                Chant = new string('x', 30),
                FanName = new string('y', 30),
                Town = new string('z', 30)
            };

            Assert.Equal("ABC", club.ShortName);
            Assert.Equal(Club.FanTextLength, club.Chant.Length);
            Assert.Equal(Club.FanTextLength, club.FanName.Length);
            Assert.Equal(Club.TownLength, club.Town.Length);
            club.Town = null;
            Assert.Equal("", club.Town);
        }

        #region View
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static Club ShownClub(ClubView view) => Field<Club>(view, "club");

        // Shows the club view in a form with the given tab open, so its bindings are live. An
        // exception WinForms raises while doing so fails the test instead of popping up a dialog.
        private static void WithIdentityTab(Action<FakeModule, ClubView> test, bool withRecord = true, string tab = "IdentityTab") => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            using (Form host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
            {
                SeedOwnClub(fake, withRecord);
                ClubView view = trainer.ClubView;
                view.RefreshValues(PlayerEnums.AddressType.OWN);
                view.InitClubTabControl();

                Exception uiError = null;
                System.Threading.ThreadExceptionEventHandler handler = (s, e) => uiError = uiError ?? e.Exception;
                Application.ThreadException += handler;
                try
                {
                    host.Controls.Add(view);
                    host.Show();
                    Field<TabControl>(view, "ClubTabControl").SelectedTab = Field<TabPage>(view, tab);
                    Application.DoEvents();
                    if (uiError != null)
                        throw new Xunit.Sdk.XunitException("Fehler in der Oberfläche: " + uiError);
                }
                finally
                {
                    Application.ThreadException -= handler;
                }
                test(fake, view);
            }
        });

        [Fact]
        public void IdentityTab_ShowsTheSettings() => WithIdentityTab((fake, view) =>
        {
            Assert.Equal("EBS", Field<TextBox>(view, "ShortNameInput").Text);
            Assert.Equal("Löwen", Field<TextBox>(view, "ChantInput").Text);
            Assert.Equal("Braunschweiger", Field<TextBox>(view, "FanNameInput").Text);
            Assert.Equal("Braunschweig", Field<TextBox>(view, "TownInput").Text);
            Assert.Equal("ohne Artikel", Field<ComboBox>(view, "ArticleInput").Text);
            Assert.Equal("von", Field<ComboBox>(view, "PrepositionInput").Text);
            Assert.Equal("Durchschnitt", Field<ComboBox>(view, "FanTurnoutInput").Text);
            Assert.Equal("normal", Field<ComboBox>(view, "BoardInput").Text);
            Assert.Equal("FC Hannover 66", Field<ComboBox>(view, "ArchRivalInput").Text);
            Assert.Equal("–", Field<ComboBox>(view, "FanFriendshipInput").Text);
            Assert.Equal("erfolgshungrig", Field<ComboBox>(view, "FanTypeInput").Text);
            Assert.True(Field<TextBox>(view, "ChantInput").Enabled);
            Assert.Same(ShownClub(view), Field<JerseyView>(view, "JerseyEditor").Club);

            // The own club can't be its own rival.
            ComboBox rival = Field<ComboBox>(view, "ArchRivalInput");
            Assert.Equal(new[] { "–", "VfL Osnabrück", "FC Hannover 66" }, Enumerable.Range(0, rival.Items.Count).Select(i => rival.GetItemText(rival.Items[i])));
        });

        [Fact]
        public void IdentityTab_EditsReachTheClub() => WithIdentityTab((fake, view) =>
        {
            ComboBox friendship = Field<ComboBox>(view, "FanFriendshipInput");
            friendship.SelectedIndex = 1;
            typeof(ComboBox).GetMethod("OnSelectionChangeCommitted", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(friendship, new object[] { EventArgs.Empty });
            Assert.Equal((byte)0, ShownClub(view).FanFriendshipId);

            // Each edit is written back like on leaving the field (every write re-reads all bindings).
            Field<ComboBox>(view, "BoardInput").SelectedValue = ClubEnums.Board.Pulverfass;
            Field<ComboBox>(view, "BoardInput").DataBindings[0].WriteValue();
            Field<TextBox>(view, "ChantInput").Text = "Eintracht";
            Field<TextBox>(view, "ChantInput").DataBindings[0].WriteValue();
            Field<ComboBox>(view, "FanTypeInput").SelectedValue = ClubEnums.FanType.Euphorisch;
            Field<ComboBox>(view, "FanTypeInput").DataBindings[0].WriteValue();
            Assert.Equal(ClubEnums.Board.Pulverfass, ShownClub(view).Board);
            Assert.Equal("Eintracht", ShownClub(view).Chant);
            Assert.Equal(ClubEnums.FanType.Euphorisch, ShownClub(view).FanType);
        });

        private static void Click(Control control, Point at, MouseButtons button) =>
            typeof(Control).GetMethod("OnMouseDown", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(control, new object[] { new MouseEventArgs(button, 1, at.X, at.Y, 0) });

        [Fact]
        public void JerseyTab_ClicksCycleTheColours_AndThePatternButtonsSetThePattern() => WithIdentityTab((fake, view) =>
        {
            JerseyView jerseys = Field<JerseyView>(view, "JerseyEditor");
            Club club = ShownClub(view);
            int changes = 0;
            jerseys.JerseyChanged += (s, e) => changes++;

            // Seeded: 0x42 (Schulter, gelb), blau, gelb | blau, gelb, blau.
            Assert.True(jerseys.PatternButton(0, ClubEnums.ShirtPattern.Schulter).Checked);
            Assert.True(jerseys.PatternButton(1, ClubEnums.ShirtPattern.Neutral).Checked);

            Click(jerseys, jerseys.GarmentCentre(0, JerseyView.Garment.Shirt), MouseButtons.Left);
            Assert.Equal(ClubEnums.JerseyColor.Blau, club.GetShirtColor(0));
            Assert.Equal(ClubEnums.ShirtPattern.Schulter, club.GetShirtPattern(0));
            Click(jerseys, jerseys.GarmentCentre(0, JerseyView.Garment.Shorts), MouseButtons.Right);
            Assert.Equal(ClubEnums.JerseyColor.Gelb, club.GetShortsColor(0));
            Click(jerseys, jerseys.GarmentCentre(1, JerseyView.Garment.Socks), MouseButtons.Left);
            Assert.Equal(ClubEnums.JerseyColor.Hellgruen, club.GetSocksColor(1));
            Click(jerseys, new Point(1, 1), MouseButtons.Left);
            Click(jerseys, jerseys.GarmentCentre(1, JerseyView.Garment.Shirt), MouseButtons.Middle);

            jerseys.PatternButton(1, ClubEnums.ShirtPattern.Brustring).Checked = true;
            Assert.Equal(ClubEnums.ShirtPattern.Brustring, club.GetShirtPattern(1));
            Assert.Equal(4, changes);

            // Weinrot wraps round to weiß, and back.
            club.SetShortsColor(1, ClubEnums.JerseyColor.Weinrot);
            jerseys.Cycle(1, JerseyView.Garment.Shorts, 1);
            Assert.Equal(ClubEnums.JerseyColor.Weiss, club.GetShortsColor(1));
            jerseys.Cycle(1, JerseyView.Garment.Shorts, -1);
            Assert.Equal(ClubEnums.JerseyColor.Weinrot, club.GetShortsColor(1));
        }, tab: "JerseyTab");

        [Fact]
        public void JerseyView_DrawsEveryPattern_AndWithoutClubDoesNothing() => StaThread.Run(() =>
        {
            using (JerseyView view = new JerseyView { Size = new Size(574, 334) })
            using (Bitmap bitmap = new Bitmap(574, 334))
            {
                view.DrawToBitmap(bitmap, new Rectangle(0, 0, 574, 334));
                view.Cycle(0, JerseyView.Garment.Shirt, 1);
                view.PatternButton(0, ClubEnums.ShirtPattern.Quer).Checked = true;

                Club club = new Club { Jersey = new byte[] { 0, 11, 0, 0x0D, 1, 1 } };
                view.Club = club;
                foreach (ClubEnums.ShirtPattern pattern in Enum.GetValues(typeof(ClubEnums.ShirtPattern)))
                {
                    club.SetShirtPattern(0, pattern);
                    view.DrawToBitmap(bitmap, new Rectangle(0, 0, 574, 334));
                    Assert.Equal(pattern == ClubEnums.ShirtPattern.Neutral || pattern == ClubEnums.ShirtPattern.AermelWieHose, !JerseyView.PatternBands(pattern).Any());
                }
                Assert.Equal(Color.Gray, JerseyView.ToColor((ClubEnums.JerseyColor)13));   // the second shirt's unknown colour

                // The shirt centre of the first set is shirt, the shorts' colour paints Brustring.
                club.SetShirtPattern(0, ClubEnums.ShirtPattern.Neutral);
                view.DrawToBitmap(bitmap, new Rectangle(0, 0, 574, 334));
                Point shirt = view.GarmentCentre(0, JerseyView.Garment.Shirt);
                Assert.Equal(JerseyView.Colors[0].ToArgb(), bitmap.GetPixel(shirt.X, shirt.Y).ToArgb());
                Assert.Null(view.GarmentAt(new Point(view.Width / 2, 2)));

                typeof(Control).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { new MouseEventArgs(MouseButtons.None, 0, shirt.X, shirt.Y, 0) });
                Assert.Equal(Cursors.Hand, view.Cursor);
                typeof(Control).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { new MouseEventArgs(MouseButtons.None, 0, 1, 1, 0) });
                Assert.Equal(Cursors.Default, view.Cursor);
            }
        });

        [Theory]
        [InlineData(PlayerEnums.Country.Deutschland)]
        [InlineData(PlayerEnums.Country.England)]
        [InlineData(PlayerEnums.Country.Frankreich)]
        [InlineData(PlayerEnums.Country.Italien)]
        [InlineData(PlayerEnums.Country.Spanien)]
        public void CountryMap_EveryLeagueCountryHasAMapWithInnerBorders(PlayerEnums.Country country)
        {
            CountryMap map = CountryMap.For(country);

            Assert.NotNull(map);
            Assert.Same(map, CountryMap.For(country));
            Assert.Equal(440, map.Width);
            Assert.Equal(320, map.Height);
            // Germany has one level of inner borders only (the Bundesländer).
            foreach (CountryMap.Layer layer in Enum.GetValues(typeof(CountryMap.Layer)).Cast<CountryMap.Layer>()
                .Where(l => country != PlayerEnums.Country.Deutschland || l != CountryMap.Layer.Subregion))
            {
                Assert.True(map.TryGetLayer(layer, out List<PointF[]> shapes), layer.ToString());
                Assert.NotEmpty(shapes);
            }
            // The country's land covers the middle of the editor's picture.
            map.TryGetLayer(CountryMap.Layer.Land, out List<PointF[]> land);
            RectangleF bounds = RectangleF.FromLTRB(land.SelectMany(s => s).Min(p => p.X), land.SelectMany(s => s).Min(p => p.Y), land.SelectMany(s => s).Max(p => p.X), land.SelectMany(s => s).Max(p => p.Y));
            Assert.True(bounds.Contains(220, 160), bounds.ToString());
        }

        [Fact]
        public void CountryMap_OtherCountriesHaveNone_AndOtherDataIsRejected()
        {
            Assert.Null(CountryMap.For(PlayerEnums.Country.Belgien));
            Assert.Null(CountryMap.For(PlayerEnums.Country.Belgien));
            Assert.Throws<System.IO.InvalidDataException>(() => CountryMap.Read(new System.IO.MemoryStream(Encoding.ASCII.GetBytes("A2GM\u0002...."))));
            Assert.Throws<System.IO.InvalidDataException>(() => CountryMap.Read(new System.IO.MemoryStream(Encoding.ASCII.GetBytes("XXXX\u0001...."))));
        }

        [Fact]
        public void ClubMap_WithACountryMap_ShowsTheEditorsWholePicture() => StaThread.Run(() =>
        {
            using (ClubMap map = new ClubMap { Size = new Size(440, 334) })
            using (Bitmap bitmap = new Bitmap(440, 334))
            {
                map.Club = new Club { Country = PlayerEnums.Country.Frankreich, MapX = 200, MapY = 150, ClubName = "Paris" };

                Assert.NotNull(map.Map);
                Assert.Equal(new PointF(0, 7), map.ToScreen(0, 0));
                Assert.Equal(new PointF(440, 327), map.ToScreen(440, 320));
                map.DrawToBitmap(bitmap, new Rectangle(0, 0, 440, 334));
                Assert.NotEqual(map.BackColor.ToArgb(), bitmap.GetPixel(2, 2).ToArgb());

                map.Map = null;
                Assert.Null(map.Map);
            }
        });

        [Fact]
        public void IdentityTab_WithoutRecord_DisablesTheRecordTexts_AndKeepsAnUnknownRival() => WithIdentityTab((fake, view) =>
        {
            Assert.False(Field<TextBox>(view, "ShortNameInput").Enabled);
            Assert.False(Field<TextBox>(view, "ChantInput").Enabled);
            Assert.False(Field<TextBox>(view, "FanNameInput").Enabled);
            Assert.Equal("Verein 2", Field<ComboBox>(view, "ArchRivalInput").Text);
        }, withRecord: false);

        [Fact]
        public void MapTab_ClickAndInputsMoveTheClub() => WithIdentityTab((fake, view) =>
        {
            ClubMap map = Field<ClubMap>(view, "ClubMapView");
            TextBox x = Field<TextBox>(view, "MapXInput");
            TextBox y = Field<TextBox>(view, "MapYInput");
            Assert.Equal("294", x.Text);
            Assert.Equal("111", y.Text);
            Assert.Equal(3, map.Clubs.Count);

            PointF osnabrueck = map.ToScreen(233, 112);
            typeof(Control).GetMethod("OnMouseClick", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(map, new object[] { new MouseEventArgs(MouseButtons.Left, 1, (int)Math.Round(osnabrueck.X), (int)Math.Round(osnabrueck.Y), 0) });
            Assert.InRange(ShownClub(view).MapX, 231, 235);
            Assert.InRange(ShownClub(view).MapY, 110, 114);
            Assert.Equal(ShownClub(view).MapX.ToString(), x.Text);

            x.Text = "250";
            y.Text = "-40000";
            Assert.Equal((short)250, ShownClub(view).MapX);
            Assert.Equal(short.MinValue, ShownClub(view).MapY);
        }, tab: "MapTab");

        [Fact]
        public void ClubMap_MapsScreenAndMapPositionsBothWays_AndFindsClubsUnderTheMouse() => StaThread.Run(() =>
        {
            using (ClubMap map = new ClubMap { Size = new Size(400, 300) })
            {
                map.Clubs = new List<ClubRecords.Entry>
                {
                    new ClubRecords.Entry { Id = 0, Name = "A", MapX = 100, MapY = 100 },
                    new ClubRecords.Entry { Id = 1, Name = "B", MapX = 300, MapY = 200 },
                    new ClubRecords.Entry { Id = 2, Name = "C", MapX = 200, MapY = 150 }
                };
                map.Club = new Club { Id = 2, MapX = 200, MapY = 150, ArchRivalId = 0, FanFriendshipId = 1, ClubName = "C" };

                PointF a = map.ToScreen(100, 100), b = map.ToScreen(300, 200);
                Assert.Equal(new Point(100, 100), map.ToMap(Point.Round(a)));
                Assert.Equal(new Point(300, 200), map.ToMap(Point.Round(b)));
                Assert.Equal(b.X - a.X, (b.Y - a.Y) * 2, 1);   // the same scale both ways
                Assert.Equal("B", map.ClubAt(Point.Round(b)).Name);
                Assert.Null(map.ClubAt(Point.Round(map.ToScreen(200, 150))));   // the own club isn't one of the others

                bool moved = false;
                map.PositionChanged += (s, e) => moved = true;
                typeof(Control).GetMethod("OnMouseClick", BindingFlags.NonPublic | BindingFlags.Instance)
                    .Invoke(map, new object[] { new MouseEventArgs(MouseButtons.Right, 1, 10, 10, 0) });
                Assert.False(moved);

                using (Bitmap bitmap = new Bitmap(400, 300))
                {
                    map.DrawToBitmap(bitmap, new Rectangle(0, 0, 400, 300));
                    map.Clubs = null;
                    map.Club = null;
                    map.DrawToBitmap(bitmap, new Rectangle(0, 0, 400, 300));
                }
                Assert.Empty(map.Clubs);
                Assert.Equal(new Point(0, 0), map.ToMap(Point.Round(map.ToScreen(0, 0))));
            }
        });

        [Fact]
        public void ClubMap_HoverShowsAndClearsTheClubName() => StaThread.Run(() =>
        {
            using (ClubMap map = new ClubMap { Size = new Size(400, 300) })
            {
                map.Clubs = new List<ClubRecords.Entry>
                {
                    new ClubRecords.Entry { Id = 0, Name = "A", MapX = 100, MapY = 100 },
                    new ClubRecords.Entry { Id = 1, Name = "B", MapX = 300, MapY = 200 }
                };
                map.Club = new Club { Id = 5, MapX = 200, MapY = 150, ClubName = "Own" };
                Point a = Point.Round(map.ToScreen(100, 100));
                MethodInfo move = typeof(Control).GetMethod("OnMouseMove", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo leave = typeof(Control).GetMethod("OnMouseLeave", BindingFlags.NonPublic | BindingFlags.Instance);

                move.Invoke(map, new object[] { new MouseEventArgs(MouseButtons.None, 0, a.X, a.Y, 0) });
                Assert.Equal("A", Field<ClubRecords.Entry>(map, "hovered").Name);
                using (Bitmap bitmap = new Bitmap(400, 300))
                    map.DrawToBitmap(bitmap, new Rectangle(0, 0, 400, 300));
                leave.Invoke(map, new object[] { EventArgs.Empty });
                Assert.Null(Field<ClubRecords.Entry>(map, "hovered"));
                leave.Invoke(map, new object[] { EventArgs.Empty });
            }
        });
        #endregion
    }
}
