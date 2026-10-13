import type { CSSProperties, ReactNode } from "react";
import { outcomeLabels, type GalleryImage, type GuestStrip, type OperatorGuestCycle } from "../GuestCycleList/index.ts";
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
  readonly className?: string;
}

export interface GuestGuestCycleDetailProps {
  readonly mode: "guest";
  /** The completed Photo Strip: an image only. */
  readonly strip: GuestStrip;
  /** Its arranged Captures, in arrangement order; discarded Captures never reach the guest view. */
  readonly captures: readonly GalleryImage[];
  /** `CLOSE`, back to the guest gallery's row. */
  readonly onClose: () => void;
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

function OperatorDetail({ cycle, onOpenFolder, onDelete, onClose, className }: OperatorGuestCycleDetailProps) {
  const facts: readonly (readonly [string, string])[] = [
    ["Time", cycle.time],
    ["Outcome", outcomeLabels[cycle.outcome]],
    ["Template", cycle.templateName],
    ...(cycle.lookName === undefined ? [] : [["Look", cycle.lookName] as const]),
    ["Captures", String(cycle.captures.length)],
  ];
  const { strip } = cycle;
  return (
    <section className={cx(styles.operator, className)} aria-label={`Guest Cycle, ${cycle.time}`}>
      <Pair stripRatio={strip === null ? 1 / 3 : strip.width / strip.height} captures={cycle.captures} empty="No Captures">
        {strip === null ? (
          <p className={styles.missing} data-tone="paper">
            No Photo Strip
          </p>
        ) : (
          <PrintObject {...strip} />
        )}
      </Pair>
      <dl className={styles.facts}>
        {facts.map(([term, value]) => (
          <div key={term} className={styles.fact}>
            <dt>{term}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
      <div className={styles.actions}>
        <TextLink onClick={onOpenFolder}>OPEN FOLDER</TextLink>
        <TextLink onClick={onDelete}>DELETE</TextLink>
        {onClose !== undefined && <TextLink onClick={onClose}>CLOSE</TextLink>}
      </div>
    </section>
  );
}

function GuestDetail({ strip, captures, onClose, className }: GuestGuestCycleDetailProps) {
  // Landscape strips (Panorama Four) turn a quarter counterclockwise, as every guest preview shows them.
  const rotated = strip.width > strip.height;
  return (
    <section className={cx(styles.guest, className)} data-tone="light" aria-label={strip.alt}>
      <Pair stripRatio={rotated ? strip.height / strip.width : strip.width / strip.height} captures={captures}>
        <PrintObject src={strip.src} alt={strip.alt} width={strip.width} height={strip.height} rotated={rotated} shadow="lifted" />
      </Pair>
      <TextLink onClick={onClose}>CLOSE</TextLink>
    </section>
  );
}
