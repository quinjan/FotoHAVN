namespace FotoHavn.Protocol.Cameras;

// Camera vocabulary shared by the brain's camera port (FotoHavn.Core.Camera), the presentations, and the test hooks.
// Defined once here so the wire names and the Core port can't drift apart.

/// <summary>Why a camera can't be used or stopped working. The wire names are camelCase; the UI owns the copy for each.</summary>
public enum CameraFailureKind
{
    /// <summary>The camera is open but no fresh frame has arrived for the staleness limit.</summary>
    Stale,

    /// <summary>The capture session reported a failure (<c>MediaCapture.Failed</c>) with no more specific cause.</summary>
    Failed,

    /// <summary>The device was unplugged or disabled.</summary>
    Removed,

    /// <summary>Another app took exclusive control while the camera was open.</summary>
    ExclusiveLost,

    /// <summary>Another app holds the camera, so FOTOHAVN can't open it.</summary>
    InUseByAnotherApp,

    /// <summary>Windows camera privacy blocks access (<c>E_ACCESSDENIED</c>): "Camera access is off in Windows privacy settings."</summary>
    PrivacyDenied,

    /// <summary>The camera offers no format the booth can use.</summary>
    NoSupportedFormat,

    /// <summary>No camera has the device ID.</summary>
    NotFound,

    /// <summary>A failure with no recognised cause.</summary>
    Unknown,
}

/// <summary>Which page receives preview frames. <see cref="None"/> stops them.</summary>
public enum PreviewTarget
{
    None,
    Operator,
    Guest,
}

/// <summary>
/// The selected camera's state, as the Operator Screen shows it (ADR 0004). "In use by the running Event" joins in M6,
/// when an Event can hold the camera; that is a protocol change.
/// </summary>
public enum CameraState
{
    /// <summary>No camera is selected or opened.</summary>
    Idle,

    /// <summary>The camera is opening or waiting for its first fresh frame.</summary>
    Checking,

    /// <summary>A fresh frame arrived in a usable format: an Eligible Camera.</summary>
    Ready,

    /// <summary>The camera can't be used; the reason says why.</summary>
    NotAvailable,

    /// <summary>Another app holds the camera.</summary>
    InUseByAnotherApp,
}

/// <summary>How <c>test.injectCameraFault</c> makes the camera fail, on top of the real or fake camera.</summary>
public enum CameraFaultKind
{
    /// <summary>Frames stop arriving, so the camera goes stale.</summary>
    Stale,

    /// <summary>The capture session reports a failure.</summary>
    Failed,

    /// <summary>The device disappears.</summary>
    Removed,

    /// <summary>Exclusive control is lost.</summary>
    ExclusiveLost,
}

/// <summary>A camera the operator can choose.</summary>
/// <param name="DeviceId">The Windows device ID, the camera's identity.</param>
/// <param name="Name">The friendly name, for display only.</param>
public sealed record CameraOption(string DeviceId, string Name);

/// <summary>The format a camera streams.</summary>
/// <param name="Width">Frame width in pixels.</param>
/// <param name="Height">Frame height in pixels.</param>
/// <param name="Fps">Frames per second.</param>
public sealed record CameraFormatView(int Width, int Height, double Fps);
