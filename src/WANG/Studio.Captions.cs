namespace WANG
{
    internal sealed partial class Studio
    {
        private List<WindowInfo> observedWindows = [];
        private bool pendingSourceRefresh;

        private void UpdateGuide()
        {
            if (liveResult is null || cards.Count == project.Windows.Count)
            {
                guide.UpdateCaptions([]);

                return;
            }

            var snapshots = observedWindows.ToDictionary(window => window.Handle);
            var connections = windowRuntime.Connections.GroupBy(connection => connection.Jar)
                .ToDictionary(group => group.Key, group => group.First());
            var captions = new List<WindowCaption>();

            foreach (var source in project.Windows.Where(window => window.Workspace == activeWorkspace && window.State != "Minimized" && !cards.ContainsKey(window.Id)))
            {
                if (!bindings.TryGetValue(source.Id, out var binding)
                    || !snapshots.TryGetValue(binding.Handle, out var snapshot)
                    || !liveResult.Windows.TryGetValue(source.Id, out var value))
                {
                    continue;
                }

                var occluders = observedWindows.Where(window => window.Order < snapshot.Order && window.State != "Minimized"
                    && window.Bounds.IntersectsWith(snapshot.Bounds))
                    .Select(window => window.Bounds)
                    .ToArray();
                var text = WindowCaption.Describe(project, source, value, connections.GetValueOrDefault(source.Id));
                var color = RelationshipAppearance.Color(WindowRuntime.IsMemory(project, source) ? Relationship.Memory : value.Relationship);

                captions.Add(new(source.Id, snapshot.Bounds, text, color, occluders));
            }

            guide.UpdateCaptions(captions);
        }
    }
}
