using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace A2G_Trainer.View
{
    /// <summary>
    /// A green tick or a red cross the quiz help puts at the left edge of an answer button of the game: a borderless,
    /// topmost window that never takes the focus and lets every click through to the game.
    /// </summary>
    internal sealed class QuizMark : Form
    {
        private const int WsExLayered = 0x80000;
        private const int WsExTransparent = 0x20;
        private const int WsExToolWindow = 0x80;
        private const int WsExNoActivate = 0x8000000;
        private const int WsExTopmost = 0x8;
        // Drawn without smoothing, so no edge pixel blends with the colour that is made transparent.
        private static readonly Color Background = Color.Magenta;

        private bool isRight;

        /// <summary>Creates a hidden mark; <paramref name="right"/> picks the tick, otherwise the cross.</summary>
        internal QuizMark(bool right)
        {
            this.isRight = right;
            this.FormBorderStyle = FormBorderStyle.None;
            this.ShowInTaskbar = false;
            this.StartPosition = FormStartPosition.Manual;
            this.BackColor = Background;
            this.TransparencyKey = Background;
            this.DoubleBuffered = true;
        }

        /// <summary>Whether this is the tick of the right answer; otherwise the cross of a wrong one.</summary>
        internal bool IsRight
        {
            get => this.isRight;
            set
            {
                if (value == this.isRight)
                    return;
                this.isRight = value;
                this.Invalidate();
            }
        }

        /// <inheritdoc />
        protected override bool ShowWithoutActivation => true;

        /// <inheritdoc />
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.ExStyle |= WsExLayered | WsExTransparent | WsExToolWindow | WsExNoActivate | WsExTopmost;
                return parameters;
            }
        }

        /// <inheritdoc />
        protected override void OnPaint(PaintEventArgs e)
        {
            int size = System.Math.Min(this.ClientSize.Width, this.ClientSize.Height);
            float width = System.Math.Max(3f, size / 5f);
            e.Graphics.SmoothingMode = SmoothingMode.None;
            using (Pen pen = new Pen(this.isRight ? Color.FromArgb(0, 170, 0) : Color.FromArgb(220, 0, 0), width) { StartCap = LineCap.Square, EndCap = LineCap.Square, LineJoin = LineJoin.Miter })
            {
                float inset = width;
                float far = size - width;
                if (this.isRight)
                    e.Graphics.DrawLines(pen, new[] { new PointF(inset, size * 0.55f), new PointF(size * 0.4f, far), new PointF(far, inset) });
                else
                {
                    e.Graphics.DrawLine(pen, inset, inset, far, far);
                    e.Graphics.DrawLine(pen, inset, far, far, inset);
                }
            }
        }
    }
}
