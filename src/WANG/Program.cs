namespace WANG
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            if (args.Length > 0 && args[0] == "--export-examples")
            {
                WindowExamples.Export(args.Length > 1 ? args[1] : "examples");

                return;
            }

            Application.Run(new Studio(args.Length > 0 ? args[0] : null));
        }
    }
}
