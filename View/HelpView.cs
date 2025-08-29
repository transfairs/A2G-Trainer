using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace A2G_Trainer_XP.View
{
    public partial class HelpView : UserControl
    {
        public HelpView()
        {
            InitializeComponent();
        }

        public HelpView(IContainer container)
        {
            container.Add(this);

            InitializeComponent();
        }
    }
}
