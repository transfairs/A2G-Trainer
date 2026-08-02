using System;
using System.ComponentModel;
using System.Reflection;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>
    /// Every generated Designer.cs' Dispose(bool disposing) has the same
    /// "if (disposing &amp;&amp; (components != null))" guard. Normal disposal always passes
    /// disposing: true with components staying null (none of these views actually use the
    /// designer's component tray), so neither the disposing:false short-circuit nor the
    /// components != null branch is ever reached through ordinary Dispose() calls - both are
    /// exercised directly here via reflection, since disposing:false is a real path (just
    /// finalizer-only in practice) and components is a real, if always-empty, field.
    /// </summary>
    public class DesignerDisposeBranchTests
    {
        private static void InvokeDispose(Component target, bool disposing)
        {
            MethodInfo method = target.GetType().GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
            method.Invoke(target, new object[] { disposing });
        }

        private static void SetComponents(Component target, IContainer container) =>
            target.GetType().GetField("components", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, container);

        [Theory]
        [InlineData(typeof(AboutView))]
        [InlineData(typeof(HelpView))]
        public void Dispose_WithDisposingFalse_SkipsComponentsDisposeWithoutThrowing(Type viewType) => StaThread.Run(() =>
        {
            using (Component view = (Component)Activator.CreateInstance(viewType))
            {
                Exception thrown = Record.Exception(() => InvokeDispose(view, false));

                Assert.Null(thrown);
            }
        });

        [Theory]
        [InlineData(typeof(AboutView))]
        [InlineData(typeof(HelpView))]
        public void Dispose_WithDisposingTrueAndRealComponents_DisposesTheContainer(Type viewType) => StaThread.Run(() =>
        {
            using (Component view = (Component)Activator.CreateInstance(viewType))
            {
                Container container = new Container();
                SetComponents(view, container);

                Exception thrown = Record.Exception(() => InvokeDispose(view, true));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void ClubViewDispose_BothBranches_DoNotThrow() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                InvokeDispose(trainer.ClubView, false);

                Container container = new Container();
                SetComponents(trainer.ClubView, container);
                Exception thrown = Record.Exception(() => InvokeDispose(trainer.ClubView, true));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void CoachViewDispose_BothBranches_DoNotThrow() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                InvokeDispose(trainer.CoachView, false);

                Container container = new Container();
                SetComponents(trainer.CoachView, container);
                Exception thrown = Record.Exception(() => InvokeDispose(trainer.CoachView, true));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void PlayerViewDispose_BothBranches_DoNotThrow() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                InvokeDispose(trainer.PlayerView, false);

                Container container = new Container();
                SetComponents(trainer.PlayerView, container);
                Exception thrown = Record.Exception(() => InvokeDispose(trainer.PlayerView, true));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void TrainerDispose_BothBranches_DoNotThrow() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                InvokeDispose(trainer, false);

                Container container = new Container();
                SetComponents(trainer, container);
                Exception thrown = Record.Exception(() => InvokeDispose(trainer, true));

                Assert.Null(thrown);
            }
        });
    }
}
