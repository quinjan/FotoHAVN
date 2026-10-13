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
  /** The Template's name, from the brain. */
  readonly templateName: string;
  /** The Look's name, from the brain; absent when the Guest Cycle ended before a Look applied. */
  readonly lookName?: string;
  /** The Photo Strip; only a completed Guest Cycle has one. */
  readonly strip: GalleryImage | null;
  /** The kept Captures, in arrangement order. */
  readonly captures: readonly GalleryImage[];
}

/** One completed Photo Strip as the guest view shows it: nothing but the image. */
export interface GuestStrip extends GalleryImage {
  /** Opaque, for `onOpen`; never shown. */
  readonly id: string;
}
