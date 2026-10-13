import { expect, test } from "../../../src/fixtures";
import { openGuestOnVirtualDisplays, viewport } from "../../../src/displays";

const layoutSizes = [
  { width: 1280, height: 720 },
  { width: 1366, height: 768 },
  { width: 1920, height: 1080 },
  { width: 2560, height: 1440 },
] as const;

test.describe("window bounds", { tag: ["@m2"] }, () => {
  test("test.setWindowBounds sizes each window's page viewport for the layout checks", async ({ app, operatorPage, hooks }) => {
    const guest = await openGuestOnVirtualDisplays(app);

    for (const { width, height } of layoutSizes) {
      await test.step(`operator ${String(width)}×${String(height)}`, async () => {
        const window = await hooks.setWindowBounds("operator", width, height);
        expect(window.role).toBe("operator");
        const seen = await viewport(operatorPage);
        expect({ width: seen.width * seen.devicePixelRatio, height: seen.height * seen.devicePixelRatio }).toEqual({ width, height });
      });

      await test.step(`guest ${String(width)}×${String(height)}`, async () => {
        const window = await hooks.setWindowBounds("guest", width, height);
        expect(window.bounds).toMatchObject({ x: guest.display.bounds.x, y: guest.display.bounds.y, width, height });
        expect(await viewport(guest.page)).toEqual({ width, height, devicePixelRatio: 1 });
      });
    }
  });
});
