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

export function formatGuestCycles(count: number): string {
  return count === 1 ? "1 Guest Cycle" : `${String(count)} Guest Cycles`;
}

const rejectionCopy: Partial<Record<RejectReason, string>> = {
  notAvailable: "That isn't ready yet.",
  eventRunning: "End the running Event to start another.",
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
