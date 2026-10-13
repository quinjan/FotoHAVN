import type { Page } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { openGuestOnVirtualDisplays } from "../../../src/displays";
import { appOrigin, appPages, type AppSession } from "../../../src/launch";

const external = "https://example.com/";

async function expectNavigationBlocked(app: AppSession, page: Page, role: "operator" | "guest"): Promise<void> {
  const before = appPages(app.browser).length;
  const pagesBefore = app.browser.contexts().flatMap((context) => context.pages()).length;

  await test.step(`${role}: script navigation to an external URL is cancelled`, async () => {
    await page.evaluate((url) => {
      window.location.href = url;
    }, external);
    await page.waitForTimeout(1_000);
    expect(page.url().startsWith(appOrigin), page.url()).toBe(true);
    await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", role);
  });

  await test.step(`${role}: a link to an external URL is cancelled`, async () => {
    await page.evaluate((url) => {
      const link = document.createElement("a");
      link.href = url;
      document.body.append(link);
      link.click();
      link.remove();
    }, external);
    await page.waitForTimeout(1_000);
    expect(page.url().startsWith(appOrigin), page.url()).toBe(true);
  });

  await test.step(`${role}: window.open opens no window`, async () => {
    await page.evaluate((url) => {
      window.open(url);
      window.open(url, "_blank", "noopener");
      window.open("https://app.fotohavn/index.html");
    }, external);
    await page.waitForTimeout(1_000);
    expect(app.browser.contexts().flatMap((context) => context.pages())).toHaveLength(pagesBefore);
    expect(appPages(app.browser)).toHaveLength(before);
    await expect(page.locator("html")).toHaveAttribute("data-fotohavn-status", "connected");
  });
}

test.describe("navigation guard", { tag: ["@m2"] }, () => {
  test("script navigation to an external URL and window.open are blocked in both windows", async ({ app, operatorPage }) => {
    const guest = await openGuestOnVirtualDisplays(app);
    await expectNavigationBlocked(app, operatorPage, "operator");
    await expectNavigationBlocked(app, guest.page, "guest");
  });
});
