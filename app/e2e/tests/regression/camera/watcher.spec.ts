import { expect, test } from "../../../src/fixtures";
import { openDiagnostics, waitForCameraState, waitForFreshFrames } from "../../../src/camera-diagnostics";

const byId = (a: { id: string }, b: { id: string }) => a.id.localeCompare(b.id);

test(
  "the camera list from test.listCameras matches the diagnostics screen",
  { tag: ["@m5", "@camera"] },
  async ({ app, operatorPage, hooks, camera }) => {
    const screen = await openDiagnostics(operatorPage, hooks, camera);
    await waitForFreshFrames(hooks);

    await test.step("the hook's names and ids equal the rendered rows", async () => {
      const listed = await hooks.listCameras();
      expect(listed.length).toBeGreaterThan(0);
      expect(listed.map((each) => each.id)).toContain(camera.id);
      await expect.poll(async () => (await screen.cameraList()).toSorted(byId)).toEqual(listed.toSorted(byId));
      await expect(screen.cameraRows).toHaveCount(listed.length);
    });

    await test.step("a removed camera drops off the screen list and the hook agrees", async () => {
      await hooks.injectCameraFault("removed");
      await waitForCameraState(hooks, "notAvailable", { timeoutMs: 5_000 });

      await expect.poll(async () => (await hooks.listCameras()).map((each) => each.id)).not.toContain(camera.id);
      const listed = await hooks.listCameras();
      await expect.poll(async () => (await screen.cameraList()).toSorted(byId)).toEqual(listed.toSorted(byId));
      await expect(screen.root).toHaveAttribute("data-camera-reason", "removed");
    });

    await test.step("clearing the fault brings the camera back on both", async () => {
      await hooks.clearCameraFault();
      await expect.poll(async () => (await hooks.listCameras()).map((each) => each.id)).toContain(camera.id);
      const listed = await hooks.listCameras();
      await expect.poll(async () => (await screen.cameraList()).toSorted(byId)).toEqual(listed.toSorted(byId));
      await waitForFreshFrames(hooks);
      await expect(screen.root).toHaveAttribute("data-camera-state", "ready");
    });

    expect((await app.close()).code).toBe(0);
  },
);
