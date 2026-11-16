using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Shows in the game's speech dialog (Mannschaftssitzung) what each speech would do in the coming
    /// match: a colour - green if the game would accept it, red if it doesn't fit the situation, grey if
    /// it is used up for this season - and its effect and uses left in front of its text, e.g. "+3% 2x ".
    /// </summary>
    /// <remarks>
    /// The verdict is the game's own: for each speech the dialog adds to its list, the stub runs the
    /// game's check of the chosen speech (FUN_004c3a20, which otherwise runs when the dialog is
    /// confirmed) with that speech as the chosen one, and leaves it as soon as its switch over the
    /// speeches has decided - at the call that releases the manager, before any message or effect.
    /// The speech number and its uses left (the check takes one) are put back afterwards. From speech 28
    /// on the game also rolls against the manager's motivation skill when the dialog is confirmed; the
    /// stub reads that skill from the manager the check releases, and a fitting motivation speech gets
    /// the chance of that roll after its note, e.g. "+5% 1x (71%) ".
    ///
    /// The list call goes to the stub (<see cref="CodeOffset"/>, thiscall: text, colour, number; esi =
    /// the dialog), and so does the check's release call (<see cref="ReleaseCodeOffset"/>, cdecl: the
    /// manager), which only acts while the list stub runs a check. The list stub then looks up the
    /// dialog's club in up to four tables the trainer fills and puts the club's note in front of the
    /// text in its own buffer (the list keeps at most 0xBF characters), which it passes instead of the
    /// game's text. The redirects live in the running process only and stay installed; switched off, the
    /// stub's flag is cleared and both calls pass straight through.
    ///
    /// Stub layout: +0x00 signature, +0x08 flag, +0x09 a check runs, +0x0A its verdict (0xFF = none),
    /// +0x0C the list function, +0x10 the check, +0x14 the release function (all absolute), +0x18 the
    /// list stub's stack pointer, +0x1C the club, +0x20 the speech number, +0x24/+0x26 the club's speech
    /// and uses left as they were, +0x28 the manager's motivation skill, +0x40 the release code, +0x90
    /// the list code, +0x280 the text buffer (0xC0 bytes), +0x340 four tables: Int32 key (country * 256 + club ID, -1 = free), then per speech
    /// number 0..43 0x40 bytes: the note (Latin-1, NUL-terminated).
    /// </remarks>
    internal sealed class SpeechEffectsDisplay : IDisposable
    {
        internal static readonly byte[] Signature = Encoding.ASCII.GetBytes("A2GSPE04");
        internal const int FlagOffset = 0x08;
        internal const int CheckingOffset = 0x09;
        internal const int VerdictOffset = 0x0A;
        internal const int FunctionOffset = 0x0C;
        internal const int CheckFunctionOffset = 0x10;
        internal const int ReleaseFunctionOffset = 0x14;
        internal const int SavedStackOffset = 0x18;
        internal const int ClubOffset = 0x1C;
        internal const int NumberOffset = 0x20;
        internal const int SavedSpeechOffset = 0x24;
        internal const int SavedUsesOffset = 0x26;
        internal const int SkillOffset = 0x28;
        internal const int ReleaseCodeOffset = 0x40;
        internal const int CodeOffset = 0x90;
        internal const int BufferOffset = 0x280;
        // The list keeps 0xC0 bytes of a text, the terminating NUL included.
        internal const int MaxTextLength = 0xBF;
        internal const int TablesOffset = BufferOffset + MaxTextLength + 1;
        internal const int Tables = 4;
        internal const int EntrySize = 0x40;
        internal const int TableSize = 4 + PreMatchSpeeches.Count * EntrySize;
        internal const int StubSize = TablesOffset + Tables * TableSize;
        internal const uint FreeKey = 0xFFFFFFFF;
        private const int TickMilliseconds = 250;

        /// <summary>Colour of a speech the game would accept (the list's colours are 0xBBGGRR).</summary>
        internal const uint FitsColour = 0x60E060;
        /// <summary>Colour of a speech that doesn't fit the situation.</summary>
        internal const uint DoesNotFitColour = 0x6060FF;
        /// <summary>Colour of a speech without uses left this season.</summary>
        internal const uint UsedUpColour = 0xA0A0A0;

        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        private readonly ProcessMemory memory;
        private readonly Func<IEnumerable<KeyValuePair<PlayerEnums.Country, ushort>>> clubs;
        private readonly object sync = new object();
        private Timer timer;
        // What each table holds, so unchanged tables aren't written again.
        private readonly byte[][] written = new byte[Tables][];

        /// <summary>
        /// Creates the display for the attached game; <paramref name="clubs"/> gives the human managers'
        /// clubs (country, ID). It does nothing until <see cref="Enabled"/>.
        /// </summary>
        internal SpeechEffectsDisplay(ProcessMemory memory, Func<IEnumerable<KeyValuePair<PlayerEnums.Country, ushort>>> clubs)
        {
            this.memory = memory;
            this.clubs = clubs;
        }

        private FixedAddresses Addresses => FixedAddresses.For(this.memory);

        /// <summary>Whether the display is available for the attached build.</summary>
        internal bool IsSupported => this.Addresses?.SpeechList != null;

        /// <summary>Whether the dialog shows the verdicts and notes.</summary>
        internal bool Enabled => this.timer != null;

        /// <summary>Switches the display on (installing the stub if needed) or off. Returns whether that succeeded.</summary>
        internal bool SetEnabled(bool enabled)
        {
            lock (this.sync)
            {
                uint? stub = enabled ? this.EnsureInstalled() : this.FindInstalledStub();
                if (enabled && !stub.HasValue)
                    return false;
                if (stub.HasValue)
                    this.memory.WriteBytesAtAddress(stub.Value + FlagOffset, new[] { (byte)(enabled ? 1 : 0) });
                if (enabled && this.timer == null)
                    this.timer = new Timer(_ => this.TimerTick(), null, 0, TickMilliseconds);
                else if (!enabled && this.timer != null)
                {
                    this.timer.Dispose();
                    this.timer = null;
                }
                Array.Clear(this.written, 0, Tables);
                return true;
            }
        }

        /// <inheritdoc />
        public void Dispose() => this.SetEnabled(false);

        private uint CallSite => this.memory.ModuleBase + this.Addresses.SpeechList.CallSite;
        private uint Function => this.memory.ModuleBase + this.Addresses.SpeechList.Function;
        private uint CheckFunction => this.memory.ModuleBase + this.Addresses.SpeechList.CheckFunction;
        private uint ReleaseSite => this.memory.ModuleBase + this.Addresses.SpeechList.ReleaseSite;
        private uint ReleaseFunction => this.memory.ModuleBase + this.Addresses.SpeechList.ReleaseFunction;

        /// <summary>
        /// The list function an installed stub at <paramref name="target"/> - <see cref="CodeOffset"/>
        /// goes on to, or null if <paramref name="target"/> isn't one of our stubs.
        /// </summary>
        internal static uint? ListFunctionOf(ProcessMemory memory, uint target) => StubField(memory, unchecked(target - CodeOffset), FunctionOffset);

        /// <summary>
        /// The release function an installed stub at <paramref name="target"/> - <see cref="ReleaseCodeOffset"/>
        /// goes on to, or null if <paramref name="target"/> isn't one of our stubs.
        /// </summary>
        internal static uint? ReleaseFunctionOf(ProcessMemory memory, uint target) => StubField(memory, unchecked(target - ReleaseCodeOffset), ReleaseFunctionOffset);

        // The dword at offset in the stub at stub, or null if there is no stub of ours.
        private static uint? StubField(ProcessMemory memory, uint stub, int offset)
        {
            byte[] head = memory.ReadBytesAtAddress(stub, offset + 4);
            if (head == null || !head.Take(Signature.Length).SequenceEqual(Signature))
                return null;
            return BitConverter.ToUInt32(head, offset);
        }

        /// <summary>Returns the installed stub, installing it first if both calls still go to the game's functions; null if that isn't possible.</summary>
        internal uint? EnsureInstalled()
        {
            if (!this.IsSupported)
                return null;
            uint? existing = this.FindInstalledStub();
            if (existing.HasValue)
                return existing;
            if (this.ReadCallTarget(this.CallSite) != this.Function || this.ReadCallTarget(this.ReleaseSite) != this.ReleaseFunction)
                return null;

            uint? stub = this.memory.AllocateExecutable(StubSize);
            if (!stub.HasValue)
                return null;
            this.memory.WriteBytesAtAddress(stub.Value, BuildStub(stub.Value, this.Function, this.CheckFunction, this.ReleaseFunction));
            // The release call first: until the list call goes to the stub, no check runs from it.
            if (!Redirect(this.memory, this.ReleaseSite, stub.Value + ReleaseCodeOffset)
                || !Redirect(this.memory, this.CallSite, stub.Value + CodeOffset))
                return null;
            Logger.Debug($"SpeechEffectsDisplay: redirected 0x{this.CallSite:X} and 0x{this.ReleaseSite:X} to a stub at 0x{stub.Value:X}.");
            return stub;
        }

        private uint? FindInstalledStub()
        {
            if (!this.IsSupported)
                return null;
            uint? target = this.ReadCallTarget(this.CallSite);
            return target.HasValue && ListFunctionOf(this.memory, target.Value).HasValue ? target.Value - CodeOffset : (uint?)null;
        }

        private uint? ReadCallTarget(uint site)
        {
            byte[] call = this.memory.ReadBytesAtAddress(site, 5);
            if (call == null || call[0] != 0xE8)
                return null;
            return unchecked((uint)(site + 5 + BitConverter.ToInt32(call, 1)));
        }

        private static bool Redirect(ProcessMemory memory, uint site, uint target) =>
            memory.WriteCode(site + 1, BitConverter.GetBytes(unchecked((int)(target - (site + 5)))));

        // A tick already queued when the display was switched off must not write again.
        private void TimerTick()
        {
            lock (this.sync)
            {
                if (this.timer != null)
                    this.Tick();
            }
        }

        /// <summary>One tick: fills a table for every human club the game keeps data for, frees the others.</summary>
        internal void Tick()
        {
            lock (this.sync)
            {
                try
                {
                    uint? stub = this.FindInstalledStub();
                    FixedAddresses addresses = this.Addresses;
                    byte[] table = addresses != null ? this.memory.ReadBytesAtAddress(addresses.Static(this.memory, Settings.SpeechTableOffset), PreMatchSpeeches.Count * PreMatchSpeeches.Stride) : null;
                    if (!stub.HasValue || table == null)
                        return;
                    List<PreMatchSpeeches.Speech> speeches = PreMatchSpeeches.Parse(table);
                    List<KeyValuePair<PlayerEnums.Country, ushort>> humans = (this.clubs?.Invoke() ?? Enumerable.Empty<KeyValuePair<PlayerEnums.Country, ushort>>())
                        .Distinct().Take(Tables).ToList();
                    for (int i = 0; i < Tables; i++)
                    {
                        byte[] content = i < humans.Count ? this.TableFor(addresses, speeches, humans[i].Key, humans[i].Value) : null;
                        content = content ?? BitConverter.GetBytes(FreeKey);
                        if (this.written[i] != null && this.written[i].SequenceEqual(content))
                            continue;
                        this.memory.WriteBytesAtAddress(stub.Value + TablesOffset + (uint)(i * TableSize), content);
                        this.written[i] = content;
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn("SpeechEffectsDisplay", ex);
                }
            }
        }

        // The club's table, or null while the game keeps no data for it.
        private byte[] TableFor(FixedAddresses addresses, List<PreMatchSpeeches.Speech> speeches, PlayerEnums.Country country, ushort clubId)
        {
            uint? uses = ClubRecords.DataField(this.memory, addresses, country, clubId, Settings.ClubSpeechUsesOffset);
            byte[] usesBytes = uses.HasValue ? this.memory.ReadBytesAtAddress(uses.Value, PreMatchSpeeches.Count * 2) : null;
            if (usesBytes == null)
                return null;
            short[] usesLeft = Enumerable.Range(0, PreMatchSpeeches.Count).Select(n => BitConverter.ToInt16(usesBytes, n * 2)).ToArray();
            return BuildTable(country, clubId, speeches, usesLeft);
        }

        /// <summary>A club's table as the stub reads it (see the class remarks); <paramref name="usesLeft"/> per speech number.</summary>
        internal static byte[] BuildTable(PlayerEnums.Country country, ushort clubId, IEnumerable<PreMatchSpeeches.Speech> speeches, short[] usesLeft)
        {
            byte[] table = new byte[TableSize];
            BitConverter.GetBytes(Key(country, clubId)).CopyTo(table, 0);
            foreach (PreMatchSpeeches.Speech speech in speeches)
            {
                if (speech.Number <= 0 || speech.Number >= PreMatchSpeeches.Count)
                    continue;
                short left = speech.Number < usesLeft.Length ? usesLeft[speech.Number] : (short)0;
                byte[] note = Latin1.GetBytes(PreMatchSpeeches.Prefix(speech, left));
                Array.Copy(note, 0, table, 4 + speech.Number * EntrySize, Math.Min(note.Length, EntrySize - 1));
            }
            return table;
        }

        /// <summary>The key the stub compares with the dialog's club (club +0x103 country, +0x102 ID).</summary>
        internal static uint Key(PlayerEnums.Country country, ushort clubId) => (uint)(byte)country << 8 | (byte)clubId;

        /// <summary>Builds the complete stub (data + code) for the given absolute addresses - see the class remarks.</summary>
        internal static byte[] BuildStub(uint stubBase, uint listFunction, uint checkFunction, uint releaseFunction)
        {
            byte[] stub = new byte[StubSize];
            Array.Copy(Signature, stub, Signature.Length);
            BitConverter.GetBytes(listFunction).CopyTo(stub, FunctionOffset);
            BitConverter.GetBytes(checkFunction).CopyTo(stub, CheckFunctionOffset);
            BitConverter.GetBytes(releaseFunction).CopyTo(stub, ReleaseFunctionOffset);
            for (int i = 0; i < Tables; i++)
                BitConverter.GetBytes(FreeKey).CopyTo(stub, TablesOffset + i * TableSize);

            uint Data(int offset) => stubBase + (uint)offset;
            X86Assembler a = new X86Assembler();

            // Release call (cdecl: the manager). The check's verdict lies at [esp+0x18] here (its
            // [esp+0x10] before "push esi / call").
            a.Emit(0x80, 0x3D); a.Dword(Data(CheckingOffset)); a.Emit(0x00);      // cmp byte [checking], 0
            a.Jump(0x75, "checked");                                               // jne checked
            a.Emit(0xFF, 0x25); a.Dword(Data(ReleaseFunctionOffset));              // jmp [release]
            a.Mark("checked");
            a.Emit(0x8B, 0x4C, 0x24, 0x04);                                        // mov ecx, [esp+4]  (manager)
            a.Emit(0x0F, 0xB6, 0x51, (byte)Settings.ManagerMotivationLevelOffset); // movzx edx, byte [ecx+0x22]  (level)
            a.Emit(0x6B, 0xD2, (byte)Settings.ManagerMotivationStride);            // imul edx, edx, 0xC
            a.Emit(0x0F, 0xBF, 0x84, 0x11); a.Dword(Settings.ManagerMotivationOffset); // movsx eax, word [ecx+edx+0x189A]
            a.Emit(0xA3); a.Dword(Data(SkillOffset));                              // mov [skill], eax
            a.Emit(0x8B, 0x44, 0x24, 0x18);                                        // mov eax, [esp+0x18]
            a.Emit(0xA2); a.Dword(Data(VerdictOffset));                            // mov [verdict], al
            a.Emit(0xFF, 0x74, 0x24, 0x04);                                        // push dword [esp+4]
            a.Emit(0xFF, 0x15); a.Dword(Data(ReleaseFunctionOffset));              // call [release]
            a.Emit(0x8B, 0x25); a.Dword(Data(SavedStackOffset));                   // mov esp, [saved esp]  (leave the check)
            a.JumpNear("resume", 0xE9);                                            // jmp resume
            a.PadTo(CodeOffset - ReleaseCodeOffset);

            // List call (thiscall: [esp+4] text, [esp+8] colour, [esp+0xC] number; esi = the dialog).
            // After pushad the arguments sit 0x20 further.
            a.Emit(0x80, 0x3D); a.Dword(Data(FlagOffset)); a.Emit(0x00);          // cmp byte [flag], 0
            a.JumpNear("original", 0x0F, 0x84);                                    // je original
            a.Emit(0x60);                                                          // pushad
            a.Emit(0x31, 0xED);                                                    // xor ebp, ebp  (no chance to show)
            a.Emit(0x8B, 0x9E); a.Dword(Settings.SpeechDialogClubOffset);          // mov ebx, [esi+0x7FE0]  (club)
            a.Emit(0x85, 0xDB);                                                    // test ebx, ebx
            a.JumpNear("restore", 0x0F, 0x84);                                     // je restore
            a.Emit(0x8B, 0x4C, 0x24, 0x2C);                                        // mov ecx, [esp+0x2C]  (number)
            a.Emit(0x83, 0xF9, PreMatchSpeeches.Count - 1);                        // cmp ecx, 43
            a.JumpNear("restore", 0x0F, 0x87);                                     // ja restore
            a.Emit(0x85, 0xC9);                                                    // test ecx, ecx
            a.JumpNear("restore", 0x0F, 0x84);                                     // je restore
            a.Emit(0x0F, 0xBF, 0x94, 0x4B); a.Dword(Settings.ClubSpeechUsesOffset); // movsx edx, word [ebx+ecx*2+0x1448]
            a.Emit(0x85, 0xD2);                                                    // test edx, edx
            a.Jump(0x7F, "check");                                                 // jg check
            a.Emit(0xC7, 0x44, 0x24, 0x28); a.Dword(UsedUpColour);                 // mov [esp+0x28], used up
            a.JumpNear("note", 0xE9);                                              // jmp note

            // Run the game's check with this speech as the chosen one; the release call comes back to resume.
            a.Mark("check");
            a.Emit(0x89, 0x1D); a.Dword(Data(ClubOffset));                         // mov [club], ebx
            a.Emit(0x89, 0x0D); a.Dword(Data(NumberOffset));                       // mov [number], ecx
            a.Emit(0x66, 0x8B, 0x83); a.Dword(Settings.ClubSpeechOffset);          // mov ax, [ebx+0x1440]
            a.Emit(0x66, 0xA3); a.Dword(Data(SavedSpeechOffset));                  // mov [saved speech], ax
            a.Emit(0x66, 0x89, 0x15); a.Dword(Data(SavedUsesOffset));              // mov [saved uses], dx
            a.Emit(0x66, 0x89, 0x8B); a.Dword(Settings.ClubSpeechOffset);          // mov [ebx+0x1440], cx
            a.Emit(0xC6, 0x05); a.Dword(Data(VerdictOffset)); a.Emit(0xFF);        // mov byte [verdict], 0xFF
            a.Emit(0xC6, 0x05); a.Dword(Data(CheckingOffset)); a.Emit(0x01);       // mov byte [checking], 1
            a.Emit(0x89, 0x25); a.Dword(Data(SavedStackOffset));                   // mov [saved esp], esp
            foreach (uint field in DialogArguments)
            {
                a.Emit(0xFF, 0xB6); a.Dword(field);                                // push dword [esi+field]
            }
            a.Emit(0xFF, 0x15); a.Dword(Data(CheckFunctionOffset));                // call [check]  (returns only if it stops early)
            a.Mark("resume");
            a.Emit(0x8B, 0x25); a.Dword(Data(SavedStackOffset));                   // mov esp, [saved esp]
            a.Emit(0x31, 0xED);                                                    // xor ebp, ebp  (the check left its own)
            a.Emit(0xC6, 0x05); a.Dword(Data(CheckingOffset)); a.Emit(0x00);       // mov byte [checking], 0
            a.Emit(0x8B, 0x1D); a.Dword(Data(ClubOffset));                         // mov ebx, [club]
            a.Emit(0x8B, 0x0D); a.Dword(Data(NumberOffset));                       // mov ecx, [number]
            a.Emit(0x66, 0xA1); a.Dword(Data(SavedSpeechOffset));                  // mov ax, [saved speech]
            a.Emit(0x66, 0x89, 0x83); a.Dword(Settings.ClubSpeechOffset);          // mov [ebx+0x1440], ax
            a.Emit(0x66, 0xA1); a.Dword(Data(SavedUsesOffset));                    // mov ax, [saved uses]
            a.Emit(0x66, 0x89, 0x84, 0x4B); a.Dword(Settings.ClubSpeechUsesOffset); // mov [ebx+ecx*2+0x1448], ax
            a.Emit(0xA0); a.Dword(Data(VerdictOffset));                            // mov al, [verdict]
            a.Emit(0x3C, 0x01);                                                    // cmp al, 1
            a.Jump(0x74, "fits");                                                  // je fits
            a.Emit(0x84, 0xC0);                                                    // test al, al
            a.Jump(0x75, "note");                                                  // jne note  (no verdict)
            a.Emit(0xC7, 0x44, 0x24, 0x28); a.Dword(DoesNotFitColour);             // mov [esp+0x28], doesn't fit
            a.Jump(0xEB, "note");                                                  // jmp note
            a.Mark("fits");
            a.Emit(0xC7, 0x44, 0x24, 0x28); a.Dword(FitsColour);                   // mov [esp+0x28], fits
            a.Emit(0x83, 0xF9, PreMatchSpeeches.FirstMotivationSpeech);            // cmp ecx, 28
            a.Jump(0x72, "note");                                                  // jb note
            a.Emit(0x6B, 0x05); a.Dword(Data(SkillOffset)); a.Emit(0x06);          // imul eax, [skill], 6
            a.Emit(0x83, 0xC0, 0x0B);                                              // add eax, 11
            a.Emit(0x83, 0xF8, 0x64);                                              // cmp eax, 100
            a.Jump(0x7E, "atMost100");                                             // jle atMost100
            a.Emit(0xB8); a.Dword(100);                                            // mov eax, 100
            a.Mark("atMost100");
            a.Emit(0x85, 0xC0);                                                    // test eax, eax
            a.Jump(0x7D, "atLeast0");                                              // jge atLeast0
            a.Emit(0x31, 0xC0);                                                    // xor eax, eax
            a.Mark("atLeast0");
            a.Emit(0x8D, 0xA8); a.Dword(0x100);                                    // lea ebp, [eax+0x100]  (chance, marked)

            // The club's note: key country * 256 + ID (club +0x103, +0x102).
            a.Mark("note");
            a.Emit(0x0F, 0xB6, 0x93); a.Dword(Settings.ClubCountryOffset);         // movzx edx, byte [ebx+0x103]
            a.Emit(0xC1, 0xE2, 0x08);                                              // shl edx, 8
            a.Emit(0x8A, 0x93); a.Dword(Settings.ClubTeamIdOffset);                // mov dl, [ebx+0x102]
            a.Emit(0xB8); a.Dword(Data(TablesOffset));                             // mov eax, tables
            a.Emit(0xBF); a.Dword(Tables);                                         // mov edi, 4
            a.Mark("find");
            a.Emit(0x39, 0x10);                                                    // cmp [eax], edx
            a.Jump(0x74, "found");                                                 // je found
            a.Emit(0x05); a.Dword(TableSize);                                      // add eax, tableSize
            a.Emit(0x4F);                                                          // dec edi
            a.Jump(0x75, "find");                                                  // jnz find
            a.JumpNear("restore", 0xE9);                                           // jmp restore
            a.Mark("found");
            a.Emit(0xC1, 0xE1, 0x06);                                              // shl ecx, 6  (* EntrySize)
            a.Emit(0x8D, 0x44, 0x08, 0x04);                                        // lea eax, [eax+ecx+4]
            a.Emit(0x80, 0x38, 0x00);                                              // cmp byte [eax], 0
            a.JumpNear("restore", 0x0F, 0x84);                                     // je restore  (no note)
            a.Emit(0xBF); a.Dword(Data(BufferOffset));                             // mov edi, buffer
            a.Mark("prefix");                                                      // the note is shorter than the buffer
            a.Emit(0x8A, 0x08);                                                    // mov cl, [eax]
            a.Emit(0x84, 0xC9);                                                    // test cl, cl
            a.Jump(0x74, "body");                                                  // je body
            a.Emit(0x88, 0x0F);                                                    // mov [edi], cl
            a.Emit(0x40, 0x47);                                                    // inc eax / inc edi
            a.Jump(0xEB, "prefix");                                                // jmp prefix
            a.Mark("body");
            // The chance of a fitting motivation speech, "(71%) ", after the note.
            a.Emit(0x85, 0xED);                                                    // test ebp, ebp
            a.Jump(0x74, "text");                                                  // jz text
            a.Emit(0xC6, 0x07, (byte)'(');                                         // mov byte [edi], '('
            a.Emit(0x47);                                                          // inc edi
            a.Emit(0x89, 0xE8);                                                    // mov eax, ebp
            a.Emit(0x0F, 0xB6, 0xC0);                                              // movzx eax, al  (0..100)
            a.Emit(0x83, 0xF8, 0x64);                                              // cmp eax, 100
            a.Jump(0x72, "below100");                                              // jb below100
            a.Emit(0xC6, 0x07, (byte)'1');                                         // mov byte [edi], '1'
            a.Emit(0x47);                                                          // inc edi
            a.Emit(0x83, 0xE8, 0x64);                                              // sub eax, 100
            a.Jump(0xEB, "twoDigits");                                             // jmp twoDigits
            a.Mark("below100");
            a.Emit(0x83, 0xF8, 0x0A);                                              // cmp eax, 10
            a.Jump(0x72, "oneDigit");                                              // jb oneDigit
            a.Mark("twoDigits");
            a.Emit(0xB1, 0x0A);                                                    // mov cl, 10
            a.Emit(0xF6, 0xF1);                                                    // div cl  (al tens, ah units)
            a.Emit(0x66, 0x05, 0x30, 0x30);                                        // add ax, '00'
            a.Emit(0x66, 0x89, 0x07);                                              // mov [edi], ax
            a.Emit(0x83, 0xC7, 0x02);                                              // add edi, 2
            a.Jump(0xEB, "percent");                                               // jmp percent
            a.Mark("oneDigit");
            a.Emit(0x04, 0x30);                                                    // add al, '0'
            a.Emit(0x88, 0x07);                                                    // mov [edi], al
            a.Emit(0x47);                                                          // inc edi
            a.Mark("percent");
            a.Emit(0xC6, 0x07, (byte)'%');                                         // mov byte [edi], '%'
            a.Emit(0xC6, 0x47, 0x01, (byte)')');                                   // mov byte [edi+1], ')'
            a.Emit(0xC6, 0x47, 0x02, (byte)' ');                                   // mov byte [edi+2], ' '
            a.Emit(0x83, 0xC7, 0x03);                                              // add edi, 3
            a.Mark("text");
            a.Emit(0x8B, 0x44, 0x24, 0x24);                                        // mov eax, [esp+0x24]  (text)
            a.Emit(0xBB); a.Dword(Data(BufferOffset + MaxTextLength));             // mov ebx, buffer + 0xBF
            a.Mark("next");
            a.Emit(0x39, 0xDF);                                                    // cmp edi, ebx
            a.Jump(0x73, "terminate");                                             // jae terminate
            a.Emit(0x8A, 0x08);                                                    // mov cl, [eax]
            a.Emit(0x88, 0x0F);                                                    // mov [edi], cl
            a.Emit(0x84, 0xC9);                                                    // test cl, cl
            a.Jump(0x74, "pass");                                                  // je pass
            a.Emit(0x40, 0x47);                                                    // inc eax / inc edi
            a.Jump(0xEB, "next");                                                  // jmp next
            a.Mark("terminate");
            a.Emit(0xC6, 0x07, 0x00);                                              // mov byte [edi], 0
            a.Mark("pass");
            a.Emit(0xC7, 0x44, 0x24, 0x24); a.Dword(Data(BufferOffset));           // mov [esp+0x24], buffer
            a.Mark("restore");
            a.Emit(0x61);                                                          // popad
            a.Mark("original");
            a.Emit(0xFF, 0x25); a.Dword(Data(FunctionOffset));                     // jmp [list function]
            byte[] code = a.ToArray();
            if (code.Length > BufferOffset - ReleaseCodeOffset)
                throw new InvalidOperationException("SpeechEffectsDisplay: stub code too long.");
            code.CopyTo(stub, ReleaseCodeOffset);
            return stub;
        }

        /// <summary>
        /// The dialog fields the check takes, pushed last argument first: home (+0x7FF4), match, opposing
        /// and own team block, opponent, club (+0x7FE0) - as the dialog's OK handler passes them.
        /// </summary>
        internal static readonly uint[] DialogArguments = { 0x7FF4, 0x7FF0, 0x7FEC, 0x7FE8, 0x7FE4, 0x7FE0 };
    }
}
