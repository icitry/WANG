namespace WANG
{
    internal sealed class ActivityLog : Form
    {
        private readonly Queue<string> entries = [];
        private readonly TextBox content = new()
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new("Consolas", 10),
            BackColor = Color.White,
            BorderStyle = BorderStyle.None
        };

        internal string Entries => string.Join(Environment.NewLine, entries);

        internal ActivityLog()
        {
            Text = "WANG / Log";
            ClientSize = new(640, 400);
            MinimumSize = new(400, 260);
            StartPosition = FormStartPosition.CenterParent;
            Padding = new(12);
            var clear = new Button
            {
                Text = "Clear log",
                Dock = DockStyle.Bottom,
                Height = 34
            };

            clear.Click += (_, _) =>
            {
                entries.Clear();
                content.Clear();
            };
            Controls.Add(content);
            Controls.Add(clear);
            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }

        internal void Write(string message)
        {
            entries.Enqueue($"{DateTime.Now:HH:mm:ss}  {message}");
            while (entries.Count > 500)
            {
                entries.Dequeue();
            }

            if (Visible)
            {
                RefreshContent();
            }
        }

        internal void Reveal(Form owner)
        {
            RefreshContent();
            Show(owner);
            BringToFront();
        }

        private void RefreshContent()
        {
            content.Text = Entries;
            content.SelectionStart = content.TextLength;
            content.ScrollToCaret();
        }
    }
}
