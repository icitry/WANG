namespace WANG
{
    internal sealed partial class Studio
    {
        private sealed record SourceGesture(Size Size, int Workspace, Guid[] Order, bool Resizing);

        private readonly Dictionary<Guid, SourceGesture> sourceGestures = [];

        private void PrepareSourceResize(Guid id)
        {
            if (busy || historySuppression > 0 || project.Version < 4 || sourceGestures.ContainsKey(id))
            {
                return;
            }

            var source = project.Windows.FirstOrDefault(w => w.Id == id);

            if (source is null || !bindings.TryGetValue(id, out var binding)
                || !DesktopObserver.ReadShape(binding.Handle, out var bounds, out var state) || state != "Normal")
            {
                return;
            }

            ReadSourceOrder(DesktopObserver.Snapshot(IsCard, false));
            sourceGestures[id] = new(bounds.Size, source.Workspace, SourceOrder(source.Workspace), true);
        }

        private Guid[] SourceOrder(int page)
        {
            return project.Windows.Where(w => w.Workspace == page).OrderBy(w => w.ZOrder).Select(w => w.Id).ToArray();
        }

        private void BeginSourceGesture(SourceWindow source, Rectangle bounds)
        {
            if (project.Version >= 4)
            {
                sourceGestures.TryAdd(source.Id, new(bounds.Size, source.Workspace, SourceOrder(source.Workspace), false));
            }
        }

        private void FinishSourceGesture(SourceWindow source, Rectangle bounds, string state)
        {
            if (!sourceGestures.Remove(source.Id, out var gesture) || state != "Normal"
                || !gesture.Resizing && gesture.Size == bounds.Size)
            {
                return;
            }

            var page = project.Windows.Where(w => w.Workspace == gesture.Workspace).ToDictionary(w => w.Id);
            var rank = 0;

            foreach (var id in gesture.Order)
            {
                if (page.Remove(id, out var window))
                {
                    window.ZOrder = rank++;
                }
            }

            foreach (var window in page.Values.OrderBy(w => w.ZOrder))
            {
                window.ZOrder = rank++;
            }

            if (gesture.Workspace == activeWorkspace)
            {
                RestoreSourceOrder();
            }
        }
    }
}
