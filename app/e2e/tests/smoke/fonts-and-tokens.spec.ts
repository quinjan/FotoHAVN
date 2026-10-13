import { expect, test } from "../../src/fixtures";
import { brandColors, openFixture, returnToApp } from "../../src/gallery";

// The real WebView2 serves the E2E UI bundle from https://app.fotohavn/ with no network, so the brand fonts and tokens
// must come from the bundle. Fixtures come from the gallery (app/ui/src/components/<Name>/<Name>.fixtures.tsx).

const bundleOrigin = "https://app.fotohavn/";

test("the brand fonts load from the bundle and computed colors match the brand tokens", { tag: ["@m3"] }, async ({ operatorPage: page }) => {
  const colors = brandColors();
  const requests: string[] = [];
  page.on("request", (request) => {
    requests.push(request.url());
  });

  try {
    await test.step("statement-heading-start loads both families with every request staying on the bundle origin", async () => {
      await openFixture(page, "statement-heading-start", "guest");
      await page.evaluate(async () => {
        // The 500 weight is the wordmark's, so no fixture draws it; load it explicitly like any use would.
        await Promise.all([
          document.fonts.load('500 32px "Cormorant Garamond"'),
          document.fonts.load('400 16px "Manrope"'),
          document.fonts.ready,
        ]);
      });

      expect(requests.length, "the page made requests").toBeGreaterThan(0);
      const offBundle = requests.filter((url) => !url.startsWith(bundleOrigin) && !url.startsWith("data:") && !url.startsWith("blob:"));
      expect(offBundle, "requests outside https://app.fotohavn/").toEqual([]);

      const fonts = await page.evaluate(() => {
        const loaded: string[] = [];
        document.fonts.forEach((face) => {
          if (face.status === "loaded") {
            loaded.push(face.family.replaceAll('"', ""));
          }
        });
        return {
          cormorant500: document.fonts.check('500 32px "Cormorant Garamond"'),
          manrope400: document.fonts.check('400 16px "Manrope"'),
          loaded,
          woff2: performance
            .getEntriesByType("resource")
            .map((entry) => entry.name)
            .filter((url) => /\.woff2(\?|$)/.test(url)),
        };
      });
      expect(fonts.cormorant500).toBe(true);
      expect(fonts.manrope400).toBe(true);
      expect(fonts.loaded).toContain("Cormorant Garamond");
      expect(fonts.loaded).toContain("Manrope");
      expect(fonts.woff2.length, "font files this page fetched").toBeGreaterThan(0);
      for (const url of fonts.woff2) {
        expect(url.startsWith(bundleOrigin), `${url} comes from the bundle`).toBe(true);
      }
    });

    await test.step("the display and reading text resolve to the brand families", async () => {
      const families = await page.evaluate(() => ({
        heading: getComputedStyle(document.querySelector("h1") ?? document.body).fontFamily,
        action: getComputedStyle(document.querySelector('[data-fotohavn-action="primary"]') ?? document.body).fontFamily,
      }));
      expect(families.heading).toMatch(/^"?Cormorant Garamond"?,/);
      expect(families.action).toMatch(/^"?Manrope"?,/);
    });

    // Each assertion names the token it expects (tokens.json), read from disk.
    await test.step("light surface: Off-white canvas, Ebony ink (statement-heading-start)", async () => {
      const surface = page.locator('main[data-fotohavn-fixture="statement-heading-start"]');
      await expect(surface).toHaveCSS("background-color", colors["off-white"] ?? "");
      await expect(surface).toHaveCSS("color", colors["ebony"] ?? "");
    });

    await test.step("PrimaryAction: Ebony fill with Off-white label (primary-action-guest)", async () => {
      await openFixture(page, "primary-action-guest", "guest");
      const action = page.locator('[data-fotohavn-action="primary"]');
      await expect(action).toHaveCSS("background-color", colors["ebony"] ?? "");
      await expect(action).toHaveCSS("color", colors["off-white"] ?? "");
    });

    await test.step("PrimaryAction inverted on dark: Off-white fill with Ebony label on a Dark Walnut surface (primary-action-inverted)", async () => {
      await openFixture(page, "primary-action-inverted", "guest");
      const action = page.locator('[data-fotohavn-action="primary"]');
      await expect(action).toHaveCSS("background-color", colors["off-white"] ?? "");
      await expect(action).toHaveCSS("color", colors["ebony"] ?? "");
      await expect(page.locator('main[data-fotohavn-fixture="primary-action-inverted"]')).toHaveCSS(
        "background-color",
        colors["dark-walnut"] ?? "",
      );
    });

    await test.step("Eyebrow: Soft Brown on light, Antique Metal on dark (eyebrow-operator, eyebrow-dark)", async () => {
      await openFixture(page, "eyebrow-operator", "operator");
      await expect(page.getByText("SANTOS AND REYES WEDDING", { exact: true })).toHaveCSS("color", colors["soft-brown"] ?? "");
      await openFixture(page, "eyebrow-dark", "guest");
      await expect(page.getByText("ON THE OTHER SIDE", { exact: true })).toHaveCSS("color", colors["antique-metal"] ?? "");
    });
  } finally {
    await returnToApp(page);
  }
});
