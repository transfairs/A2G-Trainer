using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Tests for SpeechEffectsDisplay: redirecting the list and release calls to the stub, switching it
    /// on and off, refusing foreign patches, the tables the trainer fills - and running the generated x86
    /// stub against stand-ins for the game's list, speech check and release functions.
    /// </summary>
    public class SpeechEffectsDisplayTests
    {
        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");

        private static uint Absolute(FakeModule fake, uint offset) => fake.ModuleBase + offset;

        private static void WriteCall(FakeModule fake, uint siteOffset, uint target)
        {
            int rel = unchecked((int)(target - (Absolute(fake, siteOffset) + 5)));
            fake.WriteModuleBytes(siteOffset, new byte[] { 0xE8 }.Concat(BitConverter.GetBytes(rel)).ToArray());
        }

        private static uint CallTarget(FakeModule fake, uint siteOffset)
        {
            byte[] call = fake.ReadModuleBytes(siteOffset, 5);
            return unchecked((uint)(Absolute(fake, siteOffset) + 5 + BitConverter.ToInt32(call, 1)));
        }

        // Both calls as in the unpatched game.
        internal static void WriteOriginalCalls(FakeModule fake)
        {
            WriteCall(fake, Settings.SpeechListCallSiteOffset, Absolute(fake, Settings.SpeechListFunctionOffset));
            WriteCall(fake, Settings.SpeechCheckReleaseSiteOffset, Absolute(fake, Settings.SpeechCheckReleaseFunctionOffset));
        }

        private static uint Stub(FakeModule fake) => CallTarget(fake, Settings.SpeechListCallSiteOffset) - SpeechEffectsDisplay.CodeOffset;

        private static byte Flag(FakeModule fake) => fake.Memory.ReadBytesAtAddress(Stub(fake) + SpeechEffectsDisplay.FlagOffset, 1)[0];

        [Fact]
        public void SetEnabled_RedirectsBothCallsOnce_AndSwitchesTheFlag()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                using (SpeechEffectsDisplay display = new SpeechEffectsDisplay(fake.Memory, null))
                {
                    Assert.True(display.IsSupported);
                    Assert.False(display.Enabled);

                    Assert.True(display.SetEnabled(true));
                    Assert.True(display.Enabled);
                    uint stub = Stub(fake);
                    Assert.Equal(SpeechEffectsDisplay.Signature, fake.Memory.ReadBytesAtAddress(stub, SpeechEffectsDisplay.Signature.Length));
                    Assert.Equal(stub + SpeechEffectsDisplay.ReleaseCodeOffset, CallTarget(fake, Settings.SpeechCheckReleaseSiteOffset));
                    Assert.Equal(Absolute(fake, Settings.SpeechListFunctionOffset), SpeechEffectsDisplay.ListFunctionOf(fake.Memory, stub + SpeechEffectsDisplay.CodeOffset));
                    Assert.Equal(Absolute(fake, Settings.SpeechCheckReleaseFunctionOffset), SpeechEffectsDisplay.ReleaseFunctionOf(fake.Memory, stub + SpeechEffectsDisplay.ReleaseCodeOffset));
                    Assert.Equal(Absolute(fake, Settings.SpeechCheckFunctionOffset),
                        BitConverter.ToUInt32(fake.Memory.ReadBytesAtAddress(stub + SpeechEffectsDisplay.CheckFunctionOffset, 4), 0));
                    Assert.Equal(1, Flag(fake));

                    Assert.True(display.SetEnabled(false));
                    Assert.False(display.Enabled);
                    Assert.Equal(0, Flag(fake));
                    Assert.Equal(stub, Stub(fake));

                    // Again, also from a new instance: the same stub, no second redirect.
                    using (SpeechEffectsDisplay again = new SpeechEffectsDisplay(fake.Memory, null))
                    {
                        Assert.True(again.SetEnabled(true));
                        Assert.Equal(stub, Stub(fake));
                        Assert.Equal(1, Flag(fake));
                    }
                    Assert.True(display.SetEnabled(true));
                    Assert.Equal(1, Flag(fake));
                }
                // Disposing switches it off.
                Assert.Equal(0, Flag(fake));
            }
        }

        [Fact]
        public void SetEnabled_OffWithoutAStub_ChangesNothing()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);

                Assert.True(new SpeechEffectsDisplay(fake.Memory, null).SetEnabled(false));
                Assert.Equal(Absolute(fake, Settings.SpeechListFunctionOffset), CallTarget(fake, Settings.SpeechListCallSiteOffset));
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SetEnabled_WithACallPatchedElsewhere_Fails(bool listCall)
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                WriteCall(fake, listCall ? Settings.SpeechListCallSiteOffset : Settings.SpeechCheckReleaseSiteOffset, Absolute(fake, 0x10000));
                SpeechEffectsDisplay display = new SpeechEffectsDisplay(fake.Memory, null);

                Assert.False(display.SetEnabled(true));
                Assert.False(display.Enabled);
                Assert.Equal(listCall ? Absolute(fake, 0x10000) : Absolute(fake, Settings.SpeechListFunctionOffset), CallTarget(fake, Settings.SpeechListCallSiteOffset));
            }
        }

        [Fact]
        public void SetEnabled_OnABuildWithoutConfirmedAddresses_Fails()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.Memory.Layout = new PersistentLayout(null, null, null, null, isVerified: true);
                SpeechEffectsDisplay display = new SpeechEffectsDisplay(fake.Memory, null);

                Assert.False(display.IsSupported);
                Assert.False(display.SetEnabled(true));
                Assert.True(display.SetEnabled(false));
                Assert.Null(display.EnsureInstalled());
            }
        }

        [Fact]
        public void StubFunctions_OfSomethingElse_AreNull()
        {
            using (FakeModule fake = new FakeModule())
            {
                Assert.Null(SpeechEffectsDisplay.ListFunctionOf(fake.Memory, Absolute(fake, 0x1000)));
                Assert.Null(SpeechEffectsDisplay.ReleaseFunctionOf(fake.Memory, Absolute(fake, 0x1000)));
            }
        }

        // A speech table: speech n raises defence and attack by n %, speeches 1..3 twice a season.
        private static byte[] SpeechTable()
        {
            byte[] table = new byte[PreMatchSpeeches.Count * PreMatchSpeeches.Stride];
            for (int n = 1; n < PreMatchSpeeches.Count; n++)
            {
                int at = n * PreMatchSpeeches.Stride;
                table[at + 0xC] = (byte)n;
                table[at + 0xD] = (byte)n;
                table[at + 0xE] = PreMatchSpeeches.NoHardness;
                table[at + 0xF] = n <= 3 ? (byte)2 : PreMatchSpeeches.Unlimited;
            }
            return table;
        }

        [Fact]
        public void BuildTable_HoldsTheKeyAndEachSpeechsPrefix()
        {
            short[] uses = new short[PreMatchSpeeches.Count];
            uses[2] = 1;
            byte[] table = SpeechEffectsDisplay.BuildTable(PlayerEnums.Country.Deutschland, 0x12, PreMatchSpeeches.Parse(SpeechTable()), uses);

            Assert.Equal(SpeechEffectsDisplay.TableSize, table.Length);
            Assert.Equal(SpeechEffectsDisplay.Key(PlayerEnums.Country.Deutschland, 0x12), BitConverter.ToUInt32(table, 0));
            string Note(int n) => Latin1.GetString(table, 4 + n * SpeechEffectsDisplay.EntrySize, SpeechEffectsDisplay.EntrySize).TrimEnd('\0');
            Assert.Equal("", Note(0));
            Assert.Equal("+1% 0x ", Note(1));
            Assert.Equal("+2% 1x ", Note(2));
            Assert.Equal("+4% ", Note(4));
        }

        [Fact]
        public void BuildTable_WithTooFewUses_CountsTheRestAsUsedUp()
        {
            byte[] table = SpeechEffectsDisplay.BuildTable(PlayerEnums.Country.Deutschland, 1, PreMatchSpeeches.Parse(SpeechTable()), new short[1]);

            Assert.Equal("+3% 0x ", Latin1.GetString(table, 4 + 3 * SpeechEffectsDisplay.EntrySize, 7));
        }

        // One table per human club the game keeps full data for (duplicates once), the others free;
        // a club that is no longer listed gets its table freed.
        [Fact]
        public void Tick_FillsATableForEveryHumanClubWithData_AndFreesTheRest()
        {
            using (FakeModule fake = new FakeModule())
            {
                WriteOriginalCalls(fake);
                fake.WriteModuleBytes(Settings.SpeechTableOffset, SpeechTable());
                ClubEditorSettingsTests.SeedOwnClub(fake);
                fake.WriteModuleBytes(ClubEditorSettingsTests.Record(ClubEditorSettingsTests.OwnId) + Settings.ClubRecordDataSlotOffset, new byte[] { 2 });
                uint uses = ClubRecords.DataField(fake.Memory, FixedAddresses.For(fake.Memory), ClubEditorSettingsTests.Country, ClubEditorSettingsTests.OwnId, Settings.ClubSpeechUsesOffset).Value;
                fake.Memory.WriteBytesAtAddress(uses + 2 * 2, BitConverter.GetBytes((short)1));
                KeyValuePair<PlayerEnums.Country, ushort> own = new KeyValuePair<PlayerEnums.Country, ushort>(ClubEditorSettingsTests.Country, ClubEditorSettingsTests.OwnId);
                KeyValuePair<PlayerEnums.Country, ushort>[] clubs = { own, own, new KeyValuePair<PlayerEnums.Country, ushort>(ClubEditorSettingsTests.Country, 2) };

                using (SpeechEffectsDisplay display = new SpeechEffectsDisplay(fake.Memory, () => clubs))
                {
                    display.Tick();    // nothing installed yet
                    Assert.True(display.SetEnabled(true));
                    display.Tick();

                    uint tables = Stub(fake) + SpeechEffectsDisplay.TablesOffset;
                    uint Key(int table) => Dword(fake, tables + (uint)(table * SpeechEffectsDisplay.TableSize));
                    Assert.Equal(SpeechEffectsDisplay.Key(own.Key, own.Value), Key(0));
                    Assert.Equal("+2% 1x ", Latin1.GetString(fake.Memory.ReadBytesAtAddress(tables + 4 + 2 * SpeechEffectsDisplay.EntrySize, 7)));
                    for (int i = 1; i < SpeechEffectsDisplay.Tables; i++)
                        Assert.Equal(SpeechEffectsDisplay.FreeKey, Key(i));

                    clubs = new KeyValuePair<PlayerEnums.Country, ushort>[0];
                    display.Tick();
                    Assert.Equal(SpeechEffectsDisplay.FreeKey, Key(0));
                }
            }
        }

        [Fact]
        public void Key_IsCountryTimes256PlusId()
        {
            Assert.Equal(((uint)(byte)PlayerEnums.Country.England << 8) | 7u, SpeechEffectsDisplay.Key(PlayerEnums.Country.England, 7));
        }

        // ---- The stub, run natively ------------------------------------------------------------

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void AddEntry(uint dialog, uint text, uint colour, uint number);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ReleaseManager(uint manager);

        private const uint White = 0xFFFFFF;
        // The manager's level (byte +0x22) and his motivation skill at it.
        private const byte ManagerLevel = 1;
        private const short Skill = 10;

        // The pieces of one run, all in one executable block.
        private sealed class Rig
        {
            public uint Stub, Record, Dialog, Club, Opponent, Manager, Text, Call, Check, Release, List;
        }

        private static uint Dword(FakeModule fake, uint address) => BitConverter.ToUInt32(fake.Memory.ReadBytesAtAddress(address, 4), 0);
        private static short Word(FakeModule fake, uint address) => BitConverter.ToInt16(fake.Memory.ReadBytesAtAddress(address, 2), 0);
        private static void SetWord(FakeModule fake, uint address, short value) => fake.Memory.WriteBytesAtAddress(address, BitConverter.GetBytes(value));

        // Record: +0 list calls, +4 colour, +8 number, +0xC ecx, +0x10 release calls and +0x14 their
        // argument, +0x18 match, +0x1C home, +0x20 opponent, +0x24 checks that returned, +0x28 checks,
        // +0x40 the text the list got.
        private static Rig Build(FakeModule fake)
        {
            uint block = fake.Memory.AllocateExecutable(0x20000).Value;
            Rig r = new Rig
            {
                Stub = block,
                Record = block + 0x4000,
                Call = block + 0x4200,
                List = block + 0x4300,
                Release = block + 0x4400,
                Check = block + 0x4500,
                Text = block + 0x4800,
                Club = block + 0x5000,
                Opponent = block + 0x7000,
                Dialog = block + 0x9000,
                Manager = block + 0x12000,
            };
            uint rec = r.Record;

            // cdecl (dialog, text, colour, number): calls the list stub as the game does (esi = dialog).
            fake.Memory.WriteBytesAtAddress(r.Call, Bytes(
                new byte[] { 0x56 },                                               // push esi
                new byte[] { 0x8B, 0x74, 0x24, 0x08 },                             // mov esi, [esp+8]
                new byte[] { 0xFF, 0x74, 0x24, 0x14 },                             // push dword [esp+0x14]  number
                new byte[] { 0xFF, 0x74, 0x24, 0x14 },                             // push dword [esp+0x14]  colour
                new byte[] { 0xFF, 0x74, 0x24, 0x14 },                             // push dword [esp+0x14]  text
                new byte[] { 0xB9 }, BitConverter.GetBytes(0x1157u),               // mov ecx, list object
                new byte[] { 0xB8 }, BitConverter.GetBytes(r.Stub + SpeechEffectsDisplay.CodeOffset), // mov eax, stub
                new byte[] { 0xFF, 0xD0 },                                         // call eax
                new byte[] { 0x5E, 0xC3 }));                                       // pop esi / ret

            // The list (thiscall, ret 0xC): records its arguments and copies the text.
            fake.Memory.WriteBytesAtAddress(r.List, Bytes(
                new byte[] { 0x8B, 0x44, 0x24, 0x08, 0xA3 }, BitConverter.GetBytes(rec + 4),   // colour
                new byte[] { 0x8B, 0x44, 0x24, 0x0C, 0xA3 }, BitConverter.GetBytes(rec + 8),   // number
                new byte[] { 0x89, 0x0D }, BitConverter.GetBytes(rec + 0xC),                   // ecx
                new byte[] { 0xFF, 0x05 }, BitConverter.GetBytes(rec),                         // inc calls
                new byte[] { 0x56, 0x57, 0x8B, 0x74, 0x24, 0x0C },                             // push esi / push edi / mov esi, [esp+0xC]
                new byte[] { 0xBF }, BitConverter.GetBytes(rec + 0x40),                        // mov edi, rec+0x40
                new byte[] { 0x8A, 0x06, 0x88, 0x07, 0x46, 0x47, 0x84, 0xC0, 0x75, 0xF6 },     // copy: mov al,[esi]; mov [edi],al; inc; inc; test; jnz
                new byte[] { 0x5F, 0x5E, 0xC2, 0x0C, 0x00 }));                                 // pop edi / pop esi / ret 0xC

            // The release (cdecl: manager): counts its calls.
            fake.Memory.WriteBytesAtAddress(r.Release, Bytes(
                new byte[] { 0xFF, 0x05 }, BitConverter.GetBytes(rec + 0x10),
                new byte[] { 0x8B, 0x44, 0x24, 0x04, 0xA3 }, BitConverter.GetBytes(rec + 0x14),
                new byte[] { 0xC3 }));

            // The check (cdecl: club, opponent, own team, opposing team, match, home): stops early for
            // speech 5; otherwise takes a use, judges odd numbers to fit, scrambles the registers and
            // releases the manager through the stub, with the verdict at [esp+0x10] as in the game.
            fake.Memory.WriteBytesAtAddress(r.Check, Bytes(
                new byte[] { 0xFF, 0x05 }, BitConverter.GetBytes(rec + 0x28),                  // inc checks
                new byte[] { 0x8B, 0x54, 0x24, 0x08, 0x89, 0x15 }, BitConverter.GetBytes(rec + 0x20), // opponent
                new byte[] { 0x8B, 0x54, 0x24, 0x14, 0x89, 0x15 }, BitConverter.GetBytes(rec + 0x18), // match
                new byte[] { 0x8B, 0x54, 0x24, 0x18, 0x89, 0x15 }, BitConverter.GetBytes(rec + 0x1C), // home
                new byte[] { 0x8B, 0x44, 0x24, 0x04 },                                         // mov eax, [esp+4]  club
                new byte[] { 0x0F, 0xBF, 0x88 }, BitConverter.GetBytes(Settings.ClubSpeechOffset), // movsx ecx, word [eax+0x1440]
                new byte[] { 0x83, 0xF9, 0x05, 0x75, 0x07 },                                   // cmp ecx, 5 / jne +7
                new byte[] { 0xFF, 0x05 }, BitConverter.GetBytes(rec + 0x24), new byte[] { 0xC3 }, // inc returned / ret
                new byte[] { 0x66, 0xFF, 0x8C, 0x48 }, BitConverter.GetBytes(Settings.ClubSpeechUsesOffset), // dec word [eax+ecx*2+0x1448]
                new byte[] { 0x66, 0xC7, 0x80 }, BitConverter.GetBytes(Settings.ClubSpeechOffset), new byte[] { 0x2A, 0x00 }, // mov word [eax+0x1440], 42
                new byte[] { 0x83, 0xE1, 0x01 },                                               // and ecx, 1
                new byte[] { 0x83, 0xEC, 0x30 },                                               // sub esp, 0x30
                new byte[] { 0x89, 0x4C, 0x24, 0x10 },                                         // mov [esp+0x10], ecx
                new byte[] { 0xBE, 0xEF, 0xBE, 0xAD, 0xDE, 0xBB, 0xEF, 0xBE, 0xAD, 0xDE },     // mov esi/ebx, 0xDEADBEEF
                new byte[] { 0xBF, 0xEF, 0xBE, 0xAD, 0xDE, 0xBD, 0xEF, 0xBE, 0xAD, 0xDE },     // mov edi/ebp, 0xDEADBEEF
                new byte[] { 0x68 }, BitConverter.GetBytes(r.Manager),                           // push manager
                new byte[] { 0xB8 }, BitConverter.GetBytes(r.Stub + SpeechEffectsDisplay.ReleaseCodeOffset), // mov eax, release hook
                new byte[] { 0xFF, 0xD0 },                                                     // call eax
                new byte[] { 0x83, 0xC4, 0x34 },                                               // add esp, 0x34
                new byte[] { 0xFF, 0x05 }, BitConverter.GetBytes(rec + 0x24), new byte[] { 0xC3 })); // inc returned / ret

            fake.Memory.WriteBytesAtAddress(r.Stub, SpeechEffectsDisplay.BuildStub(r.Stub, r.List, r.Check, r.Release));
            fake.Memory.WriteBytesAtAddress(r.Stub + SpeechEffectsDisplay.FlagOffset, new byte[] { 1 });

            // The dialog's fields and the club (Germany, ID 0x12; uses 2 each, speech 6 used up).
            uint[] fields = { r.Club, r.Opponent, 0x1E8, 0x1EC, 0x1F0, 1 };
            for (int i = 0; i < fields.Length; i++)
                fake.Memory.WriteBytesAtAddress(r.Dialog + 0x7FE0 + (uint)(i * 4), BitConverter.GetBytes(fields[i]));
            fake.Memory.WriteBytesAtAddress(r.Club + Settings.ClubTeamIdOffset, new byte[] { 0x12, (byte)PlayerEnums.Country.Deutschland });
            SetWord(fake, r.Club + Settings.ClubSpeechOffset, -1);
            for (int n = 1; n < PreMatchSpeeches.Count; n++)
                SetWord(fake, r.Club + Settings.ClubSpeechUsesOffset + (uint)(n * 2), (short)(n == 6 ? 0 : 2));
            // The manager: a skill at level 0 that must not be read, the one at his level.
            fake.Memory.WriteBytesAtAddress(r.Manager + Settings.ManagerMotivationLevelOffset, new[] { ManagerLevel });
            SetWord(fake, r.Manager + Settings.ManagerMotivationOffset, 99);
            SetSkill(fake, r, Skill);
            return r;
        }

        private static void SetSkill(FakeModule fake, Rig r, short skill) =>
            SetWord(fake, r.Manager + Settings.ManagerMotivationOffset + ManagerLevel * Settings.ManagerMotivationStride, skill);

        private static byte[] Bytes(params byte[][] parts) => parts.SelectMany(p => p).ToArray();

        private static void Run(FakeModule fake, Rig r, string text, uint number)
        {
            fake.Memory.WriteBytesAtAddress(r.Text, Latin1.GetBytes(text + "\0"));
            Marshal.GetDelegateForFunctionPointer<AddEntry>(new IntPtr(r.Call))(r.Dialog, r.Text, White, number);
        }

        private static void FillTable(FakeModule fake, Rig r, PlayerEnums.Country country, ushort clubId)
        {
            short[] uses = Enumerable.Repeat((short)2, PreMatchSpeeches.Count).ToArray();
            fake.Memory.WriteBytesAtAddress(r.Stub + SpeechEffectsDisplay.TablesOffset + SpeechEffectsDisplay.TableSize,
                SpeechEffectsDisplay.BuildTable(country, clubId, PreMatchSpeeches.Parse(SpeechTable()), uses));
        }

        private static string ListedText(FakeModule fake, Rig r)
        {
            byte[] text = fake.Memory.ReadBytesAtAddress(r.Record + 0x40, 0x100);
            return Latin1.GetString(text, 0, Array.IndexOf(text, (byte)0));
        }

        // The game's verdict as the colour, and the note in front of the text; afterwards the club's
        // speech and uses are as before, the manager is released once and the check never returned.
        [Theory]
        [InlineData(3u, SpeechEffectsDisplay.FitsColour, "+3% 2x Text")]
        [InlineData(4u, SpeechEffectsDisplay.DoesNotFitColour, "+4% Text")]
        public void Stub_ColoursBySpeechCheck_AndPutsTheNoteInFront(uint number, uint colour, string listed)
        {
            using (FakeModule fake = new FakeModule())
            {
                Rig r = Build(fake);
                FillTable(fake, r, PlayerEnums.Country.Deutschland, 0x12);

                Run(fake, r, "Text", number);

                Assert.Equal(1u, Dword(fake, r.Record));
                Assert.Equal(colour, Dword(fake, r.Record + 4));
                Assert.Equal(number, Dword(fake, r.Record + 8));
                Assert.Equal(0x1157u, Dword(fake, r.Record + 0xC));
                Assert.Equal(listed, ListedText(fake, r));
                Assert.Equal(1u, Dword(fake, r.Record + 0x28));
                Assert.Equal(0u, Dword(fake, r.Record + 0x24));
                Assert.Equal(1u, Dword(fake, r.Record + 0x10));
                Assert.Equal(r.Manager, Dword(fake, r.Record + 0x14));
                Assert.Equal(r.Opponent, Dword(fake, r.Record + 0x20));
                Assert.Equal(0x1F0u, Dword(fake, r.Record + 0x18));
                Assert.Equal(1u, Dword(fake, r.Record + 0x1C));
                Assert.Equal(-1, Word(fake, r.Club + Settings.ClubSpeechOffset));
                Assert.Equal(2, Word(fake, r.Club + Settings.ClubSpeechUsesOffset + number * 2));
                Assert.Equal(0, fake.Memory.ReadBytesAtAddress(r.Stub + SpeechEffectsDisplay.CheckingOffset, 1)[0]);
                // The game's own text stays as it was.
                Assert.Equal("Text\0", Latin1.GetString(fake.Memory.ReadBytesAtAddress(r.Text, 5)));

                // The game's own check (no check of the stub running) goes straight on to the release.
                Marshal.GetDelegateForFunctionPointer<ReleaseManager>(new IntPtr(r.Stub + SpeechEffectsDisplay.ReleaseCodeOffset))(0x99);
                Assert.Equal(2u, Dword(fake, r.Record + 0x10));
                Assert.Equal(0x99u, Dword(fake, r.Record + 0x14));
            }
        }

        // A fitting motivation speech (odd numbers fit) gets the chance of the game's roll after its note:
        // skill * 6 + 11 %, at most 100, at least 0.
        [Theory]
        [InlineData(29u, Skill, "+29% (71%) Text")]
        [InlineData(PreMatchSpeeches.FirstMotivationSpeech + 1u, (short)15, "+29% (100%) Text")]
        [InlineData(43u, (short)0, "+43% (11%) Text")]
        [InlineData(43u, (short)-1, "+43% (5%) Text")]
        [InlineData(43u, (short)-3, "+43% (0%) Text")]
        [InlineData(30u, Skill, "+30% Text")]
        [InlineData(27u, Skill, "+27% Text")]
        public void Stub_ForAFittingMotivationSpeech_ShowsTheChanceOfTheRoll(uint number, short skill, string listed)
        {
            using (FakeModule fake = new FakeModule())
            {
                Rig r = Build(fake);
                FillTable(fake, r, PlayerEnums.Country.Deutschland, 0x12);
                SetSkill(fake, r, skill);

                Run(fake, r, "Text", number);

                Assert.Equal(number % 2 == 1 ? SpeechEffectsDisplay.FitsColour : SpeechEffectsDisplay.DoesNotFitColour, Dword(fake, r.Record + 4));
                Assert.Equal(listed, ListedText(fake, r));
                Assert.Equal(1u, Dword(fake, r.Record + 0x10));
            }
        }

        [Theory]
        [InlineData(10, 71)]
        [InlineData(15, 100)]
        [InlineData(0, 11)]
        [InlineData(-2, 0)]
        public void MotivationChance_IsSkillTimes6Plus11_Clamped(int skill, int chance)
        {
            Assert.Equal(chance, PreMatchSpeeches.MotivationChance(skill));
        }

        [Fact]
        public void Stub_ForASpeechWithoutUsesLeft_ColoursItGreyWithoutACheck()
        {
            using (FakeModule fake = new FakeModule())
            {
                Rig r = Build(fake);

                Run(fake, r, "Text", 6);

                Assert.Equal(SpeechEffectsDisplay.UsedUpColour, Dword(fake, r.Record + 4));
                Assert.Equal(0u, Dword(fake, r.Record + 0x28));
                Assert.Equal("Text", ListedText(fake, r));
            }
        }

        [Fact]
        public void Stub_WhenTheCheckStopsEarly_KeepsTheGamesColour()
        {
            using (FakeModule fake = new FakeModule())
            {
                Rig r = Build(fake);

                Run(fake, r, "Text", 5);

                Assert.Equal(White, Dword(fake, r.Record + 4));
                Assert.Equal(1u, Dword(fake, r.Record + 0x24));
                Assert.Equal(0u, Dword(fake, r.Record + 0x10));
                Assert.Equal(-1, Word(fake, r.Club + Settings.ClubSpeechOffset));
                Assert.Equal(0, fake.Memory.ReadBytesAtAddress(r.Stub + SpeechEffectsDisplay.CheckingOffset, 1)[0]);
            }
        }

        [Theory]
        [InlineData(0u)]
        [InlineData(44u)]
        public void Stub_ForANumberOutsideTheTable_PassesThrough(uint number)
        {
            using (FakeModule fake = new FakeModule())
            {
                Rig r = Build(fake);
                FillTable(fake, r, PlayerEnums.Country.Deutschland, 0x12);

                Run(fake, r, "Text", number);

                Assert.Equal(White, Dword(fake, r.Record + 4));
                Assert.Equal("Text", ListedText(fake, r));
                Assert.Equal(0u, Dword(fake, r.Record + 0x28));
            }
        }

        [Fact]
        public void Stub_SwitchedOff_PassesThrough()
        {
            using (FakeModule fake = new FakeModule())
            {
                Rig r = Build(fake);
                FillTable(fake, r, PlayerEnums.Country.Deutschland, 0x12);
                fake.Memory.WriteBytesAtAddress(r.Stub + SpeechEffectsDisplay.FlagOffset, new byte[] { 0 });

                Run(fake, r, "Text", 3);

                Assert.Equal(White, Dword(fake, r.Record + 4));
                Assert.Equal("Text", ListedText(fake, r));
                Assert.Equal(0u, Dword(fake, r.Record + 0x28));
            }
        }

        [Fact]
        public void Stub_WithoutAClub_PassesThrough()
        {
            using (FakeModule fake = new FakeModule())
            {
                Rig r = Build(fake);
                fake.Memory.WriteBytesAtAddress(r.Dialog + Settings.SpeechDialogClubOffset, new byte[4]);

                Run(fake, r, "Text", 3);

                Assert.Equal(White, Dword(fake, r.Record + 4));
                Assert.Equal(0u, Dword(fake, r.Record + 0x28));
            }
        }

        [Fact]
        public void Stub_WithoutATableForTheClub_ColoursButKeepsTheText()
        {
            using (FakeModule fake = new FakeModule())
            {
                Rig r = Build(fake);
                FillTable(fake, r, PlayerEnums.Country.Deutschland, 0x13);

                Run(fake, r, "Text", 3);

                Assert.Equal(SpeechEffectsDisplay.FitsColour, Dword(fake, r.Record + 4));
                Assert.Equal("Text", ListedText(fake, r));
            }
        }

        [Fact]
        public void Stub_WithALongText_CutsItToWhatTheListKeeps()
        {
            using (FakeModule fake = new FakeModule())
            {
                Rig r = Build(fake);
                FillTable(fake, r, PlayerEnums.Country.Deutschland, 0x12);
                string text = new string('a', 0xF0);

                Run(fake, r, text, 4);

                string listed = ListedText(fake, r);
                Assert.Equal(SpeechEffectsDisplay.MaxTextLength, listed.Length);
                Assert.Equal(("+4% " + text).Substring(0, SpeechEffectsDisplay.MaxTextLength), listed);
            }
        }
    }
}
