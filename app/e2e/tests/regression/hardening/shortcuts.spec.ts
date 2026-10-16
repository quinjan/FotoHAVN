import path from "node:path";
import { expect, test } from "../../../src/fixtures";
import { databaseFileName, queryAll } from "../../../src/data";
import { openGuestOnVirtualDisplays } from "../../../src/displays";
import { startGuestEvent } from "../../../src/guest-cycle";
import type { AppSession } from "../../../src/launch";
import { ConfirmDialogScreen, EventListScreen } from "../../../src/screens/screens";
import { findWindow, postClose, sendAltF4, type WindowInfo } from "../../../src/win32";

const operatorTitle = "FOTOHAVN";
const guestTitle = "FOTOHAVN Guest Screen";
const closeAppTitle = "End the running Event and close FOTOHAVN?";

function pidOf(app: AppSession): number {
  if (app.run.pid === undefined) {
    throw new Error("The app has no PID.");
  }
  return app.run.pid;
}

function windowOf(app: AppSession, title: string): WindowInfo {
  const found = findWindow(pidOf(app), title);
  if (found === undefined) {
    throw new Error(`The app has no "${title}" window.`);
  }
  return found;
}

/** Waits for the app process to exit on its own and returns its exit code. */
async function waitForExit(app: AppSession, timeoutMs = 20_000): Promise<number | null> {
  await expect.poll(() => app.run.exit !== undefined, { timeout: timeoutMs, message: "the app process exits" }).toBe(true);
  return app.run.exit?.code ?? null;
}

test.describe("window shortcuts and closing the app", { tag: ["@m10", "@camera", "@cursor"] }, () => {
  test("control: with no Event running, Alt+F4 sent to the Guest window closes it, so the helper's key really reaches it", async ({ app }) => {
    const guest = await openGuestOnVirtualDisplays(app);
    const window = windowOf(app, guestTitle);
    const sent = sendAltF4(window.handle);
    expect(sent, "the Guest window took the foreground and Alt+F4 was sent").toEqual({ foreground: true, sent: true });
    await expect.poll(() => findWindow(pidOf(app), guestTitle), { timeout: 10_000, message: "the Guest window closed" }).toBeUndefined();
    await expect.poll(() => guest.page.isClosed(), { timeout: 10_000 }).toBe(true);
    // The next test needs an app that has no virtual displays yet.
    expect((await app.close()).code).toBe(0);
  });

  test("during an Active Event Alt+F4 does not close the Guest window, and closing the Operator window asks to end the Event first", async ({
    app,
    operatorPage,
    camera,
    hooks,
    dataDir,
  }) => {
    test.setTimeout(180_000);
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: "Shortcuts event", countdown: 3 });
    await run.stepInside();
    const cycleBefore = await run.cycle();
    expect(cycleBefore, "a Guest Cycle is running").not.toBeNull();
    const list = new EventListScreen(operatorPage);
    const dialog = new ConfirmDialogScreen(operatorPage);

    await test.step("Alt+F4 sent to the foreground Guest window changes nothing", async () => {
      const guestWindow = windowOf(app, guestTitle);
      const sent = sendAltF4(guestWindow.handle);
      expect(sent, "the Guest window took the foreground and Alt+F4 was sent").toEqual({ foreground: true, sent: true });
      await operatorPage.waitForTimeout(2_000);
      expect(findWindow(pidOf(app), guestTitle), "the Guest window is still open").toBeDefined();
      expect((await hooks.getDiagnostics()).guestScreenOpen).toBe(true);
      expect((await run.guest()).isClosed()).toBe(false);
      expect(await run.cycle(), "the Guest Cycle runs on").not.toBeNull();
      await expect(list.runningPanel("Shortcuts event")).toBeVisible();
    });

    await test.step("closing the Operator window asks to end the Event first, and KEEP RUNNING keeps everything running", async () => {
      expect(postClose(windowOf(app, operatorTitle).handle)).toBe(true);
      await expect(dialog.dialog).toBeVisible();
      await expect(dialog.title).toHaveText(closeAppTitle);
      expect(app.run.exit, "nothing closed while the question is open").toBeUndefined();

      await dialog.button("KEEP RUNNING").click();
      await expect(dialog.dialog).toHaveCount(0);
      expect(app.run.exit).toBeUndefined();
      expect(findWindow(pidOf(app), operatorTitle)).toBeDefined();
      expect(findWindow(pidOf(app), guestTitle)).toBeDefined();
      expect((await hooks.getDiagnostics()).wakeLock.held).toBe(true);
      await expect(list.runningPanel("Shortcuts event")).toBeVisible();
    });

    await test.step("END EVENT AND CLOSE ends the Event as an operator exit and the app exits with code 0", async () => {
      expect(postClose(windowOf(app, operatorTitle).handle)).toBe(true);
      await expect(dialog.dialog).toBeVisible();
      await dialog.button("END EVENT AND CLOSE").click();
      expect(await waitForExit(app)).toBe(0);
      const exit = await app.close();
      expect(exit.code).toBe(0);
    });

    await test.step("the Event ended cleanly: its running cycle ended operator_exit", async () => {
      const cycles = queryAll<{ id: string; end_reason: string | null; ended_at: string | null }>(
        path.join(dataDir, databaseFileName),
        "SELECT id, end_reason, ended_at FROM guest_cycles WHERE event_id = ?",
        run.eventId,
      );
      expect(cycles.map((cycle) => cycle.end_reason)).toEqual(["operator_exit"]);
      expect(cycles[0]?.ended_at).not.toBeNull();
    });
  });

  test("with no Active Event, closing the Operator window closes the app at once, with code 0", async ({ app }) => {
    expect(postClose(windowOf(app, operatorTitle).handle)).toBe(true);
    expect(await waitForExit(app, 15_000)).toBe(0);
    const exit = await app.close();
    expect(exit.code).toBe(0);
  });
});
