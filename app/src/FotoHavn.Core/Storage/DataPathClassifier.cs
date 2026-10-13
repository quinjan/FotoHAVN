namespace FotoHavn.Core.Storage;

/// <summary>What the data-path probe observed about the Data folder's location (plan section 10, Rules).</summary>
/// <param name="DriveType">The drive type of the path's root (<see cref="DriveType.Unknown"/> when it can't be told).</param>
/// <param name="IsUnc">Whether the path is a UNC path (<c>\\server\share</c>).</param>
/// <param name="IsUnderKnownCloudSyncRoot">Whether the path is under a known cloud sync root: OneDrive (<c>OneDrive</c>,
/// <c>OneDriveCommercial</c>, <c>OneDriveConsumer</c> environment variables and their paths), Dropbox (<c>info.json</c>
/// locations), Google Drive, or iCloud.</param>
/// <param name="CloudSyncProvider">The provider's name when <paramref name="IsUnderKnownCloudSyncRoot"/>, for the reason text.</param>
/// <param name="HasCloudFileAttributes">Whether the folder carries cloud-file attributes (recall on access or placeholder).</param>
/// <param name="CanWrite">Whether a probe file could be written and deleted in the folder.</param>
public sealed record DataPathFacts(
    DriveType DriveType,
    bool IsUnc,
    bool IsUnderKnownCloudSyncRoot,
    string? CloudSyncProvider,
    bool HasCloudFileAttributes,
    bool CanWrite);

/// <summary>The data-path check's outcome.</summary>
public enum DataPathStatus
{
    Ok,
    NotLocalDisk,
    CloudSynced,
    NotWritable,
}

/// <summary>A data-path verdict with its reason (brief English, no personal data; it may reach logs).</summary>
public sealed record DataPathVerdict(DataPathStatus Status, string Reason)
{
    public bool IsOk => Status == DataPathStatus.Ok;

    /// <summary>The <see cref="StorageErrorKind"/> a refusal becomes; throws for <see cref="DataPathStatus.Ok"/>.</summary>
    public StorageErrorKind ToErrorKind() => Status switch
    {
        DataPathStatus.NotLocalDisk => StorageErrorKind.NotLocalDisk,
        DataPathStatus.CloudSynced => StorageErrorKind.CloudSynced,
        DataPathStatus.NotWritable => StorageErrorKind.NotWritable,
        _ => throw new InvalidOperationException("An Ok verdict is not an error."),
    };
}

/// <summary>
/// Turns <see cref="DataPathFacts"/> into a <see cref="DataPathVerdict"/>. Pure, so Core's tests cover it. Network (UNC or
/// <see cref="DriveType.Network"/>) refuses with <see cref="DataPathStatus.NotLocalDisk"/>; a cloud sync root or cloud-file
/// attributes refuse with <see cref="DataPathStatus.CloudSynced"/>; a folder that can't be written refuses with
/// <see cref="DataPathStatus.NotWritable"/>; checks run in that order. A removable drive (the portable field build run from
/// USB) is allowed.
/// </summary>
public static class DataPathClassifier
{
    public const string NotLocalDiskReason = "The Data folder is on a network drive. Move FOTOHAVN to this computer's own disk.";

    public const string NotWritableReason = "FOTOHAVN can't write to the Data folder. Check that the folder isn't read-only and that this Windows account can change it.";

    public const string RemovableReason = "The Data folder is on a removable drive. FOTOHAVN runs from it as long as the drive stays connected.";

    public const string LocalDiskReason = "The Data folder is on this computer's own disk.";

    public static DataPathVerdict Classify(DataPathFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        if (facts.IsUnc || facts.DriveType == DriveType.Network)
        {
            return new DataPathVerdict(DataPathStatus.NotLocalDisk, NotLocalDiskReason);
        }

        if (facts.IsUnderKnownCloudSyncRoot || facts.HasCloudFileAttributes)
        {
            return new DataPathVerdict(DataPathStatus.CloudSynced, CloudSyncedReason(facts.IsUnderKnownCloudSyncRoot ? facts.CloudSyncProvider : null));
        }

        if (!facts.CanWrite)
        {
            return new DataPathVerdict(DataPathStatus.NotWritable, NotWritableReason);
        }

        return new DataPathVerdict(
            DataPathStatus.Ok,
            facts.DriveType == DriveType.Removable ? RemovableReason : LocalDiskReason);
    }

    private static string CloudSyncedReason(string? provider)
    {
        var name = string.IsNullOrWhiteSpace(provider) ? "cloud-synced" : provider.Trim();
        var article = name[0] is 'i' or 'I' ? "an" : "a";
        return $"The Data folder is inside {article} {name} folder, which can change files while the booth runs. Move FOTOHAVN outside it.";
    }
}