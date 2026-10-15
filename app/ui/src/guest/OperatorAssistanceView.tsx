import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { GuestCycleView, OperatorAssistanceView as OperatorAssistance } from "../protocol/generated.ts";
import screen from "../screens/Screen.module.css";
import { assistanceCopy } from "./finishCopy.ts";

/**
 * Operator Assistance on the Guest Screen (M8 UI-2). A placeholder until the view is built. `cycle` is the Guest Cycle the screen
 * returns to after RETRY, or null when the guest was on Start or in the guest gallery.
 */
export function OperatorAssistanceView({ view }: { readonly cycle: GuestCycleView | null; readonly view: OperatorAssistance }) {
  return (
    <div className={screen.screen} data-assistance-reason={view.reason}>
      <StatementHeading level={1} size="heading-lg" align="center" {...assistanceCopy} />
    </div>
  );
}