import type { GuestWelcomeMessage } from "../protocol/generated.ts";

/**
 * Hand-written Guest Screen fixtures for the Filters, Keepsake, and Operator Assistance views and the cycle chrome (M8 UI-2), so the UI
 * can be built before the brain produces the same states. Names start with `guest-ui-` and never repeat a brain-generated name
 * (`guest-cycle-*`, `guest-assistance-*`); wave C replaces any that the brain's fixtures cover. Owner: M8 UI-2 only; `fixtureLane.ts`
 * already merges this table.
 */
export const guestFinishFixtures: Readonly<Record<string, GuestWelcomeMessage>> = {};