import type { GuestPresentation, StartView } from "../protocol/generated.ts";
import { ConnectionNotice } from "../screens/StatusScreens.tsx";
import screen from "../screens/Screen.module.css";

/** M2 placeholder for Start: the statement and the wordmark. `STEP INSIDE →` arrives with the Guest Cycle (M8). */
function StartPlaceholder({ start }: { readonly start: StartView | null }) {
  return (
    <div className={screen.screen} data-guest-gallery-available={start?.guestGalleryAvailable ?? false}>
      <p className={screen.wordmark}>FOTOHAVN</p>
      <h1 className={screen.statement}>
        Small room. <em>Whole other world.</em>
      </h1>
      <p className={screen.line}>Come a little closer.</p>
    </div>
  );
}

function GuestFatal() {
  return (
    <div className={screen.screen}>
      <h1 className={screen.statement}>
        A little <em>interruption.</em>
      </h1>
      <p className={screen.line}>Please ask the FOTOHAVN team for help.</p>
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
