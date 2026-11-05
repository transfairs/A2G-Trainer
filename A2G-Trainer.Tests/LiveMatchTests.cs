using System;
using System.Linq;
using A2G_Trainer.Controller;
using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for the running match (LiveMatch) and the club's locks (bribery, short camp, sponsor, scouts).</summary>
    public class LiveMatchTests
    {
        private const PlayerEnums.Country Country = ClubEditorSettingsTests.Country;
        private const byte OwnId = ClubEditorSettingsTests.OwnId;
        private const uint OtherCopy = ClubEditorSettingsTests.OtherCopy;
        // The match table and clock lie past the default module block (0x82E460/0xC24E10); the tests
        // put them in its free end instead (a bigger module costs the x86 test host too much).
        private const uint Table = 0x51A000;
        private const uint Clock = 0x51F000;

        private static LiveMatch Find(FakeModule fake, byte clubId = OwnId) =>
            LiveMatch.Find(fake.Memory, FixedAddresses.For(fake.Memory), Country, clubId, fake.ModuleBase + Table, fake.ModuleBase + Clock);

        // Two matches; the second is the own club's away match in minute 63, 1:2, with a human (mask 2).
        internal static void SeedMatch(FakeModule fake, short substitutions = 1)
        {
            fake.WriteModuleBytes(Clock, new byte[] { 0, 0, 2, 0, 0, 0, 0, 0, 63, 0 });
            uint match = Table + Settings.MatchStride;
            fake.WriteModuleBytes(match, new byte[] { (byte)Country, 0, 2, 0, (byte)Country, 0, OwnId, 0 });
            fake.WriteModuleBytes(match + 0xE, new byte[] { 1, 0, 2, 0 });
            fake.WriteModuleBytes(match + 0x240, new byte[] { 2, 0 });
            fake.WriteModuleBytes(match + 0x16C, new byte[] { 3, 2, 4, 0, (byte)substitutions });
            // Line-up 101..111, bench 120, 121 and empty places.
            byte[] lineUp = Enumerable.Range(101, 11).SelectMany(id => BitConverter.GetBytes((ushort)id)).ToArray();
            byte[] bench = { 120, 0, 121, 0 };
            fake.WriteModuleBytes(OtherCopy + Settings.ClubLineUpOffset, lineUp);
            fake.WriteModuleBytes(OtherCopy + Settings.ClubBenchOffset, bench);
            fake.WriteDisplayCacheBytes(Settings.ClubLineUpOffset.ToString("X"), lineUp);
            fake.WriteDisplayCacheBytes(Settings.ClubBenchOffset.ToString("X"), bench);
        }

        [Fact]
        public void Find_TakesTheClubsMatchWithAHumanTeam_AndReadsIt()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                Assert.Null(Find(fake));
                SeedMatch(fake);

                LiveMatch match = Find(fake);

                Assert.NotNull(match);
                Assert.False(match.IsHome);
                Assert.Equal((short)63, match.Minute);
                Assert.Equal((short)1, match.HomeGoals);
                Assert.Equal((short)2, match.AwayGoals);
                Assert.Equal(Country, match.Opponent.Key);
                Assert.Equal((ushort)2, match.Opponent.Value);
                Assert.Equal((byte)3, match.Attitude);
                Assert.Equal((byte)2, match.Hardness);
                Assert.Equal((byte)4, match.Effort);
                Assert.Equal((byte)1, match.Substitutions);
                Assert.Equal(new ushort[] { 120, 121, 0, 0, 0, 0, 0 }, match.Bench);
                Assert.Equal((ushort)101, match.LineUp[0]);
                // The opponent's match without a human of its own isn't found for it.
                Assert.Null(Find(fake, 0));
                Assert.Null(LiveMatch.Find(fake.Memory, null, Country, OwnId));
            }
        }

        [Fact]
        public void Bench_WithoutAWorkingCopy_ComesFromTheRecord()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                SeedMatch(fake);
                // Before kick-off the game's club cache holds no copy of the club.
                fake.WriteModuleBytes(Settings.ClubCacheTableOffset, new byte[Settings.ClubCacheSlots * Settings.ClubCacheStride]);
                uint record = ClubEditorSettingsTests.Record(OwnId);
                fake.WriteModuleBytes(record + 0x148, BitConverter.GetBytes((ushort)105));
                fake.WriteModuleBytes(record + 0x15E, new byte[] { 122, 0, 0, 0, 123, 0 });

                LiveMatch match = Find(fake);

                Assert.Equal(new ushort[] { 122, 0, 123, 0, 0, 0, 0 }, match.Bench);
                Assert.Equal((ushort)105, match.LineUp[0]);
            }
        }

        [Fact]
        public void Writes_ReachTheMatchAndEveryCopyOfTheBench()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                SeedMatch(fake, substitutions: 3);
                LiveMatch match = Find(fake);
                uint at = Table + Settings.MatchStride;

                match.Substitutions = 9;
                Assert.Equal((byte)3, match.Substitutions);
                match.Substitutions = 0;
                match.Attitude = 6;
                match.Hardness = 4;
                match.Effort = 9;
                match.WriteBench(new ushort[] { 130, 0, 131 });

                Assert.Equal(new byte[] { 6, 4, 4, 0, 0 }, fake.ReadModuleBytes(at + 0x16C, 5));
                // The computer must leave the away team's tactics alone now.
                Assert.Equal((byte)(2 | 8), fake.ReadModuleBytes(at + 0x240, 1)[0]);
                byte[] bench = new byte[14];
                bench[0] = 130;
                bench[4] = 131;
                Assert.Equal(bench, fake.ReadModuleBytes(OtherCopy + Settings.ClubBenchOffset, 14));
                Assert.Equal(bench, fake.ReadModuleBytes(ClubEditorSettingsTests.Record(OwnId) + 0x15E, 14));
            }
        }

        [Fact]
        public void BenchCandidates_LeaveOutTheLineUpAndPlayersSentOff()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                SeedMatch(fake);
                // Working copies in the cache: 140 sent off (0x21), 142 substituted (5), 143 substituted
                // after a booking (0x0D), 144 played without going off (1).
                (ushort Id, byte Flags)[] players = { (140, 0x21), (142, 0x05), (143, 0x0D), (144, 0x01) };
                for (int i = 0; i < players.Length; i++)
                {
                    uint copy = Settings.PlayerCacheTableOffset + (uint)i * Settings.PlayerCacheStride;
                    fake.WriteModuleBytes(copy, new byte[] { 1 });
                    fake.WriteModuleBytes(copy + Settings.PlayerCacheCopyOffset, BitConverter.GetBytes(players[i].Id));
                    fake.WriteModuleBytes(copy + Settings.PlayerCacheCopyOffset + Settings.PlayerMatchFlagsOffset, new[] { players[i].Flags });
                }
                LiveMatch match = Find(fake);

                Assert.Equal(new ushort[] { 120, 141, 142, 143 }, match.BenchCandidates(new ushort[] { 101, 120, 140, 141, 142, 143, 144, 0 }));
            }
        }

        private static T Field<T>(object target, string name) =>
            (T)target.GetType().GetField(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(target);

        // A club view of its own (no main window: each one costs the x86 test host ~20 MB).
        [Fact]
        public void LiveTab_ShowsTheMatch_AndWritesOnlyWhatWasChanged() => AnstossTrainer.Shared.Tests.StaThread.Run(() =>
        {
            using (FakeModule fake = new FakeModule())
            using (A2G_Trainer.View.ClubView view = new A2G_Trainer.View.ClubView(fake.Memory, null))
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                SeedMatch(fake);
                typeof(A2G_Trainer.View.ClubView).GetField("club", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .SetValue(view, new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN).Club);
                System.Reflection.MethodInfo show = typeof(A2G_Trainer.View.ClubView).GetMethod("ShowLiveMatch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new[] { typeof(LiveMatch) }, null);
                System.Reflection.MethodInfo apply = typeof(A2G_Trainer.View.ClubView).GetMethod("ApplyLiveMatch", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                show.Invoke(view, new object[] { Find(fake) });

                Assert.StartsWith("63. Minute, 1:2 bei", Field<System.Windows.Forms.Label>(view, "liveStatusValue").Text);
                Assert.True(Field<System.Windows.Forms.GroupBox>(view, "liveTacticsBox").Enabled);
                uint match = Table + Settings.MatchStride;

                // Nothing changed: nothing written, even if the match went on meanwhile.
                fake.WriteModuleBytes(match + 0x170, new byte[] { 2 });
                apply.Invoke(view, null);
                Assert.Equal((byte)2, fake.ReadModuleBytes(match + 0x170, 1)[0]);

                Field<System.Windows.Forms.ComboBox>(view, "liveAttitudeInput").SelectedIndex = 6;
                apply.Invoke(view, null);
                Assert.Equal((byte)6, fake.ReadModuleBytes(match + 0x16C, 1)[0]);
                Assert.Equal((byte)2, fake.ReadModuleBytes(match + 0x170, 1)[0]);

                // The places show the players, not their IDs.
                System.Windows.Forms.ComboBox place = Field<System.Windows.Forms.ComboBox[]>(view, "liveBenchInputs")[0];
                Assert.Equal(((NationalSquads.Member)place.SelectedItem).ToString(), place.GetItemText(place.SelectedItem));
                Assert.NotEqual("120", place.GetItemText(place.SelectedItem));
                // A player on another place isn't offered; given up, he is again.
                Func<System.Windows.Forms.ComboBox, ushort[]> offered = c => c.Items.Cast<NationalSquads.Member>().Select(m => m.Id).ToArray();
                Assert.DoesNotContain((ushort)121, offered(place));
                Assert.Contains((ushort)120, offered(place));

                Field<System.Windows.Forms.TextBox>(view, "liveSubstitutionsInput").Text = "0";
                Field<System.Windows.Forms.ComboBox[]>(view, "liveBenchInputs")[1].SelectedIndex = 0;
                Assert.Contains((ushort)121, offered(place));
                Assert.Equal((ushort)120, place.SelectedValue);
                apply.Invoke(view, null);
                Assert.Equal((byte)0, fake.ReadModuleBytes(match + 0x170, 1)[0]);
                Assert.Equal((byte)0, fake.ReadModuleBytes(OtherCopy + Settings.ClubBenchOffset + 2, 1)[0]);

                // Without a running match the tab is disabled.
                fake.WriteModuleBytes(Clock + 2, new byte[] { 0, 0 });
                show.Invoke(view, new object[] { Find(fake) });
                Assert.Equal("Kein laufendes Spiel", Field<System.Windows.Forms.Label>(view, "liveStatusValue").Text);
                apply.Invoke(view, null);
            }
        });

        [Fact]
        public void ClubLocks_AreReadAndWritten()
        {
            using (FakeModule fake = new FakeModule())
            {
                ClubEditorSettingsTests.SeedOwnClub(fake);
                fake.WriteDisplayCacheBytes("14A", new byte[] { 0xB4 });
                fake.WriteDisplayCacheBytes("1074", BitConverter.GetBytes(0x0900u));
                fake.WriteDisplayCacheBytes("FC", new byte[] { 2 });
                fake.WriteDisplayCacheBytes("2FC", BitConverter.GetBytes(2500000));
                ClubController controller = new ClubController(fake.Memory, isGog: false, PlayerEnums.AddressType.OWN);
                Club club = controller.Club;

                Assert.Equal((byte)0xB4, club.Bribery);
                Assert.Equal(0x900u, club.ShortCamp);
                Assert.Equal((byte)1, club.ShortCampsThisSeason);
                Assert.Equal((byte)2, club.SponsorRequests);
                Assert.Equal(2500000, club.ScoutSpending);

                club.Bribery = 0x04;
                club.ShortCampsThisSeason = 7;
                club.SponsorRequests = 0;
                club.ScoutSpending = 0;
                controller.Save();

                Assert.Equal((byte)0x04, fake.ReadDisplayCacheBytes("14A", 1)[0]);
                Assert.Equal((byte)0x04, fake.ReadModuleBytes(ClubEditorSettingsTests.Record(OwnId) + 0x13E, 1)[0]);
                Assert.Equal(0xA00u, BitConverter.ToUInt32(fake.ReadDisplayCacheBytes("1074", 4), 0));
                Assert.Equal(0xA00u, BitConverter.ToUInt32(fake.ReadModuleBytes(ClubEditorSettingsTests.Record(OwnId) + 0x6FC, 4), 0));
                Assert.Equal((byte)0, fake.ReadDisplayCacheBytes("FC", 1)[0]);
                Assert.Equal(0, BitConverter.ToInt32(fake.ReadDisplayCacheBytes("2FC", 4), 0));
            }
        }
    }
}
