/** The most rounds of "wait for the animations that are running" before the document is called ready anyway. */
const maxSettleRounds = 10;

function nextFrame(): Promise<void> {
  return new Promise((resolve) => {
    if (typeof requestAnimationFrame === "function") {
      requestAnimationFrame(() => {
        resolve();
      });
    } else {
      setTimeout(resolve, 0);
    }
  });
}

/** Finite animations and transitions still running; looping ones (a spinner, a pulse) never finish and are skipped, and so is a delayed reveal, which only waits. */
function unfinishedAnimations(): Animation[] {
  const getAnimations = (document as Partial<Document>).getAnimations;
  if (typeof getAnimations !== "function") {
    return [];
  }
  return getAnimations.call(document).filter((animation) => {
    const iterations = animation.effect?.getComputedTiming().iterations ?? 1;
    const target = (animation.effect as Partial<KeyframeEffect> | null)?.target;
    return animation.playState !== "finished" && Number.isFinite(iterations) && target?.closest("[data-delayed-reveal]") == null;
  });
}

/**
 * Resolves when a freshly mounted fixture holds still, so a screenshot or an axe run reads the same page every time:
 * the web fonts have loaded, two animation frames have painted the mounted tree, and every finite animation or
 * transition has finished. Both fixture lanes (Chromium in app/ui and WebView2 in app/e2e) wait on the
 * `data-fotohavn-gallery-ready` attribute this gates, so they capture an identical settled state.
 */
export async function settleFixtureDocument(): Promise<void> {
  const fonts = (document as Partial<Document>).fonts;
  await (fonts?.ready ?? Promise.resolve());
  await nextFrame();
  await nextFrame();
  for (let round = 0; round < maxSettleRounds; round += 1) {
    const pending = unfinishedAnimations();
    if (pending.length === 0) {
      return;
    }
    await Promise.allSettled(pending.map((animation) => animation.finished));
    await nextFrame();
  }
}
