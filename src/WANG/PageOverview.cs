using System.Drawing.Drawing2D;

namespace WANG
{
    internal sealed class PageOverview : Form
    {
        private readonly Map map = new()
        {
            Dock = DockStyle.Fill
        };

        private readonly ToolStripLabel counts = new();
        internal event Action<int>? PageChosen;
        internal int PageCount => map.Groups.Count;
        internal int WindowCount => map.Groups.Sum(g => g.Windows.Length);

        internal PageOverview()
        {
            Text = "WANG / All pages";
            var area = Screen.PrimaryScreen!.WorkingArea;

            Size = new(Math.Max(500, Math.Min(1150, area.Width - 80)), Math.Max(400, Math.Min(800, area.Height - 80)));
            StartPosition = FormStartPosition.CenterScreen;
            var toolbar = new ToolStrip
            {
                Dock = DockStyle.Top,
                GripStyle = ToolStripGripStyle.Hidden
            };

            toolbar.Items.Add("Fit all", null, (_, _) => map.Fit());
            toolbar.Items.Add("−", null, (_, _) => map.ZoomAt(1 / 1.3f, new(map.Width / 2, map.Height / 2)));
            toolbar.Items.Add("+", null, (_, _) => map.ZoomAt(1.3f, new(map.Width / 2, map.Height / 2)));
            toolbar.Items.Add(counts);
            Controls.Add(map);
            Controls.Add(toolbar);
            Controls.Add(new Label
            {
                Dock = DockStyle.Bottom,
                Height = 42,
                Padding = new(8),
                Text = "Live miniatures — wheel to zoom, drag to pan, click a group to edit its real windows.\nGroups stay separate; this view never changes source sizes or connections."
            });
            map.OpenPage += OpenPage;
            FormClosing += (_, e) =>
            {
                if (e.CloseReason == CloseReason.UserClosing)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
        }

        internal void SetSource(Project project, RelationshipResult? values, int activePage)
        {
            map.SetSource(project, values, activePage);
            counts.Text = $"{PageCount} pages • {WindowCount} windows";
        }

        internal void OpenPage(int id)
        {
            if (!map.Groups.Any(g => g.Id == id))
            {
                return;
            }

            PageChosen?.Invoke(id);
            Hide();
        }

        internal void Fit()
        {
            map.Fit();
        }

        private sealed record Group(int Id, int Index, string Name, SourceWindow[] Windows, RectangleF Tile);

        private sealed class Map : Control
        {
            internal List<Group> Groups { get; private set; } = [];
            internal float Zoom { get; private set; } = 1;

            internal event Action<int>? OpenPage;
            private IReadOnlyDictionary<Guid, WindowValue> values = new Dictionary<Guid, WindowValue>();
            private int activePage;
            private SizeF world = new(460, 340);
            private PointF pan;
            private bool fit = true;
            private Point? dragStart;
            private PointF originalPan;
            private string pageIds = "";
            private bool jarGeometry;
            private int jarHeightStep = 1;
            private int maximumJar = JarRuler.MaximumIndex;

            internal Map()
            {
                DoubleBuffered = true;
                ResizeRedraw = true;
                BackColor = Color.FromArgb(23, 29, 40);
            }

            internal void SetSource(Project project, RelationshipResult? result, int active)
            {
                values = result?.Windows ?? new Dictionary<Guid, WindowValue>();
                activePage = active;
                jarGeometry = project.Version >= 4;
                jarHeightStep = JarRuler.Step(project);
                maximumJar = JarRuler.Maximum(project);
                var pages = project.Workspaces.OrderBy(p => p.Key).ToArray();
                var columns = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(pages.Length)));
                var signature = string.Join(",", pages.Select(p => p.Key));

                Groups = pages.Select((page, i) => new Group(
                    page.Key,
                    i + 1,
                    page.Value,
                    project.Windows.Where(w => w.Workspace == page.Key).ToArray(),
                    new(20 + i % columns * 460, 20 + i / columns * 340, 440, 320)))
                    .ToList();
                world = new(columns * 460 + 20, (int)Math.Ceiling(pages.Length / (double)columns) * 340 + 20);
                if (signature != pageIds)
                {
                    pageIds = signature;
                    fit = true;
                }

                if (fit)
                {
                    Fit();
                }
                else
                {
                    Invalidate();
                }
            }

            internal void Fit()
            {
                fit = true;
                Zoom = Math.Clamp(Math.Min(Math.Max(1, Width - 20) / world.Width, Math.Max(1, Height - 20) / world.Height), .01f, 3);
                pan = new((Width - world.Width * Zoom) / 2, (Height - world.Height * Zoom) / 2);
                Invalidate();
            }

            internal void ZoomAt(float factor, Point anchor)
            {
                var old = Zoom;

                Zoom = Math.Clamp(Zoom * factor, .01f, 3);
                fit = false;
                pan = new(anchor.X - (anchor.X - pan.X) * Zoom / old, anchor.Y - (anchor.Y - pan.Y) * Zoom / old);
                Invalidate();
            }

            protected override void OnSizeChanged(EventArgs e)
            {
                base.OnSizeChanged(e);
                if (fit)
                {
                    Fit();
                }
            }

            private RectangleF ScreenBounds(RectangleF rect)
            {
                return new(pan.X + rect.X * Zoom, pan.Y + rect.Y * Zoom, rect.Width * Zoom, rect.Height * Zoom);
            }

            protected override void OnMouseWheel(MouseEventArgs e)
            {
                base.OnMouseWheel(e);
                ZoomAt(e.Delta > 0 ? 1.2f : 1 / 1.2f, e.Location);
            }

            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button is MouseButtons.Left or MouseButtons.Middle)
                {
                    dragStart = e.Location;
                    originalPan = pan;
                    Capture = true;
                }
            }

            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                if (dragStart is Point start)
                {
                    fit = false;
                    pan = new(originalPan.X + e.X - start.X, originalPan.Y + e.Y - start.Y);
                    Invalidate();
                }
            }

            protected override void OnMouseCaptureChanged(EventArgs e)
            {
                base.OnMouseCaptureChanged(e);
                if (!Capture)
                {
                    dragStart = null;
                }
            }

            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                var start = dragStart;

                dragStart = null;
                Capture = false;
                if (e.Button != MouseButtons.Left || start is null
                    || Math.Abs(e.X - start.Value.X) + Math.Abs(e.Y - start.Value.Y) > 4)
                {
                    return;
                }

                var group = Groups.FirstOrDefault(g => ScreenBounds(g.Tile).Contains(e.Location));

                if (group is not null)
                {
                    OpenPage?.Invoke(group.Id);
                }
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                var graphics = e.Graphics;

                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var titleFont = new Font("Segoe UI", 9, FontStyle.Bold);
                using var numberFont = new Font("Segoe UI", Math.Clamp(10 * Zoom, 6, 18), FontStyle.Bold);
                using var centered = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    Trimming = StringTrimming.EllipsisCharacter
                };
                using var titleFormat = new StringFormat
                {
                    Trimming = StringTrimming.EllipsisCharacter,
                    FormatFlags = StringFormatFlags.NoWrap
                };

                foreach (var group in Groups)
                {
                    var tile = ScreenBounds(group.Tile);

                    if (!tile.IntersectsWith(ClientRectangle))
                    {
                        continue;
                    }

                    using var background = new SolidBrush(Color.FromArgb(43, 52, 69));

                    graphics.FillRectangle(background, tile);
                    using var border = new Pen(
                        group.Id == activePage ? Color.Aquamarine : Color.FromArgb(100, 113, 134),
                        group.Id == activePage ? 2 : 1);

                    graphics.DrawRectangle(border, tile.X, tile.Y, tile.Width, tile.Height);
                    var header = Math.Min(30, tile.Height * .32f);

                    graphics.DrawString(
                        $"{group.Index}. {group.Name}",
                        titleFont,
                        Brushes.White,
                        new RectangleF(tile.X + 5, tile.Y + 4, Math.Max(1, tile.Width - 10), header),
                        titleFormat);
                    var inner = new RectangleF(tile.X + 6, tile.Y + header, tile.Width - 12, tile.Height - header - 6);

                    if (inner.Width < 8 || inner.Height < 8)
                    {
                        continue;
                    }

                    if (group.Windows.Length == 0)
                    {
                        graphics.DrawString("Empty page", titleFont, Brushes.LightGray, inner, centered);
                        continue;
                    }

                    var bounds = group.Windows.Select(w => w.Bounds).Aggregate(Rectangle.Union);
                    var scale = Math.Min(inner.Width / Math.Max(1, bounds.Width), inner.Height / Math.Max(1, bounds.Height));
                    var state = graphics.Save();

                    graphics.SetClip(inner);
                    foreach (var window in group.Windows.OrderByDescending(w => w.ZOrder))
                    {
                        var rect = new RectangleF(
                            inner.X + (window.X - bounds.X) * scale,
                            inner.Y + (window.Y - bounds.Y) * scale,
                            Math.Max(2, window.Width * scale),
                            Math.Max(2, window.Height * scale));
                        var value = values.GetValueOrDefault(window.Id);
                        var color = jarGeometry && JarRuler.Contains(window.Height, jarHeightStep, maximumJar)
                            ? RelationshipAppearance.Color(Relationship.Memory)
                            : RelationshipAppearance.Color(value?.Relationship ?? Relationship.Number);

                        if (window.State == "Minimized")
                        {
                            color = Color.FromArgb(105, 115, 129);
                        }

                        using var body = new SolidBrush(color);

                        graphics.FillRectangle(body, rect);
                        graphics.DrawRectangle(Pens.DimGray, rect.X, rect.Y, rect.Width, rect.Height);
                        var bar = Math.Min(12, Math.Max(2, rect.Height * .15f));
                        using var caption = new SolidBrush(Color.FromArgb(205, 214, 225));

                        graphics.FillRectangle(caption, rect.X, rect.Y, rect.Width, bar);
                        if (rect.Width >= 15 && rect.Height >= 15)
                        {
                            graphics.DrawString(
                                value?.Error is not null ? "?" : value?.Value.ToString("0.###") ?? "",
                                numberFont,
                                Brushes.Black,
                                new RectangleF(rect.X, rect.Y + bar, rect.Width, Math.Max(1, rect.Height - bar)),
                                centered);
                        }
                    }

                    graphics.Restore(state);
                }
            }
        }
    }
}
