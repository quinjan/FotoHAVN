/**
 * The Event Gallery's view models (implementation plan, Event Gallery). The operator view gets the whole record; the
 * guest view gets a reduced model with only what a guest may see, an image, its proportion, and its alternative text,
 * so no time, Template, Look, end reason, count, or action can reach the Guest Screen through these types.
 */

/** How a Guest Cycle ended (CONTEXT.md, Guest Cycles). */
export type GuestCycleOutcome = "completed" | "started-over" | "ended-by-operator" | "app-closed" | "unrecoverable";

/** The Event Gallery's words for each ending (DESIGN.md, Operator Screen copy). */
export const outcomeLabels: Readonly<Record<GuestCycleOutcome, string>> = {
  completed: "Completed",
  "started-over": "Started over",
  "ended-by-operator": "Ended by operator",
  "app-closed": "App closed",
  unrecoverable: "Unrecoverable",
};

/** An image in the gallery: a Photo Strip or a Capture, or a thumbnail of one, with its native size in pixels. */
export interface GalleryImage {
  readonly src: string;
  readonly alt: string;
  readonly width: number;
  readonly height: number;
}

/** One Guest Cycle as the operator view shows it, in the list and in the detail. */
export interface OperatorGuestCycle {
  readonly id: string;
  /** The time it began, formatted for display by the screen, e.g. "8:42 PM". */
  readonly time: string;
  readonly outcome: GuestCycleOutcome;
  /** The Template's name, from the brain; absent for a Guest Cycle with no Photo Strip, which records no Template. */
  readonly templateName?: string;
  /** The Look's name, from the brain; absent for a Guest Cycle with no Photo Strip. */
  readonly lookName?: string;
  /** The Photo Strip; only a completed Guest Cycle has one. */
  readonly strip: GalleryImage | null;
  /** The kept Captures, in arrangement order. */
  readonly captures: readonly GalleryImage[];
  /** The Guest Cycle's files can't be found; it shows "These photographs can't be found." in place of its images. */
  readonly unavailable?: boolean;
}

/** What the operator reads where a Guest Cycle's files are gone. */
export const unavailableCopy = "These photographs can't be found.";

/** The lines read out while a page is on its way: the first page, and the pages after it. */
export const loadingFirstCopy = "Loading photographs.";
export const loadingMoreCopy = "Loading more photographs.";

/** The Template and Look as one caption line ("Signature Strip · Sepia"), or null when neither is recorded. */
export function templateAndLook(cycle: Pick<OperatorGuestCycle, "templateName" | "lookName">): string | null {
  const names = [cycle.templateName, cycle.lookName].filter((name): name is string => name !== undefined);
  return names.length === 0 ? null : names.join(" · ");
}

/** One completed Photo Strip as the guest view shows it: nothing but the image. */
export interface GuestStrip extends GalleryImage {
  /** Opaque, for `onOpen`; never shown. */
  readonly id: string;
}
