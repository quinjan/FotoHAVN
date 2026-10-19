import { useEffect, useId, useRef } from "react";
import { PrintObject } from "../PrintObject/index.ts";
import { cx } from "../shared/cx.ts";
import { StatementHeading } from "../StatementHeading/index.ts";
import { TextLink } from "../TextLink/index.ts";
import {
  loadingFirstCopy,
  loadingMoreCopy,
  outcomeLabels,
  templateAndLook,
  unavailableCopy,
  type GalleryImage,
  type GuestStrip,
  type OperatorGuestCycle,
} from "./galleryModel.ts";
import styles from "./GuestCycleList.module.css";
import { useNearEnd } from "./useNearEnd.ts";

export interface OperatorGuestCycleListProps {
  readonly mode: "operator";
  /** The Event's Guest Cycles loaded so far, newest first. */
  readonly cycles: readonly OperatorGuestCycle[];
  /** More Guest Cycles exist beyond those loaded; shows `SHOW MORE`. */
  readonly hasMore: boolean;
  /** A page is loading: `SHOW MORE` is unavailable, with the reason beside it. */
  readonly loading?: boolean;
  readonly onOpen: (id: string) => void;
  /** Asks to delete; the screen confirms first (it names the time and counts the files). */
  readonly onDelete: (id: string) => void;
  /** Asks for the next page (40 at a time): `SHOW MORE`, the keyboard's way to it. */
  readonly onLoadMore: () => void;
  /** Asks for the next page when the end of the list scrolls near; once for each `pageKey`. Without it, only `SHOW MORE` loads. */
  readonly onNearEnd?: () => void;
  /** Identifies the page being waited for (the brain's next cursor), so `onNearEnd` asks once for each. Default: the number of Guest Cycles loaded. */
  readonly pageKey?: string | null;
  /** Where focus lands when the list opens again from a Guest Cycle's detail: that Guest Cycle's `OPEN`, or the one now at its position. */
  readonly restoreFocus?: { readonly id: string; readonly index: number } | null;
  /** The list's accessible name. Default "Guest Cycles". */
  readonly label?: string;
  readonly className?: string;
}

export interface GuestGuestCycleListProps {
  readonly mode: "guest";
  /** The Active Event's completed Photo Strips, newest first: images only, no metadata. */
  readonly strips: readonly GuestStrip[];
  readonly onOpen: (id: string) => void;
  /** `BACK TO START ←`. */
  readonly onBack: () => void;
  /** More completed Photo Strips exist; scrolling to the end of the row asks for them. */
  readonly hasMore?: boolean;
  readonly onLoadMore?: () => void;
  /** Identifies the page being waited for (the brain's next cursor), so the row asks once for each. Default: the number of strips loaded. */
  readonly pageKey?: string | null;
  /** A page is on its way: the row says so at its end (or alone, while the first page loads). */
  readonly loading?: boolean;
  /** Where focus lands when the row opens again from a strip's detail: that strip's button (the row scrolls to show it). */
  readonly restoreFocus?: string | null;
  /** The heading's level. Default 1: the guest gallery is a whole Guest Screen view. */
  readonly headingLevel?: 1 | 2;
  readonly className?: string;
}

export type GuestCycleListProps = OperatorGuestCycleListProps | GuestGuestCycleListProps;

/**
 * The Event Gallery's list, in two modes. `operator`: Guest Cycles newest first, each Photo Strip as a print at its
 * real proportion (or its kept Captures, with "No Photo Strip"), a Meta caption, `OPEN` and `DELETE`, and paged
 * loading. `guest`: the guest gallery on Warm Ivory, "Look at *you.*", completed Photo Strips only, as resting prints in
 * a row that scrolls horizontally by touch, and `BACK TO START ←`; it takes a reduced model with no metadata.
 */
export function GuestCycleList(props: GuestCycleListProps) {
  return props.mode === "guest" ? <GuestGallery {...props} /> : <OperatorGallery {...props} />;
}

function CaptureSheet({ captures }: { readonly captures: readonly GalleryImage[] }) {
  return (
    <span className={styles.sheet} data-tone="paper">
      {captures.map((capture, index) => (
        // Captures carry no id of their own; their order is their arrangement.
        <img key={index} className={styles.capture} src={capture.src} alt="" width={capture.width} height={capture.height} draggable={false} />
      ))}
    </span>
  );
}

function UnavailableSheet() {
  return <span className={styles.unavailable} data-tone="paper" />;
}

function OperatorItem({
  cycle,
  onOpen,
  onDelete,
  openRef,
}: {
  readonly cycle: OperatorGuestCycle;
  readonly onOpen: (id: string) => void;
  readonly onDelete: (id: string) => void;
  readonly openRef: (element: HTMLButtonElement | null) => void;
}) {
  const captionId = useId();
  const open = () => {
    onOpen(cycle.id);
  };
  const names = templateAndLook(cycle);
  const unavailable = cycle.unavailable === true;
  const wide = cycle.strip !== null && cycle.strip.width > cycle.strip.height;
  return (
    <li className={styles.item} data-unavailable={unavailable || undefined} data-wide={wide || undefined}>
      {/* The print opens the Guest Cycle for pointer and touch; keyboard and screen readers use `OPEN` below. */}
      <button type="button" className={styles.open} tabIndex={-1} aria-hidden="true" onClick={open}>
        {unavailable ? (
          <UnavailableSheet />
        ) : cycle.strip === null ? (
          <CaptureSheet captures={cycle.captures} />
        ) : (
          <PrintObject {...cycle.strip} fit="scale" />
        )}
      </button>
      {/* Spaces between the lines, so the caption reads as words when it describes the actions; flex drops them. */}
      <p id={captionId} className={styles.caption}>
        <span className={styles.time}>{cycle.time}</span>{" "}
        <span>{outcomeLabels[cycle.outcome]}</span>
        {names !== null && (
          <>
            {" "}
            <span>{names}</span>
          </>
        )}
        {unavailable ? (
          <>
            {" "}
            <span>{unavailableCopy}</span>
          </>
        ) : (
          cycle.strip === null && (
            <>
              {" "}
              <span>No Photo Strip</span>
            </>
          )
        )}
      </p>

      <div className={styles.actions}>
        <TextLink ref={openRef} size="meta" onClick={open} aria-describedby={captionId}>
          OPEN
        </TextLink>
        <TextLink
          size="meta"
          onClick={() => {
            onDelete(cycle.id);
          }}
          aria-describedby={captionId}
        >
          DELETE
        </TextLink>
      </div>
    </li>
  );
}

function OperatorGallery({
  cycles,
  hasMore,
  loading = false,
  onOpen,
  onDelete,
  onLoadMore,
  onNearEnd,
  pageKey,
  restoreFocus = null,
  label = "Guest Cycles",
  className,
}: OperatorGuestCycleListProps) {
  const statusId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const endRef = useRef<HTMLSpanElement>(null);
  const openButtons = useRef(new Map<string, HTMLButtonElement>());
  const firstNew = useRef<number | null>(null);
  const deleted = useRef<{ readonly id: string; readonly index: number } | null>(null);

  // Focus goes to the OPEN action of this Guest Cycle, or of the one now in its place, or to the list itself when none is left.
  const focusNear = (id: string, index: number) => {
    const neighbor = cycles[Math.min(index, cycles.length - 1)]?.id;
    const target = openButtons.current.get(id) ?? (neighbor === undefined ? undefined : openButtons.current.get(neighbor));
    (target ?? rootRef.current)?.focus();
  };

  // Coming back from a Guest Cycle's detail, focus lands where the operator left.
  useEffect(() => {
    if (restoreFocus !== null) {
      focusNear(restoreFocus.id, restoreFocus.index);
    }
    // Once, when the list opens.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // A deleted Guest Cycle leaves the list at once; its opener is gone, so focus, if it was lost with it, moves to what took its place.
  useEffect(() => {
    const gone = deleted.current;
    if (gone !== null && !cycles.some((cycle) => cycle.id === gone.id)) {
      deleted.current = null;
      if (document.activeElement === null || document.activeElement === document.body) {
        focusNear(gone.id, gone.index);
      }
    }
    // `focusNear` reads the same `cycles`.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [cycles]);

  // When the last page arrives, `SHOW MORE` goes away; focus moves to the first Guest Cycle it brought.
  useEffect(() => {
    const index = firstNew.current;
    if (index === null || cycles.length <= index) {
      return;
    }
    firstNew.current = null;
    const id = cycles[index]?.id;
    if (!hasMore && id !== undefined) {
      openButtons.current.get(id)?.focus();
    }
  }, [cycles, hasMore]);

  // The end of the list coming near asks for the next page, once for each page; `SHOW MORE` stays for the keyboard.
  useNearEnd({
    enabled: hasMore && !loading && onNearEnd !== undefined,
    pageKey: pageKey ?? String(cycles.length),
    sentinel: endRef,
    root: null,
    margin: "0px 0px 600px 0px",
    onNearEnd: () => {
      onNearEnd?.();
    },
  });

  const loadMore = () => {
    firstNew.current = cycles.length;
    onLoadMore();
  };

  return (
    <div ref={rootRef} className={cx(styles.operator, className)} tabIndex={cycles.length === 0 ? -1 : undefined}>
      {cycles.length === 0 ? (
        <p className={styles.empty}>No photographs yet.</p>
      ) : (
        <ul className={styles.list} aria-label={label} aria-busy={loading || undefined}>
          {cycles.map((cycle, index) => (
            <OperatorItem
              key={cycle.id}
              cycle={cycle}
              onOpen={onOpen}
              onDelete={(id) => {
                deleted.current = { id, index };
                onDelete(id);
              }}
              openRef={(element) => {
                if (element === null) {
                  openButtons.current.delete(cycle.id);
                } else {
                  openButtons.current.set(cycle.id, element);
                }
              }}
            />
          ))}
        </ul>
      )}
      {hasMore && (
        <div className={styles.more}>
          <TextLink arrow="down" disabled={loading} onClick={loadMore} aria-describedby={statusId}>
            SHOW MORE
          </TextLink>
          <p id={statusId} className={styles.status} role="status">
            {loading ? loadingMoreCopy : ""}
          </p>
        </div>
      )}
      <span ref={endRef} className={styles.operatorEnd} aria-hidden="true" />
    </div>
  );
}

function GuestGallery({
  strips,
  onOpen,
  onBack,
  hasMore = false,
  onLoadMore,
  pageKey,
  loading = false,
  restoreFocus = null,
  headingLevel = 1,
  className,
}: GuestGuestCycleListProps) {
  const headingId = useId();
  const endRef = useRef<HTMLSpanElement>(null);
  const rowRef = useRef<HTMLDivElement>(null);
  const openButtons = useRef(new Map<string, HTMLButtonElement>());

  // Coming back from a strip's detail, focus lands on that strip.
  useEffect(() => {
    if (restoreFocus !== null) {
      openButtons.current.get(restoreFocus)?.focus();
    }
    // Once, when the row opens.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Reaching the end of the row asks for the next page, once for each; the guest never needs a control for it.
  useNearEnd({
    enabled: hasMore && !loading && onLoadMore !== undefined,
    pageKey: pageKey ?? String(strips.length),
    sentinel: endRef,
    root: rowRef,
    margin: "0px 100% 0px 0px",
    onNearEnd: () => {
      onLoadMore?.();
    },
  });

  return (
    <section className={cx(styles.guest, className)} data-tone="light" aria-labelledby={headingId}>
      <StatementHeading id={headingId} lead="Look at" turn="you." level={headingLevel} align="center" />
      <div ref={rowRef} className={styles.row} data-fotohavn-scroll-region="">
        {strips.length > 0 && (
          <ul className={styles.strips} aria-labelledby={headingId}>
            {strips.map((strip) => (
              <li key={strip.id} className={styles.stripItem}>
                <button
                  type="button"
                  className={styles.open}
                  ref={(element) => {
                    if (element === null) {
                      openButtons.current.delete(strip.id);
                    } else {
                      openButtons.current.set(strip.id, element);
                    }
                  }}
                  onClick={() => {
                    onOpen(strip.id);
                  }}
                >
                  <PrintObject
                    src={strip.src}
                    alt={strip.alt}
                    width={strip.width}
                    height={strip.height}
                    rotated={strip.width > strip.height}
                    fit="scale"
                  />
                </button>
              </li>
            ))}
          </ul>
        )}
        <span ref={endRef} className={styles.end} aria-hidden="true" />
        <p className={styles.loading} role="status" data-alone={strips.length === 0 || undefined}>
          {loading ? (strips.length === 0 ? loadingFirstCopy : loadingMoreCopy) : ""}
        </p>
      </div>
      <TextLink arrow="left" onClick={onBack}>
        BACK TO START
      </TextLink>
    </section>
  );
}
