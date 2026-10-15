import { size, viewport } from "../../../src/displays";
import { expect, test } from "../../../src/fixtures";
import { createEvent, endEventFromOperator, guestPage } from "../../../src/operator";
import { EventListScreen, GuestStartScreen } from "../../../src/screens/screens";

test.describe("the operator flow on two real displays", { tag: ["@m6", "@physical-displays"] }, () => {
  test("an Event created for the second real display starts there with Start, and ends with END EVENT", async ({
    app,
    operatorPage,
    camera,
    hooks,
  }) => {
    const real = (await hooks.getDiagnostics()).displays.displays;
    test.skip(real.length < 2, "not run: one display");

    const second = real.find((display) => !display.isPrimary);
    expect(second, JSON.stringify(real)).toBeDefined();
    if (second === undefined) {
      return;
    }
    const list = new EventListScreen(operatorPage);
    const name = "Two real displays";

    await createEvent(
      { app, page: operatorPage, camera },
      { name, venue: "Harbour Hall", customer: "Lindqvist & Co", notes: "Bring the props.", display: second.name, pin: "4821" },
      "saveAndStart",
    );

    const guest = await guestPage(app);
    await expect(new GuestStartScreen(guest).stepInside).toBeVisible();
    const diagnostics = await hooks.getDiagnostics();
    expect(diagnostics.displays.virtual).toBe(false);
    const guestWindow = diagnostics.windows.find((window) => window.role === "guest");
    expect(guestWindow?.bounds).toEqual(second.bounds);
    expect(guestWindow?.displayId).toBe(second.id);
    expect(await viewport(guest)).toEqual({ ...size(second.bounds), devicePixelRatio: 1 });
    await expect(list.runningPanel(name)).toBeVisible();
    await expect(list.runningFact("display")).toHaveText("Connected");

    await endEventFromOperator(app, operatorPage);
    expect((await hooks.getDiagnostics()).guestScreenOpen).toBe(false);
  });
});
