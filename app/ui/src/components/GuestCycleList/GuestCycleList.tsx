import { useEffect, useId, useRef } from "react";
import { PrintObject } from "../PrintObject/index.ts";
import { cx } from "../shared/cx.ts";
import { StatementHeading } from "../StatementHeading/index.ts";
import { TextLink } from "../TextLink/index.ts";
import { outcomeLabels, type GalleryImage, type GuestStrip, type OperatorGuestCycle } from "./galleryModel.ts";
import styles from "./GuestCycleList.module.css";

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
  /** Asks for the next page (40 at a time). */
  readonly onLoadMore: () => void;
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
  return (
    <li className={styles.item}>
      {/* The print opens the Guest Cycle for pointer and touch; keyboard and screen readers use `OPEN` below. */}
      <button type="button" className={styles.open} tabIndex={-1} aria-hidden="true" onClick={open}>
        {cycle.strip === null ? <CaptureSheet captures={cycle.captures} /> : <PrintObject {...cycle.strip} fit="scale" />}
      </button>
      {/* Spaces between the lines, so the caption reads as words when it describes the actions; flex drops them. */}
      <p id={captionId} className={styles.caption}>
        <span className={styles.time}>{cycle.time}</span>{" "}
        <span>{outcomeLabels[cycle.outcome]}</span>{" "}
        <span>{cycle.lookName === undefined ? cycle.templateName : `${cycle.templateName} · ${cycle.lookName}`}</span>
        {cycle.strip === null && (
          <>
            {" "}
            <span>No Photo Strip</span>
          </>
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
  label = "Guest Cycles",
  className,
}: OperatorGuestCycleListProps) {
  const statusId = useId();
  const openButtons = useRef(new Map<string, HTMLButtonElement>());
  const firstNew = useRef<number | null>(null);

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

  const loadMore = () => {
    firstNew.current = cycles.length;
    onLoadMore();
  };

  return (
    <div className={cx(styles.operator, className)}>
      {cycles.length === 0 ? (
        <p className={styles.empty}>No photographs yet.</p>
      ) : (
        <ul className={styles.list} aria-label={label} aria-busy={loading || undefined}>
          {cycles.map((cycle) => (
            <OperatorItem
              key={cycle.id}
              cycle={cycle}
              onOpen={onOpen}
              onDelete={onDelete}
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
            {loading ? "Loading more photographs." : ""}
          </p>
        </div>
      )}
    </div>
  );
}

function GuestGallery({ strips, onOpen, onBack, hasMore = false, onLoadMore, headingLevel = 1, className }: GuestGuestCycleListProps) {
  const headingId = useId();
  const endRef = useRef<HTMLSpanElement>(null);
  const rowRef = useRef<HTMLDivElement>(null);

  // Reaching the end of the row asks for the next page; the guest never needs a control for it.
  useEffect(() => {
    const end = endRef.current;
    if (!hasMore || onLoadMore === undefined || end === null || typeof IntersectionObserver === "undefined") {
      return;
    }
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries.some((entry) => entry.isIntersecting)) {
          onLoadMore();
        }
      },
      { root: rowRef.current },
    );
    observer.observe(end);
    return () => {
      observer.disconnect();
    };
  }, [hasMore, onLoadMore, strips.length]);

  return (
    <section className={cx(styles.guest, className)} data-tone="light" aria-labelledby={headingId}>
      <StatementHeading id={headingId} lead="Look at" turn="you." level={headingLevel} align="center" />
      <div ref={rowRef} className={styles.row} data-fotohavn-scroll-region="">
        <ul className={styles.strips} aria-labelledby={headingId}>
          {strips.map((strip) => (
            <li key={strip.id} className={styles.stripItem}>
              <button
                type="button"
                className={styles.open}
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
        <span ref={endRef} className={styles.end} aria-hidden="true" />
      </div>
      <TextLink arrow="left" onClick={onBack}>
        BACK TO START
      </TextLink>
    </section>
  );
}
