namespace WANG
{
    public static class JarRuler
    {
        public const int FirstHeight = 320;
        public const int MaximumIndex = 256;
        public const int LegacyMaximumIndex = 255;
        public const int PixelsPerIndex = 4;

        public static int Maximum(Project project)
        {
            return project.Version >= 7 ? MaximumIndex : LegacyMaximumIndex;
        }

        public static int Step(Project project)
        {
            return project.Version >= 6 ? PixelsPerIndex : 1;
        }

        public static bool Contains(int height, int step, int maximum = MaximumIndex)
        {
            return height >= FirstHeight && height < FirstHeight + (maximum + 1) * step;
        }

        public static int Index(int height, int step)
        {
            return (height - FirstHeight) / step;
        }

        public static int Height(Project project, int index)
        {
            if (index < 0 || index > Maximum(project))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            var step = Step(project);

            return FirstHeight + index * step + step / 2;
        }

        public static Project Widen(Project original)
        {
            if (original.Version != 5)
            {
                return original;
            }

            var widened = Project.FromCapture(original.Capture());

            widened.Version = 6;
            foreach (var window in widened.Windows)
            {
                if (!WindowRuntime.IsMemory(original, window))
                {
                    continue;
                }

                var middle = window.Y + window.Height / 2;
                var index = WindowRuntime.Address(original, window);

                window.Height = Height(widened, index);
                window.Y = middle - window.Height / 2;
            }

            foreach (var page in widened.Windows.GroupBy(window => window.Workspace))
            {
                var offset = Math.Max(0, 40 - page.Min(window => window.Y));

                foreach (var window in page)
                {
                    window.Y += offset;
                }
            }

            try
            {
                widened.Validate();
            }
            catch (InvalidDataException)
            {
                return original;
            }


            return WindowRuntime.LayoutSignature(original) == WindowRuntime.LayoutSignature(widened) ? widened : original;
        }
    }
}
