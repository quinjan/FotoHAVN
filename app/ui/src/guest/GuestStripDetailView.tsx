import { useEffect, useRef } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { GuestCycleDetail } from "../components/GuestCycleDetail/index.ts";
import type { GuestOverlays, GuestStripDetailView as GuestStripDetailData } from "../protocol/generated.ts";
import { GuestGalleryChrome } from "./GuestGalleryChrome.tsx";
import { guestDetail } from "./guestGalleryMapping.ts";

/**
 * The guest strip detail (plan section 8, Guest view), drawn from the brain's view alone: the strip lifted large beside its
 * arranged Captures, `CLOSE` (`CloseGuestStrip`), and `BACK TO START ←` (`BackToStart`). Nothing zooms past this size. Focus
 * starts on `CLOSE`. The Phase Timer is `GuestGalleryChrome`'s, as on the row; the timer is not reset by opening a strip.
 */
export function GuestStripDetailView({ view, overlays }: { readonly view: GuestStripDetailData; readonly overlays: GuestOverlays }) {
  const client = useBrainClient();
  const closeRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    closeRef.current?.focus();
  }, []);

  return (
    <GuestGalleryChrome overlays={overlays}>
      <GuestCycleDetail
        mode="guest"
        {...guestDetail(view)}
        closeRef={closeRef}
        onClose={() => {
          void client.sendCommand("CloseGuestStrip", {});
        }}
        onBack={() => {
          void client.sendCommand("BackToStart", {});
        }}
      />
    </GuestGalleryChrome>
  );
}
