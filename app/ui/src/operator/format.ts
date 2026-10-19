import type { RejectReason } from "../protocol/generated.ts";
import type { CommandResult, TestHookName, TestResult } from "../brain/types.ts";

// Fixed English names, so the Event list reads the same whatever the operator machine's language or region.
const weekdays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"] as const;
const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"] as const;

function formatDay(weekday: number, day: number, month: number, year: number): string {
  return `${weekdays[weekday] ?? ""} ${String(day)} ${months[month] ?? ""} ${String(year)}`;
}

/** An Event date (`yyyy-MM-dd`, a calendar day with no time zone), e.g. "Sat 14 Nov 2026". */
export function formatEventDate(date: string): string {
  const parsed = new Date(`${date}T00:00:00Z`);
  return Number.isNaN(parsed.getTime())
    ? date
    : formatDay(parsed.getUTCDay(), parsed.getUTCDate(), parsed.getUTCMonth(), parsed.getUTCFullYear());
}

/** A last-used instant (ISO date-time) as a local calendar day, e.g. "Last used Sat 3 Oct 2026"; null means the Event was never run. */
export function formatLastUsed(lastUsed: string | null): string {
  if (lastUsed === null) {
    return "Not used yet";
  }
  const parsed = new Date(lastUsed);
  return Number.isNaN(parsed.getTime())
    ? lastUsed
    : `Last used ${formatDay(parsed.getDay(), parsed.getDate(), parsed.getMonth(), parsed.getFullYear())}`;
}

/** A Guest Cycle's start as the operator reads it: local time with the date, e.g. "13 Oct, 8:42 PM". */
export function formatGuestCycleTime(startedAt: string): string {
  const parsed = new Date(startedAt);
  if (Number.isNaN(parsed.getTime())) {
    return startedAt;
  }
  const hours = parsed.getHours();
  const minutes = String(parsed.getMinutes()).padStart(2, "0");
  return `${String(parsed.getDate())} ${months[parsed.getMonth()] ?? ""}, ${String(hours % 12 === 0 ? 12 : hours % 12)}:${minutes} ${hours < 12 ? "AM" : "PM"}`;
}

export function formatGuestCycles(count: number): string {
  return count === 1 ? "1 Guest Cycle" : `${String(count)} Guest Cycles`;
}

/**
 * The Event Gallery's count line, e.g. "8 Guest Cycles, 4 completed". The gallery loads 40 Guest Cycles at a time,
 * newest first, so while more remain the line says it counts only the newest ones loaded ("Newest 40 Guest Cycles, 22 completed").
 */
export function formatGalleryCount(loaded: number, completed: number, moreToLoad: boolean): string {
  const done = completed === 0 ? "none completed" : `${String(completed)} completed`;
  return moreToLoad ? `Newest ${formatGuestCycles(loaded)}, ${done}` : `${formatGuestCycles(loaded)}, ${done}`;
}

const minuteMs = 60_000;

/**
 * How long an Event has been running, e.g. "Just started", "12 min", "1 h 12 min", or "2 d 3 h". `startedAt` is the
 * brain's ISO instant and `now` is the page's clock in epoch milliseconds; an instant in the future counts as now.
 */
export function formatTimeSinceStart(startedAt: string, now: number): string {
  const started = new Date(startedAt).getTime();
  if (Number.isNaN(started)) {
    return "—";
  }
  const minutes = Math.floor(Math.max(0, now - started) / minuteMs);
  if (minutes < 1) {
    return "Just started";
  }
  if (minutes < 60) {
    return `${String(minutes)} min`;
  }
  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    const rest = minutes % 60;
    return rest === 0 ? `${String(hours)} h` : `${String(hours)} h ${String(rest)} min`;
  }
  const days = Math.floor(hours / 24);
  const restHours = hours % 24;
  return restHours === 0 ? `${String(days)} d` : `${String(days)} d ${String(restHours)} h`;
}

function countText(count: number, one: string, many: string): string {
  return count === 1 ? `1 ${one}` : `${String(count)} ${many}`;
}

/** What deleting an Event removes, e.g. "12 Guest Cycles, 11 Photo Strips, and 41 photographs"; null when it has nothing yet. */
export function formatDeletionCounts(counts: { readonly guestCycles: number; readonly photoStrips: number; readonly captures: number }): string | null {
  if (counts.guestCycles === 0 && counts.photoStrips === 0 && counts.captures === 0) {
    return null;
  }
  const parts = [
    countText(counts.guestCycles, "Guest Cycle", "Guest Cycles"),
    countText(counts.photoStrips, "Photo Strip", "Photo Strips"),
    countText(counts.captures, "photograph", "photographs"),
  ];
  return `${parts[0] ?? ""}, ${parts[1] ?? ""}, and ${parts[2] ?? ""}`;
}

const rejectionCopy: Partial<Record<RejectReason, string>> = {
  notAvailable: "That isn't ready yet.",
  eventRunning: "End the running Event to start another.",
  validationFailed: "Some details need attention.",
};

const didntWork = "That didn't work. Try again.";

function localFailureNotice(error: string): string {
  return error === "timeout" || error === "notConnected" || error === "disconnected"
    ? "The booth didn't answer. Try again."
    : didntWork;
}

/** The polite line shown when a command doesn't go through; null when it was accepted. */
export function commandNotice(result: CommandResult): string | null {
  switch (result.status) {
    case "accepted":
      return null;
    case "rejected":
      return (result.reason === null ? undefined : rejectionCopy[result.reason]) ?? didntWork;
    case "failed":
      return localFailureNotice(result.error);
  }
}

/** The polite line shown when an E2E test hook doesn't go through; null when it did. */
export function hookNotice(result: TestResult<TestHookName>): string | null {
  return result.ok ? null : localFailureNotice(result.error);
}
