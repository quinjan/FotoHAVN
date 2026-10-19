import type { CommandResult } from "../brain/types.ts";
import type { StartUnavailableReason } from "../protocol/generated.ts";

/**
 * The Guest Screen's own copy (app/DESIGN.md, Guest Screen copy). The brain sends facts and codes; the words live here.
 * Start's statement and the PIN dialog's lines are fixed by the plan and the M3 copy; the rest is written in brand voice.
 */

export const stepInsideLabel = "STEP INSIDE";
export const galleryLinkLabel = "SEE THE GOOD COMPANY";

/** What a guest reads when `STEP INSIDE →` or the gallery link doesn't go through. Quiet, and never about the machine. */
export const notReadyNotice = "One moment. The booth is getting ready.";

export const pinDialogTitle = "Operator PIN";
export const pinDialogDescription = "Enter the Event's Operator PIN to end the Event.";
/** When a PIN submit doesn't go through (the brain isn't reachable). A wrong PIN is not this: the presentation says it. */
export const pinSubmitFailed = "That didn't go through. Try again.";

/** The wrong attempts that lock the pad (`PinRules.MaxWrongAttempts`); the pad says how many tries are left from it. */
export const maxWrongAttempts = 5;

export interface UnavailableCopy {
  readonly lead: string;
  readonly turn: string;
  readonly supporting: string;
}

/** Said under the Start unavailable statement after a long wait; CSS reveals it after about 25 seconds. Neutral, no cause named. */
export const startUnavailableWaitHint = "If this takes a while, please ask the FOTOHAVN team.";

/** Start unavailable, by reason: calm and neutral. The brand's "A little interruption." is Operator Assistance, not this. */
export function startUnavailableCopy(reason: StartUnavailableReason): UnavailableCopy {
  return reason === "camera"
    ? { lead: "The booth is", turn: "getting ready.", supporting: "One moment." }
    : { lead: "The booth is", turn: "almost ready.", supporting: "Thank you for waiting." };
}

/** The quiet notice for a command the brain turned down, or null when it went through. */
export function guestCommandNotice(result: CommandResult): string | null {
  return result.status === "accepted" ? null : notReadyNotice;
}
