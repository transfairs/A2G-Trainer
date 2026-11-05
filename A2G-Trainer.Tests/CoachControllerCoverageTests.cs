using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// CoachController's national team, friendlies and championship tips where parts of the game's
    /// data can't be read, plus the other trainers' national teams and an away friendly.
    /// </summary>
    public class CoachControllerCoverageTests
    {
        // Mitspieler 1 (trainer slot 0): his friendly block and the record fields the national team uses.
        private static uint FriendlyBlock => Settings.NationalFriendlyBlockOffset + Settings.NationalFriendlyBlockStride;
        private static uint RecordOffset(uint field) => Settings.CoachRecordTableOffset + Settings.CoachRecordStride + field;

        private static object Invoke(object target, string name, params object[] args) =>
            target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        private static void UseFixedAddresses(CoachController controller, FixedAddresses addresses) =>
            typeof(CoachController).GetField("fixedAddresses", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(controller, addresses);

        private static CoachController Load(FakeModule fake) => new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

        private static void WriteOriginalOfferCall(FakeModule fake)
        {
            uint site = fake.ModuleBase + Settings.OfferGenerateCallSiteOffset;
            int rel = unchecked((int)(fake.ModuleBase + Settings.OfferGenerateFunctionOffset - (site + 5)));
            fake.WriteModuleBytes(Settings.OfferGenerateCallSiteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
        }

        private static bool OfferCallRedirected(FakeModule fake)
        {
            byte[] call = fake.ReadModuleBytes(Settings.OfferGenerateCallSiteOffset, 5);
            return unchecked((uint)(fake.ModuleBase + Settings.OfferGenerateCallSiteOffset + 5 + BitConverter.ToInt32(call, 1))) != fake.ModuleBase + Settings.OfferGenerateFunctionOffset;
        }

        private static void SeedFriendlies(FakeModule fake, uint block)
        {
            fake.WriteModuleBytes(block + Settings.NationalFriendlyChosenOffset, new byte[]
            {
                (byte)PlayerEnums.Country.Schweiz, 0, 200, 0, 0, 0,
                (byte)PlayerEnums.Country.Belgien, 0, 200, 0, 1, 0,
                (byte)PlayerEnums.Country.Dänemark, 0, 200, 0, 0, 0
            });
            fake.WriteModuleBytes(block + Settings.NationalFriendlyResultsOffset, new byte[] { 0xFF, 0, 0xFF, 0, 0xFF, 0 });
        }

        private static void MakeHollandsCoach(FakeModule fake)
        {
            fake.WriteModuleBytes(RecordOffset(Settings.CoachRecordNationalTeamOffset), BitConverter.GetBytes((ushort)PlayerEnums.Country.Holland));
            fake.WriteModuleBytes(Settings.CoachSlotTableOffset + Settings.CoachSlotStride + Settings.CoachSlotNationalTeamOffset, BitConverter.GetBytes((ushort)PlayerEnums.Country.Holland));
        }

        [Fact]
        public void NationalTeamsOfOtherTrainers_ListsOnlyNationalTeamEntries()
        {
            using (FakeModule fake = new FakeModule())
            {
                // The national coach table moved into the free end of the FakeModule.
                const uint table = 0x51C000;
                uint shift = unchecked(table - Settings.NationalCoachTableOffset);
                InjectedFixedAddresses.Use(fake, new FixedAddresses(shift, 0, null, null));
                void Entry(int mitspieler, byte country, ushort club) =>
                    fake.WriteModuleBytes(table + (uint)mitspieler * Settings.NationalCoachEntryStride, new byte[] { country, 0 }.Concat(BitConverter.GetBytes(club)).ToArray());
                Entry(1, (byte)PlayerEnums.Country.Holland, 200);    // the trainer asked about
                Entry(2, (byte)PlayerEnums.Country.Italien, 200);
                Entry(3, 0, 200);                                      // no national team
                Entry(4, (byte)PlayerEnums.Country.Spanien, 17);      // a club, not a national team

                List<PlayerEnums.Country> teams = CoachController.NationalTeamsOfOtherTrainers(fake.Memory, 0);

                Assert.Equal(new[] { PlayerEnums.Country.Italien }, teams);
                Assert.Equal(new[] { PlayerEnums.Country.Holland, PlayerEnums.Country.Italien }, CoachController.NationalTeamsOfOtherTrainers(fake.Memory, 3));
            }
        }

        [Fact]
        public void WriteNationalTeam_ForANationalCoachWithUnreadableFriendlyCandidates_LeavesTheGameAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalOfferCall(fake);
                MakeHollandsCoach(fake);
                fake.WriteModuleBytes(FriendlyBlock, new byte[] { (byte)PlayerEnums.Country.Belgien });
                CoachController controller = Load(fake);
                Assert.True(controller.Coach.IsNationalCoach);
                controller.Coach.NationalSalary = 4711;
                fake.MakeModuleRegionUnreadable(FriendlyBlock, Settings.NationalFriendlyCandidatesLength);

                Invoke(controller, "WriteNationalTeam");

                Assert.False(OfferCallRedirected(fake));
                Assert.False(controller.Coach.IsNationalTeamPending);
                Assert.Equal(4711, BitConverter.ToInt32(fake.ReadModuleBytes(RecordOffset(Settings.CoachRecordNationalSalaryOffset), 4), 0));
            }
        }

        [Fact]
        public void WriteNationalTeam_ForANationalCoachWithFriendlyCandidates_LeavesTheGameAlone_AndWithoutThemAsksIt()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalOfferCall(fake);
                MakeHollandsCoach(fake);
                fake.WriteModuleBytes(FriendlyBlock, new byte[] { (byte)PlayerEnums.Country.Belgien });
                CoachController controller = Load(fake);

                Invoke(controller, "WriteNationalTeam");
                Assert.False(OfferCallRedirected(fake));
                Assert.False(controller.Coach.IsNationalTeamPending);

                fake.WriteModuleBytes(FriendlyBlock, new byte[] { 0 });
                Invoke(controller, "WriteNationalTeam");
                Assert.True(OfferCallRedirected(fake));
                Assert.True(controller.Coach.IsNationalTeamPending);
            }
        }

        [Fact]
        public void WriteNationalTeam_WithoutANationalTeam_ClearsTheContractAndLeavesTheGameAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalOfferCall(fake);
                fake.WriteModuleBytes(RecordOffset(Settings.CoachRecordNationalSalaryOffset), BitConverter.GetBytes(500));
                CoachController controller = Load(fake);
                Assert.True(controller.Coach.HasNationalTeam);
                Assert.False(controller.Coach.IsNationalCoach);

                Invoke(controller, "WriteNationalTeam");

                Assert.False(OfferCallRedirected(fake));
                Assert.Equal(0, controller.Coach.NationalSalary);
                Assert.Equal(0, BitConverter.ToInt32(fake.ReadModuleBytes(RecordOffset(Settings.CoachRecordNationalSalaryOffset), 4), 0));
            }
        }

        [Fact]
        public void ReadFriendlies_WithUnreadableChoices_ResultsOrSeasonDay_ListsNone()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeHollandsCoach(fake);
                SeedFriendlies(fake, FriendlyBlock);
                CoachController controller = Load(fake);
                Coach coach = controller.Coach;
                Assert.Equal(3, coach.Friendlies.Count);

                // Season day unreadable.
                fake.MakeModuleRegionUnreadable(Settings.GlobalCounterOffset, 2);
                Invoke(controller, "ReadFriendlies", coach);
                Assert.Empty(coach.Friendlies);

                // Results on an unreadable page, the choices right before it readable.
                const uint page = 0x4C0000;
                uint block = page - Settings.NationalFriendlyResultsOffset;
                uint shift = unchecked(block - FriendlyBlock);
                Assert.True(Settings.GlobalCounterOffset + shift < (uint)FakeModule.DefaultModuleBlockSize);
                SeedFriendlies(fake, block);
                fake.WriteModuleBytes(Settings.GlobalCounterOffset + shift, BitConverter.GetBytes((short)10));
                UseFixedAddresses(controller, new FixedAddresses(shift, 0, null, null));
                Invoke(controller, "ReadFriendlies", coach);
                Assert.Equal(3, coach.Friendlies.Count);
                fake.MakeModuleRegionUnreadable(page, 1);
                Invoke(controller, "ReadFriendlies", coach);
                Assert.Empty(coach.Friendlies);

                // Choices unreadable.
                UseFixedAddresses(controller, FixedAddresses.Original);
                fake.MakeModuleRegionUnreadable(FriendlyBlock + Settings.NationalFriendlyChosenOffset, 18);
                Invoke(controller, "ReadFriendlies", coach);
                Assert.Empty(coach.Friendlies);
            }
        }

        [Fact]
        public void WriteFriendlies_AnAwayMatch_IsMarkedAway()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeHollandsCoach(fake);
                CoachController controller = Load(fake);
                controller.Coach.Friendlies.Clear();
                controller.Coach.Friendlies.Add(new NationalFriendly { Index = 1, Opponent = PlayerEnums.Country.Italien, AtHome = false });
                controller.Coach.Friendlies.Add(new NationalFriendly { Index = 2, Opponent = PlayerEnums.Country.Spanien, AtHome = true, IsLocked = true });

                Invoke(controller, "WriteFriendlies");

                Assert.Equal(new byte[]
                {
                    0, 0, 0, 0, 0, 0,
                    (byte)PlayerEnums.Country.Italien, 0, 200, 0, 1, 0,
                    0, 0, 0, 0, 0, 0
                }, fake.ReadModuleBytes(FriendlyBlock + Settings.NationalFriendlyChosenOffset, 18));
            }
        }

        [Fact]
        public void WriteChampionTips_SkipsCountriesWhoseLeagueSystemCantBeRead()
        {
            using (FakeModule fake = new FakeModule())
            {
                CoachController controller = Load(fake);
                Assert.NotEmpty(controller.Coach.ChampionTips);
                ChampionTip tip = controller.Coach.ChampionTips.Values.First();
                uint at = ((uint?)Invoke(controller, "ChampionTipAddress", tip.Country)).Value;
                tip.Stake = 1234;

                fake.MakeModuleRegionUnreadable(Settings.LeagueSystemByCountryOffset, 0x100);
                Invoke(controller, "WriteChampionTips");

                Assert.Equal(0, BitConverter.ToInt32(fake.Memory.ReadBytesAtAddress(at, 4), 0));
            }
        }
    }
}
