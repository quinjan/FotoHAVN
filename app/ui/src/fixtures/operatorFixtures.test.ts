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
} from "./operatorFixtures.ts";

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

  it("names one storage error fixture per kind, in kebab case", () => {
    const names = Object.keys(operatorFixtures()).filter((name) => name.startsWith("operator-storage-error-"));
    expect(names).toHaveLength(storageErrorKinds.length);
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
});
