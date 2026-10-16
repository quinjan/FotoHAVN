import { useState } from "react";
import { PrintObject } from "../components/PrintObject/index.ts";
import { usePrefersReducedMotion } from "../components/shared/reducedMotion.ts";
import { stripPreviewAlt } from "./finishCopy.ts";
import styles from "./FiltersStripPreview.module.css";

/** One print in the stack: `src` null is the blank print that holds the shape before any picture; `ready` is set when the picture has loaded. */
interface Layer {
  readonly src: string | null;
  readonly ready: boolean;
}

const blank: Layer = { src: null, ready: true };

/** The layers from the newest ready one on: the picture shown and anything still loading above it. */
function settle(layers: readonly Layer[]): readonly Layer[] {
  const shown = layers.findLastIndex((layer) => layer.ready);
  return layers.slice(Math.max(shown, 0));
}

/** A new picture joins above the ready ones; one still loading is replaced by it, and one already shown on top needs no new layer. */
function withPicture(layers: readonly Layer[], src: string): readonly Layer[] {
  const ready = layers.filter((layer) => layer.ready).slice(-2);
  return ready.at(-1)?.src === src ? ready : [...ready, { src, ready: false }];
}

export interface FiltersStripPreviewProps {
  /** The composed strip's URL, or null before the first composition. A new URL crossfades over the one shown. */
  readonly src: string | null;
  /** The brain is composing: the strip shown stays, a little quieter, and never moves. */
  readonly composing: boolean;
  /** The Template's paper size in native pixels, so the print's shape is reserved before any picture loads. */
  readonly width: number;
  readonly height: number;
  /** Whether the paper is wider than tall: Panorama Four is turned a quarter turn counterclockwise, as everywhere on the Guest Screen. */
  readonly landscape: boolean;
}

/**
 * The strip preview on the Filters step: a print object whose shape comes from the Template (the view carries its size), so
 * it holds its place from the first frame, blank until the first picture loads. It keeps its last good picture while a new one
 * loads and while a composition fails (Q25), then crossfades to the new picture at `standard` (240 ms; none under reduced motion).
 */
export function FiltersStripPreview({ src, composing, width, height, landscape }: FiltersStripPreviewProps) {
  const reducedMotion = usePrefersReducedMotion();
  const [layers, setLayers] = useState<readonly Layer[]>(() => (src === null ? [blank] : [blank, { src, ready: false }]));
  const [requested, setRequested] = useState(src);
  if (src !== requested) {
    setRequested(src);
    if (src !== null) {
      setLayers((previous) => withPicture(previous, src));
    }
  }
  const shown = layers.findLastIndex((layer) => layer.ready && layer.src !== null);

  function loaded(url: string | null): void {
    setLayers((previous) => {
      const next = previous.map((layer) => (layer.src === url ? { ...layer, ready: true } : layer));
      return reducedMotion ? settle(next) : next;
    });
  }

  return (
    <div className={styles.preview} data-composing={composing || undefined} data-empty={shown < 0 || undefined}>
      {layers.map((layer, index) => (
        <div
          key={layer.src ?? "blank"}
          className={styles.layer}
          data-waiting={!layer.ready || undefined}
          data-entering={(layer.ready && index > 0) || undefined}
          onLoad={
            layer.ready
              ? undefined
              : () => {
                  loaded(layer.src);
                }
          }
          onAnimationEnd={(event) => {
            if (event.target === event.currentTarget) {
              setLayers(settle);
            }
          }}
        >
          <PrintObject src={layer.src} alt={index === shown ? stripPreviewAlt : ""} width={width} height={height} rotated={landscape} />
        </div>
      ))}
    </div>
  );
}
