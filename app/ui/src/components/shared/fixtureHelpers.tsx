import { useEffect, useRef, type ReactNode } from "react";

/**
 * Fixture-only helpers. Import them from `*.fixtures.tsx` files only, so field builds never include them.
 */

/** Focuses the first focusable element inside on mount, so a fixture can show the focus ring. */
export function FocusFirst({ children }: { readonly children: ReactNode }) {
  const ref = useRef<HTMLDivElement>(null);
  useEffect(() => {
    ref.current?.querySelector<HTMLElement>("button, a[href], input, select, textarea, [tabindex]")?.focus();
  }, []);
  return <div ref={ref}>{children}</div>;
}

/** Does nothing; the `onClick` for fixture actions. */
export function noop(): void {
  // Fixtures render states; they send no commands.
}
