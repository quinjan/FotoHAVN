import { useEffect, useRef } from "react";
import { GuestCycleDetail } from "../../components/GuestCycleDetail/index.ts";
import { StatementHeading } from "../../components/StatementHeading/index.ts";
import { TextLink } from "../../components/TextLink/index.ts";
import type { GuestCycleDetailView, RunningEventPanel } from "../../protocol/generated.ts";
import { OperatorAssistanceAlert } from "../OperatorAssistanceAlert.tsx";
import { useSendCommand } from "../OperatorCommands.tsx";
import { backToGalleryLabel, detailHeading } from "./galleryCopy.ts";
import { operatorDetail } from "./galleryMapping.ts";
import styles from "./Gallery.module.css";

/**
 * The operator's Guest Cycle detail (plan section 8, Operator view), drawn from the brain's view alone: the full Photo Strip,
 * the kept Captures in arrangement order, the facts, `OPEN FOLDER` (`OpenGuestCycleFolder`), `DELETE` (`DeleteGuestCycle`; the
 * confirmation is the shell's overlay, not this surface's), and `BACK TO GALLERY` (`CloseGuestCycle`), where focus starts so
 * the way back is one key away. A Guest Cycle whose files are gone says so.
 */
export function GuestCycleDetailSurface({ view, running }: { readonly view: GuestCycleDetailView; readonly running: RunningEventPanel | null }) {
  const send = useSendCommand();
  const backRef = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    backRef.current?.focus();
  }, []);

  const close = () => {
    void send("CloseGuestCycle", {}, { key: "close-guest-cycle" });
  };
  return (
    <div className={styles.view} data-gallery-guest-cycle={view.guestCycleId}>
      <header className={styles.header}>
        <TextLink ref={backRef} arrow="left" onClick={close}>
          {backToGalleryLabel}
        </TextLink>
        <StatementHeading level={1} eyebrow={view.eventName} lead={detailHeading.lead} turn={detailHeading.turn} />
      </header>
      <div className={styles.statuses}>
        <OperatorAssistanceAlert
          reason={running?.assistance?.reason ?? null}
          onReturn={() => {
            void send("CloseGallery", {}, { key: "close-gallery" });
          }}
        />
      </div>
      <GuestCycleDetail
        mode="operator"
        cycle={operatorDetail(view)}
        canOpenFolder={view.canOpenFolder}
        canDelete={view.canDelete}
        onOpenFolder={() => {
          void send("OpenGuestCycleFolder", { guestCycleId: view.guestCycleId }, { key: "open-guest-cycle-folder" });
        }}
        onDelete={() => {
          void send("DeleteGuestCycle", { guestCycleId: view.guestCycleId }, { key: "delete-guest-cycle" });
        }}
      />
    </div>
  );
}
