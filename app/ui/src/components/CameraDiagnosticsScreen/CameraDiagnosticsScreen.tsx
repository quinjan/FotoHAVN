import type { CameraDiagnosticsView } from "../../protocol/generated.ts";
import { Eyebrow } from "../Eyebrow/index.ts";
import { InlineStatus } from "../InlineStatus/index.ts";
import { LivePreview, silentFrameSource, type FrameSource } from "../LivePreview/index.ts";
import { OptionList } from "../OptionList/index.ts";
import { PrimaryAction } from "../PrimaryAction/index.ts";
import { StatementHeading } from "../StatementHeading/index.ts";
import { cx } from "../shared/cx.ts";
import { Switch } from "../Switch/index.ts";
import {
  cameraStatusIsAlert,
  cameraStatusText,
  chooseCameraCopy,
  formatCameraFormat,
  formatCaptureFacts,
  formatFps,
  formatMeasuredRate,
  formatPictureAge,
  noCamerasCopy,
  noCamerasHelp,
  noPictureCopy,
} from "./cameraText.ts";
import styles from "./CameraDiagnosticsScreen.module.css";

/** What the page itself measured about the frames it received (see `useFrameStats`). */
export interface MeasuredFrames {
  readonly fps: number | null;
  readonly ageMs: number | null;
}

/** What the host reports about the same frames, from `test.getDiagnostics` (E2E builds only). */
export interface HostFrameReport {
  readonly deliveredFps: number | null;
  readonly droppedFrames: number | null;
  readonly frameAgeMs: number | null;
}

/** The operator's controls; they send E2E hooks, so field builds leave them out and the screen is read-only. */
export interface CameraDiagnosticsControls {
  readonly onSelect: (deviceId: string) => void;
  readonly onMirrorChange: (mirrored: boolean) => void;
  readonly onCapture: () => void;
  /** A capture is in flight: `CAPTURE` is unavailable until it ends. */
  readonly capturing?: boolean;
  /** The outcome line when a hook didn't go through; null shows nothing. */
  readonly notice?: string | null;
}

export interface CameraDiagnosticsScreenProps {
  readonly view: CameraDiagnosticsView;
  /** Where the live preview's frames come from; drawn only while the camera is `ready`. */
  readonly source: FrameSource;
  readonly measured: MeasuredFrames;
  readonly host?: HostFrameReport | null;
  readonly controls?: CameraDiagnosticsControls | undefined;
  readonly className?: string;
}

/** A camera that is not delivering: the preview shows its waiting line instead of a picture. */
function waitingMessage(view: CameraDiagnosticsView): string | undefined {
  if (view.cameras.length === 0) {
    return noPictureCopy;
  }
  if (view.selectedDeviceId === null) {
    return chooseCameraCopy;
  }
  return view.state === "notAvailable" || view.state === "inUseByAnotherApp" ? noPictureCopy : undefined;
}

function hostLine(host: HostFrameReport): string {
  const parts = [
    host.deliveredFps === null ? null : `Delivered ${formatFps(host.deliveredFps)}`,
    host.droppedFrames === null ? null : `Dropped ${String(host.droppedFrames)}`,
    host.frameAgeMs === null ? null : `Camera frame age ${String(Math.round(host.frameAgeMs))} ms`,
  ].filter((part) => part !== null);
  return parts.length === 0 ? "—" : parts.join(" · ");
}

/**
 * The operator's camera diagnostics view (DESIGN.md, Operator Screen): an eyebrow and statement heading, the live
 * preview beside the camera list, the camera's state as one status line (the state, and for a camera that isn't
 * available the reason in words), the format, the page's measured frame rate and frame age, and the last capture's
 * facts. The preview draws only while the camera is `ready`, mirrored by CSS when the view says so.
 *
 * Everything on it is read-only unless `controls` is given; `controls` are for E2E builds, whose hooks pick the camera,
 * flip the mirror, and capture. `CAPTURE` is the view's one primary action.
 */
export function CameraDiagnosticsScreen({ view, source, measured, host = null, controls, className }: CameraDiagnosticsScreenProps) {
  const ready = view.state === "ready";
  const capture = view.lastCapture;
  const waiting = waitingMessage(view);
  return (
    <div className={cx(styles.screen, className)} data-camera-state={view.state} data-camera-reason={view.reason ?? undefined}>
      <header className={styles.header}>
        <Eyebrow>CAMERA DIAGNOSTICS</Eyebrow>
        <StatementHeading level={1} size="heading" lead="Check the" turn="camera." />
      </header>
      <div className={styles.body}>
        <div className={styles.stage}>
          <div className={styles.preview}>
            <LivePreview
              source={ready ? source : silentFrameSource}
              mirrored={view.mirrored}
              {...(waiting === undefined ? {} : { waitingMessage: waiting })}
            />
          </div>
          {controls !== undefined && (
            <div className={styles.controls}>
              <Switch label="Mirror" checked={view.mirrored} onCheckedChange={controls.onMirrorChange} />
              <PrimaryAction disabled={!ready || controls.capturing === true} onClick={controls.onCapture}>
                CAPTURE
              </PrimaryAction>
              <InlineStatus tone="alert">{controls.notice ?? ""}</InlineStatus>
            </div>
          )}
        </div>
        <div className={styles.side}>
          {view.cameras.length === 0 ? (
            <div className={styles.empty}>
              <p className={styles.emptyTitle}>{noCamerasCopy}</p>
              <p className={styles.emptyHelp}>{noCamerasHelp}</p>
            </div>
          ) : (
            <OptionList
              variant="radio"
              label="Camera"
              value={view.selectedDeviceId}
              onChange={controls?.onSelect}
              items={view.cameras.map((camera) => ({
                value: camera.deviceId,
                title: camera.name,
                disabled: controls === undefined,
              }))}
            />
          )}
          <InlineStatus tone={cameraStatusIsAlert(view.state) ? "alert" : "neutral"}>
            {view.cameras.length === 0 ? "" : cameraStatusText(view)}
          </InlineStatus>
          <dl className={styles.facts}>
            <div className={styles.fact}>
              <dt className={styles.label}>Format</dt>
              <dd className={styles.value}>{formatCameraFormat(view.format)}</dd>
            </div>
            <div className={styles.fact}>
              <dt className={styles.label}>Frame rate</dt>
              <dd className={styles.value} data-camera-measure="fps">
                {formatMeasuredRate(measured.fps)}
              </dd>
            </div>
            <div className={styles.fact}>
              <dt className={styles.label}>Last picture</dt>
              <dd className={styles.value} data-camera-measure="age">
                {formatPictureAge(measured.ageMs)}
              </dd>
            </div>
            {host !== null && (
              <div className={styles.fact}>
                <dt className={styles.label}>Booth report</dt>
                <dd className={styles.value} data-camera-measure="host">
                  {hostLine(host)}
                </dd>
              </div>
            )}
            <div className={styles.fact}>
              <dt className={styles.label}>Last capture</dt>
              <dd className={styles.value} data-camera-measure="capture">
                {capture === null ? (
                  "None yet"
                ) : (
                  <>
                    <span className={styles.file}>{capture.fileName}</span>
                    <span className={styles.detail}>{formatCaptureFacts(capture)}</span>
                  </>
                )}
              </dd>
            </div>
          </dl>
        </div>
      </div>
    </div>
  );
}
