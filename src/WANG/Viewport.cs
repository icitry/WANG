namespace WANG
{
    internal sealed class Viewport : Form
    {
        private sealed class Picture : Control
        {
            internal IReadOnlyList<Drawing> Frame = [];

            internal Picture()
            {
                DoubleBuffered = true;
                Dock = DockStyle.Fill;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var xScale = ClientSize.Width / 100f;
                var yScale = ClientSize.Height / 60f;

                foreach (var item in Frame)
                {
                    if (item.Kind == "Rect" && item.Width > 0 && item.Height > 0 && new[]
                    {
                        item.X,
                        item.Y,
                        item.Width,
                        item.Height
                    }.All(v => double.IsFinite(v) && Math.Abs(v) < 100000))
                    {
                        e.Graphics.FillRectangle(
                            Brushes.Aquamarine,
                            (float)item.X * xScale,
                            (float)item.Y * yScale,
                            (float)item.Width * xScale,
                            (float)item.Height * yScale);
                    }

                    if (item.Kind == "Number")
                    {
                        using var font = new Font("Segoe UI", 24, FontStyle.Bold);

                        e.Graphics.DrawString(item.Value.ToString("0.##"), font, Brushes.White, 20, 14);
                    }
                }
            }
        }

        private readonly Picture picture = new();
        private readonly TextBox printed = new()
        {
            Dock = DockStyle.Bottom,
            Height = 140,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new("Consolas", 18),
            BackColor = Color.FromArgb(22, 28, 40),
            ForeColor = Color.White,
            Visible = false
        };

        private readonly Label message = new()
        {
            Dock = DockStyle.Top,
            Height = 32,
            Padding = new(8),
            ForeColor = Color.White,
            Visible = false
        };

        internal IReadOnlyList<double> PrintedValues { get; private set; } = [];
        internal string? RuntimeError { get; private set; }

        private double pointerX = 50;
        private double pointerY = 30;
        private bool space;

        public Viewport()
        {
            Text = "WANG / Program output";
            ClientSize = new(800, 480);
            DoubleBuffered = true;
            BackColor = Color.FromArgb(15, 19, 30);
            KeyPreview = true;
            Controls.Add(picture);
            Controls.Add(printed);
            Controls.Add(message);
            picture.MouseMove += (_, e) =>
            {
                pointerX = Math.Clamp(e.X * 100d / Math.Max(1, picture.ClientSize.Width), 0, 100);
                pointerY = Math.Clamp(e.Y * 60d / Math.Max(1, picture.ClientSize.Height), 0, 60);
            };
            KeyDown += (_, e) =>
            {
                if (e.KeyCode == Keys.Space)
                {
                    space = true;
                }
            };
            KeyUp += (_, e) =>
            {
                if (e.KeyCode == Keys.Space)
                {
                    space = false;
                }
            };
            Deactivate += (_, _) => space = false;
            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }

        public double ReadInput(int channel)
        {
            return channel switch
            {
                0 => pointerY,
                1 => space ? 1 : 0,
                2 => 1d / 60,
                WindowRuntime.PointerXChannel => pointerX,
                _ => 0
            };
        }

        protected override bool ShowWithoutActivation => true;

        public void SetFrame(IReadOnlyList<Drawing> commands)
        {
            picture.Frame = commands;
            picture.Visible = commands.Count > 0;
            printed.Dock = picture.Visible ? DockStyle.Bottom : DockStyle.Fill;
            picture.Invalidate();
        }


        public void SetValues(IReadOnlyList<double> values, string? error = null, string state = "")
        {
            if (!PrintedValues.SequenceEqual(values))
            {
                PrintedValues = values.ToArray();
                printed.Text = string.Join(Environment.NewLine, values.Select(value => value.ToString("0.###")));
                printed.SelectionStart = printed.TextLength;
                printed.ScrollToCaret();
            }

            printed.Visible = values.Count > 0;
            picture.Visible = picture.Frame.Count > 0;
            printed.Dock = picture.Visible ? DockStyle.Bottom : DockStyle.Fill;
            RuntimeError = error;
            message.Text = RuntimeError ?? state;
            message.ForeColor = RuntimeError is null ? Color.White : Color.Salmon;
            message.Visible = message.Text.Length > 0;
        }
    }
}
