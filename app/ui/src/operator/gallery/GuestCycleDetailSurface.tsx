import { StatementHeading } from "../../components/StatementHeading/index.ts";
import type { GuestCycleDetailView, RunningEventPanel } from "../../protocol/generated.ts";
import screen from "../../screens/Screen.module.css";

/**
 * The operator's Guest Cycle detail (M9 UI-1; plan section 8, Event Gallery, Operator view). Wave A routes `surface: guestCycleDetail` here
 * and ships a placeholder; UI-1 builds it from `view` alone with `GuestCycleDetail` in operator mode: the full Photo Strip, the Captures in
 * arrangement order, the facts, `OPEN FOLDER` (`OpenGuestCycleFolder`), `DELETE` (`DeleteGuestCycle`), and the way back (`CloseGuestCycle`). The
 * delete confirmation is the shell's `confirmation` overlay (kind `deleteGuestCycle`), not part of this surface. `OperatorRoot` is frozen: it
 * passes exactly these props.
 */
export function GuestCycleDetailSurface({ view }: { readonly view: GuestCycleDetailView; readonly running: RunningEventPanel | null }) {
  return (
    <div className={screen.screen} data-gallery-guest-cycle={view.guestCycleId}>
      <StatementHeading level={1} size="heading-lg" align="center" lead="Guest" turn="Cycle." />
    </div>
  );
}
