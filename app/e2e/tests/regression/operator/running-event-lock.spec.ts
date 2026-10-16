import { expect, test } from "../../../src/fixtures";
import { OperatorGalleryScreen } from "../../../src/gallery";
import { sendCommand } from "../../../src/hooks";
import { createEvent, endEventFromOperator, openNewEventAndFill, useVirtualScreens } from "../../../src/operator";
import { ConfirmDialogScreen, EventListScreen, EventSetupScreen } from "../../../src/screens/screens";

const reason = "End the running Event to start another.";

test.describe("the running Event's lock", { tag: ["@m6", "@camera"] }, () => {
  test("while Event A runs, its row offers only Gallery and Open folder, Start is disabled elsewhere, and A can't be edited, but Event B can be created, edited with the same camera, and deleted", async ({
    app,
    operatorPage,
    camera,
  }) => {
    await useVirtualScreens(app, 2);
    const context = { app, page: operatorPage, camera };
    const list = new EventListScreen(operatorPage);
    const setup = new EventSetupScreen(operatorPage);
    const dialog = new ConfirmDialogScreen(operatorPage);
    const gallery = new OperatorGalleryScreen(operatorPage);
    const nameA = "Event A";
    const nameB = "Event B";

    const idA = await test.step("start Event A", () =>
      createEvent(context, { name: nameA, display: "Virtual guest", pin: "4821" }, "saveAndStart"));

    await test.step("A's row offers only Gallery and Open folder", async () => {
      const row = list.row(nameA);
      await expect(row).toContainText("RUNNING");
      await expect(row.getByRole("button")).toHaveText(["GALLERY", "OPEN FOLDER"]);
      await expect(list.rowAction(nameA, "START")).toHaveCount(0);
      await expect(list.rowAction(nameA, "EDIT")).toHaveCount(0);
      await expect(list.rowAction(nameA, "DELETE")).toHaveCount(0);
    });

    await test.step("A's commands are rejected by the brain with eventRunning, from the Operator page", async () => {
      expect(await sendCommand(operatorPage, "EditEvent", { eventId: idA })).toEqual({ status: "rejected", reason: "eventRunning" });
      expect(await sendCommand(operatorPage, "DeleteEvent", { eventId: idA })).toEqual({ status: "rejected", reason: "eventRunning" });
      expect(await sendCommand(operatorPage, "StartEvent", { eventId: idA })).toEqual({ status: "rejected", reason: "eventRunning" });
      await expect(list.main).toBeVisible();
      await expect(dialog.dialog).toHaveCount(0);
    });

    await test.step("Event B can be created while A runs: its camera field says it is in use by the running Event", async () => {
      await openNewEventAndFill(context, { name: nameB, camera: false, display: "Virtual guest" });
      await setup.camera.selectOption({ label: camera.name });
      await expect(setup.cameraStatus).toContainText("In use by the running Event");
      await expect(setup.camera.locator("option:checked")).toHaveText(camera.name);
      await expect(setup.saveAndStart).toHaveAttribute("aria-disabled", "true");
      await expect(setup.main.getByText(reason, { exact: true })).toBeVisible();
      await setup.save.click();
      await expect(list.row(nameB)).toBeVisible();
    });

    await test.step("Start is disabled on every row that offers it, with the reason", async () => {
      const start = list.rowAction(nameB, "START");
      await expect(start).toHaveAttribute("aria-disabled", "true");
      await expect(list.rowReason(nameB)).toBeVisible();
      const startedBefore = (await app.hooks.getDiagnostics()).guestScreenOpen;
      await start.click({ force: true });
      await expect(list.main).toBeVisible();
      await expect(list.heading).toBeVisible();
      expect((await app.hooks.getDiagnostics()).guestScreenOpen).toBe(startedBefore);
    });

    await test.step("GALLERY opens the running Event's gallery, live and empty, and BACK TO EVENTS returns", async () => {
      await list.rowAction(nameA, "GALLERY").click();
      await expect(gallery.main).toBeVisible();
      await expect(gallery.main.getByText("RUNNING", { exact: true })).toBeVisible();
      await expect(gallery.empty).toBeVisible();
      await gallery.backToEvents.click();
      await expect(list.main).toBeVisible();
    });

    await test.step("GALLERY on Event B, which doesn't run, opens its gallery too, not live", async () => {
      await list.rowAction(nameB, "GALLERY").click();
      await expect(gallery.main).toBeVisible();
      await expect(gallery.main.getByText("RUNNING", { exact: true })).toHaveCount(0);
      await expect(gallery.empty).toBeVisible();
      await gallery.backToEvents.click();
      await expect(list.main).toBeVisible();
    });

    await test.step("Event B can be edited while A runs, keeping the same camera", async () => {
      await list.rowAction(nameB, "EDIT").click();
      await expect(setup.heading).toBeVisible();
      await expect(setup.name).toHaveValue(nameB);
      await expect(setup.camera.locator("option:checked")).toHaveText(camera.name);
      await expect(setup.cameraStatus).toContainText("In use by the running Event");
      await setup.name.fill("Event B, renamed");
      await setup.save.click();
      await expect(list.row("Event B, renamed")).toBeVisible();
      await expect(list.row(nameB)).toHaveCount(0);
    });

    await test.step("Event B can be deleted while A runs", async () => {
      await list.rowAction("Event B, renamed", "DELETE").click();
      await expect(dialog.dialog).toBeVisible();
      await expect(dialog.dialog).toContainText("Event B, renamed");
      await dialog.button("DELETE EVENT").click();
      await expect(list.row("Event B, renamed")).toHaveCount(0);
      await expect(list.runningPanel(nameA)).toBeVisible();
    });

    await test.step("end A afterwards", () => endEventFromOperator(app, operatorPage));
  });
});
