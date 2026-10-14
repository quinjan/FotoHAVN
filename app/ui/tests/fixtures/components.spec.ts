import { readdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir, type Resolution } from "./resolutions.ts";

// The component fixture lane: every fixture the gallery discovers (src/components/**/*.fixtures.tsx), rendered
// alone at `?gallery=<id>` in the E2E bundle. Test titles come from the fixture files on disk, so a new component's
// fixtures are covered with no edit here; the fixture list comes from the gallery's own manifest (`?gallery=manifest`),
// and a guard test checks the two agree. Screenshots: ../output/fixtures/components/<id>/<w>x<h>.png.

interface ManifestFixture {
  readonly id: string;
  readonly title: string;
  readonly surface: "light" | "dark";
  readonly role: "guest" | "operator";
  readonly layout: "centered" | "fill";
  readonly source: string;
}

const srcDir = resolve(dirname(fileURLToPath(import.meta.url)), "../../src");
const componentScreenshots = resolve(screenshotDir, "components");

const sources = readdirSync(resolve(srcDir, "components"), { recursive: true, encoding: "utf8" })
  .map((path) => `components/${path.replaceAll("\\", "/")}`)
  .filter((path) => path.endsWith(".fixtures.tsx"))
  .sort();

function componentName(source: string): string {
  return source.replace(/^.*\//, "").replace(/\.fixtures\.tsx$/, "");
}

async function readManifest(page: Page): Promise<readonly ManifestFixture[]> {
  await page.goto("/index.html?gallery=manifest");
  await expect(page.locator('[data-fotohavn-gallery-error="true"]')).toHaveCount(0);
  const text = await page.locator("#fotohavn-gallery-manifest").textContent();
  return (JSON.parse(text ?? "{}") as { fixtures: ManifestFixture[] }).fixtures;
}

/** Each fixture a multi-fixture test walks adds this much to that test's timeout (open, checks, screenshot, axe). */
const perFixtureTimeoutMs = 8_000;
/** The manifest read that starts every multi-fixture test (one page load) gets its own allowance on top of the base. */
const manifestTimeoutMs = 30_000;

async function fixturesFrom(page: Page, source: string): Promise<readonly ManifestFixture[]> {
  // One test walks every fixture of a component, so its budget is the base plus the manifest read plus a share per
  // fixture, instead of the same 30 s for one fixture and for fourteen. The manifest allowance is granted before the
  // read, since a slow machine is slowest on the first page load.
  test.setTimeout(test.info().timeout + manifestTimeoutMs);
  const fixtures = (await readManifest(page)).filter((fixture) => fixture.source === source);
  expect(fixtures.length, `${source} has fixtures in the gallery manifest`).toBeGreaterThan(0);
  test.setTimeout(test.info().timeout + fixtures.length * perFixtureTimeoutMs);
  return fixtures;
}

/** Opens one fixture alone and waits for the gallery's settle marker (fonts loaded, painted, finite animations finished). */
async function openFixture(page: Page, fixture: ManifestFixture): Promise<void> {
  await page.goto(`/index.html?gallery=${encodeURIComponent(fixture.id)}`);
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-gallery-ready", "true");
  await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", fixture.role);
  await expect(page.locator(`main[data-fotohavn-fixture="${fixture.id}"]`)).toBeVisible();
}

/** No page scrollbars for any fixture; guest fixtures also have no scrolling region except a marked strip row. */
async function expectNoOverflow(page: Page, fixture: ManifestFixture): Promise<void> {
  const overflow = await page.evaluate((guest) => {
    const root = document.documentElement;
    const scrollers = guest
      ? [...document.querySelectorAll<HTMLElement>("main *")]
          .filter((element) => element.closest("[data-fotohavn-scroll-region]") === null)
          .filter((element) => {
            const style = getComputedStyle(element);
            const scrollsX = /auto|scroll/.test(style.overflowX) && element.scrollWidth > element.clientWidth;
            const scrollsY = /auto|scroll/.test(style.overflowY) && element.scrollHeight > element.clientHeight;
            return scrollsX || scrollsY;
          })
          .map((element) => element.outerHTML.slice(0, 80))
      : [];
    return {
      horizontal: root.scrollWidth > root.clientWidth,
      vertical: root.scrollHeight > root.clientHeight,
      scrollers,
    };
  }, fixture.role === "guest");
  expect(overflow, `${fixture.id} overflows`).toEqual({ horizontal: false, vertical: false, scrollers: [] });
}

/** Every visible control meets the role's minimum target: 64 × 64 on the Guest Screen, 48 × 48 on the Operator Screen. */
async function expectTargets(page: Page, fixture: ManifestFixture): Promise<void> {
  const minimum = fixture.role === "guest" ? 64 : 48;
  const small = await page.evaluate((min) => {
    const controls = document.querySelectorAll<HTMLElement>(
      "main button, main a[href], main input, main select, main textarea, main [role='button'], main [tabindex]:not([tabindex='-1'])",
    );
    return [...controls]
      .map((control) => ({ control, box: control.getBoundingClientRect() }))
      .filter(({ box }) => box.width > 0 && box.height > 0)
      .filter(({ box }) => box.width + 0.5 < min || box.height + 0.5 < min)
      .map(({ control, box }) => `${control.textContent.trim()} ${String(Math.round(box.width))}x${String(Math.round(box.height))}`);
  }, minimum);
  expect(small, `${fixture.id} controls smaller than ${String(minimum)} x ${String(minimum)}`).toEqual([]);
}

async function expectAtMostOnePrimary(page: Page, fixture: ManifestFixture): Promise<void> {
  expect(await page.locator('[data-fotohavn-action="primary"]').count(), `${fixture.id} primary actions`).toBeLessThanOrEqual(1);
}

async function expectAccessible(page: Page, fixture: ManifestFixture, { forcedColors = false } = {}): Promise<void> {
  // A component fixture is not a page, so it needn't hold an <h1>; every other axe rule applies. Under forced colors
  // the system palette owns contrast, and axe's color-contrast reads the authored colors against the forced
  // background, so that run swaps it for expectForcedColorsHonored.
  const axe = await new AxeBuilder({ page })
    .disableRules(["page-has-heading-one", ...(forcedColors ? ["color-contrast"] : [])])
    .analyze();
  expect(axe.violations, `${fixture.id} axe violations`).toEqual([]);
}

/**
 * High Contrast keeps every control and state visible: all text in the fixture takes a system color (nothing opts out
 * with forced-color-adjust), and bordered actions keep their border, so their bounds show without the fill.
 */
async function expectForcedColorsHonored(page: Page, fixture: ManifestFixture): Promise<void> {
  const problems = await page.evaluate(() => {
    const probe = document.createElement("span");
    document.body.append(probe);
    const system = new Set(
      ["CanvasText", "LinkText", "VisitedText", "ButtonText", "GrayText", "HighlightText", "Highlight", "Mark", "MarkText"].map((name) => {
        probe.style.color = name;
        return getComputedStyle(probe).color;
      }),
    );
    probe.remove();
    const issues: string[] = [];
    for (const element of document.querySelectorAll<HTMLElement>("main *")) {
      const style = getComputedStyle(element);
      if (element.childNodes.length > 0 && !system.has(style.color)) {
        issues.push(`${element.tagName} color ${style.color}`);
      }
      if (style.forcedColorAdjust === "none") {
        issues.push(`${element.tagName} opts out of forced colors`);
      }
    }
    for (const action of document.querySelectorAll<HTMLElement>('main [data-fotohavn-action="primary"], main [data-fotohavn-action="secondary"]')) {
      if (parseFloat(getComputedStyle(action).borderTopWidth) < 1) {
        issues.push(`${action.textContent} has no border`);
      }
    }
    return issues;
  });
  expect(problems, `${fixture.id} under forced colors`).toEqual([]);
}

function screenshotPath(fixture: ManifestFixture, name: string): string {
  return resolve(componentScreenshots, fixture.id, `${name}.png`);
}

test("the gallery manifest covers every fixtures file, with unique ids", async ({ page }) => {
  const manifest = await readManifest(page);
  expect([...new Set(manifest.map((fixture) => fixture.source))].sort()).toEqual(sources);
  expect(new Set(manifest.map((fixture) => fixture.id)).size).toBe(manifest.length);
});

test("fonts and tokens come from the bundle", async ({ page, baseURL }) => {
  const fontRequests: string[] = [];
  page.on("request", (request) => {
    if (request.resourceType() === "font") {
      fontRequests.push(request.url());
    }
  });
  await page.setViewportSize({ width: 1280, height: 720 });
  await openFixture(page, {
    id: "statement-heading-start",
    title: "",
    surface: "light",
    role: "guest",
    layout: "centered",
    source: "",
  });

  const preloads = await page.locator('link[rel="preload"][as="font"]').evaluateAll((links) =>
    links.map((link) => (link as HTMLLinkElement).href),
  );
  expect(preloads).toHaveLength(5);
  const loaded = await page.evaluate(() =>
    [...document.fonts].filter((face) => face.status === "loaded").map((face) => `${face.family.replaceAll('"', "")} ${face.weight} ${face.style}`),
  );
  expect(loaded).toEqual(
    expect.arrayContaining(["Cormorant Garamond 400 normal", "Cormorant Garamond 400 italic", "Manrope 400 normal", "Manrope 600 normal"]),
  );
  expect(fontRequests.length).toBeGreaterThan(0);
  for (const url of [...fontRequests, ...preloads]) {
    expect(url.startsWith(`${baseURL ?? ""}/assets/`), url).toBe(true);
    expect(url).toMatch(/\.woff2$/);
  }

  const computed = await page.evaluate(() => {
    const body = getComputedStyle(document.body);
    const heading = getComputedStyle(document.querySelector("h1") ?? document.body);
    return { background: body.backgroundColor, ink: body.color, headingFont: heading.fontFamily, headingSize: heading.fontSize };
  });
  expect(computed).toEqual({
    background: "rgb(251, 248, 242)",
    ink: "rgb(30, 26, 23)",
    headingFont: '"Cormorant Garamond", "Times New Roman", Georgia, serif',
    headingSize: "88px",
  });
});

for (const source of sources) {
  const component = componentName(source);

  for (const resolution of resolutions) {
    const name = resolutionName(resolution);

    test(`${component} fixtures at ${name}`, async ({ page }) => {
      await page.setViewportSize(resolution);
      for (const fixture of await fixturesFrom(page, source)) {
        await test.step(fixture.id, async () => {
          await openFixture(page, fixture);
          await expectNoOverflow(page, fixture);
          await expectTargets(page, fixture);
          await expectAtMostOnePrimary(page, fixture);
          await page.screenshot({ path: screenshotPath(fixture, name) });
          await expectAccessible(page, fixture);
        });
      }
    });
  }

  const reference: Resolution = resolutions[0];
  const referenceName = resolutionName(reference);

  test(`${component} fixtures with reduced motion at ${referenceName}`, async ({ page }) => {
    await page.setViewportSize(reference);
    await page.emulateMedia({ reducedMotion: "reduce" });
    for (const fixture of await fixturesFrom(page, source)) {
      await test.step(fixture.id, async () => {
        await openFixture(page, fixture);
        const slow = await page.evaluate(() =>
          [...document.querySelectorAll("*")].flatMap((element) => {
            const style = getComputedStyle(element);
            return [...style.transitionDuration.split(","), ...style.animationDuration.split(",")]
              .map((value) => parseFloat(value) * (value.trim().endsWith("ms") ? 1 : 1000))
              .filter((milliseconds) => milliseconds > 1)
              .map((milliseconds) => `${element.tagName} ${String(milliseconds)}ms`);
          }),
        );
        expect(slow, `${fixture.id} motion longer than 1 ms under reduced motion`).toEqual([]);
        await expectNoOverflow(page, fixture);
        await page.screenshot({ path: screenshotPath(fixture, `reduced-motion-${referenceName}`) });
        await expectAccessible(page, fixture);
      });
    }
  });

  test(`${component} fixtures with forced colors at ${referenceName}`, async ({ page }) => {
    await page.setViewportSize(reference);
    await page.emulateMedia({ forcedColors: "active" });
    for (const fixture of await fixturesFrom(page, source)) {
      await test.step(fixture.id, async () => {
        await openFixture(page, fixture);
        await expectNoOverflow(page, fixture);
        await expectTargets(page, fixture);
        await expectForcedColorsHonored(page, fixture);
        await page.screenshot({ path: screenshotPath(fixture, `forced-colors-${referenceName}`) });
        await expectAccessible(page, fixture, { forcedColors: true });
      });
    }
  });
}
