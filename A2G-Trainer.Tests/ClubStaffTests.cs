using System;
using System.Text;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for the co-trainer and goalkeeper trainer of a club (ClubController).</summary>
    public class ClubStaffTests
    {
        // The goalkeeper trainer table sits near module+0x7F31E8 and the name pool table near +0x7FE540.
        private const int ModuleSize = 0x810000;
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        private static uint CoTrainerEntry(ushort id) => Settings.CoachPoolTableOffset + id * Settings.StaffEntryStride;
        private static uint GoalkeeperCoachEntry(ushort id) => Settings.GoalkeeperCoachTableOffset + id * Settings.StaffEntryStride;

        private static void WriteEntry(FakeModule fake, uint entry, ushort first, ushort last, byte competence, byte contract, int salary)
        {
            fake.WriteModuleBytes(entry, BitConverter.GetBytes(first));
            fake.WriteModuleBytes(entry + 2, BitConverter.GetBytes(last));
            fake.WriteModuleBytes(entry + Settings.StaffCompetenceOffset, new[] { competence });
            fake.WriteModuleBytes(entry + 7, new byte[] { 44 });
            fake.WriteModuleBytes(entry + Settings.StaffContractOffset, new[] { contract });
            fake.WriteModuleBytes(entry + Settings.StaffSalaryOffset, BitConverter.GetBytes(salary));
        }

        // Name pool entries 0-3 through the game's table.
        private static void WriteNames(FakeModule fake, params string[] names)
        {
            const uint pool = 0x4F0000;
            fake.WriteModuleBytes(Settings.NamePoolPointerOffset, BitConverter.GetBytes(fake.ModuleBase + pool));
            fake.WriteModuleBytes(Settings.NamePoolCountOffset, BitConverter.GetBytes((ushort)names.Length));
            uint at = pool;
            for (int i = 0; i < names.Length; i++)
            {
                fake.WriteModuleBytes(Settings.NamePoolTableOffset + (uint)i * 4, BitConverter.GetBytes(fake.ModuleBase + at));
                byte[] text = Latin1.GetBytes(names[i] + "\0");
                fake.WriteModuleBytes(at, text);
                at += (uint)text.Length;
            }
            fake.WriteModuleBytes(Settings.NamePoolEndPointerOffset, BitConverter.GetBytes(fake.ModuleBase + at));
        }

        private static ushort NameIndex(FakeModule fake, uint entry, uint offset) => BitConverter.ToUInt16(fake.ReadModuleBytes(entry + offset, 2), 0);

        private static FakeModule OwnClubWithStaff(ushort coTrainer, ushort goalkeeperCoach)
        {
            FakeModule fake = new FakeModule(moduleBlockSize: ModuleSize);
            fake.RoutePointerSlot(Settings.ClubAddress[0]);
            Addresses own = AddressPresets.OWN_CLUB;
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 15 });
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.CoTrainer], BitConverter.GetBytes(coTrainer));
            fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.GoalkeeperCoach], BitConverter.GetBytes(goalkeeperCoach));
            WriteNames(fake, "", "Horst", "Hrubesch", "Sepp", "Maier");
            WriteEntry(fake, CoTrainerEntry(5), 1, 2, 9, 2, 300000);
            WriteEntry(fake, GoalkeeperCoachEntry(3), 3, 4, 12, 1, 85000);
            return fake;
        }

        [Fact]
        public void Read_ShowsBothTrainersFromTheirEntries()
        {
            using (FakeModule fake = OwnClubWithStaff(5, 3))
            {
                Club club = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN).Club;

                Assert.True(club.HasCoTrainer);
                Assert.Equal("Horst", club.CoTrainerFirstname);
                Assert.Equal("Hrubesch", club.CoTrainerLastname);
                Assert.Equal((byte)9, club.CoTrainerCompetence);
                Assert.Equal((byte)2, club.CoTrainerContract);
                Assert.Equal(300000, club.CoTrainerSalary);
                Assert.True(club.HasGoalkeeperCoach);
                Assert.Equal("Sepp", club.GoalkeeperCoachFirstname);
                Assert.Equal("Maier", club.GoalkeeperCoachLastname);
                Assert.Equal((byte)12, club.GoalkeeperCoachCompetence);
                Assert.Equal((byte)1, club.GoalkeeperCoachContract);
                Assert.Equal(85000, club.GoalkeeperCoachSalary);
            }
        }

        [Fact]
        public void Save_WritesOnlyTheEntries_AndCapsTheCompetence()
        {
            using (FakeModule fake = OwnClubWithStaff(5, 3))
            {
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;
                club.ClubName = "X";
                club.StadiumName = "Y";
                club.CoTrainerCompetence = 20;
                club.CoTrainerContract = 3;
                club.CoTrainerSalary = 450000;
                club.GoalkeeperCoachCompetence = 15;
                club.GoalkeeperCoachContract = 0;
                club.GoalkeeperCoachSalary = 120000;

                controller.Save();

                uint co = CoTrainerEntry(5);
                Assert.Equal((byte)15, fake.ReadModuleBytes(co + Settings.StaffCompetenceOffset, 1)[0]);
                Assert.Equal((byte)3, fake.ReadModuleBytes(co + Settings.StaffContractOffset, 1)[0]);
                Assert.Equal(450000, BitConverter.ToInt32(fake.ReadModuleBytes(co + Settings.StaffSalaryOffset, 4), 0));
                // Name and age stay.
                Assert.Equal((ushort)1, BitConverter.ToUInt16(fake.ReadModuleBytes(co, 2), 0));
                Assert.Equal((byte)44, fake.ReadModuleBytes(co + 7, 1)[0]);
                uint goalkeeper = GoalkeeperCoachEntry(3);
                Assert.Equal((byte)15, fake.ReadModuleBytes(goalkeeper + Settings.StaffCompetenceOffset, 1)[0]);
                Assert.Equal((byte)0, fake.ReadModuleBytes(goalkeeper + Settings.StaffContractOffset, 1)[0]);
                Assert.Equal(120000, BitConverter.ToInt32(fake.ReadModuleBytes(goalkeeper + Settings.StaffSalaryOffset, 4), 0));
            }
        }

        [Fact]
        public void WithoutStaff_ShowsNone_AndSavingTouchesNoEntry()
        {
            using (FakeModule fake = OwnClubWithStaff(0, 0))
            {
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;

                Assert.False(club.HasCoTrainer);
                Assert.Equal("keiner", club.CoTrainerFirstname);
                Assert.Equal(0, club.CoTrainerSalary);
                Assert.False(club.HasGoalkeeperCoach);
                Assert.Equal("keiner", club.GoalkeeperCoachFirstname);

                club.ClubName = "X";
                club.StadiumName = "Y";
                controller.Save();

                Assert.Equal((byte)9, fake.ReadModuleBytes(CoTrainerEntry(5) + Settings.StaffCompetenceOffset, 1)[0]);
                Assert.Equal(new byte[4], fake.ReadModuleBytes(Settings.CoachPoolTableOffset + Settings.StaffSalaryOffset, 4));
            }
        }

        [Fact]
        public void Save_Rename_ReusesAnExistingName_AppendsANewOne_AndLeavesUnchangedOrEmptyAlone()
        {
            using (FakeModule fake = OwnClubWithStaff(5, 3))
            {
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;
                club.ClubName = "X";
                club.StadiumName = "Y";
                club.CoTrainerFirstname = "Sepp";          // already in the pool (index 3)
                club.CoTrainerLastname = "  Rehhagel ";     // new, trimmed like the game
                club.GoalkeeperCoachLastname = "";          // empty: stays

                controller.Save();

                Assert.Equal((ushort)3, NameIndex(fake, CoTrainerEntry(5), 0));
                Assert.Equal((ushort)5, NameIndex(fake, CoTrainerEntry(5), 2));
                Assert.Equal((ushort)3, NameIndex(fake, GoalkeeperCoachEntry(3), 0));
                Assert.Equal((ushort)4, NameIndex(fake, GoalkeeperCoachEntry(3), 2));
                Assert.Equal((ushort)6, BitConverter.ToUInt16(fake.ReadModuleBytes(Settings.NamePoolCountOffset, 2), 0));

                Club reread = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN).Club;
                Assert.Equal("Sepp", reread.CoTrainerFirstname);
                Assert.Equal("Rehhagel", reread.CoTrainerLastname);
                Assert.Equal("Maier", reread.GoalkeeperCoachLastname);
            }
        }

        [Fact]
        public void UnresolvableName_IsEmpty_AndAnIdOutsideTheTableShowsTheNumber()
        {
            using (FakeModule fake = OwnClubWithStaff(5, 200))
            {
                WriteEntry(fake, CoTrainerEntry(5), 30, 31, 9, 2, 300000);

                Club club = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN).Club;

                Assert.Equal("", club.CoTrainerFirstname);
                Assert.Equal("", club.CoTrainerLastname);
                Assert.Equal("Nr. 200 (nicht lesbar)", club.GoalkeeperCoachFirstname);
                Assert.Equal((byte)0, club.GoalkeeperCoachCompetence);
            }
        }
    }
}
