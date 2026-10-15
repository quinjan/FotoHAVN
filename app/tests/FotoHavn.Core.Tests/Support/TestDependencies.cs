using FotoHavn.Catalog;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Media;
using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Support;

/// <summary>Brain dependencies with the real catalog data, a <see cref="FakeCompositor"/>, a fresh <see cref="PreviewCache"/>, and a fixed time zone, so no test or fixture depends on the machine's.</summary>
internal static class TestDependencies
{
    /// <summary>The zone every test brain uses for "today": a fixed offset east of UTC, never the machine's zone.</summary>
    public static TimeZoneInfo FixedTimeZone { get; } = TimeZoneInfo.CreateCustomTimeZone("fotohavn-test-zone", TimeSpan.FromHours(2), "FOTOHAVN test zone", "FOTOHAVN test zone");

    public static BrainDependencies Create(IBrainExecutor executor, IClock clock, IDisplayAdapter displays, ICameraAdapter camera, IHostServices host, IEventStore store)
    {
        var catalog = new CatalogData();
        return new BrainDependencies(executor, clock, displays, camera, host, store, catalog, new FakeCompositor(catalog), new PreviewCache(), new NoOpWakeLock()) { LocalTimeZone = FixedTimeZone };
    }
}
