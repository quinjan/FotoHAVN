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
            RemoveLinks(new DirectoryInfo(Path));
            Directory.Delete(Path, recursive: true);
        }
    }

    // Directory.Delete(recursive) fails on Windows when a junction is inside, so the links go first, on their own.
    private static void RemoveLinks(DirectoryInfo directory)
    {
        foreach (var child in directory.EnumerateDirectories().ToList())
        {
            if (child.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                child.Delete();
            }
            else
            {
                RemoveLinks(child);
            }
        }
    }
}
