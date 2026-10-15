import { useEffect, useRef, useState, type ReactNode } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { Notice } from "../components/Notice/index.ts";
import { cx } from "../components/shared/cx.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { GuestCycleView, GuestPresentation } from "../protocol/generated.ts";
import { ConnectionNotice } from "../screens/StatusScreens.tsx";
import screen from "../screens/Screen.module.css";
import { ExitHold } from "./ExitHold.tsx";
import { FiltersStep } from "./FiltersStep.tsx";
import { guestCommandNotice } from "./guestCopy.ts";
import { GuestCycleChrome } from "./GuestCycleChrome.tsx";
import { KeepsakeStep } from "./KeepsakeStep.tsx";
import { OperatorAssistanceView } from "./OperatorAssistanceView.tsx";
import { PhotographsStep } from "./PhotographsStep.tsx";
import { PinDialog } from "./PinDialog.tsx";
import { StartSurface, StartUnavailableSurface, type GuestActions } from "./StartSurface.tsx";
import { TemplateStep } from "./TemplateStep.tsx";
import styles from "./Guest.module.css";

function GuestFatal() {
  return (
    <div className={screen.screen}>
      <StatementHeading
        level={1}
        size="heading-lg"
        align="center"
        lead="A little"
        turn="interruption."
        supporting="Please ask the FOTOHAVN team for help."
      />
    </div>
  );
}

/** A surface a later milestone builds, or a cycle surface whose view the brain did not send; guests see only the wordmark. */
function GuestPending() {
  return (
    <div className={screen.screen}>
      <h1 className={screen.wordmark}>FOTOHAVN</h1>
    </div>
  );
}

/** A Guest Cycle step inside the shared chrome. The brain sends the cycle and the step view together, so a missing one is a placeholder. */
function withChrome<View>(
  presentation: GuestPresentation,
  render: (cycle: GuestCycleView, view: View) => ReactNode,
  view: View | null,
): ReactNode {
  const { guestCycle } = presentation;
  if (guestCycle === null || view === null) {
    return <GuestPending />;
  }
  return (
    <GuestCycleChrome cycle={guestCycle} overlays={presentation.overlays}>
      {render(guestCycle, view)}
    </GuestCycleChrome>
  );
}

function GuestSurface({ presentation, actions }: { readonly presentation: GuestPresentation; readonly actions: GuestActions }) {
  switch (presentation.surface) {
    case "start":
      return <StartSurface start={presentation.start} actions={actions} />;
    case "startUnavailable":
      return presentation.startUnavailable === null ? (
        <StartSurface start={null} actions={actions} />
      ) : (
        <StartUnavailableSurface unavailable={presentation.startUnavailable} actions={actions} />
      );
    case "template":
      return withChrome(presentation, (cycle, view) => <TemplateStep cycle={cycle} view={view} />, presentation.template);
    case "photographs":
      return withChrome(presentation, (cycle, view) => <PhotographsStep cycle={cycle} view={view} />, presentation.photographs);
    case "filters":
      return withChrome(presentation, (cycle, view) => <FiltersStep cycle={cycle} view={view} />, presentation.filters);
    case "keepsake":
      return withChrome(presentation, (cycle, view) => <KeepsakeStep cycle={cycle} view={view} />, presentation.keepsake);
    case "operatorAssistance":
      return presentation.operatorAssistance === null ? (
        <GuestPending />
      ) : (
        <OperatorAssistanceView cycle={presentation.guestCycle} view={presentation.operatorAssistance} />
      );
    case "fatal":
      return <GuestFatal />;
    default:
      return <GuestPending />;
  }
}

/** Every Guest Screen surface, chosen by the guest presentation, with the exit hold on all of them. */
export function GuestRoot({
  presentation,
  reconnecting,
}: {
  readonly presentation: GuestPresentation;
  readonly reconnecting: boolean;
}) {
  const client = useBrainClient();
  const [localNotice, setLocalNotice] = useState("");
  const pinDialog = presentation.overlays.pinDialog;
  const dialogOpen = pinDialog !== null;
  const exitHold = useRef<HTMLDivElement>(null);
  const dialogWasOpen = useRef(false);
  const brainNotice = presentation.overlays.notice?.message ?? "";
  const notice = brainNotice === "" ? localNotice : brainNotice;

  // The exit hold is unmounted while the PIN dialog is open, so the dialog has no opener to return to: focus goes back to the hold when it closes.
  useEffect(() => {
    if (dialogWasOpen.current && !dialogOpen) {
      exitHold.current?.querySelector("button")?.focus();
    }
    dialogWasOpen.current = dialogOpen;
  }, [dialogOpen]);

  async function send(name: "BeginGuestCycle" | "OpenGuestGallery"): Promise<void> {
    setLocalNotice("");
    setLocalNotice(guestCommandNotice(await client.sendCommand(name, {})) ?? "");
  }

  const actions: GuestActions = {
    onStepInside: () => {
      void send("BeginGuestCycle");
    },
    onOpenGallery: () => {
      void send("OpenGuestGallery");
    },
  };

  return (
    <main className={screen.root} data-fotohavn-surface={presentation.surface}>
      <div className={styles.surface} inert={pinDialog !== null}>
        <GuestSurface presentation={presentation} actions={actions} />
      </div>
      {pinDialog === null ? <ExitHold ref={exitHold} /> : <PinDialog pin={pinDialog} />}
      <div className={screen.notices} inert={pinDialog !== null}>
        <Notice
          className={cx(styles.notice)}
          {...(brainNotice === "" && localNotice !== ""
            ? {
                onDismiss: () => {
                  setLocalNotice("");
                },
              }
            : {})}
        >
          {notice}
        </Notice>
        <ConnectionNotice reconnecting={reconnecting} />
      </div>
    </main>
  );
}
