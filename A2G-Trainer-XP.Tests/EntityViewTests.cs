using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.View;
using Xunit;

namespace A2G_Trainer_XP.Tests
{
    // Overrides the real Process.Start call (StartProcess) so EntityView.LinkLabel_LinkClicked's
    // own logic is still exercised for real, without popping open a real browser.
    internal class TestableClubView : ClubView
    {
        public TestableClubView(ProcessMemory memory, ProcessController controller) : base(memory, controller) { }
        public string LastStartedUrl { get; private set; }
        internal override void StartProcess(string url) => this.LastStartedUrl = url;
    }

    /// <summary>
    /// Tests for EntityView's shared field-clearing, input-validation, and game-attached-check
    /// helpers, driven through a real ClubView (a concrete EntityView subclass) since EntityView
    /// itself has only a protected constructor.
    /// </summary>
    public class EntityViewTests
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private const uint WM_CLOSE = 0x0010;

        private static object InvokePrivate(object target, string methodName, params object[] args) =>
            target.GetType().GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, args);

        [Fact]
        public void OnPropertyChanged_RaisesThePropertyChangedEvent() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                string raised = null;
                trainer.ClubView.PropertyChanged += (s, e) => raised = e.PropertyName;

                InvokePrivate(trainer.ClubView, "OnPropertyChanged", "TestProperty");

                Assert.Equal("TestProperty", raised);
            }
        });

        [Fact]
        public void OnPropertyChanged_WithNoSubscriber_DoesNotThrow() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                Exception thrown = Record.Exception(() => InvokePrivate(trainer.ClubView, "OnPropertyChanged", "TestProperty"));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void ClearAllFields_BlanksEveryInputControlRecursively_ExceptExcluded() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                TextBox textBox = new TextBox { Text = "hello" };
                TextBox excludedTextBox = new TextBox { Text = "keep me" };
                ComboBox comboBox = new ComboBox();
                comboBox.Items.Add("A");
                comboBox.Items.Add("B");
                comboBox.SelectedIndex = 1;
                comboBox.Text = "B";
                CheckBox checkBox = new CheckBox { Checked = true };
                RadioButton radioButton = new RadioButton { Checked = true };
                Panel nested = new Panel();
                TextBox nestedTextBox = new TextBox { Text = "nested" };
                nested.Controls.Add(nestedTextBox);

                Panel root = new Panel();
                root.Controls.Add(textBox);
                root.Controls.Add(excludedTextBox);
                root.Controls.Add(comboBox);
                root.Controls.Add(checkBox);
                root.Controls.Add(radioButton);
                root.Controls.Add(nested);

                InvokePrivate(trainer.ClubView, "ClearAllFields", root, new System.Collections.Generic.HashSet<Control> { excludedTextBox });

                Assert.Equal(string.Empty, textBox.Text);
                Assert.Equal("keep me", excludedTextBox.Text);
                Assert.Equal(-1, comboBox.SelectedIndex);
                Assert.Equal(string.Empty, comboBox.Text);
                Assert.False(checkBox.Checked);
                Assert.False(radioButton.Checked);
                Assert.Equal(string.Empty, nestedTextBox.Text);
            }
        });

        [Fact]
        public void IsGameRunning_WhenAttached_ReturnsTrue() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                trainer.Memory.OpenProcess(System.Diagnostics.Process.GetCurrentProcess().Id);

                Assert.True((bool)InvokePrivate(trainer.ClubView, "IsGameRunning", true));
            }
        });

        [Fact]
        public void IsGameRunning_WhenNotAttachedAndSilent_ReturnsFalseWithoutShowingADialog() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                Assert.False((bool)InvokePrivate(trainer.ClubView, "IsGameRunning", true));
            }
        });

        [Fact]
        public void IsGameRunning_WhenNotAttachedAndNotSilent_ShowsAnErrorDialogThenReturnsFalse() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                bool? result = null;
                Exception threadException = null;
                Thread dialogThread = new Thread(() =>
                {
                    try
                    {
                        result = (bool)InvokePrivate(trainer.ClubView, "IsGameRunning", false);
                    }
                    catch (Exception ex)
                    {
                        threadException = ex;
                    }
                });
                dialogThread.SetApartmentState(ApartmentState.STA);
                dialogThread.Start();

                IntPtr dialogHandle = IntPtr.Zero;
                for (int attempt = 0; attempt < 100 && dialogHandle == IntPtr.Zero; attempt++)
                {
                    Thread.Sleep(50);
                    dialogHandle = FindWindow(null, "Error");
                }

                Assert.NotEqual(IntPtr.Zero, dialogHandle);
                PostMessage(dialogHandle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

                Assert.True(dialogThread.Join(TimeSpan.FromSeconds(5)));
                Assert.Null(threadException);
                Assert.False(result);
            }
        });

        [Theory]
        [InlineData('5', false)]
        [InlineData('a', true)]
        [InlineData('\b', false)] // backspace - a control character, allowed same as a digit
        public void NumericOnlyKeyPress_AllowsDigitsAndControlCharsOnly(char key, bool expectedHandled) => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                KeyPressEventArgs e = new KeyPressEventArgs(key);

                InvokePrivate(trainer.ClubView, "NumericOnly_KeyPress", null, e);

                Assert.Equal(expectedHandled, e.Handled);
            }
        });

        [Theory]
        [InlineData("123", "123")]
        [InlineData("not-a-number", "65535")]
        [InlineData("", "")]
        public void UshortMaxNumberTextChanged_ClampsInvalidInputToMax(string input, string expected) => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                TextBox textBox = new TextBox { Text = input };

                InvokePrivate(trainer.ClubView, "UshortMaxNumber_TextChanged", textBox, EventArgs.Empty);

                Assert.Equal(expected, textBox.Text);
            }
        });

        [Theory]
        [InlineData("123", "123")]
        [InlineData("not-a-number", "2147483647")]
        [InlineData("", "")]
        public void IntMaxNumberTextChanged_ClampsInvalidInputToMax(string input, string expected) => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                TextBox textBox = new TextBox { Text = input };

                InvokePrivate(trainer.ClubView, "IntMaxNumber_TextChanged", textBox, EventArgs.Empty);

                Assert.Equal(expected, textBox.Text);
            }
        });

        [Theory]
        [InlineData("123", "123")]
        [InlineData("not-a-number", "255")]
        [InlineData("", "")]
        public void ByteMax255TextChanged_ClampsInvalidInputToMax(string input, string expected) => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                TextBox textBox = new TextBox { Text = input };

                InvokePrivate(trainer.ClubView, "ByteMax255_TextChanged", textBox, EventArgs.Empty);

                Assert.Equal(expected, textBox.Text);
            }
        });

        [Fact]
        public void LinkLabelLinkClicked_WithNonStringLinkData_DoesNothing() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                LinkLabel label = new LinkLabel();
                LinkLabel.Link link = new LinkLabel.Link { LinkData = 42 };
                LinkLabelLinkClickedEventArgs e = new LinkLabelLinkClickedEventArgs(link);

                Exception thrown = Record.Exception(() => InvokePrivate(trainer.ClubView, "LinkLabel_LinkClicked", label, e));

                Assert.Null(thrown);
            }
        });

        [Fact]
        public void LinkLabelLinkClicked_WithAUrl_StartsThatUrl() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            using (TestableClubView view = new TestableClubView(trainer.Memory, new ProcessController(trainer)))
            {
                LinkLabel label = new LinkLabel();
                LinkLabel.Link link = new LinkLabel.Link { LinkData = "https://example.invalid" };
                LinkLabelLinkClickedEventArgs e = new LinkLabelLinkClickedEventArgs(link);

                InvokePrivate(view, "LinkLabel_LinkClicked", label, e);

                Assert.Equal("https://example.invalid", view.LastStartedUrl);
            }
        });

        // Exercises the real (non-overridden) StartProcess body via a real ClubView. A bogus,
        // nonexistent file name makes Process.Start fail fast with a Win32Exception instead of
        // actually opening anything, so this is safe to run unattended while still covering the
        // real Process.Start call.
        [Fact]
        public void StartProcess_Real_WithNonexistentTarget_ThrowsWithoutOpeningAnApplication() => StaThread.Run(() =>
        {
            using (Trainer trainer = new Trainer())
            {
                Exception thrown = Record.Exception(() => InvokePrivate(trainer.ClubView, "StartProcess", "a2g-trainer-xp-test-nonexistent-target.invalid"));

                Assert.NotNull(thrown);
            }
        });
    }
}
