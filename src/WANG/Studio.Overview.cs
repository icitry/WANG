namespace WANG
{
    internal sealed partial class Studio
    {
        private readonly PageOverview pageOverview = new();
        private long nextOverviewRefresh;

        private void SetupOverview()
        {
            pageOverview.PageChosen += page => Safely(() =>
            {
                SetRoutine(page);
                Activate();
            });
            FormClosed += (_, _) => pageOverview.Dispose();
        }

        private void ShowOverview()
        {
            pageOverview.SetSource(project, liveResult, activeWorkspace);
            pageOverview.Show(this);
            pageOverview.BringToFront();
            pageOverview.Fit();
        }

        private void UpdateOverview()
        {
            if (!pageOverview.Visible || Environment.TickCount64 < nextOverviewRefresh)
            {
                return;
            }

            nextOverviewRefresh = Environment.TickCount64 + 150;
            pageOverview.SetSource(project, liveResult, activeWorkspace);
        }
    }
}
