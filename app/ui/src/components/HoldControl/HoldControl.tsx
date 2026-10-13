import { LockSimpleIcon } from "@phosphor-icons/react";
import {
  useEffect,
  useId,
  useRef,
  useState,
  type CSSProperties,
  type KeyboardEvent,
  type PointerEvent,
} from "react";
import { cx } from "../shared/cx.ts";
import { usePrefersReducedMotion } from "../shared/reducedMotion.ts";
import styles from "./HoldControl.module.css";

/** How long the operator holds, in milliseconds (app/DESIGN.md: a 3-second press-and-hold). */
export const holdDurationMs = 3000;

/** How often the progress ring and text update while held. */
const tickMs = 50;

export type HoldPhase = "idle" | "holding" | "complete";

function secondsText(seconds: number): string {
  return seconds === 1 ? "1 second" : `${String(seconds)} seconds`;
}

export interface HoldControlViewProps {
  readonly phase: HoldPhase;
  /** 0 to 1. */
  readonly progress: number;
  /** Whole seconds left while holding. */
  readonly secondsLeft: number;
  /** The polite announcement: once when a hold starts, once when it is released early. */
  readonly announcement?: string;
  readonly className?: string | undefined;
  readonly onKeyDown?: (event: KeyboardEvent<HTMLButtonElement>) => void;
  readonly onKeyUp?: (event: KeyboardEvent<HTMLButtonElement>) => void;
  readonly onBlur?: () => void;
  readonly onPointerDown?: (event: PointerEvent<HTMLButtonElement>) => void;
  readonly onPointerUp?: () => void;
  readonly onPointerCancel?: () => void;
  readonly onLostPointerCapture?: () => void;
}

/**
 * How the exit hold looks in each phase. HoldControl drives it; fixtures render it directly to show a hold in
 * progress. Under reduced motion the ring isn't drawn, and the text carries the progress alone.
 */
export function HoldControlView({
  phase,
  progress,
  secondsLeft,
  announcement = "",
  className,
  ...handlers
}: HoldControlViewProps) {
  const reducedMotion = usePrefersReducedMotion();
  const hintId = useId();
  const progressId = useId();
  const ring = { "--hold-progress": String(progress) } as CSSProperties;
  return (
    <div className={cx(styles.hold, className)} data-phase={phase}>
      <button
        type="button"
        className={styles.button}
        aria-label="Operator exit"
        aria-describedby={phase === "holding" ? `${hintId} ${progressId}` : hintId}
        onContextMenu={(event) => {
          event.preventDefault();
        }}
        {...handlers}
      >
        <svg className={styles.ring} viewBox="0 0 48 48" aria-hidden="true" data-motion={reducedMotion ? "reduced" : "full"}>
          <g transform="rotate(-90 24 24)">
            <circle className={styles.track} cx="24" cy="24" r="22" pathLength={1} />
            {!reducedMotion && <circle className={styles.arc} cx="24" cy="24" r="22" pathLength={1} style={ring} />}
          </g>
        </svg>
        <span className={styles.icon} aria-hidden="true">
          <LockSimpleIcon weight="light" size="1em" />
        </span>
      </button>
      <span id={progressId} className={styles.progress}>
        {phase === "holding" ? `Keep holding: ${secondsText(secondsLeft)}` : ""}
      </span>
      <span id={hintId} className={styles.visuallyHidden}>
        Press and hold for 3 seconds.
      </span>
      <span className={styles.visuallyHidden} role="status">
        {announcement}
      </span>
    </div>
  );
}

export interface HoldControlProps {
  /** The hold reached 3 seconds (`CompleteExitHold`). Called once per hold. */
  readonly onComplete: () => void;
  /** A hold began (`BeginExitHold`). */
  readonly onHoldStart?: () => void;
  /** The hold was released early; nothing happens. */
  readonly onHoldCancel?: () => void;
  readonly className?: string;
}

/**
 * The discreet exit hold in the Guest Screen's top-right corner (app/DESIGN.md, Exit hold): a 64 × 64 control named
 * "Operator exit" that must be pressed and held for 3 seconds, by pointer or touch, or with Enter or Space held on the
 * focused control. Releasing early cancels. It shows a progress ring and the same progress as text ("Keep holding:
 * 2 seconds"), and announces only when a hold starts or is released early. It stacks at `--app-z-exit-hold`; the
 * screen places it and hides it while the PIN dialog is open.
 */
export function HoldControl({ onComplete, onHoldStart, onHoldCancel, className }: HoldControlProps) {
  const [phase, setPhase] = useState<HoldPhase>("idle");
  const [elapsed, setElapsed] = useState(0);
  const [announcement, setAnnouncement] = useState("");
  const phaseRef = useRef<HoldPhase>("idle");
  const keyRef = useRef<string | null>(null);
  const timers = useRef<{ tick?: number; done?: number }>({});
  const callbacks = useRef({ onComplete, onHoldStart, onHoldCancel });

  useEffect(() => {
    callbacks.current = { onComplete, onHoldStart, onHoldCancel };
  });

  function clearTimers(): void {
    window.clearInterval(timers.current.tick);
    window.clearTimeout(timers.current.done);
    timers.current = {};
  }

  useEffect(() => clearTimers, []);

  function moveTo(next: HoldPhase): void {
    phaseRef.current = next;
    setPhase(next);
  }

  function begin(): void {
    if (phaseRef.current !== "idle") {
      return;
    }
    const startedAt = Date.now();
    moveTo("holding");
    setElapsed(0);
    setAnnouncement("Keep holding for 3 seconds.");
    timers.current.tick = window.setInterval(() => {
      setElapsed(Math.min(Date.now() - startedAt, holdDurationMs));
    }, tickMs);
    timers.current.done = window.setTimeout(() => {
      clearTimers();
      moveTo("complete");
      setElapsed(holdDurationMs);
      callbacks.current.onComplete();
    }, holdDurationMs);
    callbacks.current.onHoldStart?.();
  }

  function release(): void {
    keyRef.current = null;
    const was = phaseRef.current;
    if (was === "idle") {
      return;
    }
    clearTimers();
    moveTo("idle");
    setElapsed(0);
    if (was === "holding") {
      setAnnouncement("Released. Hold for 3 seconds to exit.");
      callbacks.current.onHoldCancel?.();
    }
  }

  return (
    <HoldControlView
      phase={phase}
      progress={elapsed / holdDurationMs}
      secondsLeft={Math.max(1, Math.ceil((holdDurationMs - elapsed) / 1000))}
      announcement={announcement}
      className={className}
      onKeyDown={(event) => {
        if (event.key !== "Enter" && event.key !== " ") {
          return;
        }
        event.preventDefault();
        if (!event.repeat && keyRef.current === null) {
          keyRef.current = event.key;
          begin();
        }
      }}
      onKeyUp={(event) => {
        if (event.key === keyRef.current) {
          event.preventDefault();
          release();
        }
      }}
      onBlur={release}
      onPointerDown={(event) => {
        if (event.button !== 0) {
          return;
        }
        if ("setPointerCapture" in event.currentTarget) {
          event.currentTarget.setPointerCapture(event.pointerId);
        }
        begin();
      }}
      onPointerUp={release}
      onPointerCancel={release}
      onLostPointerCapture={release}
    />
  );
}
