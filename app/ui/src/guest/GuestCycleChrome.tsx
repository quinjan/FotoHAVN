import { useState, type ReactNode } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { PhaseTimerWarning, type PhaseTimerWarningPhase } from "../components/PhaseTimerWarning/index.ts";
import { cx } from "../components/shared/cx.ts";
import { StartOverAction, type StartOverStep } from "../components/StartOverAction/index.ts";
import { StepProgress, type GuestStepId } from "../components/StepProgress/index.ts";
import type { GuestCycleView, GuestOverlays, GuestStep, PhaseTimer } from "../protocol/generated.ts";
import { stepsLabel } from "./finishCopy.ts";
import styles from "./GuestCycleChrome.module.css";

/** The warning's phase ids are the component's kebab-case; Keepsake never warns, so it maps to nothing. */
const warningPhases: Readonly<Record<PhaseTimer, PhaseTimerWarningPhase | null>> = {
  template: "template",
  photographsCapture: "photographs-capture",
  photographsReview: "photographs-review",
  filters: "filters",
  keepsake: null,
  guestGallery: "guest-gallery",
};

const startOverSteps: ReadonlySet<string> = new Set<StartOverStep>(["template", "photographs", "filters"]);

function isStartOverStep(step: GuestCycleView["step"]): step is StartOverStep {
  return startOverSteps.has(step);
}

/**
 * What every Guest Cycle step shares (M8 UI-2), drawn from `cycle` and `overlays` alone (the UI keeps no rule):
 *
 * - the step progress, omitting skipped steps and numbering the rest from 01 (Q26), with BACK as `GoToStep` on the steps
 *   the brain says can be revisited;
 * - the quiet `START OVER` on Template, Photographs, and Filters, with its confirmation (`StartOver`, `ConfirmStartOver`,
 *   `KeepGoing`); the content behind the confirmation is inert;
 * - the Phase Timer warning with `MORE TIME` (`MoreTime`), in the band between the progress and START OVER, announced once;
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
  const client = useBrainClient();
  const [lastStep, setLastStep] = useState(cycle.step);
  const [fades, setFades] = useState(false);
  if (lastStep !== cycle.step) {
    setLastStep(cycle.step);
    setFades(true);
  }

  const startOverStep = cycle.startOverAvailable && isStartOverStep(cycle.step) ? cycle.step : null;
  // The confirmation belongs to START OVER: where START OVER isn't offered (Keepsake), nothing is held behind a dialog.
  const confirming = startOverStep !== null && overlays.startOverConfirmation !== null;
  const warning = cycle.step === "keepsake" ? null : overlays.phaseTimerWarning;
  const warningPhase = warning === null ? null : warningPhases[warning.phase];
  // A warning already under way when the chrome mounts (a reconnect, a fixture) is shown at rest instead of arriving again.
  const [warnedAtMount, setWarnedAtMount] = useState(warning !== null);
  if (warning === null && warnedAtMount) {
    setWarnedAtMount(false);
  }

  const shown = cycle.steps.filter((entry) => entry.state !== "skipped");
  const canGoBack = cycle.step !== "keepsake" && shown.some((entry) => entry.canGoBack);
  const send = (name: "StartOver" | "ConfirmStartOver" | "KeepGoing" | "MoreTime"): void => {
    void client.sendCommand(name, {});
  };

  return (
    <div className={styles.chrome} data-fotohavn-cycle-step={cycle.step} data-start-over-available={cycle.startOverAvailable}>
      <header className={styles.band}>
        <StepProgress
          className={cx(styles.progress)}
          label={stepsLabel}
          current={cycle.step}
          steps={shown.map((entry) => entry.step)}
          skipped={shown.filter((entry) => !entry.canGoBack).map((entry) => entry.step)}
          {...(canGoBack
            ? {
                // Keepsake is last, so it is never completed while another step is current: only the three earlier steps arrive here.
                onSelectStep: (step: GuestStepId) => {
                  void client.sendCommand("GoToStep", { step: step as GuestStep });
                },
              }
            : {})}
        />
        {warning !== null && warningPhase !== null && (
          <PhaseTimerWarning
            key={warningPhase}
            className={cx(styles.warning)}
            phase={warningPhase}
            secondsRemaining={warning.remainingSeconds}
            arrive={!warnedAtMount}
            onMoreTime={() => {
              send("MoreTime");
            }}
          />
        )}
        {startOverStep !== null && (
          <StartOverAction
            step={startOverStep}
            confirming={confirming}
            onStartOver={() => {
              send("StartOver");
            }}
            onConfirm={() => {
              send("ConfirmStartOver");
            }}
            onKeepGoing={() => {
              send("KeepGoing");
            }}
          />
        )}
      </header>
      <div key={cycle.step} className={styles.stage} data-fade={fades ? "in" : undefined} inert={confirming}>
        {children}
      </div>
    </div>
  );
}
