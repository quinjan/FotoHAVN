import { existsSync, statSync } from "node:fs";
import { expect, test } from "../../src/fixtures";
import { appOrigin } from "../../src/launch";
import { EventListScreen } from "../../src/screens/screens";

test("the E2E build launches, renders the Operator page, and exits cleanly", { tag: ["@m1"] }, async ({
  app,
  operatorPage,
  dataDir,
}) => {
  await test.step("the Data folder is created at the given path", () => {
    expect(existsSync(dataDir) && statSync(dataDir).isDirectory(), `Data folder at ${dataDir}`).toBe(true);
  });

  await test.step("Playwright is connected through CDP to the app page", async () => {
    expect(app.browser.isConnected()).toBe(true);
    expect(operatorPage.url().startsWith(appOrigin)).toBe(true);
  });

  await test.step("the Operator page renders after its handshake", async () => {
    await expect(operatorPage).toHaveTitle("FOTOHAVN");
    await expect(operatorPage.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");
    await expect(new EventListScreen(operatorPage).heading).toBeVisible();
  });

  await test.step("test.exit shuts the app down with exit code 0", async () => {
    const exit = await app.close();
    expect(exit.code).toBe(0);
    expect(existsSync(dataDir), "Data folder still present after exit").toBe(true);
  });
});
