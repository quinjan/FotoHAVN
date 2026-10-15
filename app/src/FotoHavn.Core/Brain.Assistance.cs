using FotoHavn.Protocol;

namespace FotoHavn.Core;

// Operator Assistance (plan section 7, Failures inside a Guest Cycle). Owner: M8 wave B1. Wave A wired RetryAssistance (both roles) to this
// stub, which answers notAvailable. Intended content: entering Operator Assistance on a camera, storage, or guest-display failure (with
// or without a Guest Cycle), pausing the Phase Timer, the RETRY that re-proves the camera, storage, and bound display (reopening the Guest
// Screen if needed), and returning to the step the cycle was on or to Start.
public sealed partial class Brain
{
    private static CommandResultMessage RetryAssistance(string id) => NotAvailable(id);
}
