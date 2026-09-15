/* Blob photographs stay local and must not pass through a remote image optimizer. */
/* eslint-disable @next/next/no-img-element */
import { useEffect, useRef, useState, type PointerEvent } from "react";
import { DotsSixVerticalIcon } from "@phosphor-icons/react";
import { getFrame, getLayout, getTemplate, previewLayout, previewArtworkStyle, type FrameId, type LayoutId } from "./presets";
import type { PositionMoveSource, Session } from "./session";
import styles from "./OnlinePhotobooth.module.css";

export function FrameMiniature({ layoutId, frameId }: { layoutId: LayoutId; frameId: FrameId }) {
  const layout = getLayout(layoutId), frame = getFrame(frameId);
  const bottom = Math.max(...layout.slots.map((slot) => slot.y + slot.height));
  return <svg aria-hidden="true" viewBox={`0 0 ${layout.width} ${layout.height}`} className={styles.miniature}>
    <rect width={layout.width} height={layout.height} fill={frame.paper} />
    {layout.slots.map((slot, index) => <g key={index}>
      {frameId === "gallery-v1" && <rect x={slot.x - 12} y={slot.y - 12} width={slot.width + 24} height={slot.height + 24} fill="#FBF8F2" />}
      <rect x={slot.x} y={slot.y} width={slot.width} height={slot.height} fill={frameId === "walnut-v1" ? "#67584B" : "#DDD2C2"} />
    </g>)}
    {(frameId === "archive-v1" || frameId === "walnut-v1") && <line x1="60" x2={layout.width - 60} y1={bottom + 30} y2={bottom + 30} stroke={frameId === "walnut-v1" ? "#B39A78" : "#D1C4B4"} strokeWidth="3" />}
    <text x={layout.width / 2} y={(bottom + layout.height) / 2 + 10} fill={frame.ink} textAnchor="middle" fontFamily="var(--font-display)" fontSize={layout.width > 900 ? 65 : 68}>FOTOHAVN</text>
  </svg>;
}

type Props = {
  state: Session; url?: string; ready: boolean; interactive?: boolean; reveal?: boolean;
  onSelect?: (index: number) => void;
  onReorder?: (source: PositionMoveSource, index: number) => void;
  onDragCancel?: (index: number) => void;
  onFiles?: (files: File[], index: number | null) => void;
  showSelection?: boolean;
};

type PointerGesture = {
  pointerId: number; source: PositionMoveSource; owner: HTMLButtonElement;
  x: number; y: number; active: boolean;
};

function releasePointer(gesture: PointerGesture) {
  if (gesture.owner.hasPointerCapture(gesture.pointerId)) gesture.owner.releasePointerCapture(gesture.pointerId);
}

export function PrintPreview({ state, url, ready, interactive = false, reveal = false, onSelect, onReorder, onDragCancel, onFiles, showSelection = true }: Props) {
  const layout = getLayout(state.layoutId);
  const displayLayout = previewLayout(layout);
  const printRef = useRef<HTMLDivElement>(null);
  const pointer = useRef<PointerGesture | null>(null);
  const suppressClick = useRef(false);
  const [fileHover, setFileHover] = useState(false);
  const [dragView, setDragView] = useState<{ source: PositionMoveSource; target: number | null } | null>(null);
  const enabled = interactive && ready && !state.operation;
  const dragging = enabled && !state.move && dragView?.source.revision === state.revision ? dragView : null;

  useEffect(() => {
    // Release the external pointer session when this workspace changes or loses
    // focus. Touch never captures a pointer, so native scrolling stays available.
    const abandon = () => {
      const gesture = pointer.current;
      if (!gesture) return;
      pointer.current = null;
      suppressClick.current = gesture.active;
      releasePointer(gesture);
      setDragView(null);
    };
    const visibility = () => { if (document.hidden) abandon(); };
    window.addEventListener("blur", abandon);
    document.addEventListener("visibilitychange", visibility);
    return () => {
      window.removeEventListener("blur", abandon);
      document.removeEventListener("visibilitychange", visibility);
      abandon();
    };
  }, [enabled, state.revision, state.move]);

  function destination(event: PointerEvent<HTMLButtonElement>) {
    const element = event.currentTarget.ownerDocument.elementFromPoint(event.clientX, event.clientY)?.closest<HTMLButtonElement>("button[data-photo-position]");
    if (!element || element.disabled || !printRef.current?.contains(element)) return null;
    const index = Number(element.dataset.photoPosition);
    return Number.isInteger(index) && index >= 0 && index < state.slots.length ? index : null;
  }

  function beginPointer(event: PointerEvent<HTMLButtonElement>, index: number) {
    suppressClick.current = false;
    if (!enabled || state.move || pointer.current || !state.slots[index] || event.pointerType === "touch" || !event.isPrimary || event.button !== 0) return;
    const gesture: PointerGesture = {
      pointerId: event.pointerId, source: { index, photoId: state.slots[index], revision: state.revision },
      owner: event.currentTarget, x: event.clientX, y: event.clientY, active: false,
    };
    try { event.currentTarget.setPointerCapture(event.pointerId); }
    catch { return; }
    pointer.current = gesture;
  }

  function movePointer(event: PointerEvent<HTMLButtonElement>) {
    const gesture = pointer.current;
    if (!gesture || gesture.pointerId !== event.pointerId) return;
    if (!gesture.active && Math.hypot(event.clientX - gesture.x, event.clientY - gesture.y) < 8) return;
    gesture.active = true;
    event.preventDefault();
    setDragView({ source: gesture.source, target: destination(event) });
  }

  function cancelPointer(pointerId?: number) {
    const gesture = pointer.current;
    if (!gesture || (pointerId !== undefined && pointerId !== gesture.pointerId)) return false;
    pointer.current = null;
    suppressClick.current = gesture.active;
    releasePointer(gesture);
    setDragView(null);
    if (gesture.active) onDragCancel?.(gesture.source.index);
    return true;
  }

  function finishPointer(event: PointerEvent<HTMLButtonElement>) {
    const gesture = pointer.current;
    if (!gesture || gesture.pointerId !== event.pointerId) return;
    const moved = gesture.active || Math.hypot(event.clientX - gesture.x, event.clientY - gesture.y) >= 8;
    const index = moved ? destination(event) : null;
    // Consume before releasing capture: lostpointercapture and click must never
    // commit a second swap, even when React has not rendered this drop yet.
    pointer.current = null;
    suppressClick.current = moved;
    releasePointer(gesture);
    setDragView(null);
    if (!moved) return;
    event.preventDefault();
    if (enabled && index !== null && index !== gesture.source.index) onReorder?.(gesture.source, index);
    else onDragCancel?.(gesture.source.index);
  }

  return <div className={`${styles.printStage} ${reveal ? styles.reveal : ""} ${fileHover && enabled ? styles.fileDropTarget : ""}`} data-print-stage
    onDragOver={(event) => {
      if (!interactive || !event.dataTransfer.types.includes("Files")) return;
      event.preventDefault(); event.dataTransfer.dropEffect = enabled && !state.move ? "copy" : "none";
      if (enabled && !state.move) setFileHover(true);
    }}
    onDragLeave={(event) => { if (!event.currentTarget.contains(event.relatedTarget as Node | null)) setFileHover(false); }}
    onDrop={(event) => {
      if (!interactive || !event.dataTransfer.types.includes("Files")) return;
      event.preventDefault(); setFileHover(false);
      if (!enabled || state.move) return;
      const position = (event.target as Element).closest<HTMLElement>("[data-photo-position]");
      const index = position && event.currentTarget.contains(position) ? Number(position.dataset.photoPosition) : null;
      onFiles?.(Array.from(event.dataTransfer.files), index);
    }}>
    <div ref={printRef} className={styles.print} data-layout={layout.id} style={{ aspectRatio: `${displayLayout.width} / ${displayLayout.height}` }} aria-busy={!ready}>
      <div className={styles.previewArtwork} style={previewArtworkStyle(layout)}>{url ? <img key={url} className={styles.printImage} src={url} width={layout.width} height={layout.height} alt={ready ? `${layout.name}${getTemplate(state.layoutId) ? " template" : ` with ${getFrame(state.frameId).name} frame`}${state.stage === "download" ? ", finished downloadable image" : ""}` : "Previous framed preview; preparing your new selection"} draggable={false} /> : <FrameMiniature layoutId={state.layoutId} frameId={state.frameId} />}</div>
      {state.slots.map((id, index) => {
        const rect = displayLayout.slots[index];
        const style = { left: `${rect.x / displayLayout.width * 100}%`, top: `${rect.y / displayLayout.height * 100}%`, width: `${rect.width / displayLayout.width * 100}%`, height: `${rect.height / displayLayout.height * 100}%` };
        if (!interactive) return !id && (!url || ready) && <span key={index} className={styles.emptyNumber} style={style}>{String(index + 1).padStart(2, "0")}</span>;
        return <button key={index} type="button" id={`photograph-position-${index}`} data-photo-position={index}
          className={`${styles.photoPosition} ${showSelection && state.selected === index ? styles.positionSelected : ""} ${state.move || dragging ? styles.moveTarget : ""} ${dragging?.source.index === index ? styles.positionDragging : ""} ${dragging?.target === index ? styles.positionDropTarget : ""}`}
          style={style} disabled={!!state.operation || !ready}
          data-draggable={!!id && !state.move}
          aria-label={`Photograph ${index + 1}, ${id ? "captured" : "empty"}${state.move ? `, ${id ? "swap here" : "move here"}` : showSelection && state.selected === index ? ", selected" : ""}`}
          aria-pressed={showSelection && !state.move && state.selected === index}
          onClick={(event) => {
            if (event.detail > 0 && suppressClick.current) { suppressClick.current = false; event.preventDefault(); event.stopPropagation(); return; }
            onSelect?.(index);
          }} draggable={false} onDragStart={(event) => event.preventDefault()}
          onPointerDown={(event) => beginPointer(event, index)} onPointerMove={movePointer} onPointerUp={finishPointer}
          onPointerCancel={(event) => cancelPointer(event.pointerId)} onLostPointerCapture={(event) => cancelPointer(event.pointerId)}
          onKeyDown={(event) => { if (event.key === "Escape" && cancelPointer()) { event.preventDefault(); event.stopPropagation(); } }}>
          <span className={id ? styles.positionNumber : styles.emptySlotNumber}>{String(index + 1).padStart(2, "0")}</span>
          {id && showSelection && state.selected === index && !state.operation && <DotsSixVerticalIcon className={styles.positionHandle} size={22} weight="bold" aria-hidden="true" />}
          {(state.move || dragging) && <span className={styles.moveHere}>{id ? "Swap here" : "Move here"}</span>}
        </button>;
      })}
    </div>
  </div>;
}
