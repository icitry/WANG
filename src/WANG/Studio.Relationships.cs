namespace WANG
{
    internal static class RelationshipAppearance
    {
        internal static Color Color(Relationship relationship)
        {
            return relationship switch
            {
                Relationship.Add => System.Drawing.Color.FromArgb(185, 229, 208),
                Relationship.Subtract => System.Drawing.Color.FromArgb(250, 204, 189),
                Relationship.Multiply => System.Drawing.Color.FromArgb(216, 202, 246),
                Relationship.Divide => System.Drawing.Color.FromArgb(189, 220, 249),
                Relationship.One => System.Drawing.Color.FromArgb(250, 226, 159),
                Relationship.Memory => System.Drawing.Color.FromArgb(179, 224, 224),
                Relationship.Select => System.Drawing.Color.FromArgb(250, 226, 159),
                Relationship.Less or Relationship.Equal or Relationship.Greater or Relationship.LessEqual => System.Drawing.Color.FromArgb(248, 214, 235),
                Relationship.Rectangle => System.Drawing.Color.FromArgb(189, 220, 249),
                _ => System.Drawing.Color.FromArgb(222, 230, 238)
            };
        }
    }

    internal sealed partial class Studio
    {
        private RelationshipResult? liveResult;
        private string? livePublished;
        private bool forceLiveOutput;
        private readonly WindowRuntime windowRuntime = new();
        private ISet<Guid>? liveAvailable;
        private readonly List<double> livePrinted = [];

        private void ReadSourceOrder(List<WindowInfo> snapshots)
        {
            if (sourceGestures.Count > 0)
            {
                return;
            }

            var rank = 0;
            var observed = new HashSet<Guid>();

            foreach (var snapshot in snapshots)
            {
                var source = project.Windows.FirstOrDefault(w => w.Workspace == activeWorkspace && bindings.TryGetValue(w.Id, out var b) && b.Handle == snapshot.Handle
                    && b.Process == snapshot.ProcessId);

                if (source is not null)
                {
                    source.ZOrder = rank++;
                    observed.Add(source.Id);
                }
            }

            foreach (var source in project.Windows.Where(w => w.Workspace == activeWorkspace && !observed.Contains(w.Id))
                .OrderBy(w => w.ZOrder))
            {
                source.ZOrder = rank++;
            }
        }

        private void RestoreSourceOrder()
        {
            foreach (var source in project.Windows.Where(w => w.Workspace == activeWorkspace).OrderByDescending(w => w.ZOrder))
            {
                if (bindings.TryGetValue(source.Id, out var b) && Alive(b)
                    && !Native.SetWindowPos(b.Handle, 0, 0, 0, 0, 0, 0x13))
                {
                    throw new InvalidOperationException("Stacking restore failed.");
                }
            }
        }

        private void RefreshRelationships(ISet<Guid> missing)
        {
            var visible = project.Windows.Select(w => w.Id).Where(id => !missing.Contains(id)).ToHashSet();

            liveAvailable = visible;
            liveResult = project.Version >= 4
                ? windowRuntime.Preview(project, activeWorkspace, viewport.ReadInput, visible)
                : Relationships.Evaluate(project, activeWorkspace, visible);
            if (!available)
            {
                running = false;
            }

            PaintLiveValues();
        }

        private void PaintLiveValues()
        {
            if (liveResult is null)
            {
                return;
            }

            var sources = project.Windows.ToDictionary(w => w.Id);
            var connections = windowRuntime.Connections.GroupBy(connection => connection.Jar)
                .ToDictionary(group => group.Key, group => group.First());

            foreach (var (id, value) in liveResult.Windows)
            {
                if (cards.TryGetValue(id, out var form) && form is HistoryCard card && !card.IsDisposed)
                {
                    var text = value.Error is null ? value.Value.ToString("0.###") : "?";
                    var source = sources[id];
                    var jar = project.Version >= 4 && WindowRuntime.IsMemory(project, source);
                    var note = jar ? $"Jar {WindowRuntime.Address(project, source)}" : "";
                    var color = RelationshipAppearance.Color(jar ? Relationship.Memory : value.Relationship);

                    if (jar && connections.TryGetValue(id, out var connection))
                    {
                        note += connection.Delayed ? " • stores next step" : " • shared now";
                    }

                    if (card.CenterValue != text || card.BackColor != color || card.JarNote != note)
                    {
                        card.CenterValue = text;
                        card.BackColor = color;
                        card.JarNote = note;
                        card.Invalidate();
                    }
                }
            }
        }

        private void PublishRelationships()
        {
            if (liveResult is null || !running && !forceLiveOutput)
            {
                return;
            }

            forceLiveOutput = false;
            var result = liveResult.Result;
            var signature = $"{result?.Id}/{result?.Value:R}/{result?.Error}";
            var frame = project.Version >= 4 ? windowRuntime.Frame : [];
            var error = project.Version >= 4 ? windowRuntime.Error : result?.Error;

            viewport.SetFrame(frame);
            if (project.Version >= 4 && frame.Count == 0 && signature != livePublished && result is { Error: null })
            {
                if (livePrinted.Count == 128)
                {
                    livePrinted.RemoveAt(0);
                }

                livePrinted.Add(result.Value);
            }

            viewport.SetValues(
                project.Version >= 4 && frame.Count == 0
                    ? livePrinted
                    : result is not null && result.Error is null ? [result.Value] : [],
                error);
            if (signature != livePublished && result is not null && !viewport.Visible)
            {
                viewport.Show();
            }

            livePublished = signature;
        }

        private void TickWindows()
        {
            windowRuntime.Tick(project, activeWorkspace, viewport.ReadInput, liveAvailable);
            liveResult = windowRuntime.Result;
            PaintLiveValues();
            UpdateGuide();
            PublishRelationships();
            UpdateOverview();
            if (windowRuntime.Error is not null)
            {
                running = false;
            }
        }

        private void RelationshipKey(Keys key, nint? focused, Point? spawn)
        {
            if (key == Keys.N)
            {
                EditSource("Open a window", () => NewDesktopWindow(spawn ?? Cursor.Position));

                return;
            }

            if (key == Keys.E)
            {
                EditSource("Capture or release a window", () => CaptureDesktopWindow(focused));

                return;
            }

            if (key == Keys.Space)
            {
                running = !running;
                RefreshWindows();

                return;
            }

            if (key is Keys.Z or Keys.Y)
            {
                NavigateHistory(key == Keys.Y);

                return;
            }

            if (key is Keys.PageUp or Keys.PageDown)
            {
                NavigateRoutine(key == Keys.PageDown ? 1 : -1);

                return;
            }

            if (key == Keys.F)
            {
                EditSource("Create a recipe page", () =>
                {
                    var page = Enumerable.Range(1, 511).First(n => !project.Workspaces.ContainsKey(n));

                    project.Workspaces[page] = $"Recipe {page}";
                    UpdateWorkspaceList();
                    SetRoutine(page);
                });

                return;
            }

            if (key is Keys.P or Keys.O)
            {
                EditSource(key == Keys.P ? "Push a value" : "Pop a value", () => EditPile(key == Keys.P, focused));

                return;
            }

            var source = DesktopTarget(focused);

            if (key == Keys.Delete)
            {
                EditSource("Remove a source window", () =>
                {
                    var handle = bindings[source.Id].Handle;

                    releasedDesktopWindows.Add(handle);
                    project.Windows.Remove(source);
                    bindings.Remove(source.Id);
                    if (cards.Remove(source.Id, out var card))
                    {
                        card.Dispose();
                    }
                });

                return;
            }

            EditSource("Arrange a window", () =>
            {
                var handle = bindings[source.Id].Handle;

                if (key is Keys.Up or Keys.Down)
                {
                    var bounds = source.Bounds;

                    bounds.Height = Math.Clamp(bounds.Height + (key == Keys.Up ? 1 : -1) * JarRuler.Step(project), 120, 10000);
                    DesktopObserver.RestoreShape(handle, bounds, "Normal");
                }

                if (key == Keys.M)
                {
                    var free = Enumerable.Range(0, JarRuler.Maximum(project) + 1)
                        .Where(index => !WindowRuntime.IsInput(project, index))
                        .First(n => project.Windows.All(w => !WindowRuntime.IsMemory(project, w) || WindowRuntime.Address(project, w) != n));
                    var bounds = source.Bounds;

                    bounds.Height = WindowRuntime.IsMemory(project, source) ? 240 : JarRuler.Height(project, free);
                    DesktopObserver.RestoreShape(handle, bounds, "Normal");
                }

                if (key == Keys.B && !Native.SetWindowPos(handle, 1, 0, 0, 0, 0, 0x13))
                {
                    throw new InvalidOperationException("Send to back failed.");
                }

                if (key is Keys.Left or Keys.Right)
                {
                    var bounds = source.Bounds;

                    bounds.Width = Math.Clamp(bounds.Width + (key == Keys.Left ? -1 : 1) * project.ValueStep, 180, 10000);
                    DesktopObserver.RestoreShape(handle, bounds, "Normal");
                }
            });
        }

        private void ShowRelationshipHelp()
        {
            using var help = new HelpWindow(project);
            help.ShowDialog(this);
        }
    }
}
