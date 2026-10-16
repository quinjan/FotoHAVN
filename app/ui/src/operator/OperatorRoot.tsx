import { Notice } from "../components/Notice/index.ts";
import { StartingScreen } from "../components/StartingScreen/index.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { OperatorNoticeCode, OperatorPresentation } from "../protocol/generated.ts";
import { ConnectionNotice } from "../screens/StatusScreens.tsx";
import screen from "../screens/Screen.module.css";
import { CameraDiagnosticsSurface } from "./CameraDiagnosticsSurface.tsx";
import { noticeCopy, singleScreenCopy } from "./copy.ts";
import { EventList } from "./EventList.tsx";
import { EventGallerySurface } from "./gallery/EventGallerySurface.tsx";
import { GuestCycleDetailSurface } from "./gallery/GuestCycleDetailSurface.tsx";
import styles from "./Operator.module.css";
import { LocalNotice, OperatorNoticeProvider, useSendCommand } from "./OperatorCommands.tsx";
import { OperatorDialogs } from "./OperatorDialogs.tsx";
import { EventSetup } from "./setup/EventSetup.tsx";
import { StorageErrorSurface } from "./StorageErrorSurface.tsx";

function OperatorFatal({ reason }: { readonly reason: string | null }) {
  return (
    <div className={screen.screen}>
      <StatementHeading
        level={1}
        size="heading-lg"
        align="center"
        lead="Something needs"
        turn="attention."
        {...(reason === null ? {} : { supporting: reason })}
      />
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

/** The Guest Screen covers this window (one display only): the operator is told, and only the hold and PIN end it. */
function GuestScreenActive() {
  return (
    <div className={screen.screen}>
      <StatementHeading level={1} size="heading-lg" align="center" lead={singleScreenCopy} />
    </div>
  );
}

function OperatorSurface({ presentation }: { readonly presentation: OperatorPresentation }) {
  switch (presentation.surface) {
    case "starting":
      return <StartingScreen />;
    case "eventList":
      return <EventList events={presentation.eventList?.events ?? []} running={presentation.runningEvent} />;
    case "eventSetup":
      return presentation.eventSetup === null ? (
        <OperatorPending />
      ) : (
        <EventSetup
          view={presentation.eventSetup}
          identifying={presentation.overlays.identifyDisplays !== null}
          assistance={presentation.runningEvent?.assistance?.reason ?? null}
        />
      );
    case "guestScreenActive":
      return <GuestScreenActive />;
    case "eventGallery":
      return presentation.eventGallery === null ? (
        <OperatorPending />
      ) : (
        <EventGallerySurface view={presentation.eventGallery} running={presentation.runningEvent} />
      );
    case "guestCycleDetail":
      return presentation.guestCycleDetail === null ? (
        <OperatorPending />
      ) : (
        <GuestCycleDetailSurface view={presentation.guestCycleDetail} running={presentation.runningEvent} />
      );
    case "fatal":
      return <OperatorFatal reason={presentation.fatal?.reason ?? null} />;
    case "storageError":
      return presentation.storageError === null ? <OperatorPending /> : <StorageErrorSurface view={presentation.storageError} />;
    case "cameraDiagnostics":
      return presentation.cameraDiagnostics === null ? (
        <OperatorPending />
      ) : (
        <CameraDiagnosticsSurface view={presentation.cameraDiagnostics} />
      );
    default:
      return <OperatorPending />;
  }
}

/** The brain's notice (a save or delete that didn't go through): the code is the brain's, the words are here. */
function BrainNotice({ code }: { readonly code: OperatorNoticeCode | null }) {
  const send = useSendCommand();
  return (
    <Notice
      onDismiss={() => {
        void send("DismissNotice", {}, { key: "dismiss-notice" });
      }}
    >
      {code === null ? "" : noticeCopy[code]}
    </Notice>
  );
}

/**
 * Every Operator Screen surface, chosen by the operator presentation. Notices sit above the surface; a confirmation
 * opens over it, and while one is open the surface behind is inert, so focus and the keyboard stay in the dialog.
 */
export function OperatorRoot({
  presentation,
  reconnecting,
}: {
  readonly presentation: OperatorPresentation;
  readonly reconnecting: boolean;
}) {
  const { confirmation, notice } = presentation.overlays;
  return (
    <main className={screen.root} data-fotohavn-surface={presentation.surface}>
      <OperatorNoticeProvider>
        <div inert={confirmation !== null}>
          <div className={styles.notices}>
            <LocalNotice />
            <BrainNotice code={notice === null ? null : notice.code} />
          </div>
          <OperatorSurface presentation={presentation} />
        </div>
        <div className={screen.notices}>
          <ConnectionNotice reconnecting={reconnecting} />
        </div>
        <OperatorDialogs confirmation={confirmation} />
      </OperatorNoticeProvider>
    </main>
  );
}
