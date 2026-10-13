import type { Page } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { openGuestOnVirtualDisplays } from "../../../src/displays";
import { HookError, sendHook, type Role } from "../../../src/hooks";
import type { AppSession } from "../../../src/launch";

const recoveryLimitMs = 5_000;

async function revision(page: Page): Promise<string | null> {
  return page.locator("html").getAttribute("data-fotohavn-revision");
}

/** Marks the page's JavaScript realm; a recreated WebView2 (or any reload) loses the mark. */
async function mark(page: Page): Promise<void> {
  await page.evaluate(() => {
    (window as unknown as { __fotohavnMark?: boolean }).__fotohavnMark = true;
  });
}

async function isMarked(page: Page): Promise<boolean> {
  return page.evaluate(() => (window as unknown as { __fotohavnMark?: boolean }).__fotohavnMark === true);
}

/** Waits for a page with `role` that isn't `previous` and has no mark: the recreated WebView2's page. */
async function waitForRecoveredPage(app: AppSession, role: Role, previous: Page, deadline: number): Promise<Page> {
  while (Date.now() < deadline) {
    const page = await app.pageFor(role, { timeoutMs: Math.max(250, deadline - Date.now()) }).catch(() => undefined);
    if (page !== undefined && page !== previous && !(await isMarked(page).catch(() => true))) {
      return page;
    }
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  throw new Error(`the ${role} UI didn't come back within ${recoveryLimitMs} ms`);
}

async function crash(page: Page, role: Role): Promise<number> {
  const reply = await sendHook(page, "test.crashUi", { role });
  if (!reply.ok) {
    throw new HookError("test.crashUi", reply.error);
  }
  return Date.now();
}

test.describe("UI crash recovery", { tag: ["@m2"] }, () => {
  test("each window's UI returns within 5 s at the same revision after test.crashUi, and the other window is unaffected", async ({
    app,
    operatorPage,
  }) => {
    test.setTimeout(120_000);
    const guest = await openGuestOnVirtualDisplays(app);
    let guestPage = guest.page;

    const operatorRevision = await revision(operatorPage);
    const guestRevision = await revision(guestPage);
    expect(operatorRevision).toBe("1");
    expect(guestRevision).toBe("1");

    await test.step("test.crashUi('guest'): the guest UI returns at the same revision; the operator page is untouched", async () => {
      await mark(operatorPage);
      await mark(guestPage);
      const crashedAt = await crash(operatorPage, "guest");

      const recovered = await waitForRecoveredPage(app, "guest", guestPage, crashedAt + recoveryLimitMs);
      expect(Date.now() - crashedAt).toBeLessThanOrEqual(recoveryLimitMs);
      expect(await revision(recovered)).toBe(guestRevision);
      guestPage = recovered;

      expect(await isMarked(operatorPage)).toBe(true);
      await expect(operatorPage.locator("html")).toHaveAttribute("data-fotohavn-status", "connected");
      expect(await revision(operatorPage)).toBe(operatorRevision);
    });

    await test.step("test.crashUi('operator'): the operator UI returns at the same revision; the guest page is untouched", async () => {
      await mark(guestPage);
      const crashedAt = await crash(guestPage, "operator");

      const recovered = await waitForRecoveredPage(app, "operator", operatorPage, crashedAt + recoveryLimitMs);
      expect(Date.now() - crashedAt).toBeLessThanOrEqual(recoveryLimitMs);
      expect(await revision(recovered)).toBe(operatorRevision);

      expect(await isMarked(guestPage)).toBe(true);
      await expect(guestPage.locator("html")).toHaveAttribute("data-fotohavn-status", "connected");
      expect(await revision(guestPage)).toBe(guestRevision);
    });

    await test.step("both roles are connected again", async () => {
      const diagnostics = await app.hooks.getDiagnostics();
      expect(diagnostics.connectedRoles).toEqual(["operator", "guest"]);
      expect(diagnostics.presentations).toEqual([
        { role: "operator", revision: 1, surface: "eventList" },
        { role: "guest", revision: 1, surface: "start" },
      ]);
    });
  });
});
