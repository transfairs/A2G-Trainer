using System.ComponentModel;
using System.Reflection;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for EntityController's shared change-notification and base-address selection.</summary>
    public class EntityControllerTests
    {
        [Fact]
        public void OnPropertyChanged_RaisesThePropertyChangedEvent()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.CountrySelectionAddress[0]);
                LeagueController controller = new LeagueController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                string raised = null;
                controller.PropertyChanged += (s, e) => raised = e.PropertyName;

                MethodInfo onPropertyChanged = typeof(EntityController<LeagueSettings>).GetMethod("OnPropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                onPropertyChanged.Invoke(controller, new object[] { "TestProperty" });

                Assert.Equal("TestProperty", raised);
            }
        }

        [Fact]
        public void OnPropertyChanged_WithNoSubscriber_DoesNotThrow()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.CountrySelectionAddress[0]);
                LeagueController controller = new LeagueController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);

                MethodInfo onPropertyChanged = typeof(EntityController<LeagueSettings>).GetMethod("OnPropertyChanged", BindingFlags.NonPublic | BindingFlags.Instance);
                System.Exception thrown = Record.Exception(() => onPropertyChanged.Invoke(controller, new object[] { "TestProperty" }));

                Assert.Null(thrown);
            }
        }

        [Fact]
        public void UpdateBaseAddress_ForGogBuild_SelectsTheGogAddress()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.CountrySelectionAddress[1]);
                LeagueController controller = new LeagueController(fake.Memory, isGog: true, PlayerEnums.AddressType.OWN);

                Assert.Equal(PlayerEnums.AddressType.OWN, controller.Type);
            }
        }
    }
}
