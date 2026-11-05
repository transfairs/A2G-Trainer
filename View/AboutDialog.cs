using AnstossTrainer.Shared;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>About dialog: version, project links, credits and a link to the log folder.</summary>
    public class AboutDialog : Form
    {
        internal const string GithubUrl = "https://github.com/transfairs/A2G-Trainer";
        internal const string ForumUrl = "https://www.anstoss-juenger.de/index.php/topic,4619.0.html";
        internal const string StrajkUrl = "https://www.anstoss-juenger.de/index.php/topic,6260.0.html";
        internal const string TwentyTwoUrl = "https://www.anstoss-juenger.de/index.php/topic,4619.msg434382.html#msg434382";

        private readonly ToolTip tip = new ToolTip();

        /// <summary>Opens a link target (URL or folder) with the shell; tests replace it to avoid opening a browser.</summary>
        internal static Action<string> Open = target => Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true });

        /// <summary>Shows why a link couldn't be opened; tests replace it so no message box blocks them.</summary>
        internal static Action<IWin32Window, string, string> ShowError = (owner, text, caption) => MessageBox.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);

        /// <summary>The folder holding trainer.log (see Logger.A2.cs).</summary>
        internal static string LogFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Logger.AppFolder);

        /// <summary>
        /// The version the release pipeline stamped into Properties/VersionInfo.cs
        /// (AssemblyInformationalVersion, e.g. "0.7.0-alpha"), so it can't go stale relative to the
        /// build/tag; local builds show VersionInfo.cs's own default ("0.0.0-dev").
        /// </summary>
        internal static string Version => ((AssemblyInformationalVersionAttribute)typeof(AboutDialog).Assembly
            .GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false)[0]).InformationalVersion;

        // The logo PNG scaled down to 64 px (the icon only has a 256 px frame, which a 64 px box would crop).
        private static Bitmap LoadLogo()
        {
            Bitmap logo = new Bitmap(64, 64);
            using (Bitmap source = Properties.Resources.Trainer_Logo)
            using (Graphics g = Graphics.FromImage(logo))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.DrawImage(source, 0, 0, 64, 64);
            }
            return logo;
        }

        /// <summary>Builds the dialog.</summary>
        public AboutDialog()
        {
            this.Text = "Über A2G-Trainer";
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = SystemFonts.MessageBoxFont;
            this.ClientSize = new Size(480, 304);

            PictureBox picture = new PictureBox { Name = "Logo", Image = LoadLogo(), SizeMode = PictureBoxSizeMode.Normal };
            picture.SetBounds(12, 12, 64, 64);
            this.Controls.Add(picture);
            int x = 88;
            int w = this.ClientSize.Width - x - 12;

            Label title = new Label { Name = "TitleLabel", Text = "A2G-Trainer", Font = new Font(this.Font.FontFamily, this.Font.Size * 1.5f, FontStyle.Bold), AutoSize = false };
            title.SetBounds(x, 10, w - 110, 30);
            Label version = new Label { Name = "VersionLabel", Text = "v" + Version, AutoSize = false, TextAlign = ContentAlignment.MiddleRight, ForeColor = SystemColors.GrayText };
            version.SetBounds(this.ClientSize.Width - 122, 14, 110, 22);
            Label description = new Label { Text = "Echtzeit-Speichereditor für Anstoss 2 Gold.", AutoSize = false };
            description.SetBounds(x, 44, w, 20);
            this.Controls.AddRange(new Control[] { title, version, description });

            this.AddLink(x, 72, w, 20, "GitHub: " + GithubUrl, GithubUrl, GithubUrl);
            this.AddLink(x, 94, w, 20, "Mit Unterstützung der Mitglieder des anstoss-juenger.de-Forums.", "anstoss-juenger.de", ForumUrl);

            Label idea = new Label { Text = "Nach einer Idee von Alexander Neufeld.", AutoSize = false };
            idea.SetBounds(x, 122, w, 20);
            Label contributor = new Label { Text = "Contributor: strajk-", AutoSize = false };
            contributor.SetBounds(x, 150, w, 20);
            this.Controls.AddRange(new Control[] { idea, contributor });
            this.AddLink(x, 178, w, 36, "Vielen Dank an strajk- für die Vorarbeit und an 22 für den Beistand bei der Adressrecherche!",
                "strajk-", StrajkUrl, " 22 ", TwentyTwoUrl);
            this.AddLink(x, 222, w, 36, "Protokoll:\n" + LogFolder, LogFolder, LogFolder);

            Button ok = new Button { Name = "OkButton", Text = "OK", DialogResult = DialogResult.OK };
            ok.SetBounds(this.ClientSize.Width - 102, 266, 90, 28);
            this.Controls.Add(ok);
            this.AcceptButton = this.CancelButton = ok;
        }

        // A label whose given parts (pairs of text and target) are clickable. A part with spaces
        // around it is matched with them, so a short one like "22" isn't found inside a word.
        private void AddLink(int x, int y, int w, int h, string text, params string[] parts)
        {
            LinkLabel link = new LinkLabel { Text = text, AutoSize = false, AutoEllipsis = true };
            link.Links.Clear();
            for (int i = 0; i < parts.Length; i += 2)
            {
                string part = parts[i].Trim();
                int start = text.IndexOf(parts[i], StringComparison.Ordinal) + parts[i].IndexOf(part, StringComparison.Ordinal);
                link.Links.Add(start, part.Length, parts[i + 1]);
            }
            link.SetBounds(x, y, w, h);
            link.LinkClicked += (s, e) => this.OpenLink(e.Link.LinkData as string);
            this.tip.SetToolTip(link, parts.Length == 2 ? parts[1] : null);
            this.Controls.Add(link);
        }

        // A folder is created first: the log folder only exists once something was logged.
        internal void OpenLink(string target)
        {
            if (string.IsNullOrEmpty(target))
                return;
            try
            {
                if (target == LogFolder)
                    Directory.CreateDirectory(target);
                Open(target);
            }
            catch (Exception ex)
            {
                ShowError(this, ex.Message, this.Text);
            }
        }

        /// <summary>Disposes the tooltip component along with the form.</summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                this.tip.Dispose();
            base.Dispose(disposing);
        }
    }
}
