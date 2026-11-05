using System;
using System.Collections.Generic;
using System.IO;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// The block compression of the game's savegame files (unpacked by FUN_00655b10, packed by
    /// FUN_00655510 via FUN_006319d0). A packed block starts with an Int32: negative = that many
    /// bytes follow unpacked, positive = unpacked length of the LZ77 bit stream that follows.
    /// </summary>
    /// <remarks>
    /// The bit stream is read in little-endian UInt32 words, least significant bit first. Flag
    /// bit 0: a literal byte (8 bits). Flag bit 1: a back reference - 3 bits select a distance
    /// class k (k+1 further bits, plus 2^(k+1)-2, the static tables at +2F8528/+2F8568), the copy
    /// starts that value + 1 bytes back; the length is 2 plus fields of 2, 3, 4, ... bits, each
    /// further field following only while the previous one was all ones (table +2F858C).
    /// </remarks>
    internal static class SaveGameCodec
    {
        // Largest distance a back reference can reach: class 7 covers 254..509, plus 1.
        private const int MaxDistance = 510;
        private const int WindowSize = 1024;
        private const int MinMatch = 2;
        private const int MaxChain = 96;
        // Keeps the length fields within 15 bits: the game's mask table (+2F858C) is only known to
        // hold sensible entries that far, and longer runs cost next to nothing anyway.
        private const int MaxMatch = 0x8000;

        /// <summary>Unpacks a block as the game does; throws <see cref="InvalidDataException"/> if it is damaged.</summary>
        public static byte[] Decompress(byte[] packed)
        {
            if (packed == null || packed.Length < 4)
                throw new InvalidDataException("Block ist zu kurz.");

            int length = BitConverter.ToInt32(packed, 0);
            if (length <= 0)
            {
                int raw = -length;
                if (packed.Length < 4 + raw)
                    throw new InvalidDataException("Unkomprimierter Block ist zu kurz.");
                byte[] copy = new byte[raw];
                Array.Copy(packed, 4, copy, 0, raw);
                return copy;
            }

            byte[] output = new byte[length];
            BitReader reader = new BitReader(packed, 4);
            int position = 0;
            while (position < length)
            {
                if (reader.Read(1) == 0)
                {
                    output[position++] = (byte)reader.Read(8);
                    continue;
                }

                int distanceClass = (int)reader.Read(3);
                int distance = (int)reader.Read(distanceClass + 1) + (1 << (distanceClass + 1)) - 2 + 1;
                int count = MinMatch;
                for (int width = 2; ; width++)
                {
                    uint field = reader.Read(width);
                    count += (int)field;
                    if (field != (1u << width) - 1)
                        break;
                }
                if (distance > position || count > length - position)
                    throw new InvalidDataException("Ungültiger Rückverweis im Block.");
                for (int i = 0; i < count; i++, position++)
                    output[position] = output[position - distance];
            }
            return output;
        }

        /// <summary>Packs <paramref name="data"/> so that <see cref="Decompress"/> (and the game) restore it exactly.</summary>
        public static byte[] Compress(byte[] data)
        {
            if (data.Length == 0)
                return new byte[4];

            BitWriter writer = new BitWriter(Math.Min(data.Length, 0x10000));
            int[] head = new int[1 << 16];
            // Earlier position with the same hash, kept only for the last WindowSize positions: the
            // chain walk stops at MaxDistance anyway, before it could reach an overwritten slot.
            int[] previous = new int[WindowSize];
            for (int i = 0; i < head.Length; i++)
                head[i] = -1;

            int position = 0;
            while (position < data.Length)
            {
                int bestLength = 0, bestDistance = 0;
                if (position + MinMatch <= data.Length)
                {
                    int candidate = head[Hash(data, position)];
                    for (int chain = 0; candidate >= 0 && position - candidate <= MaxDistance && chain < MaxChain; chain++)
                    {
                        int matched = 0;
                        while (matched < MaxMatch && position + matched < data.Length && data[candidate + matched] == data[position + matched])
                            matched++;
                        if (matched > bestLength)
                        {
                            bestLength = matched;
                            bestDistance = position - candidate;
                            if (matched == MaxMatch || position + matched == data.Length)
                                break;
                        }
                        candidate = previous[candidate & (WindowSize - 1)];
                    }
                }

                int step;
                if (bestLength >= MinMatch && Cost(bestDistance, bestLength) < bestLength * 9)
                {
                    WriteMatch(writer, bestDistance, bestLength);
                    step = bestLength;
                }
                else
                {
                    writer.Write(0, 1);
                    writer.Write(data[position], 8);
                    step = 1;
                }

                for (int end = position + step; position < end; position++)
                {
                    if (position + MinMatch <= data.Length)
                    {
                        int hash = Hash(data, position);
                        previous[position & (WindowSize - 1)] = head[hash];
                        head[hash] = position;
                    }
                }
            }

            byte[] bits = writer.ToArray();
            if (bits.Length >= data.Length)
                return Raw(data);

            byte[] packed = new byte[4 + bits.Length];
            Array.Copy(BitConverter.GetBytes(data.Length), packed, 4);
            Array.Copy(bits, 0, packed, 4, bits.Length);
            return packed;
        }

        /// <summary>A block holding <paramref name="data"/> unpacked (negative length), which the game reads as well.</summary>
        public static byte[] Raw(byte[] data)
        {
            byte[] packed = new byte[4 + data.Length];
            Array.Copy(BitConverter.GetBytes(-data.Length), packed, 4);
            Array.Copy(data, 0, packed, 4, data.Length);
            return packed;
        }

        private static int Hash(byte[] data, int position) => data[position] | (data[position + 1] << 8);

        private static int DistanceClass(int distance)
        {
            int value = distance - 1;
            int distanceClass = 0;
            while (value > (1 << (distanceClass + 2)) - 3)
                distanceClass++;
            return distanceClass;
        }

        // Bits a back reference takes, to decide whether it beats literals.
        private static int Cost(int distance, int length)
        {
            int bits = 1 + 3 + DistanceClass(distance) + 1;
            int rest = length - MinMatch;
            for (int width = 2; ; width++)
            {
                bits += width;
                int full = (1 << width) - 1;
                if (rest < full)
                    return bits;
                rest -= full;
            }
        }

        private static void WriteMatch(BitWriter writer, int distance, int length)
        {
            int distanceClass = DistanceClass(distance);
            writer.Write(1, 1);
            writer.Write((uint)distanceClass, 3);
            writer.Write((uint)(distance - 1 - ((1 << (distanceClass + 1)) - 2)), distanceClass + 1);
            int rest = length - MinMatch;
            for (int width = 2; ; width++)
            {
                uint full = (1u << width) - 1;
                if (rest < full)
                {
                    writer.Write((uint)rest, width);
                    return;
                }
                writer.Write(full, width);
                rest -= (int)full;
            }
        }

        private sealed class BitReader
        {
            private readonly byte[] data;
            private long bit;

            public BitReader(byte[] data, int offset)
            {
                this.data = data;
                this.bit = (long)offset * 8;
            }

            public uint Read(int count)
            {
                uint value = 0;
                for (int i = 0; i < count; i++, this.bit++)
                {
                    long index = this.bit >> 3;
                    if (index >= this.data.Length)
                        throw new InvalidDataException("Block endet zu früh.");
                    value |= (uint)((this.data[index] >> (int)(this.bit & 7)) & 1) << i;
                }
                return value;
            }
        }

        private sealed class BitWriter
        {
            private readonly List<byte> bytes;
            private int current;
            private int used;

            public BitWriter(int capacity)
            {
                this.bytes = new List<byte>(capacity);
            }

            public void Write(uint value, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    this.current |= (int)((value >> i) & 1) << this.used;
                    if (++this.used == 8)
                    {
                        this.bytes.Add((byte)this.current);
                        this.current = 0;
                        this.used = 0;
                    }
                }
            }

            // Whole UInt32 words, as the game reads them, plus one spare word: its reader fetches
            // the next word as soon as the current one is used up.
            public byte[] ToArray()
            {
                if (this.used > 0)
                    this.bytes.Add((byte)this.current);
                while (this.bytes.Count % 4 != 0)
                    this.bytes.Add(0);
                for (int i = 0; i < 4; i++)
                    this.bytes.Add(0);
                return this.bytes.ToArray();
            }
        }
    }
}
