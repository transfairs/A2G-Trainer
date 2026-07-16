using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace A2G_Trainer_XP.Controller
{
    public class MemProcess
    {
        public Process Process { get; internal set; }
        public ProcessModule MainModule { get; internal set; }
        internal IntPtr Handle { get; set; }
        internal IntPtr ModuleBaseAddress { get; set; }
    }

    // Replaces the external libs\Memory.dll. Only implements the exact address-string dialect
    // ("ModuleName+hexA,hexB") this project actually generates: hexA is the offset - from the
    // module base - of a static pointer field, hexB is the offset within the struct it points to.
    // Verified against the live game: dereferencing base+hexA and reading at (that pointer + hexB)
    // reproduces the exact field values the original Memory.dll produced.
    public class ProcessMemory
    {
        private const uint ProcessAllAccess = 2035711u;
        private const uint MinValidAddress = 0x10000;

        public MemProcess mProc { get; } = new MemProcess();

        [DllImport("kernel32.dll", EntryPoint = "OpenProcess")]
        private static extern IntPtr OpenProcessNative(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll")]
        private static extern bool ReadProcessMemory(IntPtr hProcess, UIntPtr lpBaseAddress, [Out] byte[] lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        private static extern bool WriteProcessMemory(IntPtr hProcess, UIntPtr lpBaseAddress, byte[] lpBuffer, UIntPtr nSize, IntPtr lpNumberOfBytesWritten);

        public bool OpenProcess(int pid)
        {
            if (pid <= 0)
                return false;

            // Already attached to this exact process - cheap no-op, matches the original library's
            // behavior (callers rely on this being safe to call every tick).
            if (this.mProc.Process != null && this.mProc.Process.Id == pid)
                return true;

            Process process;
            try
            {
                process = Process.GetProcessById(pid);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (!process.Responding)
                return false;

            IntPtr handle = OpenProcessNative(ProcessAllAccess, true, pid);
            if (handle == IntPtr.Zero)
                return false;

            this.mProc.Process = process;
            this.mProc.Handle = handle;
            this.mProc.MainModule = process.MainModule;
            this.mProc.ModuleBaseAddress = process.MainModule.BaseAddress;
            return true;
        }

        public int ReadByte(string address)
        {
            byte[] bytes = ReadBytes(address, 1);
            return bytes != null ? bytes[0] : 0;
        }

        public byte[] ReadBytes(string address, int length)
        {
            uint finalAddress = ResolveAddress(address);
            if (finalAddress < MinValidAddress)
                return null;

            byte[] buffer = new byte[length];
            if (!ReadProcessMemory(this.mProc.Handle, (UIntPtr)finalAddress, buffer, (UIntPtr)length, IntPtr.Zero))
                return null;

            return buffer;
        }

        public string ReadString(string address, int length, Encoding stringEncoding)
        {
            byte[] bytes = ReadBytes(address, length);
            if (bytes == null)
                return "";

            return stringEncoding.GetString(bytes).Split('\0')[0];
        }

        public void WriteBytes(string address, byte[] data)
        {
            uint finalAddress = ResolveAddress(address);
            if (finalAddress < MinValidAddress)
                return;

            WriteProcessMemory(this.mProc.Handle, (UIntPtr)finalAddress, data, (UIntPtr)data.Length, IntPtr.Zero);
        }

        public void WriteMemory(string address, string type, string value, Encoding stringEncoding)
        {
            if (type != "string")
                throw new ArgumentException($"ProcessMemory.WriteMemory only supports the \"string\" type, got \"{type}\".", nameof(type));

            WriteBytes(address, stringEncoding.GetBytes(value));
        }

        // "ModuleName+hexA,hexB" -> *(uint32*)(mainModuleBase + hexA) + hexB.
        // The module name itself is never resolved by lookup: every address this project generates
        // refers to its own already-attached main module, whose base address is cached in OpenProcess.
        private uint ResolveAddress(string address)
        {
            int plusIndex = address.IndexOf('+');
            int commaIndex = address.IndexOf(',');
            if (plusIndex < 0 || commaIndex < 0 || commaIndex < plusIndex)
                throw new FormatException($"Unsupported address format: \"{address}\". Expected \"Module+hexA,hexB\".");

            uint offsetA = ParseHex(address.Substring(plusIndex + 1, commaIndex - plusIndex - 1));
            uint offsetB = ParseHex(address.Substring(commaIndex + 1));

            uint moduleBase = (uint)this.mProc.ModuleBaseAddress.ToInt64();
            uint pointerFieldAddress = moduleBase + offsetA;

            byte[] pointerBytes = new byte[4];
            if (!ReadProcessMemory(this.mProc.Handle, (UIntPtr)pointerFieldAddress, pointerBytes, (UIntPtr)4, IntPtr.Zero))
                return 0;

            uint pointerValue = BitConverter.ToUInt32(pointerBytes, 0);
            return pointerValue + offsetB;
        }

        private static uint ParseHex(string token)
        {
            token = token.Trim();
            return token.Length == 0 ? 0u : Convert.ToUInt32(token, 16);
        }
    }
}
