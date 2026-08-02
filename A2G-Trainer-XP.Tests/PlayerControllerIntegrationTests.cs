using System;
using System.Diagnostics;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Integration tests for PlayerController against a real (self-attached) process.</summary>
    public class PlayerControllerIntegrationTests
    {
        [Fact]
        public void GetEntity_WithSelfAttachedProcess_ReadsPersistentFieldsWithoutThrowing()
        {
            Assert.Equal(4, IntPtr.Size);

            ProcessMemory memory = new ProcessMemory();
            Assert.True(memory.OpenProcess(Process.GetCurrentProcess().Id));

            Club club = new Club { PlayerCount = 1, AmateurPlayerCount = 0 };

            PlayerController controller = new PlayerController(memory, club, isGog: false, PlayerEnums.AddressType.OWN);

            Assert.Single(controller.EntityList);
            Assert.NotNull(controller.EntityList[0]);
        }
    }
}
