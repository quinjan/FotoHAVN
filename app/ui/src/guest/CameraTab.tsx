import { CameraIcon } from "@phosphor-icons/react";
import { useId, type CSSProperties } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { CaptureFlash } from "../components/CaptureFlash/index.ts";
import { CaptureProgress } from "../components/CaptureProgress/index.ts";
import { CountdownNumeral } from "../components/CountdownNumeral/index.ts";
import { LivePreview, silentFrameSource } from "../components/LivePreview/index.ts";
import { cx } from "../components/shared/cx.ts";
import { TextLink } from "../components/TextLink/index.ts";
import type { GuestCycleView, PhotographsView } from "../protocol/generated.ts";
import { cameraCopy } from "./captureCopy.ts";
import styles from "./CameraTab.module.css";
import { usePhotographsCommands } from "./PhotographsCommands.ts";

/**
 * The Photographs step's Camera tab (app/DESIGN.md, Photographs: Camera): one Dark Walnut band under the top band, all of
 * it live preview. The capture area (the next window's crop) takes the band's full height, centred, with the picture
 * around it dimmed; the countdown numeral sits at its top centre and the photograph count at its bottom centre, one flash
 * per Capture. While the sequence can start, the whole preview is one button, "Tap to start taking photographs", with a tap-to-start
 * prompt over the centre; once it runs, tapping does nothing and the prompt goes (there is no stop). The links out
 * (`SEE MY PHOTOGRAPHS`, and `KEEP ORIGINAL` during a Retake) sit in the dimmed band to the right of the capture area.
 * The brain owns the sequence: every number, flash, and announcement is drawn from the presentation alone.
 */
export function CameraTab({ view }: { readonly cycle: GuestCycleView; readonly view: PhotographsView }) {
  const client = useBrainClient();
  const commands = usePhotographsCommands();
  const waitingId = useId();
  const { sequence } = view;
  const filled = view.windows.filter((window) => window.captureId !== null).length;
  const state = sequence.running ? "running" : view.canTakePhotographs ? "ready" : "waiting";
  const retaking = view.selection.mode === "retaking";

  return (
    <div
      className={styles.tab}
      data-tone="dark"
      data-photographs-tab="camera"
      data-sequence-running={sequence.running}
      data-sequence-pausing={sequence.pausing}
      style={{ "--capture-ratio": String(view.cropAspect) } as CSSProperties}
    >
      <h1 className={styles.visuallyHidden}>{`${cameraCopy.lead} ${cameraCopy.turn}`}</h1>
      <LivePreview
        className={cx(styles.preview)}
        framing="capture"
        source={client.frames ?? silentFrameSource}
        guideRatio={view.cropAspect}
        mirrored={view.mirrored}
        label={cameraCopy.previewLabel}
      >
        <CountdownNumeral value={sequence.countdownRemaining ?? 0} cue={cameraCopy.lookAtCamera} />
        <CaptureProgress className={cx(styles.count)} taken={filled} total={view.windows.length} />
      </LivePreview>
      <CaptureFlash flashKey={sequence.flashSequence} />
      <button
        type="button"
        className={styles.start}
        data-state={state}
        aria-label={cameraCopy.start}
        aria-disabled={state !== "ready"}
        aria-describedby={state === "waiting" ? waitingId : undefined}
        onClick={() => {
          if (state === "ready") {
            commands.takePhotographs();
          }
        }}
      >
        {state !== "running" && (
          <span className={styles.prompt}>
            <span className={styles.glassDisc}>
              <CameraIcon weight="light" size="1em" aria-hidden="true" />
            </span>
            {state === "ready" ? (
              <span className={styles.tap}>{cameraCopy.tapToStart}</span>
            ) : (
              <span id={waitingId} className={styles.waiting}>
                {cameraCopy.waiting}
              </span>
            )}
          </span>
        )}
      </button>
      {(view.canReview || retaking) && (
        <div className={styles.actions}>
          {view.canReview && (
            <TextLink arrow="right" onClick={commands.showArrangeTab}>
              {cameraCopy.review}
            </TextLink>
          )}
          {retaking && <TextLink onClick={commands.keepOriginal}>{cameraCopy.keepOriginal}</TextLink>}
        </div>
      )}
    </div>
  );
}