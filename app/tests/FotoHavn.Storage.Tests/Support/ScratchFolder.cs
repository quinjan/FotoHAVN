namespace FotoHavn.Storage.Tests.Support;

/// <summary>
/// A fresh folder for one test, under the test assembly's output folder (<c>bin/.../scratch/&lt;guid&gt;</c>), deleted on
/// dispose. Storage tests use real files here instead of the system temp folder.
/// </summary>
public sealed class ScratchFolder : IDisposable
{
    public ScratchFolder()
    {
        Path = System.IO.Path.Combine(AppContext.BaseDirectory, "scratch", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    /// <summary>Joins <paramref name="parts"/> onto the folder.</summary>
    public string Combine(params string[] parts) => System.IO.Path.Combine([Path, .. parts]);

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
