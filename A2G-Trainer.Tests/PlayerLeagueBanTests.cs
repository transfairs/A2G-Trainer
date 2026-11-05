using A2G_Trainer.Model;
using Xunit;

namespace A2G_Trainer.Tests
{
    /// <summary>Tests for turning the edited ban count into the game's league-ban bytes (Player.ApplyLeagueBanChange).</summary>
    public class PlayerLeagueBanTests
    {
        private static Player Banned(byte state, byte reason, ushort endDay, byte nextMatch)
        {
            Player player = new Player();
            player.SetLeagueBan(state, reason, endDay, nextMatch);
            return player;
        }

        [Fact]
        public void SetLeagueBan_ShowsOnlyTheMatchesLeft()
        {
            Player player = Banned(0x83, 0x80, 40, 0x83);

            Assert.Equal((byte)3, player.RedCardBannedMatches);
        }

        [Fact]
        public void UnchangedCount_ChangesNothing()
        {
            Player player = Banned(0x83, 0x80, 40, 0x83);

            Assert.False(player.ApplyLeagueBanChange(10));
            Assert.Equal((byte)0x83, player.LeagueBanState);
            Assert.Equal((ushort)40, player.LeagueBanEndDay);
        }

        [Fact]
        public void NewBan_IsARedCardEndingAfterItsMatches()
        {
            Player player = Banned(0, 0, 0, 0);
            player.RedCardBannedMatches = 4;

            Assert.True(player.ApplyLeagueBanChange(10));
            Assert.Equal((byte)4, player.LeagueBanState);
            Assert.Equal(Player.LeagueBanRedCard, player.LeagueBanReason);
            Assert.Equal((ushort)(10 + 4 * 7 + 1), player.LeagueBanEndDay);
            Assert.Equal((byte)4, player.NextMatchBan);
        }

        [Fact]
        public void NewRedCardBan_WithoutSeasonDay_ChangesNothing()
        {
            Player player = Banned(0, 0, 0, 0);
            player.RedCardBannedMatches = 4;

            Assert.False(player.ApplyLeagueBanChange(null));
            Assert.Equal((byte)0, player.LeagueBanState);
            Assert.Equal((byte)0, player.LeagueBanReason);
        }

        [Fact]
        public void ZeroMatches_ClearsEverything()
        {
            Player player = Banned(0x83, 0x80, 40, 0x83);
            player.RedCardBannedMatches = 0;

            Assert.True(player.ApplyLeagueBanChange(null));
            Assert.Equal((byte)0, player.LeagueBanState);
            Assert.Equal((byte)0, player.LeagueBanReason);
            Assert.Equal((ushort)0, player.LeagueBanEndDay);
            Assert.Equal((byte)0, player.NextMatchBan);
        }

        [Fact]
        public void ChangedRedCardBan_KeepsFlagBitsAndReasonAndMovesTheEndDay()
        {
            Player player = Banned(0x83, 0x85, 40, 0x83);
            player.RedCardBannedMatches = 6;

            Assert.True(player.ApplyLeagueBanChange(10));
            Assert.Equal((byte)0x86, player.LeagueBanState);
            Assert.Equal((byte)0x85, player.LeagueBanReason);
            Assert.Equal((ushort)(10 + 6 * 7 + 1), player.LeagueBanEndDay);
            Assert.Equal((byte)0x86, player.NextMatchBan);
        }

        [Fact]
        public void ChangedYellowCardBan_KeepsReasonAndEndDay_EvenWithoutSeasonDay()
        {
            Player player = Banned(0x01, 0x20, 0, 0x01);
            player.RedCardBannedMatches = 2;

            Assert.True(player.ApplyLeagueBanChange(null));
            Assert.Equal((byte)0x02, player.LeagueBanState);
            Assert.Equal((byte)0x20, player.LeagueBanReason);
            Assert.Equal((ushort)0, player.LeagueBanEndDay);
        }

        [Fact]
        public void NextMatchBanOfAnotherCompetition_Stays()
        {
            Player player = Banned(0, 0, 0, 0x40);
            player.RedCardBannedMatches = 2;

            Assert.True(player.ApplyLeagueBanChange(10));
            Assert.Equal((byte)0x40, player.NextMatchBan);
        }

        [Fact]
        public void CountAbove31_IsCappedAt31()
        {
            Player player = Banned(0, 0, 0, 0);
            player.RedCardBannedMatches = 40;

            Assert.True(player.ApplyLeagueBanChange(10));
            Assert.Equal((byte)31, player.LeagueBanState);
            Assert.Equal((byte)31, player.RedCardBannedMatches);
        }
    }
}
