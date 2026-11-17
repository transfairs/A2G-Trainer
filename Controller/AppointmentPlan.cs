using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// The "Terminplaner" of a human-run club: the trainer's and the manager's plan over 42 days from
    /// the reference day (Sunday of the current week), grouped into appointments, which can be
    /// cancelled, moved and - some of them - added (see docs/savegame-format.md, "Terminplaner").
    /// </summary>
    /// <remarks>
    /// Most appointments only show in the plan; the game holds them in their own lists and takes them
    /// from there: a negotiation stays in #70 and takes place even with its plan entry gone, a test
    /// match in #62. Cancelling and moving therefore change that list first, then the plan. League and
    /// national matches are rebuilt from the fixtures every week, the court date from the player's
    /// suspension, tournaments from their own tables: those stay as they are. A plan entry is written
    /// to every working copy of the club and to its data block, and days 0-6 to the week overview of
    /// its human managers as FUN_006191b0 builds it.
    /// </remarks>
    internal sealed class AppointmentPlan
    {
        /// <summary>Days the plans cover.</summary>
        internal const int Days = 42;
        /// <summary>Bytes of a trainer plan day.</summary>
        internal const int TrainerEntrySize = 12;
        /// <summary>Bytes of a manager plan day.</summary>
        internal const int ManagerEntrySize = 8;
        /// <summary>Training units of a day (3 bytes).</summary>
        internal const int UnitsSize = 3;
        private const int WeekDays = 7;
        private const int NegotiationStride = 0x48;
        private const int NegotiationCapacity = 200;
        private const int ExtensionCapacity = 30;
        private const int TalkStride = 8;
        private const int TalkCapacity = 28;
        private const int TestMatchStride = 0x14;
        private const int TestMatchCount = 300;
        private const int PairStride = 10;
        private const int PairCapacity = 50;

        /// <summary>Where an appointment really lives, and so how it can be changed.</summary>
        internal enum Source
        {
            /// <summary>Only in the plan: changed there.</summary>
            Plan,
            /// <summary>A transfer negotiation in #70 (entry +2 day, +6 player).</summary>
            Negotiation,
            /// <summary>A contract extension in #71 (day, player).</summary>
            Extension,
            /// <summary>A talk with a youth player in #90 (player, ..., day at +6).</summary>
            YouthTalk,
            /// <summary>A talk with an amateur in #91 (as #90).</summary>
            AmateurTalk,
            /// <summary>A test match in #62 (status, day, the two clubs).</summary>
            TestMatch,
            /// <summary>Rebuilt by the game (fixtures, court, tournaments, appointments of other managers) or not known: read only.</summary>
            Game,
        }

        /// <summary>An appointment type (the plan entry's byte +1).</summary>
        internal sealed class Kind
        {
            internal Kind(byte code, string name, int length, bool trainer, bool manager, Source source)
            {
                this.Code = code;
                this.Name = name;
                this.Length = length;
                this.Trainer = trainer;
                this.Manager = manager;
                this.Source = source;
            }

            /// <summary>The type byte.</summary>
            public byte Code { get; }
            /// <summary>The game's name for it (FUN_00521830).</summary>
            public string Name { get; }
            /// <summary>Days it takes (table 0x6C7508, at least 1).</summary>
            public int Length { get; }
            /// <summary>Whether it goes into the trainer's plan (table 0x6C20C8 bit 0).</summary>
            public bool Trainer { get; }
            /// <summary>Whether it goes into the manager's plan (bit 1).</summary>
            public bool Manager { get; }
            /// <summary>Where it really lives.</summary>
            public Source Source { get; }
        }

        /// <summary>Camp type 0 (the KI's "KondiLager") and 1 ("RegenerationL").</summary>
        internal static readonly string[] CampKindNames = { "Kondition", "Regeneration" };

        /// <summary>Training units of a camp's 7 days per camp type (templates at 0x6C8598 + 0x1E, the same in both builds).</summary>
        internal static readonly byte[][] CampUnits =
        {
            new byte[] { 0x01, 0x0B, 0x00, 0x0B, 0x0C, 0x00, 0x0B, 0x0C, 0x00, 0x0B, 0x0C, 0x00, 0x0C, 0x0C, 0x00, 0x0B, 0x0C, 0x00, 0x0B, 0x07, 0x00 },
            new byte[] { 0x01, 0x14, 0x00, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x05, 0x00, 0x00, 0x00, 0x05, 0x12, 0x00, 0x11, 0x02, 0x00 },
        };

        /// <summary>Camp locations 1-22 (texts at 0x6BBBB8); index 0 is "fällt aus".</summary>
        internal static readonly string[] CampLocationNames =
        {
            "fällt aus",
            "Trainingscamp \"Novgorod\", Sibirien",
            "Sportschule Kaiserfranz",
            "Sporthotel \"Fabi\", Waldbüttelbrunn",
            "Höhentrainingslager in den Anden",
            "Hotel \"Lumberjack\", Alaska",
            "Hotel \"Hotzenblooz\", Bayerischer Wald",
            "Hotel \"Zum Hannes\", Dainbach (Baden)",
            "NORACSA Trainingscentrum Gütersloh",
            "Fitneßcamp Dr. chem. Do Ping, China",
            "Pension \"Druidenzirkel\", Stonehenge",
            "Hotel \"Deuxieme Ligue\", Marseille",
            "Vale do Garrao, Algarve",
            "Faro, Portugal",
            "Höhentrainingslager Davos",
            "Hotel \"Silver Train\", Maracaibo",
            "NORACSA Trainingscentrum Bahamas",
            "Hotel \"Vin Rouge\", Bordeaux",
            "Montego Bay, Jamaika",
            "Orlando, Florida",
            "Club \"Mondial\", Tahiti",
            "Reeperbahnplatz, St. Pauli",
            "Mondbasis Alpha",
        };

        /// <summary>Every known type.</summary>
        internal static readonly Kind[] Kinds =
        {
            new Kind(0x01, "Trainingswoche", 5, true, false, Source.Plan),
            new Kind(0x02, "Training", 1, true, false, Source.Plan),
            new Kind(0x03, "Trainingslager", 7, true, false, Source.Plan),
            new Kind(0x04, "Krankenbesuch", 1, true, true, Source.Plan),
            new Kind(0x05, "Trainingsfrei", 1, true, false, Source.Plan),
            new Kind(0x0A, "Vertragsverlängerung", 1, false, true, Source.Extension),
            new Kind(0x0B, "Vertragsverhandlung", 1, false, true, Source.Negotiation),
            new Kind(0x0C, "Vertragsverhandlung", 1, false, true, Source.Negotiation),
            new Kind(0x0D, "Vertragsverhandlung (Jugend)", 1, false, true, Source.YouthTalk),
            new Kind(0x0E, "Vertragsverhandlung (Amateur)", 1, false, true, Source.AmateurTalk),
            new Kind(0x0F, "Vorvertragsverhandlung", 1, false, true, Source.Game),
            new Kind(0x10, "Termin mit Mitspieler", 1, true, true, Source.Game),
            new Kind(0x11, "Gespräch", 1, false, true, Source.Plan),
            new Kind(0x12, "Fanclubtreffen", 1, false, true, Source.Plan),
            new Kind(0x13, "Vereinsfest", 1, false, true, Source.Plan),
            new Kind(0x14, "Freundschaftsspiel", 1, true, false, Source.TestMatch),
            new Kind(0x15, "Spiel", 1, true, true, Source.Game),
            new Kind(0x16, "Turnier", 2, true, false, Source.Game),
            new Kind(0x17, "Hallenturnier", 2, true, true, Source.Game),
            new Kind(0x18, "Werbetermin", 1, true, false, Source.Plan),
            new Kind(0x19, "Tag der offenen Tür", 1, true, true, Source.Plan),
            new Kind(0x1A, "Nachhilfeunterricht", 1, true, true, Source.Plan),
            new Kind(0x1B, "Verbandsgericht", 1, false, true, Source.Game),
            new Kind(0x1C, "Termin", 1, false, true, Source.Game),
        };

        /// <summary>The types that can be added: in the manager's plan only, both plans, and the camp and the test match.</summary>
        internal static readonly byte[] AddableKinds = { 0x03, 0x14, 0x12, 0x13, 0x19 };

        /// <summary>The type <paramref name="code"/>, or an unknown one shown by its number (read only).</summary>
        internal static Kind KindOf(byte code) => Kinds.FirstOrDefault(k => k.Code == code)
            ?? new Kind(code, $"Termin (Art {code})", 1, true, true, Source.Game);

        /// <summary>One appointment: a run of days with the same type in one or both plans.</summary>
        internal sealed class Appointment
        {
            /// <summary>Its type.</summary>
            public Kind Kind { get; set; }
            /// <summary>Its first day in the plans (0-41).</summary>
            public int Day { get; set; }
            /// <summary>Its days in the plans (fewer if it began before day 0).</summary>
            public int Length { get; set; }
            /// <summary>Whether it is in the trainer's plan.</summary>
            public bool InTrainerPlan { get; set; }
            /// <summary>Whether it is in the manager's plan.</summary>
            public bool InManagerPlan { get; set; }
            /// <summary>Whether it began before day 0 (the second half of a camp or tournament).</summary>
            public bool BeganBefore { get; set; }
            /// <summary>The trainer's plan entry of its first day (zero if not in that plan).</summary>
            public byte[] TrainerEntry { get; set; }
            /// <summary>The manager's plan entry of its first day (zero if not in that plan).</summary>
            public byte[] ManagerEntry { get; set; }

            /// <summary>The player it concerns (negotiations, talks, sick visit), or 0.</summary>
            public ushort PlayerId =>
                this.InManagerPlan && (this.Kind.Code == 0x04 || (this.Kind.Code >= 0x0A && this.Kind.Code <= 0x0F) || this.Kind.Code == 0x11)
                    ? BitConverter.ToUInt16(this.ManagerEntry, 2)
                    : this.Kind.Code == 0x04 ? BitConverter.ToUInt16(this.TrainerEntry, 4) : (ushort)0;

            /// <summary>The other club (selling club of a negotiation, opponent of a match), or null.</summary>
            public KeyValuePair<PlayerEnums.Country, ushort>? Club
            {
                get
                {
                    if (this.Kind.Code == 0x0B || this.Kind.Code == 0x0C)
                        return new KeyValuePair<PlayerEnums.Country, ushort>((PlayerEnums.Country)this.ManagerEntry[4], BitConverter.ToUInt16(this.ManagerEntry, 6));
                    if ((this.Kind.Code == 0x14 || this.Kind.Code == 0x15) && this.InTrainerPlan && BitConverter.ToUInt16(this.TrainerEntry, 6) != 0)
                        return new KeyValuePair<PlayerEnums.Country, ushort>((PlayerEnums.Country)this.TrainerEntry[4], BitConverter.ToUInt16(this.TrainerEntry, 6));
                    return null;
                }
            }

            /// <summary>Camp type (index in <see cref="CampKindNames"/>) of a camp.</summary>
            public int CampKind => BitConverter.ToInt16(this.TrainerEntry, 4);
            /// <summary>Camp location (index in <see cref="CampLocationNames"/>) of a camp.</summary>
            public int CampLocation => BitConverter.ToInt16(this.TrainerEntry, 0xA);
        }

        private readonly ProcessMemory memory;
        private readonly FixedAddresses fixedAddresses;
        private readonly List<uint> workingCopies;
        private readonly uint dataBlock;
        private readonly uint negotiations;
        private readonly uint extensions;
        private readonly byte[] managers;
        private readonly byte[] trainer;
        private readonly byte[] manager;
        private readonly byte[] units;
        // The plans as last written, to find the days whose week overview changes.
        private byte[] writtenTrainer, writtenManager;

        private AppointmentPlan(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, ushort clubId,
            List<uint> workingCopies, uint dataBlock, uint negotiations, uint extensions, short referenceDay, short today, byte[] managers, byte[] trainer, byte[] manager, byte[] units)
        {
            this.negotiations = negotiations;
            this.extensions = extensions;
            this.memory = memory;
            this.fixedAddresses = fixedAddresses;
            this.Country = country;
            this.ClubId = clubId;
            this.workingCopies = workingCopies;
            this.dataBlock = dataBlock;
            this.ReferenceDay = referenceDay;
            this.Today = today;
            this.managers = managers;
            this.trainer = trainer;
            this.manager = manager;
            this.units = units;
            this.writtenTrainer = (byte[])trainer.Clone();
            this.writtenManager = (byte[])manager.Clone();
        }

        /// <summary>Country of the club.</summary>
        public PlayerEnums.Country Country { get; }
        /// <summary>ID of the club.</summary>
        public ushort ClubId { get; }
        /// <summary>The season day of plan day 0 (Sunday of the current week).</summary>
        public short ReferenceDay { get; }
        /// <summary>Today's season day.</summary>
        public short Today { get; }
        /// <summary>Today's plan day.</summary>
        public int TodayIndex => this.Today - this.ReferenceDay;

        /// <summary>
        /// Reads the plans of the club <paramref name="country"/>/<paramref name="clubId"/>; null without
        /// a savegame, for a club without a human manager or a build whose addresses aren't known.
        /// </summary>
        internal static AppointmentPlan Read(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, ushort clubId) =>
            fixedAddresses == null ? null
                : Read(memory, fixedAddresses, country, clubId, fixedAddresses.Runtime(memory, Settings.NegotiationListOffset), fixedAddresses.Runtime(memory, Settings.ExtensionListOffset));

        /// <summary>As <see cref="Read(ProcessMemory, FixedAddresses, PlayerEnums.Country, ushort)"/>, with the negotiations (#70) and contract extensions (#71) at the given addresses.</summary>
        internal static AppointmentPlan Read(ProcessMemory memory, FixedAddresses fixedAddresses, PlayerEnums.Country country, ushort clubId, uint negotiations, uint extensions)
        {
            uint? record = ClubRecords.RecordAddress(memory, fixedAddresses, country, clubId);
            byte[] slot = record.HasValue ? memory.ReadBytesAtAddress(record.Value + Settings.ClubRecordDataSlotOffset, 1) : null;
            byte[] days = memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.GlobalCounterOffset), 4);
            if (slot == null || slot[0] == 0 || slot[0] > 4 || days == null)
                return null;

            uint dataBlock = fixedAddresses.Runtime(memory, Settings.ClubPlanDataOffset) + (uint)(slot[0] - 1) * Settings.ClubDataStride;
            List<uint> copies = ClubRecords.WorkingCopies(memory, fixedAddresses, country, clubId);
            // The working copy is the newer one while the game holds it; the data block otherwise.
            uint source = copies.Count > 0 ? copies[0] : 0;
            byte[] trainer = source != 0 ? memory.ReadBytesAtAddress(source + Settings.ClubTrainerPlanOffset, Days * TrainerEntrySize)
                : memory.ReadBytesAtAddress(dataBlock + Settings.ClubPlanDataTrainerOffset, Days * TrainerEntrySize);
            byte[] manager = source != 0 ? memory.ReadBytesAtAddress(source + Settings.ClubManagerPlanOffset, Days * ManagerEntrySize)
                : memory.ReadBytesAtAddress(dataBlock + Settings.ClubPlanDataManagerOffset, Days * ManagerEntrySize);
            byte[] units = source != 0 ? memory.ReadBytesAtAddress(source + Settings.ClubTrainingUnitsOffset, Days * UnitsSize)
                : memory.ReadBytesAtAddress(dataBlock + Settings.ClubPlanDataUnitsOffset, Days * UnitsSize);
            byte[] managers = source != 0 ? memory.ReadBytesAtAddress(source + Settings.ClubHumanOffset, 2) : null;
            if (trainer == null || manager == null || units == null)
                return null;

            return new AppointmentPlan(memory, fixedAddresses, country, clubId, copies, dataBlock, negotiations, extensions,
                BitConverter.ToInt16(days, 2), BitConverter.ToInt16(days, 0), managers ?? new byte[2], trainer, manager, units);
        }

        /// <summary>Reads the club's plans again, from the same places.</summary>
        internal AppointmentPlan Reload() => Read(this.memory, this.fixedAddresses, this.Country, this.ClubId, this.negotiations, this.extensions);

        private byte TrainerType(int day) => this.trainer[day * TrainerEntrySize + 1];
        private byte ManagerType(int day) => this.manager[day * ManagerEntrySize + 1];
        private short TrainerSequence(int day) => BitConverter.ToInt16(this.trainer, day * TrainerEntrySize + 2);
        private byte[] TrainerEntry(int day) => this.trainer.Skip(day * TrainerEntrySize).Take(TrainerEntrySize).ToArray();
        private byte[] ManagerEntry(int day) => this.manager.Skip(day * ManagerEntrySize).Take(ManagerEntrySize).ToArray();

        /// <summary>The appointments in both plans, by day (trainer's plan first).</summary>
        internal List<Appointment> Appointments()
        {
            List<Appointment> list = new List<Appointment>();
            Appointment run = null;
            for (int day = 0; day < Days; day++)
            {
                byte type = this.TrainerType(day);
                short sequence = this.TrainerSequence(day);
                Kind kind = type != 0 ? KindOf(type) : null;
                if (kind != null && kind.Length > 1 && run != null && run.Kind.Code == type && run.Day + run.Length == day && sequence > 1
                    && sequence <= kind.Length)
                {
                    run.Length++;
                }
                else if (kind != null)
                {
                    run = new Appointment
                    {
                        Kind = kind,
                        Day = day,
                        Length = 1,
                        InTrainerPlan = true,
                        BeganBefore = kind.Length > 1 && day == 0 && sequence > 1,
                        TrainerEntry = this.TrainerEntry(day),
                        ManagerEntry = new byte[ManagerEntrySize],
                    };
                    list.Add(run);
                }
                else
                {
                    run = null;
                }

                byte managerType = this.ManagerType(day);
                if (managerType == 0)
                    continue;
                Appointment same = list.FirstOrDefault(a => a.InTrainerPlan && a.Kind.Code == managerType && a.Day <= day && day < a.Day + a.Length);
                if (same != null)
                {
                    if (same.Day == day)
                        same.ManagerEntry = this.ManagerEntry(day);
                    same.InManagerPlan = true;
                    continue;
                }
                list.Add(new Appointment
                {
                    Kind = KindOf(managerType),
                    Day = day,
                    Length = 1,
                    InManagerPlan = true,
                    TrainerEntry = new byte[TrainerEntrySize],
                    ManagerEntry = this.ManagerEntry(day),
                });
            }
            return list;
        }

        /// <summary>Whether <paramref name="appointment"/> can be cancelled: one the game doesn't rebuild, not over yet.</summary>
        internal bool CanCancel(Appointment appointment) =>
            appointment.Kind.Source != Source.Game && appointment.Day + appointment.Length - 1 >= this.TodayIndex;

        /// <summary>Whether <paramref name="appointment"/> can be moved: as cancelled, and not begun yet.</summary>
        internal bool CanMove(Appointment appointment) =>
            this.CanCancel(appointment) && !appointment.BeganBefore && appointment.Day > this.TodayIndex;

        /// <summary>
        /// Whether an appointment of <paramref name="kind"/> fits at <paramref name="day"/>: after today,
        /// inside the plans, every day free in both plans (<paramref name="except"/> doesn't count, it is
        /// the one being moved), a camp on a Sunday.
        /// </summary>
        internal bool Fits(Kind kind, int day, Appointment except = null)
        {
            if (day <= this.TodayIndex || day + kind.Length > Days)
                return false;
            if (kind.Code == 0x03 && day % WeekDays != 0)
                return false;
            for (int d = day; d < day + kind.Length; d++)
            {
                bool own = except != null && except.Day <= d && d < except.Day + except.Length;
                if (own)
                    continue;
                if (this.TrainerType(d) != 0 || this.ManagerType(d) != 0)
                    return false;
            }
            return true;
        }

        /// <summary>Cancels <paramref name="appointment"/>: in its list, then in the plans. Returns whether it was cancelled.</summary>
        internal bool Cancel(Appointment appointment)
        {
            if (!this.CanCancel(appointment) || !this.ChangeList(appointment, null))
                return false;
            this.Clear(appointment);
            this.Write();
            Logger.Debug($"AppointmentPlan: {appointment.Kind.Name} on day {this.ReferenceDay + appointment.Day} cancelled.");
            return true;
        }

        /// <summary>Moves <paramref name="appointment"/> to <paramref name="day"/>: in its list, then in the plans. Returns whether it was moved.</summary>
        internal bool Move(Appointment appointment, int day)
        {
            if (!this.CanMove(appointment) || !this.Fits(appointment.Kind, day, appointment) || !this.ChangeList(appointment, day))
                return false;
            byte[][] trainerDays = Enumerable.Range(appointment.Day, appointment.Length).Select(this.TrainerEntry).ToArray();
            byte[][] managerDays = Enumerable.Range(appointment.Day, appointment.Length).Select(this.ManagerEntry).ToArray();
            byte[][] unitDays = Enumerable.Range(appointment.Day, appointment.Length).Select(d => this.units.Skip(d * UnitsSize).Take(UnitsSize).ToArray()).ToArray();
            bool training = appointment.Kind.Code == 0x01 || appointment.Kind.Code == 0x03;
            this.Clear(appointment);
            for (int i = 0; i < appointment.Length; i++)
            {
                if (appointment.InTrainerPlan)
                    trainerDays[i].CopyTo(this.trainer, (day + i) * TrainerEntrySize);
                if (appointment.InManagerPlan)
                    managerDays[i].CopyTo(this.manager, (day + i) * ManagerEntrySize);
                if (training)
                    unitDays[i].CopyTo(this.units, (day + i) * UnitsSize);
            }
            this.Write();
            Logger.Debug($"AppointmentPlan: {appointment.Kind.Name} moved from day {this.ReferenceDay + appointment.Day} to {this.ReferenceDay + day}.");
            return true;
        }

        /// <summary>Adds a camp of type <paramref name="campKind"/> at <paramref name="location"/> (1-22) for the week from <paramref name="day"/>. Returns whether it was added.</summary>
        internal bool AddCamp(int day, int campKind, int location)
        {
            Kind kind = KindOf(0x03);
            if (campKind < 0 || campKind >= CampUnits.Length || location < 1 || location >= CampLocationNames.Length || !this.Fits(kind, day))
                return false;
            for (int i = 0; i < kind.Length; i++)
            {
                byte[] entry = new byte[TrainerEntrySize];
                entry[1] = kind.Code;
                BitConverter.GetBytes((short)(i + 1)).CopyTo(entry, 2);
                BitConverter.GetBytes((short)campKind).CopyTo(entry, 4);
                BitConverter.GetBytes((short)location).CopyTo(entry, 0xA);
                entry.CopyTo(this.trainer, (day + i) * TrainerEntrySize);
                Array.Copy(CampUnits[campKind], i * UnitsSize, this.units, (day + i) * UnitsSize, UnitsSize);
            }
            this.Write();
            Logger.Debug($"AppointmentPlan: camp {campKind} at {location} from day {this.ReferenceDay + day} added.");
            return true;
        }

        /// <summary>Adds an appointment that lives in the plan only (Fanclubtreffen, Vereinsfest, Tag der offenen Tür) at <paramref name="day"/>. Returns whether it was added.</summary>
        internal bool AddSimple(byte code, int day)
        {
            Kind kind = KindOf(code);
            if (!AddableKinds.Contains(code) || kind.Source != Source.Plan || code == 0x03 || !this.Fits(kind, day))
                return false;
            if (kind.Trainer)
            {
                byte[] entry = new byte[TrainerEntrySize];
                entry[1] = code;
                entry[2] = 1;
                entry.CopyTo(this.trainer, day * TrainerEntrySize);
            }
            if (kind.Manager)
            {
                byte[] entry = new byte[ManagerEntrySize];
                entry[1] = code;
                entry.CopyTo(this.manager, day * ManagerEntrySize);
            }
            this.Write();
            Logger.Debug($"AppointmentPlan: {kind.Name} on day {this.ReferenceDay + day} added.");
            return true;
        }

        /// <summary>
        /// Arranges a test match against <paramref name="opponent"/> at <paramref name="day"/>: a free
        /// #62 entry (the club as A, <paramref name="away"/> gives the opponent home advantage, the
        /// starting fee factor <paramref name="fee"/>, 0 shares the takings), the pair in #113 and the
        /// trainer's plan. Returns whether it was arranged.
        /// </summary>
        internal bool AddTestMatch(int day, PlayerEnums.Country opponentCountry, ushort opponentId, bool away, byte fee)
        {
            Kind kind = KindOf(0x14);
            if (!this.Fits(kind, day) || (opponentCountry == this.Country && opponentId == this.ClubId))
                return false;
            uint table = this.fixedAddresses.Runtime(this.memory, Settings.TestMatchTableOffset);
            byte[] matches = this.memory.ReadBytesAtAddress(table, TestMatchCount * TestMatchStride);
            if (matches == null)
                return false;
            short date = (short)(this.ReferenceDay + day);
            int free = -1;
            for (int i = 0; i < TestMatchCount; i++)
            {
                int at = i * TestMatchStride;
                if (matches[at + 1] == 0)
                {
                    if (free < 0)
                        free = i;
                    continue;
                }
                // The opponent may not play another test match that day.
                if (BitConverter.ToInt16(matches, at + 2) == date && (IsClub(matches, at + 4, opponentCountry, opponentId) || IsClub(matches, at + 8, opponentCountry, opponentId)))
                    return false;
            }
            if (free < 0)
                return false;

            byte[] match = new byte[TestMatchStride];
            match[0] = matches[free * TestMatchStride];
            match[1] = 1;
            BitConverter.GetBytes(date).CopyTo(match, 2);
            match[4] = (byte)this.Country;
            BitConverter.GetBytes(this.ClubId).CopyTo(match, 6);
            match[8] = (byte)opponentCountry;
            BitConverter.GetBytes(opponentId).CopyTo(match, 0xA);
            match[0x12] = (byte)(away ? 1 : 0);
            match[0x13] = fee;
            this.memory.WriteBytesAtAddress(table + (uint)(free * TestMatchStride), match);

            uint pairs = this.fixedAddresses.Runtime(this.memory, Settings.FriendlyPairListOffset);
            byte[] count = this.memory.ReadBytesAtAddress(pairs, 2);
            int pairCount = count != null ? BitConverter.ToInt16(count, 0) : PairCapacity;
            if (pairCount >= 0 && pairCount < PairCapacity)
            {
                byte[] pair = new byte[PairStride];
                BitConverter.GetBytes(date).CopyTo(pair, 0);
                BitConverter.GetBytes((short)this.Country).CopyTo(pair, 2);
                BitConverter.GetBytes(this.ClubId).CopyTo(pair, 4);
                BitConverter.GetBytes((short)opponentCountry).CopyTo(pair, 6);
                BitConverter.GetBytes(opponentId).CopyTo(pair, 8);
                this.memory.WriteBytesAtAddress(pairs + 2 + (uint)(pairCount * PairStride), pair);
                this.memory.WriteBytesAtAddress(pairs, BitConverter.GetBytes((short)(pairCount + 1)));
            }

            byte[] entry = new byte[TrainerEntrySize];
            entry[1] = kind.Code;
            entry[2] = 1;
            entry[4] = (byte)opponentCountry;
            BitConverter.GetBytes(opponentId).CopyTo(entry, 6);
            entry[8] = (byte)(away ? 1 : 0);
            entry[0xB] = fee;
            entry.CopyTo(this.trainer, day * TrainerEntrySize);
            this.Write();
            Logger.Debug($"AppointmentPlan: test match against {opponentCountry}/{opponentId} on day {date} (#62 entry {free}).");
            return true;
        }

        private static bool IsClub(byte[] data, int at, PlayerEnums.Country country, ushort id) =>
            data[at] == (byte)country && BitConverter.ToUInt16(data, at + 2) == id;

        // Takes the plan days of the appointment out (type and day number, as the game's dialog does;
        // the manager's entry only by its type).
        private void Clear(Appointment appointment)
        {
            for (int d = appointment.Day; d < appointment.Day + appointment.Length; d++)
            {
                if (appointment.InTrainerPlan)
                {
                    this.trainer[d * TrainerEntrySize + 1] = 0;
                    this.trainer[d * TrainerEntrySize + 2] = 0;
                    this.trainer[d * TrainerEntrySize + 3] = 0;
                }
                if (appointment.InManagerPlan)
                    this.manager[d * ManagerEntrySize + 1] = 0;
            }
        }

        // Cancels (day null) or moves the appointment in the list it lives in; true for one that lives
        // in the plan only. False if its list entry isn't there.
        private bool ChangeList(Appointment appointment, int? day)
        {
            short date = (short)(this.ReferenceDay + appointment.Day);
            short? newDate = day.HasValue ? (short)(this.ReferenceDay + day.Value) : (short?)null;
            ushort player = appointment.PlayerId;
            switch (appointment.Kind.Source)
            {
                case Source.Plan:
                    return true;
                case Source.Negotiation:
                    return this.ChangeEntry(this.negotiations, 4, NegotiationStride, NegotiationCapacity, 2, newDate,
                        e => BitConverter.ToInt16(e, 2) == date && BitConverter.ToUInt16(e, 6) == player);
                case Source.Extension:
                    return this.ChangeEntry(this.extensions, 2, 4, ExtensionCapacity, 0, newDate,
                        e => BitConverter.ToInt16(e, 0) == date && BitConverter.ToUInt16(e, 2) == player);
                case Source.YouthTalk:
                case Source.AmateurTalk:
                    return this.ChangeEntry(this.fixedAddresses.Runtime(this.memory, appointment.Kind.Source == Source.YouthTalk ? Settings.YouthTalkListOffset : Settings.AmateurTalkListOffset),
                        2, TalkStride, TalkCapacity, 6, newDate,
                        e => BitConverter.ToInt16(e, 6) == date && BitConverter.ToUInt16(e, 0) == player);
                case Source.TestMatch:
                    return this.ChangeTestMatch(appointment, date, newDate);
                default:
                    return false;
            }
        }

        // The list entry the test finds: moved to newDate, or taken out as the game's own functions do
        // (FUN_00634410: the following entries move up, the count goes down).
        private bool ChangeEntry(uint list, int head, int stride, int capacity, int dayAt, short? newDate, Func<byte[], bool> test)
        {
            byte[] count = this.memory.ReadBytesAtAddress(list, 2);
            short n = count != null ? BitConverter.ToInt16(count, 0) : (short)0;
            if (n <= 0 || n > capacity)
                return false;
            byte[] entries = this.memory.ReadBytesAtAddress(list + (uint)head, n * stride);
            if (entries == null)
                return false;
            for (int i = 0; i < n; i++)
            {
                byte[] entry = entries.Skip(i * stride).Take(stride).ToArray();
                if (!test(entry))
                    continue;
                uint at = list + (uint)(head + i * stride);
                if (newDate.HasValue)
                {
                    this.memory.WriteBytesAtAddress(at + (uint)dayAt, BitConverter.GetBytes(newDate.Value));
                    return true;
                }
                byte[] rest = entries.Skip((i + 1) * stride).ToArray();
                if (rest.Length > 0)
                    this.memory.WriteBytesAtAddress(at, rest);
                this.memory.WriteBytesAtAddress(list, BitConverter.GetBytes((short)(n - 1)));
                return true;
            }
            return false;
        }

        // The test match in #62 (status 1, the day, the club and its opponent): status 0 to cancel, the
        // new day to move - then also its pair in #113.
        private bool ChangeTestMatch(Appointment appointment, short date, short? newDate)
        {
            KeyValuePair<PlayerEnums.Country, ushort>? opponent = appointment.Club;
            uint table = this.fixedAddresses.Runtime(this.memory, Settings.TestMatchTableOffset);
            byte[] matches = this.memory.ReadBytesAtAddress(table, TestMatchCount * TestMatchStride);
            if (matches == null)
                return false;
            for (int i = 0; i < TestMatchCount; i++)
            {
                int at = i * TestMatchStride;
                if (matches[at + 1] != 1 || BitConverter.ToInt16(matches, at + 2) != date)
                    continue;
                bool ownA = IsClub(matches, at + 4, this.Country, this.ClubId), ownB = IsClub(matches, at + 8, this.Country, this.ClubId);
                if (!ownA && !ownB)
                    continue;
                if (opponent.HasValue && !IsClub(matches, ownA ? at + 8 : at + 4, opponent.Value.Key, opponent.Value.Value))
                    continue;
                uint entry = table + (uint)at;
                if (!newDate.HasValue)
                {
                    this.memory.WriteBytesAtAddress(entry + 1, new byte[] { 0 });
                    return true;
                }
                this.memory.WriteBytesAtAddress(entry + 2, BitConverter.GetBytes(newDate.Value));
                this.MovePair(date, newDate.Value, matches, at);
                return true;
            }
            return false;
        }

        // The pair of the test match at `at` in #113 gets the new day (it keeps a pair for 42 days).
        private void MovePair(short date, short newDate, byte[] matches, int at)
        {
            uint pairs = this.fixedAddresses.Runtime(this.memory, Settings.FriendlyPairListOffset);
            byte[] count = this.memory.ReadBytesAtAddress(pairs, 2);
            short n = count != null ? BitConverter.ToInt16(count, 0) : (short)0;
            if (n <= 0 || n > PairCapacity)
                return;
            byte[] entries = this.memory.ReadBytesAtAddress(pairs + 2, n * PairStride);
            if (entries == null)
                return;
            for (int i = 0; i < n; i++)
            {
                int p = i * PairStride;
                bool same = BitConverter.ToInt16(entries, p) == date
                    && entries[p + 2] == matches[at + 4] && BitConverter.ToUInt16(entries, p + 4) == BitConverter.ToUInt16(matches, at + 6)
                    && entries[p + 6] == matches[at + 8] && BitConverter.ToUInt16(entries, p + 8) == BitConverter.ToUInt16(matches, at + 0xA);
                if (same)
                {
                    this.memory.WriteBytesAtAddress(pairs + 2 + (uint)p, BitConverter.GetBytes(newDate));
                    return;
                }
            }
        }

        // The plans go to every working copy of the club and to its data block, the days 0-6 that
        // changed also to the week overview of its human managers (an unchanged day keeps what the
        // game put there, e.g. a national match).
        private void Write()
        {
            foreach (uint copy in this.workingCopies)
            {
                this.memory.WriteBytesAtAddress(copy + Settings.ClubTrainerPlanOffset, this.trainer);
                this.memory.WriteBytesAtAddress(copy + Settings.ClubManagerPlanOffset, this.manager);
                this.memory.WriteBytesAtAddress(copy + Settings.ClubTrainingUnitsOffset, this.units);
            }
            this.memory.WriteBytesAtAddress(this.dataBlock + Settings.ClubPlanDataTrainerOffset, this.trainer);
            this.memory.WriteBytesAtAddress(this.dataBlock + Settings.ClubPlanDataManagerOffset, this.manager);
            this.memory.WriteBytesAtAddress(this.dataBlock + Settings.ClubPlanDataUnitsOffset, this.units);

            foreach (byte number in this.managers.Where(m => m != 0 && m <= 4).Distinct())
            {
                uint slot = this.fixedAddresses.Runtime(this.memory, Settings.CoachSlotTableOffset) + number * Settings.CoachSlotStride + Settings.CoachSlotWeekOffset;
                uint record = this.fixedAddresses.Runtime(this.memory, Settings.CoachRecordTableOffset) + number * Settings.CoachRecordStride + Settings.CoachRecordWeekOffset;
                for (int day = 0; day < WeekDays; day++)
                {
                    if (!this.Changed(day))
                        continue;
                    byte[] overview = this.Overview(day);
                    this.memory.WriteBytesAtAddress(slot + (uint)(day * TrainerEntrySize), overview);
                    this.memory.WriteBytesAtAddress(record + (uint)(day * TrainerEntrySize), overview);
                }
            }
            this.writtenTrainer = (byte[])this.trainer.Clone();
            this.writtenManager = (byte[])this.manager.Clone();
        }

        private bool Changed(int day) =>
            !this.trainer.Skip(day * TrainerEntrySize).Take(TrainerEntrySize).SequenceEqual(this.writtenTrainer.Skip(day * TrainerEntrySize).Take(TrainerEntrySize))
            || !this.manager.Skip(day * ManagerEntrySize).Take(ManagerEntrySize).SequenceEqual(this.writtenManager.Skip(day * ManagerEntrySize).Take(ManagerEntrySize));

        /// <summary>The week overview's entry of <paramref name="day"/> (0-6) as FUN_006191b0 builds it from the plans.</summary>
        internal byte[] Overview(int day)
        {
            byte[] overview = new byte[TrainerEntrySize];
            byte managerType = this.ManagerType(day);
            byte trainerType = this.TrainerType(day);
            int m = day * ManagerEntrySize, t = day * TrainerEntrySize;
            if (managerType != 0)
            {
                overview[1] = managerType;
                switch (managerType)
                {
                    case 0x04: case 0x0A: case 0x0D: case 0x0E: case 0x0F: case 0x11: case 0x1B:
                        Array.Copy(this.manager, m + 2, overview, 4, 2);
                        break;
                    case 0x0B: case 0x0C:
                        Array.Copy(this.manager, m + 2, overview, 4, 6);
                        break;
                    case 0x10:
                        Array.Copy(this.manager, m + 2, overview, 4, 4);
                        break;
                    case 0x15:
                        Array.Copy(this.trainer, t + 4, overview, 4, 6);
                        break;
                }
            }
            else if (trainerType != 0)
            {
                overview[1] = trainerType;
                Array.Copy(this.trainer, t + 2, overview, 2, 2);
                switch (trainerType)
                {
                    case 0x03: case 0x14:
                        Array.Copy(this.trainer, t + 4, overview, 4, 8);
                        break;
                    case 0x04:
                        Array.Copy(this.trainer, t + 4, overview, 4, 2);
                        break;
                    case 0x10: case 0x16: case 0x17:
                        Array.Copy(this.trainer, t + 4, overview, 4, 4);
                        break;
                    case 0x15:
                        Array.Copy(this.trainer, t + 4, overview, 4, 6);
                        break;
                }
            }
            return overview;
        }
    }
}
