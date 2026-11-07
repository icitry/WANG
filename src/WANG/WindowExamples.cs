namespace WANG
{
    internal static class WindowExamples
    {
        internal static Project Arithmetic()
        {
            return Relationships.Addition();
        }


        internal static Project Decision()
        {
            var builder = new WindowProgramBuilder();
            var condition = builder.Less(builder.Number(2), builder.Number(3));

            return builder.Finish(builder.Choose(condition, builder.Number(10), builder.Number(20)), "Choose automatically");
        }

        internal static Project Countdown()
        {
            var builder = new WindowProgramBuilder();
            var count = builder.Cell(3);
            var positive = builder.Less(builder.Number(0), count, "Still above zero");
            var minusOne = builder.Sub(count, builder.Number(1), "One less");

            builder.Remember(count, builder.Choose(positive, minusOne, builder.Number(0)), "Advance the countdown");

            return builder.Finish(count, "Countdown");
        }





        internal static Project Cursor()
        {
            var project = Relationships.Empty();

            project.Workspaces[0] = "Follow the cursor";
            foreach (var (name, x, y, width, height, order) in new[]
            {
                ("Draw at the cursor", 900, 650, 400, 240, 0),
                ("Cursor X", 600, 650, 400, JarRuler.Height(project, WindowRuntime.PointerXAddress), 1),
                ("Cursor Y", 1200, 650, 400, JarRuler.Height(project, WindowRuntime.PointerYAddress), 2),
                ("Square width", 900, 400, 424, JarRuler.Height(project, 4), 3),
                ("Square height", 900, 800, 424, JarRuler.Height(project, 4), 4)
            })
            {
                project.Windows.Add(new()
                {
                    Name = name,
                    X = x,
                    Y = y,
                    Width = width,
                    Height = height,
                    ZOrder = order
                });
            }

            project.Validate();

            return project;
        }

        internal static Project Pong()
        {
            var builder = new WindowProgramBuilder();
            var x = builder.Cell(50);
            var y = builder.Cell(30);
            var horizontalSpeed = builder.Cell(2);
            var verticalSpeed = builder.Cell(1);
            var score = builder.Cell(0);
            var zero = builder.Number(0);
            var one = builder.Number(1);
            var nextX = builder.Add(x, horizontalSpeed, "Next ball X");
            var nextY = builder.Add(y, verticalSpeed, "Next ball Y");
            var top = builder.Less(nextY, zero, "Above the ceiling");
            var bottom = builder.Less(builder.Number(57), nextY, "Below the floor");
            var boundedY = builder.Choose(top, zero, builder.Choose(bottom, builder.Number(57), nextY), "Keep the ball inside vertically");
            var reverseY = builder.Add(top, bottom);
            var nextVerticalSpeed = builder.Choose(reverseY, builder.Mul(builder.Number(-1), verticalSpeed), verticalSpeed, "Vertical bounce");
            var pointer = builder.Input(WindowRuntime.PointerYAddress);
            var space = builder.Input(WindowRuntime.SpaceAddress);
            var paddleTarget = builder.Sub(pointer, builder.Number(6), "Center the paddle on the pointer");
            var paddle = builder.Choose(
                builder.Less(paddleTarget, zero),
                zero,
                builder.Choose(builder.Less(builder.Number(48), paddleTarget), builder.Number(48), paddleTarget),
                "Keep the paddle on screen");
            var nearPaddle = builder.Mul(builder.Less(builder.Number(91), nextX), builder.Less(nextX, builder.Number(97)));
            var belowTop = builder.Less(paddle, builder.Add(builder.Number(3), boundedY));
            var aboveBottom = builder.Less(boundedY, builder.Add(builder.Number(12), paddle));
            var goingRight = builder.Less(zero, horizontalSpeed);
            var hit = builder.Mul(builder.Mul(nearPaddle, belowTop), builder.Mul(aboveBottom, goingRight), "Paddle collision");
            var left = builder.Less(nextX, zero, "Left wall");
            var reverseX = builder.Add(left, hit);
            var nextHorizontalSpeed = builder.Choose(reverseX, builder.Mul(builder.Number(-1), horizontalSpeed), horizontalSpeed, "Horizontal bounce");
            var miss = builder.Less(builder.Number(97), nextX, "Missed the paddle");
            var reset = builder.Add(miss, space, "Restart after a miss or Space");

            builder.Remember(x, builder.Choose(reset, builder.Number(50), builder.Choose(left, zero, nextX)), "Remember ball X");
            builder.Remember(y, builder.Choose(reset, builder.Number(30), boundedY), "Remember ball Y");
            builder.Remember(
                horizontalSpeed,
                builder.Choose(reset, builder.Number(2), nextHorizontalSpeed),
                "Remember horizontal speed");
            builder.Remember(verticalSpeed, builder.Choose(reset, one, nextVerticalSpeed), "Remember vertical speed");
            builder.Remember(score, builder.Choose(space, zero, builder.Add(score, hit)), "Count paddle hits");
            builder.Rectangle(x, y, builder.Number(3), builder.Number(3), "Draw ball");
            builder.Rectangle(builder.Number(94), paddle, builder.Number(2), builder.Number(12), "Draw paddle");
            builder.Rectangle(zero, zero, one, builder.Number(60), "Draw left wall");

            return builder.Finish(score, "Pong");
        }

        internal static void Export(string directory)
        {
            Directory.CreateDirectory(directory);
            foreach (var (name, make) in new (string, Func<Project>)[]
            {
                ("01-arithmetic", Arithmetic),
                ("02-decision", Decision),
                ("03-countdown", Countdown),
                ("04-cursor", Cursor),
                ("05-pong", Pong)
            })
            {
                make().Save(Path.Combine(directory, name + ".wang"));
            }
        }
    }
}
