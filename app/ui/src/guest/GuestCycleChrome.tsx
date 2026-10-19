import { useState, type ReactNode } from "react";
import { PhaseTimerPill } from "../components/PhaseTimerPill/index.ts";
import { cx } from "../components/shared/cx.ts";
import { StepProgress } from "../components/StepProgress/index.ts";
import type { GuestCycleView, GuestOverlays } from "../protocol/generated.ts";
import { stepsLabel } from "./finishCopy.ts";
import styles from "./GuestCycleChrome.module.css";

/**
 * What every Guest Cycle step shares, drawn from `cycle` and `overlays` alone (the UI keeps no rule):
 *
 * - the step progress, omitting skipped steps and numbering the rest from 01 (Q26); it only shows where the guest is, and
 *   no step in it is a control (app/DESIGN.md, Deliberate departures: no going back by the step progress);
 * - the Phase Timer, always visible at the band's right end, beside the exit hold, while the brain sends one for the
 *   step, with no way to extend it;
 * - the step crossfade: after the first step, each step arrives with a 240 ms fade (none under reduced motion).
 */
export function GuestCycleChrome({
  cycle,
  overlays,
  children,
}: {
  readonly cycle: GuestCycleView;
  readonly overlays: GuestOverlays;
  readonly children: ReactNode;
}) {
  const [lastStep, setLastStep] = useState(cycle.step);
  const [fades, setFades] = useState(false);
  if (lastStep !== cycle.step) {
    setLastStep(cycle.step);
    setFades(true);
  }

  const shown = cycle.steps.filter((entry) => entry.state !== "skipped");
  const timer = overlays.phaseTimer;

  return (
    <div className={styles.chrome} data-fotohavn-cycle-step={cycle.step}>
      <header className={styles.band}>
        <StepProgress
          className={cx(styles.progress)}
          label={stepsLabel}
          current={cycle.step}
          steps={shown.map((entry) => entry.step)}
        />
        {timer !== null && (
          <PhaseTimerPill
            key={timer.phase}
            className={cx(styles.timer)}
            secondsRemaining={timer.remainingSeconds}
            paused={timer.paused}
          />
        )}
      </header>
      <div key={cycle.step} className={styles.stage} data-fade={fades ? "in" : undefined}>
        {children}
      </div>
    </div>
  );
}