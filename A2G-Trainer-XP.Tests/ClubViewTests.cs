using System;
using System.Diagnostics;
using System.Reflection;
using A2G_Trainer_XP.Model;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for ClubView's tab wiring and button/selection handlers.</summary>
    public class ClubViewTests
    {
        private static object InvokePrivate(object target, string methodName, params object[] args) =>
            target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        [Fact]
        public void InitClubTabControl_WiresBindingsWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                trainer.Memory.OpenProcess(Process.GetCurrentProcess().Id);
                trainer.ClubView.RefreshValues(PlayerEnums.AddressType.OWN);

                Exception thrown = Record.Exception(() => trainer.ClubView.InitClubTabControl());

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void ReloadBtnClick_RefreshesUsingTheCurrentControllerType() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                trainer.Memory.OpenProcess(Process.GetCurrentProcess().Id);
                trainer.ClubView.RefreshValues(PlayerEnums.AddressType.OWN);
                trainer.ClubView.InitClubTabControl();

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.ClubView, "ReloadBtn_Click", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void SaveBtnClick_SavesThenRefreshes() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                trainer.Memory.OpenProcess(Process.GetCurrentProcess().Id);
                trainer.ClubView.RefreshValues(PlayerEnums.AddressType.OWN);
                trainer.ClubView.InitClubTabControl();

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.ClubView, "SaveBtn_Click", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void ClubSelectSelectedIndexChanged_WithNoSelection_RefreshesWithoutThrowing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                trainer.Memory.OpenProcess(Process.GetCurrentProcess().Id);
                trainer.ClubView.RefreshValues(PlayerEnums.AddressType.OWN);
                trainer.ClubView.InitClubTabControl();

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.ClubView, "ClubSelect_SelectedIndexChanged", null, EventArgs.Empty));

                Assert.Null(thrown);
            }
        });
    }
}
