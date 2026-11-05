using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
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
    /// Tests for editing any club's data: the working copy/record mapping (ClubRecords.Segments,
    /// Image, RecordField), the record-based ClubController, and the club selection of the club view.
    /// </summary>
    public class ClubDataTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");
        private static Addresses Own => AddressPresets.OWN_CLUB;

        private static uint Record(int index) => Settings.ClubRecordTableOffset + (uint)index * Settings.ClubRecordStride;
        private static uint Hex(ClubEnums.AddressKey key) => Convert.ToUInt32(Own[key], 16);
        // Working-copy offset -> record offset, by the game's fetch.
        private static uint RecordOffset(ClubEnums.AddressKey key) => ClubRecords.RecordField(0, Hex(key), 1).Value;

        // Deutschland is league system 0 with clubs 0..2 from record 0; Arsenal is England's record 3.
        private static void WriteClub(FakeModule fake, int index, string name, PlayerEnums.Country country, byte id, int wealth)
        {
            fake.WriteModuleBytes(Record(index), Latin1.GetBytes(name + "\0"));
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordIdOffset, new[] { id });
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordCountryOffset, new[] { (byte)country });
            fake.WriteModuleBytes(Record(index) + RecordOffset(ClubEnums.AddressKey.Wealth), BitConverter.GetBytes(wealth));
        }

        private static FakeModule NewFake(ProcessMemory memory = null)
        {
            FakeModule fake = new FakeModule(memory: memory);
            fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + (uint)PlayerEnums.Country.Deutschland, new byte[] { 0 });
            fake.WriteModuleBytes(Settings.LeagueClubCountTableOffset, BitConverter.GetBytes((short)3));
            fake.WriteModuleBytes(Settings.LeagueBaseTableOffset, BitConverter.GetBytes((short)0));
            WriteClub(fake, 0, "Bayern", PlayerEnums.Country.Deutschland, 0, 5000);
            WriteClub(fake, 1, "Eintracht", PlayerEnums.Country.Deutschland, 1, 1000);
            WriteClub(fake, 3, "Arsenal", PlayerEnums.Country.England, 0, 7000);
            return fake;
        }

        // A cached working copy of the club with the given parts fetched; returns its module offset.
        private static uint CacheClub(FakeModule fake, int slot, PlayerEnums.Country country, byte id, ushort parts, uint copy)
        {
            byte[] entry = new byte[Settings.ClubCacheStride];
            entry[0] = 1;
            entry[1] = (byte)country;
            entry[2] = id;
            BitConverter.GetBytes(parts).CopyTo(entry, (int)Settings.ClubCachePartsOffset);
            BitConverter.GetBytes(fake.ModuleBase + copy).CopyTo(entry, (int)Settings.ClubCachePointerOffset);
            fake.WriteModuleBytes(Settings.ClubCacheTableOffset + (uint)slot * Settings.ClubCacheStride, entry);
            return copy;
        }

        private static ClubSquads.Entry Entry(FakeModule fake, string name) =>
            ClubSquads.Clubs(fake.Memory, FixedAddresses.For(fake.Memory)).Single(c => c.Name == name);

        #region Mapping
        [Fact]
        public void RecordField_FollowsTheGamesFetch()
        {
            Assert.Equal(0x94u, RecordOffset(ClubEnums.AddressKey.Respect));
            Assert.Equal(0xA0u, RecordOffset(ClubEnums.AddressKey.Will2Win));
            Assert.Equal(0xA2u, RecordOffset(ClubEnums.AddressKey.Spirit));
            Assert.Equal(0x198u, RecordOffset(ClubEnums.AddressKey.TeamCohesion));
            Assert.Equal(0xA3u, RecordOffset(ClubEnums.AddressKey.Pressing));
            Assert.Equal(0xA4u, RecordOffset(ClubEnums.AddressKey.OffsideTrap));
            Assert.Equal(0xA5u, RecordOffset(ClubEnums.AddressKey.AntiOffsideTrap));
            Assert.Equal(0xA6u, RecordOffset(ClubEnums.AddressKey.TeamSkills));
            Assert.Equal(0x82u, RecordOffset(ClubEnums.AddressKey.CoTrainer));
            Assert.Equal(0x84u, RecordOffset(ClubEnums.AddressKey.GoalkeeperCoach));
            Assert.Equal(0x5D4u, RecordOffset(ClubEnums.AddressKey.FreeTickets));
            Assert.Equal(0x1A4u, RecordOffset(ClubEnums.AddressKey.Wealth));
            Assert.Equal(0x4ECu, RecordOffset(ClubEnums.AddressKey.StadiumName));
            // The editor settings land where ClubRecords already mirrors them.
            foreach (ClubEnums.AddressKey key in Settings.ClubRecordMirrorKeys)
                Assert.Equal(Settings.ClubRecordMirrorOffset(key), RecordOffset(key));
            // Outside the record (the opponent's name) or across a segment's end: nothing.
            Assert.Null(ClubRecords.RecordField(0, Hex(ClubEnums.AddressKey.OpponentName), 1));
            Assert.Null(ClubRecords.RecordField(0, 0x8E, 4));
        }

        [Fact]
        public void Image_TakesEachPartFromTheWorkingCopyOnlyIfItIsFetched()
        {
            using (FakeModule fake = NewFake())
            {
                fake.WriteModuleBytes(Record(3) + RecordOffset(ClubEnums.AddressKey.StadiumName), Latin1.GetBytes("Highbury\0"));
                const uint copy = 0x100000;
                CacheClub(fake, 2, PlayerEnums.Country.England, 0, 0x100, copy);
                fake.WriteModuleBytes(copy + Hex(ClubEnums.AddressKey.Wealth), BitConverter.GetBytes(7500));
                fake.WriteModuleBytes(copy + Hex(ClubEnums.AddressKey.StadiumName), Latin1.GetBytes("Leer\0"));

                byte[] image = ClubRecords.Image(fake.Memory, FixedAddresses.For(fake.Memory), 3, PlayerEnums.Country.England, 0);

                Assert.Equal(7500, BitConverter.ToInt32(image, (int)Hex(ClubEnums.AddressKey.Wealth)));
                Assert.Equal("Highbury", Latin1.GetString(image, (int)Hex(ClubEnums.AddressKey.StadiumName), 8));
                Assert.Equal("Arsenal", Latin1.GetString(image, 0, 7));
                Assert.Equal((byte)PlayerEnums.Country.England, image[Hex(ClubEnums.AddressKey.COUNTRY)]);
                Assert.Equal(ClubRecords.WorkingCopyLength, ClubRecords.Image(fake.Memory, null, 3, PlayerEnums.Country.England, 0).Length);
            }
        }
        #endregion

        #region ClubController from the record
        [Fact]
        public void RecordClub_ReadsTheClubFromItsRecord()
        {
            using (FakeModule fake = NewFake())
            {
                fake.WriteModuleBytes(Record(1) + RecordOffset(ClubEnums.AddressKey.Respect), new byte[] { 77 });
                fake.WriteModuleBytes(Record(1) + RecordOffset(ClubEnums.AddressKey.StadiumName), Latin1.GetBytes("Eintracht-Stadion\0"));
                fake.WriteModuleBytes(Record(1) + Settings.ClubRecordShortNameOffset, Latin1.GetBytes("EBS\0"));

                ClubController controller = new ClubController(fake.Memory, isGog: false, Entry(fake, "Eintracht"));
                Club club = controller.Club;

                Assert.Equal(PlayerEnums.AddressType.CLUB, controller.Type);
                Assert.Equal("Eintracht", club.ClubName);
                Assert.Equal((byte)1, club.Id);
                Assert.Equal(PlayerEnums.Country.Deutschland, club.Country);
                Assert.Equal(1000, club.Wealth);
                Assert.Equal((byte)77, club.Respect);
                Assert.Equal("Eintracht-Stadion", club.StadiumName);
                Assert.True(club.HasRecord);
                Assert.Equal("EBS", club.ShortName);
                Assert.Equal(3, controller.CountryClubs.Count);
            }
        }

        [Fact]
        public void RecordClub_Save_WritesTheRecordAndEveryWorkingCopy_NotTheClubPointer()
        {
            using (FakeModule fake = NewFake())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                const uint copy = 0x100000;
                CacheClub(fake, 3, PlayerEnums.Country.England, 0, 0x180, copy);
                ClubController controller = new ClubController(fake.Memory, isGog: false, Entry(fake, "Arsenal"));
                controller.Club.Wealth = 123456;
                controller.Club.StadiumName = "Emirates";
                controller.Club.Will2Win = 9;
                controller.Club.MapX = 321;
                controller.Club.Spirit = 70;
                controller.Club.Pressing = 4;
                controller.Club.OffsideTrap = 150;
                controller.Club.Corners = 5;

                controller.Save();

                // The tactics reach the record; the morale (one byte) leaves the pressing behind it alone.
                Assert.Equal(new byte[] { 70, 4, 150 }, fake.ReadModuleBytes(Record(3) + RecordOffset(ClubEnums.AddressKey.Spirit), 3));
                Assert.Equal((ushort)(5 << 3), BitConverter.ToUInt16(fake.ReadModuleBytes(Record(3) + RecordOffset(ClubEnums.AddressKey.TeamSkills), 2), 0));
                Assert.Equal((byte)4, fake.ReadModuleBytes(copy + Hex(ClubEnums.AddressKey.Pressing), 1)[0]);

                Assert.Equal(123456, BitConverter.ToInt32(fake.ReadModuleBytes(Record(3) + RecordOffset(ClubEnums.AddressKey.Wealth), 4), 0));
                Assert.Equal(123456, BitConverter.ToInt32(fake.ReadModuleBytes(copy + Hex(ClubEnums.AddressKey.Wealth), 4), 0));
                Assert.Equal("Emirates", Latin1.GetString(fake.ReadModuleBytes(Record(3) + RecordOffset(ClubEnums.AddressKey.StadiumName), 8)));
                Assert.Equal((byte)9, fake.ReadModuleBytes(Record(3) + RecordOffset(ClubEnums.AddressKey.Will2Win), 1)[0]);
                Assert.Equal(321, BitConverter.ToInt16(fake.ReadModuleBytes(Record(3) + Settings.ClubRecordMirrorOffset(ClubEnums.AddressKey.MapX), 2), 0));
                Assert.Equal(321, BitConverter.ToInt16(fake.ReadModuleBytes(copy + Hex(ClubEnums.AddressKey.MapX), 2), 0));
                // The club pointer belongs to the own club and stays untouched.
                Assert.Equal(0, BitConverter.ToInt32(fake.ReadDisplayCacheBytes(Own[ClubEnums.AddressKey.Wealth], 4), 0));
            }
        }

        [Fact]
        public void OwnClub_Save_WritesTheRecordToo()
        {
            using (FakeModule fake = NewFake())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.NAME], Latin1.GetBytes("Eintracht\0"));
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.ID], new byte[] { 1 });
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.COUNTRY], new[] { (byte)PlayerEnums.Country.Deutschland });
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                controller.Club.Wealth = 4242;

                controller.Save();

                Assert.Equal(4242, BitConverter.ToInt32(fake.ReadDisplayCacheBytes(Own[ClubEnums.AddressKey.Wealth], 4), 0));
                Assert.Equal(4242, BitConverter.ToInt32(fake.ReadModuleBytes(Record(1) + RecordOffset(ClubEnums.AddressKey.Wealth), 4), 0));
            }
        }
        #endregion

        #region Club view
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void InvokeHandler(object target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, new object[] { null, EventArgs.Empty });

        [Fact]
        public void ClubView_ShowsAndSwitchesClubs() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = NewFake(trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.NAME], Latin1.GetBytes("Eintracht\0"));
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.ID], new byte[] { 1 });
                fake.WriteDisplayCacheBytes(Own[ClubEnums.AddressKey.COUNTRY], new[] { (byte)PlayerEnums.Country.Deutschland });
                ClubView view = trainer.ClubView;
                ComboBox country = Field<ComboBox>(view, "ClubCountryInput");
                ComboBox club = Field<ComboBox>(view, "ClubInput");

                // The own club through the club pointer, selected below the tabs.
                InvokeHandler(trainer, "OwnClubToolStripMenuItem_Click");
                view.InitClubTabControl();
                Assert.Null(view.ClubController.RecordClub);
                Assert.Equal("Eintracht", club.Text);

                // Another club comes from its record.
                country.SelectedValue = PlayerEnums.Country.England;
                Assert.Equal("Arsenal", view.ClubController.RecordClub.Name);
                Assert.Equal(7000, view.ClubController.Club.Wealth);
                Assert.EndsWith(":: Vereinsdaten: Arsenal", trainer.Text);

                // Reload and "Verein -> Anderer" keep the club picked last.
                InvokeHandler(view, "ReloadBtn_Click");
                Assert.Equal("Arsenal", view.ClubController.RecordClub.Name);
                InvokeHandler(trainer, "OwnClubToolStripMenuItem_Click");
                Assert.Null(view.ClubController.RecordClub);
                InvokeHandler(trainer, "OtherClubDataToolStripMenuItem_Click");
                Assert.Equal("Arsenal", view.ClubController.RecordClub.Name);

                // Saving writes the record and shows the club again.
                view.ClubController.Club.Wealth = 8000;
                InvokeHandler(view, "SaveBtn_Click");
                Assert.Equal(8000, BitConverter.ToInt32(fake.ReadModuleBytes(Record(3) + RecordOffset(ClubEnums.AddressKey.Wealth), 4), 0));
                Assert.Equal("Arsenal", club.Text);
            }
        });
        #endregion
    }
}
