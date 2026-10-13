import { useSyncExternalStore } from "react";

const query = "(prefers-reduced-motion: reduce)";

function mediaQuery(): MediaQueryList | undefined {
  // jsdom and some hosts have no matchMedia; motion is then allowed, and CSS still applies the media query.
  if (!("matchMedia" in window) || typeof window.matchMedia !== "function") {
    return undefined;
  }
  return window.matchMedia(query);
}

function subscribe(onChange: () => void): () => void {
  const list = mediaQuery();
  list?.addEventListener("change", onChange);
  return () => {
    list?.removeEventListener("change", onChange);
  };
}

function snapshot(): boolean {
  return mediaQuery()?.matches ?? false;
}

/** Whether the user prefers reduced motion, kept current as the preference changes. */
export function usePrefersReducedMotion(): boolean {
  return useSyncExternalStore(subscribe, snapshot, () => false);
}
