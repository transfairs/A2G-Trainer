using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// PlayerController: a league ban whose end day can't be read, a youth player made younger
    /// than a full group allows, and a roster slot that can't be read any more.
    /// </summary>
    public class PlayerControllerCoverageTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");
        private const ushort CurrentYear = 2001;
        private static uint PlayerRecord(ushort id) => Settings.PlayerRecordTableOffset + id * Settings.PlayerRecordStride;

        [Fact]
        public void ReadPersistentConstitution_WithoutAReadableBanEnd_KeepsTheBanUnset()
        {
            // Player 114's record ends on the next page: the ban fields lie before it, the ban's end day on it.
            const ushort id = 114;
            uint record = PlayerRecord(id);
            const uint page = 0x51C000;
            Assert.True(record + Settings.PlayerRecordLeagueBanOffset + 2 <= page && record + Settings.PlayerRecordLeagueBanEndOffset >= page);
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.ID], BitConverter.GetBytes(id));
                fake.WriteModuleBytes(record + Settings.PlayerRecordInjuryOffset, new byte[] { 21 });
                fake.WriteModuleBytes(record + Settings.PlayerRecordLeagueBanOffset, new byte[] { 0x82, 0x80 });
                fake.WriteModuleBytes(record + Settings.PlayerRecordLeagueBanEndOffset, BitConverter.GetBytes((ushort)30));
                Club club = new Club { PlayerCount = 1, AmateurPlayerCount = 0 };

                Player readable = new PlayerController(fake.Memory, club, isGog: false, PlayerEnums.AddressType.OWN).EntityList.Single();
                Assert.Equal((byte)0x82, readable.LeagueBanState);
                Assert.Equal((ushort)30, readable.LeagueBanEndDay);

                fake.MakeModuleRegionUnreadable(page, 1);
                Player player = new PlayerController(fake.Memory, club, isGog: false, PlayerEnums.AddressType.OWN).EntityList.Single();

                Assert.Equal((byte)21, player.Injury);
                Assert.Equal((byte)0, player.LeagueBanState);
                Assert.Equal((ushort)0, player.LeagueBanEndDay);
            }
        }

        [Fact]
        public void ReadPersistentConstitution_WithoutAReadableBan_KeepsTheBanUnset()
        {
            // A record table placed so that player 0's constitution block ends right before an
            // unreadable page and the ban fields lie on it.
            const uint page = 0x51B000;
            uint table = page - 0x38;
            Assert.True(table + Settings.PlayerRecordInjuryStartOffset + 0xD <= page && table + Settings.PlayerRecordLeagueBanOffset >= page);
            using (FakeModule fake = new FakeModule())
            {
                fake.Memory.Layout = new PersistentLayout(table, null, null, null, isVerified: true);
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                fake.WriteModuleBytes(table + Settings.PlayerRecordInjuryOffset, new byte[] { 21 });
                fake.WriteModuleBytes(table + Settings.PlayerRecordLeagueBanOffset, new byte[] { 0x82, 0x80 });
                fake.MakeModuleRegionUnreadable(page, 1);

                Player player = new PlayerController(fake.Memory, new Club { PlayerCount = 1, AmateurPlayerCount = 0 }, isGog: false, PlayerEnums.AddressType.OWN).EntityList.Single();

                Assert.Equal((byte)21, player.Injury);
                Assert.Equal((byte)0, player.LeagueBanState);
            }
        }

        [Fact]
        public void Save_ForAYouthPlayerMadeYoungerIntoAFullGroup_SaysWhyHeStays()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.Memory.Layout = new PersistentLayout(Settings.PlayerRecordTableOffset, null, Settings.AgeReferenceYearOffset, Settings.ActiveTrainerCountOffset, isVerified: true, hasOriginalBuildAnchors: true);
                fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes(CurrentYear));
                // Club record 2 "Arsenal" with an A youth of one and a full C youth.
                uint clubRecord = Settings.ClubRecordTableOffset + 2 * Settings.ClubRecordStride;
                fake.WriteModuleBytes(clubRecord, Latin1.GetBytes("Arsenal\0"));
                fake.WriteModuleBytes(clubRecord + Settings.ClubRecordCountryOffset, new[] { (byte)PlayerEnums.Country.England });
                byte[] youth = new byte[Settings.YouthStride];
                ushort[][] groups = { new ushort[] { 31 }, new ushort[0], new ushort[] { 35, 36, 37 } };
                for (int g = 0; g < 3; g++)
                {
                    youth[1 + g] = (byte)groups[g].Length;
                    groups[g].SelectMany(BitConverter.GetBytes).ToArray().CopyTo(youth, (int)Settings.YouthIdOffsets[g]);
                }
                fake.WriteModuleBytes(Settings.YouthTableOffset + 2 * Settings.YouthStride, youth);
                fake.WriteModuleBytes(PlayerRecord(31) + Settings.PlayerRecordAgeOffset, BitConverter.GetBytes((ushort)(CurrentYear - 17)));
                foreach (ushort id in new ushort[] { 35, 36, 37 })
                    fake.WriteModuleBytes(PlayerRecord(id) + Settings.PlayerRecordAgeOffset, BitConverter.GetBytes((ushort)(CurrentYear - 13)));
                ClubSquads.Entry arsenal = ClubSquads.Clubs(fake.Memory, FixedAddresses.For(fake.Memory)).Single(c => c.Name == "Arsenal");
                PlayerController controller = new PlayerController(fake.Memory, arsenal, isGog: false, PlayerEnums.AddressType.TRAINEE);
                Player player = controller.EntityList.Single(p => p.NameRecordId == 31);
                player.Age = 13;

                List<string> warnings = controller.Save(player);

                Assert.Single(warnings);
                Assert.EndsWith("Die C-Jugend ist voll, er bleibt in der A-Jugend.", warnings[0]);
                Assert.Equal(new ushort[] { 31, 35, 36, 37 }, ClubSquads.YouthIds(fake.Memory, FixedAddresses.For(fake.Memory), arsenal));
            }
        }

        [Fact]
        public void IsStillLoaded_WhenTheRosterSlotCantBeRead_IsFalse()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_PLAYERS[PlayerEnums.AddressKey.ID], BitConverter.GetBytes((ushort)3));
                PlayerController controller = new PlayerController(fake.Memory, new Club { PlayerCount = 1, AmateurPlayerCount = 0 }, isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();
                Assert.True(controller.IsStillLoaded(player));

                // The game dropped the roster: its pointer is null.
                fake.WriteModuleBytes(Convert.ToUInt32(Settings.PlayerAddress[0], 16), new byte[4]);

                Assert.False(controller.IsStillLoaded(player));
            }
        }
    }
}
