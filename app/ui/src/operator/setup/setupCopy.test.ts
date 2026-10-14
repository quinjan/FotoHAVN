import { describe, expect, it } from "vitest";
import { phaseTimers } from "../../fixtures/operatorSetupFixtures.ts";
import type { CameraFailureKind, EventSetupCameraView, EventSetupErrorCode, PhaseTimer, PhaseTimerOption, StartFailureCode } from "../../protocol/generated.ts";
import {
  cameraLine,
  errorSummary,
  fieldErrorText,
  fieldIds,
  fieldOrder,
  focusTargetId,
  startFailureText,
  timerFieldId,
  timerRange,
} from "./setupCopy.ts";

const timers = phaseTimers();
const timerFor = (phase: PhaseTimer): PhaseTimerOption => {
  const found = timers.find((timer) => timer.phase === phase);
  if (found === undefined) {
    throw new Error(`no timer ${phase}`);
  }
  return found;
};

const camera = (overrides: Partial<EventSetupCameraView>): EventSetupCameraView => ({
  cameras: [{ deviceId: "a", name: "A" }],
  selectedDeviceId: null,
  selectedName: null,
  state: "none",
  reason: null,
  previewActive: false,
  mirrored: true,
  ...overrides,
});

describe("field errors", () => {
  it.each([
    ["name", "required", "Name the Event."],
    ["name", "tooLong", "Keep the name to 80 characters or fewer."],
    ["venue", "tooLong", "Keep the venue to 120 characters or fewer."],
    ["customer", "tooLong", "Keep this to 120 characters or fewer."],
    ["notes", "tooLong", "Keep the notes to 1,000 characters or fewer."],
    ["date", "required", "This is needed."],
    ["date", "invalidDate", "That date isn't valid. Choose another."],
    ["templates", "atLeastOneTemplate", "Offer at least one Template."],
    ["defaultTemplate", "defaultTemplateNotOffered", "Choose the default from the Templates you offer."],
    ["looks", "atLeastOneLook", "Offer at least one Look."],
    ["defaultLook", "defaultLookNotOffered", "Choose the default from the Looks you offer."],
    ["pin", "pinFormat", "Use 4 to 6 digits, 0 to 9 only."],
    ["pin", "pinMismatch", "The two PINs don't match."],
    ["pin", "pinRequired", "Set an Operator PIN to start this Event."],
    ["camera", "cameraRequired", "Choose a camera."],
    ["guestDisplay", "displayRequired", "Choose the guest display."],
  ] as const satisfies readonly (readonly [string, EventSetupErrorCode, string])[])("writes %s %s in words", (field, code, text) => {
    expect(fieldErrorText(field, code, timers)).toBe(text);
  });

  it("falls back to a general line for a length error on a field that has no limit of its own", () => {
    expect(fieldErrorText("camera", "tooLong", timers)).toBe("That's too long.");
  });

  it("states a timer's own range, and 0 for Off only where it can be off", () => {
    expect(fieldErrorText("timers.template", "timerOutOfRange", timers)).toBe("Enter 0 to turn it off, or 20 to 300 seconds.");
    expect(fieldErrorText("timers.keepsake", "timerOutOfRange", timers)).toBe("Enter 5 to 60 seconds.");
    expect(fieldErrorText("timers.guestGallery", "timerOutOfRange", timers)).toBe("Enter 20 to 300 seconds.");
  });

  it("has a general line for a timer it doesn't know", () => {
    expect(fieldErrorText("timers.unknown", "timerOutOfRange", timers)).toBe("Enter a number of seconds in range.");
  });

  it("counts what needs attention", () => {
    expect(errorSummary(1)).toBe("One thing needs attention before this Event can be saved.");
    expect(errorSummary(5)).toBe("5 things need attention before this Event can be saved.");
  });
});

describe("field order and focus targets", () => {
  it("follows the form from the Event's name to the PIN, with the timers between", () => {
    expect(fieldOrder(timers)).toEqual([
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
      "timers.template",
      "timers.photographsCapture",
      "timers.photographsReview",
      "timers.filters",
      "timers.keepsake",
      "timers.guestGallery",
      "pin",
    ]);
  });

  it("sends focus to the control of each field, the group for the defaults, and the second PIN input for a mismatch", () => {
    expect(focusTargetId("name", "required")).toBe(fieldIds.name);
    expect(focusTargetId("camera", "cameraRequired")).toBe(fieldIds.camera);
    expect(focusTargetId("guestDisplay", "displayRequired")).toBe(fieldIds.guestDisplay);
    expect(focusTargetId("templates", "atLeastOneTemplate")).toBe(fieldIds.templates);
    expect(focusTargetId("defaultTemplate", "defaultTemplateNotOffered")).toBe(fieldIds.templates);
    expect(focusTargetId("defaultLook", "defaultLookNotOffered")).toBe(fieldIds.looks);
    expect(focusTargetId("timers.keepsake", "timerOutOfRange")).toBe(timerFieldId("keepsake"));
    expect(focusTargetId("pin", "pinFormat")).toBe(fieldIds.pin);
    expect(focusTargetId("pin", "pinMismatch")).toBe(fieldIds.pinConfirmation);
  });
});

describe("the camera line", () => {
  it.each([
    [{ state: "checking" }, "Checking", "neutral"],
    [{ state: "ready" }, "Ready", "neutral"],
    [{ state: "notAvailable", reason: null }, "Not available", "alert"],
    [{ state: "notAvailable", reason: "stale" }, "Not available. The camera stopped sending pictures.", "alert"],
    [{ state: "notAvailable", reason: "privacyDenied" }, "Not available. Camera access is off in Windows privacy settings.", "alert"],
    [{ state: "inUseByAnotherApp", reason: "inUseByAnotherApp" }, "In use by another app", "alert"],
    [{ state: "inUseByRunningEvent" }, "In use by the running Event. The preview waits until that Event ends.", "neutral"],
    [{ state: "notChecked" }, "Not checked while an Event is running. You can still save it.", "neutral"],
    [{ state: "missing" }, "This camera isn't connected. FOTOHAVN keeps it until you choose another.", "alert"],
    [{ state: "none", cameras: [] }, "No camera found.", "neutral"],
  ] as const satisfies readonly (readonly [Partial<EventSetupCameraView>, string, string])[])("for %o says %s", (overrides, text, tone) => {
    expect(cameraLine(camera(overrides))).toEqual({ text, tone });
  });

  it("says nothing while no camera is chosen and some are connected", () => {
    expect(cameraLine(camera({ state: "none" }))).toBeNull();
  });
});

describe("start failures", () => {
  it.each([
    ["eventRunning", "End the running Event to start another."],
    ["guestDisplayMissing", "The chosen guest display isn't connected."],
    ["storageNotWritable", "The Data folder can't be written to, so the Event can't start. Check the folder, then try again."],
    ["pinMissing", "Set an Operator PIN to start this Event."],
    ["invalidSetup", "Some details need attention before this Event can start. Check the fields below."],
  ] as const satisfies readonly (readonly [Exclude<StartFailureCode, "cameraNotEligible">, string])[])("writes %s in words", (code, text) => {
    expect(startFailureText({ code, cameraReason: null })).toBe(text);
  });

  it("gives the camera's reason when the camera isn't eligible", () => {
    const reasons: readonly CameraFailureKind[] = ["stale", "failed", "privacyDenied", "inUseByAnotherApp"];
    expect(startFailureText({ code: "cameraNotEligible", cameraReason: null })).toBe("The camera isn't ready to start.");
    expect(startFailureText({ code: "cameraNotEligible", cameraReason: "privacyDenied" })).toBe(
      "The camera isn't ready to start. Camera access is off in Windows privacy settings.",
    );
    for (const reason of reasons) {
      expect(startFailureText({ code: "cameraNotEligible", cameraReason: reason })).toMatch(/^The camera isn't ready to start\. .+\.$/);
    }
  });
});

describe("timer ranges", () => {
  it("say the limits, and whether 0 turns the timer off", () => {
    expect(timerRange(timerFor("template"))).toBe("20 to 300 seconds. Enter 0 for Off.");
    expect(timerRange(timerFor("keepsake"))).toBe("5 to 60 seconds. This timer is always on.");
  });
});
