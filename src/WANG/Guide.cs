namespace WANG
{
    internal sealed class Guide : Form
    {
        internal IReadOnlyList<WindowCaption> Captions { get; private set; } = [];

        public Guide()
        {
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            Bounds = SystemInformation.VirtualScreen;
            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;

                parameters.ExStyle |= 0x080800A0;

                return parameters;
            }
        }

        internal void UpdateCaptions(IReadOnlyList<WindowCaption> captions)
        {
            if (Captions.Count == captions.Count && Captions.Zip(captions).All(pair => pair.First.SameAs(pair.Second)))
            {
                return;
            }

            Captions = captions;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using var font = new Font("Segoe UI", 11, FontStyle.Bold);
            var flags = TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;

            foreach (var caption in Captions)
            {
                var width = Math.Max(0, caption.Bounds.Width - 24);
                var height = Math.Min(font.Height + 6, Math.Max(0, caption.Bounds.Height - 36));

                if (width == 0 || height == 0)
                {
                    continue;
                }

                var measured = TextRenderer.MeasureText(e.Graphics, caption.Text, font, new Size(int.MaxValue, height), flags);
                var line = new Rectangle(caption.Bounds.X + 12 - Left, caption.Bounds.Y + 36 - Top, Math.Min(width, measured.Width + 6), height);
                var state = e.Graphics.Save();

                e.Graphics.SetClip(line, System.Drawing.Drawing2D.CombineMode.Intersect);
                foreach (var cover in caption.Occluders)
                {
                    e.Graphics.ExcludeClip(new Rectangle(cover.X - Left, cover.Y - Top, cover.Width, cover.Height));
                }

                TextRenderer.DrawText(
                    e.Graphics,
                    caption.Text,
                    font,
                    line,
                    Color.Black,
                    caption.Color,
                    flags | TextFormatFlags.PreserveGraphicsClipping);
                e.Graphics.Restore(state);
            }
        }
    }
}
