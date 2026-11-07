namespace WANG
{
    internal sealed class WindowProgramBuilder
    {
        internal sealed record Signal(int? Slot = null, int? Constant = null);

        internal Project Project { get; } = Relationships.Empty();

        private int nextSlot = 4, nextPage = 1;
        private readonly Dictionary<int, int> initial = [];
        private readonly Dictionary<int, int> constants = [];

        internal Signal Number(int number)
        {
            return new(Constant: number);
        }

        internal Signal Input(int address)
        {
            return new(Slot: address);
        }

        internal Signal Cell(int value)
        {
            var slot = nextSlot++;

            initial[slot] = value;

            return new(Slot: slot);
        }

        private int Slot(Signal signal)
        {
            if (signal.Slot is int slot)
            {
                return slot;
            }

            var number = signal.Constant!.Value;

            if (!constants.TryGetValue(number, out slot))
            {
                slot = nextSlot++;
                initial[slot] = number;
                constants[number] = slot;
            }


            return slot;
        }

        private int Page(string name)
        {
            var page = nextPage++;

            Project.Workspaces[page] = name.Length > 60 ? name[..60] : name;

            return page;
        }

        private SourceWindow Jar(int page, int slot, int order, string name)
        {
            if (slot > JarRuler.Maximum(Project))
            {
                throw new InvalidOperationException("The example exceeded the physical memory address ruler.");
            }

            var jar = new SourceWindow
            {
                Name = name,
                Workspace = page,
                ZOrder = order,
                Width = Project.ZeroWidth + initial.GetValueOrDefault(slot) * Project.ValueStep,
                Height = JarRuler.Height(Project, slot)
            };

            Project.Windows.Add(jar);

            return jar;
        }

        private SourceWindow Receiver(int page, int literal, string name)
        {
            var source = new SourceWindow
            {
                Name = name,
                Workspace = page,
                X = 650,
                Y = 300,
                Width = Project.ZeroWidth + literal * Project.ValueStep,
                Height = 240,
                ZOrder = 0
            };

            Project.Windows.Add(source);

            return source;
        }

        private void Place(SourceWindow receiver, SourceWindow input, Relationship direction, bool comparison = false)
        {
            input.X = receiver.X + receiver.Width / 2 - input.Width / 2;
            input.Y = receiver.Y;
            switch (direction)
            {
                case Relationship.Add:
                case Relationship.Less:
                    input.X = receiver.X - input.Width + (comparison ? -16 : 100);
                    break;
                case Relationship.Subtract:
                case Relationship.Equal:
                    input.X = receiver.Bounds.Right + (comparison ? 16 : -100);
                    break;
                case Relationship.Multiply:
                    input.Y = 40;
                    break;
                case Relationship.Divide:
                    input.Y = receiver.Bounds.Bottom - 80;
                    break;
            }
        }

        private void ConnectResult(SourceWindow source, int slot, bool delayed, int order)
        {
            var jar = Jar(source.Workspace, slot, order, delayed ? "Remember next tick" : "Shared result");

            jar.X = delayed ? source.Bounds.Right : source.X - jar.Width;
            jar.Y = source.Y + source.Height / 2 - jar.Height / 2;
        }

        internal Signal Binary(Relationship operation, Signal left, Signal right, string name)
        {
            var page = Page(name);
            var output = nextSlot++;

            initial[output] = 0;
            var source = Receiver(page, left.Constant ?? 0, name);
            var rightInput = Jar(page, Slot(right), 1, "Right input");

            Place(source, rightInput, operation, operation is Relationship.Less or Relationship.Equal);
            var order = 2;

            if (left.Slot is not null)
            {
                var leftInput = Jar(page, Slot(left), order++, "Left input");

                Place(
                    source,
                    leftInput,
                    operation is Relationship.Subtract or Relationship.Equal ? Relationship.Add : Relationship.Subtract);
            }

            ConnectResult(source, output, false, order);

            return new(Slot: output);
        }

        internal Signal Add(Signal left, Signal right, string name = "Add")
        {
            return Binary(Relationship.Add, left, right, name);
        }

        internal Signal Sub(Signal left, Signal right, string name = "Subtract")
        {
            return Binary(Relationship.Subtract, left, right, name);
        }

        internal Signal Mul(Signal left, Signal right, string name = "Multiply")
        {
            return Binary(Relationship.Multiply, left, right, name);
        }

        internal Signal Div(Signal left, Signal right, string name = "Divide")
        {
            return Binary(Relationship.Divide, left, right, name);
        }

        internal Signal Less(Signal left, Signal right, string name = "Compare")
        {
            return Binary(Relationship.Less, left, right, name);
        }

        internal Signal Equal(Signal left, Signal right, string name = "Equal")
        {
            return Binary(Relationship.Equal, left, right, name);
        }

        internal Signal Choose(Signal condition, Signal yes, Signal no, string name = "Choose a path")
        {
            var page = Page(name);
            var output = nextSlot++;

            initial[output] = 0;
            var source = Receiver(page, 0, name);
            var inputs = new[]
            {
                condition,
                yes,
                no
            };

            for (var i = 0; i < 3; i++)
            {
                Place(
                    source,
                    Jar(page, Slot(inputs[i]), i + 1, i == 0 ? "Condition" : "Choice"),
                    i == 0 ? Relationship.Add : i == 1 ? Relationship.Subtract : Relationship.Multiply);
            }

            ConnectResult(source, output, false, 4);

            return new(Slot: output);
        }

        internal void Remember(Signal cell, Signal value, string name = "Remember")
        {
            var page = Page(name);
            var source = Receiver(page, value.Constant ?? 0, name);
            var order = 1;

            if (value.Slot is not null)
            {
                Place(source, Jar(page, Slot(value), order++, "Next value"), Relationship.Add);
            }

            ConnectResult(source, cell.Slot!.Value, true, order);
        }

        internal void Rectangle(Signal x, Signal y, Signal width, Signal height, string name)
        {
            var page = Page(name);
            var source = Receiver(page, 0, name);
            var inputs = new[]
            {
                x,
                y,
                width,
                height
            };

            for (var i = 0; i < 4; i++)
            {
                Place(
                    source,
                    Jar(page, Slot(inputs[i]), i + 1, "Picture coordinate"),
                    new[]
                    {
                        Relationship.Add,
                        Relationship.Subtract,
                        Relationship.Multiply,
                        Relationship.Divide
                    }[i]);
            }
        }

        internal Project Finish(Signal result, string name)
        {
            Project.Workspaces[0] = name;
            var source = Receiver(0, result.Constant ?? 0, name);

            if (result.Slot is not null)
            {
                Place(source, Jar(0, Slot(result), 1, "Program result"), Relationship.Add);
            }

            Project.Validate();

            return Project;
        }
    }
}
