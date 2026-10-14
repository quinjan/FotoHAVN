import { describe, expect, it } from "vitest";
import { commandNotice, formatDeletionCounts, formatEventDate, formatGuestCycles, formatLastUsed, formatTimeSinceStart, hookNotice } from "./format.ts";

describe("operator formatting", () => {
  it("formats an Event date as a calendar day, whatever the time zone", () => {
    expect(formatEventDate("2026-01-01")).toBe("Thu 1 Jan 2026");
    expect(formatEventDate("2026-11-14")).toBe("Sat 14 Nov 2026");
    expect(formatEventDate("2026-12-31")).toBe("Thu 31 Dec 2026");
    expect(formatEventDate("not a date")).toBe("not a date");
  });

  it("formats last used, or says the Event was never used", () => {
    expect(formatLastUsed(null)).toBe("Not used yet");
    expect(formatLastUsed("2026-10-03T12:00:00Z")).toBe("Last used Sat 3 Oct 2026");
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

  it("explains each test hook outcome politely", () => {
    expect(hookNotice({ ok: true, result: {} })).toBeNull();
    expect(hookNotice({ ok: false, error: "hookFailed" })).toBe("That didn't work. Try again.");
    expect(hookNotice({ ok: false, error: "unknownCamera" })).toBe("That didn't work. Try again.");
    expect(hookNotice({ ok: false, error: "timeout" })).toBe("The booth didn't answer. Try again.");
    expect(hookNotice({ ok: false, error: "disconnected" })).toBe("The booth didn't answer. Try again.");
  });

  it("explains a save the brain refused for its fields", () => {
    expect(commandNotice({ status: "rejected", reason: "validationFailed" })).toBe("Some details need attention.");
  });

  describe("time since start", () => {
    const started = "2026-10-13T09:30:00Z";
    const at = (minutes: number) => Date.parse(started) + minutes * 60_000;

    it("counts whole minutes, then hours and minutes, then days and hours", () => {
      expect(formatTimeSinceStart(started, at(0))).toBe("Just started");
      expect(formatTimeSinceStart(started, at(0) + 59_999)).toBe("Just started");
      expect(formatTimeSinceStart(started, at(1))).toBe("1 min");
      expect(formatTimeSinceStart(started, at(59))).toBe("59 min");
      expect(formatTimeSinceStart(started, at(60))).toBe("1 h");
      expect(formatTimeSinceStart(started, at(72))).toBe("1 h 12 min");
      expect(formatTimeSinceStart(started, at(23 * 60 + 59))).toBe("23 h 59 min");
      expect(formatTimeSinceStart(started, at(24 * 60))).toBe("1 d");
      expect(formatTimeSinceStart(started, at(51 * 60))).toBe("2 d 3 h");
    });

    it("treats a start in the future as just started, and an unreadable one as a dash", () => {
      expect(formatTimeSinceStart(started, at(-5))).toBe("Just started");
      expect(formatTimeSinceStart("not a time", at(5))).toBe("—");
    });
  });

  describe("deletion counts", () => {
    it("lists Guest Cycles, Photo Strips, and photographs", () => {
      expect(formatDeletionCounts({ guestCycles: 12, photoStrips: 11, captures: 41 })).toBe("12 Guest Cycles, 11 Photo Strips, and 41 photographs");
      expect(formatDeletionCounts({ guestCycles: 1, photoStrips: 1, captures: 1 })).toBe("1 Guest Cycle, 1 Photo Strip, and 1 photograph");
      expect(formatDeletionCounts({ guestCycles: 1, photoStrips: 0, captures: 2 })).toBe("1 Guest Cycle, 0 Photo Strips, and 2 photographs");
    });

    it("has nothing to list for an Event that was never used", () => {
      expect(formatDeletionCounts({ guestCycles: 0, photoStrips: 0, captures: 0 })).toBeNull();
    });
  });
});
