import { virtualGuestId, virtualOperatorId } from "../../../src/displays";
import { expect, test } from "../../../src/fixtures";
import { useVirtualScreens } from "../../../src/operator";
import { EventListScreen, EventSetupScreen } from "../../../src/screens/screens";

test.describe("identifying displays", { tag: ["@m6", "@timing"] }, () => {
  test("IDENTIFY DISPLAYS shows one numbered overlay per display, matching the picker's numbers, for about 3 seconds", async ({
    app,
    operatorPage,
    hooks,
  }) => {
    await useVirtualScreens(app, 2);
    await new EventListScreen(operatorPage).newEvent.click();
    const setup = new EventSetupScreen(operatorPage);
    await expect(setup.heading).toBeVisible();

    const pickerNumbers = await test.step("the picker numbers the displays left to right", async () => {
      const rows = await setup.displayRows();
      expect(rows.map((row) => row.name)).toEqual(["Virtual operator", "Virtual guest"]);
      const numbers = rows.map((row) => Number(/Display (\d+)/.exec(row.detail)?.[1]));
      expect(numbers).toEqual([1, 2]);
      expect((await hooks.getDiagnostics()).identifyOverlays.filter((overlay) => overlay.visible)).toEqual([]);
      return new Map([
        [virtualOperatorId, numbers[0]],
        [virtualGuestId, numbers[1]],
      ]);
    });

    const clicked = await test.step("IDENTIFY DISPLAYS shows one visible overlay per display, with the picker's numbers", async () => {
      await setup.identifyDisplays.click();
      const at = performance.now();
      await expect
        .poll(async () => (await hooks.getDiagnostics()).identifyOverlays.filter((overlay) => overlay.visible).length, { timeout: 2_000 })
        .toBe(2);
      const overlays = (await hooks.getDiagnostics()).identifyOverlays;
      expect(overlays).toHaveLength(2);
      for (const overlay of overlays) {
        expect(overlay.visible).toBe(true);
        expect(overlay.number, overlay.displayId).toBe(pickerNumbers.get(overlay.displayId));
      }
      expect(overlays.map((overlay) => overlay.number).sort()).toEqual([1, 2]);
      await expect(setup.identifyDisplays).toHaveAttribute("aria-disabled", "true");
      return at;
    });

    await test.step("two seconds in, the overlays are still showing", async () => {
      await operatorPage.waitForTimeout(Math.max(0, 2_000 - (performance.now() - clicked)));
      const overlays = (await hooks.getDiagnostics()).identifyOverlays;
      expect(overlays.filter((overlay) => overlay.visible)).toHaveLength(2);
    });

    await test.step("by four seconds they are hidden, and IDENTIFY DISPLAYS is available again", async () => {
      await operatorPage.waitForTimeout(Math.max(0, 4_000 - (performance.now() - clicked)));
      const overlays = (await hooks.getDiagnostics()).identifyOverlays;
      expect(overlays.filter((overlay) => overlay.visible)).toEqual([]);
      await expect(setup.identifyDisplays).not.toHaveAttribute("aria-disabled", "true");
    });
  });
});
