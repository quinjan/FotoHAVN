import { useState, type SyntheticEvent } from "react";
import { PrintObject } from "../components/PrintObject/index.ts";
import { usePrefersReducedMotion } from "../components/shared/reducedMotion.ts";
import { stripPreviewAlt } from "./finishCopy.ts";
import styles from "./FiltersStripPreview.module.css";

interface Layer {
  readonly src: string;
  readonly width: number;
  readonly height: number;
}

/** Used until an image reports its size: the Signature Strip's proportion, which every Photo Strip but Panorama Four shares. */
const fallbackSize = { width: 600, height: 1800 } as const;

export interface FiltersStripPreviewProps {
  /** The composed strip's URL, or null before the first composition. A new URL crossfades over the one shown. */
  readonly src: string | null;
  /** The brain is composing: the strip shown stays, a little quieter, and never moves. */
  readonly composing: boolean;
}

/**
 * The strip preview on the Filters step: a print object that keeps its last good picture while a new one loads and
 * while a composition fails (Q25), then crossfades to the new picture at `standard` (240 ms; none under reduced motion).
 * The print takes its shape from the image itself (Filters carries no Template geometry): a landscape picture, Panorama
 * Four, is turned a quarter turn counterclockwise exactly as everywhere else on the Guest Screen.
 */
export function FiltersStripPreview({ src, composing }: FiltersStripPreviewProps) {
  const reducedMotion = usePrefersReducedMotion();
  const [layers, setLayers] = useState<readonly Layer[]>([]);
  const newest = layers.at(-1);
  const pending = src !== null && src !== newest?.src ? src : null;
  const visible = reducedMotion ? layers.slice(-1) : layers;

  function loaded(url: string, event: SyntheticEvent<HTMLImageElement>): void {
    const { naturalWidth, naturalHeight } = event.currentTarget;
    const size = naturalWidth > 0 && naturalHeight > 0 ? { width: naturalWidth, height: naturalHeight } : fallbackSize;
    setLayers((previous) => [...previous.slice(-1), { src: url, ...size }]);
  }

  return (
    <div className={styles.preview} data-composing={composing || undefined} data-empty={newest === undefined || undefined}>
      {visible.map((layer, index) => (
        <div
          key={layer.src}
          className={styles.layer}
          data-entering={index > 0 || undefined}
          onAnimationEnd={(event) => {
            if (event.target === event.currentTarget) {
              setLayers((previous) => previous.slice(-1));
            }
          }}
        >
          <PrintObject src={layer.src} alt={index === visible.length - 1 ? stripPreviewAlt : ""} width={layer.width} height={layer.height} rotated={layer.width > layer.height} />
        </div>
      ))}
      {pending !== null && (
        <img
          key={pending}
          className={styles.loader}
          src={pending}
          alt=""
          onLoad={(event) => {
            loaded(pending, event);
          }}
        />
      )}
    </div>
  );
}
