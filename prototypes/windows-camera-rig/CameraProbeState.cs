namespace FotoHavn.CameraRigPrototype;

public enum ProbePhase
{
    Enumerating,
    DeviceSelected,
    Initializing,
    AwaitingPreviewFrame,
    Ready,
    Capturing,
    Disconnected,
    Faulted
}
public sealed record ReadinessChecks(
    bool ExactIdentityResolved = false,
    bool ExclusiveInitialization = false,
    bool PreviewCapability = false,
    bool PhotoCapability = false,
    bool FreshPreviewFrame = false,
    bool StorageWritable = false)
{
    public bool AllPassed => ExactIdentityResolved
        && ExclusiveInitialization
        && PreviewCapability
        && PhotoCapability
        && FreshPreviewFrame
        && StorageWritable;
}

public sealed record CameraProbeState(
    ProbePhase Phase,
    string? SelectedDeviceId,
    string? SelectedDisplayName,
    ReadinessChecks Checks,
    int CompletedCaptures,
    string Status,
    string? Error = null)
{
    public static CameraProbeState Initial => new(
        ProbePhase.Enumerating,
        null,
        null,
        new ReadinessChecks(),
        0,
        "Enumerating Windows video-capture devices");
}

public abstract record ProbeAction;
public sealed record DeviceChosen(string DeviceId, string DisplayName) : ProbeAction;
public sealed record InitializationStarted : ProbeAction;
public sealed record InitializationMeasured(bool PreviewCapability, bool PhotoCapability, bool StorageWritable) : ProbeAction;
public sealed record PreviewFrameReceived : ProbeAction;
public sealed record CaptureSequenceStarted : ProbeAction;
public sealed record CaptureSaved(int Number) : ProbeAction;
public sealed record SelectedDeviceRemoved : ProbeAction;
public sealed record ProbeFailed(string Message) : ProbeAction;

public static class CameraProbeReducer
{
    public static CameraProbeState Apply(CameraProbeState state, ProbeAction action) => action switch
    {
        DeviceChosen selected => new CameraProbeState(
            ProbePhase.DeviceSelected,
            selected.DeviceId,
            selected.DisplayName,
            new ReadinessChecks(ExactIdentityResolved: true),
            0,
            "Selected exact Windows video-capture interface"),

        InitializationStarted => state with
        {
            Phase = ProbePhase.Initializing,
            Checks = state.Checks with
            {
                ExclusiveInitialization = false,
                PreviewCapability = false,
                PhotoCapability = false,
                FreshPreviewFrame = false,
                StorageWritable = false
            },
            CompletedCaptures = 0,
            Status = "Opening exclusive MediaCapture session",
            Error = null
        },

        InitializationMeasured measured => state with
        {
            Phase = ProbePhase.AwaitingPreviewFrame,
            Checks = state.Checks with
            {
                ExclusiveInitialization = true,
                PreviewCapability = measured.PreviewCapability,
                PhotoCapability = measured.PhotoCapability,
                StorageWritable = measured.StorageWritable
            },
            Status = "Capabilities passed; waiting for a fresh preview frame"
        },

        PreviewFrameReceived => state with
        {
            Phase = state.Checks with { FreshPreviewFrame = true } is { AllPassed: true }
                ? ProbePhase.Ready
                : ProbePhase.Faulted,
            Checks = state.Checks with { FreshPreviewFrame = true },
            Status = "Fresh mirrored preview received; camera is ready"
        },

        CaptureSequenceStarted => state with
        {
            Phase = ProbePhase.Capturing,
            CompletedCaptures = 0,
            Status = "Capturing four ordered, unmirrored JPEGs"
        },

        CaptureSaved saved => state with
        {
            Phase = saved.Number == 4 ? ProbePhase.Ready : ProbePhase.Capturing,
            CompletedCaptures = saved.Number,
            Status = saved.Number == 4
                ? "Four ordered Captures saved locally"
                : $"Saved Capture {saved.Number} of 4"
        },

        SelectedDeviceRemoved => state with
        {
            Phase = ProbePhase.Disconnected,
            Checks = state.Checks with
            {
                ExactIdentityResolved = false,
                ExclusiveInitialization = false,
                FreshPreviewFrame = false
            },
            Status = "Selected camera disconnected; initialize a new session after reconnect"
        },

        ProbeFailed failed => state with
        {
            Phase = ProbePhase.Faulted,
            Status = "Camera probe failed",
            Error = failed.Message
        },

        _ => state
    };
}
