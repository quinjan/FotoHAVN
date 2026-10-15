import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { GuestCycleView, PhotographsView } from "../protocol/generated.ts";
import screen from "../screens/Screen.module.css";
import { photographsCopy } from "./captureCopy.ts";

/** The Photographs step's Camera tab (M8 UI-1). A placeholder until the tab is built. */
export function CameraTab({ view }: { readonly cycle: GuestCycleView; readonly view: PhotographsView }) {
  return (
    <div className={screen.screen} data-photographs-tab="camera" data-sequence-running={view.sequence.running}>
      <StatementHeading
        level={1}
        size="heading-lg"
        align="center"
        lead={photographsCopy.lead}
        turn={photographsCopy.turn}
        supporting={photographsCopy.supporting}
      />
    </div>
  );
}