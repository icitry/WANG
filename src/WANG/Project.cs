using System.Text.Json;

namespace WANG
{
    public sealed class SourceWindow
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "Window";
        public int Workspace { get; set; }
        public int ZOrder { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; } = 400;
        public int Height { get; set; } = 240;
        public string State { get; set; } = "Normal";

        [System.Text.Json.Serialization.JsonIgnore]
        public Rectangle Bounds
        {
            get => new(X, Y, Width, Height);
            set
            {
                X = value.X;
                Y = value.Y;
                Width = value.Width;
                Height = value.Height;
            }
        }
    }

    public sealed class Project
    {
        public int Version { get; set; } = 8;
        public int OriginX { get; set; } = 30;
        public int OriginY { get; set; } = 150;
        public int ZeroWidth { get; set; } = 400;
        public int ValueStep { get; set; } = 8;
        public List<SourceWindow> Windows { get; set; } = [];
        public Dictionary<int, string> Workspaces { get; set; } = new()
        {
            [0] = "Main"
        };

        private static readonly JsonSerializerOptions Json = new()
        {
            WriteIndented = true
        };

        internal string Capture()
        {
            return JsonSerializer.Serialize(this, Json);
        }

        internal static Project FromCapture(string source)
        {
            var project = JsonSerializer.Deserialize<Project>(source, Json) ?? throw new InvalidDataException("Empty program.");

            project.Validate();

            return project;
        }

        public void Validate()
        {
            if (Version is not (4 or 5 or 6 or 7 or 8))
            {
                throw new InvalidDataException("Unsupported program format.");
            }

            if (ValueStep is < 1 or > 100 || ZeroWidth is < 64 or > 1000 || Math.Abs((long)OriginX) > 32000
                || Math.Abs((long)OriginY) > 32000)
            {
                throw new InvalidDataException("Invalid number ruler.");
            }

            if (Workspaces is null || Workspaces.Count is < 1 or > 512 || !Workspaces.ContainsKey(0)
                || Workspaces.Any(page => page.Key is < 0 or >= 512 || string.IsNullOrWhiteSpace(page.Value) || page.Value.Length > 60))
            {
                throw new InvalidDataException("Invalid pages.");
            }

            if (Windows is null || Windows.Count > 1000 || Windows.Any(window => window is null || window.Id == Guid.Empty)
                || Windows.Select(window => window.Id).Distinct().Count() != Windows.Count)
            {
                throw new InvalidDataException("Invalid source IDs or source limit exceeded (1000).");
            }

            foreach (var window in Windows)
            {
                if (window.ZOrder is < 0 or > 1000 || window.Name is null || window.Name.Length > 80
                    || window.Width is < 64 or > 10000
                    || window.Height is < 32 or > 10000
                    || Math.Abs((long)window.X) > 32000
                    || Math.Abs((long)window.Y) > 32000
                    || window.State is not ("Normal" or "Minimized" or "Maximized")
                    || !Workspaces.ContainsKey(window.Workspace))
                {
                    throw new InvalidDataException("Invalid source geometry, state or page.");
                }
            }
        }

        public void Save(string path)
        {
            Validate();
            var temporary = path + ".tmp";

            File.WriteAllText(temporary, Capture());
            File.Move(temporary, path, true);
        }

        public static Project Load(string path)
        {
            if (new FileInfo(path).Length > 4_000_000)
            {
                throw new InvalidDataException("Program file is too large.");
            }


            return FromCapture(File.ReadAllText(path));
        }
    }

    public record Drawing(string Kind, double X = 0, double Y = 0, double Width = 0, double Height = 0, double Value = 0);

    public static class WindowValues
    {
        public static int Value(Project project, SourceWindow window)
        {
            return WidthValue(project, window.Width);
        }

        public static int WidthValue(Project project, int width)
        {
            return (int)Math.Round((width - project.ZeroWidth) / (double)project.ValueStep, MidpointRounding.AwayFromZero);
        }
    }
}
