namespace WANG
{
    public enum Relationship
    {
        Number,
        Add,
        Subtract,
        Multiply,
        Divide,
        Off,
        One,
        Less,
        Equal,
        Greater,
        LessEqual,
        Select,
        Memory,
        Rectangle,
        Stack
    }

    public record WindowValue(Guid Id, double Literal, double Value, Relationship Relationship, Guid? Input = null, string? Error = null);

    public record RelationshipResult(Dictionary<Guid, WindowValue> Windows, Guid? Output)
    {
        public WindowValue? Result => Output is Guid id ? Windows.GetValueOrDefault(id) : null;
    }

    public static class Relationships
    {
        public const int NewWindowWidth = 400, NewWindowHeight = 240;

        public static Project Empty()
        {
            return new()
            {
                Version = 8,
                ZeroWidth = NewWindowWidth,
                ValueStep = 8
            };
        }

        public static bool Overlaps(SourceWindow a, SourceWindow b)
        {
            var shared = Rectangle.Intersect(a.Bounds, b.Bounds);

            return shared.Width >= 8 && shared.Height >= 8;
        }

        public static Relationship Direction(SourceWindow receiver, SourceWindow input)
        {
            var dx = input.X + input.Width / 2d - receiver.X - receiver.Width / 2d;
            var dy = input.Y + input.Height / 2d - receiver.Y - receiver.Height / 2d;

            return Math.Abs(dx) >= Math.Abs(dy)
                ? dx <= 0 ? Relationship.Add : Relationship.Subtract
                : dy < 0 ? Relationship.Multiply : Relationship.Divide;
        }

        public static RelationshipResult Evaluate(Project project, int page = 0, ISet<Guid>? available = null)
        {
            return new WindowRuntime().Preview(project, page, available: available);
        }

        public static Project Addition()
        {
            var project = Empty();

            project.Windows.Add(new()
            {
                Name = "Receiver",
                Bounds = new(420, 300, project.ZeroWidth + 2 * project.ValueStep, NewWindowHeight),
                ZOrder = 0
            });
            project.Windows.Add(new()
            {
                Name = "Input",
                Bounds = new(150, 300, project.ZeroWidth + 3 * project.ValueStep, NewWindowHeight),
                ZOrder = 1
            });

            return project;
        }

    }
}
