import { useEffect, useRef } from "react";
import type { GuestPresentation } from "../protocol/generated.ts";
import { sharedCaptureSounds, type CaptureSoundSink } from "./captureSoundSink.ts";

interface Seen {
  readonly countdown: number | null;
  readonly flash: number;
}

/**
 * Plays the capture sequence's sounds from the guest presentation (app/DESIGN.md, Sound): one tick for each new countdown
 * numeral and one shutter sound for each Capture, aligned with the flash. It plays only when the brain says the Event's Guest
 * sounds are on, and never for what the page already shows when it mounts or when the Photographs step returns, so a
 * reload or a step change stays silent. Audio is readied from the guest's own taps, which the browser counts as the gesture
 * it needs. Reduced motion doesn't change it: that preference is about movement, and the numerals and flash stay the visual cues.
 */
export function useCaptureSounds(presentation: GuestPresentation, sink: CaptureSoundSink = sharedCaptureSounds()): void {
  const sequence = presentation.surface === "photographs" ? (presentation.photographs?.sequence ?? null) : null;
  const inSequenceView = sequence !== null;
  const soundsOn = sequence?.soundsOn ?? false;
  const countdown = sequence?.countdownRemaining ?? null;
  const flash = sequence?.flashSequence ?? 0;
  const seen = useRef<Seen | null>(null);

  useEffect(() => {
    if (!soundsOn) {
      return undefined;
    }
    const prime = () => {
      sink.prime();
    };
    document.addEventListener("pointerdown", prime, true);
    document.addEventListener("keydown", prime, true);
    return () => {
      document.removeEventListener("pointerdown", prime, true);
      document.removeEventListener("keydown", prime, true);
    };
  }, [soundsOn, sink]);

  useEffect(() => {
    const before = seen.current;
    seen.current = inSequenceView ? { countdown, flash } : null;
    if (before === null || !inSequenceView || !soundsOn) {
      return;
    }
    if (flash > before.flash) {
      sink.shutter();
    }
    if (countdown !== null && countdown > 0 && countdown !== before.countdown) {
      sink.tick();
    }
  }, [inSequenceView, soundsOn, countdown, flash, sink]);
}
