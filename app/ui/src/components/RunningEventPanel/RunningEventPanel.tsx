import { useId } from "react";
import { Eyebrow } from "../Eyebrow/index.ts";
import { InlineStatus } from "../InlineStatus/index.ts";
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

export type RunningCameraState = "ready" | "not-available";

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

const cameraText: Record<RunningCameraState, string> = { ready: "Ready", "not-available": "Not available" };

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
   * The reason, in text, while the Active Event is in Operator Assistance; undefined otherwise. While set, the panel
   * shows the alert line and `RETRY` as the Operator Screen's one primary action.
   */
  readonly assistanceReason?: string;
  /** Opens the running Event's live operator gallery. */
  readonly onOpenGallery: () => void;
  /** Retries after Operator Assistance. Offered only while `assistanceReason` is set. */
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
 * primary action. During Operator Assistance it adds an alert line with the reason in a polite live region and offers
 * `RETRY` as the single primary action; the Event list then drops `NEW EVENT` to a secondary action.
 */
export function RunningEventPanel({
  eventName,
  guestDisplay,
  camera,
  step,
  completedGuestCycles,
  timeSinceStart,
  assistanceReason,
  onOpenGallery,
  onRetry,
  onEndEvent,
  headingLevel = 2,
  className,
}: RunningEventPanelProps) {
  const headingId = useId();
  const Heading = `h${String(headingLevel)}` as "h2" | "h3";
  const inAssistance = assistanceReason !== undefined;
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
      data-tone="light"
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
        <InlineStatus tone="alert">{assistanceReason}</InlineStatus>
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
