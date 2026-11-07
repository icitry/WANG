namespace WANG
{
    internal sealed partial class Studio : Form
    {
        private readonly DesktopObserver observer = new();
        private readonly System.Windows.Forms.Timer timer = new()
        {
            Interval = 200
        };

        private readonly System.Windows.Forms.Timer clock = new()
        {
            Interval = 16
        };

        private readonly Viewport viewport = new();
        private readonly Guide guide = new();
        private readonly ActivityLog activityLog = new();
        private readonly Dictionary<nint, string> discovered = [];
        private readonly Dictionary<Guid, (nint Handle, uint Process)> bindings = [];
        private readonly HashSet<Guid> closedBindings = [];
        private readonly Dictionary<Guid, Form> cards = [];
        private Project project = Relationships.Empty();
        private int activeWorkspace;
        private int nextAlias = 1;
        private bool busy;
        private bool running = true;
        private bool available;

        public Studio(string? initialProject = null)
        {
            Text = "WANG";
            Font = new("Segoe UI", 9);
            AutoScaleDimensions = new(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            SetupDesktopConsole();
            observer.Changed += ObserveEditEvent;
            timer.Tick += (_, _) => RefreshWindows();
            clock.Tick += (_, _) =>
            {
                if (pendingSourceRefresh && !busy)
                {
                    RefreshWindows();
                }

                if (running && available && nativeEdits.Count == 0)
                {
                    TickWindows();
                }
            };
            Shown += (_, _) =>
            {
                ConfigureDesktopMode();
                guide.Show();
                RefreshWindows();
                if (initialProject is not null)
                {
                    Safely(() => OpenProject(Project.Load(initialProject)));
                }
            };
            timer.Start();
            clock.Start();
            FormClosed += (_, _) =>
            {
                RevealReleasedWindows();
                timer.Dispose();
                clock.Dispose();
                observer.Dispose();
                historyTips.Dispose();
                guide.Dispose();
                viewport.Dispose();
                activityLog.Dispose();
                foreach (var card in cards.Values.ToArray())
                {
                    card.Dispose();
                }
            };
        }

        private void UpdateWorkspaceList()
        {
            UpdatePageControls();
            UpdateOverview();
        }

        private void SaveProject()
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "WANG program (*.wang)|*.wang",
                DefaultExt = "wang",
                AddExtension = true,
                FileName = "program.wang"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                SaveProject(dialog.FileName);
            }
        }

        private void SaveProject(string path)
        {
            RefreshWindows();
            project.Save(path);
        }

        private void Safely(Action action)
        {
            try
            {
                action();
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException
                or InvalidOperationException)
            {
                activityLog.Write(error switch
                {
                    UnauthorizedAccessException => "Access denied.",
                    System.Text.Json.JsonException => "Invalid JSON.",
                    FileNotFoundException => "File not found.",
                    DirectoryNotFoundException => "Directory not found.",
                    IOException => "I/O failed.",
                    _ => error.Message
                });
            }
        }

        private bool IsCard(nint handle)
        {
            if (IsHandleCreated && Handle == handle || guide.IsHandleCreated && guide.Handle == handle
                || viewport.IsHandleCreated && viewport.Handle == handle
                || activityLog.IsHandleCreated && activityLog.Handle == handle
                || pageOverview.IsHandleCreated && pageOverview.Handle == handle)
            {
                return false;
            }


            return cards.Values.Any(c => !c.IsDisposed && c.IsHandleCreated && c.Handle == handle)
                || project.Windows.Any(w => !cards.ContainsKey(w.Id) && !closedBindings.Contains(w.Id) && bindings.TryGetValue(w.Id, out var b)
                && b.Handle == handle
                && Alive(b));
        }

        private void NewCard(SourceWindow? source = null)
        {
            source ??= new()
            {
                Name = $"Piece {project.Windows.Count + 1}",
                Workspace = activeWorkspace,
                X = project.OriginX,
                Y = project.OriginY + project.Windows.Count(w => w.Workspace == activeWorkspace) * 64
            };
            if (!project.Windows.Contains(source))
            {
                project.Windows.Add(source);
            }

            var card = new HistoryCard(
                HandleHistoryShortcut,
                width => WindowValues.WidthValue(project, width),
                () => PrepareSourceResize(source.Id),
                address => WindowRuntime.IsInput(project, address))
            {
                Text = " ",
                ShowIcon = false,
                MaximizeBox = project.Version < 5,
                GeometryJarFeedback = project.Version >= 4,
                JarHeightStep = JarRuler.Step(project),
                MaximumJar = JarRuler.Maximum(project),
                FormBorderStyle = FormBorderStyle.Sizable,
                AutoScaleMode = AutoScaleMode.None,
                MinimumSize = new(180, Math.Max(120, SystemInformation.MinWindowTrackSize.Height)),
                Bounds = source.Bounds,
                StartPosition = FormStartPosition.Manual,
                BackColor = RelationshipAppearance.Color(Relationship.Number)
            };

            cards[source.Id] = card;
            card.HandleCreated += (_, _) =>
            {
                Native.GetWindowThreadProcessId(card.Handle, out var ownerProcess);
                Bind(source.Id, card.Handle, ownerProcess);
            };
            if (source.Workspace == activeWorkspace)
            {
                card.Show();
            }

            Native.GetWindowThreadProcessId(card.Handle, out var process);
            Bind(source.Id, card.Handle, process);
            if (source.State == "Minimized")
            {
                card.WindowState = FormWindowState.Minimized;
            }

            if (source.State == "Maximized")
            {
                card.WindowState = FormWindowState.Maximized;
            }

            if (DesktopObserver.ReadShape(card.Handle, out var effectiveBounds, out _))
            {
                source.Bounds = effectiveBounds;
            }
        }

        private void LoadProject()
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "WANG program (*.wang)|*.wang|Earlier desktop save (*.json)|*.json"
            };

            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                OpenProject(Project.Load(dialog.FileName));
            }
        }

        private void OpenProject(Project loaded)
        {
            loaded.Validate();
            loaded = JarRuler.Widen(loaded);
            var retainedJarRuler = loaded.Version == 5;

            busy = true;
            running = false;
            windowRuntime.Reset();
            livePrinted.Clear();
            liveAvailable = null;
            pilePosition = null;
            RevealReleasedWindows();
            foreach (var old in bindings.Where(b => !cards.ContainsKey(b.Key)))
            {
                releasedDesktopWindows.Add(old.Value.Handle);
            }

            foreach (var card in cards.Values.ToArray())
            {
                card.Dispose();
            }

            cards.Clear();
            bindings.Clear();
            bindingArchive.Clear();
            closedBindings.Clear();
            nativeEdits.Clear();
            sourceGestures.Clear();
            finishedEdits.Clear();
            observedStates.Clear();
            project = loaded;
            activeWorkspace = 0;
            lastDesktopSource = null;
            liveResult = null;
            livePublished = null;
            forceLiveOutput = false;
            UpdateWorkspaceList();
            foreach (var source in project.Windows.ToArray())
            {
                NewCard(source);
            }

            if (project.Version >= 3)
            {
                RestoreSourceOrder();
                running = true;
            }

            busy = false;
            historySuppression++;
            try
            {
                RefreshWindows();
                history.Reset(CaptureEdit());
            }
            finally
            {
                historySuppression--;
            }

            UpdateHistoryControls();
            ConfigureDesktopMode();
            if (retainedJarRuler)
            {
                activityLog.Write("Legacy jar spacing retained.");
            }
        }

        private void RefreshWindows()
        {
            if (busy)
            {
                return;
            }

            pendingSourceRefresh = false;
            RemoveClosedSources();
            if (historySuppression == 0)
            {
                DrainFinishedEdits();
            }

            observedWindows = DesktopObserver.Snapshot(handle => !guide.IsHandleCreated || handle != guide.Handle, false);
            var snapshots = observedWindows.Where(window => window.ProcessId != Environment.ProcessId || IsCard(window.Handle)).ToList();

            foreach (var window in snapshots)
            {
                var isNew = !discovered.TryGetValue(window.Handle, out var alias);

                if (isNew)
                {
                    discovered[window.Handle] = alias = $"Window {nextAlias++}";
                }

                alias ??= "Window";
                var source = project.Windows.FirstOrDefault(w => !closedBindings.Contains(w.Id) && bindings.TryGetValue(w.Id, out var binding)
                    && binding.Handle == window.Handle
                    && binding.Process == window.ProcessId);

                if (source is null && isNew && discoveryReady && !releasedDesktopWindows.Contains(window.Handle))
                {
                    source = new()
                    {
                        Name = alias,
                        Workspace = activeWorkspace,
                        Bounds = window.NormalBounds,
                        State = window.State
                    };
                    project.Windows.Add(source);
                    Bind(source.Id, window.Handle, window.ProcessId);
                }

                if (source is not null)
                {
                    source.Bounds = window.NormalBounds;
                    source.State = window.State;
                }

                if (source is not null && window.Focused)
                {
                    lastDesktopSource = source.Id;
                }
            }

            foreach (var handle in discovered.Keys.Except(snapshots.Select(w => w.Handle)).ToArray())
            {
                discovered.Remove(handle);
            }

            releasedDesktopWindows.RemoveWhere(handle => !Native.IsWindow(handle));
            discoveryReady = true;
            if (project.Version >= 3)
            {
                ReadSourceOrder(snapshots);
            }

            var missing = project.Windows.Where(w => closedBindings.Contains(w.Id) || !bindings.TryGetValue(w.Id, out var b) || !Native.IsWindow(b.Handle)
                || Native.GetWindowThreadProcessId(b.Handle, out var currentProcess) == 0
                || currentProcess != b.Process)
                .ToArray();

            available = missing.Length == 0;
            if (project.Version >= 3)
            {
                RefreshRelationships(missing.Select(w => w.Id).ToHashSet());
            }

            if (observer.CallbackError is not null)
            {
                running = false;
                available = false;
            }

            UpdateGuide();
            if (historySuppression == 0)
            {
                history.Observe(CaptureEdit(), Environment.TickCount64, nativeEdits.Count > 0);
            }

            UpdateHistoryControls();
            UpdateDesktopConsole();
        }
    }
}
