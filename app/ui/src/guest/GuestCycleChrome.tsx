import type { ReactNode } from "react";
import type { GuestCycleView, GuestOverlays } from "../protocol/generated.ts";

/**
 * What every Guest Cycle step shares (M8 UI-2): the step progress, the quiet START OVER with its confirmation, and the phase timer warning
 * with MORE TIME, drawn from `cycle` and `overlays`. A placeholder until built: it wraps the step and names it for tests.
 */
export function GuestCycleChrome({ cycle, children }: { readonly cycle: GuestCycleView; readonly overlays: GuestOverlays; readonly children: ReactNode }) {
  return (
    <div data-fotohavn-cycle-step={cycle.step} data-start-over-available={cycle.startOverAvailable}>
      {children}
    </div>
  );
}