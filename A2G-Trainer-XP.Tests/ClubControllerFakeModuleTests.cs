using System;
using System.Reflection;
using System.Text;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for ClubController's field reads/writes against a FakeModule.</summary>
    public class ClubControllerFakeModuleTests
    {
        [Fact]
        public void GetEntity_OwnType_ReadsEveryOwnSpecificFieldThroughTheNewReadHelpers()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                Addresses own = AddressPresets.OWN_CLUB;
                Encoding latin1 = Encoding.GetEncoding("iso-8859-1");

                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.NAME], latin1.GetBytes("Holstein Kiel\0"));
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.ID], new byte[] { 52 });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.COUNTRY], new byte[] { (byte)PlayerEnums.Country.Deutschland });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 15 });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.AMATEUR_PLAYER_COUNT], new byte[] { 1 });

                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.Wealth], BitConverter.GetBytes(1234567));
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.EarningsLeagueGames], BitConverter.GetBytes(111));
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.EarningsFriendlyGames], BitConverter.GetBytes(222));
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.EarningsAds], BitConverter.GetBytes(333));
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.SponsorCash], BitConverter.GetBytes(444));
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.SponsorPeriod], new byte[] { 5 });

                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.Respect], new byte[] { 61 });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.Spirit], new byte[] { 62 });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.Will2Win], new byte[] { 63 });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.TeamCohesion], new byte[] { 64 });

                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.FreeTickets], BitConverter.GetBytes((ushort)7001));
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.RoadGameSupport], BitConverter.GetBytes((ushort)7002));

                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.StadiumName], latin1.GetBytes("Holstein-Stadion\0"));

                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.ROOF], BitConverter.GetBytes((ushort)ClubEnums.Roof.BlockA));
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.DisplayUnit], new byte[] { (byte)ClubEnums.DisplayUnit.KleineLED });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.HasFloodLight], new byte[] { 1 });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.HasGrassHeating], new byte[] { 0 });
                fake.WriteDisplayCacheBytes(own[ClubEnums.AddressKey.FieldCondition], new byte[] { 0 });

                foreach (char letter in "ABCDEFGHIJKL")
                {
                    byte weeksValue = (byte)(letter - 'A' + 1);
                    ushort standingsValue = (ushort)(1000 + (letter - 'A'));
                    ushort seatsValue = (ushort)(2000 + (letter - 'A'));

                    fake.WriteDisplayCacheBytes(own[(ClubEnums.AddressKey)Enum.Parse(typeof(ClubEnums.AddressKey), $"Block{letter}Weeks")], new[] { weeksValue });
                    fake.WriteDisplayCacheBytes(own[(ClubEnums.AddressKey)Enum.Parse(typeof(ClubEnums.AddressKey), $"Block{letter}Standings")], BitConverter.GetBytes(standingsValue));
                    fake.WriteDisplayCacheBytes(own[(ClubEnums.AddressKey)Enum.Parse(typeof(ClubEnums.AddressKey), $"Block{letter}Seats")], BitConverter.GetBytes(seatsValue));
                }

                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;

                Assert.Equal("Holstein Kiel", club.ClubName);
                Assert.Equal((byte)52, club.Id);
                Assert.Equal(PlayerEnums.Country.Deutschland, club.Country);
                Assert.Equal((ushort)15, club.PlayerCount);
                Assert.Equal((byte)1, club.AmateurPlayerCount);

                Assert.Equal(1234567, club.Wealth);
                Assert.Equal(111, club.EarningsLeagueGames);
                Assert.Equal(222, club.EarningsFriendlyGames);
                Assert.Equal(333, club.EarningsAds);
                Assert.Equal(444, club.SponsorCash);
                Assert.Equal((byte)5, club.SponsorPeriod);

                Assert.Equal((byte)61, club.Respect);
                Assert.Equal((byte)62, club.Spirit);
                Assert.Equal((byte)63, club.Will2Win);
                Assert.Equal((byte)64, club.TeamCohesion);

                Assert.Equal((ushort)7001, club.FreeTickets);
                Assert.Equal((ushort)7002, club.RoadGameSupport);

                Assert.Equal("Holstein-Stadion", club.StadiumName);

                Assert.Equal(ClubEnums.Roof.BlockA, club.Roof);
                Assert.Equal(ClubEnums.DisplayUnit.KleineLED, club.DisplayUnit);
                Assert.True(club.HasFloodLight);
                Assert.False(club.HasGrassHeating);
                Assert.Equal(ClubEnums.FieldCondition.Clean, club.FieldCondition);

                foreach (char letter in "ABCDEFGHIJKL")
                {
                    byte expectedWeeks = (byte)(letter - 'A' + 1);
                    ushort expectedStandings = (ushort)(1000 + (letter - 'A'));
                    ushort expectedSeats = (ushort)(2000 + (letter - 'A'));

                    PropertyInfo weeksProp = typeof(Club).GetProperty($"Block{letter}Weeks");
                    PropertyInfo standingsProp = typeof(Club).GetProperty($"Block{letter}Standings");
                    PropertyInfo seatsProp = typeof(Club).GetProperty($"Block{letter}Seats");

                    Assert.Equal(expectedWeeks, (byte)weeksProp.GetValue(club));
                    Assert.Equal(expectedStandings, (ushort)standingsProp.GetValue(club));
                    Assert.Equal(expectedSeats, (ushort)seatsProp.GetValue(club));
                }
            }
        }
    }
}
