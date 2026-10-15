import { expect, test } from "../../../src/fixtures";
import { size, viewport } from "../../../src/displays";

test.describe("physical displays", { tag: ["@m2", "@physical-displays", "@cursor"] }, () => {
  test("with two real displays the Guest window covers the second display's full bounds", async ({ app, hooks }) => {
    const diagnostics = await hooks.getDiagnostics();
    const real = diagnostics.displays.displays;
    test.skip(real.length < 2, "not run: one display");

    const second = real.find((display) => !display.isPrimary);
    expect(second, JSON.stringify(real)).toBeDefined();
    if (second === undefined) {
      return;
    }

    await hooks.openGuestScreen(second.id);
    const guestPage = await app.pageFor("guest", { timeoutMs: 20_000 });

    const after = await hooks.getDiagnostics();
    expect(after.displays.virtual).toBe(false);
    const guestWindow = after.windows.find((window) => window.role === "guest");
    expect(guestWindow?.bounds).toEqual(second.bounds);
    expect(guestWindow?.displayId).toBe(second.id);
    expect(await viewport(guestPage)).toEqual({ ...size(second.bounds), devicePixelRatio: 1 });
    await expect(guestPage.locator("html")).toHaveAttribute("data-fotohavn-role", "guest");
  });
});
