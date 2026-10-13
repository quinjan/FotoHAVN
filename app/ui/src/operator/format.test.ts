import { describe, expect, it } from "vitest";
import { commandNotice, formatEventDate, formatGuestCycles, formatLastUsed } from "./format.ts";

describe("operator formatting", () => {
  it("formats an Event date as a calendar day, whatever the time zone", () => {
    expect(formatEventDate("2026-01-01")).toBe("Jan 1, 2026");
    expect(formatEventDate("2026-12-31")).toBe("Dec 31, 2026");
    expect(formatEventDate("not a date")).toBe("not a date");
  });

  it("formats last used, or says the Event was never used", () => {
    expect(formatLastUsed(null)).toBe("Not used yet");
    expect(formatLastUsed("2026-10-03T12:00:00Z")).toBe("Oct 3, 2026");
    expect(formatLastUsed("yesterday")).toBe("yesterday");
  });

  it("counts Guest Cycles", () => {
    expect(formatGuestCycles(0)).toBe("0 Guest Cycles");
    expect(formatGuestCycles(1)).toBe("1 Guest Cycle");
    expect(formatGuestCycles(2)).toBe("2 Guest Cycles");
  });

  it("explains each command outcome politely", () => {
    expect(commandNotice({ status: "accepted" })).toBeNull();
    expect(commandNotice({ status: "rejected", reason: "notAvailable" })).toBe("That isn't ready yet.");
    expect(commandNotice({ status: "rejected", reason: "eventRunning" })).toBe("End the running Event to start another.");
    expect(commandNotice({ status: "rejected", reason: "wrongRole" })).toBe("That didn't work. Try again.");
    expect(commandNotice({ status: "rejected", reason: null })).toBe("That didn't work. Try again.");
    expect(commandNotice({ status: "failed", error: "timeout" })).toBe("The booth didn't answer. Try again.");
    expect(commandNotice({ status: "failed", error: "notConnected" })).toBe("The booth didn't answer. Try again.");
    expect(commandNotice({ status: "failed", error: "disconnected" })).toBe("The booth didn't answer. Try again.");
    expect(commandNotice({ status: "failed", error: "invalidEnvelope" })).toBe("That didn't work. Try again.");
  });
});
