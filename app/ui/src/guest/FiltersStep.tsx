import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { FiltersView, GuestCycleView } from "../protocol/generated.ts";
import screen from "../screens/Screen.module.css";
import { filtersCopy } from "./finishCopy.ts";

/** The Filters step (M8 UI-2). A placeholder until the step is built. */
export function FiltersStep({ view }: { readonly cycle: GuestCycleView; readonly view: FiltersView }) {
  return (
    <div className={screen.screen} data-selected-look={view.selectedLookId}>
      <StatementHeading level={1} size="heading-lg" align="center" {...filtersCopy} />
    </div>
  );
}