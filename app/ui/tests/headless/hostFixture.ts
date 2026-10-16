import { randomBytes } from "node:crypto";
import { mkdirSync, rmSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { test as base, expect, type Browser, type Page } from "@playwright/test";
import { e2eUiFolder, headlessOutputDir, startHeadlessHost, type HeadlessHost } from "./headlessHost.ts";

// The integration lane's shared pieces: one real headless host per worker (real brain, WebSocket bridge, real store and
// compositor, fake camera and displays) serving the E2E UI, and role pages that connect to it with their own tokens.

interface ReceivedMessage {
  readonly type: string;
  readonly role?: string;
  readonly revision?: number;
}

export interface RolePage {
  readonly page: Page;
  /** Every brain message this page received over its WebSocket, oldest first. */
  readonly received: ReceivedMessage[];
}

export const test = base.extend<object, { host: HeadlessHost }>({
  host: [
    // eslint-disable-next-line no-empty-pattern -- Playwright fixtures must destructure their first argument.
    async ({}, use) => {
      const dataFolder = resolve(headlessOutputDir, `${String(Date.now())}-${randomBytes(3).toString("hex")}`, "Data");
      mkdirSync(dataFolder, { recursive: true });
      const host = await startHeadlessHost({ uiFolder: e2eUiFolder, dataFolder });
      try {
        await use(host);
      } finally {
        await host.stop();
        // The host's file handles (the database, the logs) and a virus scanner can hold the folder for a moment after
        // the process is gone, and Windows answers EPERM or EBUSY; retry, and never fail the run over a git-ignored
        // scratch folder that the next run's own folder doesn't share.
        try {
          rmSync(dirname(dataFolder), { recursive: true, force: true, maxRetries: 20, retryDelay: 250 });
        } catch (error) {
          console.warn(`Could not remove ${dirname(dataFolder)}: ${String(error)}`);
        }
      }
    },
    { scope: "worker", timeout: 90_000 },
  ],
});

export { expect };

export async function openRolePage(browser: Browser, url: string): Promise<RolePage> {
  const context = await browser.newContext();
  const page = await context.newPage();
  const received: ReceivedMessage[] = [];
  page.on("websocket", (socket) => {
    socket.on("framereceived", ({ payload }) => {
      if (typeof payload === "string") {
        received.push(JSON.parse(payload) as ReceivedMessage);
      }
    });
  });
  await page.goto(url);
  return { page, received };
}

export async function expectHandshake(page: Page, role: "operator" | "guest"): Promise<void> {
  const root = page.locator("html");
  await expect(root).toHaveAttribute("data-fotohavn-status", "connected");
  await expect(root).toHaveAttribute("data-fotohavn-role", role);
  // The Operator Screen shows Starting (revision 1) until the Event store opens, then the Event list (revision 2).
  await expect(root).toHaveAttribute("data-fotohavn-revision", role === "operator" ? "2" : "1");
}
