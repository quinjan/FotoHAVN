import type { CSSProperties } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { InlineStatus } from "../components/InlineStatus/index.ts";
import { OptionList, type OptionListItem } from "../components/OptionList/index.ts";
import { PrimaryAction } from "../components/PrimaryAction/index.ts";
import { cx } from "../components/shared/cx.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import type { FiltersView, GuestCycleView } from "../protocol/generated.ts";
import {
  compositionFailedLine,
  composingLine,
  filtersCopy,
  finishStripLabel,
  looksLabel,
  retryCompositionLabel,
} from "./finishCopy.ts";
import { FiltersStripPreview } from "./FiltersStripPreview.tsx";
import styles from "./FiltersStep.module.css";

/** Past this many Looks the list takes two columns and drops the descriptions from sight, so the step still fits without scrolling. */
const denseFrom = 5;

/**
 * The Filters step (plan section 7, Steps; app/DESIGN.md, Guest Screen): the strip preview as a print object on a Cream Paper
 * mat on the left, and on the right the heading, the option list of offered Looks with thumbnails, and `FINISH MY STRIP →`
 * as the one primary action. While the brain composes, the strip shown stays and a polite line says so. When a composition
 * fails (Q25) the previous strip stays, the inline error shows, and `TRY AGAIN` (`RetryComposition`) takes the primary
 * action's place: `FinishStrip` isn't valid while a failure shows. Every fact comes from the presentation; the UI holds no rule.
 */
export function FiltersStep({ view }: { readonly cycle: GuestCycleView; readonly view: FiltersView }) {
  const client = useBrainClient();
  const url = (key: string | null): string | null => (key === null || client.previewUrl === undefined ? null : client.previewUrl(key));
  const dense = view.looks.length >= denseFrom;
  const items: readonly OptionListItem[] = view.looks.map((look) => {
    const thumbnail = url(look.thumbnailKey);
    return {
      value: look.id,
      title: look.name,
      description: dense ? <span className={styles.quiet}>{look.description}</span> : look.description,
      thumbnail: thumbnail === null ? <span /> : <img src={thumbnail} alt="" />,
    };
  });
  const statusLine = view.compositionFailed ? compositionFailedLine : view.composing ? composingLine : "";

  return (
    <div className={styles.step} data-selected-look={view.selectedLookId} data-composing={view.composing || undefined}>
      <div className={styles.mat} data-tone="paper">
        <FiltersStripPreview
          src={url(view.stripPreviewKey)}
          composing={view.composing}
          width={view.width}
          height={view.height}
          landscape={view.landscape}
        />
      </div>
      <div className={styles.options}>
        <StatementHeading level={1} size="heading" {...filtersCopy} />
        <div
          className={styles.looks}
          data-dense={dense || undefined}
          style={{ "--look-rows": String(Math.ceil(view.looks.length / 2)) } as CSSProperties}
        >
          <OptionList
            variant="radio"
            label={looksLabel}
            hideLabel
            items={items}
            value={view.selectedLookId}
            onChange={(lookId) => {
              void client.sendCommand("SelectLook", { lookId });
            }}
          />
        </div>
        <div className={styles.footer}>
          <InlineStatus tone={view.compositionFailed ? "alert" : "neutral"} className={cx(styles.status)}>
            {statusLine}
          </InlineStatus>
          {view.compositionFailed ? (
            <PrimaryAction
              onClick={() => {
                void client.sendCommand("RetryComposition", {});
              }}
            >
              {retryCompositionLabel}
            </PrimaryAction>
          ) : (
            <PrimaryAction
              arrow="right"
              disabled={!view.canFinish}
              onClick={() => {
                void client.sendCommand("FinishStrip", {});
              }}
            >
              {finishStripLabel}
            </PrimaryAction>
          )}
        </div>
      </div>
    </div>
  );
}
