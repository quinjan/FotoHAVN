import type { EventSetupStartFailure } from "../../protocol/generated.ts";

/** Event setup's steps (DESIGN.md, Event setup): the sections grouped into five steps, any of which can be opened. */
export type SetupStepId = "event" | "hardware" | "templates" | "timing" | "guests";

export interface SetupStep {
  readonly id: SetupStepId;
  /** The step's label after its number, as DESIGN.md's copy table writes it (`02 Hardware`). */
  readonly label: string;
}

export const setupSteps: readonly SetupStep[] = [
  { id: "event", label: "Event" },
  { id: "hardware", label: "Hardware" },
  { id: "templates", label: "Templates" },
  { id: "timing", label: "Timing" },
  { id: "guests", label: "Guests" },
];

/** `01`, `02`, … for a step. */
export function stepNumber(id: SetupStepId): string {
  return String(setupSteps.findIndex((step) => step.id === id) + 1).padStart(2, "0");
}

/** The step that shows a field named in the brain's `fieldErrors`. */
export function stepForField(field: string): SetupStepId {
  if (field.startsWith("timers.")) {
    return "timing";
  }
  switch (field) {
    case "camera":
    case "guestDisplay":
      return "hardware";
    case "templates":
    case "defaultTemplate":
    case "looks":
    case "defaultLook":
      return "templates";
    case "pin":
      return "guests";
    default:
      return "event";
  }
}

/** The step to open when Start didn't go ahead: where the operator can fix what the brain named. */
export function stepForStartFailure(failure: EventSetupStartFailure): SetupStepId | null {
  switch (failure.code) {
    case "cameraNotEligible":
    case "guestDisplayMissing":
    case "guestScreenNotReady":
      return "hardware";
    case "pinMissing":
      return "guests";
    default:
      return null;
  }
}
