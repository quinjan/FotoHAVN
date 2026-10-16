import { useEffect, useRef } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { PrimaryAction } from "../components/PrimaryAction/index.ts";
import { SecondaryAction } from "../components/SecondaryAction/index.ts";
import { StatementHeading } from "../components/StatementHeading/index.ts";
import { TextLink } from "../components/TextLink/index.ts";
import type { GuestCycleView, PhotographsView } from "../protocol/generated.ts";
import { ArrangeRetake } from "./ArrangeRetake.tsx";
import { ArrangeStrip } from "./ArrangeStrip.tsx";
import styles from "./ArrangeTab.module.css";
import { arrangeCopy } from "./captureCopy.ts";
import { composingLine } from "./finishCopy.ts";
import { usePhotographsCommands } from "./PhotographsCommands.ts";

/**
 * The Photographs step's Arrange tab (app/DESIGN.md, Photographs: Arrange): the strip as a print with the guest's
 * photographs in its windows, and beside it what the selection allows. Tap a photograph to select it, then `RETAKE` or
 * `MOVE` (and tap or key to a destination); a drag does the same swap but is never required. A Retake's new
 * photograph waits beside the original until it is accepted, tried again, or the original is kept. The brain decides
 * every rule; the strip only asks. When Filters is skipped, `FINISH MY STRIP →` composes the strip while the guest stays here:
 * the brain then refuses every command (`canReview` false with no sequence and no candidate), so the strip goes quiet and a
 * polite line says the strip is being prepared.
 */
export function ArrangeTab({ cycle, view }: { readonly cycle: GuestCycleView; readonly view: PhotographsView }) {
  const client = useBrainClient();
  const commands = usePhotographsCommands();
  const { selection, retakeCandidate, sequence } = view;
  const selectedIndex = selection.index;
  const hasEmptyWindow = view.windows.some((window) => window.captureId === null);
  const idle = !sequence.running;
  const preparing = idle && retakeCandidate === null && !view.canReview;
  const filtersSkipped = cycle.steps.some((entry) => entry.step === "filters" && entry.state === "skipped");
  const liveRef = useRef<HTMLSpanElement>(null);
  const previous = useRef({ candidate: retakeCandidate !== null, revision: view.revision, preparing });

  useEffect(() => {
    const before = previous.current;
    previous.current = { candidate: retakeCandidate !== null, revision: view.revision, preparing };
    const live = liveRef.current;
    if (live === null) {
      return;
    }
    if (preparing && !before.preparing) {
      live.textContent = composingLine;
    } else if (retakeCandidate !== null && !before.candidate) {
      live.textContent = arrangeCopy.announceCandidate;
    } else if (view.revision !== before.revision && selectedIndex !== null) {
      live.textContent = arrangeCopy.announceMoved(selectedIndex + 1);
    }
  }, [retakeCandidate, view.revision, selectedIndex, preparing]);

  return (
    <div
      className={styles.tab}
      data-photographs-tab="arrange"
      data-arrangement-revision={view.revision}
      data-selection-mode={selection.mode}
    >
      <div className={styles.mat} data-tone="paper">
        <ArrangeStrip
          template={view.template}
          windows={view.windows}
          mode={selection.mode}
          selected={selectedIndex}
          interactive={idle && retakeCandidate === null && !preparing}
          previewUrl={client.previewUrl}
          onSelect={commands.selectPosition}
          onMoveTo={commands.moveTo}
          onDragPlace={(source, captureId, target) => {
            commands.dragPlace(source, captureId, view.revision, target);
          }}
        />
      </div>
      <div className={styles.side}>
        <StatementHeading
          level={1}
          size="heading-sm"
          lead={arrangeCopy.lead}
          turn={arrangeCopy.turn}
          supporting={arrangeCopy.hint}
        />
        <div className={styles.panel}>
          {preparing ? (
            <p className={styles.status}>{composingLine}</p>
          ) : retakeCandidate !== null ? (
            <ArrangeRetake view={view} candidate={retakeCandidate} commands={commands} />
          ) : (
            <Selection view={view} commands={commands} />
          )}
        </div>
        {view.retainedCount > 0 && <p className={styles.retained}>{arrangeCopy.retained(view.retainedCount)}</p>}
        {retakeCandidate === null && (
          <div className={styles.actions}>
            {hasEmptyWindow && idle && selection.mode === "none" && (
              <TextLink arrow="left" onClick={commands.showCameraTab}>
                {arrangeCopy.takeMore}
              </TextLink>
            )}
            <PrimaryAction
              arrow="right"
              disabled={!view.canContinue}
              onClick={() => {
                commands.goToStep("filters");
              }}
            >
              {filtersSkipped ? arrangeCopy.finish : arrangeCopy.continue}
            </PrimaryAction>
          </div>
        )}
        <span ref={liveRef} className={styles.visuallyHidden} aria-live="polite" />
      </div>
    </div>
  );
}

function Selection({ view, commands }: { readonly view: PhotographsView; readonly commands: ReturnType<typeof usePhotographsCommands> }) {
  const { selection } = view;
  const index = selection.index;
  if (selection.mode === "moving" && index !== null) {
    return (
      <>
        <p className={styles.status}>{arrangeCopy.moving(index + 1)}</p>
        <div className={styles.row}>
          <SecondaryAction onClick={commands.cancelMove}>{arrangeCopy.cancelMove}</SecondaryAction>
        </div>
      </>
    );
  }
  if (selection.mode === "selected" && index !== null) {
    return (
      <>
        <p className={styles.status}>{arrangeCopy.selected(index + 1)}</p>
        <div className={styles.row}>
          <SecondaryAction
            onClick={() => {
              commands.beginRetake(index);
            }}
          >
            {arrangeCopy.retake}
          </SecondaryAction>
          <SecondaryAction
            onClick={() => {
              commands.beginMove(index);
            }}
          >
            {arrangeCopy.move}
          </SecondaryAction>
        </div>
      </>
    );
  }
  if (selection.mode === "retaking") {
    return (
      <div className={styles.row}>
        <TextLink onClick={commands.keepOriginal}>{arrangeCopy.keepOriginal}</TextLink>
      </div>
    );
  }
  return view.canContinue ? null : <p className={styles.status}>{arrangeCopy.emptyHint}</p>;
}
