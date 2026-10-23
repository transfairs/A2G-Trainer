using System;

namespace A2G_Trainer_XP.Controller
{
    /// <summary>
    /// The savegame's calendar values that injury-related player-record fields are stored against
    /// (see Settings.StartYearOffset; other builds move it like all runtime data, see FixedAddresses),
    /// with the game's own conversions between those stored values and what the game shows
    /// (FUN_00582a60 unpacks, FUN_00583120 packs).
    /// </summary>
    public sealed class GameCalendar
    {
        private readonly ProcessMemory memory;
        // Distance of the calendar data from the original build's addresses (see FixedAddresses.RuntimeShiftFor).
        private readonly uint shift;

        private GameCalendar(ProcessMemory memory, uint shift, short startYear, short dayCounter, short daysPerYear, short globalCounter)
        {
            this.memory = memory;
            this.shift = shift;
            this.StartYear = startYear;
            this.DayCounter = dayCounter;
            this.DaysPerYear = daysPerYear;
            this.GlobalCounter = globalCounter;
        }

        /// <summary>The year the savegame started in.</summary>
        public short StartYear { get; }
        /// <summary>Days since 1 January of <see cref="StartYear"/> - "today".</summary>
        public short DayCounter { get; }
        /// <summary>Days per year the game uses when turning an end date back into a number of days (365).</summary>
        public short DaysPerYear { get; }
        /// <summary>The counter injury starts and vulnerability end values are stored against.</summary>
        public short GlobalCounter { get; }

        /// <summary>Reads the calendar of the attached savegame, or null if it isn't known for this build or can't be read.</summary>
        public static GameCalendar Read(ProcessMemory memory)
        {
            uint? shift = FixedAddresses.RuntimeShiftFor(memory, memory.Layout);
            if (!shift.HasValue)
                return null;

            short? startYear = ReadInt16(memory, Settings.StartYearOffset + shift.Value);
            short? dayCounter = ReadInt16(memory, Settings.DayCounterOffset + shift.Value);
            short? daysPerYear = ReadInt16(memory, Settings.DaysPerYearOffset + shift.Value);
            short? globalCounter = ReadInt16(memory, Settings.GlobalCounterOffset + shift.Value);
            if (!startYear.HasValue || !dayCounter.HasValue || !daysPerYear.HasValue || !globalCounter.HasValue || daysPerYear.Value <= 0)
                return null;

            return new GameCalendar(memory, shift.Value, startYear.Value, dayCounter.Value, daysPerYear.Value, globalCounter.Value);
        }

        /// <summary>Remaining injury days for a stored end date, exactly as the game unpacks it (never negative).</summary>
        public ushort InjuredDaysFromEndDate(short endYear, short endDayOfYear)
        {
            if (endYear < this.StartYear)
                return 0;

            int days = (endYear - this.StartYear) * this.DaysPerYear + endDayOfYear - this.DayCounter;
            return days < 0 ? (ushort)0 : (ushort)days;
        }

        /// <summary>
        /// The end date (year, day of year) for an injury lasting <paramref name="injuredDays"/> more
        /// days, looked up in the game's own calendar table the way the game packs it - or null if
        /// that day lies outside the table.
        /// </summary>
        public Tuple<short, short> EndDateForInjuredDays(ushort injuredDays)
        {
            int dayIndex = this.DayCounter + injuredDays;
            byte[] entry = this.memory.ReadBytesAtAddress(this.memory.ModuleBase + Settings.CalendarTableOffset + this.shift + (uint)dayIndex * 4, 4);
            if (entry == null)
                return null;

            short year = BitConverter.ToInt16(entry, 0);
            short dayOfYear = BitConverter.ToInt16(entry, 2);
            return year == 0 ? null : Tuple.Create(year, dayOfYear);
        }

        /// <summary>Remaining vulnerability for a stored end value (0 = not vulnerable), as the game unpacks it into its one-byte field.</summary>
        public byte VulnerableTicksFromEndValue(ushort endValue) => endValue == 0 ? (byte)0 : (byte)(endValue - this.GlobalCounter);

        /// <summary>The stored end value for <paramref name="ticks"/> more vulnerability ticks (0 = not vulnerable).</summary>
        public ushort VulnerableEndValueForTicks(byte ticks) => ticks == 0 ? (ushort)0 : (ushort)(this.GlobalCounter + ticks);

        private static short? ReadInt16(ProcessMemory memory, uint moduleOffset)
        {
            byte[] bytes = memory.ReadBytesAtAddress(memory.ModuleBase + moduleOffset, 2);
            return bytes == null ? (short?)null : BitConverter.ToInt16(bytes, 0);
        }
    }
}
