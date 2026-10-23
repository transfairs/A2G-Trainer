using System;
using System.Collections.Generic;
using System.Linq;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for PlayerController's persistent-record read/write paths (ReadPersistentFields/WritePersistentXxx) against a FakeModule.</summary>
    public class PlayerControllerPersistentRecordTests
    {
        private const ushort CurrentYear = 2001;

        private static Addresses Own => AddressPresets.OWN_PLAYERS;

        private static Club NewSinglePlayerClub() => new Club { PlayerCount = 1, AmateurPlayerCount = 0 };

        private static void SeedDisplayCache(FakeModule fake)
        {
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.ID], BitConverter.GetBytes((ushort)0));
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.AGE], new byte[] { 90 });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.LEVEL], new byte[] { 91 });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.SKIN], new byte[] { (byte)PlayerEnums.SkinColor.Black });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.HAIR], new byte[] { (byte)PlayerEnums.HairColor.Glatze });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.POSITION], new byte[] { (byte)PlayerEnums.Position.S });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.SECONDARY_1], new byte[] { (byte)PlayerEnums.Position.OM });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.SECONDARY_2], new byte[] { (byte)PlayerEnums.Position.DM });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.SKILLS], BitConverter.GetBytes((ushort)0x0101));
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.NEG_SKILLS], BitConverter.GetBytes((ushort)0x0202));
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.PERSONALITY], BitConverter.GetBytes((ushort)0x0404));
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.CHARACTER], new byte[] { (byte)PlayerEnums.Character.Skandalnudel });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.HEALTH], new byte[] { (byte)PlayerEnums.Health.Knieprobleme });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.UNHAPPY], BitConverter.GetBytes((ushort)0x0808));
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.HAPPY], new byte[] { (byte)PlayerEnums.Happy.TollePraemien });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.FORM], new byte[] { 92 });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.CONDITION], new byte[] { 93 });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.FRESHNESS], new byte[] { 94 });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.NATIONALITY], new byte[] { (byte)PlayerEnums.Country.Wales });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.SALARY], BitConverter.GetBytes((ushort)9001));
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.SHOWUP], BitConverter.GetBytes((ushort)9002));
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.FIRSTNAME], new byte[] { (byte)'Z', (byte)'Z', 0 });
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.LASTNAME], new byte[] { (byte)'Y', (byte)'Y', 0 });
        }

        private static void WritePersistentRecord(FakeModule fake, byte age = 24, byte level = 42)
        {
            fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes(CurrentYear));

            uint b = Settings.PlayerRecordTableOffset;
            fake.WriteModuleBytes(b + Settings.PlayerRecordSkinColorOffset, new byte[] { (byte)PlayerEnums.SkinColor.Dark, (byte)PlayerEnums.HairColor.Rot });
            fake.WriteModuleBytes(b + Settings.PlayerRecordAgeOffset, BitConverter.GetBytes((ushort)(CurrentYear - age)));
            fake.WriteModuleBytes(b + Settings.PlayerRecordLevelOffset, new byte[] { level });
            fake.WriteModuleBytes(b + Settings.PlayerRecordPositionOffset, new byte[] { (byte)PlayerEnums.Position.MD, (byte)PlayerEnums.Position.LV, (byte)PlayerEnums.Position.RV });
            fake.WriteModuleBytes(b + Settings.PlayerRecordSkillsOffset, Concat(BitConverter.GetBytes((ushort)0x0311), BitConverter.GetBytes((ushort)0x0522)));
            fake.WriteModuleBytes(b + Settings.PlayerRecordPersonalityOffset, Concat(BitConverter.GetBytes((ushort)0x0644), new byte[] { (byte)PlayerEnums.Character.Phlegma, (byte)PlayerEnums.Health.Robustheit }));
            fake.WriteModuleBytes(b + Settings.PlayerRecordUnhappyOffset, Concat(BitConverter.GetBytes((ushort)0x0955), new byte[] { (byte)PlayerEnums.Happy.TollerTrainer }));
            fake.WriteModuleBytes(b + Settings.PlayerRecordFormOffset, new byte[] { 20 });
            fake.WriteModuleBytes(b + Settings.PlayerRecordConditionOffset, new byte[] { 81, 85 });
            fake.WriteModuleBytes(b + Settings.PlayerRecordNationalityOffset, new byte[] { (byte)PlayerEnums.Country.Deutschland });
            fake.WriteModuleBytes(b + Settings.PlayerRecordSalaryOffset, Concat(BitConverter.GetBytes((ushort)1234), BitConverter.GetBytes((ushort)56), BitConverter.GetBytes((ushort)78), BitConverter.GetBytes((ushort)910)));
            // Contract ends 2004 and the player joined in 1997: 3 years left, 4 years in the club.
            fake.WriteModuleBytes(b + Settings.PlayerRecordContractEndYearOffset, Concat(BitConverter.GetBytes((ushort)2004), BitConverter.GetBytes((ushort)1997), new byte[] { (byte)PlayerEnums.Contract.OptionClub }));
            fake.WriteModuleBytes(b + Settings.PlayerRecordCareerOffset, new byte[] { (byte)PlayerEnums.Career.Retires });
        }

        private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

        [Fact]
        public void ReadPersistentFields_WithReadableRecord_OverridesDisplayCacheValues()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                SeedDisplayCache(fake);
                WritePersistentRecord(fake, age: 24, level: 42);

                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();

                Assert.Equal((byte)24, player.Age);
                Assert.Equal((byte)42, player.Level);
                Assert.Equal(PlayerEnums.SkinColor.Dark, player.SkinColor);
                Assert.Equal(PlayerEnums.HairColor.Rot, player.HairColor);
                Assert.Equal(PlayerEnums.Position.MD, player.Position);
                Assert.Equal(new[] { PlayerEnums.Position.LV, PlayerEnums.Position.RV }, player.SecondaryPositions);
                Assert.Equal((PlayerEnums.Skills)0x0311, player.Skills);
                Assert.Equal((PlayerEnums.Skills)0x0522, player.NegativeSkills);
                Assert.Equal((PlayerEnums.Personality)0x0644, player.Personality);
                Assert.Equal(PlayerEnums.Character.Phlegma, player.Character);
                Assert.Equal(PlayerEnums.Health.Robustheit, player.Health);
                Assert.Equal((PlayerEnums.Unhappy)0x0955, player.Unhappy);
                Assert.Equal(PlayerEnums.Happy.TollerTrainer, player.Happy);
                Assert.Equal((byte)20, player.Form);
                Assert.Equal((byte)81, player.Condition);
                Assert.Equal((byte)85, player.Freshness);
                Assert.Equal(PlayerEnums.Country.Deutschland, player.Nationality);
                Assert.Equal((ushort)1234, player.Salary);
                Assert.Equal((ushort)56, player.ShowUpBonus);
                Assert.Equal((ushort)78, player.GoalsBonus);
                Assert.Equal((ushort)910, player.TransferFee);
                Assert.Equal((byte)3, player.ContractDuration);
                Assert.Equal((byte)4, player.YearsInClub);
                Assert.Equal(PlayerEnums.Contract.OptionClub, player.ContractDetails);
                Assert.Equal(PlayerEnums.Career.Retires, player.Career);
            }
        }

        [Fact]
        public void ReadPersistentFields_WithExpiredContract_ClampsDurationAtZeroLikeTheGame()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                SeedDisplayCache(fake);
                WritePersistentRecord(fake);
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset + Settings.PlayerRecordContractEndYearOffset, BitConverter.GetBytes((ushort)(CurrentYear - 2)));

                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);

                Assert.Equal((byte)0, controller.EntityList.Single().ContractDuration);
            }
        }

        [Fact]
        public void ReadPersistentFields_WhenRecordUnreadable_KeepsDisplayCacheValues()
        {
            // The record is made unreadable explicitly. Shrinking the module block so the record lies
            // past its end used to be flaky: whatever Windows happened to map right after the block
            // (another test's allocation) was then read as the record.
            using (FakeModule fake = new FakeModule())
            {
                fake.MakeModuleRegionUnreadable(Settings.PlayerRecordTableOffset, (int)Settings.PlayerRecordStride);
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                SeedDisplayCache(fake);

                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();

                Assert.Equal((byte)90, player.Age);
                Assert.Equal((byte)91, player.Level);
                Assert.Equal(PlayerEnums.SkinColor.Black, player.SkinColor);
                Assert.Equal(PlayerEnums.HairColor.Glatze, player.HairColor);
                Assert.Equal(PlayerEnums.Position.S, player.Position);
                Assert.Equal(new[] { PlayerEnums.Position.OM, PlayerEnums.Position.DM }, player.SecondaryPositions);
                Assert.Equal((PlayerEnums.Skills)0x0101, player.Skills);
                Assert.Equal((PlayerEnums.Skills)0x0202, player.NegativeSkills);
                Assert.Equal((PlayerEnums.Personality)0x0404, player.Personality);
                Assert.Equal(PlayerEnums.Character.Skandalnudel, player.Character);
                Assert.Equal(PlayerEnums.Health.Knieprobleme, player.Health);
                Assert.Equal((PlayerEnums.Unhappy)0x0808, player.Unhappy);
                Assert.Equal(PlayerEnums.Happy.TollePraemien, player.Happy);
                Assert.Equal((byte)92, player.Form);
                Assert.Equal((byte)93, player.Condition);
                Assert.Equal((byte)94, player.Freshness);
                Assert.Equal(PlayerEnums.Country.Wales, player.Nationality);
                Assert.Equal((ushort)9001, player.Salary);
                Assert.Equal((ushort)9002, player.ShowUpBonus);
            }
        }

        [Fact]
        public void ReadPersistentFields_WhenOnlyCurrentYearUnreadable_KeepsDisplayCacheAgeButOverridesEverythingElse()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                SeedDisplayCache(fake);
                WritePersistentRecord(fake, age: 24, level: 42);
                fake.MakeModuleRegionUnreadable(Settings.AgeReferenceYearOffset, 2);

                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();

                Assert.Equal((byte)90, player.Age);
                Assert.Equal((byte)42, player.Level);
                Assert.Equal(PlayerEnums.Position.MD, player.Position);
            }
        }

        [Fact]
        public void Save_WritesEveryPersistentFieldWithExactBytes_AndAlsoUpdatesDisplayCacheForNonTrainee()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                SeedDisplayCache(fake);
                fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes(CurrentYear));

                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();

                player.Age = 31;
                player.Level = 77;
                player.SkinColor = PlayerEnums.SkinColor.Dark;
                player.HairColor = PlayerEnums.HairColor.Braun;
                player.Position = PlayerEnums.Position.DM;
                player.SecondaryPositions = new List<PlayerEnums.Position> { PlayerEnums.Position.RM, PlayerEnums.Position.LM };
                player.Skills = (PlayerEnums.Skills)0x0155;
                player.NegativeSkills = (PlayerEnums.Skills)0x0266;
                player.Personality = (PlayerEnums.Personality)0x0377;
                player.Character = PlayerEnums.Character.Musterprofi;
                player.Health = PlayerEnums.Health.Anfaelligkeit;
                player.Unhappy = (PlayerEnums.Unhappy)0x0488;
                player.Happy = PlayerEnums.Happy.TolleStimmung;
                player.Form = 60;
                player.Condition = 61;
                player.Freshness = 62;
                player.Nationality = PlayerEnums.Country.Italien;
                player.Salary = 4321;
                player.ShowUpBonus = 654;
                player.GoalsBonus = 987;
                player.TransferFee = 1500;
                player.ContractDuration = 2;
                player.YearsInClub = 6;
                player.ContractDetails = PlayerEnums.Contract.Leased | PlayerEnums.Contract.BuyOption;
                player.Career = PlayerEnums.Career.Retires;

                controller.Save(player);

                uint b = Settings.PlayerRecordTableOffset;
                Assert.Equal(Concat(BitConverter.GetBytes((ushort)987), BitConverter.GetBytes((ushort)1500)), fake.ReadModuleBytes(b + Settings.PlayerRecordGoalsBonusOffset, 4));
                // Stored as end year / join year, relative to the savegame's current year (2001).
                Assert.Equal(Concat(BitConverter.GetBytes((ushort)2003), BitConverter.GetBytes((ushort)1995), new byte[] { (byte)(PlayerEnums.Contract.Leased | PlayerEnums.Contract.BuyOption) }), fake.ReadModuleBytes(b + Settings.PlayerRecordContractEndYearOffset, 5));
                Assert.Equal((byte)PlayerEnums.Career.Retires, fake.ReadModuleBytes(b + Settings.PlayerRecordCareerOffset, 1)[0]);
                Assert.Equal(new byte[] { (byte)PlayerEnums.SkinColor.Dark, (byte)PlayerEnums.HairColor.Braun }, fake.ReadModuleBytes(b + Settings.PlayerRecordSkinColorOffset, 2));
                // Stored as the birth year (UInt16): 2001 - 31.
                Assert.Equal(BitConverter.GetBytes((ushort)1970), fake.ReadModuleBytes(b + Settings.PlayerRecordAgeOffset, 2));
                Assert.Equal((byte)77, fake.ReadModuleBytes(b + Settings.PlayerRecordLevelOffset, 1)[0]);
                Assert.Equal(new byte[] { (byte)PlayerEnums.Position.DM, (byte)PlayerEnums.Position.RM, (byte)PlayerEnums.Position.LM }, fake.ReadModuleBytes(b + Settings.PlayerRecordPositionOffset, 3));
                Assert.Equal(Concat(BitConverter.GetBytes((ushort)0x0155), BitConverter.GetBytes((ushort)0x0266)), fake.ReadModuleBytes(b + Settings.PlayerRecordSkillsOffset, 4));
                Assert.Equal(Concat(BitConverter.GetBytes((ushort)0x0377), new byte[] { (byte)PlayerEnums.Character.Musterprofi, (byte)PlayerEnums.Health.Anfaelligkeit }), fake.ReadModuleBytes(b + Settings.PlayerRecordPersonalityOffset, 4));
                Assert.Equal(Concat(BitConverter.GetBytes((ushort)0x0488), new byte[] { (byte)PlayerEnums.Happy.TolleStimmung }), fake.ReadModuleBytes(b + Settings.PlayerRecordUnhappyOffset, 3));
                Assert.Equal((byte)60, fake.ReadModuleBytes(b + Settings.PlayerRecordFormOffset, 1)[0]);
                Assert.Equal(new byte[] { 61, 62 }, fake.ReadModuleBytes(b + Settings.PlayerRecordConditionOffset, 2));
                Assert.Equal((byte)PlayerEnums.Country.Italien, fake.ReadModuleBytes(b + Settings.PlayerRecordNationalityOffset, 1)[0]);
                Assert.Equal(Concat(BitConverter.GetBytes((ushort)4321), BitConverter.GetBytes((ushort)654)), fake.ReadModuleBytes(b + Settings.PlayerRecordSalaryOffset, 4));

                Assert.Equal((byte)77, fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.LEVEL], 1)[0]);
                Assert.Equal((ushort)4321, BitConverter.ToUInt16(fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.SALARY], 2), 0));
            }
        }

        [Fact]
        public void Save_WhenCurrentYearUnreadable_SkipsAgeWriteButStillWritesOtherPersistentFields()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                SeedDisplayCache(fake);
                fake.MakeModuleRegionUnreadable(Settings.AgeReferenceYearOffset, 2);

                uint b = Settings.PlayerRecordTableOffset;
                fake.WriteModuleBytes(b + Settings.PlayerRecordAgeOffset, new byte[] { 0xAB, 0xCD });
                fake.WriteModuleBytes(b + Settings.PlayerRecordContractEndYearOffset, new byte[] { 0x11, 0x22, 0x33, 0x44 });

                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();
                player.Level = 55;
                player.ContractDetails = PlayerEnums.Contract.Seated;

                controller.Save(player);

                Assert.Equal(new byte[] { 0xAB, 0xCD }, fake.ReadModuleBytes(b + Settings.PlayerRecordAgeOffset, 2));
                Assert.Equal((byte)55, fake.ReadModuleBytes(b + Settings.PlayerRecordLevelOffset, 1)[0]);
                // Both years need the current year to encode, so they stay untouched; the details byte doesn't.
                Assert.Equal(new byte[] { 0x11, 0x22, 0x33, 0x44 }, fake.ReadModuleBytes(b + Settings.PlayerRecordContractEndYearOffset, 4));
                Assert.Equal((byte)PlayerEnums.Contract.Seated, fake.ReadModuleBytes(b + Settings.PlayerRecordContractDetailsOffset, 1)[0]);
            }
        }

        [Fact]
        public void Save_ForTrainee_SkipsDisplayCacheWrites_ButStillWritesPersistentFields()
        {
            AddressPresets.InitDynamicTeam("0");

            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.PlayerAddress[0]);
                SeedDisplayCache(fake);
                fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes(CurrentYear));

                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.TRAINEE);
                Player player = controller.EntityList.Single();
                player.Level = 77;
                player.Salary = 4321;
                player.ShowUpBonus = 654;
                player.Firstname = "Max";

                controller.Save(player);

                uint b = Settings.PlayerRecordTableOffset;
                Assert.Equal((byte)77, fake.ReadModuleBytes(b + Settings.PlayerRecordLevelOffset, 1)[0]);
                Assert.Equal(Concat(BitConverter.GetBytes((ushort)4321), BitConverter.GetBytes((ushort)654)), fake.ReadModuleBytes(b + Settings.PlayerRecordSalaryOffset, 4));

                Assert.Equal((byte)91, fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.LEVEL], 1)[0]);
                Assert.Equal((ushort)9001, BitConverter.ToUInt16(fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.SALARY], 2), 0));
                Assert.Equal((byte)'Z', fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.FIRSTNAME], 1)[0]);
            }
        }
    }
}
