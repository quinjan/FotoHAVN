import { resolve } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir } from "./resolutions.ts";

// M1 placeholder page. M3 replaces this with the fixture gallery route.
for (const resolution of resolutions) {
  const name = resolutionName(resolution);

  test.describe(`placeholder at ${name}`, () => {
    test.use({ viewport: resolution });

    test("renders without scrolling and without axe violations", async ({ page }) => {
      await page.goto("/index.html");

      await expect(page).toHaveTitle("FOTOHAVN");
      await expect(page.getByRole("main")).toBeVisible();
      await expect(page.getByRole("heading", { level: 1, name: "FOTOHAVN" })).toBeVisible();
      await expect(page.getByText("The booth is getting ready.")).toBeVisible();

      const overflow = await page.evaluate(() => {
        const root = document.documentElement;
        return {
          horizontal: root.scrollWidth > root.clientWidth,
          vertical: root.scrollHeight > root.clientHeight,
        };
      });
      expect(overflow).toEqual({ horizontal: false, vertical: false });

      await page.screenshot({ path: resolve(screenshotDir, `placeholder-${name}.png`) });

      const axe = await new AxeBuilder({ page }).analyze();
      expect(axe.violations).toEqual([]);
    });
  });
}
