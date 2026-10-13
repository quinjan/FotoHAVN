import { expect, test } from "../../src/fixtures";
import { EventListScreen } from "../../src/screens/screens";

test("the operator handshake completes and the empty Event list renders", { tag: ["@m2"] }, async ({ app, operatorPage, hooks }) => {
  await test.step("the Operator page completed its handshake as operator once storage opened (Starting is revision 1, the Event list revision 2)", async () => {
    const root = operatorPage.locator("html");
    await expect(root).toHaveAttribute("data-fotohavn-status", "connected");
    await expect(root).toHaveAttribute("data-fotohavn-role", "operator");
    await expect.poll(async () => (await hooks.getDiagnostics()).storage.state).toBe("ready");
    await expect(root).toHaveAttribute("data-fotohavn-revision", "2");

    const diagnostics = await hooks.getDiagnostics();
    expect(diagnostics.connectedRoles).toEqual(["operator"]);
    expect(diagnostics.presentations).toContainEqual({ role: "operator", revision: 2, surface: "eventList" });
    expect(diagnostics.guestScreenOpen).toBe(false);
  });

  await test.step('the empty Event list renders "No Events yet."', async () => {
    const screen = new EventListScreen(operatorPage);
    await expect(screen.heading).toBeVisible();
    await expect(screen.empty).toBeVisible();
    await expect(screen.newEvent).toBeVisible();
  });

  await test.step("NEW EVENT is answered by the brain (not available until M6)", async () => {
    const screen = new EventListScreen(operatorPage);
    await screen.newEvent.click();
    await expect(screen.notice).toHaveText("That isn't ready yet.");
    const diagnostics = await hooks.getDiagnostics();
    expect(diagnostics.lastCommandResults.at(-1)).toMatchObject({
      name: "OpenNewEvent",
      role: "operator",
      status: "rejected",
      reason: "notAvailable",
    });
  });

  await test.step("the app exits cleanly", async () => {
    expect((await app.close()).code).toBe(0);
  });
});
