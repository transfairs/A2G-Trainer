using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    /// <summary>Base class for the tab views (Club/Coach/Player), providing shared field-clearing, input validation, and game-attached checks.</summary>
    public partial class EntityView : UserControl
    {
        #region INotifyPropertyChanged
        /// <summary>Raised whenever a bound property on this view changes.</summary>
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
        protected BindingSource bindingSource;
        internal ProcessMemory Memory { get => this.memory; private set => this.memory = value; }
        private ProcessMemory memory;

        /// <summary>Controller currently backing this view's club data, if any.</summary>
        public ClubController ClubController { get => this.clubController; private set { } }
        protected ClubController clubController;
        /// <summary>Controller currently backing this view's coach data, if any.</summary>
        public CoachController CoachController { get => this.coachController; private set { } }
        protected CoachController coachController;
        /// <summary>Controller currently backing this view's player data, if any.</summary>
        public PlayerController PlayerController { get => this.playerController; private set { } }
        protected PlayerController playerController;
        protected ProcessController processController;

        protected EntityView(ProcessMemory memory, ProcessController processController)
        {
            this.memory = memory;
            this.processController = processController;
        }

        /// <summary>Recursively blanks every input control under <paramref name="parent"/>, except those in <paramref name="exclude"/>.</summary>
        protected void ClearAllFields(Control parent, ISet<Control> exclude = null)
        {
            foreach (Control ctrl in parent.Controls)
            {
                if (exclude != null && exclude.Contains(ctrl))
                    continue;

                if (ctrl is TextBox tb)
                    tb.Clear();
                else if (ctrl is ComboBox cb)
                {
                    cb.SelectedIndex = -1;
                    // For an editable (DropDown-style) combo, SelectedIndex alone doesn't blank the
                    // visible text - only a DropDownList combo auto-clears its text that way.
                    cb.Text = string.Empty;
                }
                else if (ctrl is CheckBox chk)
                    chk.Checked = false;
                else if (ctrl is RadioButton rb)
                    rb.Checked = false;
                else if (ctrl.HasChildren)
                    ClearAllFields(ctrl, exclude);
            }
        }
        /// <summary>Whether the game process is currently attached; optionally shows an error dialog if not.</summary>
        protected bool IsGameRunning(bool silent = false)
        {
            if (this.memory.mProc.Process == null)
            {
                if(!silent)
                    System.Windows.Forms.MessageBox.Show(this, "Anstoss-2-Gold-Prozess nicht gefunden", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }

            return true;
        }

        protected void NumericOnly_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar))
            {
                e.Handled = true;
            }
        }
        protected void UshortMaxNumber_TextChanged(object sender, EventArgs e)
        {
            var textBox = (TextBox)sender;
            if (!string.IsNullOrEmpty(textBox.Text) && !ushort.TryParse(textBox.Text, out _))
            {
                textBox.Text = "65535";
                textBox.SelectionStart = textBox.Text.Length;
            }
        }
        protected void IntMaxNumber_TextChanged(object sender, EventArgs e)
        {
            var textBox = (TextBox)sender;
            if (!string.IsNullOrEmpty(textBox.Text) && !int.TryParse(textBox.Text, out _))
            {
                textBox.Text = "2147483647";
                textBox.SelectionStart = textBox.Text.Length;
            }
        }
        protected void ByteMax255_TextChanged(object sender, EventArgs e)
        {
            var textBox = (TextBox)sender;
            if (!string.IsNullOrEmpty(textBox.Text) && !byte.TryParse(textBox.Text, out _))
            {
                textBox.Text = "255";
                textBox.SelectionStart = textBox.Text.Length;
            }
        }
        protected void LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = e.Link.LinkData as string;
            if (!string.IsNullOrEmpty(url))
            {
                this.StartProcess(url);
            }
        }

        // Extracted so tests can override the actual OS process launch (which would otherwise pop
        // open a real browser) while still exercising LinkLabel_LinkClicked's own logic for real.
        internal virtual void StartProcess(string url) => Process.Start(url);
    }
}
