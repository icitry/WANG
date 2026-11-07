namespace WANG
{
    internal sealed partial class Studio
    {
        private sealed class HistoryCard : Form
        {
            private readonly Func<Keys, bool> shortcut;
            private readonly Func<int, int> widthValue;
            private readonly Action beforeResize;
            private readonly Func<int, bool> inputJar;

            internal HistoryCard(Func<Keys, bool> shortcut, Func<int, int> widthValue, Action beforeResize, Func<int, bool> inputJar)
            {
                this.shortcut = shortcut;
                this.widthValue = widthValue;
                this.beforeResize = beforeResize;
                this.inputJar = inputJar;
                ResizeRedraw = true;
                DoubleBuffered = true;
            }

            internal string? CenterValue;
            internal bool GeometryJarFeedback;
            internal int JarHeightStep = 1;
            internal int MaximumJar = JarRuler.MaximumIndex;
            internal string JarNote = "";

            internal string StartingValueText
            {
                get
                {
                    if (!GeometryJarFeedback || WindowState != FormWindowState.Normal
                        || !JarRuler.Contains(Height, JarHeightStep, MaximumJar)
                        || inputJar(JarRuler.Index(Height, JarHeightStep)))
                    {
                        return "";
                    }

                    return $"Start: {widthValue(Width)}";
                }
            }

            protected override void OnResize(EventArgs e)
            {
                base.OnResize(e);
                if (!GeometryJarFeedback || WindowState != FormWindowState.Normal)
                {
                    return;
                }

                var jar = JarRuler.Contains(Height, JarHeightStep, MaximumJar);

                JarNote = jar ? $"Jar {JarRuler.Index(Height, JarHeightStep)}" : "";
                BackColor = RelationshipAppearance.Color(jar ? Relationship.Memory : Relationship.Number);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                base.OnPaint(e);
                if (CenterValue is null)
                {
                    return;
                }

                using var font = new Font("Segoe UI", 24, FontStyle.Bold);

                TextRenderer.DrawText(
                    e.Graphics,
                    CenterValue,
                    font,
                    ClientRectangle,
                    Color.FromArgb(24, 30, 42),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                var startingValue = StartingValueText;

                if (startingValue.Length > 0)
                {
                    using var startingFont = new Font("Segoe UI", 11);

                    TextRenderer.DrawText(
                        e.Graphics,
                        startingValue,
                        startingFont,
                        new Rectangle(8, 8, Math.Max(0, ClientSize.Width - 16), 28),
                        Color.FromArgb(34, 80, 80),
                        TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }

                if (JarNote.Length > 0)
                {
                    using var noteFont = new Font("Segoe UI", 9);

                    TextRenderer.DrawText(
                        e.Graphics,
                        JarNote,
                        noteFont,
                        new Rectangle(8, Math.Max(0, ClientSize.Height - 24), Math.Max(0, ClientSize.Width - 16), 20),
                        Color.FromArgb(34, 80, 80),
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
            }

            protected override void WndProc(ref Message message)
            {
                var hit = message.Msg == 0x21 ? (int)((long)message.LParam & 0xffff) : (int)message.WParam;
                var borderPress = message.Msg == 0xa1
                    || message.Msg == 0x21 && ((long)message.LParam >> 16 & 0xffff) == 0xa1;

                if (borderPress && hit is >= 10 and <= 17)
                {
                    beforeResize();
                }

                base.WndProc(ref message);
            }

            protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
            {
                return shortcut(keyData) || base.ProcessCmdKey(ref msg, keyData);
            }
        }

        private sealed record FinishedEdit(Guid Id, Rectangle? Bounds, string? State, bool Begin, string Description);

        private readonly EditHistory history = new();
        private readonly ToolTip historyTips = new();
        private readonly Dictionary<Guid, (nint Handle, uint Process)> bindingArchive = [];
        private readonly HashSet<nint> nativeEdits = [];
        private readonly Queue<FinishedEdit> finishedEdits = [];
        private readonly Dictionary<Guid, string> observedStates = [];
        private int historySuppression;

        private void UpdateHistoryControls()
        {
            desktopUndo.Enabled = history.CanUndo;
            desktopRedo.Enabled = history.CanRedo;
            historyTips.SetToolTip(desktopUndo, $"Undo: {history.UndoDescription} (Ctrl+Z)");
            historyTips.SetToolTip(desktopRedo, $"Redo: {history.RedoDescription} (Ctrl+Y)");
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            return HandleHistoryShortcut(keyData) || base.ProcessCmdKey(ref msg, keyData);
        }

        private bool HandleHistoryShortcut(Keys keyData)
        {
            if (keyData is not (Keys.Control | Keys.Z) and not (Keys.Control | Keys.Y) and not (Keys.Control | Keys.Shift | Keys.Z))
            {
                return false;
            }

            Safely(() => NavigateHistory(keyData != (Keys.Control | Keys.Z)));

            return true;
        }

        private void Bind(Guid id, nint handle, uint process)
        {
            if (bindings.TryGetValue(id, out var previous) && previous.Handle != handle)
            {
                nativeEdits.Remove(previous.Handle);
                sourceGestures.Remove(id);
            }

            bindings[id] = bindingArchive[id] = (handle, process);
            releasedDesktopWindows.Remove(handle);
            closedBindings.Remove(id);
        }

        private static bool Alive((nint Handle, uint Process) binding)
        {
            return binding.Handle != 0 && Native.IsWindow(binding.Handle)
                && Native.GetWindowThreadProcessId(binding.Handle, out var process) != 0
                && process == binding.Process;
        }

        private EditSnapshot CaptureEdit()
        {


            return EditSnapshot.Capture(
                project,
                project.Windows.Where(w => closedBindings.Contains(w.Id) || !bindings.TryGetValue(w.Id, out var b) || !Alive(b))
                    .Select(w => w.Id),
                activeWorkspace);
        }

        private void ObserveEditEvent(uint type, nint handle)
        {
            if (type == 3)
            {
                pendingSourceRefresh = true;
            }

            if (type == 3
                && (IsCard(handle)
                || Native.GetWindowThreadProcessId(handle, out var foregroundProcess) != 0
                && foregroundProcess != Environment.ProcessId))
            {
                lastDesktopWindow = handle;
            }

            if (type == 0x8001 && !busy)
            {
                foreach (var id in bindingArchive.Where(b => b.Value.Handle == handle).Select(b => b.Key).ToArray())
                {
                    if (Alive(bindingArchive[id]))
                    {
                        continue;
                    }

                    closedBindings.Add(id);
                    nativeEdits.Remove(handle);
                    if (historySuppression == 0 && bindings.ContainsKey(id) && project.Windows.Any(w => w.Id == id))
                    {
                        finishedEdits.Enqueue(new(id, null, null, false, "Close a program window"));
                    }
                }
            }

            if (busy || historySuppression > 0)
            {
                return;
            }

            var source = project.Windows.FirstOrDefault(w => bindings.TryGetValue(w.Id, out var b) && b.Handle == handle && !closedBindings.Contains(w.Id));

            if (source is null || !DesktopObserver.ReadShape(handle, out var bounds, out var state))
            {
                return;
            }

            if (type is 10 or 11 or 22 or 23 or 0x800A or 0x800B)
            {
                pendingSourceRefresh = true;
            }

            if (type == 10)
            {
                BeginSourceGesture(source, bounds);
                nativeEdits.Add(handle);
                finishedEdits.Enqueue(new(source.Id, bounds, state, true, "Before moving a window"));
            }

            if (type == 11)
            {
                FinishSourceGesture(source, bounds, state);
                nativeEdits.Remove(handle);
                finishedEdits.Enqueue(new(source.Id, bounds, state, false, "Move or resize a window"));
            }

            var previous = observedStates.GetValueOrDefault(source.Id, source.State);

            if (type is 22 or 23 or 0x800A or 0x800B && !nativeEdits.Contains(handle) && previous != state)
            {
                finishedEdits.Enqueue(new(source.Id, bounds, state, false, "Change a window's state"));
            }

            observedStates[source.Id] = state;
        }

        private void DrainFinishedEdits()
        {
            while (finishedEdits.TryDequeue(out var edit))
            {
                var source = project.Windows.FirstOrDefault(w => w.Id == edit.Id);

                if (source is null)
                {
                    continue;
                }

                if (edit.Bounds is Rectangle bounds)
                {
                    source.Bounds = bounds;
                }

                if (edit.State is not null)
                {
                    source.State = edit.State;
                }

                history.Record(CaptureEdit(), edit.Begin ? "Edit arrangement" : edit.Description);
            }
        }

        private void EditSource(string description, Action action)
        {
            RefreshWindows();
            history.Record(CaptureEdit());
            historySuppression++;
            try
            {
                action();
            }
            finally
            {
                try
                {
                    RefreshWindows();
                    history.Record(CaptureEdit(), description);
                }
                finally
                {
                    historySuppression--;
                    UpdateHistoryControls();
                }
            }
        }

        private void NavigateHistory(bool forward)
        {
            running = false;
            RefreshWindows();
            history.Record(CaptureEdit());
            var target = history.Peek(forward);

            if (target is null)
            {
                UpdateHistoryControls();

                return;
            }

            var before = CaptureEdit();

            try
            {
                RestoreEdit(target);
            }
            catch (InvalidOperationException error)
            {
                try
                {
                    RestoreEdit(before);
                }
                catch (InvalidOperationException)
                {
                    activityLog.Write("Rollback failed. Paused.");
                }

                throw new InvalidOperationException("Restore failed. History retained.", error);
            }

            history.Accept(forward);
            running = false;
            UpdateHistoryControls();
            Activate();
        }

        private void RestoreEdit(EditSnapshot snapshot)
        {
            var restored = snapshot.RestoreProject();
            var missing = snapshot.Missing.ToHashSet();
            var ids = restored.Windows.Select(w => w.Id).ToHashSet();

            busy = true;
            historySuppression++;

            try
            {
                nativeEdits.Clear();
                sourceGestures.Clear();
                finishedEdits.Clear();
                observedStates.Clear();
                foreach (var id in cards.Keys.Where(id => !ids.Contains(id) || missing.Contains(id)).ToArray())
                {
                    var card = cards[id];

                    cards.Remove(id);
                    card.Dispose();
                }

                foreach (var id in bindings.Keys.Where(id => !ids.Contains(id) || missing.Contains(id)).ToArray())
                {
                    if (project.Version >= 2)
                    {
                        releasedDesktopWindows.Add(bindings[id].Handle);
                    }

                    if (!cards.ContainsKey(id) && Alive(bindings[id]) && !Native.IsWindowVisible(bindings[id].Handle))
                    {
                        Native.ShowWindow(bindings[id].Handle, 4);
                    }

                    bindings.Remove(id);
                }

                project = restored;
                activeWorkspace = restored.Workspaces.ContainsKey(snapshot.Workspace) ? snapshot.Workspace : 0;
                UpdateWorkspaceList();
                foreach (var source in project.Windows)
                {
                    if (missing.Contains(source.Id))
                    {
                        closedBindings.Add(source.Id);
                        continue;
                    }

                    if (!bindings.TryGetValue(source.Id, out var binding) || closedBindings.Contains(source.Id)
                        || !Alive(binding))
                    {
                        if (!closedBindings.Contains(source.Id) && bindingArchive.TryGetValue(source.Id, out var archived)
                            && Alive(archived))
                        {
                            Bind(source.Id, archived.Handle, archived.Process);
                        }
                        else
                        {
                            NewCard(source);
                        }
                    }

                    binding = bindings[source.Id];
                    if (cards.TryGetValue(source.Id, out var owned))
                    {
                        owned.FormBorderStyle = FormBorderStyle.Sizable;
                        owned.Text = " ";
                        owned.BackColor = RelationshipAppearance.Color(Relationship.Number);
                    }

                    binding = bindings[source.Id];
                    if (!DesktopObserver.ReadShape(binding.Handle, out var bounds, out var state) || bounds != source.Bounds
                        || state != source.State)
                    {
                        DesktopObserver.RestoreShape(binding.Handle, source.Bounds, source.State);
                    }

                    if (owned is not null)
                    {
                        if (source.Workspace == activeWorkspace)
                        {
                            owned.Show();
                        }
                        else
                        {
                            owned.Hide();
                        }
                    }

                    observedStates[source.Id] = source.State;
                }

                if (project.Version >= 3)
                {
                    RestoreSourceOrder();
                }
            }
            finally
            {
                busy = false;
                historySuppression--;
            }

            historySuppression++;
            try
            {
                RefreshWindows();
                if (!snapshot.SameArrangement(CaptureEdit()))
                {
                    throw new InvalidOperationException("Window geometry rejected. Check size limits.");
                }
            }
            finally
            {
                historySuppression--;
            }
        }
    }
}
