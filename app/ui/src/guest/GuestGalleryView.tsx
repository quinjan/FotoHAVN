import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { GuestGalleryView as GuestGalleryData, GuestOverlays } from "../protocol/generated.ts";
import screen from "../screens/Screen.module.css";

/**
 * The guest gallery (M9 UI-2; plan section 8, Event Gallery, Guest view). Wave A routes `surface: guestGallery` here and ships a placeholder;
 * UI-2 builds it from `view` alone with `GuestCycleList` in guest mode: "Look at *you.*", the horizontally scrolling row of Photo Strips
 * (`OpenGuestStrip` with a strip's `guestCycleId`, `LoadMoreGuestStrips` with `view.nextCursor`), and `BACK TO START ←` (`BackToStart`). The
 * `guest_gallery` Phase Timer's warning is `overlays.phaseTimerWarning` (phase `guestGallery`) with `MORE TIME` (`MoreTime`); the exit hold and
 * the PIN dialog are the shell's. `GuestRoot` is frozen: it passes exactly these props.
 */
export function GuestGalleryView({ view }: { readonly view: GuestGalleryData; readonly overlays: GuestOverlays }) {
  return (
    <div className={screen.screen} data-guest-strip-count={view.strips.length}>
      <StatementHeading level={1} size="heading-lg" align="center" lead="Look at" turn="you." />
    </div>
  );
}
