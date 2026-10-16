import { useEffect, useRef } from "react";
import type { GuestCycleView, PhotographsView } from "../protocol/generated.ts";
import { ArrangeTab } from "./ArrangeTab.tsx";
import { CameraTab } from "./CameraTab.tsx";
import { arrangeCopy } from "./captureCopy.ts";
import styles from "./PhotographsStep.module.css";

/**
 * The Photographs step (M8 UI-1): the Camera tab or the Arrange tab, as the brain says. The step outlives the swap
 * between them, so it owns the announcement of a Retake's new photograph: the brain commits the candidate while the
 * Camera tab shows and then flips to a fresh Arrange tab. The candidate's image identifies it, so each candidate is
 * announced exactly once.
 */
export function PhotographsStep({ cycle, view }: { readonly cycle: GuestCycleView; readonly view: PhotographsView }) {
  const liveRef = useRef<HTMLSpanElement>(null);
  const candidate = view.retakeCandidate?.imageUrl ?? null;
  const announced = useRef(candidate);

  useEffect(() => {
    if (candidate === announced.current) {
      return;
    }
    announced.current = candidate;
    if (liveRef.current !== null) {
      liveRef.current.textContent = candidate === null ? "" : arrangeCopy.announceCandidate;
    }
  }, [candidate]);

  return (
    <>
      {view.tab === "camera" ? <CameraTab cycle={cycle} view={view} /> : <ArrangeTab cycle={cycle} view={view} />}
      <span ref={liveRef} className={styles.announcer} aria-live="polite" />
    </>
  );
}