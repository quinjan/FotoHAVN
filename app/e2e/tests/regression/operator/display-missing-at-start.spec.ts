import path from "node:path";
import { queryOne } from "../../../src/data";
import { virtualGuestId } from "../../../src/displays";
import { expect, test } from "../../../src/fixtures";
import { createEvent, endEventFromOperator, useVirtualScreens } from "../../../src/operator";
import { EventListScreen, EventSetupScreen } from "../../../src/screens/screens";

const notConnected = "The chosen guest display isn't connected.";

function binding(dataDir: string, id: string): { guest_display_id: string; guest_display_name: string; camera_device_id: string } | undefined {
  return queryOne(path.join(dataDir, "fotohavn.db"), "SELECT guest_display_id, guest_display_name, camera_device_id FROM events WHERE id = ?", id);
}

test.describe("Start when the guest display is missing", { tag: ["@m6", "@camera"] }, () => {
  test("Start stays in setup with the reason and keeps the binding; once the display is back, Start succeeds", async ({
    app,
    operatorPage,
    camera,
    hooks,
    dataDir,
  }) => {
    const screens = await useVirtualScreens(app, 2);
    const list = new EventListScreen(operatorPage);
    const setup = new EventSetupScreen(operatorPage);
    const name = "Missing display";
    const guestDisplay = screens.guest;
    if (guestDisplay === undefined) {
      throw new Error("Two virtual displays were expected.");
    }

    const id = await createEvent({ app, page: operatorPage, camera }, { name, display: guestDisplay.name, pin: "4821" });
    const before = binding(dataDir, id);
    expect(before?.guest_display_id).toBe(virtualGuestId);

    await test.step("the bound display is disconnected, then Start", async () => {
      await hooks.disconnectDisplay(virtualGuestId);
      await list.rowAction(name, "START").click();
    });

    await test.step("Event setup opens with the reason, and the Event did not start", async () => {
      await expect(setup.heading).toBeVisible();
      await expect(setup.statusLine(notConnected).first()).toBeVisible();
      await expect(setup.name).toHaveValue(name);
      const diagnostics = await hooks.getDiagnostics();
      expect(diagnostics.guestScreenOpen).toBe(false);
      expect(diagnostics.wakeLock.held).toBe(false);
    });

    await test.step("the setup still shows the bound display, as not connected; the binding is unchanged", async () => {
      const rows = await setup.displayRows();
      const bound = rows.find((row) => row.name === guestDisplay.name);
      expect(bound, JSON.stringify(rows)).toMatchObject({ checked: true, disabled: true });
      expect(bound?.detail).toContain("Not connected");
      expect(rows.filter((row) => row.checked)).toHaveLength(1);
      expect(binding(dataDir, id)).toEqual(before);
    });

    await test.step("the display is reconnected and SAVE AND START starts the Event", async () => {
      await hooks.connectDisplay(virtualGuestId);
      await expect(setup.display(guestDisplay.name)).toBeEnabled();
      await expect(setup.display(guestDisplay.name)).toBeChecked();
      await setup.goTo("Guests");
      await setup.saveAndStart.click();
      await expect(list.runningPanel(name)).toBeVisible({ timeout: 30_000 });
      expect((await hooks.getDiagnostics()).guestScreenOpen).toBe(true);
      expect(binding(dataDir, id)).toEqual(before);
    });

    await endEventFromOperator(app, operatorPage);
  });
});
