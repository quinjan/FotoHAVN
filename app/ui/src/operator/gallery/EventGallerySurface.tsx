import { useEffect, useState } from "react";
import { GuestCycleList, loadingFirstCopy } from "../../components/GuestCycleList/index.ts";
import { InlineStatus } from "../../components/InlineStatus/index.ts";
import { StatementHeading } from "../../components/StatementHeading/index.ts";
import { TextLink } from "../../components/TextLink/index.ts";
import type { EventGalleryView, RunningEventPanel } from "../../protocol/generated.ts";
import { OperatorAssistanceAlert } from "../OperatorAssistanceAlert.tsx";
import { formatGalleryCount } from "../format.ts";
import { useSendCommand } from "../OperatorCommands.tsx";
import { backToEventsLabel, heading, loadFailedLine, runningLabel, runningLine, tryAgainLabel } from "./galleryCopy.ts";
import { forgetOpened, rememberedFocus, rememberOpened } from "./galleryFocus.ts";
import { operatorItem } from "./galleryMapping.ts";
import styles from "./Gallery.module.css";

/**
 * The operator's Event Gallery (plan section 8, Operator view), drawn from the brain's view alone: the Event's Guest Cycles
 * newest first (the list component in operator mode), the next page on `SHOW MORE ↓` or when the end of the list comes
 * near (`LoadMoreGuestCycles` with the view's cursor, once for each cursor), `OPEN` (`OpenGuestCycle`), `DELETE`
 * (`DeleteGuestCycle`, which the brain answers with its confirmation), and `BACK TO EVENTS` (`CloseGallery`). A page that
 * couldn't be read says so with `TRY AGAIN`; the running Event's gallery says it is live, and shows Operator Assistance.
 */
export function EventGallerySurface({ view, running }: { readonly view: EventGalleryView; readonly running: RunningEventPanel | null }) {
  const send = useSendCommand();
  const [restoreFocus] = useState(() => {
    const remembered = rememberedFocus();
    return remembered?.eventId === view.eventId ? remembered : null;
  });
  useEffect(() => {
    forgetOpened();
  }, []);

  const cycles = view.items.map(operatorItem);
  const close = () => {
    void send("CloseGallery", {}, { key: "close-gallery" });
  };
  const loadMore = (quiet: boolean) => {
    if (view.nextCursor !== null) {
      void send(
        "LoadMoreGuestCycles",
        { cursor: view.nextCursor },
        { key: "load-more-guest-cycles", ...(quiet ? { expected: ["notAvailable"] as const } : {}) },
      );
    }
  };
  const retry = () => {
    if (view.nextCursor === null) {
      void send("OpenGallery", { eventId: view.eventId }, { key: "open-gallery" });
    } else {
      loadMore(false);
    }
  };

  const firstPageLoading = view.items.length === 0 && view.loadingMore && !view.loadFailed;
  return (
    <div className={styles.view} data-gallery-event={view.eventId}>
      <header className={styles.header}>
        <TextLink arrow="left" onClick={close}>
          {backToEventsLabel}
        </TextLink>
        <StatementHeading level={1} eyebrow={view.eventName} lead={heading.lead} turn={heading.turn} />
        {view.items.length > 0 && (
          <p className={styles.count}>
            {formatGalleryCount(
              view.items.length,
              view.items.filter((item) => item.outcome === "completed").length,
              view.nextCursor !== null,
            )}
          </p>
        )}
        {view.running && (
          <p className={styles.live}>
            <span className={styles.badge}>{runningLabel}</span>
            <span>{runningLine}</span>
          </p>
        )}
      </header>
      <div className={styles.statuses}>
        <OperatorAssistanceAlert reason={running?.assistance?.reason ?? null} onReturn={close} />
        <InlineStatus>{firstPageLoading ? loadingFirstCopy : ""}</InlineStatus>
      </div>
      {(view.items.length > 0 || view.empty) && (
        <GuestCycleList
          mode="operator"
          cycles={cycles}
          hasMore={view.nextCursor !== null && !view.loadFailed}
          loading={view.loadingMore}
          pageKey={view.nextCursor}
          restoreFocus={restoreFocus}
          onOpen={(id) => {
            rememberOpened({ eventId: view.eventId, id, index: view.items.findIndex((item) => item.guestCycleId === id) });
            void send("OpenGuestCycle", { guestCycleId: id }, { key: "open-guest-cycle" });
          }}
          onDelete={(id) => {
            void send("DeleteGuestCycle", { guestCycleId: id }, { key: "delete-guest-cycle" });
          }}
          onLoadMore={() => {
            loadMore(false);
          }}
          onNearEnd={() => {
            loadMore(true);
          }}
        />
      )}
      <InlineStatus
        tone="alert"
        action={
          <TextLink size="meta" onClick={retry}>
            {tryAgainLabel}
          </TextLink>
        }
      >
        {view.loadFailed ? loadFailedLine : ""}
      </InlineStatus>
    </div>
  );
}
