using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// In-app help tab. Its content is the online help (help/index.html, embedded in the exe, see
    /// HelpDocument): one tab per section of the online help, and a second row of tabs for its
    /// sub-tabs, so both helps always say the same.
    /// </summary>
    public partial class HelpView : UserControl
    {
        /// <summary>Creates the Help view; it fills itself from the embedded online help when first shown.</summary>
        public HelpView()
        {
            InitializeComponent();
        }

        /// <summary>Creates the Help view and registers it with the given designer container; it fills itself when first shown.</summary>
        public HelpView(IContainer container)
        {
            container.Add(this);

            InitializeComponent();
        }

        // Built when the help is first shown, not with every main window: some forty text boxes
        // would cost every start time and memory for a view most sessions never open.
        private bool loaded;

        /// <summary>Fills the tabs from the embedded online help, once.</summary>
        internal void EnsureHelpLoaded()
        {
            if (this.loaded)
                return;
            this.loaded = true;
            this.InitHelpTexts();
        }

        /// <inheritdoc/>
        protected override void OnVisibleChanged(System.EventArgs e)
        {
            if (this.Visible)
                this.EnsureHelpLoaded();
            base.OnVisibleChanged(e);
        }

        private void InitHelpTexts()
        {
            foreach (HelpDocument.Page page in HelpDocument.FromResource().Pages)
            {
                TabPage tab = new TabPage(page.Title) { UseVisualStyleBackColor = true, Padding = new Padding(5, 6, 3, 3) };
                if (page.Sections.Count == 1)
                {
                    tab.Controls.Add(NewHelpBox(page.Sections[0].Value));
                }
                else
                {
                    TabControl sections = new TabControl { Dock = DockStyle.Fill };
                    foreach (KeyValuePair<string, string> section in page.Sections)
                    {
                        TabPage sectionTab = new TabPage(section.Key ?? page.Title) { UseVisualStyleBackColor = true, Padding = new Padding(5, 6, 3, 3) };
                        sectionTab.Controls.Add(NewHelpBox(section.Value));
                        sections.TabPages.Add(sectionTab);
                    }
                    tab.Controls.Add(sections);
                }
                this.MainTabControl.TabPages.Add(tab);
            }
        }

        private static RichTextBox NewHelpBox(string rtf)
        {
            RichTextBox box = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.HighlightText,
                Rtf = rtf
            };
            return box;
        }
    }
}
