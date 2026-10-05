using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for PersistentLayoutCalibrator against a FakeModule large enough to hold every anchor
    /// (incl. the GOG-shifted name-pool pointer), plus PlayerController's calibration hook.
    /// </summary>
    public class PersistentLayoutCalibratorTests
    {
        private const int ModuleSize = 0x820000;
        private const ushort CurrentYear = 2001;
        private const byte TrainerCount = 2;

        private static readonly uint GogTable = Settings.PlayerRecordTableOffset + Settings.GogOffset;
        private static readonly uint GogPool = Settings.NamePoolPointerOffset + Settings.GogOffset;
        private static readonly uint GogYear = Settings.AgeReferenceYearOffset + Settings.GogOffset;
        private static readonly uint GogTrainerCount = Settings.ActiveTrainerCountOffset + Settings.GogOffset;

        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        private static readonly ushort[] Ids = { 3, 17, 42, 100, 5 };
        private static readonly PlayerEnums.Position[] Positions = { PlayerEnums.Position.TO, PlayerEnums.Position.LV, PlayerEnums.Position.MD, PlayerEnums.Position.LM, PlayerEnums.Position.S };

        // The roster as the display cache shows it; pool string 2i is player i's Firstname, 2i+1 the Lastname.
        private static List<Player> NewRoster() => Ids.Select((id, i) => new Player
        {
            NameRecordId = id,
            Level = (byte)(120 + i * 7),
            Position = Positions[i],
            Age = (byte)(20 + i),
            Firstname = "Vorname" + i,
            Lastname = "Nachname" + i
        }).ToList();

        private static void WriteRecords(FakeModule fake, uint tableOffset, IEnumerable<Player> players)
        {
            int i = 0;
            foreach (Player p in players)
            {
                uint record = tableOffset + (uint)p.NameRecordId * Settings.PlayerRecordStride;
                fake.WriteModuleBytes(record, BitConverter.GetBytes((ushort)(2 * i)));
                fake.WriteModuleBytes(record + 2, BitConverter.GetBytes((ushort)(2 * i + 1)));
                fake.WriteModuleBytes(record + Settings.PlayerRecordAgeOffset, new[] { (byte)(CurrentYear - Settings.AgeReferenceYearEpoch - p.Age) });
                fake.WriteModuleBytes(record + Settings.PlayerRecordLevelOffset, new[] { p.Level, (byte)p.Position });
                i++;
            }
        }

        private static void WriteYear(FakeModule fake, uint yearOffset)
        {
            fake.WriteModuleBytes(yearOffset, BitConverter.GetBytes(CurrentYear));
            fake.WriteModuleBytes(yearOffset + (Settings.ActiveTrainerCountOffset - Settings.AgeReferenceYearOffset), new[] { TrainerCount });
        }

        // The pool lives outside the module, like the game's heap-allocated one.
        private sealed class Pool : IDisposable
        {
            public IntPtr Block { get; }
            public uint Address => (uint)this.Block.ToInt64();

            public Pool(int playerCount)
            {
                byte[] bytes = Enumerable.Range(0, playerCount)
                    .SelectMany(i => Latin1.GetBytes($"Vorname{i}\0Nachname{i}\0"))
                    .ToArray();
                this.Block = Marshal.AllocHGlobal(bytes.Length);
                Marshal.Copy(bytes, 0, this.Block, bytes.Length);
            }

            public void Dispose() => Marshal.FreeHGlobal(this.Block);
        }

        private static void WritePoolPointer(FakeModule fake, uint pointerOffset, Pool pool) =>
            fake.WriteModuleBytes(pointerOffset, BitConverter.GetBytes(pool.Address));

        [Fact]
        public void Calibrate_WithTooFewUsableSamples_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            {
                List<Player> roster = NewRoster().Take(PersistentLayoutCalibrator.MinSamples - 1).ToList();
                roster.Add(new Player { NameRecordId = 9, Level = 0 });
                WriteRecords(fake, GogTable, roster);

                Assert.Null(new PersistentLayoutCalibrator(fake.Memory).Calibrate(roster));
            }
        }

        [Fact]
        public void Calibrate_WithEverythingAtTheGogGuess_ConfirmsTheGuess()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            using (Pool pool = new Pool(Ids.Length))
            {
                List<Player> roster = NewRoster();
                WriteRecords(fake, GogTable, roster);
                WriteYear(fake, GogYear);
                WritePoolPointer(fake, GogPool, pool);

                PersistentLayout layout = new PersistentLayoutCalibrator(fake.Memory).Calibrate(roster);

                Assert.True(layout.IsVerified);
                Assert.Equal(GogTable, layout.PlayerRecordTableOffset);
                Assert.Equal(GogYear, layout.AgeReferenceYearOffset);
                Assert.Equal(GogTrainerCount, layout.ActiveTrainerCountOffset);
                Assert.Equal(GogPool, layout.NamePoolPointerOffset);
            }
        }

        [Fact]
        public void Calibrate_WithEverythingAtTheOriginalOffsets_FindsThoseInstead()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            using (Pool pool = new Pool(Ids.Length))
            {
                List<Player> roster = NewRoster();
                WriteRecords(fake, Settings.PlayerRecordTableOffset, roster);
                WriteYear(fake, Settings.AgeReferenceYearOffset);
                WritePoolPointer(fake, Settings.NamePoolPointerOffset, pool);

                PersistentLayout layout = new PersistentLayoutCalibrator(fake.Memory).Calibrate(roster);

                Assert.Equal(Settings.PlayerRecordTableOffset, layout.PlayerRecordTableOffset);
                Assert.Equal(Settings.AgeReferenceYearOffset, layout.AgeReferenceYearOffset);
                Assert.Equal(Settings.ActiveTrainerCountOffset, layout.ActiveTrainerCountOffset);
                Assert.Equal(Settings.NamePoolPointerOffset, layout.NamePoolPointerOffset);
            }
        }

        [Fact]
        public void Calibrate_WithEverythingShiftedElsewhere_ScansForTheTableAndAppliesItsShiftToTheRest()
        {
            const uint shift = 0x2468;
            using (FakeModule fake = new FakeModule(ModuleSize))
            using (Pool pool = new Pool(Ids.Length))
            {
                List<Player> roster = NewRoster();
                WriteRecords(fake, Settings.PlayerRecordTableOffset + shift, roster);
                WriteYear(fake, Settings.AgeReferenceYearOffset + shift);
                WritePoolPointer(fake, Settings.NamePoolPointerOffset + shift, pool);

                PersistentLayout layout = new PersistentLayoutCalibrator(fake.Memory).Calibrate(roster);

                Assert.Equal(Settings.PlayerRecordTableOffset + shift, layout.PlayerRecordTableOffset);
                Assert.Equal(Settings.AgeReferenceYearOffset + shift, layout.AgeReferenceYearOffset);
                Assert.Equal(Settings.NamePoolPointerOffset + shift, layout.NamePoolPointerOffset);
            }
        }

        [Fact]
        public void Calibrate_WithYearAndPoolNearButNotAtThePrediction_FindsThemInTheSearchWindow()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            using (Pool pool = new Pool(Ids.Length))
            {
                List<Player> roster = NewRoster();
                WriteRecords(fake, GogTable, roster);
                WriteYear(fake, GogYear + 0x40);
                // A decoy pointer right at the prediction, to a string that isn't a matching pool.
                IntPtr decoy = Marshal.AllocHGlobal(16);
                try
                {
                    Marshal.Copy(Latin1.GetBytes("Falsch\0Falsch\0\0"), 0, decoy, 15);
                    fake.WriteModuleBytes(GogPool, BitConverter.GetBytes((uint)decoy.ToInt64()));
                    WritePoolPointer(fake, GogPool - 0x80, pool);

                    PersistentLayout layout = new PersistentLayoutCalibrator(fake.Memory).Calibrate(roster);

                    Assert.Equal(GogTable, layout.PlayerRecordTableOffset);
                    Assert.Equal(GogYear + 0x40, layout.AgeReferenceYearOffset);
                    Assert.Equal(GogTrainerCount + 0x40, layout.ActiveTrainerCountOffset);
                    Assert.Equal(GogPool - 0x80, layout.NamePoolPointerOffset);
                }
                finally
                {
                    Marshal.FreeHGlobal(decoy);
                }
            }
        }

        [Fact]
        public void Calibrate_WithoutYearOrPool_ReturnsTheTableAndLeavesTheRestUnknown()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            {
                List<Player> roster = NewRoster();
                WriteRecords(fake, GogTable, roster);

                PersistentLayout layout = new PersistentLayoutCalibrator(fake.Memory).Calibrate(roster);

                Assert.Equal(GogTable, layout.PlayerRecordTableOffset);
                Assert.Null(layout.AgeReferenceYearOffset);
                Assert.Null(layout.ActiveTrainerCountOffset);
                Assert.Null(layout.NamePoolPointerOffset);
            }
        }

        [Fact]
        public void Calibrate_WithImplausibleTrainerCount_KeepsTheYearButNotTheCount()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            {
                List<Player> roster = NewRoster();
                WriteRecords(fake, GogTable, roster);
                WriteYear(fake, GogYear);
                fake.WriteModuleBytes(GogTrainerCount, new byte[] { 0 });

                PersistentLayout layout = new PersistentLayoutCalibrator(fake.Memory).Calibrate(roster);

                Assert.Equal(GogYear, layout.AgeReferenceYearOffset);
                Assert.Null(layout.ActiveTrainerCountOffset);
            }
        }

        [Fact]
        public void Calibrate_WithFewerThanTwoNamedSamples_SkipsTheNamePool()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            using (Pool pool = new Pool(Ids.Length))
            {
                List<Player> roster = NewRoster();
                foreach (Player p in roster.Skip(1))
                    p.Firstname = "";
                WriteRecords(fake, GogTable, roster);
                WritePoolPointer(fake, GogPool, pool);

                Assert.Null(new PersistentLayoutCalibrator(fake.Memory).Calibrate(roster).NamePoolPointerOffset);
            }
        }

        [Fact]
        public void Calibrate_WithNoMatchingTableAnywhere_ReturnsNull()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            {
                Assert.Null(new PersistentLayoutCalibrator(fake.Memory).Calibrate(NewRoster()));
            }
        }

        [Fact]
        public void Calibrate_WithTheTableMatchingInTwoPlaces_RefusesToGuess()
        {
            using (FakeModule fake = new FakeModule(ModuleSize))
            {
                List<Player> roster = NewRoster();
                WriteRecords(fake, 0x100000, roster);
                WriteRecords(fake, 0x200000, roster);

                Assert.Null(new PersistentLayoutCalibrator(fake.Memory).Calibrate(roster));
            }
        }

        [Fact]
        public void Calibrate_WithUnreadablePagesInTheModule_StillScansTheRest()
        {
            const uint table = 0x300000;
            using (FakeModule fake = new FakeModule(ModuleSize))
            {
                List<Player> roster = NewRoster();
                WriteRecords(fake, table, roster);
                fake.MakeModuleRegionUnreadable(0x10000, 0x1000);

                Assert.Equal(table, new PersistentLayoutCalibrator(fake.Memory).Calibrate(roster).PlayerRecordTableOffset);
            }
        }

        #region PlayerController hook

        private static Addresses Own => AddressPresets.OWN_PLAYERS;

        private static string At(string fieldOffset, int playerIndex) =>
            (Convert.ToUInt32(fieldOffset, 16) + (uint)playerIndex * Convert.ToUInt32(Settings.PlayerOffset, 16)).ToString("X");

        // Display-cache roster in the GOG layout (pointer slot PlayerAddress[1]), matching NewRoster().
        private static FakeModule NewGogFake(List<Player> roster)
        {
            FakeModule fake = new FakeModule(ModuleSize);
            fake.Memory.Layout = PersistentLayout.CreateGogGuess();
            fake.RoutePointerSlot(Settings.PlayerAddress[1]);
            for (int i = 0; i < roster.Count; i++)
            {
                Player p = roster[i];
                fake.WriteDisplayCacheBytes(At(Own[PlayerEnums.AddressKey.ID], i), BitConverter.GetBytes(p.NameRecordId));
                fake.WriteDisplayCacheBytes(At(Own[PlayerEnums.AddressKey.LEVEL], i), new[] { p.Level });
                fake.WriteDisplayCacheBytes(At(Own[PlayerEnums.AddressKey.POSITION], i), new[] { (byte)p.Position });
                fake.WriteDisplayCacheBytes(At(Own[PlayerEnums.AddressKey.AGE], i), new[] { p.Age });
                fake.WriteDisplayCacheBytes(At(Own[PlayerEnums.AddressKey.SALARY], i), BitConverter.GetBytes((ushort)500));
                fake.WriteDisplayCacheBytes(At(Own[PlayerEnums.AddressKey.FIRSTNAME], i), Latin1.GetBytes(p.Firstname + "\0"));
                fake.WriteDisplayCacheBytes(At(Own[PlayerEnums.AddressKey.LASTNAME], i), Latin1.GetBytes(p.Lastname + "\0"));
            }
            return fake;
        }

        private static Club RosterClub(int count) => new Club { PlayerCount = (ushort)count, AmateurPlayerCount = 0 };

        [Fact]
        public void RefreshPlayerList_OwnWithMatchingGogData_CalibratesAndReadsPersistentValues()
        {
            List<Player> roster = NewRoster();
            using (FakeModule fake = NewGogFake(roster))
            using (Pool pool = new Pool(roster.Count))
            {
                WriteRecords(fake, GogTable, roster);
                WriteYear(fake, GogYear);
                WritePoolPointer(fake, GogPool, pool);
                // Persistent-only value that differs from the display cache's 500, proving the re-read.
                fake.WriteModuleBytes(GogTable + (uint)roster[0].NameRecordId * Settings.PlayerRecordStride + Settings.PlayerRecordSalaryOffset, BitConverter.GetBytes((ushort)777));

                PlayerController controller = new PlayerController(fake.Memory, RosterClub(roster.Count), isGog: true, PlayerEnums.AddressType.OWN);

                Assert.True(fake.Memory.Layout.IsVerified);
                Assert.Equal(GogTable, fake.Memory.Layout.PlayerRecordTableOffset);
                Assert.Equal((ushort)777, controller.EntityList[0].Salary);
                Assert.Equal(roster[2].Age, controller.EntityList[2].Age);
            }
        }

        [Fact]
        public void RefreshPlayerList_OwnWithoutMatchingData_KeepsDisplayCacheAndRemembersTheFailure()
        {
            List<Player> roster = NewRoster();
            using (FakeModule fake = NewGogFake(roster))
            {
                PersistentLayout guess = fake.Memory.Layout;

                PlayerController controller = new PlayerController(fake.Memory, RosterClub(roster.Count), isGog: true, PlayerEnums.AddressType.OWN);

                Assert.Same(guess, fake.Memory.Layout);
                Assert.False(guess.IsVerified);
                Assert.NotNull(guess.FailedCalibrationSignature);
                Assert.Equal(roster.Select(p => p.Level), controller.EntityList.Select(p => p.Level));
                Assert.Equal((ushort)500, controller.EntityList[0].Salary);

                // Same roster again: no new attempt, the layout stays as it was.
                controller.RefreshPlayerList(RosterClub(roster.Count));
                Assert.Same(guess, fake.Memory.Layout);
            }
        }

        [Fact]
        public void Save_WithUnverifiedLayout_NeverWritesToTheGuessedRecordTable()
        {
            List<Player> roster = NewRoster();
            using (FakeModule fake = NewGogFake(roster))
            {
                PlayerController controller = new PlayerController(fake.Memory, RosterClub(roster.Count), isGog: true, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList[0];
                player.Level = 99;

                List<string> warnings = controller.SaveEntityList();

                uint record = GogTable + (uint)player.NameRecordId * Settings.PlayerRecordStride;
                Assert.Equal(new byte[Settings.PlayerRecordStride], fake.ReadModuleBytes(record, (int)Settings.PlayerRecordStride));
                Assert.Equal((byte)99, fake.ReadDisplayCacheBytes(At(Own[PlayerEnums.AddressKey.LEVEL], 0), 1)[0]);
                Assert.Empty(warnings);
            }
        }

        [Fact]
        public void Save_WithoutNamePool_WarnsOnlyAboutActualRenames()
        {
            List<Player> roster = NewRoster();
            using (FakeModule fake = NewGogFake(roster))
            {
                PlayerController controller = new PlayerController(fake.Memory, RosterClub(roster.Count), isGog: true, PlayerEnums.AddressType.OWN);
                controller.EntityList[1].Firstname = "Neu";
                controller.EntityList[2].Lastname = "Anders";

                List<string> warnings = controller.SaveEntityList();

                Assert.Equal(2, warnings.Count);
                Assert.Contains(warnings, w => w.StartsWith("Neu Nachname1: Vorname"));
                Assert.Contains(warnings, w => w.StartsWith("Vorname2 Anders: Nachname"));
            }
        }

        [Fact]
        public void SaveEntityList_ForTraineeWithUnverifiedLayout_ReportsThatNothingWasSaved()
        {
            AddressPresets.InitDynamicTeam("0");
            List<Player> roster = NewRoster();
            using (FakeModule fake = NewGogFake(roster))
            {
                PlayerController controller = new PlayerController(fake.Memory, RosterClub(roster.Count), isGog: true, PlayerEnums.AddressType.TRAINEE);

                List<string> warnings = controller.SaveEntityList();

                Assert.Single(warnings);
                Assert.Contains("Jugendspielern wurden NICHT gespeichert", warnings[0]);
            }
        }

        #endregion
    }
}
