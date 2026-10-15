import path from "node:path";
import { queryOne } from "../../../src/data";
import { expect, test } from "../../../src/fixtures";
import { createEvent, endEventFromOperator, useVirtualScreens } from "../../../src/operator";
import { EventListScreen, EventSetupScreen } from "../../../src/screens/screens";

function binding(dataDir: string, id: string): { camera_device_id: string; camera_name: string; guest_display_id: string } | undefined {
  return queryOne(path.join(dataDir, "fotohavn.db"), "SELECT camera_device_id, camera_name, guest_display_id FROM events WHERE id = ?", id);
}

test.describe("Start when the camera isn't eligible", { tag: ["@m6", "@camera"] }, () => {
  test("with an injected camera fault Start stays in setup with the reason and keeps the binding; with the fault cleared, Start succeeds", async ({
    app,
    operatorPage,
    camera,
    hooks,
    dataDir,
  }) => {
    const screens = await useVirtualScreens(app, 2);
    const list = new EventListScreen(operatorPage);
    const setup = new EventSetupScreen(operatorPage);
    const name = "Camera trouble";

    const id = await createEvent({ app, page: operatorPage, camera }, { name, display: screens.guest?.name ?? "", pin: "4821" });
    const before = binding(dataDir, id);
    expect(before?.camera_name).toBe(camera.name);

    await test.step("with the camera removed, Start opens Event setup with the camera's start failure (the camera can't be found)", async () => {
      await hooks.injectCameraFault("removed");
      await list.rowAction(name, "START").click();
      await expect(setup.heading).toBeVisible();
      await expect(setup.statusLine("The camera isn't ready to start. FOTOHAVN can't find this camera.").first()).toBeVisible({ timeout: 30_000 });
      const diagnostics = await hooks.getDiagnostics();
      expect(diagnostics.guestScreenOpen).toBe(false);
      expect(diagnostics.wakeLock.held).toBe(false);
      await expect(list.main).toHaveCount(0);
    });

    await test.step("the binding is not replaced", async () => {
      await expect(setup.camera.locator("option:checked")).toHaveText(`${camera.name} (not connected)`);
      await expect(setup.cameraStatus).toHaveText("This camera isn't connected. FOTOHAVN keeps it until you choose another.");
      expect(binding(dataDir, id)).toEqual(before);
    });

    await test.step("with the fault cleared, SAVE AND START starts the Event", async () => {
      await hooks.clearCameraFault();
      await setup.saveAndStart.click();
      await expect(list.runningPanel(name)).toBeVisible({ timeout: 30_000 });
      expect((await hooks.getDiagnostics()).camera.state).toBe("ready");
      expect(binding(dataDir, id)).toEqual(before);
    });

    await endEventFromOperator(app, operatorPage);
  });
});
