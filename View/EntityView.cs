using A2G_Trainer_XP.Controller;
using A2G_Trainer_XP.Model;
using Memory;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    public partial class EntityView : UserControl
    {
        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        #endregion
        protected BindingSource bindingSource;
        internal Mem Memory { get => this.memory; private set => this.memory = value; }
        private Mem memory;

        public ClubController ClubController { get => this.clubController; private set { } }
        protected ClubController clubController;
        public PlayerController PlayerController { get => this.playerController; private set { } }
        protected PlayerController playerController;
        protected ProcessController processController;

        protected EntityView()
        {
            this.memory = new Mem();
        }
        protected EntityView(IContainer container)
        {
            container.Add(this);
            this.memory = new Mem();
        }

        protected EntityView(Mem memory, ProcessController processController)
        {
            this.memory = memory;this.processController = processController;
            this.processController = processController;
        }

        protected void ClearAllFields(Control parent)
        {
            foreach (Control ctrl in parent.Controls)
            {
                if (ctrl is TextBox tb)
                    tb.Clear();
                else if (ctrl is ComboBox cb)
                    cb.SelectedIndex = -1;
                else if (ctrl is CheckBox chk)
                    chk.Checked = false;
                else if (ctrl is RadioButton rb)
                    rb.Checked = false;
                else if (ctrl.HasChildren)
                    ClearAllFields(ctrl);
            }
        }
        protected bool IsGameRunning()
        {
            if (this.memory.mProc.Process == null)
            {
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
                Process.Start(url);
            }
        }
    }
}
