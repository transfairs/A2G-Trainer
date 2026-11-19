using System;
using System.Linq;
using System.Text;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Trainer slots follow the game's turn order (Settings.MitspielerOrderTableOffset), which maps each
    /// place to a Mitspieler ID. The IDs need not be 1..count: a savegame with a single manager can keep
    /// him as ID 4 while the working copy of ID 1 still holds a manager of an earlier game.
    /// </summary>
    public class MitspielerOrderTests
    {
        private static uint WorkingCopy(int id, uint slotOffset) => Settings.CoachSlotTableOffset + (uint)id * Settings.CoachSlotStride + slotOffset;

        private static void WriteOrder(FakeModule fake, params short[] ids)
        {
            fake.WriteModuleBytes(Settings.ActiveTrainerCountOffset, new byte[] { (byte)ids.Length });
            for (int place = 0; place < ids.Length; place++)
                fake.WriteModuleBytes(Settings.MitspielerOrderTableOffset + (uint)(place + 1) * 2, BitConverter.GetBytes(ids[place]));
        }

        [Fact]
        public void MitspielerIdOf_ReadsTheOrderTable()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOrder(fake, 4, 2);

                Assert.Equal(4, CoachController.MitspielerIdOf(fake.Memory, 0));
                Assert.Equal(2, CoachController.MitspielerIdOf(fake.Memory, 1));
            }
        }

        [Theory]
        [InlineData((short)-1)]
        [InlineData((short)0)]
        [InlineData((short)5)]
        public void MitspielerIdOf_WithoutAValidEntry_FallsBackToSlotPlusOne(short entry)
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(Settings.MitspielerOrderTableOffset + 2 * 2, BitConverter.GetBytes(entry));

                Assert.Equal(2, CoachController.MitspielerIdOf(fake.Memory, 1));
                Assert.Equal(6, CoachController.MitspielerIdOf(fake.Memory, 5));
            }
        }

        [Fact]
        public void MitspielerIdOf_WithoutAVerifiedLayout_FallsBackToSlotPlusOne()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOrder(fake, 4);
                fake.Memory.Layout = PersistentLayout.CreateGogGuess();

                Assert.Equal(1, CoachController.MitspielerIdOf(fake.Memory, 0));
            }
        }

        [Fact]
        public void MitspielerIdOf_WithoutALayout_FallsBackToSlotPlusOne()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOrder(fake, 4);
                fake.Memory.Layout = null;

                Assert.Equal(1, CoachController.MitspielerIdOf(fake.Memory, 0));
            }
        }

        [Fact]
        public void MitspielerIdOf_OutsideTheTrainerSlots_FallsBackToSlotPlusOne()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOrder(fake, 4);

                Assert.Equal(0, CoachController.MitspielerIdOf(fake.Memory, -1));
            }
        }

        [Fact]
        public void SaveGameEditor_Coaches_FollowTheOrderTable()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            uint b = SaveGameFile.ModuleBase;
            file.WriteInt16(b + Settings.ActiveTrainerCountOffset, 2);
            file.WriteInt16(b + Settings.MitspielerOrderTableOffset + 1 * 2, 4);
            file.WriteInt16(b + Settings.MitspielerOrderTableOffset + 2 * 2, -1);

            Assert.Equal(new[] { 4, 2 }, SaveGameEditor.For(file).Coaches.Select(c => c.MitspielerId));
        }

        [Fact]
        public void CoachController_ReadsTheManagerOfTheMappedId()
        {
            using (FakeModule fake = new FakeModule())
            {
                Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
                WriteOrder(fake, 4);
                // ID 1 still holds the manager of an earlier game at another club.
                fake.WriteModuleBytes(WorkingCopy(1, 0), latin1.GetBytes("Alt\0"));
                fake.WriteModuleBytes(WorkingCopy(1, 0x1C), new byte[] { 14, 0, 98, 0 });
                fake.WriteModuleBytes(WorkingCopy(4, 0), latin1.GetBytes("Neu\0"));
                fake.WriteModuleBytes(WorkingCopy(4, 0x1C), new byte[] { 14, 0, 73, 0 });

                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                var active = CoachController.GetActiveTrainers(fake.Memory, false, PlayerEnums.AddressType.OWN);

                Assert.Equal(4, controller.MitspielerId);
                Assert.Equal("Neu", controller.Coach.Firstname);
                Assert.Equal((byte)73, controller.Coach.CurrentClubId);
                Assert.Equal("Neu", Assert.Single(active).Value.Firstname);
                Assert.Equal("Neu", CoachController.ForClub(fake.Memory, false, (PlayerEnums.Country)14, 73).Coach.Firstname);
                Assert.Null(CoachController.ForClub(fake.Memory, false, (PlayerEnums.Country)14, 98));
            }
        }
    }
}
