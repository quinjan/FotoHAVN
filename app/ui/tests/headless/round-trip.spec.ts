import type { Page } from "@playwright/test";
import { expect, expectHandshake, openRolePage, test } from "./hostFixture.ts";

// Integration lane (plan §14, M2): the real headless host (real brain, WebSocket bridge) with one operator and one
// guest page in Chromium, each using the UI's own WebSocket BrainClient. The host serves the E2E UI (dist-e2e/, built
// by the web server in playwright.config.ts) so the pages expose `window.fotohavnE2E`; the host itself is a normal
// build, without test hooks.

interface E2EWindow {
  readonly fotohavnE2E: {
    readonly kind: string;
    sendCommand(name: string, payload: object): Promise<unknown>;
  };
}

test.describe.configure({ mode: "serial" });

function sendCommand(page: Page, name: string): Promise<unknown> {
  return page.evaluate((command) => (window as unknown as E2EWindow).fotohavnE2E.sendCommand(command, {}), name);
}

test("operator and guest pages handshake over the WebSocket bridge with the roles their tokens grant", async ({
  browser,
  host,
}) => {
  const operator = await openRolePage(browser, host.listening.operatorUrl);
  const guest = await openRolePage(browser, host.listening.guestUrl);

  await expectHandshake(operator.page, "operator");
  await expectHandshake(guest.page, "guest");
  expect(await operator.page.evaluate(() => (window as unknown as E2EWindow).fotohavnE2E.kind)).toBe("websocket");
  expect(await guest.page.evaluate(() => (window as unknown as E2EWindow).fotohavnE2E.kind)).toBe("websocket");

  await expect(operator.page.locator('[data-fotohavn-surface="eventList"]')).toBeVisible();
  await expect(operator.page.getByText("No Events yet.")).toBeVisible();
  await expect(guest.page.locator('[data-fotohavn-surface="start"]')).toBeVisible();
  await expect(guest.page.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();

  await operator.page.context().close();
  await guest.page.context().close();
});

test("each page receives only its own role's welcome and presentations", async ({ browser, host }) => {
  const operator = await openRolePage(browser, host.listening.operatorUrl);
  const guest = await openRolePage(browser, host.listening.guestUrl);
  await expectHandshake(operator.page, "operator");
  await expectHandshake(guest.page, "guest");

  for (const [rolePage, role] of [
    [operator, "operator"],
    [guest, "guest"],
  ] as const) {
    const snapshots = rolePage.received.filter((message) => message.type === "welcome" || message.type === "presentation");
    expect(snapshots.length, `${role} snapshots`).toBeGreaterThan(0);
    expect(snapshots.map((message) => message.role)).toEqual(snapshots.map(() => role));
    expect(rolePage.received.filter((message) => message.type === "welcome")).toHaveLength(1);
  }

  await operator.page.context().close();
  await guest.page.context().close();
});

test("a guest page's OpenNewEvent is rejected wrongRole", async ({ browser, host }) => {
  const guest = await openRolePage(browser, host.listening.guestUrl);
  await expectHandshake(guest.page, "guest");

  expect(await sendCommand(guest.page, "OpenNewEvent")).toEqual({ status: "rejected", reason: "wrongRole" });
  expect(guest.received).toContainEqual(expect.objectContaining({ type: "commandResult", status: "rejected", reason: "wrongRole" }));
  await expect(guest.page.locator("html")).toHaveAttribute("data-fotohavn-revision", "1");

  await guest.page.context().close();
});

test("an operator page's OpenNewEvent opens Event setup from the button, and a second one from the client is rejected notAvailable", async ({
  browser,
  host,
}) => {
  const operator = await openRolePage(browser, host.listening.operatorUrl);
  await expectHandshake(operator.page, "operator");

  await operator.page.getByRole("button", { name: "NEW EVENT" }).click();
  await expect(operator.page.locator('[data-fotohavn-surface="eventSetup"]')).toBeVisible();
  expect(await sendCommand(operator.page, "OpenNewEvent")).toEqual({ status: "rejected", reason: "notAvailable" });
  expect(operator.received.filter((message) => message.type === "commandResult")).toHaveLength(2);

  await operator.page.context().close();
});
