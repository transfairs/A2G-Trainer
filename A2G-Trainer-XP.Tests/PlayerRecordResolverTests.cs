using System;
using System.Diagnostics;
using A2G_Trainer_XP.Controller;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for PlayerRecordResolver's persistent-record address arithmetic.</summary>
    public class PlayerRecordResolverTests
    {
        [Fact]
        public void GetFieldAddress_WithoutOpenProcess_OmitsModuleBase()
        {
            ProcessMemory memory = new ProcessMemory();
            PlayerRecordResolver resolver = new PlayerRecordResolver(memory);

            uint expected = Settings.PlayerRecordTableOffset + Settings.PlayerRecordLevelOffset;
            Assert.Equal(expected, resolver.GetFieldAddress(playerId: 0, Settings.PlayerRecordLevelOffset));
        }

        [Theory]
        [InlineData((ushort)0)]
        [InlineData((ushort)1)]
        [InlineData((ushort)500)]
        [InlineData(ushort.MaxValue)]
        public void GetFieldAddress_ScalesByPlayerIdTimesStride(ushort playerId)
        {
            ProcessMemory memory = new ProcessMemory();
            PlayerRecordResolver resolver = new PlayerRecordResolver(memory);

            uint expected = Settings.PlayerRecordTableOffset + (uint)playerId * Settings.PlayerRecordStride + Settings.PlayerRecordSalaryOffset;
            Assert.Equal(expected, resolver.GetFieldAddress(playerId, Settings.PlayerRecordSalaryOffset));
        }

        [Fact]
        public void GetFieldAddress_AddsFieldOffsetOnTopOfTheRecordBase()
        {
            ProcessMemory memory = new ProcessMemory();
            PlayerRecordResolver resolver = new PlayerRecordResolver(memory);

            uint recordBase = resolver.GetFieldAddress(playerId: 42, fieldOffset: 0);

            Assert.Equal(recordBase + 0x10u, resolver.GetFieldAddress(playerId: 42, fieldOffset: 0x10));
        }

        [Fact]
        public void GetFieldAddress_WithLiveProcess_IncludesRealModuleBase()
        {
            Assert.Equal(4, IntPtr.Size);

            ProcessMemory memory = new ProcessMemory();
            Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));
            PlayerRecordResolver resolver = new PlayerRecordResolver(memory);

            uint expected = memory.ModuleBase + Settings.PlayerRecordTableOffset + 3u * Settings.PlayerRecordStride + Settings.PlayerRecordAgeOffset;
            Assert.Equal(expected, resolver.GetFieldAddress(playerId: 3, Settings.PlayerRecordAgeOffset));
        }
    }
}
