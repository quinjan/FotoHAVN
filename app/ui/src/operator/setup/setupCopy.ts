import type {
  EventSetupCameraView,
  EventSetupErrorCode,
  EventSetupFieldError,
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
      return `Enter ${String(timer.min)} to ${String(timer.max)} seconds.`;
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

/** Whether an error blocks only `SAVE AND START`: a draft without an Operator PIN still saves. */
export function blocksOnlyStart(error: EventSetupFieldError): boolean {
  return error.field === "pin" && error.code === "pinRequired";
}

/** The heading's count of what blocks saving. */
export function errorSummary(count: number): string {
  return count === 1
    ? "One thing needs attention before this Event can be saved."
    : `${String(count)} things need attention before this Event can be saved.`;
}

/**
 * The error summary's heading, which is also the polite announcement after a failed save: what blocks saving, or, when
 * only the missing Operator PIN is left, that the Event can be saved but not started.
 */
export function errorSummaryHeading(errors: readonly EventSetupFieldError[]): string {
  const blockers = errors.filter((error) => !blocksOnlyStart(error)).length;
  if (blockers > 0) {
    return errorSummary(blockers);
  }
  return errors.length === 0 ? "" : "This Event can be saved, but it needs an Operator PIN to start.";
}

const fieldNames: Readonly<Record<string, string>> = {
  name: "Name",
  date: "Date",
  venue: "Venue",
  customer: "Customer or host",
  notes: "Notes",
};

/**
 * One line of the error summary. The summary reads away from its fields, so each line names the field it is about;
 * a message that already does (Offer at least one Template.) is the field's own.
 */
export function errorSummaryItem(
  field: string,
  code: EventSetupErrorCode,
  timers: readonly PhaseTimerOption[],
  otherBlockers: boolean,
): string {
  const name = fieldNames[field];
  switch (code) {
    case "required":
      return field === "name" ? "Name the Event." : `${name ?? "This"} is needed.`;
    case "tooLong":
      return name === undefined ? "That's too long." : `${name} is too long.`;
    case "invalidDate":
      return "The date isn't valid. Choose another.";
    case "timerOutOfRange": {
      const timer = timers.find((candidate) => `timers.${candidate.phase}` === field);
      return timer === undefined
        ? "A timer needs a number of seconds in range."
        : `${timerNames[timer.phase]} needs ${String(timer.min)} to ${String(timer.max)} seconds.`;
    }
    case "pinFormat":
      return "The Operator PIN needs 4 to 6 digits, 0 to 9 only.";
    case "pinRequired":
      return otherBlockers ? "Set an Operator PIN to start this Event. Saving doesn't need one." : "Set an Operator PIN to start this Event.";
    default:
      return fieldErrorText(field, code, timers);
  }
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

/** The names a timer's controls carry for assistive technology, and the unit written after its seconds. */
export const timerControlCopy = {
  unit: "s",
  switchName: (timer: string) => `${timer} timer`,
  secondsName: (timer: string) => `${timer} seconds`,
} as const;

/**
 * The Timing step's one shared line: the range the timers that can be off run in, and what off means. It is built from
 * the brain's own limits, and a timer whose range differs (Keepsake) says its own.
 */
export function timingIntro(timers: readonly PhaseTimerOption[]): string {
  const range = groupRange(timers);
  const lead = range === null ? "" : `Most timers run ${String(range.min)} to ${String(range.max)} seconds. `;
  return `${lead}Turn a timer off and that step waits for the guest to continue. If they walk away, only ending the Event clears it.`;
}

/** The limits shared by the timers that can be off; null when there are none. */
export function groupRange(timers: readonly PhaseTimerOption[]): { readonly min: number; readonly max: number } | null {
  const first = timers.find((timer) => timer.canBeOff);
  return first === undefined ? null : { min: first.min, max: first.max };
}

/** What differs for one timer: the outcome, and its limits when they aren't the group's or when it can't be off. */
export function timerHelp(timer: PhaseTimerOption, timers: readonly PhaseTimerOption[]): string {
  const range = groupRange(timers);
  const shared = timer.canBeOff && range !== null && timer.min === range.min && timer.max === range.max;
  return [shared ? null : `${String(timer.min)} to ${String(timer.max)} seconds.`, timer.canBeOff ? null : "Always on.", timerRunsOut[timer.phase]]
    .filter((line) => line !== null)
    .join(" ");
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
    guestScreenNotReady: "The guest display didn't respond. Check it, then try again.",
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
  stepsLabel: "Event setup steps",
  stepNeedsAttention: ", needs attention",
  continueTo: (step: string) => `CONTINUE TO ${step.toUpperCase()}`,
  previousStep: "PREVIOUS STEP",
  summary: {
    eyebrow: "SUMMARY",
    untitled: "New Event",
    event: "Event",
    hardware: "Hardware",
    templates: (count: number) => (count === 1 ? "1 Template" : `${String(count)} Templates`),
    templatesName: "Templates",
    looks: (count: number) => (count === 1 ? "1 Look" : `${String(count)} Looks`),
    looksName: "Looks",
    timing: "Timing",
    guests: "Guests and PIN",
    needsAttention: "Needs attention",
    noCamera: "No camera chosen",
    noDisplay: "None chosen",
    noneOffered: "None offered",
    byDefault: (name: string) => `${name} by default`,
    isDefault: "Default",
    allOn: "All on",
    someOff: (count: number) => `${String(count)} off`,
    off: "Off",
    on: "On",
    seconds: (seconds: number) => `${String(seconds)} s`,
    countdown: (seconds: number) => `${String(seconds)} seconds`,
    pinSet: "PIN set",
    pinEntered: "New PIN entered",
    pinNotSet: "PIN not set",
    edit: "EDIT",
    editLabel: (group: string) => `Edit ${group}`,
    rows: {
      name: "Name",
      date: "Date",
      venue: "Venue",
      customer: "Customer or host",
      camera: "Camera",
      mirror: "Mirror",
      countdown: "Countdown",
      guestDisplay: "Guest display",
      printer: "Printer",
      guestGallery: "Guest gallery",
      guestSounds: "Guest sounds",
      pin: "Operator PIN",
    },
  },
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
    guestSounds: "Guest sounds",
    guestSoundsHelp: "Plays a tick during the countdown and a shutter sound for each photograph.",
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
