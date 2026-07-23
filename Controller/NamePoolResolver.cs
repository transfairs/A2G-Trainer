using System;

namespace A2G_Trainer_XP.Controller
{
    // Resolves the true, save-persistent address of a player's Firstname/Lastname.
    //
    // Background: the address the rest of this project uses for names (Settings.PlayerAddress via
    // AddressPresets.*[FIRSTNAME]/[LASTNAME]) is a transient *display cache*. Writing there updates
    // what's shown on screen, but the value is rebuilt from scratch (at a new heap address) every
    // time a savegame loads, so it doesn't actually persist a rename. The real, permanent string
    // lives in a single pool of tightly packed, null-terminated strings that's rebuilt fresh (at a
    // new heap address) on every load. Two things about that pool ARE stable - found by tracing the
    // savegame-load path live in Cheat Engine, see docs/pointer-investigation-guide.md:
    //
    //   1. anstoss2.exe+7FDD58 always holds a pointer to the current pool's start, no matter when
    //      you read it (confirmed stable across restarts and across different savegames).
    //   2. Every player has a small fixed-size record at anstoss2.exe+516678 + PlayerId*0xC8 whose
    //      first 4 bytes are two 16-bit indices: [+0] = which string in the pool is their Firstname,
    //      [+2] = which string is their Lastname. PlayerId here is the existing 2-byte value at
    //      offset 0 of the player's normal (display-cache) struct - the same slot PlayerController
    //      already reads as a 1-byte "Id" today; NameRecordId on Player is the same field read wide.
    //
    // The pool has no fixed stride - names are variable length, so "the Nth string" only turns into
    // a byte offset by counting null terminators from the start; there's no arithmetic shortcut.
    // That also means overwriting one entry with a replacement of a DIFFERENT byte length would shift
    // every later name in the pool. Callers must only write same-length replacements (see
    // PlayerController.Save) until a safe pool-rewrite is implemented.
    public class NamePoolResolver
    {
        private const int FirstnameIndexOffset = 0;
        private const int LastnameIndexOffset = 2;

        // Starting size for the scan buffer, doubled until the target index is found or MaxScanBufferSize
        // is hit. A fixed 64KB buffer turned out too small in practice - Lastname indices for some
        // players run well past that (observed a failure to resolve at 64KB that a bigger buffer
        // fixed), so we grow instead of guessing one size.
        private const int InitialScanBufferSize = 0x10000;
        private const int MaxScanBufferSize = 0x800000; // 8 MB - comfortably above anything observed

        private readonly ProcessMemory memory;

        public NamePoolResolver(ProcessMemory memory)
        {
            this.memory = memory;
        }

        public uint? ResolveFirstnameAddress(ushort playerId) => this.Resolve(playerId, FirstnameIndexOffset);

        public uint? ResolveLastnameAddress(ushort playerId) => this.Resolve(playerId, LastnameIndexOffset);

        private uint? Resolve(ushort playerId, int indexFieldOffset)
        {
            uint recordAddress = this.memory.ModuleBase + Settings.PlayerRecordTableOffset + (uint)playerId * Settings.PlayerRecordStride;

            byte[] indexBytes = this.memory.ReadBytesAtAddress(recordAddress + (uint)indexFieldOffset, 2);
            if (indexBytes == null)
                return null;
            int targetIndex = BitConverter.ToUInt16(indexBytes, 0);

            byte[] poolBaseBytes = this.memory.ReadBytesAtAddress(this.memory.ModuleBase + Settings.NamePoolPointerOffset, 4);
            if (poolBaseBytes == null)
                return null;
            uint poolBase = BitConverter.ToUInt32(poolBaseBytes, 0);
            if (poolBase < 0x10000)
                return null;

            for (int scanBufferSize = InitialScanBufferSize; scanBufferSize <= MaxScanBufferSize; scanBufferSize *= 2)
            {
                byte[] buffer = this.memory.ReadBytesAtAddress(poolBase, scanBufferSize);
                if (buffer == null)
                    return null;

                int pos = 0;
                bool ranOffEndOfBuffer = false;
                for (int i = 0; i < targetIndex; i++)
                {
                    int nullIndex = Array.IndexOf(buffer, (byte)0, pos);
                    if (nullIndex < 0)
                    {
                        ranOffEndOfBuffer = true;
                        break; // this buffer size wasn't enough - retry with a bigger one
                    }
                    pos = nullIndex + 1;
                }

                if (!ranOffEndOfBuffer)
                    return poolBase + (uint)pos;
            }

            return null; // target index still not found even at MaxScanBufferSize - fail safely rather than guess
        }
    }
}
