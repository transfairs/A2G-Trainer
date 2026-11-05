using System;
using System.Collections.Generic;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// The remaining branches of Club (requirements, public image stages), GameCosts' change
    /// notification, GameRates' guards and LeagueController's dismissal flag and blocking check.
    /// </summary>
    public class ClubFinanceCoverageTests
    {
        [Fact]
        public void SetRequirement_TransferSurplus_LiftsTheTransferLimit_AndViceVersa()
        {
            Club club = new Club();
            club.SetRequirement(ClubEnums.Requirement.TransferLimit, true);
            club.SetRequirement(ClubEnums.Requirement.DebtLimit, true);

            club.SetRequirement(ClubEnums.Requirement.TransferSurplus, true);

            Assert.True(club.HasRequirement(ClubEnums.Requirement.TransferSurplus));
            Assert.False(club.HasRequirement(ClubEnums.Requirement.TransferLimit));
            Assert.True(club.HasRequirement(ClubEnums.Requirement.DebtLimit));

            club.SetRequirement(ClubEnums.Requirement.TransferLimit, true);
            Assert.False(club.HasRequirement(ClubEnums.Requirement.TransferSurplus));
            Assert.True(club.HasRequirement(ClubEnums.Requirement.TransferLimit));

            club.SetRequirement(ClubEnums.Requirement.TransferLimit, false);
            Assert.Equal((byte)ClubEnums.Requirement.DebtLimit, club.Requirements);
        }

        [Theory]
        [InlineData(50, ClubEnums.PublicImage.SehrBeliebt)]
        [InlineData(45, ClubEnums.PublicImage.SehrBeliebt)]
        [InlineData(44, ClubEnums.PublicImage.RechtAngesehen)]
        [InlineData(35, ClubEnums.PublicImage.RechtAngesehen)]
        [InlineData(34, ClubEnums.PublicImage.Neutral)]
        [InlineData(25, ClubEnums.PublicImage.Neutral)]
        [InlineData(24, ClubEnums.PublicImage.WenigAngesehen)]
        [InlineData(15, ClubEnums.PublicImage.WenigAngesehen)]
        [InlineData(14, ClubEnums.PublicImage.VollkommenVerhasst)]
        [InlineData(10, ClubEnums.PublicImage.VollkommenVerhasst)]
        public void PublicImageStage_FollowsTheGamesThresholds(byte value, ClubEnums.PublicImage stage)
        {
            Club club = new Club { PublicImage = value };

            Assert.Equal(stage, club.PublicImageStage);

            club.PublicImageStage = stage;
            Assert.Equal(stage, club.PublicImageStage);
        }

        [Fact]
        public void GameCosts_RoundToTheGamesUnits_AndNotifyOnlyWhenSomeoneListens()
        {
            GameCosts costs = new GameCosts();
            costs.SmallStatistics = 14999;
            Assert.Equal(10000, costs.SmallStatistics);

            List<string> changed = new List<string>();
            costs.PropertyChanged += (s, e) => changed.Add(e.PropertyName);
            costs.LargeStatistics = 25000;
            costs.Computer = 149999;

            Assert.Equal(30000, costs.LargeStatistics);
            Assert.Equal(100000, costs.Computer);
            Assert.Equal(new[] { nameof(GameCosts.LargeStatistics), nameof(GameCosts.Computer) }, changed);
        }

        [Fact]
        public void GameRates_Read_WithAnUnreadableCorporationTax_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(Settings.InterestLoanOffset, BitConverter.GetBytes((short)500));
                Assert.NotNull(GameRates.Read(fake.Memory, FixedAddresses.Original));

                fake.MakeModuleRegionUnreadable(Settings.CorporationTaxOffset, 1);

                Assert.Null(GameRates.Read(fake.Memory, FixedAddresses.Original));
            }
        }

        [Fact]
        public void GameRates_Write_WithoutRatesOrCosts_WritesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.WriteModuleBytes(Settings.InterestLoanOffset, BitConverter.GetBytes((short)500));
                fake.WriteModuleBytes(Settings.CorporationTaxOffset, new byte[] { 30 });
                fake.WriteModuleBytes(Settings.DepartmentCostTableOffset, new byte[] { 0, 0, 2, 0 });

                GameRates.Write(fake.Memory, FixedAddresses.Original, null);
                GameRates.Write(fake.Memory, null, new InterestRates { Loan = 9m });
                GameRates.WriteCosts(fake.Memory, FixedAddresses.Original, null);
                GameRates.WriteCosts(fake.Memory, null, new GameCosts());

                Assert.Equal((short)500, BitConverter.ToInt16(fake.ReadModuleBytes(Settings.InterestLoanOffset, 2), 0));
                Assert.Equal((byte)30, fake.ReadModuleBytes(Settings.CorporationTaxOffset, 1)[0]);
                Assert.Equal(new byte[] { 0, 0, 2, 0 }, fake.ReadModuleBytes(Settings.DepartmentCostTableOffset, 4));
            }
        }

        [Fact]
        public void LeagueController_Save_WritesWithDismissalsSwitchedOn()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.CountrySelectionAddress[0]);
                string offset = AddressPresets.LEAGUE_SETTINGS[LeagueEnums.AddressKey.WITH_DISMISSALS];
                LeagueController controller = new LeagueController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Assert.False(controller.League.WithDismissals);

                controller.League.WithDismissals = true;
                controller.Save();

                Assert.Equal((byte)1, fake.ReadDisplayCacheBytes(offset, 1)[0]);
                Assert.True(new LeagueController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN).League.WithDismissals);
            }
        }

        [Fact]
        public void DeactivationBlockedReason_TrainersTiedToOtherCountries_DoNotBlock()
        {
            Coach elsewhere = new Coach { Firstname = "Otto", Lastname = "Rehhagel", CurrentCountry = PlayerEnums.Country.Deutschland, CountryNextSeason = PlayerEnums.Country.Deutschland };
            Coach moving = new Coach { CurrentCountry = PlayerEnums.Country.Deutschland, CountryNextSeason = PlayerEnums.Country.England };

            Assert.Null(LeagueController.DeactivationBlockedReason(PlayerEnums.Country.Italien, new[] { elsewhere }));
            Assert.Equal("Ein Trainer wechselt zur nächsten Saison dorthin.",
                LeagueController.DeactivationBlockedReason(PlayerEnums.Country.England, new[] { elsewhere, moving }));
        }
    }
}
