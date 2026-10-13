namespace FotoHavn.Storage.Tests;

public sealed class SqliteRuntimeTests
{
    [Fact]
    public void Bundled_sqlite_engine_loads()
    {
        var version = SqliteRuntime.GetVersion();

        Assert.Matches(@"^3\.\d+\.\d+", version);
    }
}
