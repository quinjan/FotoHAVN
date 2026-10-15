import path from "node:path";
import { queryOne } from "../../../src/data";
import { expect, test } from "../../../src/fixtures";
import { createEvent, eventIdByName, useVirtualScreens } from "../../../src/operator";
import { ConfirmDialogScreen, EventListScreen, EventSetupScreen } from "../../../src/screens/screens";

function storedName(dataDir: string, id: string): string | undefined {
  return queryOne<{ name: string }>(path.join(dataDir, "fotohavn.db"), "SELECT name FROM events WHERE id = ?", id)?.name;
}

test.describe("editing an Event and leaving Event setup", { tag: ["@m6"] }, () => {
  test("KEEP EDITING stays with the change, DISCARD CHANGES returns with the old name, and SAVE returns with the new name", async ({
    app,
    operatorPage,
    camera,
    dataDir,
  }) => {
    await useVirtualScreens(app, 2);
    const list = new EventListScreen(operatorPage);
    const setup = new EventSetupScreen(operatorPage);
    const dialog = new ConfirmDialogScreen(operatorPage);
    const oldName = "Quayside open day";
    const newName = "Quayside open evening";

    const id = await createEvent({ app, page: operatorPage, camera }, { name: oldName, display: "Virtual guest" });

    await test.step("opening an Event and leaving without a change returns to the list at once", async () => {
      await list.rowAction(oldName, "EDIT").click();
      await expect(setup.heading).toBeVisible();
      await expect(setup.name).toHaveValue(oldName);
      await setup.back.click();
      await expect(list.row(oldName)).toBeVisible();
      await expect(dialog.dialog).toHaveCount(0);
    });

    await test.step("change the name and leave: the dialog asks, and KEEP EDITING stays with the change", async () => {
      await list.rowAction(oldName, "EDIT").click();
      await setup.name.fill(newName);
      await setup.back.click();
      await expect(dialog.dialog).toBeVisible();
      await expect(dialog.title).toHaveText("Save your changes?");
      await expect(dialog.button("SAVE")).toBeVisible();
      await expect(dialog.button("DISCARD CHANGES")).toBeVisible();
      await dialog.button("KEEP EDITING").click();
      await expect(dialog.dialog).toHaveCount(0);
      await expect(setup.main).toBeVisible();
      await expect(setup.name).toHaveValue(newName);
      expect(storedName(dataDir, id)).toBe(oldName);
    });

    await test.step("DISCARD CHANGES returns to the list with the old name, in the store too", async () => {
      await setup.back.click();
      await dialog.button("DISCARD CHANGES").click();
      await expect(list.row(oldName)).toBeVisible();
      await expect(list.row(newName)).toHaveCount(0);
      expect(storedName(dataDir, id)).toBe(oldName);
    });

    await test.step("SAVE returns to the list with the new name, in the store too", async () => {
      await list.rowAction(oldName, "EDIT").click();
      await expect(setup.name).toHaveValue(oldName);
      await setup.name.fill(newName);
      await setup.back.click();
      await dialog.button("SAVE").click();
      await expect(list.row(newName)).toBeVisible();
      await expect(list.row(oldName)).toHaveCount(0);
      expect(storedName(dataDir, id)).toBe(newName);
      expect(eventIdByName(dataDir, newName)).toBe(id);
    });
  });
});
