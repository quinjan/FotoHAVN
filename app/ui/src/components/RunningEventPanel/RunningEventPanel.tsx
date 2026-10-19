import { WarningCircleIcon } from "@phosphor-icons/react";
import { useId } from "react";
import { Eyebrow } from "../Eyebrow/index.ts";
import { PrimaryAction } from "../PrimaryAction/index.ts";
import { SecondaryAction } from "../SecondaryAction/index.ts";
import { cx } from "../shared/cx.ts";
import { TextLink } from "../TextLink/index.ts";
import styles from "./RunningEventPanel.module.css";

/** Where the guest is in the running Event, as the brain reports it. */
export type RunningEventStep =
  | "start"
  | "template"
  | "photographs"
  | "filters"
  | "keepsake"
  | "guest-gallery"
  | "operator-assistance";

export type GuestDisplayState = "connected" | "disconnected";

/** `checking` is the neutral state while the camera has not yet delivered a fresh frame. */
export type RunningCameraState = "ready" | "checking" | "not-available";

const stepText: Record<RunningEventStep, string> = {
  start: "Start",
  template: "Template",
  photographs: "Photographs",
  filters: "Filters",
  keepsake: "Keepsake",
  "guest-gallery": "Guest gallery",
  "operator-assistance": "Operator Assistance",
};

const displayText: Record<GuestDisplayState, string> = { connected: "Connected", disconnected: "Disconnected" };

const cameraText: Record<RunningCameraState, string> = { ready: "Ready", checking: "Checking", "not-available": "Not available" };

/** The alert's two sentences: the reason, then the fix, which ends with `RETRY`. */
export interface RunningEventAssistance {
  readonly reason: string;
  readonly fix: string;
}

export interface RunningEventPanelProps {
  /** The running Event's name, in the Subheading role. */
  readonly eventName: string;
  readonly guestDisplay: GuestDisplayState;
  /** Shown whether or not a Guest Cycle is running. */
  readonly camera: RunningCameraState;
  readonly step: RunningEventStep;
  readonly completedGuestCycles: number;
  /** The time since the Event started, formatted by the screen (it updates live), e.g. "1 h 12 min". */
  readonly timeSinceStart: string;
  /**
   * What went wrong and what to do about it, in text, while the Active Event is in Operator Assistance; undefined
   * otherwise. While set, the panel becomes the Operator Screen's one dark band with the alert in large type, and
   * offers `RETRY` as the single primary action.
   */
  readonly assistance?: RunningEventAssistance;
  /** Opens the running Event's live operator gallery. */
  readonly onOpenGallery: () => void;
  /** Retries after Operator Assistance. Offered only while `assistance` is set. */
  readonly onRetry: () => void;
  /** Opens the End Event confirmation. */
  readonly onEndEvent: () => void;
  /** The Event name's heading level under the Event list's heading. Default 2. */
  readonly headingLevel?: 2 | 3;
  readonly className?: string;
}

/**
 * The running-Event panel (DESIGN.md, Operator Screen), pinned above the Event list and only there: a Warm Ivory band
 * with a 2 px Muted Brass left rule, the Event name, its live facts as Meta labels with text values, and `OPEN
 * GALLERY` and `END EVENT`. Normally its actions are text and secondary actions, so `NEW EVENT →` stays the view's
 * primary action. During Operator Assistance it turns into the view's one dark band (Dark Walnut, Off-white type), with
 * the reason and the fix in large type in a polite live region that is announced once, and `RETRY` as the single primary
 * action under them; the Event list then drops `NEW EVENT` to a secondary action.
 */
export function RunningEventPanel({
  eventName,
  guestDisplay,
  camera,
  step,
  completedGuestCycles,
  timeSinceStart,
  assistance,
  onOpenGallery,
  onRetry,
  onEndEvent,
  headingLevel = 2,
  className,
}: RunningEventPanelProps) {
  const headingId = useId();
  const Heading = `h${String(headingLevel)}` as "h2" | "h3";
  const inAssistance = assistance !== undefined;
  const facts: readonly (readonly [label: string, value: string, key: string])[] = [
    ["GUEST DISPLAY", displayText[guestDisplay], "display"],
    ["CAMERA", cameraText[camera], "camera"],
    ["CURRENT STEP", stepText[step], "step"],
    ["COMPLETED GUEST CYCLES", String(completedGuestCycles), "cycles"],
    ["TIME SINCE START", timeSinceStart, "time"],
  ];
  return (
    <div
      className={cx(styles.panel, className)}
      data-tone={inAssistance ? "dark" : "light"}
      data-assistance={inAssistance || undefined}
      role="group"
      aria-labelledby={headingId}
    >
      <div className={styles.summary}>
        <div className={styles.title}>
          <Eyebrow>RUNNING</Eyebrow>
          <Heading id={headingId} className={styles.name}>
            {eventName}
          </Heading>
        </div>
        {/* Always mounted, so the alert is announced (politely, once) when it fills. */}
        <div className={styles.alert} role="status" data-empty={!inAssistance || undefined}>
          {assistance !== undefined && (
            <>
              <span className={styles.alertIcon} aria-hidden="true">
                <WarningCircleIcon weight="light" size="1em" />
              </span>
              <p className={styles.alertText}>
                <span className={styles.reason}>{assistance.reason}</span> <span className={styles.fix}>{assistance.fix}</span>
              </p>
            </>
          )}
        </div>
        <dl className={styles.facts}>
          {facts.map(([label, value, key]) => (
            <div key={key} className={styles.fact} data-fact={key}>
              <dt className={styles.label}>{label}</dt>
              <dd className={styles.value}>{value}</dd>
            </div>
          ))}
        </dl>
      </div>
      <div className={styles.actions}>
        {inAssistance && <PrimaryAction onClick={onRetry}>RETRY</PrimaryAction>}
        <TextLink onClick={onOpenGallery}>OPEN GALLERY</TextLink>
        <SecondaryAction onClick={onEndEvent}>END EVENT</SecondaryAction>
      </div>
    </div>
  );
}
