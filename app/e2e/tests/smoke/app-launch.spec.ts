import { existsSync, statSync } from "node:fs";
import { expect, test } from "../../src/fixtures";
import { appOrigin } from "../../src/launch";
import { PlaceholderScreen } from "../../src/screens/placeholder-screen";

test("the E2E build launches, renders the placeholder page, and exits cleanly", { tag: ["@m1"] }, async ({
  app,
  page,
  dataDir,
}) => {
  await test.step("the Data folder is created at the given path", () => {
    expect(existsSync(dataDir) && statSync(dataDir).isDirectory(), `Data folder at ${dataDir}`).toBe(true);
  });

  await test.step("Playwright is connected through CDP to the app page", async () => {
    expect(app.browser.isConnected()).toBe(true);
    expect(page.url().startsWith(appOrigin)).toBe(true);
  });

  await test.step("the placeholder page renders", async () => {
    const screen = new PlaceholderScreen(page);
    await expect(page).toHaveTitle("FOTOHAVN");
    await expect(screen.heading).toBeVisible();
    await expect(screen.status).toBeVisible();
  });

  await test.step("test.exit shuts the app down with exit code 0", async () => {
    const exit = await app.close();
    expect(exit.code).toBe(0);
    expect(existsSync(dataDir), "Data folder still present after exit").toBe(true);
  });
});
