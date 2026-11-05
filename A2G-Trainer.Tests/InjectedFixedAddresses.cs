using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using A2G_Trainer.Controller;

namespace A2G_Trainer.Tests
{
    /// <summary>
    /// Test support for code that looks its addresses up through FixedAddresses.For(memory): makes
    /// the FakeModule look like a calibrated non-original build (shift 0 for the player records) and
    /// registers the given FixedAddresses as what was "discovered" for that layout, so tests can
    /// hand in their own code addresses and runtime shifts.
    /// </summary>
    internal static class InjectedFixedAddresses
    {
        public static void Use(FakeModule fake, FixedAddresses addresses)
        {
            fake.Memory.Build = GameBuild.Gog;
            PersistentLayout layout = new PersistentLayout(unchecked(Settings.PlayerRecordTableOffset + addresses.RuntimeShift), null, null, null, isVerified: true);
            Type holderType = typeof(FixedAddresses).GetNestedType("Holder", BindingFlags.NonPublic);
            object holder = Activator.CreateInstance(holderType, nonPublic: true);
            holderType.GetField("Value").SetValue(holder, addresses);
            object table = typeof(FixedAddresses).GetField("Discovered", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            table.GetType().GetMethod("Add").Invoke(table, new[] { layout, holder });
            fake.Memory.Layout = layout;
        }
    }

    /// <summary>
    /// A page of this process's memory that can be read and written but never made executable (a
    /// view of a section created without execute rights): ProcessMemory.WriteCode fails on it, as on
    /// code another tool protected.
    /// </summary>
    internal sealed class NonExecutablePage : IDisposable
    {
        private const uint PAGE_READWRITE = 0x04;
        private const uint FILE_MAP_WRITE = 0x02;
        public const int Size = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFileMapping(IntPtr hFile, IntPtr attributes, uint protect, uint maxSizeHigh, uint maxSizeLow, string name);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr mapping, uint access, uint offsetHigh, uint offsetLow, UIntPtr bytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr address);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr handle);

        private readonly IntPtr mapping;
        private readonly IntPtr view;

        public uint Address => (uint)this.view.ToInt64();

        public NonExecutablePage()
        {
            this.mapping = CreateFileMapping(new IntPtr(-1), IntPtr.Zero, PAGE_READWRITE, 0, Size, null);
            if (this.mapping == IntPtr.Zero)
                throw new InvalidOperationException("CreateFileMapping failed.");
            this.view = MapViewOfFile(this.mapping, FILE_MAP_WRITE, 0, 0, (UIntPtr)Size);
            if (this.view == IntPtr.Zero)
                throw new InvalidOperationException("MapViewOfFile failed.");
        }

        public void Write(uint offset, byte[] data) => Marshal.Copy(data, 0, this.view + (int)offset, data.Length);

        public void Dispose()
        {
            UnmapViewOfFile(this.view);
            CloseHandle(this.mapping);
        }
    }
}
