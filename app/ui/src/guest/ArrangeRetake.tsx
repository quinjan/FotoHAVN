import type { CSSProperties } from "react";
import { PrimaryAction } from "../components/PrimaryAction/index.ts";
import { SecondaryAction } from "../components/SecondaryAction/index.ts";
import { TextLink } from "../components/TextLink/index.ts";
import type { PhotographsView, RetakeCandidateView } from "../protocol/generated.ts";
import styles from "./ArrangeRetake.module.css";
import { arrangeCopy } from "./captureCopy.ts";
import type { PhotographsCommands } from "./PhotographsCommands.ts";

/**
 * A Retake whose new photograph has arrived: the original and the new one side by side at the window's own proportion,
 * with `ACCEPT` as the one primary action, `TRY AGAIN`, and `KEEP ORIGINAL`. Until the guest decides, the original
 * stays in its window on the strip.
 */
export function ArrangeRetake({
  view,
  candidate,
  commands,
}: {
  readonly view: PhotographsView;
  readonly candidate: RetakeCandidateView;
  readonly commands: PhotographsCommands;
}) {
  const window = view.template.windows[candidate.index];
  const original = view.windows[candidate.index]?.imageUrl ?? null;
  const ratio = window === undefined ? 4 / 3 : window.width / window.height;
  const frame = { "--window-ratio": String(ratio) } as CSSProperties;
  return (
    <div className={styles.retake} data-retake-index={candidate.index}>
      <p className={styles.status}>{arrangeCopy.comparing(candidate.index + 1)}</p>
      <div className={styles.compare} style={frame}>
        <figure className={styles.figure}>
          {original !== null && <img className={styles.image} src={original} alt="" draggable={false} />}
          <figcaption className={styles.caption}>{arrangeCopy.original}</figcaption>
        </figure>
        <figure className={styles.figure} data-candidate="">
          <img className={styles.image} src={candidate.imageUrl} alt="" draggable={false} />
          <figcaption className={styles.caption}>{arrangeCopy.candidate}</figcaption>
        </figure>
      </div>
      <div className={styles.row}>
        <PrimaryAction onClick={commands.acceptRetake}>{arrangeCopy.accept}</PrimaryAction>
        <SecondaryAction onClick={commands.tryAgain}>{arrangeCopy.tryAgain}</SecondaryAction>
        <TextLink onClick={commands.keepOriginal}>{arrangeCopy.keepOriginal}</TextLink>
      </div>
    </div>
  );
}
