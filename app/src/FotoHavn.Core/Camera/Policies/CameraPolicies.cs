using System.Diagnostics.CodeAnalysis;
using FotoHavn.Protocol.Cameras;

namespace FotoHavn.Core.Camera.Policies;

// The camera policies (ADR 0004: "Policies in the brain"). Every rule that isn't hardware or OS I/O lives here as pure,
// unit-tested code that adapters and the brain share. M5 wave A fixed these signatures; wave B1 implements the members
// marked as stubs (they throw NotImplementedException and carry a coverage exclusion that B1 removes with the stub).

/// <summary>Chooses the stream format: 1920×1080 at 30 fps first, a bounded fallback down to 1280×720 at 15 fps, no probing.</summary>
public static class CameraFormatPolicy
{
    /// <summary>The most preferred width.</summary>
    public const int PreferredWidth = 1920;

    /// <summary>The most preferred height.</summary>
    public const int PreferredHeight = 1080;

    /// <summary>The smallest width the booth accepts.</summary>
    public const int MinimumWidth = 1280;

    /// <summary>The smallest height the booth accepts.</summary>
    public const int MinimumHeight = 720;

    /// <summary>
    /// Picks the best format from what the camera offers, or null when none is usable (the adapter then fails the open
    /// with <see cref="CameraFailureKind.NoSupportedFormat"/>). Specified order, first match wins, frame rates compared
    /// within ±0.5 fps so 29.97 counts as 30: 1920×1080 at 30, 1920×1080 at 15 or more, 1280×720 at 30,
    /// 1280×720 at 15 or more. Within a rung, the subtype preference is NV12, YUY2, MJPG, any other subtype in ordinal
    /// order, then the higher frame rate. Nothing below 1280×720, below 15 fps, or of another size is ever chosen.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public static CameraFormat? Choose(IReadOnlyList<CameraFormat> formats) =>
        throw new NotImplementedException("M5 wave B1 implements CameraFormatPolicy.Choose.");
}

/// <summary>Decides whether a camera is delivering frames: the readiness proof, and the staleness limit during an Active Event.</summary>
public static class FrameFreshness
{
    /// <summary>The newest frame may be this old for a camera to prove itself ready (ADR 0004 and plan section 11: 2 s).</summary>
    public static readonly TimeSpan EligibleMaxAge = TimeSpan.FromSeconds(2);

    /// <summary>Frames older than this during an Active Event are a camera failure (<see cref="CameraFailureKind.Stale"/>; ADR 0004: 2 s).</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(2);

    /// <summary>
    /// True when <paramref name="latest"/> exists and its age, <paramref name="now"/> minus its timestamp, is at most
    /// <paramref name="maxAge"/>. No frame is never fresh. A frame stamped after <paramref name="now"/> (clock jitter) has age zero.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public static bool IsFresh(TimeSpan now, CameraFrameInfo? latest, TimeSpan maxAge) =>
        throw new NotImplementedException("M5 wave B1 implements FrameFreshness.IsFresh.");
}

/// <summary>The pure rule adapters use to pick the frame for a Capture: the first one stamped at or after the countdown's end.</summary>
public static class CaptureFrameSelector
{
    /// <summary>True when a frame stamped <paramref name="frameTimestamp"/> may be the Capture: it is at or after <paramref name="notBefore"/>.</summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public static bool IsEligible(TimeSpan frameTimestamp, TimeSpan notBefore) =>
        throw new NotImplementedException("M5 wave B1 implements CaptureFrameSelector.IsEligible.");

    /// <summary>
    /// The index of the first frame in <paramref name="timestampsInArrivalOrder"/> that is eligible, or null when none is
    /// (the adapter waits for the next frame). A frame stamped before <paramref name="notBefore"/> is never selected,
    /// however late it arrived or however many frames follow it.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public static int? Select(IReadOnlyList<TimeSpan> timestampsInArrivalOrder, TimeSpan notBefore) =>
        throw new NotImplementedException("M5 wave B1 implements CaptureFrameSelector.Select.");
}

/// <summary>Where a failure happened, because the same HRESULT means different things when opening and when streaming.</summary>
public enum CameraOperation
{
    /// <summary>Opening the camera (<c>MediaCapture.InitializeAsync</c>, choosing and applying the format).</summary>
    Open,

    /// <summary>The open stream failed (<c>MediaCapture.Failed</c>, the frame reader, a device change).</summary>
    Streaming,
}

/// <summary>
/// The HRESULTs the adapter meets and the classifier names (from <c>mferror.h</c> and <c>winerror.h</c>), as signed ints.
/// </summary>
public static class CameraHResults
{
    /// <summary><c>E_ACCESSDENIED</c>: Windows camera privacy is off for desktop apps (Q16).</summary>
    public const int AccessDenied = unchecked((int)0x80070005);

    /// <summary><c>HRESULT_FROM_WIN32(ERROR_SHARING_VIOLATION)</c>: another process holds the device.</summary>
    public const int SharingViolation = unchecked((int)0x80070020);

    /// <summary><c>MF_E_HW_MFT_FAILED_START_STREAMING</c> (0xC00D3704): the hardware couldn't start streaming, which is what an open camera held by another app commonly reports on open.</summary>
    public const int HwMftFailedStartStreaming = unchecked((int)0xC00D3704);

    /// <summary><c>MF_E_VIDEO_DEVICE_LOCKED</c> (0xC00D4E24): the video device is locked by another client.</summary>
    public const int VideoDeviceLocked = unchecked((int)0xC00D4E24);

    /// <summary><c>MF_E_VIDEO_RECORDING_DEVICE_INVALIDATED</c> (0xC00D3EA2): the device is no longer present.</summary>
    public const int VideoRecordingDeviceInvalidated = unchecked((int)0xC00D3EA2);

    /// <summary><c>MF_E_VIDEO_RECORDING_DEVICE_PREEMPTED</c> (0xC00D3EA3): another app took the device over.</summary>
    public const int VideoRecordingDevicePreempted = unchecked((int)0xC00D3EA3);

    /// <summary><c>HRESULT_FROM_WIN32(ERROR_DEVICE_NOT_CONNECTED)</c> (0x8007048F).</summary>
    public const int DeviceNotConnected = unchecked((int)0x8007048F);

    /// <summary><c>HRESULT_FROM_WIN32(ERROR_DEVICE_REMOVED)</c> (0x80070651).</summary>
    public const int DeviceRemoved = unchecked((int)0x80070651);

    /// <summary><c>HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND)</c> (0x80070002): no device has the ID.</summary>
    public const int FileNotFound = unchecked((int)0x80070002);

    /// <summary><c>HRESULT_FROM_WIN32(ERROR_NOT_FOUND)</c> (0x80070490).</summary>
    public const int NotFound = unchecked((int)0x80070490);

    /// <summary><c>MF_E_NOT_FOUND</c> (0xC00D36D5).</summary>
    public const int MediaFoundationNotFound = unchecked((int)0xC00D36D5);

    /// <summary><c>MF_E_INVALIDMEDIATYPE</c> (0xC00D36B4).</summary>
    public const int InvalidMediaType = unchecked((int)0xC00D36B4);

    /// <summary><c>MF_E_NO_MORE_TYPES</c> (0xC00D36B9).</summary>
    public const int NoMoreTypes = unchecked((int)0xC00D36B9);

    /// <summary><c>MF_E_UNSUPPORTED_FORMAT</c> (0xC00D3E98).</summary>
    public const int UnsupportedFormat = unchecked((int)0xC00D3E98);
}

/// <summary>Turns an HRESULT or an exception into a <see cref="CameraFailureKind"/>.</summary>
public static class CameraFailureClassifier
{
    /// <summary>
    /// Specified mapping (anything else is <see cref="CameraFailureKind.Unknown"/> when opening and
    /// <see cref="CameraFailureKind.Failed"/> when streaming):
    /// <list type="bullet">
    /// <item><see cref="CameraHResults.AccessDenied"/> → <see cref="CameraFailureKind.PrivacyDenied"/> in both operations.</item>
    /// <item>Open: <see cref="CameraHResults.SharingViolation"/>, <see cref="CameraHResults.HwMftFailedStartStreaming"/>, <see cref="CameraHResults.VideoDeviceLocked"/>, <see cref="CameraHResults.VideoRecordingDevicePreempted"/> → <see cref="CameraFailureKind.InUseByAnotherApp"/>.</item>
    /// <item>Streaming: <see cref="CameraHResults.VideoRecordingDevicePreempted"/>, <see cref="CameraHResults.SharingViolation"/>, <see cref="CameraHResults.VideoDeviceLocked"/> → <see cref="CameraFailureKind.ExclusiveLost"/>.</item>
    /// <item>Streaming: <see cref="CameraHResults.VideoRecordingDeviceInvalidated"/>, <see cref="CameraHResults.DeviceNotConnected"/>, <see cref="CameraHResults.DeviceRemoved"/> → <see cref="CameraFailureKind.Removed"/>.</item>
    /// <item>Open: those three, <see cref="CameraHResults.FileNotFound"/>, <see cref="CameraHResults.NotFound"/>, <see cref="CameraHResults.MediaFoundationNotFound"/> → <see cref="CameraFailureKind.NotFound"/>.</item>
    /// <item>Open: <see cref="CameraHResults.InvalidMediaType"/>, <see cref="CameraHResults.NoMoreTypes"/>, <see cref="CameraHResults.UnsupportedFormat"/> → <see cref="CameraFailureKind.NoSupportedFormat"/>.</item>
    /// </list>
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public static CameraFailureKind Classify(int hresult, CameraOperation operation) =>
        throw new NotImplementedException("M5 wave B1 implements CameraFailureClassifier.Classify(int).");

    /// <summary>
    /// Classifies an exception: a <see cref="CameraException"/> keeps its kind; an <see cref="UnauthorizedAccessException"/>
    /// (WinRT's <c>E_ACCESSDENIED</c>) is <see cref="CameraFailureKind.PrivacyDenied"/>; any other exception goes through
    /// <see cref="Classify(int, CameraOperation)"/> with its <see cref="Exception.HResult"/>.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public static CameraFailureKind Classify(Exception exception, CameraOperation operation) =>
        throw new NotImplementedException("M5 wave B1 implements CameraFailureClassifier.Classify(Exception).");
}

/// <summary>Which failures the brain's Retry may try again.</summary>
public static class CameraRetryPolicy
{
    /// <summary>
    /// True for every kind except <see cref="CameraFailureKind.NoSupportedFormat"/>, which the same camera will repeat.
    /// A retry always opens a new session: Windows doesn't promise that a failed <c>MediaCapture</c> revives.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public static bool IsRetryable(CameraFailureKind kind) =>
        throw new NotImplementedException("M5 wave B1 implements CameraRetryPolicy.IsRetryable.");
}

/// <summary>The one-frame-in-flight rule for one preview target, shared by both hosts' channels. Thread-safe.</summary>
public sealed class PreviewBackPressure
{
    /// <summary>The window <see cref="PreviewFrameStats.DeliveredPerSecond"/> is measured over.</summary>
    public static readonly TimeSpan RateWindow = TimeSpan.FromSeconds(2);

    /// <summary>A frame still unacked after this long is treated as lost (the page crashed or never acked) and stops blocking.</summary>
    public static readonly TimeSpan InFlightTimeout = TimeSpan.FromSeconds(1);

    /// <summary>Whether a frame is in flight and not yet timed out.</summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public bool IsInFlight(DateTimeOffset now) =>
        throw new NotImplementedException("M5 wave B1 implements PreviewBackPressure.IsInFlight.");

    /// <summary>
    /// Claims the one in-flight slot for <paramref name="sequence"/> and returns true, or returns false and counts a dropped
    /// frame when a frame is in flight. A frame that has been in flight for <see cref="InFlightTimeout"/> or longer is
    /// replaced (and counted as dropped).
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public bool TryBegin(long sequence, DateTimeOffset now) =>
        throw new NotImplementedException("M5 wave B1 implements PreviewBackPressure.TryBegin.");

    /// <summary>
    /// Handles the page's ack. When <paramref name="wireSequence"/> equals the in-flight frame's wire sequence
    /// (<see cref="PreviewFrameHeader.ToWireSequence"/>), frees the slot, counts a delivered frame, and returns true.
    /// Any other ack (late, duplicate, unknown) changes nothing and returns false.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public bool Acknowledge(uint wireSequence, DateTimeOffset now) =>
        throw new NotImplementedException("M5 wave B1 implements PreviewBackPressure.Acknowledge.");

    /// <summary>Frees the slot without counting a frame, for when the page disconnects or reloads. Counters keep their values.</summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public void Reset() =>
        throw new NotImplementedException("M5 wave B1 implements PreviewBackPressure.Reset.");

    /// <summary>The counters, with <see cref="PreviewFrameStats.DeliveredPerSecond"/> measured over the last <see cref="RateWindow"/> ending at <paramref name="now"/>.</summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public PreviewFrameStats Snapshot(DateTimeOffset now) =>
        throw new NotImplementedException("M5 wave B1 implements PreviewBackPressure.Snapshot.");
}

/// <summary>Mirrors a frame left to right (the Event's mirror setting applies to preview and saved Captures, D22).</summary>
public static class MirrorTransform
{
    /// <summary>
    /// Reverses the order of the pixels in every row of a BGRA8 frame in place: pixel x moves to width − 1 − x. Padding
    /// bytes beyond <c>width × 4</c> in each row are left alone. Throws <see cref="ArgumentOutOfRangeException"/> for a
    /// non-positive size or a stride below <c>width × 4</c>, and <see cref="ArgumentException"/> when
    /// <paramref name="bgra"/> is shorter than <c>stride × (height − 1) + width × 4</c>.
    /// </summary>
    [ExcludeFromCodeCoverage(Justification = "Contract stub until M5 wave B1 implements the policy.")]
    public static void MirrorBgraInPlace(Span<byte> bgra, int width, int height, int stride) =>
        throw new NotImplementedException("M5 wave B1 implements MirrorTransform.MirrorBgraInPlace.");
}

/// <summary>
/// How a Capture is encoded (ADR 0004): JPEG at quality 92, sRGB, no metadata. The mirror lives in Core; the encoder
/// lives in <c>FotoHavn.Camera.Windows</c> (SkiaSharp), because Core stays free of imaging libraries, and the adapter
/// reads these values so a test can pin them.
/// </summary>
public static class JpegEncodingSpec
{
    /// <summary>The JPEG quality, 0 to 100.</summary>
    public const int Quality = 92;

    /// <summary>The colour space the pixels are tagged and encoded in.</summary>
    public const string ColorSpace = "sRGB";

    /// <summary>Whether to write EXIF, XMP, or ICC metadata. It is never written: a Capture carries no camera or location data.</summary>
    public const bool WritesMetadata = false;
}

/// <summary>The camera state and reason the Operator Screen shows for a failure kind.</summary>
public static class CameraStateMapping
{
    /// <summary>
    /// <see cref="CameraFailureKind.InUseByAnotherApp"/> is <see cref="CameraState.InUseByAnotherApp"/>; every other kind is
    /// <see cref="CameraState.NotAvailable"/>. The reason is always the kind itself.
    /// </summary>
    public static (CameraState State, CameraFailureKind Reason) For(CameraFailureKind kind) =>
        (kind == CameraFailureKind.InUseByAnotherApp ? CameraState.InUseByAnotherApp : CameraState.NotAvailable, kind);
}
