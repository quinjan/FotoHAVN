import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { GuestOverlays, GuestStripDetailView as GuestStripDetailData } from "../protocol/generated.ts";
import screen from "../screens/Screen.module.css";

/**
 * The guest strip detail (M9 UI-2; plan section 8, Event Gallery, Guest view). Wave A routes `surface: guestStripDetail` here and ships a
 * placeholder; UI-2 builds it from `view` alone with `GuestCycleDetail` in guest mode: the strip large with its arranged Captures, `CLOSE`
 * (`CloseGuestStrip`), and `BACK TO START ←` (`BackToStart`). The `guest_gallery` Phase Timer's warning is `overlays.phaseTimerWarning`
 * (phase `guestGallery`) with `MORE TIME` (`MoreTime`). `GuestRoot` is frozen: it passes exactly these props.
 */
export function GuestStripDetailView({ view }: { readonly view: GuestStripDetailData; readonly overlays: GuestOverlays }) {
  return (
    <div className={screen.screen} data-guest-capture-count={view.captures.length}>
      <StatementHeading level={1} size="heading-lg" align="center" lead="Look at" turn="you." />
    </div>
  );
}
