using System.Text.Json;
using FotoHavn.Core.Storage;

namespace FotoHavn.Storage.DataPath;

/// <summary>
/// Gathers the <see cref="DataPathFacts"/> of the Data folder: drive type (including UNC and mapped network drives),
/// known cloud-sync roots (OneDrive, Dropbox, Google Drive, iCloud Drive), cloud-file attributes, and whether a probe file
/// can be created and deleted. The decisions live in <see cref="DataPathClassifier"/>; the operating-system reads live
/// behind <see cref="IDataPathEnvironment"/>, so this logic is tested with fake environments. It never throws: anything
/// it can't read counts as "unknown" (no cloud root, no attributes, <c>CanWrite = false</c>).
/// </summary>
public sealed class WindowsDataPathProbe : IDataPathProbe
{
    /// <summary><c>FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS</c>: a cloud placeholder whose data is fetched on access.</summary>
    public const int RecallOnDataAccess = 0x400000;

    /// <summary><c>FILE_ATTRIBUTE_RECALL_ON_OPEN</c>.</summary>
    public const int RecallOnOpen = 0x40000;

    /// <summary><c>FILE_ATTRIBUTE_PINNED</c>: a cloud-sync client keeps the file local.</summary>
    public const int Pinned = 0x80000;

    /// <summary><c>FILE_ATTRIBUTE_UNPINNED</c>: a cloud-sync client may drop the local copy.</summary>
    public const int Unpinned = 0x100000;

    private const int CloudAttributeMask = RecallOnDataAccess | RecallOnOpen | Pinned | Unpinned;

    private const string GoogleDriveLabel = "Google Drive";

    private readonly IDataPathEnvironment _environment;

    public WindowsDataPathProbe()
        : this(SystemDataPathEnvironment.Instance)
    {
    }

    public WindowsDataPathProbe(IDataPathEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        _environment = environment;
    }

    public DataPathFacts Probe(string dataRoot)
    {
        ArgumentNullException.ThrowIfNull(dataRoot);
        var path = Normalize(dataRoot, out var isUnc);
        var root = Guard(() => Path.GetPathRoot(path), null);
        var driveType = isUnc
            ? DriveType.Network
            : string.IsNullOrEmpty(root) ? DriveType.Unknown : Guard(() => _environment.GetDriveType(root), DriveType.Unknown);

        var provider = Guard(() => FindCloudProvider(path, isUnc ? null : root), null);
        var hasCloudAttributes = Guard(() => HasCloudAttributes(path), false);
        var canWrite = Guard(() => _environment.TryWriteProbeFile(path), false);

        return new DataPathFacts(driveType, isUnc, provider is not null, provider, hasCloudAttributes, canWrite);
    }

    /// <summary>The cloud provider whose sync root holds <paramref name="path"/>, or null.</summary>
    private string? FindCloudProvider(string path, string? driveRoot)
    {
        foreach (var (provider, root) in CloudRoots())
        {
            if (IsUnder(path, root))
            {
                return provider;
            }
        }

        if (!string.IsNullOrEmpty(driveRoot)
            && string.Equals(_environment.GetVolumeLabel(driveRoot), GoogleDriveLabel, StringComparison.OrdinalIgnoreCase))
        {
            return GoogleDriveLabel;
        }

        return null;
    }

    private IEnumerable<(string Provider, string Root)> CloudRoots()
    {
        foreach (var name in new[] { "OneDrive", "OneDriveConsumer", "OneDriveCommercial" })
        {
            if (_environment.GetEnvironmentVariable(name) is { } oneDrive)
            {
                yield return ("OneDrive", oneDrive);
            }
        }

        foreach (var name in new[] { "APPDATA", "LOCALAPPDATA" })
        {
            if (_environment.GetEnvironmentVariable(name) is not { } appData)
            {
                continue;
            }

            if (_environment.ReadAllText(Path.Combine(appData, "Dropbox", "info.json")) is not { } info)
            {
                continue;
            }

            foreach (var dropboxRoot in DropboxRoots(info))
            {
                yield return ("Dropbox", dropboxRoot);
            }
        }

        if (_environment.GetEnvironmentVariable("USERPROFILE") is { } profile)
        {
            yield return (GoogleDriveLabel, Path.Combine(profile, "Google Drive"));
            yield return ("iCloud Drive", Path.Combine(profile, "iCloudDrive"));
        }
    }

    /// <summary>The <c>path</c> values of a Dropbox <c>info.json</c>: <c>{ "personal": { "path": "..." }, "business": { "path": "..." } }</c>.</summary>
    private static List<string> DropboxRoots(string json)
    {
        var roots = new List<string>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return roots;
            }

            foreach (var account in document.RootElement.EnumerateObject())
            {
                if (account.Value.ValueKind == JsonValueKind.Object
                    && account.Value.TryGetProperty("path", out var path)
                    && path.ValueKind == JsonValueKind.String
                    && !string.IsNullOrWhiteSpace(path.GetString()))
                {
                    roots.Add(path.GetString()!);
                }
            }
        }
        catch (JsonException)
        {
            roots.Clear();
        }

        return roots;
    }

    /// <summary>Whether the path, or its nearest ancestor that exists, carries a cloud-file attribute.</summary>
    private bool HasCloudAttributes(string path)
    {
        string? candidate = path;
        while (!string.IsNullOrEmpty(candidate))
        {
            if (_environment.PathExists(candidate))
            {
                return _environment.GetAttributes(candidate) is { } attributes && ((int)attributes & CloudAttributeMask) != 0;
            }

            candidate = Path.GetDirectoryName(candidate);
        }

        return false;
    }

    private static bool IsUnder(string path, string root)
    {
        var fullRoot = Guard(() => Path.GetFullPath(root), null);
        if (string.IsNullOrEmpty(fullRoot) || string.Equals(Path.GetPathRoot(fullRoot), fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            // A blank root or a bare drive root would claim the whole drive.
            return false;
        }

        var trimmed = fullRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(path.TrimEnd(Path.DirectorySeparatorChar), trimmed, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(trimmed + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The full path with a <c>\\?\</c> prefix removed (<c>\\?\UNC\server\share</c> becomes <c>\\server\share</c>); <paramref name="isUnc"/> says whether it is a UNC path.</summary>
    private static string Normalize(string dataRoot, out bool isUnc)
    {
        var path = Guard(() => Path.GetFullPath(dataRoot), dataRoot);
        const string extendedUnc = @"\\?\UNC\";
        const string extended = @"\\?\";
        if (path.StartsWith(extendedUnc, StringComparison.OrdinalIgnoreCase))
        {
            path = @"\\" + path[extendedUnc.Length..];
        }
        else if (path.Length > extended.Length + 1 && path.StartsWith(extended, StringComparison.Ordinal) && path[extended.Length + 1] == ':')
        {
            path = path[extended.Length..];
        }

        isUnc = path.StartsWith(@"\\", StringComparison.Ordinal) && !path.StartsWith(extended, StringComparison.Ordinal) && !path.StartsWith(@"\\.\", StringComparison.Ordinal);
        return path;
    }

    private static T Guard<T>(Func<T> read, T fallback)
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
