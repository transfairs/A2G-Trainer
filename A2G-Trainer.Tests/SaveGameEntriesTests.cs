using System;
using System.ComponentModel;
using System.Linq;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for the savegame editor's entries: players, clubs, trainers and the overview read and
    /// write their record fields, with the game's conversions, in a savegame file.
    /// </summary>
    public class SaveGameEntriesTests
    {
        private const short Year = 2001;
        private const short DayCounter = 100;
        private const short GlobalCounter = 500;
        private const uint Base = SaveGameFile.ModuleBase;

        private static SaveGameFile NewFile()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            file.WriteInt16(Base + Settings.AgeReferenceYearOffset, Year);
            file.WriteInt16(Base + Settings.StartYearOffset, Year);
            file.WriteInt16(Base + Settings.DayCounterOffset, DayCounter);
            file.WriteInt16(Base + Settings.DaysPerYearOffset, 365);
            file.WriteInt16(Base + Settings.GlobalCounterOffset, GlobalCounter);
            // Calendar table: day counter n -> (Year, n) for the first year, then the next year.
            for (int day = 0; day < 700; day++)
            {
                uint entry = Base + Settings.CalendarTableOffset + (uint)day * 4;
                file.WriteInt16(entry, (short)(day < 365 ? Year : Year + 1));
                file.WriteInt16(entry + 2, (short)(day % 365));
            }
            return file;
        }

        private static SaveGameEditor NewEditor() => SaveGameEditor.For(NewFile());

        private static uint Record(ushort id, uint offset) => Base + Settings.PlayerRecordTableOffset + id * Settings.PlayerRecordStride + offset;

        private static uint ClubRecord(int index, uint offset) => Base + Settings.ClubRecordTableOffset + (uint)index * Settings.ClubRecordStride + offset;

        // A club in record <index> with the given country/ID and name (record +0xF4 ID, +0xF8 country).
        private static void PutClub(SaveGameFile file, int index, PlayerEnums.Country country, byte id, string name)
        {
            file.WriteString(ClubRecord(index, 0), name, 20);
            file.WriteByte(ClubRecord(index, 0xF4), id);
            file.WriteByte(ClubRecord(index, 0xF8), (byte)country);
        }

        [Fact]
        public void Player_Names_ComeFromThePoolAndRenameOnlyThisPlayer()
        {
            SaveGameEditor editor = NewEditor();
            int thomas = editor.File.AddName("Thomas");
            int haessler = editor.File.AddName("Häßler");
            foreach (ushort id in new ushort[] { 1, 2 })
            {
                editor.File.WriteUInt16(Record(id, 0), (ushort)thomas);
                editor.File.WriteUInt16(Record(id, 2), (ushort)haessler);
            }

            SaveGamePlayer player = editor.Player(1);
            Assert.Equal("Thomas Häßler", player.ToString());
            Assert.True(player.IsUsed);

            player.Firstname = "Icke";

            Assert.Equal("Icke", player.Firstname);
            Assert.Equal("Thomas", editor.Player(2).Firstname);
            Assert.Equal("Icke", SaveGameFile.Parse(editor.File.Serialize()).GetName(editor.File.FindName("Icke")));
        }

        [Fact]
        public void Player_Names_AreChecked()
        {
            SaveGamePlayer player = NewEditor().Player(1);

            Assert.Throws<ArgumentException>(() => player.Firstname = " ");
            Assert.Throws<ArgumentException>(() => player.Firstname = "Maximilian");
            Assert.Throws<ArgumentException>(() => player.Lastname = "Schweinsteigerle");
            player.Lastname = "Schweinsteiger";
            Assert.Equal("Schweinsteiger", player.Lastname);
        }

        [Fact]
        public void Player_YearFields_AreStoredAsYears()
        {
            SaveGameEditor editor = NewEditor();
            SaveGamePlayer player = editor.Player(7);

            player.Age = 24;
            player.ContractDuration = 3;
            player.YearsInClub = 2;

            Assert.Equal(Year - 24, editor.File.ReadUInt16(Record(7, Settings.PlayerRecordAgeOffset)));
            Assert.Equal(Year + 3, editor.File.ReadUInt16(Record(7, Settings.PlayerRecordContractEndYearOffset)));
            Assert.Equal(Year - 2, editor.File.ReadUInt16(Record(7, Settings.PlayerRecordContractEndYearOffset + 2)));
            Assert.Equal(24, player.Age);
            Assert.Equal(3, player.ContractDuration);
            Assert.Equal(2, player.YearsInClub);
            Assert.Throws<ArgumentException>(() => player.Age = 0);
        }

        [Fact]
        public void Player_ExpiredContract_ReadsAsZero()
        {
            SaveGameEditor editor = NewEditor();
            editor.File.WriteUInt16(Record(3, Settings.PlayerRecordContractEndYearOffset), Year - 1);

            Assert.Equal(0, editor.Player(3).ContractDuration);
        }

        [Fact]
        public void Player_InjuredDays_AreStoredAsEndDate()
        {
            SaveGameEditor editor = NewEditor();
            SaveGamePlayer player = editor.Player(5);

            player.InjuredDays = 10;

            Assert.Equal((ushort)GlobalCounter, editor.File.ReadUInt16(Record(5, Settings.PlayerRecordInjuryStartOffset)));
            Assert.Equal(Year, editor.File.ReadInt16(Record(5, Settings.PlayerRecordInjuryEndYearOffset)));
            Assert.Equal(DayCounter + 10, editor.File.ReadInt16(Record(5, Settings.PlayerRecordInjuryEndYearOffset + 2)));
            Assert.Equal(10, player.InjuredDays);

            player.InjuredDays = 300;
            Assert.Equal(Year + 1, editor.File.ReadInt16(Record(5, Settings.PlayerRecordInjuryEndYearOffset)));
            Assert.Equal(300, player.InjuredDays);
            // A running injury keeps its start.
            Assert.Equal((ushort)GlobalCounter, editor.File.ReadUInt16(Record(5, Settings.PlayerRecordInjuryStartOffset)));

            editor.File.WriteByte(Record(5, Settings.PlayerRecordInjuryClearedFlagOffset), 1);
            player.InjuredDays = 0;
            Assert.Equal(new byte[6], editor.File.Read(Record(5, Settings.PlayerRecordInjuryStartOffset), 6));
            Assert.Equal(0, editor.File.ReadByte(Record(5, Settings.PlayerRecordInjuryClearedFlagOffset)));

            Assert.Throws<ArgumentException>(() => player.InjuredDays = 5000);
        }

        [Fact]
        public void Player_Vulnerable_IsStoredAsCounterEndValue()
        {
            SaveGameEditor editor = NewEditor();
            SaveGamePlayer player = editor.Player(5);

            player.VulnerableTicks = 7;
            Assert.Equal(GlobalCounter + 7, editor.File.ReadUInt16(Record(5, Settings.PlayerRecordVulnerableOffset)));
            Assert.Equal(7, player.VulnerableTicks);

            player.VulnerableTicks = 0;
            Assert.Equal(0, editor.File.ReadUInt16(Record(5, Settings.PlayerRecordVulnerableOffset)));
        }

        [Fact]
        public void Player_Nationality_KeepsBit7()
        {
            SaveGameEditor editor = NewEditor();
            editor.File.WriteByte(Record(4, Settings.PlayerRecordNationalityOffset), 0xB6);
            SaveGamePlayer player = editor.Player(4);

            Assert.Equal((PlayerEnums.Country)0x36, player.Nationality);
            player.Nationality = PlayerEnums.Country.Deutschland;

            Assert.Equal(0x80 | (byte)PlayerEnums.Country.Deutschland, editor.File.ReadByte(Record(4, Settings.PlayerRecordNationalityOffset)));
        }

        [Fact]
        public void Player_Doping_KeepsTheMarkerBits()
        {
            SaveGameEditor editor = NewEditor();
            uint doping = Record(4, Settings.PlayerRecordBannedOffset + 1);
            editor.File.WriteByte(doping, 0x30);
            SaveGamePlayer player = editor.Player(4);

            Assert.False(player.Doped);
            player.Doped = true;
            Assert.Equal(0x3C, editor.File.ReadByte(doping));
            editor.File.WriteByte(doping, 0x35);
            player.Doped = true;
            Assert.Equal(0x35, editor.File.ReadByte(doping));
            player.Doped = false;
            Assert.Equal(0x30, editor.File.ReadByte(doping));
        }

        [Fact]
        public void Player_PlainFields_UseTheRecordOffsets()
        {
            SaveGameEditor editor = NewEditor();
            SaveGamePlayer player = editor.Player(9);

            player.Level = 11;
            player.Position = (PlayerEnums.Position)3;
            player.SecondaryPosition2 = (PlayerEnums.Position)5;
            player.Salary = 1234;
            player.BuyPrice = 100000;
            player.Goals = 17;
            player.Jersey = 10;
            player.Skills = (PlayerEnums.Skills)0x0101;

            Assert.Equal(11, editor.File.ReadByte(Record(9, Settings.PlayerRecordLevelOffset)));
            Assert.Equal(3, editor.File.ReadByte(Record(9, Settings.PlayerRecordPositionOffset)));
            Assert.Equal(5, editor.File.ReadByte(Record(9, Settings.PlayerRecordPositionOffset + 2)));
            Assert.Equal(1234, editor.File.ReadUInt16(Record(9, Settings.PlayerRecordSalaryOffset)));
            Assert.Equal(100000u, editor.File.ReadUInt32(Record(9, Settings.PlayerRecordBuyPriceOffset)));
            Assert.Equal(17, editor.File.ReadByte(Record(9, Settings.PlayerRecordGoalsOffset)));
            Assert.Equal(0x0101, editor.File.ReadUInt16(Record(9, Settings.PlayerRecordSkillsOffset)));
            Assert.Throws<ArgumentException>(() => player.Jersey = 100);
        }

        [Fact]
        public void Player_RaisesPropertyChanged()
        {
            SaveGamePlayer player = NewEditor().Player(1);
            string changed = null;
            player.PropertyChanged += (s, e) => changed = e.PropertyName;

            player.Form = 3;

            Assert.Equal(nameof(SaveGamePlayer.Form), changed);
        }

        [Fact]
        public void FindPlayers_MatchesFullNameIgnoringCase()
        {
            SaveGameEditor editor = NewEditor();
            editor.File.WriteUInt16(Record(10, 0), (ushort)editor.File.AddName("Oliver"));
            editor.File.WriteUInt16(Record(10, 2), (ushort)editor.File.AddName("Kahn"));
            editor.File.WriteUInt16(Record(11, 0), (ushort)editor.File.AddName("Olaf"));
            editor.File.WriteUInt16(Record(11, 2), (ushort)editor.File.AddName("Thon"));

            Assert.Equal(new ushort[] { 10 }, editor.FindPlayers("r kah", 10).Select(p => p.Id));
            Assert.Equal(2, editor.FindPlayers("ol", 10).Count);
            Assert.Single(editor.FindPlayers("ol", 1));
            Assert.Empty(editor.FindPlayers(" ", 10));
            Assert.Throws<ArgumentOutOfRangeException>(() => editor.Player(SaveGamePlayer.RecordCount));
        }

        [Fact]
        public void Clubs_ComeFromTheCountrysLeagueSystem()
        {
            SaveGameFile file = NewFile();
            // Germany uses league system 1 (records from 124 on), England system 0.
            file.WriteByte(Base + Settings.LeagueSystemByCountryOffset + (byte)PlayerEnums.Country.Deutschland, 1);
            file.WriteByte(Base + Settings.LeagueSystemByCountryOffset + (byte)PlayerEnums.Country.England, 0);
            file.WriteInt16(Base + Settings.BonusCountryCountOffset + 2, (short)PlayerEnums.Country.England);
            PutClub(file, 124, PlayerEnums.Country.Deutschland, 0, "Werder Bremen");
            PutClub(file, 126, PlayerEnums.Country.Deutschland, 2, "VfL Bochum");
            PutClub(file, 0, PlayerEnums.Country.England, 0, "Arsenal");
            SaveGameEditor editor = SaveGameEditor.For(file);

            Assert.Equal(PlayerEnums.Country.England, editor.MainCountry);
            Assert.Equal(PlayerEnums.Country.England, editor.Countries.First());
            Assert.Equal(5, editor.Countries.Count());
            Assert.Equal(new[] { "Werder Bremen", "VfL Bochum" }, editor.Clubs(PlayerEnums.Country.Deutschland).Select(c => c.Name));
            Assert.Equal("VfL Bochum", editor.ClubName(PlayerEnums.Country.Deutschland, 2));
            Assert.Equal("-", editor.ClubName(PlayerEnums.Country.Deutschland, 1));
            Assert.Equal(new[] { "Arsenal" }, editor.Clubs(PlayerEnums.Country.England).Select(c => c.Name));
            // A country without a valid league system has no clubs.
            file.WriteByte(Base + Settings.LeagueSystemByCountryOffset + (byte)PlayerEnums.Country.Spanien, 9);
            Assert.Empty(editor.Clubs(PlayerEnums.Country.Spanien));
        }

        [Fact]
        public void Club_SquadIdsComeFromTheRecord()
        {
            SaveGameEditor editor = NewEditor();
            editor.File.WriteInt16(ClubRecord(0, 0xDE), 2);
            editor.File.WriteUInt16(ClubRecord(0, 0x570), 17);
            editor.File.WriteUInt16(ClubRecord(0, 0x572), 18);
            editor.File.WriteByte(ClubRecord(0, 0x5BD), 1);
            editor.File.WriteUInt16(ClubRecord(0, 0x5BE), 40);
            SaveGameClub club = new SaveGameClub(editor, 0);

            Assert.Equal(new ushort[] { 17, 18 }, club.SquadIds);
            Assert.Equal(new ushort[] { 40 }, club.AmateurIds);
        }

        [Fact]
        public void Club_Fields_UseTheRecordOffsets()
        {
            SaveGameEditor editor = NewEditor();
            SaveGameClub club = new SaveGameClub(editor, 3);

            club.Name = "Eintr. Braunschweig";
            club.ShortName = "EBS";
            club.Town = "Braunschweig";
            club.Wealth = 5000000;
            club.SponsorCash = 250000;
            club.Respect = 9;
            club.Will2Win = 8;
            club.Spirit = 7;
            club.TeamCohesion = 6;
            club.Shirt1Color = (ClubEnums.JerseyColor)2;
            club.Shirt1Pattern = (ClubEnums.ShirtPattern)3;
            club.StadiumName = "Eintracht-Stadion";
            club.SeasonGoal = (CoachEnums.SeasonGoal)4;

            Assert.Equal("Eintr. Braunschweig", club.Name);
            Assert.Throws<ArgumentException>(() => club.Name = "Eintracht Braunschweig");
            Assert.Equal("EBS", editor.File.ReadString(ClubRecord(3, 0x14), 4));
            Assert.Equal("Braunschweig", editor.File.ReadString(ClubRecord(3, 0x509), 17));
            Assert.Equal(5000000, editor.File.ReadInt32(ClubRecord(3, 0x1A4)));
            Assert.Equal(250000, editor.File.ReadInt32(ClubRecord(3, 0x414)));
            Assert.Equal(9, editor.File.ReadByte(ClubRecord(3, 0x94)));
            Assert.Equal(8, editor.File.ReadByte(ClubRecord(3, 0xA0)));
            Assert.Equal(7, editor.File.ReadByte(ClubRecord(3, 0xA2)));
            Assert.Equal(6, editor.File.ReadByte(ClubRecord(3, 0x198)));

            editor.File.WriteUInt16(ClubRecord(3, 0xA6), 4 << 9);
            club.Pressing = 1;
            club.OffsideTrap = 140;
            club.AntiOffsideTrap = 2;
            club.FreeKicks = 3;
            club.Corners = 4;
            club.ThrowIns = 5;
            club.DirtyTricks = 1;
            Assert.Equal(7, editor.File.ReadByte(ClubRecord(3, 0xA2)));
            Assert.Equal(1, editor.File.ReadByte(ClubRecord(3, 0xA3)));
            Assert.Equal(140, editor.File.ReadByte(ClubRecord(3, 0xA4)));
            Assert.Equal(2, editor.File.ReadByte(ClubRecord(3, 0xA5)));
            // The hidden value in bits 9-11 stays.
            Assert.Equal(3 | 4 << 3 | 5 << 6 | 4 << 9 | 1 << 12, editor.File.ReadUInt16(ClubRecord(3, 0xA6)));
            Assert.Equal((byte)4, club.Corners);
            Assert.Throws<ArgumentException>(() => club.Pressing = 6);
            Assert.Throws<ArgumentException>(() => club.DirtyTricks = 6);
            Assert.Equal(0x32, editor.File.ReadByte(ClubRecord(3, 0xE0)));
            Assert.Equal("Eintracht-Stadion", editor.File.ReadString(ClubRecord(3, 0x4EC), 29));
            Assert.Equal(4, editor.File.ReadByte(ClubRecord(3, Settings.ClubRecordSeasonGoalOffset)));
            Assert.Equal((ClubEnums.JerseyColor)2, club.Shirt1Color);
            Assert.Equal((ClubEnums.ShirtPattern)3, club.Shirt1Pattern);
            Assert.Throws<ArgumentException>(() => club.ShortName = "ABCD");
        }

        [Fact]
        public void Club_StadiumBlocks_KeepTheCapacityInStep()
        {
            SaveGameEditor editor = NewEditor();
            SaveGameClub club = new SaveGameClub(editor, 3);

            club.Blocks.A.Seats = 1000;
            club.Blocks.B.Standings = 500;
            club.Blocks.C.Seats = 200;
            club.Blocks.C.Weeks = 4;
            club.Blocks.A.Roof = true;
            club.Blocks.L.Roof = true;

            Assert.Equal(1500, club.Capacity);
            Assert.Equal(1500, editor.File.ReadInt32(ClubRecord(3, 0x520)));
            Assert.Equal((ushort)((1 << 0) | (1 << 11)), editor.File.ReadUInt16(ClubRecord(3, 0x560)));
            Assert.Equal(4, editor.File.ReadByte(ClubRecord(3, 0x556)));
            Assert.True(club.Blocks.A.Roof);
            Assert.False(club.Blocks.B.Roof);
            Assert.Equal("1000 Plätze", club.Blocks.A.ToString());
            club.Blocks.A.Roof = false;
            Assert.False(club.Blocks.A.Roof);
            Assert.Throws<ArgumentException>(() => club.Blocks.A.Seats = -1);
        }

        [Fact]
        public void ClubIdConverter_ShowsAndPicksNamesOfTheSameCountry()
        {
            SaveGameFile file = NewFile();
            file.WriteByte(Base + Settings.LeagueSystemByCountryOffset + (byte)PlayerEnums.Country.Deutschland, 0);
            PutClub(file, 0, PlayerEnums.Country.Deutschland, 0, "Werder Bremen");
            PutClub(file, 1, PlayerEnums.Country.Deutschland, 1, "Bayern München");
            SaveGameEditor editor = SaveGameEditor.For(file);
            SaveGameClub werder = editor.Clubs(PlayerEnums.Country.Deutschland)[0];
            PropertyDescriptor rival = TypeDescriptor.GetProperties(werder)[nameof(SaveGameClub.ArchRivalId)];
            TestContext context = new TestContext(werder, rival);
            TypeConverter converter = rival.Converter;

            Assert.True(converter.GetStandardValuesSupported(context));
            Assert.True(converter.GetStandardValuesExclusive(context));
            Assert.Equal(new object[] { (byte)1, (byte)0 }, converter.GetStandardValues(context).Cast<object>());
            Assert.Equal("Bayern München", converter.ConvertTo(context, null, (byte)1, typeof(string)));
            Assert.Equal("9", converter.ConvertTo(context, null, (byte)9, typeof(string)));
            Assert.Equal((byte)1, converter.ConvertFrom(context, null, "Bayern München"));
            Assert.Equal((byte)7, converter.ConvertFrom(context, null, "7"));
            Assert.Throws<ArgumentException>(() => converter.ConvertFrom(context, null, "Schalke"));
        }

        [Fact]
        public void Coach_Fields_UseTheRecordAndCompetencyTables()
        {
            SaveGameFile file = NewFile();
            file.WriteInt16(Base + Settings.ActiveTrainerCountOffset, 2);
            SaveGameEditor editor = SaveGameEditor.For(file);
            SaveGameCoach coach = editor.Coaches[1];
            uint record = Base + Settings.CoachRecordTableOffset + 2 * Settings.CoachRecordStride;
            uint competency = Base + Settings.CoachCompetencyRecordOffset + 2 * Settings.CoachCompetencyRecordStride;

            Assert.Equal(2, editor.Coaches.Count);
            Assert.Equal("Manager 2", coach.ToString());
            coach.Firstname = "Robin";
            coach.Lastname = "Trauer";
            coach.Level = 3;
            coach.Salary = 700000;
            coach.Wealth = 123;
            coach.Image = (CoachEnums.ManagerImage)5;
            coach.Motivation = 42;
            coach.HasExtensionOption = true;
            coach.Vehicle = CoachEnums.VehiclePerk.Bahncard;

            Assert.Equal("Robin Trauer", coach.ToString());
            Assert.Equal("Robin", file.ReadString(record, 10));
            Assert.Equal("Trauer", file.ReadString(record + 0xA, 16));
            Assert.Equal(3, file.ReadByte(record + 0x22));
            Assert.Equal(700000, file.ReadInt32(record + 0x48));
            Assert.Equal(123, file.ReadInt32(record + 0x90));
            Assert.Equal(5, file.ReadUInt16(Base + Settings.CoachImageRecordOffset + 2 * Settings.CoachCompetencyRecordStride));
            Assert.Equal(42, file.ReadUInt16(competency + (3 * 6 + 1) * 2));
            Assert.Equal(1, file.ReadUInt16(record + 0x60));
            Assert.Equal(CoachEnums.VehiclePerk.Bahncard, coach.Vehicle);
            Assert.Equal(CoachEnums.HousingPerk.Keine, coach.Housing);
            Assert.Throws<ArgumentException>(() => coach.Level = 16);
            coach.HasExtensionOption = false;
            Assert.False(coach.HasExtensionOption);
        }

        // Board trust: base Int16 +0x94 plus bonus bytes +0x96/+0x97 (unsigned), +0x98/+0x99 (signed).
        [Fact]
        public void Coach_BoardTrust_ReadsTheCappedSumAndWritesOnlyTheBase()
        {
            SaveGameFile file = NewFile();
            file.WriteInt16(Base + Settings.ActiveTrainerCountOffset, 1);
            uint record = Base + Settings.CoachRecordTableOffset + Settings.CoachRecordStride;
            file.WriteInt16(record + 0x94, 50);
            file.WriteByte(record + 0x96, 20);
            file.WriteByte(record + 0x99, 0xFB);
            SaveGameCoach coach = SaveGameEditor.For(file).Coaches.Single();

            Assert.Equal((byte)65, coach.BoardTrust);

            coach.BoardTrust = 100;

            Assert.Equal((byte)100, coach.BoardTrust);
            Assert.Equal(85, file.ReadInt16(record + 0x94));
            Assert.Equal(20, file.ReadByte(record + 0x96));
            Assert.Throws<ArgumentException>(() => coach.BoardTrust = 101);
        }

        [Fact]
        public void Coach_ClubsAndStocksShowClubNames()
        {
            SaveGameFile file = NewFile();
            file.WriteInt16(Base + Settings.ActiveTrainerCountOffset, 1);
            file.WriteByte(Base + Settings.LeagueSystemByCountryOffset + (byte)PlayerEnums.Country.Deutschland, 0);
            PutClub(file, 5, PlayerEnums.Country.Deutschland, 5, "Hansa Rostock");
            uint record = Base + Settings.CoachRecordTableOffset + Settings.CoachRecordStride;
            file.WriteByte(record + 0x1C, (byte)PlayerEnums.Country.Deutschland);
            file.WriteByte(record + 0x1E, 5);
            file.WriteByte(record + 0xD8, (byte)PlayerEnums.Country.Deutschland);
            file.WriteByte(record + 0xDA, 5);
            SaveGameCoach coach = SaveGameEditor.For(file).Coaches.Single();

            coach.Stocks.Stock1.Shares = 30;
            coach.Stocks.Stock1.Price = 12;

            Assert.Equal("Hansa Rostock", coach.CurrentClub);
            Assert.Equal("-", coach.NextClub);
            Assert.Equal("30 x Hansa Rostock", coach.Stocks.Stock1.ToString());
            Assert.Equal("-", coach.Stocks.Stock2.ToString());
            Assert.Equal(30, file.ReadUInt16(record + 0xDC));
            Assert.Equal(12, file.ReadUInt16(record + 0xE0));
        }

        [Fact]
        public void Overview_ShowsTheSavegamesValues()
        {
            SaveGameFile file = NewFile();
            file.WriteInt16(Base + Settings.ActiveTrainerCountOffset, 2);
            file.AddName("Kahn");
            SaveGameOverview overview = SaveGameEditor.For(file).Overview;

            Assert.Equal(new DateTime(Year, 1, 1).AddDays(DayCounter).ToString("dd.MM.yyyy"), overview.Date);
            Assert.Equal(Year, overview.CalendarYear);
            Assert.Equal(2, overview.Trainers);
            Assert.Equal(1, overview.Names);
            Assert.Equal("Spielstand", overview.ToString());
        }

        // Reads every property the grid shows (including the expandable blocks and stocks), shows it
        // as text and writes it back unchanged - on the last records, so every field must lie inside
        // the savegame's blocks.
        private static void RoundTrip(object entry)
        {
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(entry, new Attribute[] { BrowsableAttribute.Yes }))
            {
                object value = property.GetValue(entry);
                Assert.NotNull(property.Converter.ConvertToString(new TestContext(entry, property), value));
                if (property.Converter is ExpandableObjectConverter)
                    RoundTrip(value);
                else if (!property.IsReadOnly)
                    property.SetValue(entry, value);
                Assert.Equal(value?.ToString(), property.GetValue(entry)?.ToString());
            }
        }

        [Fact]
        public void EveryProperty_ReadsAndWritesBack()
        {
            SaveGameFile file = NewFile();
            file.WriteInt16(Base + Settings.ActiveTrainerCountOffset, Coach.MaxTrainers);
            SaveGameEditor editor = SaveGameEditor.For(file);
            SaveGamePlayer player = editor.Player(SaveGamePlayer.RecordCount - 1);
            player.Firstname = "Erik";
            player.Lastname = "Meijer";
            player.Age = 30;
            player.YearsInClub = 2;

            RoundTrip(player);
            RoundTrip(new SaveGameClub(editor, SaveGameClub.RecordCount - 1));
            RoundTrip(editor.Coaches.Last());
            RoundTrip(editor.Overview);

            Assert.Equal("12 Blöcke", new SaveGameClub(editor, 0).Blocks.ToString());
            Assert.Equal("6 Plätze", editor.Coaches[0].Stocks.ToString());
        }

        [Fact]
        public void Overview_WithoutCalendarEntry_ShowsTheYear()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            file.WriteInt16(Base + Settings.AgeReferenceYearOffset, 1998);

            Assert.Equal("1998", SaveGameEditor.For(file).Overview.Date);
        }

        private sealed class TestContext : ITypeDescriptorContext
        {
            public TestContext(object instance, PropertyDescriptor property)
            {
                this.Instance = instance;
                this.PropertyDescriptor = property;
            }

            public IContainer Container => null;
            public object Instance { get; }
            public PropertyDescriptor PropertyDescriptor { get; }
            public object GetService(Type serviceType) => null;
            public void OnComponentChanged() { }
            public bool OnComponentChanging() => true;
        }
    }
}
