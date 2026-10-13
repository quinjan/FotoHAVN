namespace FotoHavn.Storage.DataPath;

/// <summary>
/// The operating-system reads the data-path probe makes, and nothing else, so the probe's own logic can be tested against
/// a fake environment. Every member must be safe to call with a path that doesn't exist; a failure is reported as the
/// "unknown" answer (null, false, <see cref="DriveType.Unknown"/>), never as an exception.
/// </summary>
public interface IDataPathEnvironment
{
    /// <summary>An environment variable's value, or null when it isn't set or is blank.</summary>
    string? GetEnvironmentVariable(string name);

    /// <summary>A file's text, or null when it doesn't exist or can't be read.</summary>
    string? ReadAllText(string path);

    /// <summary>Whether a file or a directory exists.</summary>
    bool PathExists(string path);

    /// <summary>The drive type of the volume whose root is <paramref name="root"/> (for example <c>C:\</c>).</summary>
    DriveType GetDriveType(string root);

    /// <summary>The volume label of the drive whose root is <paramref name="root"/>, or null when it has none or can't be read.</summary>
    string? GetVolumeLabel(string root);

    /// <summary>The Windows file attributes of an existing file or directory, or null when they can't be read.</summary>
    FileAttributes? GetAttributes(string path);

    /// <summary>Creates a probe file in <paramref name="directory"/>, then deletes it. False when either step fails.</summary>
    bool TryWriteProbeFile(string directory);
}

/// <summary>The real <see cref="IDataPathEnvironment"/>: thin wrappers over the .NET APIs that turn any failure into the "unknown" answer.</summary>
public sealed class SystemDataPathEnvironment : IDataPathEnvironment
{
    public static SystemDataPathEnvironment Instance { get; } = new();

    public string? GetEnvironmentVariable(string name)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    public string? ReadAllText(string path) => Try(() => File.ReadAllText(path), null);

    public bool PathExists(string path) => Try(() => Path.Exists(path), false);

    public DriveType GetDriveType(string root) => Try(() => new DriveInfo(root).DriveType, DriveType.Unknown);

    public string? GetVolumeLabel(string root) => Try(() => NullIfEmpty(new DriveInfo(root).VolumeLabel), null);

    public FileAttributes? GetAttributes(string path) => Try<FileAttributes?>(() => File.GetAttributes(path), null);

    public bool TryWriteProbeFile(string directory) => Try(
        () =>
        {
            var probe = Path.Combine(directory, ".fotohavn-write-probe-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllBytes(probe, [0x46]);
            File.Delete(probe);
            return true;
        },
        false);

    private static string? NullIfEmpty(string? value) => string.IsNullOrEmpty(value) ? null : value;

    // The probe contract is "never throws for an unreadable path", so every failure becomes the fallback.
    private static T Try<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
