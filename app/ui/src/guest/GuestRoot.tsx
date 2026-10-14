import type { GuestPresentation, StartView } from "../protocol/generated.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import { ConnectionNotice } from "../screens/StatusScreens.tsx";
import screen from "../screens/Screen.module.css";

/**
 * M2 placeholder for Start: the wordmark at the top, the centered statement, and its supporting line. `STEP INSIDE →`
 * and `SEE THE GOOD COMPANY →` arrive with the Guest Cycle (M8).
 */
function StartPlaceholder({ start }: { readonly start: StartView | null }) {
  return (
    <div className={screen.start} data-guest-gallery-available={start?.guestGalleryLinkVisible ?? false}>
      <p className={screen.wordmark}>FOTOHAVN</p>
      <StatementHeading
        level={1}
        size="display"
        align="center"
        lead="Small room."
        turn="Whole other world."
        supporting="Come a little closer."
      />
    </div>
  );
}

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

/** A surface a later milestone builds; guests see only the wordmark. */
function GuestPending() {
  return (
    <div className={screen.screen}>
      <h1 className={screen.wordmark}>FOTOHAVN</h1>
    </div>
  );
}

function GuestSurface({ presentation }: { readonly presentation: GuestPresentation }) {
  switch (presentation.surface) {
    case "start":
      return <StartPlaceholder start={presentation.start} />;
    case "fatal":
      return <GuestFatal />;
    default:
      return <GuestPending />;
  }
}

/** Every Guest Screen surface, chosen by the guest presentation. */
export function GuestRoot({
  presentation,
  reconnecting,
}: {
  readonly presentation: GuestPresentation;
  readonly reconnecting: boolean;
}) {
  return (
    <main className={screen.root} data-fotohavn-surface={presentation.surface}>
      <GuestSurface presentation={presentation} />
      <div className={screen.notices}>
        <p className={screen.notice} role="status">
          {presentation.overlays.notice?.message ?? ""}
        </p>
        <ConnectionNotice reconnecting={reconnecting} />
      </div>
    </main>
  );
}
