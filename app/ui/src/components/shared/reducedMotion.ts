import { useSyncExternalStore } from "react";

const reducedMotionQuery = "(prefers-reduced-motion: reduce)";

function mediaList(): MediaQueryList | undefined {
  // Test DOMs (jsdom) don't implement matchMedia; treat its absence as no preference.
  const media: Partial<Pick<Window, "matchMedia">> = window;
  return media.matchMedia?.(reducedMotionQuery);
}

function subscribe(onChange: () => void): () => void {
  const list = mediaList();
  list?.addEventListener("change", onChange);
  return () => {
    list?.removeEventListener("change", onChange);
  };
}

function prefersReducedMotion(): boolean {
  return mediaList()?.matches ?? false;
}

/**
 * Whether the user prefers reduced motion (brand motion.md), kept live. Use it only where a component's behavior, not
 * just its CSS, changes: a progress ring that isn't drawn, a flash that becomes a static change. CSS transforms still
 * belong inside `@media (prefers-reduced-motion: no-preference)`.
 */
export function usePrefersReducedMotion(): boolean {
  return useSyncExternalStore(subscribe, prefersReducedMotion, () => false);
}
