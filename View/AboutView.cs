using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    /// <summary>About tab: credits and clickable links to the project's home/community threads.</summary>
    public partial class AboutView : UserControl
    {
        /// <summary>Creates the About view and wires up its clickable links.</summary>
        public AboutView()
        {
            InitializeComponent();

            this.GithubLinkLabel.Links.Clear();
            this.GithubLinkLabel.Links.Add(8, 41, "https://github.com/transfairs/a2g-trainer");
            this.AnstossJuengerLinkLabel.Links.Clear();
            this.AnstossJuengerLinkLabel.Links.Add(37, 18, "https://www.anstoss-juenger.de/index.php/topic,4619.0.html");
            this.StrajkLinkLabel.Links.Clear();
            this.StrajkLinkLabel.Links.Add(15, 7, "https://www.anstoss-juenger.de/index.php/topic,6260.0.html");
            this.StrajkLinkLabel.Links.Add(48, 2, "https://www.anstoss-juenger.de/index.php/topic,4619.msg434382.html#msg434382");
        }

        /// <summary>Creates the About view and registers it with the given designer container.</summary>
        public AboutView(IContainer container)
        {
            container.Add(this);

            InitializeComponent();
        }
        private void LinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = e.Link.LinkData as string;
            if (!string.IsNullOrEmpty(url))
            {
                this.StartProcess(url);
            }
        }

        // Extracted so tests can override the actual OS process launch (which would otherwise pop
        // open a real browser) while still exercising LinkLabel_LinkClicked's own logic for real.
        internal virtual void StartProcess(string url) => Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });

        private void GithubLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.LinkLabel_LinkClicked(sender, e);
        }

        private void AnstossJuengerLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.LinkLabel_LinkClicked(sender, e);
        }

        private void StrajkLinkLabel_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            this.LinkLabel_LinkClicked(sender, e);
        }
    }
}
