using A2G_Trainer.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Shows in the game what each interview answer does: appends its effects, e.g. " [Fans -3, Vorstand -3]",
    /// to the answer texts of the game's question list, so the game itself shows them on the answer buttons.
    /// </summary>
    /// <remarks>
    /// The interview engine (Settings.InterviewEngineOffset) loads its 510 questions from TEXTE\inttext.crn
    /// at the first interview after the game starts (flag +0x14, CPtrList +0x8C, head +0x90). A question
    /// (0xA8 bytes) holds its four answers as MFC CStrings at +0x54..+0x60 and each answer's two effect
    /// pairs (code, value) at +0x64 + answer * 0x10. A labelled answer gets a new CString (its own block
    /// in the game process, with a reference count the game never counts down to 0, and the original
    /// pointer stored right before it); switching off puts the original pointers back. Only the
    /// effects InterviewEffectsFix lets the game apply are named, with codes 10 and 12 as the fix maps them.
    /// </remarks>
    internal sealed class InterviewEffectsDisplay : IDisposable
    {
        internal const uint EngineLoadedOffset = 0x14;
        internal const uint EngineQuestionsHeadOffset = 0x90;
        internal const uint AnswerTextsOffset = 0x54;
        internal const uint AnswerEffectsOffset = 0x64;
        internal const int QuestionSize = 0xA8;
        internal const int Answers = 4;
        // The game keeps 510 questions; more nodes than this mean the list isn't what it should be.
        private const int MaxQuestions = 2000;
        private const int MaxTextLength = 1024;
        // A CString's data: reference count, length and capacity right before the text. Ours is
        // preceded by the original pointer.
        private const int StringHeader = 12;
        private const int EntryHeader = 4 + StringHeader;
        private const int LockedReferences = 0x40000000;
        private const int BlockSize = 0x40000;
        private const int TickMilliseconds = 250;

        private static readonly Encoding Latin1 = Encoding.GetEncoding("iso-8859-1");
        private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

        private readonly ProcessMemory memory;
        private readonly object sync = new object();
        // Each labelled answer slot: the pointer it had and the one it has now.
        private readonly Dictionary<uint, Pointers> labelled = new Dictionary<uint, Pointers>();
        // Strings already written, by original pointer, reused when labels are switched back on.
        private readonly Dictionary<uint, uint> written = new Dictionary<uint, uint>();
        private Timer timer;
        private uint block, blockUsed;

        private sealed class Pointers
        {
            public uint Original, Labelled;
        }

        /// <summary>Creates the display for the attached game; it does nothing until <see cref="Enabled"/>.</summary>
        internal InterviewEffectsDisplay(ProcessMemory memory) => this.memory = memory;

        /// <summary>Whether the answers show their effects while the box is ticked.</summary>
        internal bool Enabled
        {
            get => this.timer != null;
            set
            {
                lock (this.sync)
                {
                    if (value == this.Enabled)
                        return;
                    if (value)
                        this.timer = new Timer(_ => this.TimerTick(), null, 0, TickMilliseconds);
                    else
                    {
                        this.timer.Dispose();
                        this.timer = null;
                        this.Restore();
                    }
                }
            }
        }

        /// <inheritdoc />
        public void Dispose() => this.Enabled = false;

        /// <summary>The label for an answer's four effect values (two pairs of code and value), e.g. " [Fans -3, Vorstand -3]".</summary>
        internal static string Describe(int[] effects)
        {
            List<string> parts = new List<string>();
            for (int pair = 0; pair + 1 < effects.Length; pair += 2)
            {
                string part = DescribeEffect(effects[pair], effects[pair + 1]);
                if (part != null)
                    parts.Add(part);
            }
            return " [" + (parts.Count > 0 ? string.Join(", ", parts) : "keine Wirkung") + "]";
        }

        // What a pair does with InterviewEffectsFix (FUN_004f87f0, with 10 and 12 as the fix maps them);
        // null for codes the game ignores or uses only to steer the interview (4, 7, 8: mood, topic,
        // follow-up question).
        private static string DescribeEffect(int code, int value)
        {
            string signed = value.ToString("+0;-0;0");
            switch (code)
            {
                case 0: return "Fans " + signed;
                case 1: return "Öffentlichkeit " + signed;
                case 2: return "Medien " + signed;
                case 3: return "Reporter " + signed;
                case 5: return "Vorstand " + signed;
                case 6: return "Schiri";
                // Stored in tenths (0..200), shown by the game as 0,0..20,0.
                case InterviewEffectsFix.TeamMoraleCode: return "Moral " + (value / 10.0).ToString("+0.0;-0.0;0", German);
                case 11: return "Form -1";
                case InterviewEffectsFix.FineCode: return "Strafe " + value.ToString("#,0", German) + " DM";
                case 13:
                case 14: return "Abbruch";
                default: return null;
            }
        }

        // A tick already queued when the display was switched off must not label again.
        private void TimerTick()
        {
            lock (this.sync)
            {
                if (this.timer != null)
                    this.Tick();
            }
        }

        // One tick: label every answer of the loaded question list that isn't labelled yet.
        internal void Tick()
        {
            lock (this.sync)
            {
                try
                {
                    foreach (uint question in this.Questions())
                        this.Label(question);
                }
                catch (Exception ex)
                {
                    Logger.Warn("InterviewEffectsDisplay", ex);
                }
            }
        }

        // The questions of the engine's list, empty while it isn't loaded.
        private IEnumerable<uint> Questions()
        {
            FixedAddresses addresses = FixedAddresses.For(this.memory);
            if (addresses == null)
                yield break;
            uint? engine = this.ReadUInt32(addresses.Runtime(this.memory, Settings.InterviewEngineOffset));
            if (!engine.HasValue || engine.Value == 0)
                yield break;
            byte[] loaded = this.memory.ReadBytesAtAddress(engine.Value + EngineLoadedOffset, 1);
            if (loaded == null || loaded[0] != 1)
                yield break;

            uint? node = this.ReadUInt32(engine.Value + EngineQuestionsHeadOffset);
            for (int count = 0; node.HasValue && node.Value != 0 && count < MaxQuestions; count++)
            {
                uint? question = this.ReadUInt32(node.Value + 8);
                if (question.HasValue && question.Value != 0)
                    yield return question.Value;
                node = this.ReadUInt32(node.Value);
            }
        }

        private void Label(uint question)
        {
            byte[] bytes = this.memory.ReadBytesAtAddress(question, QuestionSize);
            if (bytes == null)
                return;
            for (int answer = 0; answer < Answers; answer++)
            {
                uint slot = question + AnswerTextsOffset + (uint)answer * 4;
                uint current = BitConverter.ToUInt32(bytes, (int)AnswerTextsOffset + answer * 4);
                if (current == 0 || (this.labelled.TryGetValue(slot, out Pointers known) && known.Labelled == current))
                    continue;

                int[] effects = new int[4];
                for (int i = 0; i < effects.Length; i++)
                    effects[i] = BitConverter.ToInt32(bytes, (int)AnswerEffectsOffset + answer * 0x10 + i * 4);
                byte[] suffix = Latin1.GetBytes(Describe(effects));

                // Labelled by an earlier trainer session: keep it, but remember the original for switching off.
                uint? original = this.LabelledOriginal(current, suffix);
                if (original.HasValue)
                {
                    this.labelled[slot] = new Pointers { Original = original.Value, Labelled = current };
                    continue;
                }
                if (!this.written.TryGetValue(current, out uint text))
                {
                    byte[] originalText = this.ReadText(current);
                    if (originalText == null)
                        continue;
                    uint? entry = this.WriteEntry(current, Concat(originalText, suffix));
                    if (!entry.HasValue)
                        return;
                    text = entry.Value;
                    this.written[current] = text;
                }
                this.memory.WriteBytesAtAddress(slot, BitConverter.GetBytes(text));
                this.labelled[slot] = new Pointers { Original = current, Labelled = text };
            }
        }

        // If text is one of our labelled strings (the original pointer before it, and its text plus
        // suffix), the original pointer; otherwise null.
        private uint? LabelledOriginal(uint text, byte[] suffix)
        {
            uint? original = this.ReadUInt32(text - EntryHeader);
            if (!original.HasValue || original.Value == 0 || original.Value == text)
                return null;
            byte[] current = this.ReadText(text);
            byte[] before = this.ReadText(original.Value);
            if (current == null || before == null || current.Length != before.Length + suffix.Length)
                return null;
            for (int i = 0; i < current.Length; i++)
            {
                if (current[i] != (i < before.Length ? before[i] : suffix[i - before.Length]))
                    return null;
            }
            return original;
        }

        // The bytes of the CString at text (its length is stored right before it), or null.
        private byte[] ReadText(uint text)
        {
            byte[] header = this.memory.ReadBytesAtAddress(text - StringHeader, StringHeader);
            if (header == null)
                return null;
            int length = BitConverter.ToInt32(header, 4);
            if (length < 0 || length > MaxTextLength)
                return null;
            return length == 0 ? new byte[0] : this.memory.ReadBytesAtAddress(text, length);
        }

        // Writes the original pointer, a CString header and the text into the block; returns the new
        // CString pointer, or null if no memory could be had.
        private uint? WriteEntry(uint original, byte[] text)
        {
            int size = (EntryHeader + text.Length + 1 + 3) & ~3;
            if (this.block == 0 || this.blockUsed + size > BlockSize)
            {
                uint? allocated = this.memory.AllocateExecutable(BlockSize);
                if (!allocated.HasValue)
                {
                    Logger.Warn("InterviewEffectsDisplay: kein Speicher im Spiel für die Antworttexte.");
                    return null;
                }
                this.block = allocated.Value;
                this.blockUsed = 0;
            }
            byte[] entry = new byte[size];
            BitConverter.GetBytes(original).CopyTo(entry, 0);
            BitConverter.GetBytes(LockedReferences).CopyTo(entry, 4);
            BitConverter.GetBytes(text.Length).CopyTo(entry, 8);
            BitConverter.GetBytes(text.Length).CopyTo(entry, 12);
            text.CopyTo(entry, EntryHeader);
            uint address = this.block + this.blockUsed;
            this.memory.WriteBytesAtAddress(address, entry);
            this.blockUsed += (uint)size;
            return address + EntryHeader;
        }

        // Puts the original texts back where the game still has ours.
        private void Restore()
        {
            try
            {
                foreach (KeyValuePair<uint, Pointers> slot in this.labelled)
                {
                    if (this.ReadUInt32(slot.Key) == slot.Value.Labelled)
                        this.memory.WriteBytesAtAddress(slot.Key, BitConverter.GetBytes(slot.Value.Original));
                }
            }
            catch (Exception ex)
            {
                Logger.Warn("InterviewEffectsDisplay", ex);
            }
            this.labelled.Clear();
        }

        private uint? ReadUInt32(uint address)
        {
            byte[] bytes = this.memory.ReadBytesAtAddress(address, 4);
            return bytes != null ? BitConverter.ToUInt32(bytes, 0) : (uint?)null;
        }

        private static byte[] Concat(byte[] first, byte[] second)
        {
            byte[] result = new byte[first.Length + second.Length];
            first.CopyTo(result, 0);
            second.CopyTo(result, first.Length);
            return result;
        }
    }
}
