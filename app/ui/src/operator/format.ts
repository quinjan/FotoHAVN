import type { RejectReason } from "../protocol/generated.ts";
import type { CommandResult } from "../brain/types.ts";

const dayFormat = new Intl.DateTimeFormat("en", { dateStyle: "medium", timeZone: "UTC" });
const localDayFormat = new Intl.DateTimeFormat("en", { dateStyle: "medium" });

/** An Event date (`yyyy-MM-dd`, a calendar day with no time zone), e.g. "Oct 3, 2026". */
export function formatEventDate(date: string): string {
  const parsed = new Date(`${date}T00:00:00Z`);
  return Number.isNaN(parsed.getTime()) ? date : dayFormat.format(parsed);
}

/** A last-used instant (ISO date-time) as a local calendar day; null means the Event was never run. */
export function formatLastUsed(lastUsed: string | null): string {
  if (lastUsed === null) {
    return "Not used yet";
  }
  const parsed = new Date(lastUsed);
  return Number.isNaN(parsed.getTime()) ? lastUsed : localDayFormat.format(parsed);
}

export function formatGuestCycles(count: number): string {
  return count === 1 ? "1 Guest Cycle" : `${String(count)} Guest Cycles`;
}

const rejectionCopy: Partial<Record<RejectReason, string>> = {
  notAvailable: "That isn't ready yet.",
  eventRunning: "End the running Event to start another.",
};

/** The polite line shown when a command doesn't go through; null when it was accepted. */
export function commandNotice(result: CommandResult): string | null {
  switch (result.status) {
    case "accepted":
      return null;
    case "rejected":
      return (result.reason === null ? undefined : rejectionCopy[result.reason]) ?? "That didn't work. Try again.";
    case "failed":
      return result.error === "timeout" || result.error === "notConnected" || result.error === "disconnected"
        ? "The booth didn't answer. Try again."
        : "That didn't work. Try again.";
  }
}
