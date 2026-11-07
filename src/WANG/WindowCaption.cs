namespace WANG
{
    internal sealed record WindowCaption(Guid Source, Rectangle Bounds, string Text, Color Color, Rectangle[] Occluders)
    {
        internal static string Describe(Project project, SourceWindow source, WindowValue value, JarConnection? connection)
        {
            var number = value.Error is null ? value.Value.ToString("0.###") : "?";
            var size = $"{source.Width} × {source.Height} px";

            if (WindowRuntime.IsMemory(project, source))
            {
                var address = WindowRuntime.Address(project, source);
                var role = $"Jar {address}";

                if (WindowRuntime.IsInput(project, address))
                {
                    role += WindowRuntime.InputChannel(project, address) switch
                    {
                        WindowRuntime.PointerXChannel => ": mouse X",
                        0 => ": mouse Y",
                        1 => ": Space",
                        2 => ": step time",
                        _ => ""
                    };

                    return $"{number} | {role} | {size}";
                }

                var write = connection is null ? "" : connection.Delayed ? " | stores next step" : " | shared now";

                return $"{number} | {role} | Start: {WindowValues.Value(project, source)}{write} | {size}";
            }

            var operation = value.Relationship switch
            {
                Relationship.Number => "Number",
                Relationship.Add => "Add",
                Relationship.Subtract => "Subtract",
                Relationship.Multiply => "Multiply",
                Relationship.Divide => "Divide",
                Relationship.Less => "Less than",
                Relationship.Equal => "Equal",
                Relationship.Greater => "Greater than",
                Relationship.LessEqual => "Less or equal",
                Relationship.Select => "Choose",
                Relationship.Rectangle => "Rectangle",
                Relationship.Stack => "Pile",
                Relationship.One => "Maximized: 1",
                _ => "Off"
            };
            var width = value.Relationship == Relationship.Number ? "" : $" | Width: {WindowValues.Value(project, source)}";

            return $"{number} | {operation}{width} | {size}";
        }

        internal bool SameAs(WindowCaption other)
        {
            return Source == other.Source && Bounds == other.Bounds && Text == other.Text && Color == other.Color
                && Occluders.SequenceEqual(other.Occluders);
        }
    }
}
