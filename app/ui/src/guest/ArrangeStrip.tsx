import { useRef, useState, type CSSProperties, type KeyboardEvent, type PointerEvent } from "react";
import { cx } from "../components/shared/cx.ts";
import { arrowStep } from "../components/shared/roving.ts";
import type { PhotographSelectionMode, PhotographsTemplateView, PhotographsWindowView } from "../protocol/generated.ts";
import { hasDragged, windowAtPoint } from "./ArrangeDrag.ts";
import styles from "./ArrangeStrip.module.css";
import { arrangeCopy } from "./captureCopy.ts";
import { templateImageSource } from "./TemplateArt.ts";

export interface ArrangeStripProps {
  readonly template: PhotographsTemplateView;
  /** The arrangement revision shown now; a drag sends the one it began at. */
  readonly revision: number;
  readonly windows: readonly PhotographsWindowView[];
  /** The brain's selection: a Move turns every window into a destination. */
  readonly mode: PhotographSelectionMode;
  readonly selected: number | null;
  /** False while a sequence runs or a Retake candidate waits: the strip only shows. */
  readonly interactive: boolean;
  readonly previewUrl: ((key: string) => string) | undefined;
  readonly onSelect: (index: number) => void;
  readonly onMoveTo: (index: number) => void;
  readonly onDragPlace: (sourceIndex: number, captureId: string, revision: number, targetIndex: number) => void;
}

interface Press {
  readonly index: number;
  readonly captureId: string;
  readonly revision: number;
  readonly pointerId: number;
  readonly x: number;
  readonly y: number;
  dragging: boolean;
}

/**
 * The Photo Strip being arranged (app/DESIGN.md, Photographs: Arrange): the Template's blank preview as the background
 * of a print at its native proportion, with each photograph cover-cropped into its window. Panorama Four turns a
 * quarter counterclockwise, as every guest preview does. A tap selects a photograph (or chooses a destination while a
 * Move waits), arrow keys step between windows, and a press that travels far enough drags the photograph onto another
 * window. Nothing here needs a drag: Move and its destination choice do the same job with taps or keys.
 */
export function ArrangeStrip({ template, revision, windows, mode, selected, interactive, previewUrl, onSelect, onMoveTo, onDragPlace }: ArrangeStripProps) {
  const cells = useRef<(HTMLElement | null)[]>([]);
  const press = useRef<Press | null>(null);
  const suppressClick = useRef(false);
  const [drag, setDrag] = useState<{ readonly source: number; readonly over: number | null } | null>(null);
  const moving = mode === "moving";
  const total = template.windows.length;
  const canDrag = interactive && !moving && mode !== "retaking";

  function overIndex(x: number, y: number): number | null {
    return windowAtPoint(
      cells.current.map((cell) => cell?.getBoundingClientRect() ?? null),
      x,
      y,
    );
  }

  function pointerDown(event: PointerEvent<HTMLButtonElement>, index: number, captureId: string): void {
    if (!canDrag || (event.pointerType === "mouse" && event.button !== 0)) {
      return;
    }
    press.current = { index, captureId, revision, pointerId: event.pointerId, x: event.clientX, y: event.clientY, dragging: false };
    event.currentTarget.setPointerCapture(event.pointerId);
  }

  function pointerMove(event: PointerEvent<HTMLButtonElement>): void {
    const current = press.current;
    if (current?.pointerId !== event.pointerId) {
      return;
    }
    if (!current.dragging && hasDragged(current.x, current.y, event.clientX, event.clientY)) {
      current.dragging = true;
    }
    if (current.dragging) {
      const over = overIndex(event.clientX, event.clientY);
      setDrag((before) => (before?.source === current.index && before.over === over ? before : { source: current.index, over }));
    }
  }

  function pointerUp(event: PointerEvent<HTMLButtonElement>): void {
    const current = press.current;
    if (current?.pointerId !== event.pointerId) {
      return;
    }
    press.current = null;
    setDrag(null);
    if (!current.dragging) {
      return;
    }
    // A drag can end in a click on the same button; the tap must not select on top of the drop.
    suppressClick.current = true;
    window.setTimeout(() => {
      suppressClick.current = false;
    }, 0);
    const target = overIndex(event.clientX, event.clientY);
    if (target !== null && target !== current.index) {
      onDragPlace(current.index, current.captureId, current.revision, target);
    }
  }

  function pointerCancel(): void {
    press.current = null;
    setDrag(null);
  }

  function click(index: number, occupied: boolean): void {
    if (suppressClick.current) {
      suppressClick.current = false;
      return;
    }
    if (moving) {
      onMoveTo(index);
    } else if (occupied) {
      onSelect(index);
    }
  }

  function keyDown(event: KeyboardEvent<HTMLElement>): void {
    const step = arrowStep(event.key);
    if (step === undefined) {
      return;
    }
    const buttons = cells.current.filter((cell): cell is HTMLButtonElement => cell instanceof HTMLButtonElement);
    const at = buttons.findIndex((button) => button === document.activeElement);
    const next = buttons[at + step];
    if (at >= 0 && next !== undefined) {
      event.preventDefault();
      next.focus();
    }
  }

  const rotated = template.landscape;
  const displayWidth = rotated ? template.height : template.width;
  const displayHeight = rotated ? template.width : template.height;
  const geometry = { "--print-ratio": String(displayWidth / displayHeight) } as CSSProperties;

  return (
    <div className={styles.fit} data-arrange-strip="">
      <div className={styles.print} style={geometry} data-rotated={rotated || undefined}>
        <div className={styles.surface}>
          <img className={styles.blank} src={templateImageSource(template, previewUrl)} alt="" draggable={false} />
          <div role="group" aria-label={arrangeCopy.stripLabel} className={styles.windows}>
            {template.windows.map((rect, index) => {
              const entry = windows[index];
              const imageUrl = entry?.imageUrl ?? null;
              const captureId = entry?.captureId ?? null;
              const position = index + 1;
              const place = {
                left: `${String((rect.x / template.width) * 100)}%`,
                top: `${String((rect.y / template.height) * 100)}%`,
                width: `${String((rect.width / template.width) * 100)}%`,
                height: `${String((rect.height / template.height) * 100)}%`,
              };
              const state = {
                "data-selected": (selected === index && !moving) || undefined,
                "data-moving": (selected === index && moving) || undefined,
                "data-destination": (moving && selected !== index) || undefined,
                "data-drag-source": drag?.source === index || undefined,
                "data-drag-over": (drag?.over === index && drag.source !== index) || undefined,
              };
              const badge = (
                <span className={styles.badge} aria-hidden="true">
                  {position}
                </span>
              );
              if (captureId === null || imageUrl === null) {
                if (!moving) {
                  return (
                    <span
                      key={index}
                      ref={(element) => {
                        cells.current[index] = element;
                      }}
                      className={styles.cell}
                      style={place}
                      data-empty=""
                      {...state}
                    >
                      {badge}
                      <span className={styles.hidden}>{arrangeCopy.emptyWindowName(position)}</span>
                    </span>
                  );
                }
              }
              const occupied = captureId !== null && imageUrl !== null;
              const name = moving
                ? selected === index
                  ? arrangeCopy.moveSource(position)
                  : arrangeCopy.moveTarget(position, occupied)
                : arrangeCopy.windowName(position, total);
              return (
                <button
                  key={index}
                  ref={(element) => {
                    cells.current[index] = element;
                  }}
                  type="button"
                  className={cx(styles.cell, styles.button)}
                  style={place}
                  aria-label={name}
                  aria-pressed={moving ? undefined : selected === index}
                  aria-disabled={!interactive}
                  data-empty={occupied ? undefined : ""}
                  data-draggable={(canDrag && occupied) || undefined}
                  {...state}
                  onKeyDown={keyDown}
                  onClick={() => {
                    if (interactive) {
                      click(index, occupied);
                    }
                  }}
                  onPointerDown={(event) => {
                    if (captureId !== null) {
                      pointerDown(event, index, captureId);
                    }
                  }}
                  onPointerMove={pointerMove}
                  onPointerUp={pointerUp}
                  onPointerCancel={pointerCancel}
                >
                  {occupied && <img className={styles.photo} src={imageUrl} alt="" draggable={false} />}
                  {badge}
                </button>
              );
            })}
          </div>
        </div>
      </div>
    </div>
  );
}
