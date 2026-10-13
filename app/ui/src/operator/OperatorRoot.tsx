import type { OperatorPresentation } from "../protocol/generated.ts";
import { ConnectionNotice } from "../screens/StatusScreens.tsx";
import screen from "../screens/Screen.module.css";
import { EventList } from "./EventList.tsx";
import styles from "./Operator.module.css";

function OperatorFatal({ reason }: { readonly reason: string | null }) {
  return (
    <div className={screen.screen}>
      <h1 className={screen.statement}>
        Something needs <em>attention.</em>
      </h1>
      {reason !== null && <p className={screen.line}>{reason}</p>}
    </div>
  );
}

/** A surface a later milestone builds; the operator still learns where the brain is. */
function OperatorPending() {
  return (
    <div className={screen.screen}>
      <h1 className={screen.wordmark}>FOTOHAVN</h1>
      <p className={screen.line}>This view isn&apos;t ready yet.</p>
    </div>
  );
}

function OperatorSurface({ presentation }: { readonly presentation: OperatorPresentation }) {
  switch (presentation.surface) {
    case "eventList":
      return <EventList events={presentation.eventList?.events ?? []} />;
    case "fatal":
      return <OperatorFatal reason={presentation.fatal?.reason ?? null} />;
    default:
      return <OperatorPending />;
  }
}

/** Every Operator Screen surface, chosen by the operator presentation. */
export function OperatorRoot({
  presentation,
  reconnecting,
}: {
  readonly presentation: OperatorPresentation;
  readonly reconnecting: boolean;
}) {
  return (
    <main className={screen.root} data-fotohavn-surface={presentation.surface}>
      <OperatorSurface presentation={presentation} />
      <div className={screen.notices}>
        <p className={styles.notice} role="status">
          {presentation.overlays.notice?.message ?? ""}
        </p>
        <ConnectionNotice reconnecting={reconnecting} />
      </div>
    </main>
  );
}
