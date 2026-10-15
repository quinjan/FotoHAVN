import { size, viewport } from "../../../src/displays";
import { expect, test } from "../../../src/fixtures";
import { exitHoldMs, expectEventEnded, guestPage, holdFor, openNewEventAndFill, useVirtualScreens } from "../../../src/operator";
import { EventListScreen, GuestStartScreen, PinDialogScreen, SingleScreenOperator } from "../../../src/screens/screens";

const pin = "4821";

test.describe("a single screen", { tag: ["@m6"] }, () => {
  test("with one display the Guest Screen covers the Operator Screen, which says so, offers no END EVENT, and returns only by the hold and PIN", async ({
    app,
    operatorPage,
    camera,
    hooks,
  }) => {
    const screens = await useVirtualScreens(app, 1);
    const list = new EventListScreen(operatorPage);
    const covered = new SingleScreenOperator(operatorPage);
    const name = "One screen only";

    const setup = await test.step("choosing the Operator Screen's own display warns that the Guest Screen will cover it", async () => {
      const opened = await openNewEventAndFill(
        { app, page: operatorPage, camera },
        { name, display: screens.operator.name, pin },
      );
      await expect(opened.displayGroup).toContainText("Operator Screen");
      await expect(opened.main.getByText("The Guest Screen will cover this screen. Use the hold and PIN to return.", { exact: true })).toBeVisible();
      return opened;
    });

    await test.step("SAVE AND START puts the Guest Screen over the Operator window on the same display", async () => {
      await setup.saveAndStart.click();
      const guest = await guestPage(app);
      await expect(new GuestStartScreen(guest).stepInside).toBeVisible();
      const diagnostics = await hooks.getDiagnostics();
      expect(diagnostics.windows.find((window) => window.role === "guest")?.bounds).toEqual(screens.operator.bounds);
      expect(await viewport(guest)).toEqual({ ...size(screens.operator.bounds), devicePixelRatio: 1 });
      expect(diagnostics.guestScreenOpen).toBe(true);
    });

    await test.step("the Operator presentation is the single-screen surface, with no END EVENT", async () => {
      await expect(covered.main).toBeVisible();
      await expect(covered.statement).toBeVisible();
      await expect(operatorPage.getByRole("button", { name: "END EVENT" })).toHaveCount(0);
      await expect(operatorPage.getByRole("button")).toHaveCount(0);
      const presentation = (await hooks.getDiagnostics()).presentations.find((entry) => entry.role === "operator");
      expect(presentation?.surface).toBe("guestScreenActive");
    });

    await test.step("the hold and PIN on the Guest Screen return to the Event list", async () => {
      const guest = await guestPage(app);
      const start = new GuestStartScreen(guest);
      const dialog = new PinDialogScreen(guest);
      await holdFor(start.exitHold, exitHoldMs);
      await expect(dialog.dialog).toBeVisible();
      await dialog.submitPin(pin);
      await expectEventEnded(app);
      await expect.poll(() => guest.isClosed()).toBe(true);
      await expect(list.main).toBeVisible();
      await expect(list.runningPanel()).toHaveCount(0);
      await expect(list.row(name)).toBeVisible();
      await expect(covered.main).toHaveCount(0);
    });
  });
});
