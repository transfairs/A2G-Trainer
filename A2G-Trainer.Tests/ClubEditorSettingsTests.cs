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
        internal const PlayerEnums.Country Country = PlayerEnums.Country.Deutschland;
        internal const byte OwnId = 1;
        internal const uint OtherCopy = 0x510000;

        internal static uint Record(byte id) => Settings.ClubRecordTableOffset + id * Settings.ClubRecordStride;

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
        internal static void SeedOwnClub(FakeModule fake, bool withRecord = true)
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

        // The finance block (Kalkulation, loans, investments) and the slush fund before it.
        [Fact]
        public void Budget_IsReadFromTheWorkingCopy_AndSavedToEveryCopyAndTheRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedOwnClub(fake);
                fake.WriteDisplayCacheBytes("270", new byte[] { 20, 30, 12, 0, 25, 0 });
                fake.WriteDisplayCacheBytes("2AC", BitConverter.GetBytes((short)3));
                fake.WriteDisplayCacheBytes("373", new byte[] { 2, 1, 1 });
                fake.WriteDisplayCacheBytes("144", BitConverter.GetBytes(77000));
                fake.WriteDisplayCacheBytes("148", BitConverter.GetBytes((short)15));
                // Loan slot 1: 2 Mio, 2 years at 8.25 %, repayable early; slot 0 inactive.
                byte[] loan = new byte[0x14];
                Array.Copy(BitConverter.GetBytes(2000000), loan, 4);
                Array.Copy(BitConverter.GetBytes((short)850), 0, loan, 4, 2);
                Array.Copy(BitConverter.GetBytes((short)825), 0, loan, 6, 2);
                loan[0xA] = 1;
                loan[0xB] = 3;
                fake.WriteDisplayCacheBytes("170", loan);
                // Investment slot 0: Kostol & Any, 100,000 paid in, base 100,000 at index 1000, now 1500.
                byte[] investment = new byte[0x10];
                Array.Copy(BitConverter.GetBytes(100000), investment, 4);
                Array.Copy(BitConverter.GetBytes(100000), 0, investment, 4, 4);
                Array.Copy(BitConverter.GetBytes((short)1000), 0, investment, 8, 2);
                investment[0xA] = 1;
                investment[0xB] = 1;
                fake.WriteDisplayCacheBytes("1FC", investment);
                fake.WriteModuleBytes(Settings.InvestmentIndexOffset + 4, BitConverter.GetBytes(1500));

                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;

                Assert.Equal((byte)20, club.SeasonTicketDiscount);
                Assert.Equal((byte)30, club.SeasonTicketQuota);
                Assert.Equal((short)12, club.StandingPrice);
                Assert.Equal((short)25, club.SeatPrice);
                Assert.Equal((short)3, club.Security);
                Assert.Equal((byte)2, club.LawFirm);
                Assert.Equal((byte)1, club.StatisticsDepartment);
                Assert.True(club.HasComputerDepartment);
                Assert.Equal(77000, club.SlushFund);
                Assert.Equal((short)15, club.SlushFundRate);
                ClubLoan read = Assert.Single(club.Loans);
                Assert.Equal(1, read.Slot);
                Assert.Equal(2000000, read.Amount);
                Assert.Equal((byte)1, read.Term);
                Assert.Equal(8.25m, read.Rate);
                Assert.True(read.Cancellable);
                ClubInvestment held = Assert.Single(club.Investments);
                Assert.Equal("Kostol & Any", held.KindName);
                Assert.Equal(150000, held.Value);

                club.StandingPrice = 15;
                club.LawFirm = 4;
                club.StatisticsDepartment = 2;
                club.HasComputerDepartment = false;
                club.SlushFund = 5;
                read.Amount = 3000000;
                held.Value = 300000;
                controller.Save();

                foreach (Func<uint, int, byte[]> copy in new Func<uint, int, byte[]>[]
                {
                    (offset, length) => fake.ReadDisplayCacheBytes(offset.ToString("X"), length),
                    (offset, length) => fake.ReadModuleBytes(OtherCopy + offset, length),
                    // The record keeps the finance block 0x50 further on, the slush fund 0xC before.
                    (offset, length) => fake.ReadModuleBytes(Record(OwnId) + (offset < 0x154 ? offset - 0xC : offset + 0x50), length),
                })
                {
                    Assert.Equal((short)15, BitConverter.ToInt16(copy(0x272, 2), 0));
                    Assert.Equal(new byte[] { 4, 2, 0, 4, 2, 0 }, copy(0x370, 6));
                    Assert.Equal(5, BitConverter.ToInt32(copy(0x144, 4), 0));
                    Assert.Equal(3000000, BitConverter.ToInt32(copy(0x170, 4), 0));
                    Assert.Equal(200000, BitConverter.ToInt32(copy(0x200, 4), 0));
                }
            }
        }

        [Fact]
        public void BankAndStock_InvestmentsAreCreatedAndDissolved_SharePriceGoesToTheRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedOwnClub(fake);
                fake.WriteDisplayCacheBytes("384", BitConverter.GetBytes(90000));
                fake.WriteDisplayCacheBytes("39C", BitConverter.GetBytes((short)12));
                fake.WriteDisplayCacheBytes("10E", new byte[] { 37 });
                // Slot 0 active (Tagesgeld), slot 2 active (Mme Véronique); index of kind 4 is 2000.
                fake.WriteDisplayCacheBytes("1FC", new byte[] { 0x10, 0x27, 0, 0, 0x10, 0x27, 0, 0, 0xE8, 0x03, 0, 1 });
                fake.WriteDisplayCacheBytes("21C", new byte[] { 0x20, 0x4E, 0, 0, 0x20, 0x4E, 0, 0, 0xE8, 0x03, 4, 1 });
                fake.WriteModuleBytes(Settings.InvestmentIndexOffset + 16, BitConverter.GetBytes(2000));
                // Listed, 50,000 shares at 420 DM.
                fake.WriteModuleBytes(Record(OwnId) + Settings.ClubRecordListedOffset, new byte[] { 1 });
                fake.WriteDisplayCacheBytes(Settings.ClubListedOffset.ToString("X"), new byte[] { 1 });
                fake.WriteModuleBytes(Record(OwnId) + Settings.ClubRecordStockOffset, new byte[] { 0x50, 0xC3, 0, 0, 0xA4, 0x01 });

                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;

                Assert.Equal(90000, club.LegalCosts);
                Assert.Equal((short)12, club.OverdraftLimit);
                Assert.Equal(ClubEnums.PublicImage.RechtAngesehen, club.PublicImageStage);
                Assert.True(club.IsListed);
                Assert.Equal(50000, club.IssuedShares);
                Assert.Equal((short)420, club.SharePrice);
                Assert.Equal(2, club.Investments.Count);
                Assert.Equal(40000, club.Investments[1].Value);

                club.LegalCosts = 0;
                club.PublicImageStage = ClubEnums.PublicImage.SehrBeliebt;
                club.SharePrice = 777;
                club.Investments.RemoveAt(0);
                club.Investments.Add(ClubInvestment.New(1, 50000, club.InvestmentIndices));
                controller.Save();

                Assert.Equal(0, BitConverter.ToInt32(fake.ReadDisplayCacheBytes("384", 4), 0));
                Assert.Equal((byte)50, fake.ReadDisplayCacheBytes("10E", 1)[0]);
                Assert.Equal((byte)50, fake.ReadModuleBytes(Record(OwnId) + 0x103, 1)[0]);
                // Slot 0 dissolved and reused by the new investment (Kostol & Any, index 0 -> stored 0).
                byte[] slot0 = fake.ReadDisplayCacheBytes("1FC", 0x10);
                Assert.Equal(50000, BitConverter.ToInt32(slot0, 0));
                Assert.Equal(new byte[] { 1, 1 }, new[] { slot0[0xA], slot0[0xB] });
                Assert.Equal((byte)1, fake.ReadDisplayCacheBytes("227", 1)[0]);
                byte[] stock = fake.ReadModuleBytes(Record(OwnId) + Settings.ClubRecordStockOffset, 0x2C);
                Assert.Equal((short)777, BitConverter.ToInt16(stock, 4));
                Assert.Equal((short)777, BitConverter.ToInt16(stock, 0x18));
                Assert.Equal((short)777, BitConverter.ToInt16(stock, 0x2A));
            }
        }

        [Fact]
        public void Loans_AreChangedCreatedLikeTheGameAndRepaid()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedOwnClub(fake);
                // Slot 0: 2 Mio, 1 year at 9 %, not repayable early.
                byte[] loan = new byte[0x14];
                Array.Copy(BitConverter.GetBytes(2000000), loan, 4);
                Array.Copy(BitConverter.GetBytes((short)900), 0, loan, 4, 2);
                loan[0xB] = 1;
                fake.WriteDisplayCacheBytes("15C", loan);
                // Season day 50, 17 March: day 17 of the quarter's last month -> 17 - 30 + (3 % 3) * 30 = -13.
                fake.WriteModuleBytes(Settings.GlobalCounterOffset, BitConverter.GetBytes((short)50));
                fake.WriteModuleBytes(Settings.DayOfMonthOffset, BitConverter.GetBytes((short)17));
                fake.WriteModuleBytes(Settings.MonthOffset, BitConverter.GetBytes((short)3));
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;

                ClubLoan held = Assert.Single(club.Loans);
                Assert.Equal(9m, held.Rate);
                Assert.False(held.Cancellable);
                held.Term = 2;
                held.Rate = 7.5m;
                ClubLoan added = ClubLoan.New(500000, 1, true, 800);
                Assert.Equal(new short[] { 800, 775, 750 }, added.Rates);
                Assert.Equal(new short[] { 650, 625, 600 }, ClubLoan.New(1, 0, false, 800).Rates);
                club.Loans.Add(added);
                controller.Save();

                byte[] slot0 = fake.ReadDisplayCacheBytes("15C", 0x14);
                Assert.Equal((byte)2, slot0[0xA]);
                Assert.Equal((short)750, BitConverter.ToInt16(slot0, 8));
                byte[] slot1 = fake.ReadDisplayCacheBytes("170", 0x14);
                Assert.Equal(500000, BitConverter.ToInt32(slot1, 0));
                Assert.Equal(new byte[] { 1, 3 }, new[] { slot1[0xA], slot1[0xB] });
                Assert.Equal((short)-13, BitConverter.ToInt16(slot1, 0xE));
                Assert.Equal((short)2050, BitConverter.ToInt16(slot1, 0x10));
                // The record keeps it 0x50 further on.
                Assert.Equal(500000, BitConverter.ToInt32(fake.ReadModuleBytes(Record(OwnId) + 0x170 + 0x50, 4), 0));

                club.Loans.Remove(held);
                controller.Save();
                Assert.Equal((byte)0, fake.ReadDisplayCacheBytes("167", 1)[0]);
                Assert.Equal((byte)3, fake.ReadDisplayCacheBytes("17B", 1)[0]);
            }
        }

        [Fact]
        public void GameRates_ReadAndWriteTheCostsForEveryClub()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(Settings.LawFirmFeeTableOffset, new byte[] { 10, 0, 30, 0, 15, 0, 50, 0, 20, 0, 80, 0, 30, 0, 120, 0, 50, 0, 200, 0 });
                fake.WriteModuleBytes(Settings.DepartmentCostTableOffset, new byte[] { 0, 0, 2, 0, 20, 0, 0, 0, 0, 0, 2, 0 });

                GameCosts costs = GameRates.ReadCosts(fake.Memory, FixedAddresses.Original);
                Assert.Equal("Lothar Liebling, Berlin", costs.LawFirms[2].Name);
                Assert.Equal(20000, costs.LawFirms[2].RedCard);
                Assert.Equal(200000, costs.LawFirms[4].Other);
                Assert.Equal(20000, costs.SmallStatistics);
                Assert.Equal(200000, costs.LargeStatistics);
                Assert.Equal(200000, costs.Computer);
                Assert.Equal(5, costs.Indices.Count);

                // Shown in DM, kept in the game's thousands: 500 DM rounds to 1,000 DM, shown right away.
                costs.LawFirms[2].RedCard = 500;
                Assert.Equal(1000, costs.LawFirms[2].RedCard);
                costs.LargeStatistics = 0;
                costs.Computer = 0;
                costs.Indices[3].Value = 1234;
                GameRates.WriteCosts(fake.Memory, FixedAddresses.Original, costs);

                Assert.Equal(new byte[] { 1, 0, 80, 0 }, fake.ReadModuleBytes(Settings.LawFirmFeeTableOffset + 8, 4));
                Assert.Equal(new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, fake.ReadModuleBytes(Settings.DepartmentCostTableOffset, 12));
                Assert.Equal(1234, BitConverter.ToInt32(fake.ReadModuleBytes(Settings.InvestmentIndexOffset + 12, 4), 0));
                Assert.Null(GameRates.ReadCosts(fake.Memory, null));
            }
        }

        [Fact]
        public void GameRates_ReadAndWriteTheSavegameWideRates()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(Settings.InterestOverLimitOffset, new byte[] { 0xB0, 0x04, 0x4B, 0x00, 0x20, 0x03, 0xF4, 0x01, 0, 0, 0x2C, 0x01 });
                fake.WriteModuleBytes(Settings.CorporationTaxOffset, new byte[] { 10 });

                InterestRates rates = GameRates.Read(fake.Memory, FixedAddresses.Original);
                Assert.Equal(12.00m, rates.OverLimit);
                Assert.Equal(0.75m, rates.Deposit);
                Assert.Equal(8.00m, rates.Overdraft);
                Assert.Equal(5.00m, rates.Loan);
                Assert.Equal(3.00m, rates.CallMoney);
                Assert.Equal(10m, rates.CorporationTax);

                rates.Loan = 4.25m;
                rates.CorporationTax = 150;
                GameRates.Write(fake.Memory, FixedAddresses.Original, rates);
                Assert.Equal((short)425, BitConverter.ToInt16(fake.ReadModuleBytes(Settings.InterestLoanOffset, 2), 0));
                Assert.Equal((byte)99, fake.ReadModuleBytes(Settings.CorporationTaxOffset, 1)[0]);

                Assert.Null(GameRates.Read(fake.Memory, null));
                Assert.Equal(0, GameRates.InvestmentIndex(fake.Memory, FixedAddresses.Original, 7));
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
                club.AssociationImage = ClubEnums.AssociationImage.Normal;
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
                    Assert.Equal(new byte[] { 5 }, copy(Own[ClubEnums.AddressKey.Association], 1));
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
                Assert.Equal(new byte[] { 5 }, fake.ReadModuleBytes(record + 0xF7, 1));
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
        private static void WithIdentityTab(Action<FakeModule, ClubView> test, bool withRecord = true, string tab = "IdentityTab", Action<FakeModule> seed = null) => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            using (Form host = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) })
            {
                SeedOwnClub(fake, withRecord);
                seed?.Invoke(fake);
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
            Assert.Equal("FC Hannover 66", Field<ComboBox>(view, "ArchRivalInput").Text);
            Assert.Equal("–", Field<ComboBox>(view, "FanFriendshipInput").Text);
            Assert.Equal("erfolgshungrig", Field<ComboBox>(view, "FanTypeInput").Text);
            Assert.True(Field<TextBox>(view, "ChantInput").Enabled);
            Assert.Same(ShownClub(view), Field<JerseyView>(view, "JerseyEditor").Club);

            // The own club can't be its own rival.
            ComboBox rival = Field<ComboBox>(view, "ArchRivalInput");
            Assert.Equal(new[] { "–", "VfL Osnabrück", "FC Hannover 66" }, Enumerable.Range(0, rival.Items.Count).Select(i => rival.GetItemText(rival.Items[i])));

            CheckAssociationTab(view);
        }, seed: SeedRequirements);

        [Fact]
        public void IdentityTab_EditsReachTheClub() => WithIdentityTab((fake, view) =>
        {
            ComboBox friendship = Field<ComboBox>(view, "FanFriendshipInput");
            friendship.SelectedIndex = 1;
            typeof(ComboBox).GetMethod("OnSelectionChangeCommitted", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(friendship, new object[] { EventArgs.Empty });
            Assert.Equal((byte)0, ShownClub(view).FanFriendshipId);

            // Each edit is written back like on leaving the field (every write re-reads all bindings).
            Field<TextBox>(view, "ChantInput").Text = "Eintracht";
            Field<TextBox>(view, "ChantInput").DataBindings[0].WriteValue();
            Field<ComboBox>(view, "FanTypeInput").SelectedValue = ClubEnums.FanType.Euphorisch;
            Field<ComboBox>(view, "FanTypeInput").DataBindings[0].WriteValue();
            Assert.Equal("Eintracht", ShownClub(view).Chant);
            Assert.Equal(ClubEnums.FanType.Euphorisch, ShownClub(view).FanType);
        });

        // Mitspieler 1 (one active trainer) trains the own club: base 60 plus bonus 5.
        private static void SeedManagerOfOwnClub(FakeModule fake)
        {
            Addresses coach = AddressPresets.COACH;
            fake.WriteModuleBytes(Settings.ActiveTrainerCountOffset, new byte[] { 1 });
            CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.FIRSTNAME], Latin1.GetBytes("Robin\0"));
            CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.LAST_NAME], Latin1.GetBytes("Trauer\0"));
            CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.COUNTRY], new[] { (byte)Country });
            CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.CLUB], new[] { OwnId });
            CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.TRUST], BitConverter.GetBytes((short)60));
            CoachMemory.WriteWorkingCopy(fake, coach[CoachEnums.AddressKey.TRUST_BONUS], new byte[] { 5, 0, 0, 0 });
        }

        // One test for the whole box (each Trainer window costs the x86 test host ~20 MB).
        [Fact]
        public void IdentityTab_BoardBox_ShowsTheBoardAndTheTrustOfTheClubsManager() => WithIdentityTab((fake, view) =>
        {
            Assert.Equal("normal", Field<ComboBox>(view, "BoardInput").Text);
            Assert.Equal(nameof(Club.AssociationImage), Field<ComboBox>(view, "AssociationInput").DataBindings["SelectedValue"].BindingMemberInfo.BindingField);

            // "Kalkulation" tab, built in code (same window: each one costs the x86 test host ~20 MB).
            Assert.Contains(Field<TabPage>(view, "budgetTab"), Field<TabControl>(view, "ClubTabControl").TabPages.Cast<TabPage>());
            Assert.Contains(Field<TabPage>(view, "bankTab"), Field<TabControl>(view, "ClubTabControl").TabPages.Cast<TabPage>());
            Assert.Equal(nameof(Club.PublicImageStage), Field<ComboBox>(view, "PublicImageInput").DataBindings["SelectedValue"].BindingMemberInfo.BindingField);
            Assert.Equal(nameof(Club.SharePrice), Field<TextBox>(view, "sharePriceInput").DataBindings["Text"].BindingMemberInfo.BindingField);
            Assert.True(Field<GroupBox>(view, "costsBox").Enabled);
            // The tab isn't shown, so the buttons' handlers are called directly.
            typeof(ClubView).GetMethod("AddInvestment", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
            Assert.Single(ShownClub(view).Investments);
            ShownClub(view).Investments.Clear();
            typeof(ClubView).GetMethod("RemoveInvestment", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
            Assert.Empty(ShownClub(view).Investments);
            Assert.Equal(nameof(Club.StandingPrice), Field<TextBox>(view, "standingPriceInput").DataBindings["Text"].BindingMemberInfo.BindingField);
            Assert.Equal(nameof(Club.LawFirm), Field<ComboBox>(view, "lawFirmInput").DataBindings["SelectedValue"].BindingMemberInfo.BindingField);
            Assert.Equal(nameof(Club.SlushFund), Field<TextBox>(view, "slushFundInput").DataBindings["Text"].BindingMemberInfo.BindingField);
            Assert.Same(ShownClub(view).Loans, Field<DataGridView>(view, "loansGrid").DataSource);
            Assert.Same(ShownClub(view).Investments, Field<DataGridView>(view, "investmentsGrid").DataSource);
            Assert.Equal(nameof(InterestRates.Loan), Field<TextBox>(view, "loanRateInput").DataBindings["Text"].BindingMemberInfo.BindingField);
            Assert.True(Field<GroupBox>(view, "ratesBox").Enabled);
            Field<ComboBox>(view, "BoardInput").SelectedValue = ClubEnums.Board.Pulverfass;
            Field<ComboBox>(view, "BoardInput").DataBindings[0].WriteValue();
            Assert.Equal(ClubEnums.Board.Pulverfass, ShownClub(view).Board);

            TextBox trust = Field<TextBox>(view, "BoardTrustInput");
            Assert.Equal("Robin Trauer", Field<Label>(view, "ManagerValue").Text);
            Assert.Equal("65", trust.Text);
            Assert.True(trust.Enabled);

            trust.Text = "150";
            Assert.Equal("100", trust.Text);
            typeof(ClubView).GetMethod("SaveBtn_Click", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, new object[] { null, EventArgs.Empty });

            // Only the base changes, so that base + bonus gives 100.
            Assert.Equal((short)95, BitConverter.ToInt16(CoachMemory.ReadWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.TRUST], 2), 0));
            Assert.Equal((short)95, BitConverter.ToInt16(CoachMemory.ReadRecord(fake, AddressPresets.COACH[CoachEnums.AddressKey.TRUST], 2), 0));
            Assert.Equal(new byte[] { 5, 0, 0, 0 }, CoachMemory.ReadWorkingCopy(fake, AddressPresets.COACH[CoachEnums.AddressKey.TRUST_BONUS], 4));
            Assert.Equal("100", trust.Text);

            // Without a human manager at the club the trust is locked and empty.
            fake.WriteModuleBytes(Settings.ActiveTrainerCountOffset, new byte[] { 0 });
            typeof(ClubView).GetMethod("ShowBoard", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(view, null);
            Assert.Equal("–", Field<Label>(view, "ManagerValue").Text);
            Assert.Equal("", trust.Text);
            Assert.False(trust.Enabled);
        }, seed: SeedManagerOfOwnClub);

        [Fact]
        public void IsListed_PrefersACachedWorkingCopyOverTheRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedOwnClub(fake);
                FixedAddresses fixedAddresses = FixedAddresses.For(fake.Memory);
                // Osnabrück (0) has no working copy: the record decides.
                fake.WriteModuleBytes(Record(0) + Settings.ClubRecordListedOffset, new byte[] { 1 });
                // Club 2's working copy (cache slot 4) isn't listed yet, though its record says so.
                fake.WriteModuleBytes(Record(2) + Settings.ClubRecordListedOffset, new byte[] { 1 });

                Assert.True(ClubRecords.IsListed(fake.Memory, fixedAddresses, Country, 0));
                Assert.False(ClubRecords.IsListed(fake.Memory, fixedAddresses, Country, 2));
                Assert.False(ClubRecords.IsListed(fake.Memory, fixedAddresses, Country, OwnId));

                fake.WriteDisplayCacheBytes(Settings.ClubListedOffset.ToString("X"), new byte[] { 1 });
                Assert.True(ClubRecords.IsListed(fake.Memory, fixedAddresses, Country, OwnId));
                Assert.Null(ClubRecords.IsListed(fake.Memory, fixedAddresses, Country, 9));
            }
        }

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
        public void TacticsTab_ShowsAndEditsTheTeamSkills() => WithIdentityTab((fake, view) =>
        {
            Club club = ShownClub(view);
            ComboBox pressing = Field<ComboBox>(view, "PressingInput");
            ComboBox corners = Field<ComboBox>(view, "CornersInput");
            TextBox offsideTrap = Field<TextBox>(view, "OffsideTrapInput");

            Assert.Equal(6, pressing.Items.Count);
            Assert.Equal((byte)0, pressing.SelectedValue);

            pressing.SelectedValue = (byte)4;
            pressing.DataBindings[0].WriteValue();
            corners.SelectedValue = (byte)5;
            corners.DataBindings[0].WriteValue();
            offsideTrap.Text = "130";
            offsideTrap.DataBindings[0].WriteValue();

            Assert.Equal((byte)4, club.Pressing);
            Assert.Equal((byte)5, club.Corners);
            Assert.Equal((byte)0, club.FreeKicks);
            Assert.Equal((byte)130, club.OffsideTrap);

            // No co-/goalkeeper trainer: nothing to edit; with one the inputs follow.
            Assert.Equal("keiner", Field<TextBox>(view, "CoTrainerFirstnameInput").Text);
            Assert.False(Field<ComboBox>(view, "CoTrainerCompetenceInput").Enabled);
            Assert.False(Field<TextBox>(view, "GoalkeeperCoachSalaryInput").Enabled);
            Assert.Equal(16, Field<ComboBox>(view, "GoalkeeperCoachCompetenceInput").Items.Count);
            club.CoTrainerId = 4;
            club.CoTrainerSalary = 250000;
            Assert.True(Field<ComboBox>(view, "CoTrainerCompetenceInput").Enabled);
            Assert.Equal("250000", Field<TextBox>(view, "CoTrainerSalaryInput").Text);
        }, tab: "TacticsTab");

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

        // Licence refused (0x90), requirements: no camp, debt limit 3, salary limit 120, transfer surplus 40.
        private static void SeedRequirements(FakeModule fake)
        {
            fake.WriteDisplayCacheBytes("FF", new byte[] { 0x90, 0x5A });
            fake.WriteDisplayCacheBytes("494", new byte[] { 3, 0, 120, 0, 40, 0 });
        }

        [Fact]
        public void Requirements_AreReadAndWrittenToEveryCopy()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedOwnClub(fake);
                SeedRequirements(fake);
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;

                Assert.True(club.LicenceRefused);
                Assert.True(club.HasRequirement(ClubEnums.Requirement.NoTrainingCamp));
                Assert.True(club.HasRequirement(ClubEnums.Requirement.DebtLimit));
                Assert.True(club.HasRequirement(ClubEnums.Requirement.TransferSurplus));
                Assert.True(club.HasRequirement(ClubEnums.Requirement.SalaryLimit));
                Assert.False(club.HasRequirement(ClubEnums.Requirement.NoStadiumConstruction));
                Assert.Equal((byte)3, club.DebtLimit);
                Assert.Equal((ushort)120, club.SalaryLimit);
                Assert.Equal((short)40, club.TransferLimit);

                club.LicenceRefused = false;
                club.SetRequirement(ClubEnums.Requirement.NoTrainingCamp, false);
                club.SetRequirement(ClubEnums.Requirement.SalaryLimit, false);
                club.SetRequirement(ClubEnums.Requirement.TransferLimit, true);
                club.SpectatorLimit = 25;
                controller.Save();

                foreach (Func<uint, int, byte[]> copy in new Func<uint, int, byte[]>[]
                {
                    (offset, length) => fake.ReadDisplayCacheBytes(offset.ToString("X"), length),
                    (offset, length) => fake.ReadModuleBytes(OtherCopy + offset, length),
                })
                {
                    // Bit 4 of the licence byte stays, as when the game grants the licence.
                    Assert.Equal(new byte[] { 0x10, 0x28 }, copy(0xFF, 2));
                    Assert.Equal(new byte[] { 3, 25, 0, 0, 40, 0 }, copy(0x494, 6));
                }
                Assert.Equal(new byte[] { 0x10, 0x28 }, fake.ReadModuleBytes(Record(OwnId) + 0xF5, 2));
                Assert.Equal(new byte[] { 3, 25, 0, 0 }, fake.ReadModuleBytes(Record(OwnId) + 0x4E4, 4));
            }
        }

        // The "Verband" tab, checked inside IdentityTab_ShowsTheSettings (each Trainer window costs the x86
        // test host ~20 MB); needs SeedRequirements before the view's refresh.
        private static void CheckAssociationTab(ClubView view)
        {
            Field<TabControl>(view, "ClubTabControl").SelectedTab = Field<TabPage>(view, "associationTab");
            Application.DoEvents();
            // Saving shows the same club again after clearing every box: that must not reach the club.
            view.RefreshValues(PlayerEnums.AddressType.OWN, ShownClub(view));
            Assert.True(ShownClub(view).HasRequirement(ClubEnums.Requirement.NoTrainingCamp));
            Assert.True(ShownClub(view).LicenceRefused);
            Club club = ShownClub(view);
            Dictionary<ClubEnums.Requirement, CheckBox> checks = Field<Dictionary<ClubEnums.Requirement, CheckBox>>(view, "requirementInputs");
            Assert.Equal(7, checks.Count);
            Assert.True(checks[ClubEnums.Requirement.NoTrainingCamp].Checked);
            Assert.True(Field<TextBox>(view, "transferSurplusInput").Enabled);
            Assert.False(Field<TextBox>(view, "transferLimitInput").Enabled);
            Assert.Equal("40", Field<TextBox>(view, "transferSurplusInput").Text);
            Assert.True(Field<CheckBox>(view, "licenceRefusedInput").Checked);

            checks[ClubEnums.Requirement.NoTrainingCamp].Checked = false;
            Assert.False(club.HasRequirement(ClubEnums.Requirement.NoTrainingCamp));

            // The transfer requirements exclude each other.
            checks[ClubEnums.Requirement.TransferLimit].Checked = true;
            Assert.False(checks[ClubEnums.Requirement.TransferSurplus].Checked);
            Assert.True(Field<TextBox>(view, "transferLimitInput").Enabled);

            Field<CheckBox>(view, "licenceRefusedInput").Checked = false;
            Assert.False(club.LicenceRefused);

            // Locks: the bribery bits and the camp, as set in the game.
            var locks = Field<List<KeyValuePair<CheckBox, Func<Club, bool>>>>(view, "lockInputs");
            Assert.Equal(4, locks.Count);
            locks[0].Key.Checked = true;
            Assert.Equal((byte)0x80, club.Bribery);
            locks[3].Key.Checked = true;
            Assert.Equal(0x800u, club.ShortCamp);
            locks[0].Key.Checked = false;
            locks[3].Key.Checked = false;
            Assert.Equal((byte)0, club.Bribery);
            Assert.Equal(0u, club.ShortCamp);

            // No match running (the match clock is past the test module; see LiveMatchTests for one).
            Assert.Equal("Kein laufendes Spiel", Field<Label>(view, "liveStatusValue").Text);
            Assert.False(Field<GroupBox>(view, "liveTacticsBox").Enabled);
        }
        #endregion
    }
}
