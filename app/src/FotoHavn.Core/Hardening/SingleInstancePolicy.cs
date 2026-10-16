using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace FotoHavn.Core.Hardening;

/// <summary>What a launch does once it knows whether it owns the instance mutex.</summary>
public enum SingleInstanceDecision
{
    /// <summary>This launch is the first on its Data folder: it runs.</summary>
    Proceed,

    /// <summary>Another launch owns the Data folder: signal it to bring its Operator window forward, then exit with <see cref="HostExitCodes.AlreadyRunning"/>.</summary>
    ActivateExisting,
}

/// <summary>The names two launches on the same Data folder share, and launches on different Data folders never do.</summary>
/// <param name="DataFolderKey">The normalized full Data folder path the names are derived from.</param>
/// <param name="MutexName">The named mutex the first launch holds for its whole life.</param>
/// <param name="ActivationEventName">The named auto-reset event the first launch waits on; a second launch sets it to bring the Operator window forward.</param>
public sealed record SingleInstanceNames(string DataFolderKey, string MutexName, string ActivationEventName);

/// <summary>
/// The single-instance rule (M10, ADR 0003). One FOTOHAVN may run per Data folder: the machine's one camera and one
/// Guest Screen are only safe with a single brain, and the end-to-end project runs several instances at once, each on its
/// own Data folder, so the names come from the folder and never from a constant. Pure: the host owns the mutex and the event.
/// </summary>
public static class SingleInstancePolicy
{
    /// <summary>The prefix of every name. <c>Local\</c> keeps the name in the user's session, so no privilege is needed and another user's session never collides.</summary>
    public const string NamePrefix = @"Local\FOTOHAVN-";

    /// <summary>Appended to the mutex name to name the activation event.</summary>
    public const string ActivationSuffix = "-activate";

    /// <summary>Hex digits of the SHA-256 kept in the name (64 bits).</summary>
    public const int HashLength = 16;

    /// <summary>
    /// The Data folder reduced to one spelling: absolute, <c>\</c> separators, no <c>.</c> or <c>..</c> segments, no trailing separator
    /// (a drive root keeps its one), no <c>\\?\</c> prefix, upper-case (Windows paths ignore case).
    /// </summary>
    public static string Normalize(string dataFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dataFolder);
        var full = Path.GetFullPath(dataFolder).Replace('/', '\\');
        if (full.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase))
        {
            full = @"\\" + full[@"\\?\UNC\".Length..];
        }
        else if (full.StartsWith(@"\\?\", StringComparison.Ordinal) && full.Length > 6 && full[5] == ':')
        {
            full = full[@"\\?\".Length..];
        }

        var rootLength = Path.GetPathRoot(full)?.Length ?? 0;
        while (full.Length > rootLength && full[^1] == '\\')
        {
            full = full[..^1];
        }

        return full.ToUpperInvariant();
    }

    /// <summary>The mutex and activation event names for <paramref name="dataFolder"/>.</summary>
    public static SingleInstanceNames NamesFor(string dataFolder)
    {
        var key = Normalize(dataFolder);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key)))[..HashLength].ToLower(CultureInfo.InvariantCulture);
        var mutex = NamePrefix + hash;
        return new SingleInstanceNames(key, mutex, mutex + ActivationSuffix);
    }

    /// <summary>What this launch does: run when it created the mutex, otherwise activate the running instance and exit.</summary>
    public static SingleInstanceDecision Decide(bool ownsMutex) => ownsMutex ? SingleInstanceDecision.Proceed : SingleInstanceDecision.ActivateExisting;
}
