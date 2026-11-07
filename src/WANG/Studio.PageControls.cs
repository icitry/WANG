namespace WANG
{
    internal sealed partial class Studio
    {
        private readonly Label pagePosition = new()
        {
            Width = ControlWidth,
            Height = 20,
            Margin = new(0, 0, 0, 4)
        };

        private readonly ComboBox pagePicker = new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            MaxDropDownItems = 12,
            IntegralHeight = false,
            DropDownHeight = 280
        };

        private readonly Button previousPage = new()
        {
            Text = "Previous"
        };

        private readonly Button nextPage = new()
        {
            Text = "Next"
        };

        private readonly Button newPage = new()
        {
            Text = "New page"
        };

        private bool updatingPages;
        private string pageListSignature = "";
        private nint lastDesktopWindow;
        private readonly ContextMenuStrip sourceActions = new();

        private sealed record PageChoice(int Id, string Label)
        {
            public override string ToString()
            {
                return Label;
            }
        }

        private void SetupPageControls()
        {
            Section("Pages");
            desktopConsole.Controls.Add(pagePosition);
            ControlRow(pagePicker);
            ControlRow(previousPage, nextPage);
            ControlRow(newPage, ActionButton("Rename page", ShowPageNameDialog));
            ControlRow(ActionButton("All pages overview", ShowOverview));
            SetupOverview();
            previousPage.Click += (_, _) => Safely(() => NavigateRoutine(-1));
            nextPage.Click += (_, _) => Safely(() => NavigateRoutine(1));
            newPage.Click += (_, _) => Safely(CreatePage);
            pagePicker.SelectedIndexChanged += (_, _) =>
            {
                if (!updatingPages && pagePicker.SelectedItem is PageChoice choice)
                {
                    Safely(() => SetRoutine(choice.Id));
                }
            };
            sourceActions.Items.Add("Capture / release last focused app", null, (_, _) => Safely(CaptureLastWindow));
            sourceActions.Items.Add("Send selected source to back", null, (_, _) => Safely(() => DesktopKey(Keys.B)));
            sourceActions.Items.Add("Push selected value", null, (_, _) => Safely(() => DesktopKey(Keys.P)));
            sourceActions.Items.Add("Pop selected value", null, (_, _) => Safely(() => DesktopKey(Keys.O)));
            sourceActions.Items.Add("Remove selected source", null, (_, _) => Safely(() => DesktopKey(Keys.Delete)));
            FormClosed += (_, _) => sourceActions.Dispose();
        }

        private void UpdatePageControls()
        {
            var pages = project.Workspaces.OrderBy(page => page.Key).ToArray();
            var index = Array.FindIndex(pages, page => page.Key == activeWorkspace);

            pagePosition.Text = $"Page {index + 1} of {pages.Length}";
            previousPage.Enabled = index > 0;
            nextPage.Enabled = index >= 0 && index < pages.Length - 1;
            newPage.Enabled = pages.Length < 512;
            updatingPages = true;
            try
            {
                var signature = string.Join("\n", pages.Select(page => $"{page.Key}:{page.Value}"));

                if (signature != pageListSignature)
                {
                    pageListSignature = signature;
                    pagePicker.Items.Clear();
                    for (var i = 0; i < pages.Length; i++)
                    {
                        pagePicker.Items.Add(new PageChoice(pages[i].Key, $"{i + 1}. {pages[i].Value}"));
                    }
                }

                pagePicker.SelectedIndex = index;
            }
            finally
            {
                updatingPages = false;
            }
        }

        private void CreatePage()
        {
            EditSource("Create a page", () =>
            {
                var id = Enumerable.Range(1, 511).Where(index => !project.Workspaces.ContainsKey(index))
                    .Select(index => (int?)index)
                    .FirstOrDefault() ?? throw new InvalidOperationException("Page limit reached.");

                project.Workspaces[id] = $"Page {project.Workspaces.Count + 1}";
                UpdateWorkspaceList();
                SetRoutine(id);
            });
        }

        private void ShowPageNameDialog()
        {
            using var dialog = new Form
            {
                Text = "Rename page",
                ClientSize = new(350, 140),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = FormStartPosition.CenterParent,
                Font = Font
            };
            var name = new TextBox
            {
                Text = project.Workspaces[activeWorkspace],
                MaxLength = 60,
                Bounds = new(16, 42, 318, 28)
            };
            var accept = new Button
            {
                Text = "Rename",
                Bounds = new(130, 94, 98, 32),
                DialogResult = DialogResult.OK
            };
            var cancel = new Button
            {
                Text = "Cancel",
                Bounds = new(236, 94, 98, 32),
                DialogResult = DialogResult.Cancel
            };

            dialog.Controls.Add(new Label
            {
                Text = "Page name",
                AutoSize = true,
                Location = new(16, 16)
            });
            dialog.Controls.Add(name);
            dialog.Controls.Add(accept);
            dialog.Controls.Add(cancel);
            dialog.AcceptButton = accept;
            dialog.CancelButton = cancel;
            name.TextChanged += (_, _) => accept.Enabled = !string.IsNullOrWhiteSpace(name.Text);
            name.SelectAll();
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                RenamePage(name.Text);
            }
        }

        private void RenamePage(string name)
        {
            name = name.Trim();
            if (name.Length is < 1 or > 60)
            {
                throw new ArgumentException("Page name: 1-60 characters.");
            }

            if (project.Workspaces[activeWorkspace] == name)
            {
                return;
            }

            EditSource("Rename a page", () =>
            {
                project.Workspaces[activeWorkspace] = name;
                UpdateWorkspaceList();
            });
        }

        private void CaptureLastWindow()
        {
            if (lastDesktopWindow == 0 || !Native.IsWindow(lastDesktopWindow))
            {
                throw new InvalidOperationException("Focus a window before capturing.");
            }

            EditSource("Capture or release a window", () => CaptureDesktopWindow(lastDesktopWindow));
        }
    }
}
