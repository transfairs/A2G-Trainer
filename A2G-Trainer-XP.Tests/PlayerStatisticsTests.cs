using AnstossTrainer.Shared.Tests;
using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Tests for the season statistics and the buy price ("Kaufpreis"): read from the persistent
    /// record over the display cache, written to both, and bound on the player view.
    /// </summary>
    public class PlayerStatisticsTests
    {
        private const ushort CurrentYear = 2001;

        private static Addresses Own => AddressPresets.OWN_PLAYERS;

        private static Club NewSinglePlayerClub() => new Club { PlayerCount = 1, AmateurPlayerCount = 0 };

        // Display-cache key, record offset and a distinct seed value per statistic.
        private static readonly Tuple<PlayerEnums.AddressKey, uint, byte>[] Statistics =
        {
            Tuple.Create(PlayerEnums.AddressKey.RED_CARDS, Settings.PlayerRecordRedCardsOffset, (byte)1),
            Tuple.Create(PlayerEnums.AddressKey.YELLOW_RED_CARDS, Settings.PlayerRecordYellowRedCardsOffset, (byte)2),
            Tuple.Create(PlayerEnums.AddressKey.GOALS, Settings.PlayerRecordGoalsOffset, (byte)21),
            Tuple.Create(PlayerEnums.AddressKey.OWN_GOALS, Settings.PlayerRecordOwnGoalsOffset, (byte)3),
            Tuple.Create(PlayerEnums.AddressKey.ASSISTS, Settings.PlayerRecordAssistsOffset, (byte)9),
            Tuple.Create(PlayerEnums.AddressKey.JOKER_GOALS, Settings.PlayerRecordJokerGoalsOffset, (byte)4),
            Tuple.Create(PlayerEnums.AddressKey.APPEARANCES, Settings.PlayerRecordAppearancesOffset, (byte)30),
            Tuple.Create(PlayerEnums.AddressKey.SUBBED_IN, Settings.PlayerRecordSubbedInOffset, (byte)6),
            Tuple.Create(PlayerEnums.AddressKey.SUBBED_OUT, Settings.PlayerRecordSubbedOutOffset, (byte)7),
            Tuple.Create(PlayerEnums.AddressKey.UPGRADE_GOALS, Settings.PlayerRecordUpgradeGoalsOffset, (byte)19),
        };

        private static byte[] StatisticValues(Player p) => new[]
        {
            p.RedCardsSeason, p.YellowRedCardsSeason, p.Goals, p.OwnGoals, p.Assists, p.JokerGoals, p.Appearances, p.SubbedIn, p.SubbedOut, p.UpgradeGoals
        };

        private static void SetStatistics(Player p, byte add)
        {
            p.RedCardsSeason = (byte)(1 + add); p.YellowRedCardsSeason = (byte)(2 + add); p.Goals = (byte)(21 + add);
            p.OwnGoals = (byte)(3 + add); p.Assists = (byte)(9 + add); p.JokerGoals = (byte)(4 + add);
            p.Appearances = (byte)(30 + add); p.SubbedIn = (byte)(6 + add); p.SubbedOut = (byte)(7 + add); p.UpgradeGoals = (byte)(19 + add);
        }

        private static FakeModule NewFake()
        {
            FakeModule fake = new FakeModule();
            fake.RoutePointerSlot(Settings.PlayerAddress[0]);
            fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.ID], BitConverter.GetBytes((ushort)0));
            fake.WriteModuleBytes(Settings.AgeReferenceYearOffset, BitConverter.GetBytes(CurrentYear));
            return fake;
        }

        [Fact]
        public void Read_PrefersTheRecordOverTheDisplayCache()
        {
            using (FakeModule fake = NewFake())
            {
                foreach (var stat in Statistics)
                {
                    fake.WriteDisplayCacheBytes(Own[stat.Item1], new byte[] { 99 });
                    fake.WriteModuleBytes(Settings.PlayerRecordTableOffset + stat.Item2, new byte[] { stat.Item3 });
                }
                fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.BUY_PRICE], BitConverter.GetBytes(1u));
                fake.WriteModuleBytes(Settings.PlayerRecordTableOffset + Settings.PlayerRecordBuyPriceOffset, BitConverter.GetBytes(25000u));

                Player player = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN).EntityList.Single();

                Assert.Equal(Statistics.Select(s => s.Item3).ToArray(), StatisticValues(player));
                Assert.Equal(25000u, player.BuyPrice);
            }
        }

        [Fact]
        public void Read_WhenTheStatisticsAreUnreadable_KeepsTheDisplayCacheValues()
        {
            using (FakeModule fake = NewFake())
            {
                foreach (var stat in Statistics)
                    fake.WriteDisplayCacheBytes(Own[stat.Item1], new byte[] { stat.Item3 });
                fake.MakeModuleRegionUnreadable(Settings.PlayerRecordTableOffset + Settings.PlayerRecordRedCardsOffset,
                    (int)(Settings.PlayerRecordUpgradeGoalsOffset - Settings.PlayerRecordRedCardsOffset + 1));

                Player player = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN).EntityList.Single();

                Assert.Equal(Statistics.Select(s => s.Item3).ToArray(), StatisticValues(player));
            }
        }

        [Fact]
        public void Save_WritesRecordAndDisplayCache_AndLeavesTheBytesInBetweenAlone()
        {
            using (FakeModule fake = NewFake())
            {
                uint b = Settings.PlayerRecordTableOffset;
                // Other statistics the Trainer doesn't edit, e.g. the cup goals right after the goals.
                fake.WriteModuleBytes(b + Settings.PlayerRecordGoalsOffset + 1, new byte[] { 0x5A });
                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.OWN);
                Player player = controller.EntityList.Single();
                SetStatistics(player, 0);
                player.BuyPrice = 123456;

                controller.Save(player);

                foreach (var stat in Statistics)
                {
                    Assert.Equal(stat.Item3, fake.ReadModuleBytes(b + stat.Item2, 1)[0]);
                    Assert.Equal(stat.Item3, fake.ReadDisplayCacheBytes(Own[stat.Item1], 1)[0]);
                }
                Assert.Equal((byte)0x5A, fake.ReadModuleBytes(b + Settings.PlayerRecordGoalsOffset + 1, 1)[0]);
                Assert.Equal(123456u, BitConverter.ToUInt32(fake.ReadModuleBytes(b + Settings.PlayerRecordBuyPriceOffset, 4), 0));
                Assert.Equal(123456u, BitConverter.ToUInt32(fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.BUY_PRICE], 4), 0));
            }
        }

        [Fact]
        public void Save_ForTrainee_WritesTheStatisticsToTheRecordOnly()
        {
            AddressPresets.InitDynamicTeam("0");

            using (FakeModule fake = NewFake())
            {
                fake.WriteDisplayCacheBytes(Own[PlayerEnums.AddressKey.GOALS], new byte[] { 0x11 });
                PlayerController controller = new PlayerController(fake.Memory, NewSinglePlayerClub(), isGog: false, PlayerEnums.AddressType.TRAINEE);
                Player player = controller.EntityList.Single();
                SetStatistics(player, 10);

                controller.Save(player);

                uint b = Settings.PlayerRecordTableOffset;
                Assert.Equal((byte)31, fake.ReadModuleBytes(b + Settings.PlayerRecordGoalsOffset, 1)[0]);
                Assert.Equal((byte)40, fake.ReadModuleBytes(b + Settings.PlayerRecordAppearancesOffset, 1)[0]);
                Assert.Equal((byte)0x11, fake.ReadDisplayCacheBytes(Own[PlayerEnums.AddressKey.GOALS], 1)[0]);
            }
        }

        [Fact]
        public void StatisticProperties_RaisePropertyChanged()
        {
            Player player = new Player();
            var changed = new System.Collections.Generic.List<string>();
            player.PropertyChanged += (s, e) => changed.Add(e.PropertyName);

            SetStatistics(player, 1);
            player.BuyPrice = 5;

            Assert.Equal(new[]
            {
                nameof(Player.RedCardsSeason), nameof(Player.YellowRedCardsSeason), nameof(Player.Goals), nameof(Player.OwnGoals),
                nameof(Player.Assists), nameof(Player.JokerGoals), nameof(Player.Appearances), nameof(Player.SubbedIn),
                nameof(Player.SubbedOut), nameof(Player.UpgradeGoals), nameof(Player.BuyPrice)
            }, changed);
        }

        [Fact]
        public void IsBuyOptionOnHold_TogglesOnlyBitTwo()
        {
            Player player = new Player { ContractDetails = PlayerEnums.Contract.Leased | PlayerEnums.Contract.BuyOption };

            player.IsBuyOptionOnHold = true;
            Assert.Equal((PlayerEnums.Contract)0x07, player.ContractDetails);
            Assert.True(player.IsBuyOptionOnHold);

            player.IsBuyOptionOnHold = false;
            Assert.Equal(PlayerEnums.Contract.Leased | PlayerEnums.Contract.BuyOption, player.ContractDetails);
        }

        // Both used to toggle ScheissTrainer instead of their own bit.
        [Fact]
        public void IsVerhandlungGeplatztAndIsScheissManager_ToggleTheirOwnBits()
        {
            Player player = new Player();

            player.IsVerhandlungGeplatzt = true;
            Assert.Equal(PlayerEnums.Unhappy.VerhandlungGeplatzt, player.Unhappy);

            player.IsVerhandlungGeplatzt = false;
            player.IsScheissManager = true;
            Assert.Equal(PlayerEnums.Unhappy.ScheissManager, player.Unhappy);
        }

        private static T GetPrivateField<T>(object target, string fieldName) =>
            (T)target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        [Fact]
        public void PlayerView_BindsTheStatisticsAndTheBuyPrice() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (FakeModule fake = new FakeModule(memory: trainer.Memory))
            {
                fake.RoutePointerSlot(Settings.ClubAddress[0]);
                fake.WriteDisplayCacheBytes(AddressPresets.OWN_CLUB[ClubEnums.AddressKey.PLAYER_COUNT], new byte[] { 1 });
                trainer.PlayerView.RefreshPlayerListView(PlayerEnums.AddressType.OWN);
                trainer.PlayerView.InitMainTabControl();

                Assert.Equal(nameof(Player.Goals), GetPrivateField<TextBox>(trainer.PlayerView, "GoalsInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Player.SubbedOut), GetPrivateField<TextBox>(trainer.PlayerView, "SubbedOutInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Player.UpgradeGoals), GetPrivateField<TextBox>(trainer.PlayerView, "UpgradeGoalsInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Player.BuyPrice), GetPrivateField<TextBox>(trainer.PlayerView, "BuyPriceInput").DataBindings["Text"].BindingMemberInfo.BindingField);
                Assert.Equal(nameof(Player.IsBuyOptionOnHold), GetPrivateField<CheckBox>(trainer.PlayerView, "BuyOptionOnHold").DataBindings["Checked"].BindingMemberInfo.BindingField);
            }
        });
    }
}
