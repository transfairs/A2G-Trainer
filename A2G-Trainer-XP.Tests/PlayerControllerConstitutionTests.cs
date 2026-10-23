using System;
using System.Linq;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for the persistent constitution fields (injury, vulnerability, suspension, doping,
    /// yellow cards) and the calendar conversions they rely on (GameCalendar).
    /// </summary>
    public class PlayerControllerConstitutionTests
    {
        private const short StartYear = 2001;
        private const short Today = 100;          // day counter: 100 days after 1 Jan 2001
        private const short GlobalCounter = 7;

        private static Addresses Own => AddressPresets.OWN_PLAYERS;
        private static uint Record => Settings.PlayerRecordTableOffset;  // PlayerId 0

        private static Club NewSinglePlayerClub() => new Club { PlayerCount = 1, AmateurPlayerCount = 0 };

        private static void WriteInt16s(FakeModule fake, uint offset, params short[] values) =>
            fake.WriteModuleBytes(offset, values.SelectMany(v => BitConverter.GetBytes(v)).ToArray());

        private static void SeedPlayerAndCalendar(FakeModule fake)
        {
            fake.RoutePointerSlot(Settings.PlayerAddress[0]);
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.ID], BitConverter.GetBytes((ushort)0));

            WriteInt16s(fake, Settings.StartYearOffset, StartYear);
            WriteInt16s(fake, Settings.DayCounterOffset, Today);
            WriteInt16s(fake, Settings.DaysPerYearOffset, 365);
            WriteInt16s(fake, Settings.GlobalCounterOffset, GlobalCounter);
            // Calendar table: day counter -> (year, day of year); 365 days per year from 1 Jan 2001.
            for (short day = 0; day < 800; day++)
                WriteInt16s(fake, Settings.CalendarTableOffset + (uint)day * 4, (short)(StartYear + day / 365), (short)(day % 365));
        }

        private static Player LoadPlayer(FakeModule fake, out PlayerController controller)
        {
            controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
            return controller.EntityList.Single();
        }

        [Fact]
        public void ReadPersistentFields_DecodesTheConstitutionBlock()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                // Injured since counter 3, until day 110 of 2001 (10 days from today), type 21;
                // vulnerable until counter 12 (5 ticks left); 2 matches banned; doping countdown
                // running (0x0C) plus an unrelated marker bit; 4 yellow cards.
                WriteInt16s(fake, Record + 0x2A, 3, StartYear, 110);
                fake.WriteModuleBytes(Record + 0x30, new byte[] { 21 });
                WriteInt16s(fake, Record + 0x32, GlobalCounter + 5);
                fake.WriteModuleBytes(Record + 0x34, new byte[] { 2, 0x4C, 4 });

                Player player = LoadPlayer(fake, out _);

                Assert.Equal((ushort)10, player.InjuredDays);
                Assert.Equal((byte)21, player.Injury);
                Assert.True(player.Vulnerable);
                Assert.Equal((byte)5, player.VulnerableTicks);
                Assert.Equal((byte)2, player.RedCardBannedMatches);
                Assert.True(player.Doped);
                Assert.Equal((byte)0x4C, player.DopingState);
                Assert.Equal((byte)4, player.YellowCardsSeason);
            }
        }

        [Fact]
        public void ReadPersistentFields_WithInjuryEndInThePast_ReadsZeroDays()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                WriteInt16s(fake, Record + 0x2A, 3, StartYear, 90);

                Assert.Equal((ushort)0, LoadPlayer(fake, out _).InjuredDays);
            }
        }

        [Fact]
        public void ReadPersistentFields_WithOnlyAMarkerBitInTheDopingByte_IsNotDoped()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                fake.WriteModuleBytes(Record + 0x35, new byte[] { 0x80 });

                Assert.False(LoadPlayer(fake, out _).Doped);
            }
        }

        [Fact]
        public void Save_NewInjury_StoresTodaysCounterAsStartAndTheCalendarEndDate()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                Player player = LoadPlayer(fake, out PlayerController controller);
                player.InjuredDays = 300;   // day counter 400 -> 2002, day 35
                player.Injury = 12;

                controller.Save(player);

                Assert.Equal(new short[] { GlobalCounter, 2002, 35 }, ReadInt16s(fake, Record + 0x2A, 3));
                Assert.Equal((byte)12, fake.ReadModuleBytes(Record + 0x30, 1)[0]);
            }
        }

        [Fact]
        public void Save_ExistingInjury_KeepsItsStartCounter()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                WriteInt16s(fake, Record + 0x2A, 3, StartYear, 110);
                Player player = LoadPlayer(fake, out PlayerController controller);
                player.InjuredDays = 20;

                controller.Save(player);

                Assert.Equal(new short[] { 3, StartYear, 120 }, ReadInt16s(fake, Record + 0x2A, 3));
            }
        }

        [Fact]
        public void Save_HealedInjury_ClearsStartEndDateAndTheClearedFlag()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                WriteInt16s(fake, Record + 0x2A, 3, StartYear, 110);
                fake.WriteModuleBytes(Record + Settings.PlayerRecordInjuryClearedFlagOffset, new byte[] { 1 });
                Player player = LoadPlayer(fake, out PlayerController controller);
                player.InjuredDays = 0;

                controller.Save(player);

                Assert.Equal(new byte[6], fake.ReadModuleBytes(Record + 0x2A, 6));
                Assert.Equal((byte)0, fake.ReadModuleBytes(Record + Settings.PlayerRecordInjuryClearedFlagOffset, 1)[0]);
            }
        }

        [Fact]
        public void Save_SwitchingVulnerableOn_UsesTheGamesUsualDuration_AndOffClearsIt()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                Player player = LoadPlayer(fake, out PlayerController controller);

                player.Vulnerable = true;
                controller.Save(player);
                Assert.Equal((ushort)(GlobalCounter + Player.DefaultVulnerableTicks), BitConverter.ToUInt16(fake.ReadModuleBytes(Record + 0x32, 2), 0));
                Assert.Equal(Player.DefaultVulnerableTicks, fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.VULNERABLE], 1)[0]);

                player.Vulnerable = false;
                controller.Save(player);
                Assert.Equal((ushort)0, BitConverter.ToUInt16(fake.ReadModuleBytes(Record + 0x32, 2), 0));
            }
        }

        [Fact]
        public void Save_StillVulnerable_KeepsTheRemainingDuration()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                WriteInt16s(fake, Record + 0x32, GlobalCounter + 2);
                Player player = LoadPlayer(fake, out PlayerController controller);

                controller.Save(player);

                Assert.Equal((ushort)(GlobalCounter + 2), BitConverter.ToUInt16(fake.ReadModuleBytes(Record + 0x32, 2), 0));
            }
        }

        [Fact]
        public void Save_Doping_SetsAndClearsOnlyTheDopingBits()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                fake.WriteModuleBytes(Record + 0x35, new byte[] { 0x80 });
                Player player = LoadPlayer(fake, out PlayerController controller);

                player.Doped = true;
                controller.Save(player);
                Assert.Equal((byte)0x8C, fake.ReadModuleBytes(Record + 0x35, 1)[0]);
                Assert.Equal((byte)0x8C, fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.DOPED], 1)[0]);

                player.DopingState = 0x8C;
                player.Doped = false;
                controller.Save(player);
                Assert.Equal((byte)0x80, fake.ReadModuleBytes(Record + 0x35, 1)[0]);
            }
        }

        [Fact]
        public void Save_WithoutCalendar_WritesThePlainBytesButLeavesDaysAndVulnerabilityAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                WriteInt16s(fake, Record + 0x2A, 3, StartYear, 110);
                WriteInt16s(fake, Record + 0x32, 0x1234);
                fake.Memory.Layout = new PersistentLayout(Settings.PlayerRecordTableOffset, null, null, null, isVerified: true);
                Player player = LoadPlayer(fake, out PlayerController controller);
                player.InjuredDays = 0;
                player.Vulnerable = false;
                player.RedCardBannedMatches = 3;

                controller.Save(player);

                Assert.Equal(new short[] { 3, StartYear, 110 }, ReadInt16s(fake, Record + 0x2A, 3));
                Assert.Equal((ushort)0x1234, BitConverter.ToUInt16(fake.ReadModuleBytes(Record + 0x32, 2), 0));
                Assert.Equal((byte)3, fake.ReadModuleBytes(Record + 0x34, 1)[0]);
            }
        }

        [Fact]
        public void GameCalendar_EndDateOutsideTheTable_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                GameCalendar calendar = GameCalendar.Read(fake.Memory);

                Assert.Null(calendar.EndDateForInjuredDays(5000));
            }
        }

        [Fact]
        public void GameCalendar_EndDateBeyondReadableMemory_IsNull()
        {
            // The module ends a little after the calendar table starts.
            using (FakeModule fake = new FakeModule(moduleBlockSize: ((int)Settings.CalendarTableOffset + 0x1000) & ~0xFFF))
            {
                WriteInt16s(fake, Settings.StartYearOffset, StartYear);
                WriteInt16s(fake, Settings.DayCounterOffset, Today);
                WriteInt16s(fake, Settings.DaysPerYearOffset, 365);
                GameCalendar calendar = GameCalendar.Read(fake.Memory);

                Assert.Null(calendar.EndDateForInjuredDays(5000));
            }
        }

        [Fact]
        public void Save_InjuryEndingOutsideTheCalendar_LeavesTheStoredInjuryAlone()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                WriteInt16s(fake, Record + 0x2A, 3, StartYear, 110);
                Player player = LoadPlayer(fake, out PlayerController controller);
                player.InjuredDays = 5000;

                controller.Save(player);

                Assert.Equal(new short[] { 3, StartYear, 110 }, ReadInt16s(fake, Record + 0x2A, 3));
            }
        }

        [Fact]
        public void DopingStateToWrite_WhileTheDopingCountdownRuns_KeepsTheStoredState()
        {
            Player player = new Player { DopingState = 0x8A, Doped = true };

            Assert.Equal((byte)0x8A, player.DopingStateToWrite);
        }

        [Fact]
        public void GameCalendar_WithoutOriginalBuildAnchors_IsUnavailable()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedPlayerAndCalendar(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);

                Assert.Null(GameCalendar.Read(fake.Memory));
            }
        }

        private static short[] ReadInt16s(FakeModule fake, uint offset, int count)
        {
            byte[] bytes = fake.ReadModuleBytes(offset, count * 2);
            return Enumerable.Range(0, count).Select(i => BitConverter.ToInt16(bytes, i * 2)).ToArray();
        }
    }
}
