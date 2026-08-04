namespace FotoHavn.CameraRigPrototype;

public sealed record CameraIdentity(
    string VideoDeviceInterfaceId,
    string DisplayName,
    string? DeviceInstanceId,
    string? ContainerId);

public sealed record CaptureEvidence(
    int Order,
    string FileName,
    string Sha256,
    long Bytes,
    DateTimeOffset StartedAt,
    DateTimeOffset SavedAt,
    long ElapsedMilliseconds,
    string Transform);

public sealed class ProbeReport
{
    public required string RunId { get; init; }
    public required DateTimeOffset StartedAt { get; init; }
    public required string WindowsVersion { get; init; }
    public required CameraIdentity Camera { get; init; }
    public required IReadOnlyList<StreamCapability> ObservedCapabilities { get; init; }
    public required StreamCapability SelectedPreview { get; init; }
    public required StreamCapability SelectedPhoto { get; init; }
    public required string MinimumCapabilityContract { get; init; }
    public bool PreviewMirroredByUiTransform { get; set; }
    public DateTimeOffset? FirstPreviewFrameAt { get; set; }
    public List<CaptureEvidence> Captures { get; } = [];
    public List<string> WatcherEvents { get; } = [];
    public string? Verdict { get; set; }
}
