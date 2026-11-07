namespace WANG
{
    internal sealed partial class Studio
    {
        private void RevealReleasedWindows()
        {
            foreach (var source in project.Windows)
            {
                if (!cards.ContainsKey(source.Id) && bindings.TryGetValue(source.Id, out var binding) && Alive(binding)
                    && !Native.IsWindowVisible(binding.Handle))
                {
                    Native.ShowWindow(binding.Handle, source.State == "Minimized" ? 7 : source.State == "Maximized" ? 3 : 4);
                }
            }
        }

        private SourceWindow DesktopTarget(nint? focused = null)
        {
            var foreground = focused ?? Native.GetForegroundWindow();
            var source = project.Windows.FirstOrDefault(w => bindings.TryGetValue(w.Id, out var b) && b.Handle == foreground);

            if (source is null && foreground == Handle && lastDesktopSource is Guid id)
            {
                source = project.Windows.FirstOrDefault(w => w.Id == id);
            }

            if (source is null)
            {
                throw new InvalidOperationException("Select a source first.");
            }

            lastDesktopSource = source.Id;

            return source;
        }

        private void CaptureDesktopWindow(nint? focused = null)
        {
            var handle = focused ?? Native.GetForegroundWindow();
            var source = project.Windows.FirstOrDefault(w => bindings.TryGetValue(w.Id, out var b) && b.Handle == handle);

            if (source is not null)
            {
                releasedDesktopWindows.Add(handle);
                project.Windows.Remove(source);
                bindings.Remove(source.Id);
                if (cards.Remove(source.Id, out var owned))
                {
                    owned.Dispose();
                }


                return;
            }

            var info = DesktopObserver.Snapshot(IsCard, false).FirstOrDefault(w => w.Handle == handle);

            if (info is null)
            {
                throw new InvalidOperationException("No app selected.");
            }

            source = new()
            {
                Name = discovered.GetValueOrDefault(handle, $"Window {nextAlias++}"),
                Workspace = activeWorkspace,
                Bounds = info.NormalBounds,
                State = info.State
            };
            project.Windows.Add(source);
            Bind(source.Id, handle, info.ProcessId);
            lastDesktopSource = source.Id;
        }

        private void NavigateRoutine(int direction)
        {
            var pages = project.Workspaces.Keys.Order().ToArray();
            var index = Array.IndexOf(pages, activeWorkspace);

            SetRoutine(pages[Math.Clamp(index + direction, 0, pages.Length - 1)]);
        }

        private void NewDesktopWindow()
        {
            NewDesktopWindow(Cursor.Position);
        }

        private void NewDesktopWindow(Point position)
        {
            var area = Screen.FromPoint(position).WorkingArea;
            var source = new SourceWindow
            {
                Name = $"Window {nextAlias++}",
                Workspace = activeWorkspace,
                Width = Math.Max(Relationships.NewWindowWidth, project.ZeroWidth),
                Height = Relationships.NewWindowHeight
            };

            source.X = Math.Clamp(position.X, area.Left + 10, Math.Max(area.Left + 10, area.Right - source.Width - 10));
            source.Y = Math.Clamp(position.Y, area.Top + 35, Math.Max(area.Top + 35, area.Bottom - source.Height - 10));
            NewCard(source);
            lastDesktopSource = source.Id;
        }

        private void DesktopKey(Keys key, nint? focused = null, Point? spawn = null)
        {
            RelationshipKey(key, focused, spawn);
        }

        private void SetRoutine(int id)
        {
            if (id == activeWorkspace || !project.Workspaces.ContainsKey(id))
            {
                UpdatePageControls();

                return;
            }

            RefreshWindows();
            var previous = activeWorkspace;

            activeWorkspace = id;
            busy = true;
            try
            {
                foreach (var source in project.Windows.Where(w => w.Workspace == previous || w.Workspace == id))
                {
                    if (cards.TryGetValue(source.Id, out var card) && !card.IsDisposed)
                    {
                        if (source.Workspace == id)
                        {
                            card.Show();
                        }
                        else
                        {
                            card.Hide();
                        }
                    }
                    else if (bindings.TryGetValue(source.Id, out var binding) && Alive(binding))
                    {
                        Native.ShowWindow(
                            binding.Handle,
                            source.Workspace != id
                                ? 0
                                : source.State == "Minimized" ? 7 : source.State == "Maximized" ? 3 : 4);
                    }
                }

                RestoreSourceOrder();
            }
            finally
            {
                busy = false;
            }

            lastDesktopSource = null;
            RefreshWindows();
        }
    }
}
