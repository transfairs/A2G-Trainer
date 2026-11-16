using System;
using System.Collections.Generic;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Collects the bytes of a small x86 stub, with jumps (rel8 or rel32) to labels resolved when the
    /// code is taken. Used by the stubs the trainer puts into the game (InterviewEffectsFix,
    /// OverconfidenceRule, SpeechEffectsDisplay).
    /// </summary>
    internal sealed class X86Assembler
    {
        private readonly List<byte> code = new List<byte>();
        private readonly Dictionary<string, int> labels = new Dictionary<string, int>();
        // Position of each jump's displacement, its size (1 or 4) and its label.
        private readonly List<Tuple<int, int, string>> jumps = new List<Tuple<int, int, string>>();

        /// <summary>Appends raw bytes.</summary>
        public void Emit(params byte[] bytes) => this.code.AddRange(bytes);

        /// <summary>Appends a little-endian dword.</summary>
        public void Dword(uint value) => this.code.AddRange(BitConverter.GetBytes(value));

        /// <summary>Appends a short jump (opcode + rel8) to <paramref name="label"/>, resolved by <see cref="ToArray"/>.</summary>
        public void Jump(byte opcode, string label)
        {
            this.Emit(opcode, 0x00);
            this.jumps.Add(Tuple.Create(this.code.Count - 1, 1, label));
        }

        /// <summary>
        /// Appends a near jump (opcode bytes + rel32, e.g. E9 or 0F 84) to <paramref name="label"/>,
        /// resolved by <see cref="ToArray"/>.
        /// </summary>
        public void JumpNear(string label, params byte[] opcode)
        {
            this.Emit(opcode);
            this.Dword(0);
            this.jumps.Add(Tuple.Create(this.code.Count - 4, 4, label));
        }

        /// <summary>Fills with int3 up to <paramref name="position"/>; throws if the code is already longer.</summary>
        public void PadTo(int position)
        {
            if (this.code.Count > position)
                throw new InvalidOperationException($"X86Assembler: code at 0x{this.code.Count:X} is past 0x{position:X}.");
            while (this.code.Count < position)
                this.code.Add(0xCC);
        }

        /// <summary>The current position (bytes so far).</summary>
        public int Position => this.code.Count;

        /// <summary>Puts <paramref name="label"/> at the current position.</summary>
        public void Mark(string label) => this.labels[label] = this.code.Count;

        /// <summary>The code with every jump resolved.</summary>
        public byte[] ToArray()
        {
            foreach (Tuple<int, int, string> jump in this.jumps)
            {
                int displacement = this.labels[jump.Item3] - (jump.Item1 + jump.Item2);
                if (jump.Item2 == 1)
                {
                    if (displacement < sbyte.MinValue || displacement > sbyte.MaxValue)
                        throw new InvalidOperationException($"X86Assembler: short jump to {jump.Item3} out of range.");
                    this.code[jump.Item1] = unchecked((byte)displacement);
                }
                else
                {
                    byte[] rel = BitConverter.GetBytes(displacement);
                    for (int i = 0; i < 4; i++)
                        this.code[jump.Item1 + i] = rel[i];
                }
            }
            return this.code.ToArray();
        }
    }
}
