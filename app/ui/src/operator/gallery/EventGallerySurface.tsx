import { StatementHeading } from "../../components/StatementHeading/index.ts";
import type { EventGalleryView, RunningEventPanel } from "../../protocol/generated.ts";
import screen from "../../screens/Screen.module.css";

/**
 * The operator's Event Gallery (M9 UI-1; plan section 8, Event Gallery, Operator view). Wave A routes `surface: eventGallery` here and
 * ships a placeholder; UI-1 builds it from `view` alone with `GuestCycleList` in operator mode: the list, `SHOW MORE ↓`
 * (`LoadMoreGuestCycles` with `view.nextCursor`), `OPEN` (`OpenGuestCycle`), `DELETE` (`DeleteGuestCycle`), the load-failure retry, the empty
 * state, and the inline Operator Assistance status line when `running.assistance` is set. `OperatorRoot` is frozen: it passes exactly these props.
 */
export function EventGallerySurface({ view }: { readonly view: EventGalleryView; readonly running: RunningEventPanel | null }) {
  return (
    <div className={screen.screen} data-gallery-event={view.eventId}>
      <StatementHeading level={1} size="heading-lg" align="center" lead="Event" turn="Gallery." />
    </div>
  );
}
