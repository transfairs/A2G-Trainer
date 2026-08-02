using System;
using System.Reflection;
using System.Text;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for ClubController.Save writing every own-club field back to memory.</summary>
    public class ClubControllerSaveTests
    {
        // GetEntityList's per-slot try/catch is defensive against "a single bad slot must not abort
        // the 424-slot scan" - under normal operation, GetEntity never actually throws (an unreadable
        // address just yields default/zero field values, not an exception). The only way to make it
        // throw for real is to null out the controller's own memory field mid-scan; reflection-local
        // to this one controller instance, so it can't affect any other test.
        [Fact]
        public void GetEntityList_WhenMemoryBecomesNull_SkipsTheFailingSlotAndKeepsScanning()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.ALL, loadFullList: false);
                typeof(EntityController<Club>).GetField("memory", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(controller, null);

                System.Collections.Generic.List<Club> result = null;
                Exception thrown = Record.Exception(() => result = new System.Collections.Generic.List<Club>(controller.GetEntityList()));

                Assert.Null(thrown);
                Assert.NotNull(result);
            }
        }

        [Fact]
        public void Save_WithFloodLightOffAndGrassHeatingOn_WritesTheOppositeByteFromTheOtherTest()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                Addresses own = AddressPresets.OWN_CLUB;
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 15 });

                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                controller.Club.ClubName = "X";
                controller.Club.StadiumName = "Y";
                controller.Club.HasFloodLight = false;
                controller.Club.HasGrassHeating = true;

                controller.Save();

                Assert.Equal((byte)0, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.HasFloodLight], 1)[0]);
                Assert.Equal((byte)1, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.HasGrassHeating], 1)[0]);
            }
        }

        [Fact]
        public void Save_WritesIdentityFinancesAndStadiumFieldsBackToMemory()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                Addresses own = AddressPresets.OWN_CLUB;
                Encoding latin1 = Encoding.GetEncoding("iso-8859-1");
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 15 });

                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;
                club.ClubName = "Neuer Name";
                club.StadiumName = "Neues Stadion";
                club.Wealth = 999888;
                club.EarningsLeagueGames = 111;
                club.EarningsFriendlyGames = 222;
                club.EarningsAds = 333;
                club.SponsorCash = 444;
                club.SponsorPeriod = 5;
                club.Respect = 61;
                club.Spirit = 62;
                club.Will2Win = 63;
                club.TeamCohesion = 64;
                club.FreeTickets = 7001;
                club.RoadGameSupport = 7002;
                club.Roof = ClubEnums.Roof.BlockB;
                club.DisplayUnit = ClubEnums.DisplayUnit.GrosseLED;
                club.HasFloodLight = true;
                club.HasGrassHeating = false;
                club.FieldCondition = ClubEnums.FieldCondition.Clean;
                club.BlockAWeeks = 1;
                club.BlockLWeeks = 12;
                club.BlockAStandings = 1001;
                club.BlockLStandings = 1012;
                club.BlockASeats = 2001;
                club.BlockLSeats = 2012;

                controller.Save();

                Assert.Equal("Neuer Name", latin1.GetString(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.NAME], 10)).Split('\0')[0]);
                Assert.Equal("Neues Stadion", latin1.GetString(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.StadiumName], 13)).Split('\0')[0]);
                Assert.Equal(999888, BitConverter.ToInt32(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.Wealth], 4), 0));
                Assert.Equal(111, BitConverter.ToInt32(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.EarningsLeagueGames], 4), 0));
                Assert.Equal(222, BitConverter.ToInt32(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.EarningsFriendlyGames], 4), 0));
                Assert.Equal(333, BitConverter.ToInt32(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.EarningsAds], 4), 0));
                Assert.Equal(444, BitConverter.ToInt32(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.SponsorCash], 4), 0));
                Assert.Equal((byte)5, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.SponsorPeriod], 1)[0]);
                Assert.Equal((byte)61, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.Respect], 1)[0]);
                Assert.Equal((byte)62, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.Spirit], 1)[0]);
                Assert.Equal((byte)63, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.Will2Win], 1)[0]);
                Assert.Equal((byte)64, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.TeamCohesion], 1)[0]);
                Assert.Equal((ushort)7001, BitConverter.ToUInt16(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.FreeTickets], 2), 0));
                Assert.Equal((ushort)7002, BitConverter.ToUInt16(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.RoadGameSupport], 2), 0));
                Assert.Equal((ushort)ClubEnums.Roof.BlockB, BitConverter.ToUInt16(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.ROOF], 2), 0));
                Assert.Equal((byte)ClubEnums.DisplayUnit.GrosseLED, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.DisplayUnit], 1)[0]);
                Assert.Equal((byte)1, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.HasFloodLight], 1)[0]);
                Assert.Equal((byte)0, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.HasGrassHeating], 1)[0]);
                Assert.Equal((byte)ClubEnums.FieldCondition.Clean, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.FieldCondition], 1)[0]);
                Assert.Equal((byte)1, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.BlockAWeeks], 1)[0]);
                Assert.Equal((byte)12, fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.BlockLWeeks], 1)[0]);
                Assert.Equal((ushort)1001, BitConverter.ToUInt16(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.BlockAStandings], 2), 0));
                Assert.Equal((ushort)1012, BitConverter.ToUInt16(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.BlockLStandings], 2), 0));
                Assert.Equal((ushort)2001, BitConverter.ToUInt16(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.BlockASeats], 2), 0));
                Assert.Equal((ushort)2012, BitConverter.ToUInt16(fake.ReadDisplayCacheBytes(own[ClubEnums.AddressKey.BlockLSeats], 2), 0));
            }
        }
    }
}
