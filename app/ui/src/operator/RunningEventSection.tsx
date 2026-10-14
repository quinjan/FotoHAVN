import { useEffect, useState } from "react";
import { RunningEventPanel, type RunningCameraState } from "../components/RunningEventPanel/index.ts";
import type { CameraState, RunningEventPanel as RunningEventFacts } from "../protocol/generated.ts";
import { assistanceCopy, panelStep } from "./copy.ts";
import { formatTimeSinceStart } from "./format.ts";
import { useSendCommand } from "./OperatorCommands.tsx";

/** How often the time since start is read again: it shows whole minutes, so twice a minute is always current. */
export const timeSinceStartIntervalMs = 30_000;

/** The page's clock in epoch milliseconds, read again every `intervalMs`. */
function useNow(intervalMs: number): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => {
      setNow(Date.now());
    }, intervalMs);
    return () => {
      clearInterval(timer);
    };
  }, [intervalMs]);
  return now;
}

/** `ready` is Ready, the two failing states are Not available, and the brain's neutral states read as Checking. */
export function runningCameraState(state: CameraState): RunningCameraState {
  switch (state) {
    case "ready":
      return "ready";
    case "notAvailable":
    case "inUseByAnotherApp":
      return "not-available";
    case "idle":
    case "checking":
      return "checking";
  }
}

/**
 * The running-Event panel, fed by the brain's facts and sending the panel's three commands. The time since start is the
 * only thing it works out itself, from `startedAt` and the page clock.
 */
export function RunningEventSection({ panel }: { readonly panel: RunningEventFacts }) {
  const send = useSendCommand();
  const now = useNow(timeSinceStartIntervalMs);
  return (
    <RunningEventPanel
      eventName={panel.name}
      guestDisplay={panel.guestDisplay.state}
      camera={runningCameraState(panel.camera.state)}
      step={panelStep(panel.currentStep)}
      completedGuestCycles={panel.completedGuestCycles}
      timeSinceStart={formatTimeSinceStart(panel.startedAt, now)}
      {...(panel.assistance === null ? {} : { assistanceReason: assistanceCopy[panel.assistance.reason] })}
      onOpenGallery={() => {
        void send("OpenGallery", { eventId: panel.eventId }, { key: "open-gallery" });
      }}
      onRetry={() => {
        void send("RetryAssistance", {}, { key: "retry-assistance" });
      }}
      onEndEvent={() => {
        void send("EndEvent", {}, { key: "end-event" });
      }}
    />
  );
}
