using System;
using System.Linq;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for FixedAddresses (finding the fixed addresses in other builds: runtime shift from the
    /// calibrated layout, static tables and job-offer code by pattern) and for what they enable:
    /// trainer reads/writes, job offer, calendar, name pool and bonus countries on GOG, and the club's
    /// season goal on both builds.
    /// </summary>
    public class FixedAddressesTests
    {
        private static readonly short[] LeagueBases = { 0, 124, 199, 245, 269, 295 };

        // Where the GOG build (run.exe) has them, as confirmed live on 23.10.2026.
        private const uint GogStaticOffset = 0x3100;
        private const uint GogPresentCallSite = 0x1E9989;
        private const uint GogPresentFunction = 0x22E280;
        private const uint GogAddFunction = 0x1769F0;

        private static void WriteInt16s(FakeModule fake, uint offset, params short[] values) =>
            fake.WriteModuleBytes(offset, values.SelectMany(v => BitConverter.GetBytes(v)).ToArray());

        // The patterns FixedAddresses looks for, planted where a build keeps them.
        private static void PlantStaticTables(FakeModule fake, uint staticShift)
        {
            fake.WriteModuleBytes(Settings.LeagueBaseTableOffset + staticShift, Settings.LeagueBasePattern);
            fake.WriteModuleBytes(Settings.LeagueRangeTableOffset + staticShift, Settings.LeagueRangePattern);
        }

        private static void PlantJobOfferCode(FakeModule fake, uint callSite, uint present, uint add)
        {
            fake.WriteModuleBytes(add, Settings.JobOfferAddPattern);
            fake.WriteModuleBytes(present, Settings.JobOfferPresentPattern);
            int rel = unchecked((int)(present - (callSite + 5)));
            fake.WriteModuleBytes(callSite, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).Concat(Settings.JobOfferCallTrailPattern).ToArray());
        }

        private static PersistentLayout CalibratedLayout(uint runtimeShift) => new PersistentLayout(
            Settings.PlayerRecordTableOffset + runtimeShift,
            Settings.NamePoolPointerOffset + runtimeShift,
            Settings.AgeReferenceYearOffset + runtimeShift,
            Settings.ActiveTrainerCountOffset + runtimeShift,
            isVerified: true);

        // Attached to GOG with the layout the calibrator confirms for it and the game's tables and code
        // where GOG keeps them.
        private static void MakeGog(FakeModule fake, bool withStaticTables = true, bool withJobOfferCode = true)
        {
            fake.Memory.Build = GameBuild.Gog;
            fake.Memory.Layout = CalibratedLayout(Settings.GogOffset);
            if (withStaticTables)
                PlantStaticTables(fake, GogStaticOffset);
            if (withJobOfferCode)
                PlantJobOfferCode(fake, GogPresentCallSite, GogPresentFunction, GogAddFunction);
        }

        [Fact]
        public void For_OriginalLayout_IsTheOriginalBuild()
        {
            using (FakeModule fake = new FakeModule())
            {
                Assert.Same(FixedAddresses.Original, FixedAddresses.For(fake.Memory));
            }
        }

        [Fact]
        public void For_CalibratedGog_FindsGogsShiftsAndJobOfferCode()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);

                FixedAddresses found = FixedAddresses.For(fake.Memory);

                Assert.Equal(Settings.GogOffset, found.RuntimeShift);
                Assert.Equal(GogStaticOffset, found.StaticShift);
                Assert.Equal(GogPresentCallSite, found.JobOffer.PresentCallSite);
                Assert.Equal(GogPresentFunction, found.JobOffer.PresentFunction);
                Assert.Equal(GogAddFunction, found.JobOffer.AddFunction);
            }
        }

        // A build missing from the known-build table (GameBuild.Known) but carrying the game's signature
        // and a calibrated layout gets the same search, whatever its distances are.
        [Fact]
        public void For_UnidentifiedBuild_FindsItsOwnShifts()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.Memory.Build = GameBuild.Unidentified(0x1234, 0x8C0000);
                fake.Memory.Layout = CalibratedLayout(0x3180);
                PlantStaticTables(fake, 0x3120);
                PlantJobOfferCode(fake, Settings.JobOfferPresentCallSiteOffset + 0x4000, Settings.JobOfferPresentFunctionOffset + 0x5000, Settings.JobOfferAddFunctionOffset + 0x2400);

                FixedAddresses found = FixedAddresses.For(fake.Memory);

                Assert.Equal(0x3180u, found.RuntimeShift);
                Assert.Equal(0x3120u, found.StaticShift);
                Assert.Equal(Settings.JobOfferPresentCallSiteOffset + 0x4000, found.JobOffer.PresentCallSite);
            }
        }

        [Fact]
        public void For_WithoutTheLeagueTables_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake, withStaticTables: false);

                Assert.Null(FixedAddresses.For(fake.Memory));
            }
        }

        [Fact]
        public void For_WithTheLeagueRangesNotAtTheSameShift_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake, withStaticTables: false);
                fake.WriteModuleBytes(Settings.LeagueBaseTableOffset + GogStaticOffset, Settings.LeagueBasePattern);

                Assert.Null(FixedAddresses.For(fake.Memory));
            }
        }

        // An anchor the calibrator couldn't confirm doesn't veto the shift the others agree on.
        // With the static tables 0x600 further on, a page boundary falls between the league bases and
        // country table (page 0x2C1000) and the club counts and league ranges (page 0x2C2000); with
        // 0x700 it falls between the league bases and everything else.
        private const uint SplitBeforeCounts = 0x600;
        private const uint SplitAfterBases = 0x700;

        // GOG's runtime data with the static tables at the given shift, found (and cached for this
        // layout) while everything is still readable.
        private static void MakeGogWithStaticShift(FakeModule fake, uint staticShift)
        {
            MakeGog(fake, withStaticTables: false);
            PlantStaticTables(fake, staticShift);
            Assert.Equal(staticShift, FixedAddresses.For(fake.Memory).StaticShift);
        }

        [Fact]
        public void For_WithTheLeagueRangesUnreadable_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake, withStaticTables: false);
                PlantStaticTables(fake, SplitBeforeCounts);
                // The search reads its window in chunks that needn't start on a page; ending the
                // image at the page boundary keeps the league bases' chunk readable.
                fake.Memory.mProc.ModuleSize = 0x2C2000;
                fake.MakeModuleRegionUnreadable(Settings.LeagueRangeTableOffset + SplitBeforeCounts, 1);

                Assert.Null(FixedAddresses.For(fake.Memory));
            }
        }

        [Fact]
        public void For_WithOnlyThePlayerRecordAnchorCalibrated_UsesItsShift()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                fake.Memory.Layout = new PersistentLayout(Settings.PlayerRecordTableOffset + Settings.GogOffset, null, null, null, isVerified: true);

                Assert.Equal(Settings.GogOffset, FixedAddresses.For(fake.Memory).RuntimeShift);
            }
        }

        [Fact]
        public void For_WithAModuleEndingBeforeTheSearchWindows_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                fake.Memory.mProc.ModuleSize = 0x100000;

                Assert.Null(FixedAddresses.For(fake.Memory));
            }
        }

        [Fact]
        public void For_WithTheCallSiteWindowUnreadable_HasNoJobOfferCode()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                uint window = Settings.FixedAddressSearchWindow;
                fake.MakeModuleRegionUnreadable(Settings.JobOfferPresentCallSiteOffset - window, (int)(2 * window));

                FixedAddresses found = FixedAddresses.For(fake.Memory);

                Assert.Equal(GogStaticOffset, found.StaticShift);
                Assert.Null(found.JobOffer);
            }
        }

        [Fact]
        public void For_WithACallToAnotherFunctionNearby_PicksTheCallToThePresentation()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                // Same instruction shape, but calling somewhere else.
                fake.WriteModuleBytes(GogPresentCallSite + 0x40, new byte[] { 0xE8, 0x10, 0, 0, 0 }.Concat(Settings.JobOfferCallTrailPattern).ToArray());

                Assert.Equal(GogPresentCallSite, FixedAddresses.For(fake.Memory).JobOffer.PresentCallSite);
            }
        }

        [Fact]
        public void For_WithBothFunctionsButNoPresentationCall_HasNoJobOfferCode()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake, withJobOfferCode: false);
                fake.WriteModuleBytes(GogAddFunction, Settings.JobOfferAddPattern);
                fake.WriteModuleBytes(GogPresentFunction, Settings.JobOfferPresentPattern);

                Assert.Null(FixedAddresses.For(fake.Memory).JobOffer);
            }
        }

        [Fact]
        public void For_WithTheLeagueTablesTwice_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                fake.WriteModuleBytes(Settings.LeagueBaseTableOffset + 0x100, Settings.LeagueBasePattern);

                Assert.Null(FixedAddresses.For(fake.Memory));
            }
        }

        [Fact]
        public void For_WithoutTheJobOfferCode_KeepsTheDataAddresses()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake, withJobOfferCode: false);

                FixedAddresses found = FixedAddresses.For(fake.Memory);

                Assert.Equal(Settings.GogOffset, found.RuntimeShift);
                Assert.Null(found.JobOffer);
                Assert.False(new JobOfferInjector(fake.Memory).IsSupported);
            }
        }

        [Fact]
        public void For_WithTwoPresentationCalls_HasNoJobOfferCode()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                uint second = GogPresentCallSite + 0x40;
                int rel = unchecked((int)(GogPresentFunction - (second + 5)));
                fake.WriteModuleBytes(second, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).Concat(Settings.JobOfferCallTrailPattern).ToArray());

                Assert.Null(FixedAddresses.For(fake.Memory).JobOffer);
            }
        }

        [Fact]
        public void For_BeforeCalibration_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                fake.Memory.Layout = PersistentLayout.CreateGogGuess();

                Assert.Null(FixedAddresses.For(fake.Memory));
            }
        }

        [Fact]
        public void For_CalibratedAnchorsDisagreeing_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                fake.Memory.Layout = new PersistentLayout(Settings.PlayerRecordTableOffset + Settings.GogOffset, Settings.NamePoolPointerOffset + 0x10, null, null, isVerified: true);

                Assert.Null(FixedAddresses.For(fake.Memory));
            }
        }

        [Fact]
        public void For_UnsupportedBuild_IsNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                fake.Memory.Build = GameBuild.Verlaengerung;

                Assert.Null(FixedAddresses.For(fake.Memory));
            }
        }

        [Fact]
        public void Calendar_OnGog_ReadsTheShiftedValuesAndTable()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                uint g = Settings.GogOffset;
                WriteInt16s(fake, Settings.StartYearOffset + g, 2001);
                WriteInt16s(fake, Settings.DayCounterOffset + g, 294);
                WriteInt16s(fake, Settings.DaysPerYearOffset + g, 365);
                WriteInt16s(fake, Settings.GlobalCounterOffset + g, 112);
                WriteInt16s(fake, Settings.CalendarTableOffset + g + (294 + 10) * 4, 2001, 304);

                GameCalendar calendar = GameCalendar.Read(fake.Memory);

                Assert.Equal(2001, calendar.StartYear);
                Assert.Equal(294, calendar.DayCounter);
                Assert.Equal(Tuple.Create((short)2001, (short)304), calendar.EndDateForInjuredDays(10));
            }
        }

        [Fact]
        public void NamePool_OnGog_CanAppendThroughTheShiftedBookkeeping()
        {
            // The name pool's bookkeeping sits near module+0x7FE540, beyond the default fake module.
            using (FakeModule fake = new FakeModule(moduleBlockSize: 0x810000))
            {
                MakeGog(fake);
                uint g = Settings.GogOffset;
                uint pool = fake.ModuleBase + 0x4F0000;
                fake.WriteModuleBytes(Settings.NamePoolPointerOffset + g, BitConverter.GetBytes(pool));
                fake.WriteModuleBytes(Settings.NamePoolEndPointerOffset + g, BitConverter.GetBytes(pool + 4));
                WriteInt16s(fake, Settings.NamePoolCountOffset + g, 1);
                fake.WriteModuleBytes(0x4F0000, new byte[] { (byte)'A', (byte)'b', (byte)'c', 0 });
                fake.WriteModuleBytes(Settings.NamePoolTableOffset + g, BitConverter.GetBytes(pool));

                NamePoolResolver resolver = new NamePoolResolver(fake.Memory);
                ushort? index = resolver.AppendName("Neu", System.Text.Encoding.GetEncoding("iso-8859-1"));

                Assert.True(resolver.CanAppend);
                Assert.Equal((ushort)1, index);
                Assert.Equal((ushort)2, BitConverter.ToUInt16(fake.ReadModuleBytes(Settings.NamePoolCountOffset + g, 2), 0));
                Assert.Equal(pool + 4, BitConverter.ToUInt32(fake.ReadModuleBytes(Settings.NamePoolTableOffset + g + 4, 4), 0));
            }
        }

        [Fact]
        public void BonusCountries_OnGog_LookUpTheShiftedLeagueTables()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + GogStaticOffset + (uint)PlayerEnums.Country.England, new byte[] { 1 });
                WriteInt16s(fake, Settings.LeagueTableOffset + Settings.GogOffset + 6 * Settings.LeagueStride, 18);

                Assert.Equal((short)1, LeagueController.LookupLeagueSystem(fake.Memory, PlayerEnums.Country.England));
                // League system 1 starts at league 6 (the planted range table), whose 18 clubs are in memory.
                Assert.True(LeagueController.HasClubsInTopLeague(fake.Memory, 1));
                Assert.Null(LeagueController.ActivationBlockedReason(fake.Memory, 1));
            }
        }

        private static uint GogWorkingCopy(uint slotOffset) => Settings.CoachSlotTableOffset + Settings.GogOffset + 1 * Settings.CoachSlotStride + slotOffset;
        private static uint GogRecord(uint slotOffset) => Settings.CoachRecordTableOffset + Settings.GogOffset + 1 * Settings.CoachRecordStride + slotOffset;

        [Fact]
        public void Coach_OnGog_IsReadFromAndSavedToTheShiftedCopies()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                fake.WriteModuleBytes(GogWorkingCopy(0x48), BitConverter.GetBytes(1150000));
                CoachController controller = new CoachController(fake.Memory, isGog: true, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                Assert.Equal(1150000, controller.Coach.Salary);

                controller.Coach.Salary = 2000000;
                controller.Coach.CountryNextSeason = PlayerEnums.Country.England;
                controller.Coach.ClubIdNextSeason = 9;
                controller.Save();

                Assert.Equal(2000000, BitConverter.ToInt32(fake.ReadModuleBytes(GogWorkingCopy(0x48), 4), 0));
                Assert.Equal(2000000, BitConverter.ToInt32(fake.ReadModuleBytes(GogRecord(0x48), 4), 0));
                Assert.Equal((byte)PlayerEnums.Country.England, fake.ReadModuleBytes(GogRecord(0x3C), 1)[0]);
                Assert.Equal((byte)9, fake.ReadModuleBytes(GogRecord(0x3E), 1)[0]);
                // The empty next-season contract got the current terms, as on the original build.
                Assert.Equal(2000000, BitConverter.ToInt32(fake.ReadModuleBytes(GogRecord(0x68), 4), 0));
            }
        }

        [Fact]
        public void IsEligibleNextSeasonClub_OnGog_ReadsTheShiftedLeagueTables()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                // League system 1 covers leagues 6-8 (the planted range table); its top league holds clubs 5 and 52.
                const PlayerEnums.Country country = PlayerEnums.Country.England;
                fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + GogStaticOffset + (uint)country, new byte[] { 1 });
                uint league = Settings.LeagueTableOffset + Settings.GogOffset + 6 * Settings.LeagueStride;
                WriteInt16s(fake, league, 2);
                WriteInt16s(fake, league + Settings.LeagueClubIdsOffset, 5, 52);

                Assert.True(CoachController.IsEligibleNextSeasonClub(fake.Memory, country, 52));
                Assert.False(CoachController.IsEligibleNextSeasonClub(fake.Memory, country, 53));
            }
        }

        [Fact]
        public void JobOffer_OnGog_RedirectsGogsCallSiteToAStubUsingGogsAddresses()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                uint callSite = fake.ModuleBase + GogPresentCallSite;
                uint present = fake.ModuleBase + GogPresentFunction;

                JobOfferInjector.RequestResult result = new JobOfferInjector(fake.Memory).Request(
                    new Coach { CurrentCountry = PlayerEnums.Country.Deutschland, CurrentClubId = 52 }, 0, PlayerEnums.Country.England, 7);

                Assert.Equal(JobOfferInjector.RequestResult.Requested, result);
                byte[] call = fake.ReadModuleBytes(GogPresentCallSite, 5);
                uint stub = unchecked((uint)(callSite + 5 + BitConverter.ToInt32(call, 1))) - JobOfferInjector.CodeOffset;
                byte[] code = fake.Memory.ReadBytesAtAddress(stub, JobOfferInjector.StubSize);
                Assert.True(Contains(code, new byte[] { 0xB9 }.Concat(BitConverter.GetBytes(fake.ModuleBase + Settings.JobOfferListOffset + Settings.GogOffset)).ToArray()));
                Assert.True(Contains(code, new byte[] { 0xB8 }.Concat(BitConverter.GetBytes(fake.ModuleBase + GogAddFunction)).ToArray()));
                Assert.True(Contains(code, new byte[] { 0xB8 }.Concat(BitConverter.GetBytes(present)).ToArray()));
            }
        }

        private static bool Contains(byte[] haystack, byte[] needle) =>
            Enumerable.Range(0, haystack.Length - needle.Length + 1).Any(i => haystack.Skip(i).Take(needle.Length).SequenceEqual(needle));

        // Germany is league system 0 with 124 clubs from record 0; the trainer coaches club 52.
        private const PlayerEnums.Country ClubCountry = PlayerEnums.Country.Deutschland;
        private const byte ClubId = 52;

        private static uint SeedClub(FakeModule fake, uint runtimeShift, uint staticShift, byte recordGoal, uint? cachedCopyOffset, byte cachedGoal = 0)
        {
            uint coachCountry = Settings.CoachSlotTableOffset + runtimeShift + 1 * Settings.CoachSlotStride + 0x1C;
            WriteInt16s(fake, coachCountry, (short)ClubCountry, ClubId);
            fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + staticShift + (uint)ClubCountry, new byte[] { 0 });
            WriteInt16s(fake, Settings.LeagueClubCountTableOffset + staticShift, 124);
            WriteInt16s(fake, Settings.LeagueBaseTableOffset + staticShift, LeagueBases);

            uint record = Settings.ClubRecordTableOffset + runtimeShift + ClubId * Settings.ClubRecordStride + Settings.ClubRecordSeasonGoalOffset;
            fake.WriteModuleBytes(record, new byte[] { recordGoal });

            if (cachedCopyOffset.HasValue)
            {
                // Cache slot 2: in use, this club, pointer to its working copy.
                byte[] entry = new byte[Settings.ClubCacheStride];
                entry[0] = 1;
                entry[1] = (byte)ClubCountry;
                entry[2] = ClubId;
                BitConverter.GetBytes(fake.ModuleBase + cachedCopyOffset.Value).CopyTo(entry, (int)Settings.ClubCachePointerOffset);
                fake.WriteModuleBytes(Settings.ClubCacheTableOffset + runtimeShift + 2 * Settings.ClubCacheStride, entry);
                fake.WriteModuleBytes(cachedCopyOffset.Value + Settings.ClubSeasonGoalOffset, new byte[] { cachedGoal });
            }
            return record;
        }

        private const uint CachedCopy = 0x500000;

        [Fact]
        public void SeasonGoal_IsReadFromTheClubsCachedWorkingCopy()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedClub(fake, 0, 0, recordGoal: 3, cachedCopyOffset: CachedCopy, cachedGoal: 7);
                fake.WriteModuleBytes(Settings.CoachSlotTableOffset + 1 * Settings.CoachSlotStride + 0x5C, BitConverter.GetBytes((ushort)9));

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal(CoachEnums.SeasonGoal.GuterFussball, coach.SeasonGoal);
            }
        }

        [Fact]
        public void SeasonGoal_WithoutCachedCopy_IsReadFromTheClubRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedClub(fake, 0, 0, recordGoal: 3, cachedCopyOffset: null);

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal(CoachEnums.SeasonGoal.UefaPokalplatz, coach.SeasonGoal);
            }
        }

        [Fact]
        public void SeasonGoal_WithAnOutOfRangeClubValue_IsReadFromTheContract()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedClub(fake, 0, 0, recordGoal: 200, cachedCopyOffset: null);
                fake.WriteModuleBytes(Settings.CoachSlotTableOffset + 1 * Settings.CoachSlotStride + 0x5C, BitConverter.GetBytes((ushort)CoachEnums.SeasonGoal.Klassenerhalt));

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal(CoachEnums.SeasonGoal.Klassenerhalt, coach.SeasonGoal);
            }
        }

        // The record index comes from the league base table (game data); one pointing at unreadable
        // memory leaves the contract's goal.
        [Fact]
        public void SeasonGoal_WithAnUnreadableClubRecord_IsReadFromTheContract()
        {
            using (FakeModule fake = new FakeModule())
            {
                const short firstRecord = 250;
                SeedClub(fake, 0, 0, recordGoal: 3, cachedCopyOffset: null);
                WriteInt16s(fake, Settings.LeagueBaseTableOffset, firstRecord);
                fake.MakeModuleRegionUnreadable(Settings.ClubRecordTableOffset + (uint)(firstRecord + ClubId) * Settings.ClubRecordStride + Settings.ClubRecordSeasonGoalOffset, 1);
                fake.WriteModuleBytes(Settings.CoachSlotTableOffset + 1 * Settings.CoachSlotStride + 0x5C, BitConverter.GetBytes((ushort)CoachEnums.SeasonGoal.Klassenerhalt));

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal(CoachEnums.SeasonGoal.Klassenerhalt, coach.SeasonGoal);
            }
        }

        [Fact]
        public void SeasonGoal_WithUnreadableLeagueTables_IsReadFromTheContract()
        {
            using (FakeModule fake = new FakeModule())
            {
                SeedClub(fake, 0, 0, recordGoal: 3, cachedCopyOffset: null);
                fake.WriteModuleBytes(Settings.CoachSlotTableOffset + 1 * Settings.CoachSlotStride + 0x5C, BitConverter.GetBytes((ushort)CoachEnums.SeasonGoal.Klassenerhalt));
                fake.MakeModuleRegionUnreadable(Settings.LeagueSystemByCountryOffset, 1);

                Coach coach = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal(CoachEnums.SeasonGoal.Klassenerhalt, coach.SeasonGoal);
            }
        }

        [Theory]
        [InlineData(SplitBeforeCounts, 0x2C1BE8u)]   // club counts unreadable, country table readable
        [InlineData(SplitAfterBases, 0x2C17F0u)]     // league bases unreadable, counts readable
        public void SeasonGoal_WithAnUnreadableLeagueTable_IsReadFromTheContract(uint staticShift, uint unreadableTable)
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGogWithStaticShift(fake, staticShift);
                SeedClub(fake, Settings.GogOffset, staticShift, recordGoal: 3, cachedCopyOffset: null);
                fake.WriteModuleBytes(GogWorkingCopy(0x5C), BitConverter.GetBytes((ushort)CoachEnums.SeasonGoal.Klassenerhalt));
                fake.MakeModuleRegionUnreadable(unreadableTable + staticShift, 1);

                Coach coach = new CoachController(fake.Memory, isGog: true, PlayerEnums.AddressType.OWN, trainerIndex: 0).Coach;

                Assert.Equal(CoachEnums.SeasonGoal.Klassenerhalt, coach.SeasonGoal);
            }
        }

        [Fact]
        public void IsEligibleNextSeasonClub_WithUnreadableLeagueRanges_IsUnknown()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGogWithStaticShift(fake, SplitBeforeCounts);
                fake.WriteModuleBytes(Settings.LeagueSystemByCountryOffset + SplitBeforeCounts + (uint)PlayerEnums.Country.England, new byte[] { 1 });
                fake.MakeModuleRegionUnreadable(Settings.LeagueRangeTableOffset + SplitBeforeCounts, 1);

                Assert.Null(CoachController.IsEligibleNextSeasonClub(fake.Memory, PlayerEnums.Country.England, 52));
            }
        }

        [Fact]
        public void LookupLeagueSystem_WithAnUnreadableCountryTable_IsUnknown()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.MakeModuleRegionUnreadable(Settings.LeagueSystemByCountryOffset, 1);

                Assert.Null(LeagueController.LookupLeagueSystem(fake.Memory, PlayerEnums.Country.England));
            }
        }

        [Fact]
        public void SeasonGoal_Save_SetsContractClubRecordAndCachedCopy()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint record = SeedClub(fake, 0, 0, recordGoal: 7, cachedCopyOffset: CachedCopy, cachedGoal: 7);
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

                controller.Coach.SeasonGoal = CoachEnums.SeasonGoal.Klassenerhalt;
                controller.Save();

                Assert.Equal((byte)10, fake.ReadModuleBytes(record, 1)[0]);
                Assert.Equal((byte)10, fake.ReadModuleBytes(CachedCopy + Settings.ClubSeasonGoalOffset, 1)[0]);
                Assert.Equal((ushort)10, BitConverter.ToUInt16(CoachMemory.ReadRecord(fake, AddressPresets.COACH[CoachEnums.AddressKey.SEASON_GOAL], 2), 0));
            }
        }

        [Fact]
        public void SeasonGoal_OnGog_UsesTheShiftedClubRecordAndCache()
        {
            using (FakeModule fake = new FakeModule())
            {
                MakeGog(fake);
                uint record = SeedClub(fake, Settings.GogOffset, GogStaticOffset, recordGoal: 7, cachedCopyOffset: CachedCopy, cachedGoal: 7);
                CoachController controller = new CoachController(fake.Memory, isGog: true, PlayerEnums.AddressType.OWN, trainerIndex: 0);
                Assert.Equal(CoachEnums.SeasonGoal.GuterFussball, controller.Coach.SeasonGoal);

                controller.Coach.SeasonGoal = CoachEnums.SeasonGoal.Meisterschaft;
                controller.Save();

                Assert.Equal((byte)0, fake.ReadModuleBytes(record, 1)[0]);
                Assert.Equal((byte)0, fake.ReadModuleBytes(CachedCopy + Settings.ClubSeasonGoalOffset, 1)[0]);
            }
        }

        [Fact]
        public void SeasonGoal_ForAClubOutsideTheLeagueRecords_OnlyChangesTheContract()
        {
            using (FakeModule fake = new FakeModule())
            {
                uint record = SeedClub(fake, 0, 0, recordGoal: 7, cachedCopyOffset: null);
                WriteInt16s(fake, Settings.LeagueClubCountTableOffset, 40);   // club 52 is beyond the league clubs now
                CoachController controller = new CoachController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN, trainerIndex: 0);

                controller.Coach.SeasonGoal = CoachEnums.SeasonGoal.Klassenerhalt;
                controller.Save();

                Assert.Equal((byte)7, fake.ReadModuleBytes(record, 1)[0]);
                Assert.Equal((ushort)10, BitConverter.ToUInt16(CoachMemory.ReadRecord(fake, AddressPresets.COACH[CoachEnums.AddressKey.SEASON_GOAL], 2), 0));
            }
        }
    }
}
