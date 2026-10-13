import { expect, test } from "../../../src/fixtures";
import { openGuestOnVirtualDisplays } from "../../../src/displays";
import { sendCommand } from "../../../src/hooks";

test.describe("role isolation", { tag: ["@m2"] }, () => {
  test("a guest page sending an operator command (OpenNewEvent) gets rejected wrongRole", async ({ app, operatorPage, hooks }) => {
    const guest = await openGuestOnVirtualDisplays(app);

    await test.step("the guest page's OpenNewEvent is rejected wrongRole", async () => {
      expect(await sendCommand(guest.page, "OpenNewEvent", {})).toEqual({ status: "rejected", reason: "wrongRole" });
      const last = (await hooks.getDiagnostics()).lastCommandResults.at(-1);
      expect(last).toMatchObject({ name: "OpenNewEvent", role: "guest", status: "rejected", reason: "wrongRole" });
    });

    await test.step("the same command from the Operator page reaches the brain's rules (notAvailable in M2)", async () => {
      expect(await sendCommand(operatorPage, "OpenNewEvent", {})).toEqual({ status: "rejected", reason: "notAvailable" });
    });

    await test.step("an operator-only command can't change the guest page's role or presentation", async () => {
      expect(await sendCommand(guest.page, "EditEvent", { eventId: "event-1" })).toEqual({ status: "rejected", reason: "wrongRole" });
      await expect(guest.page.locator("html")).toHaveAttribute("data-fotohavn-role", "guest");
      await expect(guest.page.locator("html")).toHaveAttribute("data-fotohavn-revision", "1");
      const diagnostics = await hooks.getDiagnostics();
      expect(diagnostics.presentations).toEqual([
        { role: "operator", revision: 1, surface: "eventList" },
        { role: "guest", revision: 1, surface: "start" },
      ]);
    });
  });
});
