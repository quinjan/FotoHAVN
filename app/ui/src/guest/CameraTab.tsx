import { useBrainClient } from "../brain/BrainClientContext.ts";
import { CaptureFlash } from "../components/CaptureFlash/index.ts";
import { CaptureProgress } from "../components/CaptureProgress/index.ts";
import { CountdownNumeral } from "../components/CountdownNumeral/index.ts";
import { LivePreview, silentFrameSource } from "../components/LivePreview/index.ts";
import { Shutter } from "../components/Shutter/index.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import { TextLink } from "../components/TextLink/index.ts";
import type { GuestCycleView, PhotographsView } from "../protocol/generated.ts";
import { cameraCopy } from "./captureCopy.ts";
import styles from "./CameraTab.module.css";
import { usePhotographsCommands } from "./PhotographsCommands.ts";

/**
 * The Photographs step's Camera tab (app/DESIGN.md, Photographs: Camera): the live preview dominant on Dark Walnut with
 * the crop guide for the next window, the countdown numeral over it, one flash per Capture, capture progress, and the
 * circular shutter. The brain owns the sequence: the shutter only starts or stops it, and every number, flash, and
 * announcement is drawn from the presentation alone.
 */
export function CameraTab({ view }: { readonly cycle: GuestCycleView; readonly view: PhotographsView }) {
  const client = useBrainClient();
  const commands = usePhotographsCommands();
  const { sequence } = view;
  const filled = view.windows.filter((window) => window.captureId !== null).length;
  const shutterAvailable = sequence.running || view.canTakePhotographs;

  return (
    <div
      className={styles.tab}
      data-photographs-tab="camera"
      data-sequence-running={sequence.running}
      data-sequence-pausing={sequence.pausing}
    >
      <div className={styles.panel} data-tone="dark">
        <div className={styles.controls}>
          <StatementHeading
            level={1}
            size="heading-sm"
            lead={cameraCopy.lead}
            turn={cameraCopy.turn}
            supporting={cameraCopy.supporting}
          />
          <CaptureProgress taken={filled} total={view.windows.length} />
          <div className={styles.shutter}>
            <Shutter
              state={sequence.running ? "recording" : "idle"}
              disabled={!shutterAvailable}
              onClick={sequence.running ? commands.stopCapture : commands.takePhotographs}
            />
            {!shutterAvailable && <p className={styles.waiting}>{cameraCopy.waiting}</p>}
          </div>
          {view.canReview && (
            <TextLink arrow="right" onClick={commands.showArrangeTab}>
              {cameraCopy.review}
            </TextLink>
          )}
          {view.selection.mode === "retaking" && (
            <TextLink onClick={commands.keepOriginal}>{cameraCopy.keepOriginal}</TextLink>
          )}
        </div>
        <div className={styles.stage}>
          <LivePreview
            source={client.frames ?? silentFrameSource}
            guideRatio={view.cropAspect}
            mirrored={view.mirrored}
            label={cameraCopy.previewLabel}
          >
            <CountdownNumeral value={sequence.countdownRemaining ?? 0} />
          </LivePreview>
          <CaptureFlash flashKey={sequence.flashSequence} />
        </div>
      </div>
    </div>
  );
}