import { useEffect, useRef, type RefObject } from "react";

/**
 * Calls `onNearEnd` when `sentinel` comes into view (inside `root`, or the page when `root` is null), at most once for each
 * `pageKey`: a page that has been asked for is not asked for again until a new page (a new key) arrives. Nothing is observed
 * while `enabled` is false, so a load in flight, a failed load, or the last page asks for nothing.
 */
export function useNearEnd({
  enabled,
  pageKey,
  sentinel,
  root,
  margin,
  onNearEnd,
}: {
  readonly enabled: boolean;
  readonly pageKey: string;
  readonly sentinel: RefObject<Element | null>;
  readonly root: RefObject<Element | null> | null;
  /** How far ahead of the edge the sentinel counts as near, as a CSS margin ("0px 100% 0px 0px"). */
  readonly margin: string;
  readonly onNearEnd: () => void;
}): void {
  const asked = useRef<string | null>(null);
  const latest = useRef(onNearEnd);
  useEffect(() => {
    latest.current = onNearEnd;
  });

  useEffect(() => {
    const end = sentinel.current;
    if (!enabled || end === null || typeof IntersectionObserver === "undefined") {
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (asked.current !== pageKey && entries.some((entry) => entry.isIntersecting)) {
          asked.current = pageKey;
          latest.current();
        }
      },
      { root: root === null ? null : root.current, rootMargin: margin },
    );
    observer.observe(end);
    return () => {
      observer.disconnect();
    };
  }, [enabled, pageKey, sentinel, root, margin]);
}
