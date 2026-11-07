namespace WANG
{
    public sealed record JarConnection(Guid Source, Guid Jar, bool Delayed);

    public sealed class WindowRuntime
    {
        public const int MemoryHeight = JarRuler.FirstHeight, MaximumAddress = JarRuler.MaximumIndex;
        public const int ContactGapTolerance = 6, ContactOverlapTolerance = 24;
        public const int PointerXAddress = 0, PointerYAddress = 1, SpaceAddress = 2, TimeAddress = 3;
        public const int PointerXChannel = 256;

        public static bool IsInput(Project project, int address)
        {
            return project.Version >= 8
                ? address is >= 0 and <= TimeAddress
                : address is >= 0 and <= 2 || project.Version == 7 && address == PointerXChannel;
        }

        internal static int InputChannel(Project project, int address)
        {
            return project.Version >= 8 ? address switch
            {
                PointerXAddress => PointerXChannel,
                PointerYAddress => 0,
                SpaceAddress => 1,
                TimeAddress => 2,
                _ => address
            } : address;
        }

        public Dictionary<int, double> Memory { get; } = [];
        public IReadOnlyList<Drawing> Frame { get; private set; } = [];
        public RelationshipResult Result { get; private set; } = new([], null);
        public IReadOnlyList<JarConnection> Connections { get; private set; } = [];
        public string? Error { get; private set; }

        public static bool IsMemory(Project project, SourceWindow window)
        {
            return JarRuler.Contains(window.Height, JarRuler.Step(project), JarRuler.Maximum(project));
        }

        public static int Address(Project project, SourceWindow window)
        {
            return JarRuler.Index(window.Height, JarRuler.Step(project));
        }

        internal static string LayoutSignature(Project project)
        {
            return new Evaluation(project, [], _ => 0, null).LayoutSignature();
        }

        public void Reset()
        {
            Memory.Clear();
            Frame = [];
            Result = new([], null);
            Connections = [];
            Error = null;
        }

        public RelationshipResult Preview(Project project, int page = 0, Func<int, double>? input = null, ISet<Guid>? available = null)
        {
            var context = new Evaluation(project, Memory, input ?? (_ => 0), available);

            Result = context.EvaluatePage(page);
            Connections = context.Connections;
            Frame = context.Frame;
            Error = context.Error;

            return Result;
        }

        public void Tick(Project project, int page = 0, Func<int, double>? input = null, ISet<Guid>? available = null)
        {
            var context = new Evaluation(project, Memory, input ?? (_ => 0), available);

            context.EvaluatePage(page);
            var next = context.NextMemory();

            if (context.Error is not null)
            {
                Result = context.EvaluatePage(page);
                Connections = context.Connections;
                Frame = context.Frame;
                Error = context.Error;

                return;
            }

            foreach (var (slot, value) in next)
            {
                Memory[slot] = value;
            }

            Preview(project, page, input, available);
        }

        private sealed record Writer(SourceWindow Source, SourceWindow Jar, bool Delayed);

        private sealed class Evaluation
        {
            private readonly Project project;
            private readonly SourceWindow[] windows;
            private readonly Dictionary<int, Writer> writers = [];
            private readonly Dictionary<int, double> memory;
            private readonly Func<int, double> input;
            private readonly Dictionary<Guid, WindowValue> values = [];
            private readonly HashSet<Guid> visiting = [];
            private readonly Dictionary<Guid, SourceWindow[]> operands = [];
            private readonly HashSet<(Guid Source, Guid Jar)> docked = [];

            internal List<Drawing> Frame { get; } = [];
            internal List<JarConnection> Connections { get; } = [];
            internal string? Error { get; private set; }

            private bool IsJar(SourceWindow window)
            {
                return IsMemory(project, window);
            }

            private int JarIndex(SourceWindow window)
            {
                return Address(project, window);
            }

            internal string LayoutSignature()
            {
                var descriptions = new List<string>();

                foreach (var window in windows.OrderBy(window => window.Id))
                {
                    if (IsJar(window))
                    {
                        descriptions.Add($"{window.Id}:jar:{JarIndex(window)}");
                        continue;
                    }

                    var inputs = FindInputs(window);
                    var relationship = InputRelationship(window, inputs);

                    descriptions.Add($"{window.Id}:{relationship}:{string.Join(',', inputs.Select(input => input.Id))}");
                }

                foreach (var connection in Connections.OrderBy(connection => connection.Source).ThenBy(connection => connection.Jar))
                {
                    descriptions.Add($"{connection.Source}>{connection.Jar}:{connection.Delayed}");
                }

                descriptions.Add(Error ?? "");

                return string.Join('\n', descriptions);
            }

            internal Evaluation(Project project, Dictionary<int, double> memory, Func<int, double> input, ISet<Guid>? available)
            {
                this.project = project;
                this.memory = memory;
                this.input = input;
                windows = project.Windows.Where(w => available is null || available.Contains(w.Id)).ToArray();
                foreach (var jar in windows.Where(IsJar))
                {
                    var slot = JarIndex(jar);

                    foreach (var source in windows.Where(w => !IsJar(w) && w.Workspace == jar.Workspace && w.State != "Minimized"
                        && (project.Version >= 6 || w.ZOrder < jar.ZOrder)))
                    {
                        var middle = source.Y + source.Height / 2;

                        if (middle < jar.Y + 8 || middle > jar.Bounds.Bottom - 8)
                        {
                            continue;
                        }

                        var delayed = Touching(jar.X - source.Bounds.Right);
                        var wire = Touching(source.X - jar.Bounds.Right);

                        if (!delayed && !wire)
                        {
                            continue;
                        }

                        if (IsInput(project, slot))
                        {
                            Error = "External input jars are read-only.";
                            continue;
                        }

                        docked.Add((source.Id, jar.Id));
                        Connections.Add(new(source.Id, jar.Id, delayed));
                        var writer = new Writer(source, jar, delayed);

                        if (writers.TryGetValue(slot, out var previous) && previous.Source.Id != source.Id)
                        {
                            Error = $"Memory {slot} has several writers. Separate its touching windows.";
                        }
                        else
                        {
                            writers[slot] = writer;
                        }
                    }
                }

                foreach (var group in windows.Where(IsJar).GroupBy(JarIndex))
                {
                    var home = writers.TryGetValue(group.Key, out var writer)
                        ? writer.Jar
                        : group.OrderBy(w => w.Workspace).ThenBy(w => w.ZOrder).First();

                    if (!IsInput(project, group.Key) && !memory.ContainsKey(group.Key))
                    {
                        memory[group.Key] = WindowValues.Value(project, home);
                    }
                }
            }

            internal RelationshipResult EvaluatePage(int page)
            {
                foreach (var window in windows)
                {
                    EvaluateWindow(window);
                }

                Frame.Clear();
                foreach (var window in windows.Where(w => !IsJar(w) && Active(w) && FindInputs(w).Length == 4))
                {
                    var value = EvaluateWindow(window);

                    if (value.Error is not null)
                    {
                        Error ??= value.Error;
                        continue;
                    }

                    if (value.Relationship != Relationship.Rectangle)
                    {
                        continue;
                    }

                    var inputs = FindInputs(window).Select(w => EvaluateWindow(w).Value).ToArray();

                    if (inputs.Any(v => !double.IsFinite(v) || Math.Abs(v) > 100000))
                    {
                        Error ??= "A picture coordinate is too large.";
                        continue;
                    }

                    if (Frame.Count < 1024)
                    {
                        Frame.Add(new("Rect", inputs[0], inputs[1], inputs[2], inputs[3]));
                    }
                }

                var output = windows.Where(w => w.Workspace == page && w.State != "Minimized").OrderBy(w => w.ZOrder)
                    .ThenBy(w => w.Id)
                    .FirstOrDefault();

                if (output is not null)
                {
                    Error ??= EvaluateWindow(output).Error;
                }


                return new(values, output?.Id);
            }

            internal Dictionary<int, double> NextMemory()
            {
                Dictionary<int, double> next = [];

                foreach (var (slot, writer) in writers.Where(p => p.Value.Delayed && Active(p.Value.Jar)))
                {
                    var result = EvaluateWindow(writer.Source);

                    if (result.Error is not null)
                    {
                        Error ??= result.Error;
                    }
                    else
                    {
                        next[slot] = result.Value;
                    }
                }


                return next;
            }

            private bool Active(SourceWindow window)
            {
                return window.State != "Minimized" && (project.Version >= 5 || window.State == "Normal");
            }

            private bool Touching(int gap)
            {
                if (project.Version < 5)
                {
                    return Math.Abs(gap) <= 3;
                }


                return gap >= -ContactOverlapTolerance && gap <= ContactGapTolerance;
            }

            private SourceWindow[] FindInputs(SourceWindow receiver)
            {
                if (operands.TryGetValue(receiver.Id, out var cached))
                {
                    return cached;
                }


                return operands[receiver.Id] = windows.Where(w => w.Workspace == receiver.Workspace && w.ZOrder > receiver.ZOrder && w.State != "Minimized"
                    && !docked.Contains((receiver.Id, w.Id))
                    && (Relationships.Overlaps(receiver, w) || Comparison(receiver, w) is not null))
                    .OrderBy(w => w.ZOrder)
                    .ThenBy(w => w.Id)
                    .ToArray();
            }

            private static Relationship? Comparison(SourceWindow receiver, SourceWindow supplier)
            {
                var yShared = Math.Min(receiver.Bounds.Bottom, supplier.Bounds.Bottom) - Math.Max(receiver.Y, supplier.Y);
                var xShared = Math.Min(receiver.Bounds.Right, supplier.Bounds.Right) - Math.Max(receiver.X, supplier.X);

                bool Gap(int distance) => distance is >= 8 and <= 24;
                if (yShared >= 8 && Gap(receiver.X - supplier.Bounds.Right))
                {
                    return Relationship.Less;
                }

                if (yShared >= 8 && Gap(supplier.X - receiver.Bounds.Right))
                {
                    return Relationship.Equal;
                }

                if (xShared >= 8 && Gap(receiver.Y - supplier.Bounds.Bottom))
                {
                    return Relationship.Greater;
                }

                if (xShared >= 8 && Gap(supplier.Y - receiver.Bounds.Bottom))
                {
                    return Relationship.LessEqual;
                }


                return null;
            }

            private static Relationship InputRelationship(SourceWindow window, SourceWindow[] inputs)
            {
                if (inputs.Length == 0)
                {
                    return Relationship.Number;
                }

                if (Math.Abs(window.X - inputs[0].X) <= 24 && Math.Abs(window.Y - inputs[0].Y) <= 24)
                {
                    return Relationship.Stack;
                }


                return inputs.Length switch
                {
                    1 or 2 => Comparison(window, inputs[0]) ?? Relationships.Direction(window, inputs[0]),
                    3 => Relationship.Select,
                    4 => Relationship.Rectangle,
                    _ => Relationship.Number
                };
            }

            private WindowValue EvaluateWindow(SourceWindow window)
            {
                if (values.TryGetValue(window.Id, out var cached))
                {
                    return cached;
                }

                var literal = WindowValues.Value(project, window);

                if (visiting.Count >= 128)
                {
                    return new(
                        window.Id,
                        literal,
                        double.NaN,
                        Relationship.Memory,
                        Error: "This calculation is nested too deeply (128 levels). Split it with a memory delay.");
                }

                WindowValue CacheResult(double value, Relationship relationship, Guid? source = null, string? error = null)
                {
                    visiting.Remove(window.Id);
                    if (!double.IsFinite(value))
                    {
                        error ??= "A window's result became too large.";
                    }


                    return values[window.Id] = new(window.Id, literal, value, relationship, source, error);
                }

                if (!visiting.Add(window.Id))
                {
                    return new(
                        window.Id,
                        literal,
                        double.NaN,
                        Relationship.Memory,
                        Error: "A wire feeds itself without a memory delay. Dock its writer on the other side of the jar.");
                }

                if (window.State == "Minimized")
                {
                    return CacheResult(0, Relationship.Off);
                }

                if (window.State == "Maximized" && project.Version < 5)
                {
                    return CacheResult(1, Relationship.One);
                }

                if (IsJar(window))
                {
                    var slot = JarIndex(window);

                    if (IsInput(project, slot))
                    {
                        return CacheResult(input(InputChannel(project, slot)), Relationship.Memory);
                    }

                    if (writers.TryGetValue(slot, out var writer) && !writer.Delayed && Active(writer.Jar))
                    {
                        var produced = EvaluateWindow(writer.Source);

                        return CacheResult(produced.Value, Relationship.Memory, writer.Source.Id, produced.Error);
                    }


                    return CacheResult(memory.GetValueOrDefault(slot), Relationship.Memory);
                }

                var inputs = FindInputs(window);
                var action = InputRelationship(window, inputs);

                if (inputs.Length == 0)
                {
                    return CacheResult(literal, Relationship.Number);
                }

                if (action == Relationship.Stack)
                {
                    return CacheResult(literal, Relationship.Stack);
                }

                if (inputs.Length > 4)
                {
                    return CacheResult(
                        double.NaN,
                        Relationship.Number,
                        error: "More than four inputs lie under this window. Separate the extra inputs.");
                }

                var right = EvaluateWindow(inputs[0]);

                if (right.Error is not null)
                {
                    return CacheResult(double.NaN, right.Relationship, inputs[0].Id, right.Error);
                }

                if (inputs.Length == 3)
                {
                    var selected = EvaluateWindow(inputs[right.Value != 0 ? 1 : 2]);

                    return CacheResult(selected.Value, Relationship.Select, selected.Id, selected.Error);
                }

                if (inputs.Length == 4)
                {
                    var error = inputs.Select(EvaluateWindow).Select(w => w.Error).FirstOrDefault(e => e is not null);

                    return CacheResult(right.Value, Relationship.Rectangle, inputs[0].Id, error);
                }

                var left = inputs.Length == 2
                    ? EvaluateWindow(inputs[1])
                    : new WindowValue(window.Id, literal, literal, Relationship.Number);

                if (left.Error is not null)
                {
                    return CacheResult(double.NaN, left.Relationship, inputs[1].Id, left.Error);
                }

                if (action == Relationship.Divide && right.Value == 0)
                {
                    return CacheResult(double.NaN, action, inputs[0].Id, "An overlapping window is dividing by zero.");
                }

                var answer = action switch
                {
                    Relationship.Add => left.Value + right.Value,
                    Relationship.Subtract => left.Value - right.Value,
                    Relationship.Multiply => left.Value * right.Value,
                    Relationship.Divide => left.Value / right.Value,
                    Relationship.Less => left.Value < right.Value ? 1 : 0,
                    Relationship.Equal => left.Value == right.Value ? 1 : 0,
                    Relationship.Greater => left.Value > right.Value ? 1 : 0,
                    Relationship.LessEqual => left.Value <= right.Value ? 1 : 0,
                    _ => left.Value
                };

                return CacheResult(answer, action, inputs[0].Id);
            }
        }
    }
}
