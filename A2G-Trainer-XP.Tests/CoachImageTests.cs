using AnstossTrainer.Shared.Tests;
using System;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for the manager image (Coach.Image): its fixed per-ID address next to the competency
    /// table, the builds it isn't known for, and the "Image" box on the coach view.
    /// </summary>
    public class CoachImageTests
    {
        private static uint ImageOffset(int trainerSlot) =>
            Settings.CoachImageRecordOffset + (uint)(trainerSlot + 1) * Settings.CoachCompetencyRecordStride;

        [Fact]
        public void ImageAddress_SitsSixBytesBeforeTheCompetencyTable()
        {
            Assert.Equal(Settings.CoachCompetencyRecordOffset - 6, Settings.CoachImageRecordOffset);
        }

        [Fact]
        public void GetEntity_ReadsTheImageOfItsOwnMitspieler()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(ImageOffset(0), BitConverter.GetBytes((ushort)CoachEnums.ManagerImage.Schleifer));
                fake.WriteModuleBytes(ImageOffset(1), BitConverter.GetBytes((ushort)CoachEnums.ManagerImage.Wissenschaftler));

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 1).Coach;

                Assert.True(coach.HasImage);
                Assert.Equal(CoachEnums.ManagerImage.Wissenschaftler, coach.Image);
            }
        }

        [Fact]
        public void Save_WritesTheImageOnlyForItsOwnMitspieler()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 1);
                controller.Coach.Image = CoachEnums.ManagerImage.Trunkenbold;

                controller.Save();

                Assert.Equal((ushort)16, BitConverter.ToUInt16(fake.ReadModuleBytes(ImageOffset(1), 2), 0));
                Assert.Equal((ushort)0, BitConverter.ToUInt16(fake.ReadModuleBytes(ImageOffset(0), 2), 0));
            }
        }

        [Fact]
        public void WithoutFixedAddresses_TheImageIsNeitherReadNorWritten()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                fake.WriteModuleBytes(ImageOffset(0), BitConverter.GetBytes((ushort)CoachEnums.ManagerImage.PrMann));

                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                Assert.False(controller.Coach.HasImage);
                Assert.Equal(CoachEnums.ManagerImage.KeinImage, controller.Coach.Image);

                controller.Coach.Image = CoachEnums.ManagerImage.Weichei;
                controller.Save();

                Assert.Equal((ushort)CoachEnums.ManagerImage.PrMann, BitConverter.ToUInt16(fake.ReadModuleBytes(ImageOffset(0), 2), 0));
            }
        }

        // The values index the game's own text table (anstoss2.exe+2B96C8), not the texts' order.
        [Theory]
        [InlineData(CoachEnums.ManagerImage.KeinImage, 0, "kein Image")]
        [InlineData(CoachEnums.ManagerImage.HarterHund, 1, "harter Hund")]
        [InlineData(CoachEnums.ManagerImage.Schleifer, 2, "Schleifer")]
        [InlineData(CoachEnums.ManagerImage.Wissenschaftler, 10, "Wissenschaftler")]
        [InlineData(CoachEnums.ManagerImage.Hoffnungstraeger, 11, "Hoffnungsträger")]
        [InlineData(CoachEnums.ManagerImage.PrMann, 12, "PR-Mann")]
        [InlineData(CoachEnums.ManagerImage.UnbekannteGroesse, 14, "unbekannte Größe")]
        [InlineData(CoachEnums.ManagerImage.Trunkenbold, 16, "Trunkenbold")]
        public void ManagerImage_MatchesTheGamesTextTable(CoachEnums.ManagerImage image, ushort value, string text)
        {
            Assert.Equal(value, (ushort)image);
            Assert.Equal(text, PlayerEnums.GetDescription(image));
        }

        [Fact]
        public void Image_RaisesPropertyChanged()
        {
            Coach coach = new Coach();
            string changed = null;
            coach.PropertyChanged += (s, e) => changed = e.PropertyName;

            coach.Image = CoachEnums.ManagerImage.Choleriker;

            Assert.Equal(nameof(Coach.Image), changed);
        }

        private static T Field<T>(CoachView view, string name) =>
            (T)typeof(CoachView).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

        [Fact]
        public void CoachView_BindsTheImageAndEnablesItOnlyWhenKnown() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteModuleBytes(ImageOffset(0), BitConverter.GetBytes((ushort)CoachEnums.ManagerImage.LustigerGeselle));
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN, trainerIndex: 0);
                trainer.CoachView.InitClubTabControl();

                ComboBox imageInput = Field<ComboBox>(trainer.CoachView, "ImageInput");
                Assert.Equal(nameof(Coach.Image), imageInput.DataBindings["SelectedValue"].BindingMemberInfo.BindingField);
                Assert.True(Field<GroupBox>(trainer.CoachView, "ImageBox").Enabled);

                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                trainer.CoachView.RefreshValues(PlayerEnums.AddressType.OWN);

                Assert.False(Field<GroupBox>(trainer.CoachView, "ImageBox").Enabled);
            }
        });
    }
}
