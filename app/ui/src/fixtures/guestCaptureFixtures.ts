import type { GuestWelcomeMessage } from "../protocol/generated.ts";

/**
 * Hand-written Guest Screen fixtures for the Template and Photographs steps (M8 UI-1), so the UI can be built before the brain
 * produces the same states. Names start with `guest-ui-` and never repeat a brain-generated name (`guest-cycle-*`); wave C replaces
 * any that the brain's fixtures cover. Owner: M8 UI-1 only; `fixtureLane.ts` already merges this table.
 */
export const guestCaptureFixtures: Readonly<Record<string, GuestWelcomeMessage>> = {};