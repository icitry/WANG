namespace WANG
{
    internal sealed class HelpWindow : Form
    {
        internal RichTextBox Content { get; } = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Color.White,
            DetectUrls = false,
            Font = new("Segoe UI", 10),
            ScrollBars = RichTextBoxScrollBars.Vertical
        };

        internal HelpWindow(Project project)
        {
            Text = "WANG / Help";
            ClientSize = new(600, 630);
            MinimumSize = new(420, 360);
            Padding = new(20);
            StartPosition = FormStartPosition.CenterParent;
            Controls.Add(Content);
            Section(
                "Numbers and calculations",
                $"A window {project.ZeroWidth} pixels wide means zero. Every {project.ValueStep} pixels adds one; shrink for negative numbers.",
                "Put a receiving window in front of overlapping inputs. An input on the left adds, on the right subtracts, above multiplies, and below divides. Its color shows the relationship.");
            Section(
                "Remembering numbers",
                $"A height of {JarRuler.FirstHeight}–{JarRuler.FirstHeight + (JarRuler.Maximum(project) + 1) * JarRuler.Step(project) - 1} pixels makes a teal jar. Each jar index has a {JarRuler.Step(project)}-pixel height band. Matching jars share a number across pages.",
                "A writable jar shows its current contents in the center and its width-based Start value near the top. Reset applies the Start values.",
                "Calculation | jar stores the answer for the next step. Jar | calculation shares it immediately. Look for ‘stores next step’ or ‘shared now’ on the destination jar.");
            Section(
                "Ordinary app windows",
                "Existing apps follow the same rules as blank windows. Their overlay is one click-through line: current value, calculation or jar, starting value for writable jars, and physical size.",
                "Many apps open between 320 and 1347 pixels tall, so they become jars. Resizing their width updates Start while keeping the remembered value; Reset applies Start. For a number that follows width directly, make the app shorter than 320 pixels, if its size limits allow it. Captions refresh during resizing and never wrap across the app's contents.");
            Section(
                "Mouse and keyboard",
                project.Version >= 8
                    ? "Jar 0 reads mouse X (0–100). Jar 1 reads mouse Y (0–60). Jar 2 reads Space (1 while held). Jar 3 supplies time per step (1/60 second). Writable jars start at Jar 4."
                    : "This earlier file keeps its saved inputs: Jar 0 reads mouse Y; Jar 1 reads Space; Jar 2 supplies time per step. Format 7 also reads mouse X through Jar 256.",
                "Mouse coordinates are inside Program output's picture. Move over the picture to update them. Space is read while Program output has focus. WANG reserves no global keyboard shortcuts.");
            Section(
                "Decisions and pictures",
                "A small 8–24 pixel gap asks a comparison. Three overlapping inputs choose condition / yes / no, starting with the nearest input behind the receiver.",
                "Four overlapping inputs draw a rectangle: X, Y, width, height, in that depth order. All drawing groups contribute to the same 100 × 60 picture. Minimize a source to disconnect it.");
            Section(
                "Pages",
                "Previous and Next stop at the first and last pages. Use the list to jump directly. New page creates one; Rename page changes its name. Both actions can be undone.",
                "All pages overview groups live miniatures by page. Scroll to zoom, drag to pan, and click a group to edit its real windows. Matching jars connect pages intentionally; windows on different pages never overlap as inputs.");
            Section(
                "Editing and running",
                "Live repeats steps. Pause stops them. Step advances once. Reset starts memory from the current widths. Undo and Redo restore your window arrangement, pause execution, and preserve running numbers and output.",
                "Window actions captures or releases the last focused app, sends a source behind others, pushes or pops a pile value, or removes a source. Existing apps keep their titles and document contents.",
                "Open and Save use .wang program files. They store the arrangement, page names and starting values, rather than running memory or other apps' documents. Output shows results and pictures; Log records editor failures.");
            Content.Select(0, 0);
        }

        private void Section(string heading, params string[] paragraphs)
        {
            using var headingFont = new Font(Content.Font, FontStyle.Bold);

            Content.SelectionFont = headingFont;
            Content.AppendText(heading + Environment.NewLine);
            Content.SelectionFont = Content.Font;
            foreach (var paragraph in paragraphs)
            {
                Content.AppendText(paragraph + Environment.NewLine + Environment.NewLine);
            }
        }
    }
}
