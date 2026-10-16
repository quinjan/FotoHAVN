import { performance } from "node:perf_hooks";
import type { Page } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { OperatorGalleryScreen, operatorGallery, touchSwipe } from "../../../src/gallery";
import { sendCommand, type HookClient } from "../../../src/hooks";
import { largeGallerySeed, seedLargeGalleryEvent } from "../../../src/seed";
import { EventListScreen } from "../../../src/screens/screens";

const cycles = 300;
const pageSize = 40;
const pageBudgetMs = 300;

function seed(dataDir: string): void {
  seedLargeGalleryEvent(dataDir, { cycles });
}

// One seeded Event with 300 ended Guest Cycles, written before the app starts: no Event is started, so no camera is used.
test.use({ appOptions: { seed } });

interface PageTiming {
  readonly page: number;
  readonly items: number;
  /** Raw milliseconds from the command send to the first diagnostics read showing the full page. */
  readonly ms: number;
  /** `ms` minus the hook-latency baseline: what the brain took, not the test hook's own round trip. */
  readonly adjustedMs: number;
}

/** The median of five plain `test.getDiagnostics` round trips: the hook's own latency, which every measured page includes at least once. */
async function hookLatencyMs(hooks: HookClient): Promise<number> {
  const samples: number[] = [];
  for (let sample = 0; sample < 5; sample += 1) {
    const start = performance.now();
    await hooks.getDiagnostics();
    samples.push(performance.now() - start);
  }
  samples.sort((left, right) => left - right);
  return samples[2] ?? 0;
}

/** Sends `command`, then reads the brain's diagnostics back to back (no pause beyond one round trip) until `ready` holds; returns the milliseconds that took. */
async function timed(
  hooks: HookClient,
  send: () => Promise<unknown>,
  ready: (gallery: NonNullable<Awaited<ReturnType<typeof operatorGallery>>>) => boolean,
): Promise<number> {
  const start = performance.now();
  await send();
  for (;;) {
    const gallery = await operatorGallery(hooks);
    if (gallery !== null && ready(gallery)) {
      return performance.now() - start;
    }
    if (performance.now() - start > 60_000) {
      throw new Error(`The gallery was not ready after 60 s: ${JSON.stringify(gallery)}`);
    }
  }
}

/** Opens the seeded Event's gallery and pages to its end through the brain's commands, timing every page. */
async function pageThrough(page: Page, hooks: HookClient, baselineMs: number): Promise<PageTiming[]> {
  const timings: PageTiming[] = [];
  const first = await timed(
    hooks,
    () => sendCommand(page, "OpenGallery", { eventId: largeGallerySeed.id }),
    (gallery) => gallery.eventId === largeGallerySeed.id && gallery.itemCount === pageSize && gallery.nextCursor !== null,
  );
  timings.push({ page: 1, items: pageSize, ms: first, adjustedMs: first - baselineMs });
  for (let next = 2; next <= Math.ceil(cycles / pageSize); next += 1) {
    const before = await operatorGallery(hooks);
    const cursor = before?.nextCursor ?? null;
    if (cursor === null) {
      throw new Error(`The gallery has no next page before page ${String(next)}.`);
    }
    const target = Math.min(next * pageSize, cycles);
    const ms = await timed(
      hooks,
      () => sendCommand(page, "LoadMoreGuestCycles", { cursor }),
      (gallery) => gallery.itemCount === target && gallery.nextCursor !== cursor,
    );
    timings.push({ page: next, items: target, ms, adjustedMs: ms - baselineMs });
  }
  return timings;
}

function describe(timings: readonly PageTiming[]): string {
  return timings
    .map((timing) => `page ${String(timing.page)} (${String(timing.items)} items): ${timing.ms.toFixed(0)} ms raw, ${timing.adjustedMs.toFixed(0)} ms adjusted`)
    .join("; ");
}

test(
  "on a seeded 300-cycle Data folder, pages load in under 300 ms each, and scrolling by touch reaches the end",
  { tag: ["@m9", "@timing"] },
  async ({ operatorPage, hooks }) => {
    test.setTimeout(300_000);
    const list = new EventListScreen(operatorPage);
    const gallery = new OperatorGalleryScreen(operatorPage);

    await expect.poll(async () => (await hooks.getDiagnostics()).storage.state, { timeout: 30_000 }).toBe("ready");
    await expect(list.row(largeGallerySeed.name)).toBeVisible();

    const baselineMs = await hookLatencyMs(hooks);
    console.log(`Hook latency baseline (median of 5 test.getDiagnostics round trips): ${baselineMs.toFixed(1)} ms`);

    let cold: PageTiming[] = [];
    await test.step("cold: the first pass makes the thumbnails (a strip warms its own when it commits during a real Event)", async () => {
      cold = await pageThrough(operatorPage, hooks, baselineMs);
      expect(await operatorGallery(hooks)).toMatchObject({ eventId: largeGallerySeed.id, itemCount: cycles, nextCursor: null });
      expect(cold.map((timing) => timing.items)).toEqual([40, 80, 120, 160, 200, 240, 280, 300]);
      console.log(`Cold pages of the seeded Event (thumbnails made on the way): ${describe(cold)}`);
    });

    let warm: PageTiming[] = [];
    await test.step("warm: after the gallery closes and opens again, every page loads in under 300 ms", async () => {
      expect(await sendCommand(operatorPage, "CloseGallery", {})).toEqual({ status: "accepted" });
      await expect.poll(() => operatorGallery(hooks)).toBeNull();
      warm = await pageThrough(operatorPage, hooks, baselineMs);
      console.log(`Warm pages of the seeded Event: ${describe(warm)}`);
      for (const timing of warm) {
        expect(
          timing.adjustedMs,
          `page ${String(timing.page)} took ${timing.ms.toFixed(0)} ms raw, ${timing.adjustedMs.toFixed(0)} ms after the ${baselineMs.toFixed(0)} ms hook baseline`,
        ).toBeLessThan(pageBudgetMs);
      }
      expect(await operatorGallery(hooks)).toMatchObject({ itemCount: cycles, nextCursor: null });
    });

    test.info().annotations.push(
      { type: "cold page timings", description: describe(cold) },
      { type: "warm page timings", description: describe(warm) },
    );

    await test.step("scrolling by touch reaches the end of the list, loading the pages on the way", async () => {
      expect(await sendCommand(operatorPage, "CloseGallery", {})).toEqual({ status: "accepted" });
      await expect(list.row(largeGallerySeed.name)).toBeVisible();
      await list.rowAction(largeGallerySeed.name, "GALLERY").click();
      await expect(gallery.main).toBeVisible();
      await expect(gallery.items).toHaveCount(pageSize);
      expect(await operatorGallery(hooks)).toMatchObject({ itemCount: pageSize });

      const { width, height } = await operatorPage.evaluate(() => ({ width: window.innerWidth, height: window.innerHeight }));
      const from = { x: Math.round(width / 2), y: Math.round(height * 0.85) };
      const to = { x: Math.round(width / 2), y: Math.round(height * 0.15) };

      await touchSwipe(operatorPage, from, to);
      await expect.poll(() => operatorPage.evaluate(() => window.scrollY), { message: "a swipe scrolls the page" }).toBeGreaterThan(0);

      const lastInView = () =>
        gallery.items.last().evaluate((element) => {
          const box = element.getBoundingClientRect();
          return box.top < window.innerHeight && box.bottom > 0;
        });
      let swipes = 1;
      for (; swipes < 400; swipes += 1) {
        const state = await operatorGallery(hooks);
        if (state?.nextCursor === null && state.itemCount === cycles && (await lastInView())) {
          break;
        }
        await touchSwipe(operatorPage, from, to);
      }
      console.log(`Touch scrolling reached the end of ${String(cycles)} Guest Cycles in ${String(swipes)} swipes.`);
      expect(await operatorGallery(hooks)).toMatchObject({ itemCount: cycles, nextCursor: null });
      await expect(gallery.items).toHaveCount(cycles);
      expect(await lastInView(), "the last Guest Cycle is in view").toBe(true);
    });

    await gallery.backToEvents.click();
    await expect(list.row(largeGallerySeed.name)).toBeVisible();
  },
);
