namespace FotoHavn.Core;

public abstract record ApplicationCommand;

public sealed record LaunchApplication : ApplicationCommand;

public enum EventTileKind
{
    NewEvent,
    SavedEvent,
}

public sealed record EventTilePresentation(
    EventTileKind Kind,
    string Label,
    string SupportingText,
    string Glyph,
    EventId? EventId = null,
    DateTimeOffset? LastSavedAt = null);

public sealed record ApplicationPresentation(
    string Heading,
    IReadOnlyList<EventTilePresentation> EventTiles,
    string? EmptyStateMessage,
    ApplicationCanvasPresentation Canvas);

public sealed record ApplicationCanvasPresentation(
    int Width,
    int Height,
    bool AllowsReflow);

public sealed record SavedEventSummary(
    EventId Id,
    string Name,
    DateTimeOffset LastSavedAt);

public readonly record struct EventId
{
    public EventId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public enum CameraReadiness
{
    NotChecked,
    Ready,
    Unavailable,
}

public interface ICameraBoundary
{
    Task<CameraReadiness> GetReadinessAsync(CancellationToken cancellationToken);
}

public sealed record CaptureReference(string ArtifactPath);

public sealed record PhotoStripCompositionRequest(
    string EventName,
    IReadOnlyList<CaptureReference> Captures);

public sealed record PhotoStripCompositionResult(
    bool IsAvailable,
    ReadOnlyMemory<byte> PngBytes,
    int Width,
    int Height);

public interface IPhotoStripCompositor
{
    Task<PhotoStripCompositionResult> ComposeAsync(
        PhotoStripCompositionRequest request,
        CancellationToken cancellationToken);
}

public interface IApplicationClock
{
    DateTimeOffset UtcNow { get; }

    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public interface IEventFileSystem
{
    Task<IReadOnlyList<SavedEventSummary>> LoadEventsAsync(CancellationToken cancellationToken);
}

public static class MotionPolicy
{
    public static TimeSpan ResolveDuration(bool animationsEnabled, TimeSpan preferredDuration)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(preferredDuration, TimeSpan.Zero);
        return animationsEnabled ? preferredDuration : TimeSpan.Zero;
    }
}
