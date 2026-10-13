using System.Diagnostics.CodeAnalysis;

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
/// Turns <see cref="DataPathFacts"/> into a <see cref="DataPathVerdict"/>. Pure, so Core's tests cover it. Wave B2
/// implements it: network (UNC or <see cref="DriveType.Network"/>) refuses with <see cref="DataPathStatus.NotLocalDisk"/>;
/// a cloud sync root or cloud-file attributes refuse with <see cref="DataPathStatus.CloudSynced"/>; a folder that can't be
/// written refuses with <see cref="DataPathStatus.NotWritable"/>; checks run in that order.
/// </summary>
public static class DataPathClassifier
{
    [ExcludeFromCodeCoverage(Justification = "Contract stub: wave B2 implements the classifier with its tests and removes this attribute.")]
    public static DataPathVerdict Classify(DataPathFacts facts) =>
        throw new NotImplementedException("Implemented by wave B2.");
}
