import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { GuestCycleView, TemplateView } from "../protocol/generated.ts";
import screen from "../screens/Screen.module.css";
import { templateCopy } from "./captureCopy.ts";

/** The Template step (M8 UI-1). A placeholder until the step is built: it renders the step's heading from the brain's view. */
export function TemplateStep({ view }: { readonly cycle: GuestCycleView; readonly view: TemplateView }) {
  return (
    <div className={screen.screen} data-selected-template={view.selectedId}>
      <StatementHeading level={1} size="heading-lg" align="center" {...templateCopy} />
    </div>
  );
}