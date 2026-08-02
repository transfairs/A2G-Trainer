using System.Collections.Generic;
using System.Linq;
using System.Text;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for CoachController.GetActiveTrainers' live-count/Firstname-heuristic fallback.</summary>
    public class CoachControllerLiveCountTests
    {
        private static void WriteCoachFirstname(FakeModule fake, int trainerSlot, string name)
        {
            const int trainerSlotStride = 0x1958;
            uint fieldOffset = 0x34B00 + (uint)(trainerSlot * trainerSlotStride);
            fake.WriteDisplayCacheBytes(fieldOffset.ToString("X"), Encoding.GetEncoding("iso-8859-1").GetBytes(name));
        }

        [Fact]
        public void GetActiveTrainers_WithPlausibleLiveCount_IgnoresEmptyFirstnames()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteModuleBytes(Settings.ActiveTrainerCountOffset, new byte[] { 2 });

                List<KeyValuePair<int, Coach>> trainers = CoachController.GetActiveTrainers(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);

                Assert.Equal(new[] { 0, 1 }, trainers.Select(kv => kv.Key));
            }
        }

        [Fact]
        public void GetActiveTrainers_WithImplausibleLiveCount_FallsBackToFirstnameHeuristic()
        {
            using (FakeModule fake = new FakeModule())
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteModuleBytes(Settings.ActiveTrainerCountOffset, new byte[] { 250 });
                WriteCoachFirstname(fake, trainerSlot: 0, "Robin");

                List<KeyValuePair<int, Coach>> trainers = CoachController.GetActiveTrainers(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);

                Assert.Equal(new[] { 0 }, trainers.Select(kv => kv.Key));
                Assert.Equal("Robin", trainers[0].Value.Firstname);
            }
        }
    }
}
