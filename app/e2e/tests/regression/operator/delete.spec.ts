import { existsSync } from "node:fs";
import path from "node:path";
import { queryOne } from "../../../src/data";
import { expect, test } from "../../../src/fixtures";
import { eventFolder } from "../../../src/operator";
import { operatorSeed, seedOperatorEvents } from "../../../src/seed";
import { ConfirmDialogScreen, EventListScreen } from "../../../src/screens/screens";

test.use({ appOptions: { seed: seedOperatorEvents } });

function storedEvents(dataDir: string, id: string): number {
  return queryOne<{ total: number }>(path.join(dataDir, "fotohavn.db"), "SELECT COUNT(*) AS total FROM events WHERE id = ?", id)?.total ?? 0;
}

test.describe("deleting an Event", { tag: ["@m6"] }, () => {
  test("the confirmation names the Event and what goes with it, the row disappears at once, and the folder and the store row are gone after cleanup", async ({
    operatorPage,
    dataDir,
  }) => {
    const list = new EventListScreen(operatorPage);
    const dialog = new ConfirmDialogScreen(operatorPage);
    const { id, name } = operatorSeed.seaside;
    const folder = eventFolder(dataDir, id);

    await expect(list.rows).toHaveCount(3);
    expect(existsSync(folder), "the seeded Event has a folder").toBe(true);

    await test.step("DELETE asks first: the Event's name and the counts", async () => {
      await list.rowAction(name, "DELETE").click();
      await expect(dialog.dialog).toBeVisible();
      await expect(dialog.title).toHaveText("Delete this Event?");
      await expect(dialog.dialog).toContainText(`“${name}” will be removed with 2 Guest Cycles, 2 Photo Strips, and 5 photographs. This can't be undone.`);
    });

    await test.step("KEEP EVENT closes the dialog and deletes nothing", async () => {
      await dialog.button("KEEP EVENT").click();
      await expect(dialog.dialog).toHaveCount(0);
      await expect(list.row(name)).toBeVisible();
      expect(existsSync(folder)).toBe(true);
      expect(storedEvents(dataDir, id)).toBe(1);
    });

    await test.step("DELETE EVENT removes the row at once", async () => {
      await list.rowAction(name, "DELETE").click();
      await expect(dialog.dialog).toContainText(name);
      await dialog.button("DELETE EVENT").click();
      await expect(dialog.dialog).toHaveCount(0);
      await expect(list.row(name)).toHaveCount(0, { timeout: 3_000 });
      await expect(list.rows).toHaveCount(2);
      await expect(list.row(operatorSeed.harbour.name)).toBeVisible();
      await expect(list.row(operatorSeed.studio.name)).toBeVisible();
    });

    await test.step("the store cleans up in the background: the folder and the row are gone within 10 s", async () => {
      await expect.poll(() => existsSync(folder), { timeout: 10_000, message: `waiting for ${folder} to be removed` }).toBe(false);
      await expect.poll(() => storedEvents(dataDir, id), { timeout: 10_000 }).toBe(0);
      expect(storedEvents(dataDir, operatorSeed.harbour.id)).toBe(1);
      await expect(list.row(name)).toHaveCount(0);
    });
  });
});
