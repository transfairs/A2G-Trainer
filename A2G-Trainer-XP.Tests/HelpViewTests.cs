using System.ComponentModel;
using System.Linq;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    /// <summary>Tests for HelpView's container constructor.</summary>
    public class HelpViewTests
    {
        [Fact]
        public void ContainerConstructor_AddsItselfToTheContainer() => StaThread.Run(() =>
        {
            using (Container container = new Container())
            using (HelpView view = new HelpView(container))
            {
                Assert.Contains(view, container.Components.Cast<IComponent>());
            }
        });
    }
}
