using FotoHavn.Catalog;
using FotoHavn.Core.Camera;
using FotoHavn.Core.Displays;
using FotoHavn.Core.Fakes;
using FotoHavn.Core.Media;
using FotoHavn.Core.Storage;

namespace FotoHavn.Core.Tests.Support;

/// <summary>Brain dependencies with the real catalog data, a <see cref="FakeCompositor"/>, and a fresh <see cref="PreviewCache"/>.</summary>
internal static class TestDependencies
{
    public static BrainDependencies Create(IBrainExecutor executor, IClock clock, IDisplayAdapter displays, ICameraAdapter camera, IHostServices host, IEventStore store)
    {
        var catalog = new CatalogData();
        return new BrainDependencies(executor, clock, displays, camera, host, store, catalog, new FakeCompositor(catalog), new PreviewCache());
    }
}
