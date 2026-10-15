namespace FotoHavn.Core;

// The capture sequence (plan section 7, Capture sequence). Owner: M8 wave B1. Wave A leaves it empty so B1 owns the file outright.
// Intended content: the countdown (one push per second), taking the first frame after the countdown through CameraSlotSession, the
// Event's mirror setting (MirrorTransform), the JPEG at quality 92, CommitCaptureAsync off the executor, the 800 ms pause, the flash
// counter (one increment per Capture), stopping, the Retake, and discarding a Retake's candidate in the store (SetCaptureStateAsync). Every
// result posts back to the executor with its operation id and is ignored when the model says it is stale (GuestCycleOutcome.Applied is
// false); a late committed Capture the model refused is set discarded in the store.
public sealed partial class Brain
{
}
