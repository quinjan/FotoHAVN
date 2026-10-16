import { useEffect, useState } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { GuestCycleList } from "../components/GuestCycleList/index.ts";
import type { GuestGalleryView as GuestGalleryData, GuestOverlays } from "../protocol/generated.ts";
import { forgetOpenedStrip, rememberedStrip, rememberOpenedStrip } from "./galleryFocus.ts";
import { GuestGalleryChrome } from "./GuestGalleryChrome.tsx";
import { guestStrip } from "./guestGalleryMapping.ts";

/**
 * The guest gallery (plan section 8, Guest view), drawn from the brain's view alone: "Look at *you.*", the row of completed
 * Photo Strips that scrolls sideways by touch (`OpenGuestStrip` with a strip's id), the next page as the row nears its end
 * (`LoadMoreGuestStrips` with the view's cursor, once for each cursor), and `BACK TO START ←` (`BackToStart`). The
 * `guest_gallery` Phase Timer's warning, with `MORE TIME`, is the shell's overlay drawn by `GuestGalleryChrome`; the exit hold
 * and the PIN dialog are `GuestRoot`'s. The view carries images and alternative text only, so nothing else can be shown.
 */
export function GuestGalleryView({ view, overlays }: { readonly view: GuestGalleryData; readonly overlays: GuestOverlays }) {
  const client = useBrainClient();
  const [restoreFocus] = useState(rememberedStrip);
  useEffect(() => {
    forgetOpenedStrip();
  }, []);

  return (
    <GuestGalleryChrome overlays={overlays}>
      <GuestCycleList
        mode="guest"
        strips={view.strips.map(guestStrip)}
        hasMore={view.nextCursor !== null}
        pageKey={view.nextCursor}
        loading={view.loadingMore}
        restoreFocus={restoreFocus}
        onOpen={(guestCycleId) => {
          rememberOpenedStrip(guestCycleId);
          void client.sendCommand("OpenGuestStrip", { guestCycleId });
        }}
        onBack={() => {
          void client.sendCommand("BackToStart", {});
        }}
        onLoadMore={() => {
          if (view.nextCursor !== null) {
            void client.sendCommand("LoadMoreGuestStrips", { cursor: view.nextCursor });
          }
        }}
      />
    </GuestGalleryChrome>
  );
}
