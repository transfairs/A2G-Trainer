using System;
using System.Collections.Generic;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// Collects the bytes of a small x86 stub, with short forward jumps (rel8) to labels resolved when
    /// the code is taken. Used by the stubs the trainer puts into the game (InterviewEffectsFix,
    /// OverconfidenceRule).
    /// </summary>
    internal sealed class X86Assembler
    {
        private readonly List<byte> code = new List<byte>();
        private readonly Dictionary<string, int> labels = new Dictionary<string, int>();
        private readonly List<KeyValuePair<int, string>> jumps = new List<KeyValuePair<int, string>>();

        /// <summary>Appends raw bytes.</summary>
        public void Emit(params byte[] bytes) => this.code.AddRange(bytes);

        /// <summary>Appends a little-endian dword.</summary>
        public void Dword(uint value) => this.code.AddRange(BitConverter.GetBytes(value));

        /// <summary>Appends a short jump (opcode + rel8) to <paramref name="label"/>, resolved by <see cref="ToArray"/>.</summary>
        public void Jump(byte opcode, string label)
        {
            this.Emit(opcode, 0x00);
            this.jumps.Add(new KeyValuePair<int, string>(this.code.Count - 1, label));
        }

        /// <summary>Puts <paramref name="label"/> at the current position.</summary>
        public void Mark(string label) => this.labels[label] = this.code.Count;

        /// <summary>The code with every jump resolved.</summary>
        public byte[] ToArray()
        {
            foreach (KeyValuePair<int, string> jump in this.jumps)
                this.code[jump.Key] = (byte)(this.labels[jump.Value] - (jump.Key + 1));
            return this.code.ToArray();
        }
    }
}
