using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Linq;
using System.Reflection;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Further tests for the savegame editor's entries: the calendar's edge cases, empty and
    /// refused names, the career and extension-option bits, club records and the club ID converter
    /// without a club.
    /// </summary>
    public class SaveGameEntriesAdditionalTests
    {
        private const short Year = 2001;
        private const short DayCounter = 100;
        private const uint Base = SaveGameFile.ModuleBase;

        private static SaveGameFile NewFile()
        {
            SaveGameFile file = SaveGameFile.CreateBlank();
            file.WriteInt16(Base + Settings.AgeReferenceYearOffset, Year);
            file.WriteInt16(Base + Settings.StartYearOffset, Year);
            file.WriteInt16(Base + Settings.DayCounterOffset, DayCounter);
            file.WriteInt16(Base + Settings.DaysPerYearOffset, 365);
            return file;
        }

        private static uint Record(ushort id, uint offset) => Base + Settings.PlayerRecordTableOffset + id * Settings.PlayerRecordStride + offset;

        private static uint ClubRecord(int index, uint offset) => Base + Settings.ClubRecordTableOffset + (uint)index * Settings.ClubRecordStride + offset;

        [Fact]
        public void Calendar_DateOf_OutsideTheTable_IsNull()
        {
            SaveGameFile file = NewFile();
            file.WriteInt16(Base + Settings.CalendarTableOffset + 4, Year);
            file.WriteInt16(Base + Settings.CalendarTableOffset + 6, 1);
            SaveGameCalendar calendar = new SaveGameCalendar(file);
            const int farAway = 0x400000;

            Assert.Equal(Tuple.Create(Year, (short)1), calendar.DateOf(1));
            Assert.Null(calendar.DateOf(-1));
            Assert.False(file.Contains(Base + Settings.CalendarTableOffset + (uint)farAway * 4, 4));
            Assert.Null(calendar.DateOf(farAway));
        }

        [Fact]
        public void Calendar_InjuryEndBeforeToday_CountsAsHealed()
        {
            SaveGameEditor editor = SaveGameEditor.For(NewFile());
            // Day 50 of this year, but today is day 100.
            editor.File.WriteInt16(Record(4, Settings.PlayerRecordInjuryEndYearOffset), Year);
            editor.File.WriteInt16(Record(4, Settings.PlayerRecordInjuryEndYearOffset + 2), 50);

            Assert.Equal(0, editor.Player(4).InjuredDays);
            Assert.Equal(0, editor.Calendar.InjuredDaysFromEndDate(Year, 99));
            Assert.Equal(1, editor.Calendar.InjuredDaysFromEndDate(Year, 101));
        }

        [Fact]
        public void FindPlayers_WithoutText_FindsNobody()
        {
            SaveGameEditor editor = SaveGameEditor.For(NewFile());
            // Index 0 marks an unused record, so the name must not be the first one.
            editor.File.AddName("Oliver");
            ushort name = (ushort)editor.File.AddName("Kahn");
            editor.File.WriteUInt16(Record(1, 2), name);

            Assert.Empty(editor.FindPlayers(null, 10));
            Assert.Empty(editor.FindPlayers("  ", 10));
            Assert.Single(editor.FindPlayers("kahn", 10));
        }

        [Fact]
        public void Player_NullName_IsRefused()
        {
            SaveGamePlayer player = SaveGameEditor.For(NewFile()).Player(1);

            ArgumentException error = Assert.Throws<ArgumentException>(() => player.Lastname = null);
            Assert.Contains("leer", error.Message);
        }

        [Fact]
        public void Player_NameWhenThePoolIsFull_IsRefused()
        {
            SaveGameEditor editor = SaveGameEditor.For(NewFile());
            List<int> offsets = (List<int>)typeof(SaveGameFile).GetField("nameOffsets", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(editor.File);
            offsets.AddRange(Enumerable.Repeat(0, Settings.NamePoolTableCapacity));
            SaveGamePlayer player = editor.Player(1);

            ArgumentException error = Assert.Throws<ArgumentException>(() => player.Firstname = "Neu");
            Assert.Contains("voll", error.Message);
            Assert.Equal(0, editor.File.ReadUInt16(Record(1, 0)));
        }

        [Fact]
        public void Player_NationalTeamRetired_SetsAndClearsOnlyItsBit()
        {
            SaveGameEditor editor = SaveGameEditor.For(NewFile());
            uint career = Record(6, Settings.PlayerRecordCareerOffset);
            editor.File.WriteByte(career, 0x01);
            SaveGamePlayer player = editor.Player(6);

            player.NationalTeamRetired = true;
            Assert.True(player.NationalTeamRetired);
            Assert.Equal(0x01 | (byte)PlayerEnums.Career.NationalTeamRetired, editor.File.ReadByte(career));

            player.NationalTeamRetired = false;
            Assert.False(player.NationalTeamRetired);
            Assert.Equal(0x01, editor.File.ReadByte(career));
        }

        [Fact]
        public void Club_NullText_ClearsTheField()
        {
            SaveGameEditor editor = SaveGameEditor.For(NewFile());
            SaveGameClub club = new SaveGameClub(editor, 3);
            club.Name = "Rot-Weiss Essen";
            Assert.Equal("Rot-Weiss Essen", club.ToString());

            club.Name = null;

            Assert.Equal(string.Empty, club.Name);
            Assert.Equal(string.Empty, editor.File.ReadString(ClubRecord(3, 0), 20));
        }

        [Fact]
        public void Club_KnowsItsRecordIndexAndName()
        {
            SaveGameFile file = NewFile();
            file.WriteString(ClubRecord(7, 0), "VfL Bochum", 20);
            SaveGameClub club = new SaveGameClub(SaveGameEditor.For(file), 7);

            Assert.Equal(7, club.RecordIndex);
            Assert.Equal("VfL Bochum", club.ToString());
            Assert.Equal(ClubRecord(7, 0), club.Address);
        }

        [Fact]
        public void Coach_ExtensionOption_KeepsAnExistingValue()
        {
            SaveGameFile file = NewFile();
            file.WriteInt16(Base + Settings.ActiveTrainerCountOffset, 1);
            uint option = Base + Settings.CoachRecordTableOffset + Settings.CoachRecordStride + 0x60;
            file.WriteUInt16(option, 2);
            SaveGameCoach coach = SaveGameEditor.For(file).Coaches.Single();

            coach.HasExtensionOption = true;

            Assert.True(coach.HasExtensionOption);
            Assert.Equal(2, file.ReadUInt16(option));
        }

        [Fact]
        public void ClubIdConverter_WithoutAClub_ShowsNumbers()
        {
            ClubIdConverter converter = new ClubIdConverter();
            TestContext notAClub = new TestContext(new object());

            Assert.Equal("4", converter.ConvertTo(null, null, (byte)4, typeof(string)));
            Assert.Equal("4", converter.ConvertTo(notAClub, null, (byte)4, typeof(string)));
            Assert.Empty(converter.GetStandardValues(notAClub).Cast<object>());
            Assert.Equal((byte)12, converter.ConvertFrom(null, null, "12"));
            Assert.Throws<ArgumentException>(() => converter.ConvertFrom(notAClub, null, "Schalke"));
        }

        [Fact]
        public void ClubIdConverter_OtherTypes_GoToTheBaseConverter()
        {
            ClubIdConverter converter = new ClubIdConverter();

            Assert.True(converter.CanConvertFrom(null, typeof(string)));
            Assert.False(converter.CanConvertFrom(null, typeof(int)));
            Assert.True(converter.CanConvertFrom(null, typeof(InstanceDescriptor)));
            Assert.True(converter.CanConvertTo(null, typeof(string)));
            Assert.False(converter.CanConvertTo(null, typeof(int)));
            // Not a club ID: the base converter's plain text.
            Assert.Equal("7", converter.ConvertTo(null, null, 7, typeof(string)));
            Assert.Throws<NotSupportedException>(() => converter.ConvertTo(null, null, (byte)7, typeof(int)));
            Assert.Throws<NotSupportedException>(() => converter.ConvertFrom(null, null, 7));
        }

        private sealed class TestContext : ITypeDescriptorContext
        {
            public TestContext(object instance) { this.Instance = instance; }
            public IContainer Container => null;
            public object Instance { get; }
            public PropertyDescriptor PropertyDescriptor => null;
            public object GetService(Type serviceType) => null;
            public void OnComponentChanged() { }
            public bool OnComponentChanging() => true;
        }
    }
}
