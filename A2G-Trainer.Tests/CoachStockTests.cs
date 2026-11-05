using System;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for the coach's stock slots in CoachController: shares as Int32, a new position written
    /// into a free slot, and an emptied slot cleared the way the game does it (FUN_004029e0).
    /// </summary>
    public class CoachStockTests
    {
        private static string SlotField(CoachEnums.AddressKey key, int slot) =>
            Tools.SumHex(new[] { AddressPresets.COACH[key], (slot * 0x10).ToString("X") });

        [Fact]
        public void GetEntity_ReadsSharesAsInt32()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachMemory.WriteWorkingCopy(fake, SlotField(CoachEnums.AddressKey.STOCK_COUNTRY, 0), new[] { (byte)PlayerEnums.Country.Deutschland });
                CoachMemory.WriteWorkingCopy(fake, SlotField(CoachEnums.AddressKey.STOCK_SHARES, 0), BitConverter.GetBytes(70000));

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal(70000, coach.Stocks[0].Shares);
            }
        }

        [Fact]
        public void Save_NewPositionInAFreeSlot_WritesCountryClubSharesAndPrice()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                Stock stock = controller.Coach.Stocks[2];
                stock.Country = PlayerEnums.Country.England;
                stock.ClubId = 4;
                stock.Shares = 70000;
                stock.Price = 33;

                controller.Save();

                foreach (Func<string, int, byte[]> read in new Func<string, int, byte[]>[]
                {
                    (offset, length) => CoachMemory.ReadWorkingCopy(fake, offset, length),
                    (offset, length) => CoachMemory.ReadRecord(fake, offset, length)
                })
                {
                    Assert.Equal((byte)PlayerEnums.Country.England, read(SlotField(CoachEnums.AddressKey.STOCK_COUNTRY, 2), 1)[0]);
                    Assert.Equal((ushort)4, BitConverter.ToUInt16(read(SlotField(CoachEnums.AddressKey.STOCK_CLUB, 2), 2), 0));
                    Assert.Equal(70000, BitConverter.ToInt32(read(SlotField(CoachEnums.AddressKey.STOCK_SHARES, 2), 4), 0));
                    Assert.Equal((ushort)33, BitConverter.ToUInt16(read(SlotField(CoachEnums.AddressKey.STOCK_PRICE, 2), 2), 0));
                }
            }
        }

        [Fact]
        public void Save_SlotWithoutSharesOrCountry_IsClearedLikeTheGameDoes()
        {
            using (FakeModule fake = new FakeModule())
            {
                // Slot 1: leftover Italien / club 7 without shares; slot 3: shares but no country.
                CoachMemory.WriteWorkingCopy(fake, SlotField(CoachEnums.AddressKey.STOCK_COUNTRY, 1), new[] { (byte)PlayerEnums.Country.Italien });
                CoachMemory.WriteWorkingCopy(fake, SlotField(CoachEnums.AddressKey.STOCK_CLUB, 1), new byte[] { 7, 0 });
                CoachMemory.WriteWorkingCopy(fake, SlotField(CoachEnums.AddressKey.STOCK_PRICE, 1), BitConverter.GetBytes((ushort)12));
                CoachMemory.WriteWorkingCopy(fake, SlotField(CoachEnums.AddressKey.STOCK_SHARES, 3), BitConverter.GetBytes(25));
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

                controller.Save();

                foreach (int slot in new[] { 1, 3 })
                {
                    Assert.Equal(new byte[1], CoachMemory.ReadWorkingCopy(fake, SlotField(CoachEnums.AddressKey.STOCK_COUNTRY, slot), 1));
                    Assert.Equal(new byte[2], CoachMemory.ReadWorkingCopy(fake, SlotField(CoachEnums.AddressKey.STOCK_CLUB, slot), 2));
                    Assert.Equal(new byte[4], CoachMemory.ReadWorkingCopy(fake, SlotField(CoachEnums.AddressKey.STOCK_SHARES, slot), 4));
                    Assert.Equal(new byte[2], CoachMemory.ReadWorkingCopy(fake, SlotField(CoachEnums.AddressKey.STOCK_PRICE, slot), 2));
                }
            }
        }
    }
}
