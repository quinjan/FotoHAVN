import type { CSSProperties, ReactNode, Ref } from "react";
import { outcomeLabels, unavailableCopy, type GalleryImage, type OperatorGuestCycle } from "../GuestCycleList/index.ts";
import { PrintObject } from "../PrintObject/index.ts";
import { cx } from "../shared/cx.ts";
import { TextLink } from "../TextLink/index.ts";
import styles from "./GuestCycleDetail.module.css";

export interface OperatorGuestCycleDetailProps {
  readonly mode: "operator";
  readonly cycle: OperatorGuestCycle;
  /** `OPEN FOLDER`: this Guest Cycle's folder. */
  readonly onOpenFolder: () => void;
  /** `DELETE`: the screen confirms first, naming the time and counting the files. */
  readonly onDelete: () => void;
  /** `CLOSE`, back to the list, when the screen shows the detail over it. */
  readonly onClose?: () => void;
  /** `OPEN FOLDER` is unavailable (the brain's `canOpenFolder`). Default true. */
  readonly canOpenFolder?: boolean;
  /** `DELETE` is unavailable while the brain counts the files for its confirmation (`canDelete`). Default true. */
  readonly canDelete?: boolean;
  readonly className?: string;
}

export interface GuestGuestCycleDetailProps {
  readonly mode: "guest";
  /** The completed Photo Strip: an image only. */
  readonly strip: GalleryImage;
  /** Its arranged Captures, in arrangement order; discarded Captures never reach the guest view. */
  readonly captures: readonly GalleryImage[];
  /** `CLOSE`, back to the guest gallery's row. */
  readonly onClose: () => void;
  /** `BACK TO START ←`, beside `CLOSE`, when the view offers it. */
  readonly onBack?: () => void;
  /** Receives the `CLOSE` button, so the view can put focus there when the detail opens. */
  readonly closeRef?: Ref<HTMLButtonElement>;
  readonly className?: string;
}

export type GuestCycleDetailProps = OperatorGuestCycleDetailProps | GuestGuestCycleDetailProps;

/**
 * One Guest Cycle from the Event Gallery, in two modes. `operator`: the Photo Strip as a resting print on the left,
 * its Captures in a Cream Paper media frame on the right, and the metadata with `OPEN FOLDER` and `DELETE` below.
 * `guest`: the Photo Strip lifted with the `print` shadow, its arranged Captures beside it, and `CLOSE`, with no
 * metadata or actions.
 */
export function GuestCycleDetail(props: GuestCycleDetailProps) {
  return props.mode === "guest" ? <GuestDetail {...props} /> : <OperatorDetail {...props} />;
}

/**
 * The strip and its media frame, sized together from the stage's height: the strip as tall as the stage, the Captures
 * in rows of two (one when there's only one) as large as the rest of the stage allows.
 */
function Pair({
  stripRatio,
  captures,
  empty,
  children,
}: {
  readonly stripRatio: number;
  readonly captures: readonly GalleryImage[];
  readonly empty?: string;
  readonly children: ReactNode;
}) {
  const columns = captures.length <= 1 ? 1 : 2;
  const first = captures[0];
  const geometry = {
    "--strip-ratio": String(stripRatio),
    "--capture-columns": String(columns),
    "--capture-rows": String(Math.max(1, Math.ceil(captures.length / columns))),
    "--capture-ratio": String(first === undefined ? 16 / 9 : first.width / first.height),
  } as CSSProperties;
  return (
    <div className={styles.stage}>
      <div className={styles.pair} style={geometry}>
        <div className={styles.stripCell}>{children}</div>
        <div className={styles.frame} data-tone="paper" data-count={captures.length}>
          {captures.length === 0 ? (
            empty !== undefined && <p className={styles.note}>{empty}</p>
          ) : (
            <ol className={styles.captures}>
              {captures.map((capture, index) => (
                // Captures carry no id of their own; their order is their arrangement.
                <li key={index}>
                  <img
                    className={styles.capture}
                    src={capture.src}
                    alt={capture.alt}
                    width={capture.width}
                    height={capture.height}
                    draggable={false}
                  />
                </li>
              ))}
            </ol>
          )}
        </div>
      </div>
    </div>
  );
}

function OperatorDetail({ cycle, onOpenFolder, onDelete, onClose, canOpenFolder = true, canDelete = true, className }: OperatorGuestCycleDetailProps) {
  const unavailable = cycle.unavailable === true;
  const facts: readonly (readonly [string, string])[] = [
    ["Time", cycle.time],
    ["Outcome", outcomeLabels[cycle.outcome]],
    ...(cycle.templateName === undefined ? [] : [["Template", cycle.templateName] as const]),
    ...(cycle.lookName === undefined ? [] : [["Look", cycle.lookName] as const]),
    ...(unavailable ? [] : [["Captures", String(cycle.captures.length)] as const]),
  ];
  const { strip } = cycle;
  return (
    <section className={cx(styles.operator, className)} aria-label={`Guest Cycle, ${cycle.time}`}>
      <Pair
        stripRatio={strip === null ? 1 / 3 : strip.width / strip.height}
        captures={cycle.captures}
        {...(unavailable ? {} : { empty: "No Captures" })}
      >
        {strip === null ? (
          unavailable ? (
            <div className={styles.missing} data-tone="paper" aria-hidden="true" />
          ) : (
            <p className={styles.missing} data-tone="paper">
              No Photo Strip
            </p>
          )
        ) : (
          <PrintObject {...strip} />
        )}
      </Pair>
      {unavailable && <p className={styles.unavailableNote}>{unavailableCopy}</p>}
      <dl className={styles.facts}>
        {facts.map(([term, value]) => (
          <div key={term} className={styles.fact}>
            <dt>{term}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
      <div className={styles.actions}>
        <TextLink disabled={!canOpenFolder} onClick={onOpenFolder}>
          OPEN FOLDER
        </TextLink>
        <TextLink disabled={!canDelete} onClick={onDelete}>
          DELETE
        </TextLink>
        {onClose !== undefined && <TextLink onClick={onClose}>CLOSE</TextLink>}
      </div>
    </section>
  );
}

function GuestDetail({ strip, captures, onClose, onBack, closeRef, className }: GuestGuestCycleDetailProps) {
  // Landscape strips (Panorama Four) turn a quarter counterclockwise, as every guest preview shows them.
  const rotated = strip.width > strip.height;
  return (
    <section className={cx(styles.guest, className)} data-tone="light" aria-label={strip.alt}>
      <Pair stripRatio={rotated ? strip.height / strip.width : strip.width / strip.height} captures={captures}>
        <PrintObject src={strip.src} alt={strip.alt} width={strip.width} height={strip.height} rotated={rotated} shadow="lifted" />
      </Pair>
      <div className={styles.guestActions}>
        <TextLink ref={closeRef} onClick={onClose}>
          CLOSE
        </TextLink>
        {onBack !== undefined && (
          <TextLink arrow="left" onClick={onBack}>
            BACK TO START
          </TextLink>
        )}
      </div>
    </section>
  );
}
