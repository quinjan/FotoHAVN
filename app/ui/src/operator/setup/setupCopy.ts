import type {
  EventSetupCameraView,
  EventSetupErrorCode,
  EventSetupStartFailure,
  PhaseTimer,
  PhaseTimerOption,
  StartFailureCode,
} from "../../protocol/generated.ts";
import { cameraReasonCopy, noCamerasCopy } from "../../components/CameraDiagnosticsScreen/cameraText.ts";
import { eventRunningCopy } from "../copy.ts";

/** Event setup's static copy (DESIGN.md, Copy: Event setup). The brain sends codes and facts; the words live here. */

/** The Core limits (`EventSetupLimits`); the inputs stop at them, and the brain checks them again at save. */
export const limits = { name: 80, venue: 120, customer: 120, notes: 1000, pinDigits: 6 } as const;

export const fieldIds = {
  name: "setup-name",
  date: "setup-date",
  venue: "setup-venue",
  customer: "setup-customer",
  notes: "setup-notes",
  camera: "setup-camera",
  guestDisplay: "setup-guest-display",
  templates: "setup-templates",
  looks: "setup-looks",
  pin: "setup-pin",
  pinConfirmation: "setup-pin-confirmation",
} as const;

export function timerFieldId(phase: PhaseTimer): string {
  return `setup-timer-${phase}`;
}

/** The field names in the brain's `fieldErrors`, in the order the form shows them (the first invalid one takes focus). */
export function fieldOrder(timers: readonly PhaseTimerOption[]): readonly string[] {
  return [
    "name",
    "date",
    "venue",
    "customer",
    "notes",
    "camera",
    "guestDisplay",
    "templates",
    "defaultTemplate",
    "looks",
    "defaultLook",
    ...timers.map((timer) => `timers.${timer.phase}`),
    "pin",
  ];
}

/** The element a field's error sends focus to; a group's first control is found inside it. */
export function focusTargetId(field: string, code: EventSetupErrorCode): string {
  switch (field) {
    case "defaultTemplate":
      return fieldIds.templates;
    case "defaultLook":
      return fieldIds.looks;
    case "pin":
      return code === "pinMismatch" ? fieldIds.pinConfirmation : fieldIds.pin;
    default:
      return field.startsWith("timers.") ? timerFieldId(field.slice("timers.".length) as PhaseTimer) : fieldIds[field as keyof typeof fieldIds];
  }
}

const tooLongText: Readonly<Record<string, string>> = {
  name: `Keep the name to ${String(limits.name)} characters or fewer.`,
  venue: `Keep the venue to ${String(limits.venue)} characters or fewer.`,
  customer: `Keep this to ${String(limits.customer)} characters or fewer.`,
  notes: "Keep the notes to 1,000 characters or fewer.",
};

/** One line per error code, for the field it belongs to; a timer's range comes from the brain's own limits. */
export function fieldErrorText(field: string, code: EventSetupErrorCode, timers: readonly PhaseTimerOption[]): string {
  switch (code) {
    case "required":
      return field === "name" ? "Name the Event." : "This is needed.";
    case "tooLong":
      return tooLongText[field] ?? "That's too long.";
    case "invalidDate":
      return "That date isn't valid. Choose another.";
    case "atLeastOneTemplate":
      return "Offer at least one Template.";
    case "defaultTemplateNotOffered":
      return "Choose the default from the Templates you offer.";
    case "atLeastOneLook":
      return "Offer at least one Look.";
    case "defaultLookNotOffered":
      return "Choose the default from the Looks you offer.";
    case "timerOutOfRange": {
      const timer = timers.find((candidate) => `timers.${candidate.phase}` === field);
      if (timer === undefined) {
        return "Enter a number of seconds in range.";
      }
      return timer.canBeOff
        ? `Enter 0 to turn it off, or ${String(timer.min)} to ${String(timer.max)} seconds.`
        : `Enter ${String(timer.min)} to ${String(timer.max)} seconds.`;
    }
    case "pinFormat":
      return "Use 4 to 6 digits, 0 to 9 only.";
    case "pinMismatch":
      return "The two PINs don't match.";
    case "pinRequired":
      return "Set an Operator PIN to start this Event.";
    case "cameraRequired":
      return "Choose a camera.";
    case "displayRequired":
      return "Choose the guest display.";
  }
}

/** The announcement after a failed save: how many things need attention. */
export function errorSummary(count: number): string {
  return count === 1
    ? "One thing needs attention before this Event can be saved."
    : `${String(count)} things need attention before this Event can be saved.`;
}

export const timerNames: Readonly<Record<PhaseTimer, string>> = {
  template: "Template",
  photographsCapture: "Photographs: capture",
  photographsReview: "Photographs: review",
  filters: "Filters",
  keepsake: "Keepsake",
  guestGallery: "Guest gallery view",
};

/** What happens when the phase's time runs out (implementation plan, Phase timers). */
export const timerRunsOut: Readonly<Record<PhaseTimer, string>> = {
  template: "When time runs out, the booth continues with the selected Template.",
  photographsCapture: "When time runs out, the booth takes the photographs that are still empty.",
  photographsReview: "When time runs out, the booth keeps the original photographs and moves on to Filters.",
  filters: "When time runs out, the booth finishes the strip with the selected Look.",
  keepsake: "When time runs out, the booth returns to Start.",
  guestGallery: "When time runs out, the booth returns to Start.",
};

/** What it means to turn a timer off, shown beside the fields that can be off. */
export const timerOffCopy = "If it is off, a guest stays on this step until someone continues or uses START OVER.";

/** The timer's state in words when it is off; a timer that is on says nothing here. */
export const timerIsOff = "Off now.";

/** The range line under a timer: its limits, and whether 0 turns it off. */
export function timerRange(timer: PhaseTimerOption): string {
  return timer.canBeOff
    ? `${String(timer.min)} to ${String(timer.max)} seconds. Enter 0 for Off.`
    : `${String(timer.min)} to ${String(timer.max)} seconds. This timer is always on.`;
}

export interface CameraLine {
  readonly text: string;
  readonly tone: "neutral" | "alert";
}

/**
 * The camera field's one status line, by the brain's state. Checking, Ready, Not available, In use by another app, and
 * In use by the running Event are the plan's; the privacy line is the approved copy, exactly.
 */
export function cameraLine(camera: EventSetupCameraView): CameraLine | null {
  switch (camera.state) {
    case "none":
      return camera.cameras.length === 0 ? { text: noCamerasCopy, tone: "neutral" } : null;
    case "checking":
      return { text: "Checking", tone: "neutral" };
    case "ready":
      return { text: "Ready", tone: "neutral" };
    case "notAvailable":
      return { text: camera.reason === null ? "Not available" : `Not available. ${cameraReasonCopy[camera.reason]}`, tone: "alert" };
    case "inUseByAnotherApp":
      return { text: "In use by another app", tone: "alert" };
    case "inUseByRunningEvent":
      return { text: "In use by the running Event. The preview waits until that Event ends.", tone: "neutral" };
    case "notChecked":
      return { text: "Not checked while an Event is running. You can still save it.", tone: "neutral" };
    case "missing":
      return { text: "This camera isn't connected. FOTOHAVN keeps it until you choose another.", tone: "alert" };
  }
}

/** Why Start didn't go ahead, in words; the brain's code decides which line. */
export function startFailureText(failure: EventSetupStartFailure): string {
  const lines: Readonly<Record<Exclude<StartFailureCode, "cameraNotEligible">, string>> = {
    eventRunning: eventRunningCopy,
    guestDisplayMissing: "The chosen guest display isn't connected.",
    storageNotWritable: "The Data folder can't be written to, so the Event can't start. Check the folder, then try again.",
    pinMissing: "Set an Operator PIN to start this Event.",
    invalidSetup: "Some details need attention before this Event can start. Check the fields below.",
  };
  if (failure.code === "cameraNotEligible") {
    return failure.cameraReason === null ? "The camera isn't ready to start." : `The camera isn't ready to start. ${cameraReasonCopy[failure.cameraReason]}`;
  }
  return lines[failure.code];
}

export const copy = {
  heading: { lead: "Set up the", turn: "Event." },
  back: "BACK TO EVENTS",
  save: "SAVE",
  saveAndStart: "SAVE AND START",
  saving: "Saving…",
  sections: {
    event: "EVENT",
    camera: "CAMERA",
    guestDisplay: "GUEST DISPLAY",
    printer: "PRINTER",
    templates: "TEMPLATES",
    filters: "FILTERS",
    timing: "TIMING",
    guests: "GUESTS",
    pin: "OPERATOR PIN",
  },
  fields: {
    name: "Name",
    date: "Date",
    venue: "Venue",
    customer: "Customer or host",
    notes: "Notes",
    camera: "Camera",
    cameraPlaceholder: "Choose a camera",
    mirror: "Mirror",
    mirrorHelp: "Flips the preview and the saved photographs, like a mirror.",
    countdown: "Countdown",
    printer: "Printer",
    printerValue: "No printer",
    templates: "Offered Templates",
    templatesHelp: "Choose the Templates guests can pick from, and mark one as the default.",
    looks: "Offered Looks",
    looksHelp: "Choose the Looks guests can pick from, and mark one as the default.",
    guestGallery: "Guest gallery",
    guestGalleryHelp: "Turn this on only with the Customer's permission.",
  },
  pin: {
    set: "PIN set",
    change: "CHANGE PIN",
    keep: "KEEP CURRENT PIN",
    label: "PIN",
    confirmation: "Confirm PIN",
    newLabel: "New PIN",
    newConfirmation: "Confirm new PIN",
    help: "4 to 6 digits. Enter it twice. It is never shown again.",
  },
} as const;
