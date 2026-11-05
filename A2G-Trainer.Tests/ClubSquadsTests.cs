using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
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
    /// Tests for listing any club's squad and youth from the records (ClubSquads, the record-based
    /// PlayerController), writing to the record and the game's working copy, and the club selection
    /// above the player list (PlayerView, Trainer menu "Anderer Verein").
    /// </summary>
    public class ClubSquadsTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");
        private const ushort CurrentYear = 2001;
        private static Addresses Own => AddressPresets.OWN_PLAYERS;

        private static uint Record(int index) => Settings.ClubRecordTableOffset + (uint)index * Settings.ClubRecordStride;
        private static uint PlayerRecord(ushort id) => Settings.PlayerRecordTableOffset + id * Settings.PlayerRecordStride;
        private static uint CacheSlot(int slot) => Settings.PlayerCacheTableOffset + (uint)slot * Settings.PlayerCacheStride;
        private static byte[] Ids(params ushort[] ids) => ids.SelectMany(BitConverter.GetBytes).ToArray();

        private static void WriteClub(FakeModule fake, int index, string name, PlayerEnums.Country country, byte id, ushort[] squad, ushort[] amateurs = null)
        {
            fake.WriteModuleBytes(Record(index), Latin1.GetBytes(name + "\0"));
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordIdOffset, new[] { id });
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordCountryOffset, new[] { (byte)country });
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordSquadCountOffset, BitConverter.GetBytes((short)squad.Length));
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordSquadOffset, Ids(squad));
            amateurs = amateurs ?? new ushort[0];
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordAmateurCountOffset, new[] { (byte)amateurs.Length });
            fake.WriteModuleBytes(Record(index) + Settings.ClubRecordAmateursOffset, Ids(amateurs));
        }

        // Youth entry of a club record index: counts at +1..+3, A/B/C IDs from +4/+0xC/+0x14.
        private static void WriteYouth(FakeModule fake, int index, ushort[] a, ushort[] b = null, ushort[] c = null)
        {
            byte[] entry = new byte[Settings.YouthStride];
            ushort[][] groups = { a, b ?? new ushort[0], c ?? new ushort[0] };
            for (int g = 0; g < 3; g++)
            {
                entry[1 + g] = (byte)groups[g].Length;
                Ids(groups[g]).CopyTo(entry, (int)Settings.YouthIdOffsets[g]);
            }
            fake.WriteModuleBytes(Settings.YouthTableOffset + (uint)index * Settings.YouthStride, entry);
        }

        private static void WritePlayer(FakeModule fake, ushort id, byte level) =>
            fake.WriteModuleBytes(PlayerRecord(id) + Settings.PlayerRecordLevelOffset, new[] { level });

        // A working copy in the game's player cache; returns its absolute address.
        private static uint WriteWorkingCopy(FakeModule fake, int slot, ushort id)
        {
            fake.WriteModuleBytes(CacheSlot(slot), new byte[] { 1, 1, 0, 0x80 });
            fake.WriteModuleBytes(CacheSlot(slot) + Settings.PlayerCacheCopyOffset, BitConverter.GetBytes(id));
            return fake.ModuleBase + CacheSlot(slot) + Settings.PlayerCacheCopyOffset;
        }

        private static byte ReadCopy(FakeModule fake, uint copy, PlayerEnums.AddressKey key) =>
            fake.Memory.ReadBytesAtAddress(copy + Convert.ToUInt32(Own[key], 16), 1)[0];

        // The original build's layout without the name pool, which lies beyond the fake module:
        // names then come from the working copy.
        private static FakeModule NewFake(ProcessMemory memory = null)
        {
            FakeModule fake = new FakeModule(memory: memory);
            fake.Memory.Layout = new PersistentLayout(Settings.PlayerRecordTableOffset, null, Settings.AgeReferenceYearOffset, Settings.ActiveTrainerCountOffset, isVerified: true, hasOriginalBuildAnchors: true);
            fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes(CurrentYear));
            return fake;
        }

        private static ClubSquads.Entry Club(FakeModule fake, string name) =>
            ClubSquads.Clubs(fake.Memory, FixedAddresses.For(fake.Memory)).Single(c => c.Name == name);

        #region ClubSquads
        [Fact]
        public void Clubs_ListsEveryRecordWithANameAndACountry()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 0, "Bayern", PlayerEnums.Country.Deutschland, 0, new ushort[0]);
                WriteClub(fake, 2, "Arsenal", PlayerEnums.Country.England, 4, new ushort[0]);
                // A record without country (an unchosen country's slot) and a nameless one are left out.
                fake.WriteModuleBytes(Record(3), Latin1.GetBytes("Leer\0"));
                fake.WriteModuleBytes(Record(4) + Settings.ClubRecordCountryOffset, new[] { (byte)PlayerEnums.Country.Italien });

                List<ClubSquads.Entry> clubs = ClubSquads.Clubs(fake.Memory, FixedAddresses.For(fake.Memory));

                Assert.Equal(new[] { "Bayern", "Arsenal" }, clubs.Select(c => c.ToString()));
                Assert.Equal(2, clubs[1].Index);
                Assert.True(clubs[1].Is(PlayerEnums.Country.England, 4));
                Assert.False(clubs[1].Is(PlayerEnums.Country.England, 0));
            }
        }

        [Fact]
        public void WithoutFixedAddresses_NothingIsListed()
        {
            using (FakeModule fake = NewFake())
            {
                ClubSquads.Entry club = new ClubSquads.Entry();
                Assert.Empty(ClubSquads.Clubs(fake.Memory, null));
                Assert.Empty(ClubSquads.SquadIds(fake.Memory, null, club));
                Assert.Empty(ClubSquads.YouthIds(fake.Memory, null, club));
                Assert.Empty(ClubSquads.PlayerWorkingCopies(fake.Memory, null));
                Assert.Null(ClubSquads.PlayerWorkingCopy(fake.Memory, null, 1));
            }
        }

        [Fact]
        public void SquadIds_FromTheRecord_SquadThenAmateurs_CappedAtTheGamesRoom()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 1, "Eintracht", PlayerEnums.Country.Deutschland, 1, new ushort[] { 20, 21 }, new ushort[] { 22 });

                Assert.Equal(new ushort[] { 20, 21, 22 }, ClubSquads.SquadIds(fake.Memory, FixedAddresses.For(fake.Memory), Club(fake, "Eintracht")));

                fake.WriteModuleBytes(Record(1) + Settings.ClubRecordSquadCountOffset, BitConverter.GetBytes((short)-1));
                fake.WriteModuleBytes(Record(1) + Settings.ClubRecordAmateurCountOffset, new byte[] { 200 });
                Assert.Equal(Settings.ClubAmateurCapacity, ClubSquads.SquadIds(fake.Memory, FixedAddresses.For(fake.Memory), Club(fake, "Eintracht")).Count);
            }
        }

        [Fact]
        public void SquadIds_OfAFetchedClub_ComeFromItsWorkingCopy()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 1, "Eintracht", PlayerEnums.Country.Deutschland, 1, new ushort[] { 20 });
                uint player1 = WriteWorkingCopy(fake, 3, 25);
                uint player2 = WriteWorkingCopy(fake, 4, 26);
                const uint copy = 0x100000;
                fake.WriteModuleBytes(copy + Settings.ClubSquadCountOffset, BitConverter.GetBytes((short)1));
                fake.WriteModuleBytes(copy + Settings.ClubSquadOffset, BitConverter.GetBytes(player1));
                fake.WriteModuleBytes(copy + Settings.ClubAmateurCountOffset, new byte[] { 1 });
                fake.WriteModuleBytes(copy + Settings.ClubAmateursOffset, BitConverter.GetBytes(player2));
                byte[] slot = new byte[Settings.ClubCacheStride];
                slot[0] = 1;
                slot[1] = (byte)PlayerEnums.Country.Deutschland;
                slot[2] = 1;
                BitConverter.GetBytes(fake.ModuleBase + copy).CopyTo(slot, (int)Settings.ClubCachePointerOffset);
                fake.WriteModuleBytes(Settings.ClubCacheTableOffset + Settings.ClubCacheStride, slot);

                Assert.Equal(new ushort[] { 25, 26 }, ClubSquads.SquadIds(fake.Memory, FixedAddresses.For(fake.Memory), Club(fake, "Eintracht")));
            }
        }

        [Fact]
        public void YouthIds_ListTheAThenBThenCYouth_CappedAtTheGamesRoom()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 2, "Arsenal", PlayerEnums.Country.England, 0, new ushort[0]);
                WriteYouth(fake, 2, new ushort[] { 31, 32 }, new ushort[] { 33 }, new ushort[] { 34, 35, 36 });

                Assert.Equal(new ushort[] { 31, 32, 33, 34, 35, 36 }, ClubSquads.YouthIds(fake.Memory, FixedAddresses.For(fake.Memory), Club(fake, "Arsenal")));

                fake.WriteModuleBytes(Settings.YouthTableOffset + 2 * Settings.YouthStride + 3, new byte[] { 9 });
                Assert.Equal(6, ClubSquads.YouthIds(fake.Memory, FixedAddresses.For(fake.Memory), Club(fake, "Arsenal")).Count);
            }
        }

        [Theory]
        [InlineData(14, 2)]
        [InlineData(15, 1)]
        [InlineData(16, 1)]
        [InlineData(17, 0)]
        [InlineData(19, 0)]
        public void YouthGroup_FollowsTheGamesAgeLimits(int age, int group)
        {
            Assert.Equal(group, ClubSquads.YouthGroup(age));
        }

        private static byte[] YouthEntry(FakeModule fake, int index) =>
            fake.ReadModuleBytes(Settings.YouthTableOffset + (uint)index * Settings.YouthStride, (int)Settings.YouthStride);

        [Fact]
        public void MoveYouthToAgeGroup_MovesThePlayerAndClosesTheGap()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 2, "Arsenal", PlayerEnums.Country.England, 0, new ushort[0]);
                WriteYouth(fake, 2, new ushort[] { 31 }, new ushort[] { 32 }, new ushort[] { 33, 34, 35 });
                ClubSquads.Entry club = Club(fake, "Arsenal");
                FixedAddresses addresses = FixedAddresses.For(fake.Memory);

                Assert.Equal(ClubSquads.YouthMove.Moved, ClubSquads.MoveYouthToAgeGroup(fake.Memory, addresses, club, 33, 17, true, out int from, out int to));
                Assert.Equal(2, from);
                Assert.Equal(0, to);
                Assert.Equal(new ushort[] { 31, 33, 32, 34, 35 }, ClubSquads.YouthIds(fake.Memory, addresses, club));
                byte[] entry = YouthEntry(fake, 2);
                Assert.Equal(new byte[] { 2, 1, 2 }, entry.Skip(1).Take(3));
                Assert.Equal(0, BitConverter.ToUInt16(entry, 0x14 + 2 * 2));

                Assert.Equal(ClubSquads.YouthMove.Unchanged, ClubSquads.MoveYouthToAgeGroup(fake.Memory, addresses, club, 33, 18, true, out _, out _));
                Assert.Equal(ClubSquads.YouthMove.NotFound, ClubSquads.MoveYouthToAgeGroup(fake.Memory, addresses, club, 99, 17, true, out _, out _));
                Assert.Equal(ClubSquads.YouthMove.NotFound, ClubSquads.MoveYouthToAgeGroup(fake.Memory, null, club, 33, 17, true, out _, out _));
            }
        }

        [Fact]
        public void MoveYouthToAgeGroup_WithoutAgeChange_OnlyMovesUp()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 2, "Arsenal", PlayerEnums.Country.England, 0, new ushort[0]);
                // A 16-year-old the season change put in the A youth, a 17-year-old still in the C youth.
                WriteYouth(fake, 2, new ushort[] { 31 }, c: new ushort[] { 33 });
                ClubSquads.Entry club = Club(fake, "Arsenal");
                FixedAddresses addresses = FixedAddresses.For(fake.Memory);

                Assert.Equal(ClubSquads.YouthMove.Unchanged, ClubSquads.MoveYouthToAgeGroup(fake.Memory, addresses, club, 31, 16, false, out _, out _));
                Assert.Equal(ClubSquads.YouthMove.Moved, ClubSquads.MoveYouthToAgeGroup(fake.Memory, addresses, club, 33, 17, false, out _, out _));
                Assert.Equal(ClubSquads.YouthMove.Moved, ClubSquads.MoveYouthToAgeGroup(fake.Memory, addresses, club, 31, 16, true, out _, out _));
                Assert.Equal(new ushort[] { 33, 31 }, ClubSquads.YouthIds(fake.Memory, addresses, club));
            }
        }

        [Fact]
        public void MoveYouthToAgeGroup_IntoAFullGroup_LeavesThePlayer()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 2, "Arsenal", PlayerEnums.Country.England, 0, new ushort[0]);
                WriteYouth(fake, 2, new ushort[] { 31, 32, 33, 34 }, c: new ushort[] { 35 });
                byte[] before = YouthEntry(fake, 2);

                Assert.Equal(ClubSquads.YouthMove.GroupFull, ClubSquads.MoveYouthToAgeGroup(fake.Memory, FixedAddresses.For(fake.Memory), Club(fake, "Arsenal"), 35, 17, true, out int from, out int to));
                Assert.Equal(2, from);
                Assert.Equal(0, to);
                Assert.Equal(before, YouthEntry(fake, 2));
            }
        }

        [Fact]
        public void Save_ForAYouthPlayerMadeOlder_MovesHimIntoTheAYouth_OrSaysWhyNot()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 2, "Arsenal", PlayerEnums.Country.England, 0, new ushort[0]);
                WriteYouth(fake, 2, new ushort[] { 31, 32, 33 }, c: new ushort[] { 35, 36 });
                foreach (ushort id in new ushort[] { 31, 32, 33 })
                    fake.WriteModuleBytes(PlayerRecord(id) + Settings.PlayerRecordAgeOffset, BitConverter.GetBytes((ushort)(CurrentYear - 17)));
                foreach (ushort id in new ushort[] { 35, 36 })
                    fake.WriteModuleBytes(PlayerRecord(id) + Settings.PlayerRecordAgeOffset, BitConverter.GetBytes((ushort)(CurrentYear - 14)));
                PlayerController controller = new PlayerController(fake.Memory, Club(fake, "Arsenal"), isGog: false, PlayerEnums.AddressType.TRAINEE);
                Player first = controller.EntityList.Single(p => p.NameRecordId == 35);
                Player second = controller.EntityList.Single(p => p.NameRecordId == 36);
                first.Age = 17;
                second.Age = 17;

                Assert.Empty(controller.Save(first));
                List<string> warnings = controller.Save(second);

                Assert.Equal(new ushort[] { 31, 32, 33, 35, 36 }, ClubSquads.YouthIds(fake.Memory, FixedAddresses.For(fake.Memory), Club(fake, "Arsenal")));
                Assert.Equal(new byte[] { 4, 0, 1 }, YouthEntry(fake, 2).Skip(1).Take(3));
                Assert.Single(warnings);
                Assert.Contains("Die A-Jugend ist voll, er bleibt in der C-Jugend.", warnings[0]);
                Assert.Equal((ushort)(CurrentYear - 17), BitConverter.ToUInt16(fake.ReadModuleBytes(PlayerRecord(36) + Settings.PlayerRecordAgeOffset, 2), 0));
            }
        }

        [Fact]
        public void PlayerWorkingCopies_FindTheSlotsInUse()
        {
            using (FakeModule fake = NewFake())
            {
                uint copy = WriteWorkingCopy(fake, 7, 42);
                // A freed slot still holding an ID doesn't count.
                fake.WriteModuleBytes(CacheSlot(8) + Settings.PlayerCacheCopyOffset, BitConverter.GetBytes((ushort)43));

                Assert.Equal(copy, ClubSquads.PlayerWorkingCopy(fake.Memory, FixedAddresses.For(fake.Memory), 42));
                Assert.Null(ClubSquads.PlayerWorkingCopy(fake.Memory, FixedAddresses.For(fake.Memory), 43));
            }
        }
        #endregion

        #region PlayerController from the records
        [Fact]
        public void RecordList_ReadsThePlayersFromTheRecords_WithTheClubAndAnyWorkingCopy()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 2, "Arsenal", PlayerEnums.Country.England, 4, new ushort[] { 30, 31 });
                WritePlayer(fake, 30, 12);
                WritePlayer(fake, 31, 13);
                uint copy = WriteWorkingCopy(fake, 0, 31);
                fake.Memory.WriteBytesAtAddress(copy + Convert.ToUInt32(Own[PlayerEnums.AddressKey.FIRSTNAME], 16), Latin1.GetBytes("Thierry\0"));

                PlayerController controller = new PlayerController(fake.Memory, Club(fake, "Arsenal"), isGog: false, PlayerEnums.AddressType.CLUB);

                Assert.Equal(new byte[] { 12, 13 }, controller.EntityList.Select(p => p.Level));
                Assert.Equal(new ushort[] { 30, 31 }, controller.EntityList.Select(p => p.NameRecordId));
                Assert.All(controller.EntityList, p => Assert.Equal(PlayerEnums.Country.England, p.ClubCountry));
                Assert.All(controller.EntityList, p => Assert.Equal((ushort)4, p.ClubId));
                Assert.Null(controller.EntityList[0].WorkingCopy);
                Assert.Equal(copy, controller.EntityList[1].WorkingCopy);
                // Without a name pool the name comes from the working copy, or stays empty.
                Assert.Equal("", controller.EntityList[0].Firstname);
                Assert.Equal("Thierry", controller.EntityList[1].Firstname);
                Assert.True(controller.IsStillLoaded(controller.EntityList[0]));
            }
        }

        [Fact]
        public void RecordList_WithoutAClub_IsEmpty()
        {
            using (FakeModule fake = NewFake())
            {
                PlayerController controller = new PlayerController(fake.Memory, (ClubSquads.Entry)null, isGog: false, PlayerEnums.AddressType.CLUB);

                Assert.Empty(controller.EntityList);
                Assert.Null(controller.RecordClub);
            }
        }

        [Fact]
        public void Save_WritesTheRecordAndTheWorkingCopyTheGameHoldsNow()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 2, "Arsenal", PlayerEnums.Country.England, 4, new ushort[] { 30, 31 });
                uint oldCopy = WriteWorkingCopy(fake, 0, 30);
                PlayerController controller = new PlayerController(fake.Memory, Club(fake, "Arsenal"), isGog: false, PlayerEnums.AddressType.CLUB);
                Player kept = controller.EntityList[0];
                Player fetched = controller.EntityList[1];

                // Meanwhile the game frees player 30's copy and fetches player 31.
                fake.WriteModuleBytes(CacheSlot(0), new byte[] { 0 });
                uint newCopy = WriteWorkingCopy(fake, 5, 31);
                kept.Level = 40;
                fetched.Level = 41;
                fetched.Firstname = "Max";
                controller.SaveEntityList();

                Assert.Equal((byte)40, fake.ReadModuleBytes(PlayerRecord(30) + Settings.PlayerRecordLevelOffset, 1)[0]);
                Assert.Equal((byte)41, fake.ReadModuleBytes(PlayerRecord(31) + Settings.PlayerRecordLevelOffset, 1)[0]);
                Assert.Equal((byte)0, ReadCopy(fake, oldCopy, PlayerEnums.AddressKey.LEVEL));
                Assert.Equal((byte)41, ReadCopy(fake, newCopy, PlayerEnums.AddressKey.LEVEL));
                Assert.Equal("Max", fake.Memory.ReadStringAtAddress(newCopy + Convert.ToUInt32(Own[PlayerEnums.AddressKey.FIRSTNAME], 16), 9, Latin1));
            }
        }

        [Fact]
        public void SaveFields_WritesConditionAndFreshnessToTheRecordAndAnyWorkingCopy()
        {
            using (FakeModule fake = NewFake())
            {
                WriteClub(fake, 2, "Arsenal", PlayerEnums.Country.England, 4, new ushort[] { 30, 31 });
                uint copy = WriteWorkingCopy(fake, 0, 31);
                PlayerController controller = new PlayerController(fake.Memory, Club(fake, "Arsenal"), isGog: false, PlayerEnums.AddressType.CLUB);
                List<PlayerEnums.AddressKey> keys = new List<PlayerEnums.AddressKey> { PlayerEnums.AddressKey.CONDITION, PlayerEnums.AddressKey.FRESHNESS, PlayerEnums.AddressKey.LEVEL };
                foreach (Player player in controller.EntityList)
                {
                    player.Condition = 99;
                    player.Freshness = 98;
                    controller.Save(player, keys);
                }

                Assert.Equal(new byte[] { 99, 98 }, fake.ReadModuleBytes(PlayerRecord(30) + Settings.PlayerRecordConditionOffset, 2));
                Assert.Equal(new byte[] { 99, 98 }, fake.ReadModuleBytes(PlayerRecord(31) + Settings.PlayerRecordConditionOffset, 2));
                Assert.Equal((byte)99, ReadCopy(fake, copy, PlayerEnums.AddressKey.CONDITION));
                Assert.Equal((byte)98, ReadCopy(fake, copy, PlayerEnums.AddressKey.FRESHNESS));
            }
        }
        #endregion

        #region Club selection
        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void InvokeHandler(object target, string name) =>
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, new object[] { null, EventArgs.Empty });

        private static IEnumerable<ushort> Listed(Trainer trainer) => trainer.PlayerView.PlayerController.EntityList.Select(p => p.NameRecordId);

        // Own club Eintracht (Deutschland 1) read through the club pointer; Bayern and Arsenal besides.
        private static void SeedSavegame(FakeModule fake)
        {
            fake.RoutePointerSlot(Settings.ClubAddress[0]);
            fake.RoutePointerSlot(Settings.PlayerAddress[0]);
            fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.NAME], Latin1.GetBytes("Eintracht\0"));
            fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.ID], new byte[] { 1 });
            fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.COUNTRY], new[] { (byte)PlayerEnums.Country.Deutschland });

            WriteClub(fake, 0, "Bayern", PlayerEnums.Country.Deutschland, 0, new ushort[] { 10, 11 }, new ushort[] { 12 });
            WriteClub(fake, 1, "Eintracht", PlayerEnums.Country.Deutschland, 1, new ushort[] { 20 });
            WriteClub(fake, 2, "Arsenal", PlayerEnums.Country.England, 0, new ushort[] { 30 });
            WriteYouth(fake, 1, new ushort[] { 21 });
            WriteYouth(fake, 2, new ushort[0], c: new ushort[] { 31 });
        }

        [Fact]
        public void ClubSelection_ShowsAndSwitchesClubsAndYouth() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = NewFake(trainer.Memory))
            {
                SeedSavegame(fake);
                PlayerView view = trainer.PlayerView;
                ComboBox country = Field<ComboBox>(view, "ClubCountryInput");
                ComboBox club = Field<ComboBox>(view, "ClubInput");
                List<string> chosen = new List<string>();
                view.ClubChosen += (c, type) => chosen.Add($"{type} {c.Name}");

                // "Anderer Verein" with nothing picked yet shows the own club.
                InvokeHandler(trainer, "OtherClubToolStripMenuItem_Click");
                Assert.Equal(new ushort[] { 20 }, Listed(trainer));
                Assert.Equal(PlayerEnums.Country.Deutschland, country.SelectedValue);
                Assert.Equal("Eintracht", club.Text);
                Assert.Equal(new[] { "Bayern", "Eintracht" }, club.Items.Cast<object>().Select(o => o.ToString()));

                // Another country shows its first club, as "Verein".
                country.SelectedValue = PlayerEnums.Country.England;
                Assert.Equal(new ushort[] { 30 }, Listed(trainer));
                Assert.Equal(PlayerEnums.AddressType.CLUB, view.PlayerController.Type);
                Assert.EndsWith(":: Verein: Arsenal", trainer.Text);

                // Reloading keeps the club; picking the shown club again changes nothing.
                InvokeHandler(view, "ReloadBtn_Click");
                Assert.Equal(new ushort[] { 30 }, Listed(trainer));
                club.SelectedIndex = 0;
                Assert.Single(chosen);

                // The youth menu shows the own club's youth; picking a club keeps the youth list.
                InvokeHandler(trainer, "TraineeToolStripMenuItem_Click");
                Assert.Equal(new ushort[] { 21 }, Listed(trainer));
                Assert.Equal("Eintracht", club.Text);
                country.SelectedValue = PlayerEnums.Country.England;
                Assert.Equal(new ushort[] { 31 }, Listed(trainer));
                Assert.Equal(PlayerEnums.AddressType.TRAINEE, view.PlayerController.Type);
                Assert.EndsWith(":: Jugendspieler: Arsenal", trainer.Text);
                InvokeHandler(view, "ReloadBtn_Click");
                Assert.Equal(new ushort[] { 31 }, Listed(trainer));

                // "Anderer Verein" reopens the club picked last for the squad, Bayern from the own squad view.
                country.SelectedValue = PlayerEnums.Country.Deutschland;
                Assert.Empty(Listed(trainer));
                InvokeHandler(trainer, "OtherClubToolStripMenuItem_Click");
                Assert.Equal(new ushort[] { 30 }, Listed(trainer));
                InvokeHandler(trainer, "OwnPlayersToolStripMenuItem_Click");
                Assert.Empty(Listed(trainer));
                Assert.Equal("Eintracht", club.Text);
                club.SelectedIndex = 0;
                Assert.Equal(new ushort[] { 10, 11, 12 }, Listed(trainer));
                Assert.Equal(new[] { "CLUB Arsenal", "TRAINEE Arsenal", "TRAINEE Bayern", "CLUB Bayern" }, chosen);
            }
        });

        [Fact]
        public void ClubSelection_WithoutClubs_StaysEmpty() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = NewFake(trainer.Memory))
            {
                trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.CLUB);

                Assert.Empty(Listed(trainer));
                Assert.Equal(-1, Field<ComboBox>(trainer.PlayerView, "ClubCountryInput").SelectedIndex);
                Assert.Empty(Field<ComboBox>(trainer.PlayerView, "ClubInput").Items);
                Assert.False(trainer.PlayerView.Controls.Find("SaveBtn", true).Single().Enabled);
            }
        });
        #endregion
    }
}
