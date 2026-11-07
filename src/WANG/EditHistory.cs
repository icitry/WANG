namespace WANG
{
    internal sealed record EditSnapshot(string Source, Guid[] Missing, int Workspace)
    {
        public static EditSnapshot Capture(Project project, IEnumerable<Guid> missing, int workspace)
        {
            return new(project.Capture(), missing.Order().ToArray(), workspace);
        }

        public bool SameArrangement(EditSnapshot other)
        {
            return Source == other.Source && Missing.SequenceEqual(other.Missing);
        }

        public Project RestoreProject()
        {
            return Project.FromCapture(Source);
        }
    }

    internal sealed class EditHistory(int capacity = 100, long settleMilliseconds = 400)
    {
        private sealed record Edit(EditSnapshot Before, EditSnapshot After, string Description);

        private readonly List<Edit> undo = [];
        private readonly List<Edit> redo = [];
        private EditSnapshot? current;
        private EditSnapshot? pending;
        private long pendingSince;

        public bool CanUndo => undo.Count > 0 || pending is not null;
        public bool CanRedo => redo.Count > 0 && pending is null;
        public string UndoDescription => undo.LastOrDefault()?.Description ?? "Edit arrangement";
        public string RedoDescription => redo.LastOrDefault()?.Description ?? "Edit arrangement";

        public void Reset(EditSnapshot snapshot)
        {
            current = snapshot;
            pending = null;
            undo.Clear();
            redo.Clear();
        }

        public void Observe(EditSnapshot snapshot, long now, bool gestureInProgress)
        {
            if (current is null)
            {
                Reset(snapshot);

                return;
            }

            if (current.SameArrangement(snapshot))
            {
                current = snapshot;
                pending = null;

                return;
            }

            if (pending is null || !pending.SameArrangement(snapshot))
            {
                pending = snapshot;
                pendingSince = now;
            }

            if (!gestureInProgress && now - pendingSince >= settleMilliseconds)
            {
                Record(snapshot);
            }
        }

        public void Record(EditSnapshot snapshot, string description = "Edit arrangement")
        {
            pending = null;
            if (current is null)
            {
                Reset(snapshot);

                return;
            }

            if (current.SameArrangement(snapshot))
            {
                current = snapshot;

                return;
            }

            undo.Add(new(current, snapshot, description));
            if (undo.Count > Math.Max(1, capacity))
            {
                undo.RemoveAt(0);
            }

            current = snapshot;
            redo.Clear();
        }

        public EditSnapshot? Peek(bool forward)
        {
            return forward ? redo.LastOrDefault()?.After : undo.LastOrDefault()?.Before;
        }

        public void Accept(bool forward)
        {
            var from = forward ? redo : undo;
            var to = forward ? undo : redo;

            if (from.Count == 0)
            {
                return;
            }

            var edit = from[^1];

            from.RemoveAt(from.Count - 1);
            to.Add(edit);
            current = forward ? edit.After : edit.Before;
            pending = null;
        }
    }
}
