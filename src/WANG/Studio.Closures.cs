namespace WANG
{
    internal sealed partial class Studio
    {
        private void RemoveClosedSources()
        {
            if (project.Version < 3)
            {
                return;
            }

            var closed = project.Windows.Where(w => closedBindings.Contains(w.Id)
                || cards.TryGetValue(w.Id, out var card) && card.IsDisposed
                || bindings.TryGetValue(w.Id, out var binding) && !Alive(binding)).ToArray();

            if (closed.Length == 0)
            {
                return;
            }

            var ids = closed.Select(w => w.Id).ToHashSet();

            foreach (var edit in finishedEdits.Where(edit => ids.Contains(edit.Id)))
            {
                var source = closed.Single(w => w.Id == edit.Id);

                if (edit.Bounds is Rectangle bounds)
                {
                    source.Bounds = bounds;
                }

                if (edit.State is not null)
                {
                    source.State = edit.State;
                }
            }

            if (historySuppression == 0)
            {
                history.Record(EditSnapshot.Capture(project, CaptureEdit().Missing.Except(ids), activeWorkspace));
            }

            foreach (var source in closed)
            {
                project.Windows.Remove(source);
                closedBindings.Add(source.Id);
                observedStates.Remove(source.Id);
                sourceGestures.Remove(source.Id);
                if (bindings.Remove(source.Id, out var binding))
                {
                    nativeEdits.Remove(binding.Handle);
                    if (lastDesktopWindow == binding.Handle)
                    {
                        lastDesktopWindow = 0;
                    }
                }

                if (cards.Remove(source.Id, out var card))
                {
                    card.Dispose();
                }

                if (lastDesktopSource == source.Id)
                {
                    lastDesktopSource = null;
                }
            }

            if (historySuppression == 0)
            {
                ReadSourceOrder(DesktopObserver.Snapshot(IsCard, false));
                history.Record(CaptureEdit(), closed.Length == 1 ? "Close a program window" : "Close program windows");
            }
        }
    }
}
