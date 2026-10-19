import { resolve } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir } from "./resolutions.ts";
import { expectStageContains } from "./stage.ts";

// The Template and Photographs steps (M8 UI-1) through the fixture lane (`?fixture=<name>`): the brain-generated
// `guest-cycle-*` files, and the hand-written `guest-ui-*` ones (src/fixtures/guestCaptureFixtures.ts) for the states the
// brain's files don't write. Every Template, Camera, and Arrange state is checked at the four Guest Screen resolutions.
// Each must fit without scrolling, keep every control at its role's minimum target, show at most one primary action, and
// pass axe. The lane's stand-in brain answers every command `rejected notAvailable`, so these are rendering checks; the
// commands are unit-tested.

async function open(page: Page, fixture: string): Promise<void> {
  await page.goto(`/index.html?fixture=${fixture}`);
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "guest");
  await page.evaluate(() => document.fonts.ready);
}

async function expectNoScrolling(page: Page): Promise<void> {
  const overflow = await page.evaluate(() => {
    const root = document.documentElement;
    return {
      horizontal: root.scrollWidth > root.clientWidth,
      vertical: root.scrollHeight > root.clientHeight,
      body: document.body.scrollHeight > document.body.clientHeight,
      regions: [...document.querySelectorAll<HTMLElement>("main *")]
        .filter((element) => {
          const style = getComputedStyle(element);
          return (
            (/auto|scroll/.test(style.overflowX) && element.scrollWidth > element.clientWidth) ||
            (/auto|scroll/.test(style.overflowY) && element.scrollHeight > element.clientHeight)
          );
        })
        .map((element) => element.outerHTML.slice(0, 80)),
    };
  });
  expect(overflow).toEqual({ horizontal: false, vertical: false, body: false, regions: [] });
}

async function expectTargets(page: Page): Promise<void> {
  const small = await page.evaluate(() => {
    const issues: string[] = [];
    for (const element of document.querySelectorAll<HTMLElement>("main button, main [role='button'], main a[href]")) {
      const box = element.getBoundingClientRect();
      if (box.width === 0 || box.height === 0) {
        continue;
      }
      if (box.width < 64 - 0.5 || box.height < 64 - 0.5) {
        issues.push(`${element.getAttribute("aria-label") ?? element.textContent} ${String(Math.round(box.width))}x${String(Math.round(box.height))}`);
      }
    }
    return issues;
  });
  expect(small).toEqual([]);
}

/**
 * Guest text is sized for standing distance (D-4): action labels (buttons and links) at least 20 px and every other label
 * at least 18 px at the 1280 reference, and they only grow from there. Text hidden for assistive technology doesn't count.
 */
async function expectGuestTextSizes(page: Page): Promise<void> {
  const small = await page.evaluate(() => {
    const issues: string[] = [];
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
      const element = node.parentElement;
      if (
        element === null ||
        (node.textContent ?? "").trim() === "" ||
        element.closest("script, style") !== null ||
        (element.closest("[inert]") !== null && element.closest("[role='alertdialog']") === null)
      ) {
        continue;
      }
      const box = element.getBoundingClientRect();
      if (box.width <= 2 || box.height <= 2) {
        continue;
      }
      const size = parseFloat(getComputedStyle(element).fontSize);
      const action = element.closest("[data-fotohavn-action]") !== null;
      if (size < (action ? 20 : 18) - 0.01) {
        issues.push(`${(node.textContent ?? "").trim().slice(0, 24)} ${String(size)}px${action ? " (action)" : ""}`);
      }
    }
    return issues;
  });
  expect(small).toEqual([]);
}

/**
 * The Camera tab (S1): the band is all preview. The capture area takes the band's full height, centred, and lies inside the
 * camera's picture; the whole band is the one start control; the count pill sits at the area's foot, inside it; and the links
 * out sit right of the area, inside the band, never over it.
 */
async function expectCameraLayout(page: Page): Promise<void> {
  const layout = await page.evaluate(() => {
    const rect = (element: Element | null | undefined) => {
      if (element === null || element === undefined) {
        return null;
      }
      const { x, y, width, height } = element.getBoundingClientRect();
      return { x, y, width, height };
    };
    const tab = document.querySelector("[data-photographs-tab='camera']");
    return {
      band: rect(tab),
      area: rect(tab?.querySelector("[data-fotohavn-crop-guide]")),
      picture: rect(tab?.querySelector("[role='img']")),
      start: rect(tab?.querySelector("button[aria-label='Tap to start taking photographs']")),
      count: rect(tab?.querySelector("[role='progressbar']")?.parentElement),
      links: [...(tab?.querySelectorAll("[data-fotohavn-action='text']") ?? [])].map((link) => rect(link)),
    };
  });
  const { band, area, picture, start, count, links } = layout;
  expect(band).not.toBeNull();
  expect(area).not.toBeNull();
  if (band === null || area === null) {
    return;
  }
  // The capture area: the band's full height, centred.
  expect(Math.abs(area.height - band.height)).toBeLessThanOrEqual(1);
  expect(Math.abs(area.y - band.y)).toBeLessThanOrEqual(1);
  expect(Math.abs(area.x + area.width / 2 - (band.x + band.width / 2))).toBeLessThanOrEqual(1);
  expect(area.width).toBeLessThan(band.width);
  // The picture runs under the whole area, so the area shows only the photograph.
  if (picture !== null) {
    expect(picture.x).toBeLessThanOrEqual(area.x + 1);
    expect(picture.y).toBeLessThanOrEqual(area.y + 1);
    expect(picture.x + picture.width).toBeGreaterThanOrEqual(area.x + area.width - 1);
    expect(picture.y + picture.height).toBeGreaterThanOrEqual(area.y + area.height - 1);
  }
  // The whole band is the start control.
  expect(start).toEqual(band);
  // The count pill: inside the area, at its foot.
  expect(count).not.toBeNull();
  expect(count?.x).toBeGreaterThanOrEqual(area.x - 0.5);
  expect((count?.x ?? 0) + (count?.width ?? 0)).toBeLessThanOrEqual(area.x + area.width + 0.5);
  expect((count?.y ?? 0) + (count?.height ?? 0)).toBeLessThanOrEqual(area.y + area.height + 0.5);
  expect(count?.y).toBeGreaterThan(area.y + area.height / 2);
  // The links out: right of the area and inside the band.
  for (const link of links) {
    expect(link?.x).toBeGreaterThanOrEqual(area.x + area.width - 0.5);
    expect((link?.x ?? 0) + (link?.width ?? 0)).toBeLessThanOrEqual(band.x + band.width + 0.5);
    expect((link?.y ?? 0) + (link?.height ?? 0)).toBeLessThanOrEqual(band.y + band.height + 0.5);
  }
}

async function expectInsideViewport(page: Page, selector: string): Promise<void> {
  const viewport = page.viewportSize();
  for (const box of await page.locator(selector).evaluateAll((elements) =>
    elements.map((element) => {
      const { x, y, width, height } = element.getBoundingClientRect();
      return { x, y, width, height };
    }),
  )) {
    expect(box.x).toBeGreaterThanOrEqual(-0.5);
    expect(box.y).toBeGreaterThanOrEqual(-0.5);
    expect(box.x + box.width).toBeLessThanOrEqual((viewport?.width ?? 0) + 0.5);
    expect(box.y + box.height).toBeLessThanOrEqual((viewport?.height ?? 0) + 0.5);
  }
}

/** Every step renders inside the real cycle chrome: its band, then a stage that holds the step, all within the screen. */
async function expectStepFitsStage(page: Page): Promise<void> {
  const viewport = page.viewportSize();
  const chrome = await page.locator("[data-fotohavn-cycle-step]").evaluate((element) => {
    const band = element.querySelector("header")?.getBoundingClientRect();
    const stage = element.children[1]?.getBoundingClientRect();
    const step = element.children[1]?.firstElementChild?.getBoundingClientRect();
    return {
      children: element.children.length,
      bandBottom: band?.bottom ?? -1,
      stageTop: stage?.top ?? -1,
      stageBottom: stage?.bottom ?? -1,
      stepTop: step?.top ?? -1,
      stepBottom: step?.bottom ?? -1,
    };
  });
  expect(chrome.children).toBe(2);
  expect(chrome.bandBottom).toBeGreaterThan(0);
  expect(chrome.stageTop).toBeGreaterThanOrEqual(chrome.bandBottom - 0.5);
  expect(chrome.stepTop).toBeGreaterThanOrEqual(chrome.stageTop - 0.5);
  expect(chrome.stepBottom).toBeLessThanOrEqual(chrome.stageBottom + 0.5);
  expect(chrome.stageBottom).toBeLessThanOrEqual((viewport?.height ?? 0) + 0.5);
}

/**
 * The Phase Timer, when the brain sends one: in the top band, right of the step progress and left of the exit hold, clear of
 * both, with no MORE TIME and no START OVER anywhere; the step progress has no controls.
 */
async function expectBand(page: Page): Promise<void> {
  await expect(page.getByRole("button", { name: "MORE TIME" })).toHaveCount(0);
  await expect(page.getByRole("button", { name: "START OVER" })).toHaveCount(0);
  await expect(page.getByRole("list", { name: "Steps" }).getByRole("button")).toHaveCount(0);
  const timer = page.getByRole("timer");
  if ((await timer.count()) === 0) {
    return;
  }
  await expect(timer).toBeVisible();
  expect(await timer.evaluate((element) => element.closest("header") !== null)).toBe(true);
  const pill = await timer.boundingBox();
  const steps = await page.getByRole("list", { name: "Steps" }).boundingBox();
  const hold = await page.getByRole("button", { name: "Operator exit" }).boundingBox();
  expect(pill?.x).toBeGreaterThanOrEqual((steps?.x ?? 0) + (steps?.width ?? 0));
  expect((pill?.x ?? 0) + (pill?.width ?? 0)).toBeLessThanOrEqual((hold?.x ?? 0) - 0.5);
  expect(Math.abs((pill?.y ?? 0) + (pill?.height ?? 0) / 2 - ((hold?.y ?? 0) + (hold?.height ?? 0) / 2))).toBeLessThan(2);
}

async function finish(page: Page, fixture: string, suffix: string): Promise<void> {
  await expectNoScrolling(page);
  await expectStepFitsStage(page);
  await expectStageContains(page);
  await expectTargets(page);
  await expectGuestTextSizes(page);
  await expectBand(page);
  expect(await page.locator('main [data-fotohavn-action="primary"]').count()).toBeLessThanOrEqual(1);
  await page.screenshot({ path: resolve(screenshotDir, `${fixture}-${suffix}.png`) });
  const axe = await new AxeBuilder({ page }).analyze();
  expect(axe.violations).toEqual([]);
}

const templateFixtures = [
  ["guest-cycle-template", "Signature Strip"],
  ["guest-ui-template-signature", "Signature Strip"],
  ["guest-ui-template-panorama", "Panorama Four"],
  ["guest-ui-template-gingham", "Butter Gingham"],
  ["guest-ui-template-timer-low", "Signature Strip"],
] as const;

/** Each Camera fixture with the state its start control is in. */
const cameraFixtures = [
  ["guest-cycle-photographs-camera", "ready"],
  ["guest-cycle-photographs-camera-one-template", "ready"],
  ["guest-cycle-photographs-countdown", "running"],
  ["guest-cycle-photographs-pausing", "running"],
  ["guest-ui-camera-panorama", "ready"],
  ["guest-ui-camera-waiting", "waiting"],
  ["guest-ui-camera-partial", "ready"],
  ["guest-ui-camera-retaking", "running"],
  ["guest-ui-camera-retaking-ready", "ready"],
  ["guest-ui-camera-last-second", "running"],
  ["guest-ui-camera-timer-low", "ready"],
] as const;

const arrangeFixtures = [
  "guest-cycle-photographs-arrange",
  "guest-cycle-photographs-arrange-landscape",
  "guest-cycle-photographs-arrange-selected",
  "guest-cycle-photographs-arrange-moving",
  "guest-cycle-photographs-arrange-one-look",
  "guest-cycle-photographs-arrange-one-look-composing",
  "guest-cycle-photographs-retaking",
  "guest-ui-arrange-gingham",
  "guest-ui-arrange-polka",
  "guest-ui-arrange-timer-low",
  "guest-ui-arrange-partial",
  "guest-ui-arrange-moving-panorama",
  "guest-ui-arrange-retaking",
  "guest-ui-arrange-retaking-panorama",
] as const;

for (const resolution of resolutions) {
  const name = resolutionName(resolution);

  test.describe(`guest photographs at ${name}`, () => {
    test.use({ viewport: resolution });

    for (const [fixture, selected] of templateFixtures) {
      test(`Template, ${fixture.replace("guest-ui-template-", "")}`, async ({ page }) => {
        await open(page, fixture);
        await expect(page.locator('[data-fotohavn-surface="template"]')).toBeVisible();
        await expect(page.getByRole("heading", { level: 1, name: "Make a little Moment" })).toBeVisible();
        await expect(page.getByText("Choose your template.")).toBeVisible();
        await expect(page.getByRole("radio", { name: new RegExp(selected) })).toBeChecked();
        await expect(page.getByRole("button", { name: "CONTINUE TO PHOTOGRAPHS" })).toHaveAttribute("aria-disabled", "false");
        await expectInsideViewport(page, '[data-fotohavn-action="primary"]');
        await finish(page, fixture, name);
      });
    }

    for (const [fixture, state] of cameraFixtures) {
      test(`Camera, ${fixture.replace("guest-ui-camera-", "")}`, async ({ page }) => {
        await open(page, fixture);
        await expect(page.locator("[data-photographs-tab='camera']")).toBeVisible();
        // S1 shows no heading over the preview; the view keeps one for assistive technology.
        await expect(page.getByRole("heading", { level: 1, name: "Find your light." })).toBeAttached();
        await expect(page.getByText("Let this moment become a memory.")).toHaveCount(0);
        await expect(page.getByRole("progressbar", { name: "Photographs" })).toBeVisible();
        await expect(page.locator("[data-fotohavn-crop-guide]")).toHaveCount(1);
        const start = page.getByRole("button", { name: "Tap to start taking photographs" });
        await expect(start).toHaveAttribute("data-state", state);
        await expect(start).toHaveAttribute("aria-disabled", state === "ready" ? "false" : "true");
        if (state === "ready") {
          await expect(start.getByText("TAP TO START")).toBeVisible();
        } else if (state === "waiting") {
          await expect(start).toHaveAccessibleDescription("One moment. The camera is getting ready.");
          await expect(start.getByText("TAP TO START")).toHaveCount(0);
        } else {
          await expect(start.getByText("TAP TO START")).toHaveCount(0);
        }
        await expect(page.getByRole("button", { name: /stop/i })).toHaveCount(0);
        await expectInsideViewport(page, "[data-photographs-tab='camera'] [data-fotohavn-crop-guide]");
        await expectCameraLayout(page);
        await finish(page, fixture, name);
      });
    }

    test("Camera: the prompt sits at the capture area's centre on glass, and the band around the area is dimmed", async ({ page }) => {
      await open(page, "guest-cycle-photographs-camera");
      const area = await page.locator("[data-fotohavn-crop-guide]").boundingBox();
      const prompt = await page.getByText("TAP TO START").boundingBox();
      expect(Math.abs((prompt?.x ?? 0) + (prompt?.width ?? 0) / 2 - ((area?.x ?? 0) + (area?.width ?? 0) / 2))).toBeLessThan(2);
      const style = await page.locator("[data-fotohavn-crop-guide]").evaluate((element) => getComputedStyle(element).boxShadow);
      // Dark Walnut at 72% (Chromium writes a color-mix() result as color(srgb …) or rgba()).
      expect(style).toMatch(/(rgba\(45, 33, 27, 0\.72\)|color\(srgb 0\.17\d* 0\.12\d* 0\.10\d* \/ 0\.72\))/);
      const glass = await page.getByText("TAP TO START").evaluate((element) => getComputedStyle(element).backgroundColor);
      expect(glass).not.toMatch(/rgba?\(0, 0, 0(, 0)?\)/);
    });

    test("Countdown: the numeral sits top-centre of the capture area on a soft disc at weight 500", async ({ page }) => {
      await open(page, "guest-cycle-photographs-countdown");
      const area = await page.locator("[data-fotohavn-crop-guide]").boundingBox();
      const disc = page.locator("[data-photographs-tab='camera'] [class*='disc']");
      const box = await disc.boundingBox();
      expect(box).not.toBeNull();
      expect(Math.abs((box?.x ?? 0) + (box?.width ?? 0) / 2 - ((area?.x ?? 0) + (area?.width ?? 0) / 2))).toBeLessThan(2);
      expect(box?.y).toBeGreaterThanOrEqual((area?.y ?? 0) - 0.5);
      expect((box?.y ?? 0) + (box?.height ?? 0) / 2).toBeLessThan((area?.y ?? 0) + (area?.height ?? 0) / 2);
      expect(box?.width).toBeGreaterThanOrEqual(160 - 0.5);
      const style = await disc.evaluate((element) => {
        const numeral = element.firstElementChild;
        return {
          background: getComputedStyle(element).backgroundColor,
          weight: numeral === null ? "" : getComputedStyle(numeral).fontWeight,
          size: numeral === null ? 0 : parseFloat(getComputedStyle(numeral).fontSize),
        };
      });
      expect(style.weight).toBe("500");
      expect(style.size).toBeGreaterThanOrEqual(128 - 0.5);
      expect(style.background).not.toMatch(/rgba?\(0, 0, 0(, 0)?\)/);
      await expect(page.getByText("Look at the camera.")).toHaveCount(0);
      await expect(page.getByText("TAP TO START")).toHaveCount(0);
    });

    test("Countdown: the last second adds the cue, below the numeral and inside the capture area", async ({ page }) => {
      await open(page, "guest-ui-camera-last-second");
      const cue = page.getByText("Look at the camera.");
      await expect(cue).toBeVisible();
      const area = await page.locator("[data-fotohavn-crop-guide]").boundingBox();
      const disc = await page.locator("[data-photographs-tab='camera'] [class*='disc']").boundingBox();
      const cueBox = await cue.boundingBox();
      expect(cueBox?.y).toBeGreaterThanOrEqual((disc?.y ?? 0) + (disc?.height ?? 0) - 0.5);
      expect(Math.abs((cueBox?.x ?? 0) + (cueBox?.width ?? 0) / 2 - ((disc?.x ?? 0) + (disc?.width ?? 0) / 2))).toBeLessThan(2);
      expect(cueBox?.x).toBeGreaterThanOrEqual((area?.x ?? 0) - 0.5);
      expect((cueBox?.x ?? 0) + (cueBox?.width ?? 0)).toBeLessThanOrEqual((area?.x ?? 0) + (area?.width ?? 0) + 0.5);
      expect((cueBox?.y ?? 0) + (cueBox?.height ?? 0)).toBeLessThanOrEqual((area?.y ?? 0) + (area?.height ?? 0) + 0.5);
    });

    test("Camera, timer low: the header pill fills and nothing in the band moves", async ({ page }) => {
      const areaOf = async (fixture: string) => {
        await open(page, fixture);
        return page.locator("[data-fotohavn-crop-guide]").boundingBox();
      };
      const calm = await areaOf("guest-cycle-photographs-camera");
      const low = await areaOf("guest-ui-camera-timer-low");
      expect(low).toEqual(calm);
      const timer = page.getByRole("timer");
      await expect(timer).toHaveText("0:08");
      await expect(timer).toHaveAttribute("data-low", "true");
      const fill = await timer.evaluate((element) => getComputedStyle(element).backgroundColor);
      expect(fill).not.toMatch(/rgba?\(0, 0, 0(, 0)?\)/);
      await expect(page.locator("main [role='status']").filter({ hasText: "8 seconds left." })).toHaveCount(1);
    });

    test("Camera: the links out read as tidy blocks beside the capture area", async ({ page }) => {
      for (const [fixture, label] of [
        ["guest-ui-camera-partial", "SEE MY PHOTOGRAPHS"],
        ["guest-ui-camera-retaking-ready", "KEEP ORIGINAL"],
      ] as const) {
        await open(page, fixture);
        const link = page.getByRole("button", { name: label });
        await expect(link).toBeVisible();
        const text = await link.locator("span").first().boundingBox();
        const lineHeight = await link.evaluate((element) => parseFloat(getComputedStyle(element).lineHeight));
        const lines = Math.round((text?.height ?? 0) / lineHeight);
        expect(lines, label).toBeLessThanOrEqual(2);
        await expectCameraLayout(page);
      }
    });

    for (const fixture of arrangeFixtures) {
      test(`Arrange, ${fixture.replace("guest-ui-arrange-", "")}`, async ({ page }) => {
        await open(page, fixture);
        await expect(page.locator("[data-photographs-tab='arrange']")).toBeVisible();
        await expect(page.getByRole("heading", { level: 1, name: "Arrange your moment." })).toBeVisible();
        await expect(page.getByText("Drag to rearrange · Tap a photograph to edit")).toBeVisible();
        await expectInsideViewport(page, "[data-arrange-strip]");
        const strip = await page.locator("[data-arrange-strip] > div").first().boundingBox();
        expect(strip?.height).toBeGreaterThan(200);
        await finish(page, fixture, name);
      });
    }

    test("steps the brain marks skipped are left out of the progress and the rest renumbered", async ({ page }) => {
      await open(page, "guest-cycle-photographs-camera-one-template");
      await expect(page.getByRole("list", { name: "Steps" }).getByRole("listitem")).toHaveText(["01 Photographs", "02 Filters", "03 Keepsake"]);
      await open(page, "guest-cycle-photographs-arrange-one-look");
      await expect(page.getByRole("list", { name: "Steps" }).getByRole("listitem")).toHaveText(["01 Template, completed", "02 Photographs", "03 Keepsake"]);
      await expect(page.getByRole("button", { name: "FINISH MY STRIP" })).toHaveAttribute("aria-disabled", "false");
      await expect(page.getByRole("button", { name: "CONTINUE TO FILTERS" })).toHaveCount(0);
    });

    test("a Retake candidate waits in Arrange beside the original, as the brain sends it", async ({ page }) => {
      await open(page, "guest-cycle-photographs-retaking");
      await expect(page.locator("[data-photographs-tab='arrange']")).toBeVisible();
      await expect(page.getByText("Keep the new photograph for position 3, or try again.")).toBeVisible();
      await expect(page.getByRole("button", { name: "ACCEPT" })).toBeVisible();
      await expect(page.getByText("Original", { exact: true })).toBeVisible();
      await expect(page.getByText("New", { exact: true })).toBeVisible();
    });

    test("while the strip is prepared with Filters skipped, Arrange goes quiet", async ({ page }) => {
      await open(page, "guest-cycle-photographs-arrange-one-look-composing");
      await expect(page.getByText("Preparing your strip.")).toBeVisible();
      await expect(page.getByRole("button", { name: "FINISH MY STRIP" })).toHaveAttribute("aria-disabled", "true");
      await expect(page.getByRole("button", { name: "RETAKE" })).toHaveCount(0);
    });
  });
}

test.describe("guest photographs, reduced motion and forced colors", () => {
  test.use({ viewport: resolutions[0] });

  for (const fixture of [
    "guest-cycle-photographs-camera",
    "guest-cycle-photographs-countdown",
    "guest-cycle-photographs-pausing",
    "guest-cycle-photographs-arrange-moving",
    "guest-cycle-photographs-retaking",
  ]) {
    test(`${fixture} with reduced motion`, async ({ page }) => {
      await page.emulateMedia({ reducedMotion: "reduce" });
      await open(page, fixture);
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-reduced-motion.png`) });
    });

    test(`${fixture} in forced colors stays legible and within the screen`, async ({ page }) => {
      await page.emulateMedia({ forcedColors: "active" });
      await open(page, fixture);
      await expectNoScrolling(page);
      await page.screenshot({ path: resolve(screenshotDir, `${fixture}-forced-colors.png`) });
    });
  }
});
