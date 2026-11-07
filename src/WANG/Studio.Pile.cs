namespace WANG
{
    internal sealed partial class Studio
    {
        private Point? pilePosition;

        private void EditPile(bool push, nint? focused)
        {
            var source = DesktopTarget(focused);
            var value = liveResult?.Windows.GetValueOrDefault(source.Id)?.Value ?? WindowValues.Value(project, source);

            if (!double.IsFinite(value))
            {
                throw new InvalidOperationException("Invalid pile value.");
            }

            if (!push)
            {
                if (livePrinted.Count == 128)
                {
                    livePrinted.RemoveAt(0);
                }

                livePrinted.Add(value);
                Native.ShowWindow(bindings[source.Id].Handle, 7);
                running = false;
                forceLiveOutput = true;

                return;
            }

            var existing = project.Windows.Where(w => w.Workspace == activeWorkspace && w.State != "Minimized")
                .Where(w => project.Windows.Any(other => other.Workspace == w.Workspace && other.Id != w.Id && other.State != "Minimized"
                && Math.Abs(w.X - other.X) <= 24
                && Math.Abs(w.Y - other.Y) <= 24))
                .OrderBy(w => w.ZOrder)
                .FirstOrDefault();
            var position = existing is not null
                ? new Point(existing.X + 18, existing.Y + 18)
                : pilePosition is Point last ? new(last.X + 18, last.Y + 18) : new Point(80, 180);
            var slot = Enumerable.Range(0, JarRuler.Maximum(project) + 1)
                .Where(index => !WindowRuntime.IsInput(project, index))
                .First(n => project.Windows.All(w => !WindowRuntime.IsMemory(project, w) || WindowRuntime.Address(project, w) != n));

            if (value < (180 - project.ZeroWidth) / (double)project.ValueStep
                || value > (10000 - project.ZeroWidth) / (double)project.ValueStep)
            {
                throw new InvalidOperationException("Value exceeds window width range.");
            }

            var width = project.ZeroWidth + (int)Math.Round(value, MidpointRounding.AwayFromZero) * project.ValueStep;

            if (width is < 180 or > 10000)
            {
                throw new InvalidOperationException("Value exceeds window width range.");
            }

            var item = new SourceWindow
            {
                Name = "Pile item",
                Workspace = activeWorkspace,
                X = position.X,
                Y = position.Y,
                Width = width,
                Height = JarRuler.Height(project, slot)
            };

            windowRuntime.Memory[slot] = value;
            NewCard(item);
            pilePosition = position;
            lastDesktopSource = item.Id;
        }
    }
}
