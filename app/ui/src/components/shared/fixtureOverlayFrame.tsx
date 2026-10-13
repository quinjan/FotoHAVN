import type { ReactNode } from "react";
import styles from "./fixtureOverlayFrame.module.css";

/**
 * Fixture-only. Contains a fixed or absolute overlay (a dialog's scrim, a capture flash) inside the fixture's own box,
 * so it covers the viewport in `?gallery=<id>` (with `layout: "fill"`) and only its preview in the gallery index.
 * `stage` paints the frame Dark Walnut, like the live preview the camera overlays sit on.
 */
export function OverlayFrame({ children, stage = false }: { readonly children: ReactNode; readonly stage?: boolean }) {
  return (
    <div className={styles.frame} data-tone={stage ? "dark" : undefined}>
      {children}
    </div>
  );
}
