import type { Page } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, tap } from "../../../src/guest-cycle";

interface MotionReport {
  /** HTML elements whose computed transform isn't `none` (the live preview's mirror, `matrix(-1, 0, 0, 1, 0, 0)`, is a mirror and not motion). */
  transforms: string[];
  /** Running animations or transitions that move or scale something. */
  moving: string[];
  /** Running animations of any kind on the capture flash. */
  flashAnimations: number;
  flashes: { motion: string | null; opacity: string; animations: number }[];
}

/** Samples the Guest page every 40 ms and keeps what moves, and every capture flash it sees. */
async function startMotionSampler(page: Page): Promise<void> {
  await page.evaluate(() => {
    const report: MotionReport = { transforms: [], moving: [], flashAnimations: 0, flashes: [] };
    (window as unknown as { __motion: MotionReport; __flashKeys: WeakSet<Element> }).__motion = report;
    const seenFlashes = new WeakSet<Element>();
    const movingProperties = /^(transform|translate|scale|rotate)$/;
    window.setInterval(() => {
      for (const element of Array.from(document.querySelectorAll<HTMLElement>("main, main *")).filter((candidate) => candidate instanceof HTMLElement)) {
        const transform = getComputedStyle(element).transform;
        if (transform !== "none" && transform !== "matrix(-1, 0, 0, 1, 0, 0)") {
          report.transforms.push(`${element.tagName.toLowerCase()}.${String(element.className).split(" ")[0] ?? ""} ${transform}`);
        }
      }
      for (const animation of document.getAnimations()) {
        const target = animation.effect instanceof KeyframeEffect ? animation.effect.target : null;
        const properties = animation.effect instanceof KeyframeEffect ? animation.effect.getKeyframes().flatMap((frame) => Object.keys(frame)) : [];
        const isFlash = target instanceof HTMLElement && target.dataset["motion"] !== undefined && target.dataset["state"] === "flash";
        if (isFlash) {
          report.flashAnimations += 1;
        }
        if (properties.some((property) => movingProperties.test(property))) {
          report.moving.push(`${(animation as CSSAnimation).animationName ?? (animation as CSSTransition).transitionProperty ?? "animation"}: ${properties.join(",")}`);
        }
      }
      for (const flash of Array.from(document.querySelectorAll<HTMLElement>("[data-state='flash'][data-motion]"))) {
        if (!seenFlashes.has(flash)) {
          seenFlashes.add(flash);
          report.flashes.push({
            motion: flash.dataset["motion"] ?? null,
            opacity: getComputedStyle(flash).opacity,
            animations: flash.getAnimations().length,
          });
        }
      }
    }, 40);
  });
}

function motionReport(page: Page): Promise<MotionReport> {
  return page.evaluate(() => (window as unknown as { __motion: MotionReport }).__motion);
}

test(
  "with reduced motion, nothing moves or scales on the Guest Screen and the flash is a static change",
  { tag: ["@m8", "@camera"] },
  async ({ app, operatorPage, camera }) => {
    test.setTimeout(240_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Reduced motion", countdown: 3 });
    const guest = await run.expectStartReady();

    await guest.page.emulateMedia({ reducedMotion: "reduce" });
    expect(await guest.page.evaluate(() => matchMedia("(prefers-reduced-motion: reduce)").matches)).toBe(true);
    await startMotionSampler(guest.page);

    await test.step("every step is reached and a full sequence of four photographs is taken", async () => {
      await tap(guest.stepInside);
      await expect(guest.surface("template")).toBeVisible();
      await tap(guest.templateContinue);
      await run.waitForStep("photographs", "camera");
      await run.takePhotographsUntil(guest, 4);
      await run.waitForStep("photographs", "arrange");
      await tap(guest.continueToFilters);
      await run.waitForStep("filters");
      await tap(guest.finishStrip);
      await expect(guest.surface("keepsake")).toBeVisible({ timeout: 30_000 });
      await expect(guest.page.getByRole("img", { name: "Your Photo Strip" })).toBeVisible();
      // The reveal's animation would still be running a moment after the Keepsake appears.
      await guest.page.waitForTimeout(800);
    });

    await test.step("no element carried a transform, no animation moved or scaled anything, and the stage never faded", async () => {
      const report = await motionReport(guest.page);
      expect(report.transforms, "elements with a transform").toEqual([]);
      expect(report.moving, "moving animations").toEqual([]);
      expect(await guest.page.evaluate(() => document.getAnimations().length), "animations still running on the Keepsake").toBe(0);
    });

    await test.step("each of the four Captures flashed once, as a static change: no animation, a still tint", async () => {
      const report = await motionReport(guest.page);
      expect(report.flashes).toHaveLength(4);
      for (const flash of report.flashes) {
        expect(flash).toEqual({ motion: "static", opacity: "0.5", animations: 0 });
      }
      expect(report.flashAnimations).toBe(0);
    });

    await run.endEventFromOperator();
  },
);
