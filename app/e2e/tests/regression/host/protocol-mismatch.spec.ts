import { expect, test } from "../../../src/fixtures";
import { sendCommand, sendHook } from "../../../src/hooks";
import { appOrigin } from "../../../src/launch";
import { EventListScreen, ProtocolMismatchScreen } from "../../../src/screens/screens";

test.describe("protocol mismatch", { tag: ["@m2"] }, () => {
  test("a UI started with a wrong protocol version shows the fixed error screen", async ({ operatorPage }) => {
    await test.step("the Operator page restarts with ?e2eProtocolVersion=0", async () => {
      await operatorPage.goto(`${appOrigin}index.html?e2eProtocolVersion=0`);
    });

    await test.step("the brain answers protocolMismatch and the page shows the fixed error screen", async () => {
      const screen = new ProtocolMismatchScreen(operatorPage);
      await expect(operatorPage.locator("html")).toHaveAttribute("data-fotohavn-status", "protocolMismatch");
      await expect(operatorPage.locator("html")).not.toHaveAttribute("data-fotohavn-role", /.+/);
      await expect(screen.heading).toBeVisible();
      await expect(screen.main.getByText("This screen and the booth are out of step. Restart FOTOHAVN to fix it.")).toBeVisible();
      await expect(screen.versions).toHaveText("Booth version 9 · Screen version 0");
      await expect(operatorPage.getByText("No Events yet.")).toHaveCount(0);
    });

    await test.step("the connection isn't handshaken: commands are rejected protocolMismatch", async () => {
      expect(await sendCommand(operatorPage, "OpenNewEvent", {})).toEqual({ status: "rejected", reason: "protocolMismatch" });
      const diagnostics = await sendHook(operatorPage, "test.getDiagnostics", null);
      expect(diagnostics.ok && diagnostics.result.connectedRoles).toEqual([]);
    });

    await test.step("a page with the right version handshakes again", async () => {
      await operatorPage.goto(`${appOrigin}index.html`);
      await expect(operatorPage.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");
      await expect(new EventListScreen(operatorPage).empty).toBeVisible();
    });
  });
});
