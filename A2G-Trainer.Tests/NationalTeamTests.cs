using AnstossTrainer.Shared;
using AnstossTrainer.Shared.Tests;
using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// Tests for the national coach (Coach.NationalTeam and its contract, written to every copy the
    /// game keeps of it) and the national squads (NationalSquads), plus the coach view's
    /// "Nationalmannschaft" tab.
    /// </summary>
    public class NationalTeamTests
    {
        // Big enough for the national coach table (+81DAB0) and the whole player record table.
        private const int ModuleSize = 0x820000;

        private static uint RecordOffset(int trainerSlot, uint fieldOffset) =>
            Settings.CoachRecordTableOffset + (uint)(trainerSlot + 1) * Settings.CoachRecordStride + fieldOffset;

        private static uint EntryOffset(int trainerSlot) =>
            Settings.NationalCoachTableOffset + (uint)(trainerSlot + 1) * Settings.NationalCoachEntryStride;

        private static uint TeamOffset(PlayerEnums.Country country) =>
            Settings.NationalTeamRecordTableOffset + (uint)country * Settings.ClubRecordStride;

        private static FakeModule NewFake(ProcessMemory memory = null) => new FakeModule(ModuleSize, memory);

        // The state the game leaves behind for a Mitspieler who trains this national team (FUN_005cf1e0).
        private static void MakeNationalCoach(FakeModule fake, int trainerSlot, PlayerEnums.Country country, int salary = 0, int bonus = 0)
        {
            byte id = (byte)(trainerSlot + 1);
            fake.WriteModuleBytes(RecordOffset(trainerSlot, Settings.CoachRecordNationalTeamOffset), BitConverter.GetBytes((ushort)country));
            fake.WriteModuleBytes(Settings.CoachSlotTableOffset + id * Settings.CoachSlotStride + Settings.CoachSlotNationalTeamOffset, BitConverter.GetBytes((ushort)country));
            fake.WriteModuleBytes(RecordOffset(trainerSlot, Settings.CoachRecordNationalSalaryOffset), BitConverter.GetBytes(salary));
            fake.WriteModuleBytes(RecordOffset(trainerSlot, Settings.CoachRecordNationalBonusOffset), BitConverter.GetBytes(bonus));
            fake.WriteModuleBytes(EntryOffset(trainerSlot), new byte[] { (byte)country, 0, 200, 0 });
            fake.WriteModuleBytes(TeamOffset(country) + Settings.NationalTeamCoachPostOffset, new byte[] { id, id });
        }

        // A working copy of the national team in the club cache (club ID 200); returns its module offset.
        private static uint CacheNationalTeam(FakeModule fake, int slot, PlayerEnums.Country country, uint copy)
        {
            byte[] entry = new byte[Settings.ClubCacheStride];
            entry[0] = 1;
            entry[1] = (byte)country;
            entry[2] = 200;
            BitConverter.GetBytes(fake.ModuleBase + copy).CopyTo(entry, (int)Settings.ClubCachePointerOffset);
            fake.WriteModuleBytes(Settings.ClubCacheTableOffset + (uint)slot * Settings.ClubCacheStride, entry);
            return copy;
        }

        private static ushort ReadUInt16(FakeModule fake, uint offset) => BitConverter.ToUInt16(fake.ReadModuleBytes(offset, 2), 0);
        private static int ReadInt32(FakeModule fake, uint offset) => BitConverter.ToInt32(fake.ReadModuleBytes(offset, 4), 0);

        // The friendly block of Mitspieler 1: chosen opponents at +0x36, results at +0x48.
        private static uint FriendlyBlock => Settings.NationalFriendlyBlockOffset + 1 * Settings.NationalFriendlyBlockStride;

        [Fact]
        public void Friendlies_AreReadAndOnlyTheOpenOnesWritten()
        {
            using (FakeModule fake = NewFake())
            {
                MakeNationalCoach(fake, 0, PlayerEnums.Country.Holland);
                // Friendly 1 played; 2 open (Belgien away); 3 open (Dänemark at home). Season day 100:
                // friendly 1 (day 101) would be locked anyway, 2 and 3 are far enough away.
                fake.WriteModuleBytes(FriendlyBlock + Settings.NationalFriendlyChosenOffset, new byte[]
                {
                    (byte)PlayerEnums.Country.Schweiz, 0, 200, 0, 0, 0,
                    (byte)PlayerEnums.Country.Belgien, 0, 200, 0, 1, 0,
                    (byte)PlayerEnums.Country.Dänemark, 0, 200, 0, 0, 0
                });
                fake.WriteModuleBytes(FriendlyBlock + Settings.NationalFriendlyResultsOffset, new byte[] { 0x21, 0, 0xFF, 0, 0xFF, 0 });
                fake.WriteModuleBytes(Settings.GlobalCounterOffset, BitConverter.GetBytes((short)100));
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                Coach coach = controller.Coach;

                Assert.Equal(3, coach.Friendlies.Count);
                Assert.True(coach.Friendlies[0].IsLocked);
                Assert.Equal(PlayerEnums.Country.Belgien, coach.Friendlies[1].Opponent);
                Assert.False(coach.Friendlies[1].AtHome);
                Assert.False(coach.Friendlies[1].IsLocked);
                Assert.True(coach.Friendlies[2].AtHome);
                Assert.Equal((short)220, coach.Friendlies[2].Day);

                coach.Friendlies[0].Opponent = PlayerEnums.Country.Italien;
                coach.Friendlies[1].Opponent = PlayerEnums.Country.Spanien;
                coach.Friendlies[1].AtHome = true;
                coach.Friendlies[2].Opponent = PlayerEnums.Country.Sonstige;
                controller.Save();

                Assert.Equal(new byte[]
                {
                    (byte)PlayerEnums.Country.Schweiz, 0, 200, 0, 0, 0,
                    (byte)PlayerEnums.Country.Spanien, 0, 200, 0, 0, 0,
                    0, 0, 0, 0, 0, 0
                }, fake.ReadModuleBytes(FriendlyBlock + Settings.NationalFriendlyChosenOffset, 18));
            }
        }

        [Fact]
        public void Friendlies_WithoutANationalTeam_AreEmpty()
        {
            using (FakeModule fake = NewFake())
            {
                Assert.Empty(new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach.Friendlies);
            }
        }

        [Fact]
        public void GetEntity_ReadsTheNationalTeamAndContractOfItsOwnMitspieler()
        {
            using (FakeModule fake = NewFake())
            {
                MakeNationalCoach(fake, 0, PlayerEnums.Country.Holland, 5, 6);
                MakeNationalCoach(fake, 1, PlayerEnums.Country.Italien, 1000000, 250000);

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 1).Coach;

                Assert.True(coach.HasNationalTeam);
                Assert.True(coach.IsNationalCoach);
                Assert.Equal(PlayerEnums.Country.Italien, coach.NationalTeam);
                Assert.Equal(1000000, coach.NationalSalary);
                Assert.Equal(250000, coach.NationalRoundBonus);
            }
        }

        [Fact]
        public void Save_ChangingTheNationalTeam_MovesTheCoachPostsAndUpdatesEveryCopy()
        {
            using (FakeModule fake = NewFake())
            {
                MakeNationalCoach(fake, 1, PlayerEnums.Country.Italien, 1000000, 250000);
                uint copy = CacheNationalTeam(fake, 2, PlayerEnums.Country.Spanien, 0x500000);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 1);
                controller.Coach.NationalTeam = PlayerEnums.Country.Spanien;
                controller.Coach.NationalSalary = 2000000;

                controller.Save();

                Assert.Equal(new byte[] { 0, 0 }, fake.ReadModuleBytes(TeamOffset(PlayerEnums.Country.Italien) + Settings.NationalTeamCoachPostOffset, 2));
                Assert.Equal(new byte[] { 2, 2 }, fake.ReadModuleBytes(TeamOffset(PlayerEnums.Country.Spanien) + Settings.NationalTeamCoachPostOffset, 2));
                Assert.Equal(new byte[] { 2, 2 }, fake.ReadModuleBytes(copy + Settings.NationalTeamCoachPostOffset, 2));
                Assert.Equal(new byte[] { 63, 0, 200, 0 }, fake.ReadModuleBytes(EntryOffset(1), 4));
                Assert.Equal((ushort)63, ReadUInt16(fake, RecordOffset(1, Settings.CoachRecordNationalTeamOffset)));
                Assert.Equal((ushort)63, ReadUInt16(fake, Settings.CoachSlotTableOffset + 2 * Settings.CoachSlotStride + Settings.CoachSlotNationalTeamOffset));
                Assert.Equal(2000000, ReadInt32(fake, RecordOffset(1, Settings.CoachRecordNationalSalaryOffset)));
                Assert.Equal(250000, ReadInt32(fake, RecordOffset(1, Settings.CoachRecordNationalBonusOffset)));
            }
        }

        [Fact]
        public void Save_LeavingTheNationalTeam_ClearsTheJobAndTheContract()
        {
            using (FakeModule fake = NewFake())
            {
                MakeNationalCoach(fake, 0, PlayerEnums.Country.Italien, 1000000, 250000);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                controller.Coach.NationalTeam = PlayerEnums.Country.Sonstige;

                controller.Save();

                Assert.False(controller.Coach.IsNationalCoach);
                Assert.Equal(new byte[] { 0, 0 }, fake.ReadModuleBytes(TeamOffset(PlayerEnums.Country.Italien) + Settings.NationalTeamCoachPostOffset, 2));
                Assert.Equal(new byte[4], fake.ReadModuleBytes(EntryOffset(0), 4));
                Assert.Equal((ushort)0, ReadUInt16(fake, RecordOffset(0, Settings.CoachRecordNationalTeamOffset)));
                Assert.Equal(0, ReadInt32(fake, RecordOffset(0, Settings.CoachRecordNationalSalaryOffset)));
                Assert.Equal(0, ReadInt32(fake, RecordOffset(0, Settings.CoachRecordNationalBonusOffset)));
            }
        }

        [Fact]
        public void Save_KeepingTheNationalTeam_OnlyWritesTheContract()
        {
            using (FakeModule fake = NewFake())
            {
                MakeNationalCoach(fake, 0, PlayerEnums.Country.Italien, 1000000, 250000);
                // A post the game set differently must stay as it is when the team doesn't change.
                fake.WriteModuleBytes(TeamOffset(PlayerEnums.Country.Italien) + Settings.NationalTeamCoachPostOffset, new byte[] { 1, 0 });
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                controller.Coach.NationalRoundBonus = 300000;

                controller.Save();

                Assert.Equal(new byte[] { 1, 0 }, fake.ReadModuleBytes(TeamOffset(PlayerEnums.Country.Italien) + Settings.NationalTeamCoachPostOffset, 2));
                Assert.Equal(300000, ReadInt32(fake, RecordOffset(0, Settings.CoachRecordNationalBonusOffset)));
                Assert.Equal(1000000, ReadInt32(fake, RecordOffset(0, Settings.CoachRecordNationalSalaryOffset)));
            }
        }

        [Fact]
        public void WithoutFixedAddresses_TheNationalTeamIsNeitherReadNorWritten()
        {
            using (FakeModule fake = NewFake())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                MakeNationalCoach(fake, 0, PlayerEnums.Country.Italien, 1000000, 0);

                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                Assert.False(controller.Coach.HasNationalTeam);
                Assert.Equal(PlayerEnums.Country.Sonstige, controller.Coach.NationalTeam);
                Assert.Equal(0, controller.Coach.NationalSalary);

                controller.Coach.NationalTeam = PlayerEnums.Country.Spanien;
                controller.Save();

                Assert.Equal((ushort)28, ReadUInt16(fake, RecordOffset(0, Settings.CoachRecordNationalTeamOffset)));
                Assert.Empty(CoachController.NationalTeamsOfOtherTrainers(fake.Memory, 1));
            }
        }

        [Fact]
        public void NationalTeamsOfOtherTrainers_ListsTheActiveOthersOnly()
        {
            using (FakeModule fake = NewFake())
            {
                fake.WriteModuleBytes(Settings.ActiveTrainerCountOffset, new byte[] { 3 });
                MakeNationalCoach(fake, 0, PlayerEnums.Country.Italien);
                MakeNationalCoach(fake, 1, PlayerEnums.Country.Holland);
                MakeNationalCoach(fake, 3, PlayerEnums.Country.Brasilien);
                // An entry without the national team's club ID isn't a national team.
                fake.WriteModuleBytes(EntryOffset(2), new byte[] { 14, 0, 5, 0 });

                Assert.Equal(new[] { PlayerEnums.Country.Holland }, CoachController.NationalTeamsOfOtherTrainers(fake.Memory, 0));
                Assert.Equal(new[] { PlayerEnums.Country.Italien, PlayerEnums.Country.Holland }, CoachController.NationalTeamsOfOtherTrainers(fake.Memory, 2));
            }
        }

        [Fact]
        public void NationalTeam_RaisesPropertyChangedForIsNationalCoach()
        {
            Coach coach = new Coach();
            List<string> changed = new List<string>();
            coach.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            coach.NationalTeam = PlayerEnums.Country.Italien;
            coach.NationalSalary = 1;
            coach.NationalRoundBonus = 2;
            coach.HasNationalTeam = true;

            Assert.Equal(new[] { nameof(Coach.NationalTeam), nameof(Coach.IsNationalCoach), nameof(Coach.NationalSalary), nameof(Coach.NationalRoundBonus), nameof(Coach.HasNationalTeam) }, changed);
            Assert.True(coach.IsNationalCoach);
        }

        [Fact]
        public void NationalTeamChoices_StartWithNoneAndLeaveOutTakenTeams()
        {
            List<PlayerEnums.Country> choices = CoachView.NationalTeamChoices(new[] { PlayerEnums.Country.Italien });

            Assert.Equal(PlayerEnums.Country.Sonstige, choices[0]);
            Assert.DoesNotContain(PlayerEnums.Country.Italien, choices);
            Assert.Equal(75, choices.Count);
            Assert.Equal(PlayerEnums.Country.Aegypten, choices[1]);
            Assert.Equal(PlayerEnums.Country.Zypern, choices.Last());
        }

        // A used player record with the given nationality, position, strength and birth year.
        private static void WritePlayer(FakeModule fake, ushort id, PlayerEnums.Country nationality, PlayerEnums.Position position, byte level, ushort birthYear = 1970, byte nationalityHighBit = 0)
        {
            uint record = Settings.PlayerRecordTableOffset + id * Settings.PlayerRecordStride;
            fake.WriteModuleBytes(record, BitConverter.GetBytes((ushort)1));
            fake.WriteModuleBytes(record + Settings.PlayerRecordAgeOffset, BitConverter.GetBytes(birthYear));
            fake.WriteModuleBytes(record + Settings.PlayerRecordLevelOffset, new byte[] { level, (byte)position });
            fake.WriteModuleBytes(record + Settings.PlayerRecordNationalityOffset, new byte[] { (byte)((byte)nationality | nationalityHighBit) });
        }

        private static void WriteSquad(FakeModule fake, PlayerEnums.Country country, params ushort[] ids)
        {
            fake.WriteModuleBytes(TeamOffset(country) + Settings.ClubRecordSquadCountOffset, BitConverter.GetBytes((short)ids.Length));
            byte[] list = new byte[ids.Length * 2];
            for (int i = 0; i < ids.Length; i++)
                BitConverter.GetBytes(ids[i]).CopyTo(list, i * 2);
            fake.WriteModuleBytes(TeamOffset(country) + Settings.ClubRecordSquadOffset, list);
        }

        [Fact]
        public void SquadIds_ReadsTheRecordsSquadAndCapsTheCount()
        {
            using (FakeModule fake = new FakeModule())
            {
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                WriteSquad(fake, PlayerEnums.Country.Italien, 10, 11, 12);

                Assert.Equal(new ushort[] { 10, 11, 12 }, NationalSquads.SquadIds(fake.Memory, fixedAddresses, PlayerEnums.Country.Italien));

                fake.WriteModuleBytes(TeamOffset(PlayerEnums.Country.Italien) + Settings.ClubRecordSquadCountOffset, BitConverter.GetBytes((short)99));
                Assert.Equal(Settings.NationalTeamSquadCapacity, NationalSquads.SquadIds(fake.Memory, fixedAddresses, PlayerEnums.Country.Italien).Count);
                Assert.Empty(NationalSquads.SquadIds(fake.Memory, fixedAddresses, PlayerEnums.Country.Sonstige));
                Assert.Empty(NationalSquads.SquadIds(fake.Memory, null, PlayerEnums.Country.Italien));
            }
        }

        [Fact]
        public void PlayersOfNationality_ListsUsedRecordsOfThatCountryStrongestFirst()
        {
            using (FakeModule fake = NewFake())
            {
                fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes((ushort)1998));
                WritePlayer(fake, 5, PlayerEnums.Country.Italien, PlayerEnums.Position.S, 9, 1974);
                WritePlayer(fake, 6, PlayerEnums.Country.Italien, PlayerEnums.Position.TO, 11, 1970, nationalityHighBit: 0x80);
                WritePlayer(fake, 7, PlayerEnums.Country.Spanien, PlayerEnums.Position.L, 12);
                // A record without name indices is unused, whatever else it holds.
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset + 8 * Settings.PlayerRecordStride + Settings.PlayerRecordNationalityOffset, new byte[] { 28 });

                List<NationalSquads.Member> players = NationalSquads.PlayersOfNationality(fake.Memory, FixedAddresses.For(fake.Memory), PlayerEnums.Country.Italien);

                Assert.Equal(new ushort[] { 6, 5 }, players.Select(p => p.Id));
                Assert.Equal(PlayerEnums.Position.TO, players[0].Position);
                Assert.Equal(28, players[0].Age);
                Assert.Equal("Spieler 6", players[0].Name);
                Assert.Equal("Spieler 6 (TO, 11, 28 J.)", players[0].ToString());
                Assert.Empty(NationalSquads.PlayersOfNationality(fake.Memory, null, PlayerEnums.Country.Italien));
                Assert.Empty(NationalSquads.PlayersOfNationality(fake.Memory, FixedAddresses.For(fake.Memory), PlayerEnums.Country.Sonstige));
            }
        }

        [Fact]
        public void Members_KeepsTheOrderAndSkipsIdsOutsideTheRecords()
        {
            using (FakeModule fake = new FakeModule())
            {
                WritePlayer(fake, 5, PlayerEnums.Country.Italien, PlayerEnums.Position.S, 9);
                WritePlayer(fake, 6, PlayerEnums.Country.Italien, PlayerEnums.Position.TO, 11);

                List<NationalSquads.Member> members = NationalSquads.Members(fake.Memory, FixedAddresses.For(fake.Memory), new ushort[] { 5, 60000, 6 });

                Assert.Equal(new ushort[] { 5, 6 }, members.Select(m => m.Id));
                Assert.Empty(NationalSquads.Members(fake.Memory, null, new ushort[] { 5 }));
            }
        }

        [Fact]
        public void Write_SortsByPositionAndRefusesWhileTheGameHasTheTeamFetched()
        {
            using (FakeModule fake = new FakeModule())
            {
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                WriteSquad(fake, PlayerEnums.Country.Italien, 1, 2, 3, 4);
                List<NationalSquads.Member> squad = new List<NationalSquads.Member>
                {
                    new NationalSquads.Member { Id = 20, Position = PlayerEnums.Position.S },
                    new NationalSquads.Member { Id = 21, Position = PlayerEnums.Position.TO },
                    new NationalSquads.Member { Id = 22, Position = PlayerEnums.Position.MD }
                };

                Assert.Equal(NationalSquads.WriteResult.Written, NationalSquads.Write(fake.Memory, fixedAddresses, PlayerEnums.Country.Italien, squad));
                Assert.Equal(new ushort[] { 21, 22, 20 }, NationalSquads.SquadIds(fake.Memory, fixedAddresses, PlayerEnums.Country.Italien));
                // The slot behind the squad is cleared, not left with an old ID.
                Assert.Equal((ushort)0, ReadUInt16(fake, TeamOffset(PlayerEnums.Country.Italien) + Settings.ClubRecordSquadOffset + 6));

                CacheNationalTeam(fake, 0, PlayerEnums.Country.Italien, 0x500000);
                Assert.Equal(NationalSquads.WriteResult.Fetched, NationalSquads.Write(fake.Memory, fixedAddresses, PlayerEnums.Country.Italien, squad.Take(1).ToList()));
                Assert.Equal(3, NationalSquads.SquadIds(fake.Memory, fixedAddresses, PlayerEnums.Country.Italien).Count);

                List<NationalSquads.Member> tooMany = Enumerable.Range(0, 23).Select(i => new NationalSquads.Member { Id = (ushort)i }).ToList();
                Assert.Equal(NationalSquads.WriteResult.TooMany, NationalSquads.Write(fake.Memory, fixedAddresses, PlayerEnums.Country.Spanien, tooMany));
                Assert.Equal(NationalSquads.WriteResult.NotSupported, NationalSquads.Write(fake.Memory, null, PlayerEnums.Country.Spanien, squad));
                Assert.Equal(NationalSquads.WriteResult.NotSupported, NationalSquads.Write(fake.Memory, fixedAddresses, PlayerEnums.Country.Sonstige, squad));
            }
        }

        [Theory]
        [InlineData((int)NationalSquads.WriteResult.Written, "Kader von Italien gespeichert.")]
        [InlineData((int)NationalSquads.WriteResult.Fetched, "Das Spiel bearbeitet die Mannschaft gerade – später erneut speichern.")]
        [InlineData((int)NationalSquads.WriteResult.TooMany, "Höchstens 22 Spieler.")]
        [InlineData((int)NationalSquads.WriteResult.NotSupported, "Für diese Spielversion nicht verfügbar.")]
        public void NationalSquadMessage_DescribesTheResult(int result, string expected)
        {
            Assert.Equal(expected, CoachView.NationalSquadMessage((NationalSquads.WriteResult)result, "Italien"));
        }

        private static T Field<T>(CoachView view, string name) =>
            (T)typeof(CoachView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        // PerformClick does nothing while the form isn't shown.
        private static void Click(Button button) =>
            typeof(Button).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(button, new object[] { EventArgs.Empty });

        [Fact]
        public void CoachView_NationalTab_EditsTheCoachAndTheSquad() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = NewFake(trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteModuleBytes(Settings.ActiveTrainerCountOffset, new byte[] { 2 });
                MakeNationalCoach(fake, 0, PlayerEnums.Country.Italien, 1000000, 250000);
                MakeNationalCoach(fake, 1, PlayerEnums.Country.Holland);
                WritePlayer(fake, 5, PlayerEnums.Country.Italien, PlayerEnums.Position.S, 9);
                WritePlayer(fake, 6, PlayerEnums.Country.Italien, PlayerEnums.Position.TO, 11);
                WriteSquad(fake, PlayerEnums.Country.Italien, 5);

                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                ComboBox team = Field<ComboBox>(trainer.CoachView, "NationalTeamInput");
                Assert.Equal(PlayerEnums.Country.Italien, team.SelectedValue);
                Assert.DoesNotContain(PlayerEnums.Country.Holland, team.Items.Cast<object>().Select(i => (PlayerEnums.Country)i.GetType().GetProperty("Value").GetValue(i)));
                Assert.Equal(nameof(Coach.NationalSalary), Field<TextBox>(trainer.CoachView, "NationalSalaryInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.True(Field<TextBox>(trainer.CoachView, "NationalSalaryInput").Enabled);

                ListBox squad = Field<ListBox>(trainer.CoachView, "NationalSquadList");
                ListBox candidates = Field<ListBox>(trainer.CoachView, "NationalCandidatesList");
                Assert.Equal(PlayerEnums.Country.Italien, Field<ComboBox>(trainer.CoachView, "NationalSquadCountryInput").SelectedValue);
                Assert.Equal(new ushort[] { 5 }, squad.Items.Cast<NationalSquads.Member>().Select(m => m.Id));
                Assert.Equal(new ushort[] { 6 }, candidates.Items.Cast<NationalSquads.Member>().Select(m => m.Id));
                Assert.Equal("Kader (1/22)", Field<Label>(trainer.CoachView, "NationalSquadLabel").Text);

                candidates.SelectedIndex = 0;
                Click(Field<Button>(trainer.CoachView, "NationalSquadAddBtn"));
                Assert.Equal(2, squad.Items.Count);
                squad.SelectedIndex = 0;
                Click(Field<Button>(trainer.CoachView, "NationalSquadRemoveBtn"));
                Assert.Equal(new ushort[] { 6 }, squad.Items.Cast<NationalSquads.Member>().Select(m => m.Id));

                Click(Field<Button>(trainer.CoachView, "NationalSquadSaveBtn"));
                Assert.Equal(new ushort[] { 6 }, NationalSquads.SquadIds(fake.Memory, FixedAddresses.For(fake.Memory), PlayerEnums.Country.Italien));
                Assert.Equal("Kader von Italien gespeichert.", Field<Label>(trainer.CoachView, "NationalSquadStatus").Text);

                team.SelectedValue = PlayerEnums.Country.Sonstige;
                Assert.False(Field<TextBox>(trainer.CoachView, "NationalSalaryInput").Enabled);
                typeof(CoachView).GetMethod("SaveBtn_Click", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(trainer.CoachView, new object[] { null, EventArgs.Empty });
                Assert.Equal((ushort)0, ReadUInt16(fake, RecordOffset(0, Settings.CoachRecordNationalTeamOffset)));
                // After the reload the squad box stays on the country it showed.
                Assert.Equal(PlayerEnums.Country.Italien, Field<ComboBox>(trainer.CoachView, "NationalSquadCountryInput").SelectedValue);

                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN);
                Assert.False(Field<GroupBox>(trainer.CoachView, "NationalCoachBox").Enabled);
                Assert.False(Field<GroupBox>(trainer.CoachView, "NationalSquadBox").Enabled);
            }
        });
    }
}
