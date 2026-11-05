using System;
using A2G_Trainer.Model;
using AnstossTrainer.Shared;

namespace A2G_Trainer.Controller
{
    /// <summary>
    /// The game's transfer list (Settings.TransferListOffset): one global list for every club, saved
    /// with the savegame. A player's career bit 4 mirrors whether he is on it.
    /// </summary>
    internal static class TransferList
    {
        // FUN_0062be30 refuses higher IDs (the generated filler players).
        private const int MaxPlayerId = 15000;
        // Entry +0xC: a rating the game computes when listing (FUN_005f8a40, about 100) but never reads back.
        private const short ListingRating = 100;

        /// <summary>
        /// Puts the player on the list for sale, the way the manager does it in the game
        /// (FUN_0062c820 -> FUN_0062be30): appended at the end, dated today, marked as listed by a
        /// human so it can be taken off in the game too. Returns false if the list is full or
        /// unreadable, he's already on it, or the ID is one the game refuses. The caller sets career bit 4.
        /// </summary>
        internal static bool Add(ProcessMemory memory, FixedAddresses fixedAddresses, Player player)
        {
            if (fixedAddresses == null || player.NameRecordId > MaxPlayerId)
                return false;
            uint list = fixedAddresses.Runtime(memory, Settings.TransferListOffset);
            byte[] countBytes = memory.ReadBytesAtAddress(list, 2);
            byte[] dayBytes = memory.ReadBytesAtAddress(fixedAddresses.Runtime(memory, Settings.GlobalCounterOffset), 2);
            if (countBytes == null || dayBytes == null)
                return false;
            short count = BitConverter.ToInt16(countBytes, 0);
            if (count < 0 || count >= Settings.TransferListCapacity)
                return false;

            uint entries = list + Settings.TransferListEntriesOffset;
            int stride = (int)Settings.TransferListEntryStride;
            byte[] all = count > 0 ? memory.ReadBytesAtAddress(entries, count * stride) : new byte[0];
            if (all == null)
                return false;
            for (int i = 0; i < count; i++)
            {
                if (BitConverter.ToUInt16(all, i * stride) == player.NameRecordId)
                    return false;
            }

            byte[] entry = new byte[stride];
            Array.Copy(BitConverter.GetBytes(player.NameRecordId), 0, entry, 0, 2);
            entry[2] = (byte)player.ClubCountry;
            Array.Copy(BitConverter.GetBytes(player.ClubId), 0, entry, 4, 2);
            entry[6] = (byte)player.Position;
            entry[7] = player.Level;
            Array.Copy(dayBytes, 0, entry, 8, 2);
            entry[0xA] = 1;
            entry[0xB] = 1;
            Array.Copy(BitConverter.GetBytes(ListingRating), 0, entry, 0xC, 2);
            memory.WriteBytesAtAddress(entries + (uint)(count * stride), entry);
            memory.WriteBytesAtAddress(list, BitConverter.GetBytes((short)(count + 1)));
            Logger.Debug($"TransferList: put player {player.NameRecordId} on (entry {count}).");
            return true;
        }

        /// <summary>
        /// Takes the player off the list the way the game does it (FUN_0062c0f0): the entries behind
        /// move up one place and the count drops; the last slot keeps its old bytes. Returns whether
        /// he was on it. The caller clears career bit 4.
        /// </summary>
        internal static bool Remove(ProcessMemory memory, FixedAddresses fixedAddresses, ushort playerId)
        {
            if (fixedAddresses == null)
                return false;

            uint list = fixedAddresses.Runtime(memory, Settings.TransferListOffset);
            byte[] countBytes = memory.ReadBytesAtAddress(list, 2);
            if (countBytes == null)
                return false;
            short count = BitConverter.ToInt16(countBytes, 0);
            if (count <= 0 || count > Settings.TransferListCapacity)
                return false;

            uint entries = list + Settings.TransferListEntriesOffset;
            int stride = (int)Settings.TransferListEntryStride;
            byte[] all = memory.ReadBytesAtAddress(entries, count * stride);
            if (all == null)
                return false;

            for (int i = 0; i < count; i++)
            {
                if (BitConverter.ToUInt16(all, i * stride) != playerId)
                    continue;

                int behind = (count - i - 1) * stride;
                if (behind > 0)
                {
                    byte[] rest = new byte[behind];
                    Array.Copy(all, (i + 1) * stride, rest, 0, behind);
                    memory.WriteBytesAtAddress(entries + (uint)(i * stride), rest);
                }
                memory.WriteBytesAtAddress(list, BitConverter.GetBytes((short)(count - 1)));
                Logger.Debug($"TransferList: took player {playerId} off (entry {i} of {count}).");
                return true;
            }
            return false;
        }
    }
}
