import type { Page } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { openGuestOnVirtualDisplays, size, viewport } from "../../../src/displays";
import { EventListScreen, GuestStartScreen } from "../../../src/screens/screens";

interface ReceivedMessage {
  type: string;
  role?: string;
}

/**
 * Records every brain message a page receives over the WebView2 bridge, from document creation on. Installed on the
 * WebView2 browser context, so it also runs in the Guest page the host creates later.
 */
function recordBrainMessages(): void {
  const target = window as unknown as {
    __fotohavnReceived?: unknown[];
    chrome?: { webview?: { addEventListener(type: "message", listener: (event: { data: unknown }) => void): void } };
  };
  if (target.__fotohavnReceived !== undefined) {
    return;
  }
  const received: unknown[] = [];
  target.__fotohavnReceived = received;
  target.chrome?.webview?.addEventListener("message", (event) => {
    received.push(event.data);
  });
}

function received(page: Page): Promise<ReceivedMessage[]> {
  return page.evaluate(() => ((window as unknown as { __fotohavnReceived?: ReceivedMessage[] }).__fotohavnReceived ?? null) as ReceivedMessage[]);
}

test.describe("two screens", { tag: ["@m2"] }, () => {
  test("with two virtual displays the Guest window opens on the second display's bounds, and each page gets only its own presentation", async ({
    app,
    operatorPage,
    hooks,
  }) => {
    await test.step("record the brain messages each page receives", async () => {
      await operatorPage.context().addInitScript(recordBrainMessages);
      await operatorPage.reload();
      operatorPage = await app.pageFor("operator");
      expect(await received(operatorPage)).not.toBeNull();
    });

    const guest = await test.step("test.openGuestScreen on the second virtual display opens the Guest page", () =>
      openGuestOnVirtualDisplays(app),
    );

    await test.step("both pages completed their handshakes with the roles the host assigned", async () => {
      await expect(operatorPage.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");
      await expect(guest.page.locator("html")).toHaveAttribute("data-fotohavn-role", "guest");
      await expect(guest.page.locator("html")).toHaveAttribute("data-fotohavn-status", "connected");
      const diagnostics = await hooks.getDiagnostics();
      expect(diagnostics.connectedRoles).toEqual(["operator", "guest"]);
      expect(diagnostics.guestScreenOpen).toBe(true);
      expect(diagnostics.displays.virtual).toBe(true);
    });

    await test.step("the Guest window covers the second virtual display's bounds", async () => {
      const diagnostics = await hooks.getDiagnostics();
      const guestWindow = diagnostics.windows.find((window) => window.role === "guest");
      expect(guestWindow?.bounds).toEqual(guest.display.bounds);
      // Guest zoom uses the monitor's actual scale, so CSS pixels are physical pixels on the Guest Screen.
      expect(await viewport(guest.page)).toEqual({ ...size(guest.display.bounds), devicePixelRatio: 1 });
      // The Operator window was not moved by the virtual displays.
      expect(diagnostics.windows.find((window) => window.role === "operator")).toBeDefined();
    });

    await test.step("each page renders and receives only its own role's presentation", async () => {
      await expect(new EventListScreen(operatorPage).empty).toBeVisible();
      await expect(new GuestStartScreen(guest.page).heading).toBeVisible();
      await expect(guest.page.getByText("No Events yet.")).toHaveCount(0);
      await expect(operatorPage.getByText("Small room.")).toHaveCount(0);

      for (const [page, role] of [
        [operatorPage, "operator"],
        [guest.page, "guest"],
      ] as const) {
        const snapshots = (await received(page)).filter((message) => message.type === "welcome" || message.type === "presentation");
        expect(snapshots.length, `${role} page snapshots`).toBeGreaterThan(0);
        expect(snapshots.map((message) => message.role)).toEqual(snapshots.map(() => role));
      }
    });

    await test.step("test.closeGuestScreen closes the Guest window", async () => {
      await hooks.closeGuestScreen();
      await expect.poll(async () => (await hooks.getDiagnostics()).windows.map((window) => window.role)).toEqual(["operator"]);
      expect((await hooks.getDiagnostics()).connectedRoles).toEqual(["operator"]);
      // Closing again with no Guest Screen open is a no-op.
      await hooks.closeGuestScreen();
      expect((await hooks.getDiagnostics()).guestScreenOpen).toBe(false);
    });
  });
});
