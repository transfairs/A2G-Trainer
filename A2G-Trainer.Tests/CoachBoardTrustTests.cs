using System;
using System.Collections.Generic;
using System.ComponentModel;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for the board's trust in the manager ("Vertrauen des Vorstandes"): the base value at
    /// working copy +0x94 plus the four bonus bytes +0x96..+0x99, capped to 0..100 the way the game
    /// computes it (FUN_00639100), read and saved through CoachController. The views are covered in
    /// CoachContractTests ("Vertrag") and ClubEditorSettingsTests ("Identität", box "Vorstand"), the save game editor
    /// in SaveGameEntriesTests.
    /// </summary>
    public class CoachBoardTrustTests
    {
        private static uint WorkingCopy(uint slotOffset) => Settings.CoachSlotTableOffset + 1 * Settings.CoachSlotStride + slotOffset;

        [Fact]
        public void AddressPresets_PointAtTheBaseAndBonusFields()
        {
            Assert.Equal("34B94", AddressPresets.COACH[CoachEnums.AddressKey.TRUST]);
            Assert.Equal("34B96", AddressPresets.COACH[CoachEnums.AddressKey.TRUST_BONUS]);
        }

        [Theory]
        [InlineData(70, 0, 70)]
        [InlineData(70, 12, 82)]
        [InlineData(95, 20, 100)]
        [InlineData(150, 0, 100)]
        [InlineData(10, -25, 0)]
        [InlineData(-15, 0, 0)]
        public void BoardTrust_IsBasePlusBonusCappedToZeroToHundred(short trustBase, int bonus, byte expected)
        {
            Coach coach = new Coach { BoardTrustBase = trustBase, BoardTrustBonus = bonus };

            Assert.Equal(expected, coach.BoardTrust);
        }

        [Theory]
        [InlineData(0, 80, 80)]
        [InlineData(12, 80, 68)]
        [InlineData(-10, 100, 110)]
        [InlineData(5, 250, 95)]
        public void BoardTrust_Set_AdjustsOnlyTheBase(int bonus, byte trust, short expectedBase)
        {
            Coach coach = new Coach { BoardTrustBonus = bonus };
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            coach.BoardTrust = trust;

            Assert.Equal(expectedBase, coach.BoardTrustBase);
            Assert.Equal(bonus, coach.BoardTrustBonus);
            Assert.Equal(Math.Min(trust, Coach.MaxBoardTrust), coach.BoardTrust);
            Assert.Contains(nameof(Coach.BoardTrust), raised);
        }

        [Fact]
        public void BoardTrustBonus_Set_RaisesBoardTrust()
        {
            Coach coach = new Coach();
            List<string> raised = new List<string>();
            ((INotifyPropertyChanged)coach).PropertyChanged += (s, e) => raised.Add(e.PropertyName);

            coach.BoardTrustBonus = 3;

            Assert.Equal(new[] { nameof(Coach.BoardTrustBonus), nameof(Coach.BoardTrust) }, raised);
        }

        [Fact]
        public void GetEntity_ReadsBaseAndSignedBonusBytes()
        {
            using (FakeModule fake = new FakeModule())
            {
                // Base 60, bonus 10 + 5 + (-3) + 2 = 14.
                fake.WriteModuleBytes(WorkingCopy(0x94), new byte[] { 60, 0, 10, 5, 0xFD, 2 });

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal((short)60, coach.BoardTrustBase);
                Assert.Equal(14, coach.BoardTrustBonus);
                Assert.Equal((byte)74, coach.BoardTrust);
            }
        }

        [Fact]
        public void GetEntity_WithANegativeBase_ReadsItSigned()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(WorkingCopy(0x94), BitConverter.GetBytes((short)-15));

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal((short)-15, coach.BoardTrustBase);
                Assert.Equal((byte)0, coach.BoardTrust);
            }
        }

        [Fact]
        public void Save_WritesTheBaseToWorkingCopyAndRecordAndLeavesTheBonusAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                byte[] bonus = { 10, 5, 0xFD, 2 };
                fake.WriteModuleBytes(WorkingCopy(0x96), bonus);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

                controller.Coach.BoardTrust = 100;
                controller.Save();

                Addresses a = AddressPresets.COACH;
                Assert.Equal((short)86, BitConverter.ToInt16(CoachMemory.ReadWorkingCopy(fake, a[CoachEnums.AddressKey.TRUST], 2), 0));
                Assert.Equal((short)86, BitConverter.ToInt16(CoachMemory.ReadRecord(fake, a[CoachEnums.AddressKey.TRUST], 2), 0));
                Assert.Equal(bonus, CoachMemory.ReadWorkingCopy(fake, a[CoachEnums.AddressKey.TRUST_BONUS], 4));
            }
        }

    }
}
