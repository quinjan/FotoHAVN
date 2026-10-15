import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { GuestCycleView, KeepsakeView } from "../protocol/generated.ts";
import screen from "../screens/Screen.module.css";
import { keepsakeCopy } from "./finishCopy.ts";

/** The Keepsake step (M8 UI-2). A placeholder until the step is built. */
export function KeepsakeStep({ view }: { readonly cycle: GuestCycleView; readonly view: KeepsakeView }) {
  return (
    <div className={screen.screen} data-strip-landscape={view.landscape}>
      <StatementHeading level={1} size="display" align="center" {...keepsakeCopy} />
    </div>
  );
}