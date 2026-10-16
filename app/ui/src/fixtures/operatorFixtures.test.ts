import { describe, expect, it } from "vitest";
import {
  cameraReasonsWithFixtures,
  eventRows,
  longEventName,
  longEventVenue,
  operatorEventList,
  operatorFixtures,
  operatorStorageError,
  storageErrorKinds,
  storageErrorKindsFromBrain,
} from "./operatorFixtures.ts";
import { operatorSetupFixtures, phaseTimers, runningEventPanel, setupView } from "./operatorSetupFixtures.ts";

describe("operator fixtures", () => {
  it("builds the requested number of Event rows, with unique ids and one running Event first", () => {
    for (const count of [0, 1, 5, 30]) {
      const rows = eventRows(count);
      expect(rows).toHaveLength(count);
      expect(new Set(rows.map((row) => row.id)).size).toBe(count);
      expect(rows.filter((row) => row.running)).toHaveLength(count === 0 ? 0 : 1);
    }
    expect(eventRows(3, false).some((row) => row.running)).toBe(false);
    expect(eventRows(5).map((row) => row.venue)).toContain(null);
    expect(eventRows(5).map((row) => row.lastUsedAt)).toContain(null);
  });

  it("uses an 80-character name and a 120-character venue for the long-text fixture", () => {
    expect(longEventName).toHaveLength(80);
    expect(longEventVenue).toHaveLength(120);
    const long = operatorFixtures()["operator-event-list-long"];
    expect(long?.presentation.eventList?.events.map((row) => row.name)).toEqual([longEventName, longEventName]);
  });

  it("wraps rows and storage errors in an operator welcome", () => {
    expect(operatorEventList(eventRows(2)).presentation).toMatchObject({ surface: "eventList", storageError: null });
    expect(operatorStorageError("locked", "C:\\Data").presentation).toMatchObject({
      surface: "storageError",
      eventList: null,
      storageError: { kind: "locked", dataPath: "C:\\Data" },
    });
  });

  it("names one storage error fixture per kind, in kebab case, leaving the kinds the brain writes to its files", () => {
    const names = Object.keys(operatorFixtures()).filter((name) => name.startsWith("operator-storage-error-"));
    expect(names).toHaveLength(storageErrorKinds.length - storageErrorKindsFromBrain.length);
    expect(names).not.toContain("operator-storage-error-disk-full");
    expect(names).toContain("operator-storage-error-not-local-disk");
    expect(names).toContain("operator-storage-error-cloud-synced");
  });

  it("writes a camera diagnostics fixture for the reasons the brain's fixtures don't cover, and one with no camera chosen", () => {
    const fixtures = operatorFixtures();
    const names = Object.keys(fixtures).filter((name) => name.startsWith("operator-camera-diagnostics-"));
    expect(names).toEqual([
      "operator-camera-diagnostics-choose-camera",
      ...cameraReasonsWithFixtures.map((reason) => `operator-camera-diagnostics-reason-${reason.replace(/[A-Z]/g, (letter) => `-${letter.toLowerCase()}`)}`),
    ]);
    expect(fixtures["operator-camera-diagnostics-reason-exclusive-lost"]?.presentation).toMatchObject({
      surface: "cameraDiagnostics",
      eventList: null,
      cameraDiagnostics: { state: "notAvailable", reason: "exclusiveLost", selectedDeviceId: "fake-camera" },
    });
    expect(fixtures["operator-camera-diagnostics-choose-camera"]?.presentation.cameraDiagnostics).toMatchObject({
      state: "idle",
      selectedDeviceId: null,
    });
  });

  it("leaves privacy denied and in use to the brain's generated fixtures", () => {
    expect(cameraReasonsWithFixtures).not.toContain("privacyDenied");
    expect(cameraReasonsWithFixtures).not.toContain("inUseByAnotherApp");
  });

  describe("running-Event and confirmation fixtures", () => {
    const fixtures = operatorFixtures();
    const presentation = (name: string) => {
      const message = fixtures[name];
      if (message === undefined) {
        throw new Error(`no fixture ${name}`);
      }
      return message.presentation;
    };

    it("pins the running-Event panel above rows whose first row is the running Event", () => {
      for (const name of [
        "operator-list-running",
        "operator-list-running-display-disconnected",
        "operator-list-running-camera-not-available",
        "operator-list-running-camera-checking",
        "operator-list-running-assistance",
        "operator-list-confirm-end",
        "operator-list-confirm-close",
      ]) {
        const { eventList, runningEvent } = presentation(name);
        expect(eventList?.events[0]?.running, name).toBe(true);
        expect(runningEvent?.name, name).toBe(eventList?.events[0]?.name);
        expect(runningEvent?.eventId, name).toBe(eventList?.events[0]?.id);
      }
    });

    it("writes each panel state: normal, display disconnected, camera not available, checking, and assistance", () => {
      expect(presentation("operator-list-running").runningEvent).toMatchObject({ guestDisplay: { state: "connected" }, camera: { state: "ready" }, assistance: null });
      expect(presentation("operator-list-running-display-disconnected").runningEvent?.guestDisplay.state).toBe("disconnected");
      expect(presentation("operator-list-running-camera-not-available").runningEvent?.camera.state).toBe("notAvailable");
      expect(presentation("operator-list-running-camera-checking").runningEvent?.camera.state).toBe("checking");
      expect(presentation("operator-list-running-assistance").runningEvent).toMatchObject({ currentStep: "operatorAssistance", assistance: { reason: "guestDisplay" } });
    });

    it("writes the delete and end confirmations, and a notice, over an Event list", () => {
      expect(presentation("operator-list-confirm-delete").overlays.confirmation).toMatchObject({ kind: "deleteEvent", counts: { guestCycles: 12, photoStrips: 11, captures: 41 } });
      expect(presentation("operator-list-confirm-delete").runningEvent).toBeNull();
      expect(presentation("operator-list-confirm-end").overlays.confirmation).toMatchObject({ kind: "endEvent", eventName: null, counts: null });
      expect(presentation("operator-list-confirm-close").overlays.confirmation).toMatchObject({ kind: "closeApp", eventName: null, counts: null, guestCycle: null });
      expect(presentation("operator-list-notice").overlays.notice).toEqual({ code: "deleteFailed" });
      expect(presentation("operator-single-screen")).toMatchObject({ surface: "guestScreenActive", eventList: null, runningEvent: { singleScreen: true } });
    });

    it("starts the running Event about an hour and a quarter ago", () => {
      const startedAt = Date.parse(runningEventPanel().startedAt);
      expect(Date.now() - startedAt).toBeGreaterThan(71 * 60_000);
      expect(Date.now() - startedAt).toBeLessThan(73 * 60_000);
    });
  });

  describe("Event setup fixtures", () => {
    const fixtures = operatorSetupFixtures();
    const view = (name: string) => {
      const found = fixtures[name]?.presentation.eventSetup;
      if (found === undefined || found === null) {
        throw new Error(`no setup fixture ${name}`);
      }
      return found;
    };

    it("names one fixture per state of the form", () => {
      expect(Object.keys(fixtures)).toEqual([
        "operator-setup-new",
        "operator-setup-new-errors",
        "operator-setup-errors-offered",
        "operator-setup-editing",
        "operator-setup-camera-checking",
        "operator-setup-camera-not-available",
        "operator-setup-camera-in-use",
        "operator-setup-camera-in-use-by-running-event",
        "operator-setup-camera-not-checked",
        "operator-setup-camera-missing",
        "operator-setup-camera-none-found",
        "operator-setup-display-missing",
        "operator-setup-covers-operator-screen",
        "operator-setup-leave-confirmation",
        "operator-setup-start-failure-display",
        "operator-setup-start-failure-camera",
        "operator-setup-start-failure-pin",
        "operator-setup-start-failure-guest-screen",
        "operator-setup-assistance",
        "operator-setup-saving",
        "operator-setup-pin-changing",
      ]);
      for (const message of Object.values(fixtures)) {
        expect(message.presentation.surface).toBe("eventSetup");
        expect(message.presentation.eventSetup).not.toBeNull();
      }
    });

    it("opens a new Event as the brain does: today's defaults, nothing bound, all Templates and Looks offered", () => {
      const fresh = setupView();
      expect(fresh).toMatchObject({ eventId: null, isNew: true, dirty: false, mirror: true, countdownSeconds: 5, guestGalleryEnabled: false, pin: { isSet: false, changing: false } });
      expect(fresh.camera.selectedDeviceId).toBeNull();
      expect(fresh.guestDisplay.selectedId).toBeNull();
      expect(fresh.templates.map((template) => template.id)).toEqual(["signature-strip", "panorama-four", "butter-gingham", "polka-keepsake"]);
      expect(fresh.templates.every((template) => template.offered)).toBe(true);
      expect(fresh.templates.filter((template) => template.isDefault).map((template) => template.id)).toEqual(["signature-strip"]);
      expect(fresh.looks).toHaveLength(7);
      expect(fresh.looks.filter((look) => look.isDefault).map((look) => look.id)).toEqual(["naturale-v1"]);
      expect(fresh.phaseTimers.map((timer) => [timer.phase, timer.seconds])).toEqual([
        ["template", 30],
        ["photographsCapture", 30],
        ["photographsReview", 45],
        ["filters", 30],
        ["keepsake", 12],
        ["guestGallery", 60],
      ]);
      expect(fresh.templates.find((template) => template.landscape)?.id).toBe("panorama-four");
    });

    it("writes the Phase Timer limits the plan states, with Keepsake and the guest gallery never off", () => {
      const timers = phaseTimers({ template: 0 });
      expect(timers.map((timer) => [timer.phase, timer.min, timer.max, timer.canBeOff])).toEqual([
        ["template", 20, 300, true],
        ["photographsCapture", 20, 300, true],
        ["photographsReview", 20, 300, true],
        ["filters", 20, 300, true],
        ["keepsake", 5, 60, false],
        ["guestGallery", 20, 300, false],
      ]);
      expect(timers[0]?.seconds).toBe(0);
    });

    it("writes the camera states, the display states, and the failures to start", () => {
      expect(view("operator-setup-camera-checking").camera.state).toBe("checking");
      expect(view("operator-setup-camera-not-available").camera).toMatchObject({ state: "notAvailable", reason: "privacyDenied" });
      expect(view("operator-setup-camera-in-use").camera.state).toBe("inUseByAnotherApp");
      expect(view("operator-setup-camera-in-use-by-running-event")).toMatchObject({ camera: { state: "inUseByRunningEvent" }, saveAndStartAvailable: false, saveAndStartDisabledReason: "eventRunning" });
      expect(view("operator-setup-camera-not-checked").camera.state).toBe("notChecked");
      expect(view("operator-setup-camera-missing").camera).toMatchObject({ state: "missing", selectedName: "Logitech BRIO" });
      expect(view("operator-setup-camera-none-found").camera.cameras).toEqual([]);
      expect(view("operator-setup-display-missing").guestDisplay).toMatchObject({ state: "missing", selectedName: "DELL P2419H" });
      expect(view("operator-setup-covers-operator-screen").guestDisplay).toMatchObject({ state: "connected", coversOperatorScreen: true });
      expect(view("operator-setup-start-failure-display").startFailure).toEqual({ code: "guestDisplayMissing", cameraReason: null });
      expect(view("operator-setup-start-failure-camera").startFailure).toEqual({ code: "cameraNotEligible", cameraReason: "stale" });
      expect(view("operator-setup-start-failure-pin")).toMatchObject({ startFailure: { code: "pinMissing" }, pin: { isSet: false } });
      expect(view("operator-setup-start-failure-guest-screen").startFailure).toEqual({ code: "guestScreenNotReady", cameraReason: null });
      expect(view("operator-setup-saving").saving).toBe(true);
    });

    it("shows errors only where the brain would: after a save attempt", () => {
      expect(view("operator-setup-new").showErrors).toBe(false);
      expect(view("operator-setup-new-errors")).toMatchObject({ showErrors: true, dirty: true });
      expect(view("operator-setup-new-errors").fieldErrors.map((error) => error.field)).toEqual(["name", "camera", "guestDisplay", "timers.keepsake", "pin"]);
      expect(view("operator-setup-errors-offered").fieldErrors.map((error) => error.code)).toEqual(["tooLong", "required", "atLeastOneTemplate", "defaultLookNotOffered", "pinMismatch"]);
    });

    it("carries the leave confirmation and the Operator Assistance alert", () => {
      expect(fixtures["operator-setup-leave-confirmation"]?.presentation.overlays.confirmation).toMatchObject({ kind: "leaveSetup", eventName: "Harbour gala" });
      expect(fixtures["operator-setup-assistance"]?.presentation.runningEvent?.assistance).toEqual({ reason: "camera" });
    });

    it("contains no PIN digits: only whether one is set", () => {
      for (const message of Object.values(fixtures)) {
        expect(JSON.stringify(message)).not.toMatch(/"pin":\s*"\d/);
      }
    });

    it("is served by the operator fixtures along with the list fixtures", () => {
      const all = operatorFixtures();
      for (const name of [...Object.keys(fixtures), "operator-list-running", "operator-single-screen"]) {
        expect(all[name], name).toBeDefined();
      }
    });
  });
});
