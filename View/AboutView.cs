using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            InitializeComponent();

            this.GithubLinkLabel.Links.Clear();
            this.GithubLinkLabel.Links.Add(8, 41, "https://github.com/transfairs/a2g-trainer");
            this.AnstossJuengerLinkLabel.Links.Clear();
            this.AnstossJuengerLinkLabel.Links.Add(37, 18, "https://www.anstoss-juenger.de/index.php/topic,4619.0.html");
            this.StrajkLinkLabel.Links.Clear();
            this.StrajkLinkLabel.Links.Add(19, 7, "https://www.anstoss-juenger.de/index.php/topic,6260.0.html");

        }

        public AboutView(IContainer container)
        {
            container.Add(this);

            InitializeComponent();
        }
    }
}
