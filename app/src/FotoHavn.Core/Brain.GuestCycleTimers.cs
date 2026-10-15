namespace FotoHavn.Core;

// The Guest Cycle's Phase Timers (plan section 7, Phase timers; D18). Owner: M8 wave B1. Wave A leaves it empty so B1 owns the file outright.
// Intended content: one PhaseTimerEngine per phase the guest is in, fed from the brain's clock (_dependencies.Clock.UtcNow) and woken by a
// single scheduled callback at NextDeadline (the warning start, then the expiry) plus a once-a-second push while the warning shows; pause on
// capture and Operator Assistance, resume afterwards; restart on MORE TIME, going back, re-entering a phase, and stopping a capture; the
// outcome of each phase's expiry (the table in plan section 7); and the Start over confirmation closing as KEEP GOING at expiry.
public sealed partial class Brain
{
}
