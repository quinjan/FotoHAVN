import path from "node:path";
import type { Locator } from "@playwright/test";
import { queryOne } from "../../../src/data";
import { expect, test } from "../../../src/fixtures";
import { openNewEventAndFill, useVirtualScreens } from "../../../src/operator";
import { ConfirmDialogScreen, EventListScreen, type EventSetupScreen } from "../../../src/screens/screens";

const oneThing = "One thing needs attention before this Event can be saved.";
const baseName = "Validation fete";

const templateNames = ["Signature Strip", "Panorama Four", "Butter Gingham", "Polka Keepsake"] as const;
const lookNames = ["Monochrome", "Naturale", "Soft Monochrome", "Soft Color", "Sepia", "Warm Monochrome", "Classic"] as const;

interface Rule {
  /** The control that carries the error text in its description. */
  readonly field: Locator;
  readonly text: RegExp;
  /** The control that takes focus, when it isn't the field itself (a group's first control). */
  readonly focus?: Locator;
  /** Exactly one thing needs attention (default true). */
  readonly single?: boolean;
}

function storedEventCount(dataDir: string): number {
  return queryOne<{ total: number }>(path.join(dataDir, "fotohavn.db"), "SELECT COUNT(*) AS total FROM events")?.total ?? 0;
}

/** The browser stops typing at a field's limit; this lifts the clamp so the brain's own length rule is what's tested. */
async function liftLimit(field: Locator): Promise<void> {
  await field.evaluate((element) => {
    element.removeAttribute("maxlength");
  });
}

test.describe("Event setup validation", { tag: ["@m6", "@camera"] }, () => {
  test("each required field and range rule blocks SAVE with its message, an error summary that takes focus, and a link to the first invalid field", async ({
    app,
    operatorPage,
    camera,
    dataDir,
  }) => {
    await useVirtualScreens(app, 2);
    const context = { app, page: operatorPage, camera };
    const list = new EventListScreen(operatorPage);
    await expect(list.empty).toBeVisible();

    /** SAVE (on the Guests step) is answered with the field's error text and the error summary focused; its first link opens the field's step and focuses the first invalid control; nothing is saved. */
    async function expectBlocked(setup: EventSetupScreen, rule: Rule): Promise<void> {
      await setup.goTo("Guests");
      await setup.save.click();
      await expect(setup.errorSummary).toBeFocused();
      await expect(rule.field).toHaveAccessibleDescription(rule.text);
      await setup.errorSummary.getByRole("button").first().click();
      await expect(rule.focus ?? rule.field).toBeFocused();
      if (rule.single !== false) {
        await expect(setup.statusLine(oneThing)).toBeVisible();
      }
      await expect(setup.main).toBeVisible();
      await expect(list.main).toHaveCount(0);
      expect(storedEventCount(dataDir), "nothing is saved").toBe(0);
    }

    async function expectValid(setup: EventSetupScreen): Promise<void> {
      await expect(setup.statusLine(/needs? attention/)).toHaveCount(0);
    }

    await test.step("no camera: SAVE is blocked on the Camera field", async () => {
      const setup = await openNewEventAndFill(context, { name: baseName, camera: false, display: "Virtual guest", pin: "4821" });
      await expectBlocked(setup, { field: setup.camera, text: /Choose a camera\./ });
      await expect(setup.camera).toHaveAttribute("aria-invalid", "true");
      await setup.back.click();
      await new ConfirmDialogScreen(operatorPage).button("DISCARD CHANGES").click();
      await expect(list.main).toBeVisible();
    });

    const setup = await test.step("no display: SAVE is blocked on the display choice; then the display is chosen", async () => {
      const opened = await openNewEventAndFill(context, { name: baseName, pin: "4821" });
      await expectBlocked(opened, {
        field: opened.displayGroup,
        text: /Choose the guest display\./,
        focus: opened.display("Virtual operator"),
      });
      await opened.display("Virtual guest").click();
      await expect(opened.display("Virtual guest")).toBeChecked();
      await expectValid(opened);
      return opened;
    });

    await test.step("empty name", async () => {
      await setup.goTo("Event");
      await setup.name.fill("");
      await expectBlocked(setup, { field: setup.name, text: /Name the Event\./ });
      await expect(setup.name).toHaveAttribute("aria-invalid", "true");
      await setup.name.fill(baseName);
      await expectValid(setup);
    });

    await test.step("a name of 81 characters (the field stops typing at 80; the brain checks it again)", async () => {
      await setup.name.fill("n".repeat(81));
      await expect(setup.name).toHaveValue("n".repeat(80));
      await liftLimit(setup.name);
      await setup.name.fill("n".repeat(81));
      await expectBlocked(setup, { field: setup.name, text: /Keep the name to 80 characters or fewer\./ });
      await setup.name.fill(baseName);
      await expectValid(setup);
    });

    await test.step("a date that is cleared", async () => {
      await setup.date.fill("");
      await expectBlocked(setup, { field: setup.date, text: /This is needed\./ });
      await setup.date.fill("2026-06-12");
      await expectValid(setup);
    });

    await test.step("a venue of 121 characters", async () => {
      await liftLimit(setup.venue);
      await setup.venue.fill("v".repeat(121));
      await expectBlocked(setup, { field: setup.venue, text: /Keep the venue to 120 characters or fewer\./ });
      await setup.venue.fill("Harbour Hall");
      await expectValid(setup);
    });

    await test.step("a customer or host of 121 characters", async () => {
      await liftLimit(setup.customer);
      await setup.customer.fill("c".repeat(121));
      await expectBlocked(setup, { field: setup.customer, text: /Keep this to 120 characters or fewer\./ });
      await setup.customer.fill("Lindqvist & Co");
      await expectValid(setup);
    });

    await test.step("notes of 1001 characters", async () => {
      await liftLimit(setup.notes);
      await setup.notes.fill("k".repeat(1001));
      await expectBlocked(setup, { field: setup.notes, text: /Keep the notes to 1,000 characters or fewer\./ });
      await setup.notes.fill("Bring the props.");
      await expectValid(setup);
    });

    await test.step("no Template offered", async () => {
      await setup.goTo("Templates");
      for (const name of templateNames) {
        await setup.template(name).click();
        await expect(setup.template(name)).not.toBeChecked();
      }
      await expectBlocked(setup, {
        field: setup.templates,
        text: /Offer at least one Template\./,
        focus: setup.template("Signature Strip"),
        single: false,
      });
      for (const name of templateNames) {
        await setup.template(name).click();
        await expect(setup.template(name)).toBeChecked();
      }
      await setup.defaultTemplate("Signature Strip").click();
      await expect(setup.defaultTemplate("Signature Strip")).toBeChecked();
      await expectValid(setup);
    });

    await test.step("the default Template is not offered", async () => {
      await setup.template("Signature Strip").click();
      await expect(setup.template("Signature Strip")).not.toBeChecked();
      await expectBlocked(setup, {
        field: setup.templates,
        text: /Choose the default from the Templates you offer\./,
        focus: setup.template("Signature Strip"),
      });
      await setup.template("Signature Strip").click();
      await setup.defaultTemplate("Signature Strip").click();
      await expect(setup.defaultTemplate("Signature Strip")).toBeChecked();
      await expectValid(setup);
    });

    await test.step("no Look offered", async () => {
      for (const name of lookNames) {
        await setup.look(name).click();
        await expect(setup.look(name)).not.toBeChecked();
      }
      await expectBlocked(setup, {
        field: setup.looks,
        text: /Offer at least one Look\./,
        focus: setup.look("Monochrome"),
        single: false,
      });
      for (const name of lookNames) {
        await setup.look(name).click();
        await expect(setup.look(name)).toBeChecked();
      }
      await setup.defaultLook("Naturale").click();
      await expect(setup.defaultLook("Naturale")).toBeChecked();
      await expectValid(setup);
    });

    await test.step("the default Look is not offered", async () => {
      await setup.look("Naturale").click();
      await expect(setup.look("Naturale")).not.toBeChecked();
      await expectBlocked(setup, {
        field: setup.looks,
        text: /Choose the default from the Looks you offer\./,
        focus: setup.look("Monochrome"),
      });
      await setup.look("Naturale").click();
      await setup.defaultLook("Naturale").click();
      await expect(setup.defaultLook("Naturale")).toBeChecked();
      await expectValid(setup);
    });

    await test.step("a Template timer out of range (10 s)", async () => {
      await setup.goTo("Timing");
      const timer = setup.timer("Template");
      await timer.fill("10");
      await expectBlocked(setup, { field: timer, text: /Enter 20 to 300 seconds\./ });
      await timer.fill("30");
      await expectValid(setup);
    });

    await test.step("Keepsake off (0) is not allowed", async () => {
      const timer = setup.timer("Keepsake");
      await timer.fill("0");
      await expectBlocked(setup, { field: timer, text: /Enter 5 to 60 seconds\./ });
      await timer.fill("12");
      await expectValid(setup);
    });

    await test.step("mismatched PINs", async () => {
      await setup.goTo("Guests");
      await setup.pin.fill("4821");
      await setup.pinConfirmation.fill("4822");
      await expectBlocked(setup, { field: setup.pinConfirmation, text: /The two PINs don't match\./ });
      await setup.pinConfirmation.fill("4821");
      await expectValid(setup);
    });

    await test.step("a PIN of 3 digits", async () => {
      await setup.pin.fill("482");
      await setup.pinConfirmation.fill("482");
      await expectBlocked(setup, { field: setup.pin, text: /Use 4 to 6 digits, 0 to 9 only\./ });
      await setup.pin.fill("4821");
      await setup.pinConfirmation.fill("4821");
      await expectValid(setup);
    });

    await test.step("with every rule met, SAVE goes back to the Event list with the Event", async () => {
      await setup.goTo("Guests");
      await setup.save.click();
      await expect(list.row(baseName)).toBeVisible();
      expect(storedEventCount(dataDir)).toBe(1);
    });
  });
});
