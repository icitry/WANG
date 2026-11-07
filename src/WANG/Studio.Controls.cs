namespace WANG
{
    internal sealed partial class Studio
    {
        private const int ControlWidth = 336, ButtonHeight = 34, ButtonGap = 8;
        private readonly FlowLayoutPanel desktopConsole = new()
        {
            Dock = DockStyle.Fill,
            Padding = new(16, 10, 16, 12),
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            BackColor = Color.FromArgb(245, 247, 250)
        };

        private readonly Button desktopRun = new()
        {
            Text = "Live"
        };

        private readonly Button desktopUndo = new()
        {
            Text = "Undo"
        };

        private readonly Button desktopRedo = new()
        {
            Text = "Redo"
        };

        private readonly ContextMenuStrip desktopExamples = new();
        private readonly Dictionary<string, Func<Project>> demos = new()
        {
            ["Arithmetic"] = WindowExamples.Arithmetic,
            ["Decision"] = WindowExamples.Decision,
            ["Countdown"] = WindowExamples.Countdown,
            ["Cursor"] = WindowExamples.Cursor,
            ["Pong"] = WindowExamples.Pong
        };

        private Guid? lastDesktopSource;
        private bool discoveryReady;
        private readonly HashSet<nint> releasedDesktopWindows = [];
        private string lastWindowError = "";

        private void SetupDesktopConsole()
        {
            Controls.Add(desktopConsole);
            SetupPageControls();
            Section("Run");
            desktopRun.Click += (_, _) => Safely(ToggleDesktopRun);
            ControlRow(desktopRun, ActionButton("Step", () =>
            {
                running = false;
                RefreshWindows();
                forceLiveOutput = true;
                TickWindows();
                UpdateDesktopConsole();
            }));
            ControlRow(ActionButton("Reset", () =>
            {
                running = false;
                windowRuntime.Reset();
                livePrinted.Clear();
                livePublished = null;
                forceLiveOutput = true;
                RefreshWindows();
            }), ActionButton("Output", () =>
            {
                PublishRelationships();
                viewport.Show();
            }));
            Section("Windows");
            ControlRow(ActionButton("New window", () => EditSource("Open a window", NewDesktopWindow)));
            desktopUndo.Click += (_, _) => Safely(() => NavigateHistory(false));
            desktopRedo.Click += (_, _) => Safely(() => NavigateHistory(true));
            ControlRow(desktopUndo, desktopRedo);
            ControlRow(ActionButton("Window actions", () => sourceActions.Show(this, PointToClient(Cursor.Position))));
            Section("Programs");
            ControlRow(ActionButton("Open", LoadProject), ActionButton("Save", SaveProject));
            ControlRow(ActionButton("Examples", ShowDesktopExamples));
            Section("Reference");
            ControlRow(ActionButton("Help", ShowRelationshipHelp), ActionButton("Log", () => activityLog.Reveal(this)));
            desktopExamples.Items.Add("Empty desktop", null, (_, _) => Safely(() => OpenProject(Relationships.Empty())));
            desktopExamples.Items.Add(new ToolStripSeparator());
            foreach (var (name, make) in demos)
            {
                desktopExamples.Items.Add(name, null, (_, _) => Safely(() =>
                {
                    OpenProject(make());
                    viewport.Show();
                }));
            }

            FormClosed += (_, _) => desktopExamples.Dispose();
        }

        private Button ActionButton(string label, Action action)
        {
            var button = new Button
            {
                Text = label
            };

            button.Click += (_, _) => Safely(action);

            return button;
        }

        private void Section(string title)
        {
            var heading = new TableLayoutPanel
            {
                Size = new(ControlWidth, 24),
                Margin = new(0, desktopConsole.Controls.Count == 0 ? 0 : 12, 0, 6),
                ColumnCount = 2,
                RowCount = 1
            };

            heading.ColumnStyles.Add(new(SizeType.AutoSize));
            heading.ColumnStyles.Add(new(SizeType.Percent, 100));
            heading.Controls.Add(
                new Label
                {
                    Text = title,
                    AutoSize = true,
                    Font = new(Font, FontStyle.Bold),
                    ForeColor = Color.FromArgb(65, 76, 94),
                    Margin = new(0, 3, 10, 0)
                },
                0,
                0);
            heading.Controls.Add(
                new Panel
                {
                    Dock = DockStyle.Top,
                    Height = 1,
                    BackColor = Color.FromArgb(207, 214, 224),
                    Margin = new(0, 12, 0, 0)
                },
                1,
                0);
            desktopConsole.Controls.Add(heading);
        }

        private void ControlRow(Control left, Control? right = null)
        {
            var row = new TableLayoutPanel
            {
                Size = new(ControlWidth, ButtonHeight),
                Margin = new(0, 0, 0, 6),
                ColumnCount = right is null ? 1 : 3,
                RowCount = 1
            };

            row.ColumnStyles.Add(new(SizeType.Percent, right is null ? 100 : 50));
            if (right is not null)
            {
                row.ColumnStyles.Add(new(SizeType.Absolute, ButtonGap));
                row.ColumnStyles.Add(new(SizeType.Percent, 50));
            }

            void Add(Control control, int column)
            {
                control.AutoSize = false;
                control.Dock = DockStyle.Fill;
                control.Margin = new(0);
                if (control is Button button)
                {
                    button.FlatStyle = FlatStyle.System;
                }

                row.Controls.Add(control, column, 0);
            }

            Add(left, 0);
            if (right is not null)
            {
                Add(right, 2);
            }

            desktopConsole.Controls.Add(row);
        }

        private void ConfigureDesktopMode()
        {
            var area = Screen.FromControl(this).WorkingArea;
            var desired = LogicalToDeviceUnits(new Size(368, 736));

            MinimumSize = Size.Empty;
            MaximumSize = Size.Empty;
            var availableHeight = area.Height - 64;

            ClientSize = new(desired.Width + (desired.Height > availableHeight ? SystemInformation.VerticalScrollBarWidth : 0), Math.Min(desired.Height, availableHeight));
            var row = desktopConsole.Controls.OfType<TableLayoutPanel>().First();
            var scale = LogicalToDeviceUnits(ControlWidth) / (float)row.Width;

            if (Math.Abs(scale - 1) > 0.01)
            {
                desktopConsole.Scale(new SizeF(scale, scale));
            }

            MinimumSize = MaximumSize = Size;
            Location = new(area.Right - Width - 12, area.Top + 20);
            Text = "WANG 0.1.0";
            UpdateDesktopConsole();
        }

        private void UpdateDesktopConsole()
        {
            PublishRelationships();
            UpdatePageControls();
            UpdateOverview();
            desktopRun.Text = running ? "Pause" : "Live";
            var error = observer.CallbackError is not null
                ? "Window observer failed. Restart WANG."
                : !available && project.Windows.Count > 0 ? "Source unavailable." : "";

            if (error != lastWindowError)
            {
                lastWindowError = error;
                if (error.Length > 0)
                {
                    activityLog.Write(error);
                }
            }
        }

        private void ToggleDesktopRun()
        {
            running = !running;
            RefreshWindows();
        }

        private void ShowDesktopExamples()
        {
            desktopExamples.Show(this, PointToClient(Cursor.Position));
        }
    }
}
