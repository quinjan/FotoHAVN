namespace FotoHavn.Storage.Tests;

public sealed class CrashPointTests
{
    [Fact]
    public void The_step_names_are_the_documented_stable_set()
    {
        Assert.Equal(
            [
                "capture.staged", "capture.flushed", "capture.renamed", "capture.inserted",
                "strip.staged", "strip.flushed", "strip.renamed", "strip.inserted",
                "event.markedDeleting", "event.folderDeleted", "event.rowsDeleted",
                "cycle.markedDeleting", "cycle.folderDeleted", "cycle.rowsDeleted",
            ],
            CrashPoint.All);
        Assert.Equal(CrashPoint.All.Count, CrashPoint.All.Distinct().Count());
    }

    [Fact]
    public void The_default_crash_points_do_nothing()
    {
        var crashPoints = NoCrashPoints.Instance;

        foreach (var step in CrashPoint.All)
        {
            crashPoints.Reached(step);
        }

        Assert.Same(NoCrashPoints.Instance, NoCrashPoints.Instance);
    }
}
