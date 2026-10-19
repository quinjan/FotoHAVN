import type { AssistanceReason, CameraFailureKind, OperatorNoticeCode, RunningEventStep as ProtocolStep } from "../protocol/generated.ts";
import { cameraReasonCopy } from "../components/CameraDiagnosticsScreen/cameraText.ts";
import type { RunningEventStep } from "../components/RunningEventPanel/index.ts";
import { formatDeletionCounts } from "./format.ts";

/** Operator copy that is not a form field: notices, confirmations, and the Operator Assistance lines (DESIGN.md, Copy). */

/** The brain sends the code; the words live here. */
export const noticeCopy: Readonly<Record<OperatorNoticeCode, string>> = {
  saveFailed: "The Event couldn't be saved. Check the Data folder, then try again.",
  deleteFailed: "The Event couldn't be deleted. Try again in a moment.",
  eventNotFound: "That Event isn't there any more.",
  guestCycleDeleteFailed: "The Guest Cycle couldn't be deleted. Try again in a moment.",
  guestCycleNotFound: "That Guest Cycle isn't there any more.",
};

export const eventRunningCopy = "End the running Event to start another.";

/** The reason for Operator Assistance, as one sentence in text. M8 sends the reasons; the words are written now. */
export const assistanceCopy: Readonly<Record<AssistanceReason, string>> = {
  camera: "The camera needs attention.",
  storage: "The Data folder needs attention.",
  guestDisplay: "The guest display isn't connected.",
};

/** The running-Event panel's alert: what went wrong, then what to do, ending with `RETRY` (DESIGN.md, Operator Assistance). */
export interface AssistanceAlertCopy {
  readonly reason: string;
  readonly fix: string;
}

const cameraFixCopy: Readonly<Record<CameraFailureKind, string>> = {
  stale: "Check its cable, then RETRY.",
  failed: "Check its cable, then RETRY.",
  removed: "Reconnect it, then RETRY.",
  exclusiveLost: "Close that app, then RETRY.",
  inUseByAnotherApp: "Close that app, then RETRY.",
  privacyDenied: "Turn it on in Windows settings, then RETRY.",
  noSupportedFormat: "Try another USB port, then RETRY.",
  notFound: "Reconnect it, then RETRY.",
  unknown: "Check that it is connected, then RETRY.",
};

const storageAlertCopy: AssistanceAlertCopy = {
  reason: "The Data folder can't be written to.",
  fix: "Check the disk and the folder, then RETRY.",
};

const guestDisplayAlertCopy: AssistanceAlertCopy = {
  reason: "The guest display isn't connected.",
  fix: "Reconnect it, then RETRY.",
};

/** For a camera whose failure the brain doesn't name (it may have recovered while the Event still waits for `RETRY`). */
const cameraGenericAlertCopy: AssistanceAlertCopy = {
  reason: "The camera needs attention.",
  fix: "Check that it is connected, then RETRY.",
};

/** The panel's Operator Assistance alert for the brain's cause; a camera cause reads the camera's own failure kind when it has one. */
export function assistanceAlertCopy(reason: AssistanceReason, cameraFailure: CameraFailureKind | null): AssistanceAlertCopy {
  switch (reason) {
    case "camera":
      return cameraFailure === null
        ? cameraGenericAlertCopy
        : { reason: cameraReasonCopy[cameraFailure], fix: cameraFixCopy[cameraFailure] };
    case "storage":
      return storageAlertCopy;
    case "guestDisplay":
      return guestDisplayAlertCopy;
  }
}

/** The text link back to the running-Event panel, on every view but the Event list. */
export const returnToRunningEventLabel = "GO TO THE RUNNING EVENT";

export const singleScreenCopy = "The booth is running on this screen.";

const stepForPanel: Readonly<Record<ProtocolStep, RunningEventStep>> = {
  start: "start",
  template: "template",
  photographs: "photographs",
  filters: "filters",
  keepsake: "keepsake",
  guestGallery: "guest-gallery",
  operatorAssistance: "operator-assistance",
};

export function panelStep(step: ProtocolStep): RunningEventStep {
  return stepForPanel[step];
}

export const endEventDialog = {
  title: "End this Event?",
  body: "Anyone using the booth will be stopped. Their photographs are kept.",
  confirmLabel: "END EVENT",
  cancelLabel: "KEEP RUNNING",
} as const;

/** Closing FOTOHAVN with an Event running: shown when the operator closes the Operator window. */
export const closeAppDialog = {
  title: "End the running Event and close FOTOHAVN?",
  body: "Anyone using the booth will be stopped. Their photographs are kept.",
  confirmLabel: "END EVENT AND CLOSE",
  cancelLabel: "KEEP RUNNING",
} as const;

export const leaveDialog = {
  title: "Save your changes?",
  body: "If you leave now, what you changed is lost unless you save it.",
  save: "SAVE",
  discard: "DISCARD CHANGES",
  keepEditing: "KEEP EDITING",
} as const;

/** The delete confirmation's sentence: the Event's name and what goes with it. */
export function deleteDialogBody(
  eventName: string,
  counts: { readonly guestCycles: number; readonly photoStrips: number; readonly captures: number } | null,
): string {
  const removed = counts === null ? null : formatDeletionCounts(counts);
  return removed === null
    ? `“${eventName}” will be removed. It has no photographs yet. This can't be undone.`
    : `“${eventName}” will be removed with ${removed}. This can't be undone.`;
}

export const deleteDialog = {
  title: "Delete this Event?",
  confirmLabel: "DELETE EVENT",
  cancelLabel: "KEEP EVENT",
} as const;

export const deleteGuestCycleDialog = {
  title: "Delete this Guest Cycle?",
  confirmLabel: "DELETE GUEST CYCLE",
  cancelLabel: "KEEP GUEST CYCLE",
} as const;

/** The Guest Cycle's delete sentence: when it began and how many files go with it (its Captures and its Photo Strip). */
export function deleteGuestCycleBody(time: string | null, fileCount: number | null): string {
  const subject = time === null ? "This Guest Cycle" : `The Guest Cycle from ${time}`;
  const files = fileCount === null ? " and its files" : fileCount === 0 ? "" : fileCount === 1 ? " and its file" : ` and its ${String(fileCount)} files`;
  return `${subject}${files} will be removed from this computer. This can't be undone.`;
}
