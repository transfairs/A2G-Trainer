using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// A button with an arrow drawn next to its text: pointing left before the text, or right after it.
    /// Drawn rather than written, because the fonts of Windows 2000 lack the arrow characters and
    /// Windows 9x draws button text through the ANSI code page. The arrow grows with the font, so it
    /// follows the view's zoom.
    /// </summary>
    internal sealed class ArrowButton : Button
    {
        private bool arrowLeft;

        /// <summary>A button with the arrow pointing right, after the text.</summary>
        public ArrowButton()
        {
            this.ReserveArrowSpace();
        }

        /// <summary>True: the arrow points left and stands before the text; false: it points right and follows it.</summary>
        [DefaultValue(false)]
        public bool ArrowLeft
        {
            get => this.arrowLeft;
            set
            {
                this.arrowLeft = value;
                this.ReserveArrowSpace();
            }
        }

        private float ArrowHeight => this.Font.Height * 0.55f;
        private float ArrowWidth => this.ArrowHeight * 0.87f;
        private int Gap => Math.Max(2, this.Font.Height / 4);

        /// <summary>Makes room for the arrow on its side, so arrow and text together stay centred.</summary>
        private void ReserveArrowSpace()
        {
            int space = (int)Math.Ceiling(this.ArrowWidth) + this.Gap;
            this.Padding = this.arrowLeft ? new Padding(space, 0, 0, 0) : new Padding(0, 0, space, 0);
        }

        /// <summary>Keeps the room for the arrow in step with the font.</summary>
        protected override void OnFontChanged(EventArgs e)
        {
            this.ReserveArrowSpace();
            base.OnFontChanged(e);
        }

        /// <summary>Draws the button, then the arrow beside the centred text, grey when disabled.</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Rectangle content = new Rectangle(
                this.Padding.Left, this.Padding.Top,
                this.ClientSize.Width - this.Padding.Horizontal, this.ClientSize.Height - this.Padding.Vertical);
            int textWidth = TextRenderer.MeasureText(e.Graphics, this.Text, this.Font).Width;
            float textLeft = content.Left + (content.Width - textWidth) / 2f;
            float height = this.ArrowHeight, width = this.ArrowWidth, y = content.Top + content.Height / 2f;
            float tip = this.arrowLeft ? textLeft - this.Gap - width : textLeft + textWidth + this.Gap + width;
            float back = this.arrowLeft ? tip + width : tip - width;
            PointF[] triangle = { new PointF(tip, y), new PointF(back, y - height / 2), new PointF(back, y + height / 2) };
            SmoothingMode smoothing = e.Graphics.SmoothingMode;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (Brush brush = new SolidBrush(this.Enabled ? this.ForeColor : SystemColors.GrayText))
                e.Graphics.FillPolygon(brush, triangle);
            e.Graphics.SmoothingMode = smoothing;
        }
    }
}
