import { PrimaryAction } from "../components/PrimaryAction/index.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import { TextLink } from "../components/TextLink/index.ts";
import type { StartUnavailableView, StartView } from "../protocol/generated.ts";
import screen from "../screens/Screen.module.css";
import { galleryLinkLabel, startUnavailableCopy, stepInsideLabel } from "./guestCopy.ts";
import styles from "./Guest.module.css";

export interface GuestActions {
  /** `BeginGuestCycle`. */
  readonly onStepInside: () => void;
  /** `OpenGuestGallery`. */
  readonly onOpenGallery: () => void;
}

function GalleryLink({ onOpen }: { readonly onOpen: () => void }) {
  return (
    <div className={styles.gallery}>
      <TextLink arrow="right" onClick={onOpen}>
        {galleryLinkLabel}
      </TextLink>
    </div>
  );
}

/**
 * Start (app/DESIGN.md, Guest Screen): the wordmark, the statement, and `STEP INSIDE →` as the one primary action, on
 * an Off-white canvas. The action is unavailable until the brain says the booth can take a guest. The gallery link
 * stays hidden unless the brain shows it.
 */
export function StartSurface({ start, actions }: { readonly start: StartView | null; readonly actions: GuestActions }) {
  const stepInsideAvailable = start?.stepInsideAvailable ?? false;
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
        action={
          <PrimaryAction arrow="right" disabled={!stepInsideAvailable} onClick={actions.onStepInside}>
            {stepInsideLabel}
          </PrimaryAction>
        }
      />
      {start?.guestGalleryLinkVisible === true && <GalleryLink onOpen={actions.onOpenGallery} />}
    </div>
  );
}

/**
 * Start unavailable: the same canvas with a calm line for why, and no action. It recovers on its own when the brain
 * sends Start again. The gallery needs no camera, so its link stays when the brain shows it.
 */
export function StartUnavailableSurface({
  unavailable,
  actions,
}: {
  readonly unavailable: StartUnavailableView;
  readonly actions: GuestActions;
}) {
  const { lead, turn, supporting } = startUnavailableCopy(unavailable.reason);
  return (
    <div className={screen.start} data-unavailable-reason={unavailable.reason}>
      <p className={screen.wordmark}>FOTOHAVN</p>
      <StatementHeading level={1} size="display" align="center" lead={lead} turn={turn} supporting={supporting} />
      {unavailable.guestGalleryLinkVisible && <GalleryLink onOpen={actions.onOpenGallery} />}
    </div>
  );
}
