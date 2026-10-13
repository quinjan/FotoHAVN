using System.Globalization;
using FotoHavn.Protocol.Testing;

namespace FotoHavn.Host.Headless;

/// <summary>
/// The headless host command line: <c>--data &lt;dir&gt;</c> (the Data folder; default <c>Data</c> beside the
/// executable), <c>--port &lt;n&gt;</c> (default 0, a random free port), and <c>--ui &lt;dir&gt;</c> (the built UI;
/// default the nearest <c>ui/dist</c> above the working directory; <c>--ui-dist</c> is the M1 spelling). Values may also
/// be written as <c>--name=value</c>. <c>--e2e-storage-fault &lt;diskFull|accessDenied|locked&gt;</c> is read in every build, but
/// only E2E builds act on it (every storage write fails from the start); a field build ignores it.
/// </summary>
internal sealed record HeadlessOptions(string? DataFolder, int Port, string? UiFolder, StorageFaultKind? E2EStorageFault = null)
{
    public static HeadlessOptions Default { get; } = new(null, 0, null);

    public static HeadlessOptions Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);

        var options = Default;
        for (var index = 0; index < args.Count; index++)
        {
            var (name, inlineValue) = Split(args[index]);
            switch (name)
            {
                case "--data":
                    options = options with { DataFolder = ReadValue(args, ref index, name, inlineValue) };
                    break;
                case "--port":
                    options = options with { Port = ParsePort(ReadValue(args, ref index, name, inlineValue)) };
                    break;
                case "--ui":
                case "--ui-dist":
                    options = options with { UiFolder = ReadValue(args, ref index, name, inlineValue) };
                    break;
                case "--e2e-storage-fault":
                    options = options with { E2EStorageFault = ParseFault(ReadValue(args, ref index, name, inlineValue)) };
                    break;
                default:
                    throw new HeadlessOptionsException($"Unknown option '{args[index]}'.");
            }
        }

        return options;
    }

    /// <summary>The Data folder as a full path: <see cref="DataFolder"/>, or <c>Data</c> under <paramref name="appFolder"/>.</summary>
    public string ResolveDataFolder(string appFolder) =>
        string.IsNullOrWhiteSpace(DataFolder) ? Path.Combine(Path.GetFullPath(appFolder), "Data") : Path.GetFullPath(DataFolder);

    /// <summary>
    /// The UI folder as a full path, or null when there is no built UI: <see cref="UiFolder"/> when it exists, otherwise
    /// the first <c>ui/dist</c> holding an <c>index.html</c> in <paramref name="workingFolder"/> or one of its parents.
    /// </summary>
    public string? ResolveUiFolder(string workingFolder)
    {
        if (!string.IsNullOrWhiteSpace(UiFolder))
        {
            return Directory.Exists(UiFolder) ? Path.GetFullPath(UiFolder) : null;
        }

        for (var folder = new DirectoryInfo(Path.GetFullPath(workingFolder)); folder is not null; folder = folder.Parent)
        {
            var candidate = Path.Combine(folder.FullName, "ui", "dist");
            if (File.Exists(Path.Combine(candidate, "index.html")))
            {
                return candidate;
            }
        }

        return null;
    }

    private static (string Name, string? InlineValue) Split(string arg)
    {
        var separator = arg.IndexOf('=', StringComparison.Ordinal);
        return arg.StartsWith("--", StringComparison.Ordinal) && separator > 0
            ? (arg[..separator], arg[(separator + 1)..])
            : (arg, null);
    }

    private static string ReadValue(IReadOnlyList<string> args, ref int index, string name, string? inlineValue)
    {
        var value = inlineValue;
        if (value is null)
        {
            if (index + 1 >= args.Count)
            {
                throw new HeadlessOptionsException($"Option '{name}' needs a value.");
            }

            value = args[++index];
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new HeadlessOptionsException($"Option '{name}' needs a value.");
        }

        return value;
    }

    private static StorageFaultKind ParseFault(string value) =>
        Enum.TryParse<StorageFaultKind>(value, ignoreCase: true, out var kind) && Enum.IsDefined(kind) && !int.TryParse(value, out _)
            ? kind
            : throw new HeadlessOptionsException($"'{value}' is not a storage fault for --e2e-storage-fault (diskFull, accessDenied, or locked).");

    private static int ParsePort(string value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var port) && port is >= 0 and <= 65535
            ? port
            : throw new HeadlessOptionsException($"'{value}' is not a valid port for --port (0-65535, 0 picks a free port).");
}

internal sealed class HeadlessOptionsException(string message) : Exception(message);
