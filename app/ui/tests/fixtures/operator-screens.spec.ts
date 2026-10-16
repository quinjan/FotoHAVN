import { resolve } from "node:path";
import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";
import { resolutionName, resolutions, screenshotDir } from "./resolutions.ts";

// The operator screens of M6 through the fixture lane: the running-Event panel, the confirmations over the Event list,
// and Event setup in each state of its fields (src/fixtures/operatorFixtures.ts and operatorSetupFixtures.ts). Each is
// checked at four resolutions: the operator minimum target size, at most one primary action, no horizontal scrolling
// (the Operator Screen may scroll vertically), and axe; screenshots land in ../output/fixtures/ for review.

interface OperatorScreen {
  readonly fixture: string;
  readonly surface: string;
  /** Text that must be on the screen. */
  readonly text: readonly string[];
}

const screens: readonly OperatorScreen[] = [
  { fixture: "operator-list-running", surface: "eventList", text: ["RUNNING", "GUEST DISPLAY", "Connected", "OPEN GALLERY", "END EVENT", "End the running Event to start another."] },
  { fixture: "operator-list-running-display-disconnected", surface: "eventList", text: ["Disconnected"] },
  { fixture: "operator-list-running-camera-not-available", surface: "eventList", text: ["Not available"] },
  { fixture: "operator-list-running-camera-checking", surface: "eventList", text: ["Checking"] },
  { fixture: "operator-list-running-assistance", surface: "eventList", text: ["The guest display isn't connected.", "RETRY", "NEW EVENT"] },
  { fixture: "operator-list-confirm-delete", surface: "eventList", text: ["Delete this Event?", "12 Guest Cycles, 11 Photo Strips, and 41 photographs", "KEEP EVENT"] },
  { fixture: "operator-list-confirm-end", surface: "eventList", text: ["End this Event?", "Their photographs are kept.", "KEEP RUNNING"] },
  { fixture: "operator-list-confirm-close", surface: "eventList", text: ["End the running Event and close FOTOHAVN?", "Their photographs are kept.", "END EVENT AND CLOSE", "KEEP RUNNING"] },
  { fixture: "operator-list-notice", surface: "eventList", text: ["The Event couldn't be deleted."] },
  { fixture: "operator-single-screen", surface: "guestScreenActive", text: ["The booth is running on this screen."] },
  { fixture: "operator-setup-new", surface: "eventSetup", text: ["Name", "Camera", "OPERATOR PIN", "SAVE AND START"] },
  { fixture: "operator-setup-new-errors", surface: "eventSetup", text: ["Name the Event.", "Choose a camera.", "Choose the guest display.", "Enter 5 to 60 seconds.", "5 things need attention before this Event can be saved."] },
  { fixture: "operator-setup-errors-offered", surface: "eventSetup", text: ["Keep the name to 80 characters or fewer.", "Offer at least one Template.", "The two PINs don't match."] },
  { fixture: "operator-setup-editing", surface: "eventSetup", text: ["PIN set", "Off now."] },
  { fixture: "operator-setup-camera-checking", surface: "eventSetup", text: ["Checking"] },
  { fixture: "operator-setup-camera-not-available", surface: "eventSetup", text: ["Camera access is off in Windows privacy settings."] },
  { fixture: "operator-setup-camera-in-use", surface: "eventSetup", text: ["In use by another app"] },
  { fixture: "operator-setup-camera-in-use-by-running-event", surface: "eventSetup", text: ["In use by the running Event", "End the running Event to start another."] },
  { fixture: "operator-setup-camera-not-checked", surface: "eventSetup", text: ["Not checked while an Event is running."] },
  { fixture: "operator-setup-camera-missing", surface: "eventSetup", text: ["This camera isn't connected."] },
  { fixture: "operator-setup-camera-none-found", surface: "eventSetup", text: ["No camera found.", "Connect a camera and it will appear here."] },
  { fixture: "operator-setup-display-missing", surface: "eventSetup", text: ["The chosen guest display isn't connected.", "Not connected"] },
  { fixture: "operator-setup-covers-operator-screen", surface: "eventSetup", text: ["The Guest Screen will cover this screen. Use the hold and PIN to return."] },
  { fixture: "operator-setup-leave-confirmation", surface: "eventSetup", text: ["Save your changes?", "DISCARD CHANGES", "KEEP EDITING"] },
  { fixture: "operator-setup-start-failure-display", surface: "eventSetup", text: ["The chosen guest display isn't connected."] },
  { fixture: "operator-setup-start-failure-camera", surface: "eventSetup", text: ["The camera isn't ready to start. The camera stopped sending pictures."] },
  { fixture: "operator-setup-start-failure-pin", surface: "eventSetup", text: ["Set an Operator PIN to start this Event."] },
  { fixture: "operator-setup-start-failure-guest-screen", surface: "eventSetup", text: ["The guest display didn't respond. Check it, then try again."] },
  { fixture: "operator-setup-assistance", surface: "eventSetup", text: ["The camera needs attention.", "GO TO THE RUNNING EVENT"] },
  { fixture: "operator-setup-saving", surface: "eventSetup", text: ["Saving…"] },
  { fixture: "operator-setup-pin-changing", surface: "eventSetup", text: ["New PIN", "KEEP CURRENT PIN"] },
];

async function expectNoHorizontalScrolling(page: Page): Promise<void> {
  expect(await page.evaluate(() => document.documentElement.scrollWidth > document.documentElement.clientWidth)).toBe(false);
}

/** Every visible control meets the Operator Screen's 48 × 48 minimum. */
async function expectTargets(page: Page, fixture: string): Promise<void> {
  const small = await page.evaluate(() => {
    const controls = document.querySelectorAll<HTMLElement>(
      "main button, main a[href], main input, main select, main textarea, main [role='button'], main [tabindex]:not([tabindex='-1'])",
    );
    return [...controls]
      .map((control) => ({ control, box: control.getBoundingClientRect() }))
      .filter(({ box }) => box.width > 0 && box.height > 0)
      .filter(({ box }) => box.width + 0.5 < 48 || box.height + 0.5 < 48)
      .map(({ control, box }) => `${control.textContent.trim()} ${String(Math.round(box.width))}x${String(Math.round(box.height))}`);
  });
  expect(small, `${fixture} controls smaller than 48 x 48`).toEqual([]);
}

for (const resolution of resolutions) {
  const name = resolutionName(resolution);

  test.describe(`operator screens at ${name}`, () => {
    test.use({ viewport: resolution });

    for (const screen of screens) {
      test(screen.fixture, async ({ page }) => {
        await page.goto(`/index.html?fixture=${screen.fixture}`);
        await expect(page.locator("html")).toHaveAttribute("data-fotohavn-role", "operator");
        await expect(page.locator(`main[data-fotohavn-surface="${screen.surface}"]`)).toBeVisible();
        for (const text of screen.text) {
          await expect(page.getByText(text).first()).toBeVisible();
        }
        await expectNoHorizontalScrolling(page);
        await expectTargets(page, screen.fixture);
        // Content behind a dialog is inert, so only what can take focus counts as the view's primary actions.
        const primaries = await page.evaluate(
          () => [...document.querySelectorAll('main [data-fotohavn-action="primary"]')].filter((action) => action.closest("[inert]") === null).length,
        );
        expect(primaries, `${screen.fixture} primary actions`).toBeLessThanOrEqual(1);
        await page.screenshot({ path: resolve(screenshotDir, `${screen.fixture}-${name}.png`), fullPage: true });
        const axe = await new AxeBuilder({ page }).analyze();
        expect(axe.violations, `${screen.fixture} axe violations`).toEqual([]);
      });
    }
  });
}

test.describe("operator screens, behaviour", () => {
  test.use({ viewport: resolutions[0] });

  test("the running row offers only Gallery and Open folder, and the others show why Start is unavailable", async ({ page }) => {
    await page.goto("/index.html?fixture=operator-list-running");
    const rows = page.getByRole("list", { name: "Events" }).getByRole("listitem");
    const running = rows.first();
    await expect(running).toContainText("RUNNING");
    await expect(running.getByRole("button")).toHaveText(["GALLERY", "OPEN FOLDER"]);
    const other = rows.nth(1);
    await expect(other.getByRole("button")).toHaveText(["START", "GALLERY", "EDIT", "OPEN FOLDER", "DELETE"]);
    await expect(other.getByRole("button", { name: /^Start/ })).toHaveAttribute("aria-disabled", "true");
    await expect(other).toContainText("End the running Event to start another.");
  });

  test("a row action the brain declines shows a polite notice", async ({ page }) => {
    await page.goto("/index.html?fixture=operator-list-running");
    await page.getByRole("button", { name: /^Edit/ }).first().click();
    await expect(page.getByRole("status").filter({ hasText: "That isn't ready yet." })).toBeVisible();
  });

  test("a confirmation traps focus, makes the content behind it inert, and puts focus on the choice that loses nothing", async ({ page }) => {
    await page.goto("/index.html?fixture=operator-list-confirm-end");
    const dialog = page.getByRole("alertdialog", { name: "End this Event?" });
    await expect(dialog).toBeVisible();
    await expect(page.getByRole("button", { name: "KEEP RUNNING" })).toBeFocused();
    await expect(page.locator("main > div[inert]")).toHaveCount(1);
    await page.keyboard.press("Tab");
    expect(await page.evaluate(() => document.activeElement?.closest("[role='alertdialog']") !== null)).toBe(true);
    await page.keyboard.press("Tab");
    await page.keyboard.press("Tab");
    expect(await page.evaluate(() => document.activeElement?.closest("[role='alertdialog']") !== null)).toBe(true);
  });

  test("the leave confirmation offers SAVE, DISCARD CHANGES, and KEEP EDITING with focus on KEEP EDITING", async ({ page }) => {
    await page.goto("/index.html?fixture=operator-setup-leave-confirmation");
    await expect(page.getByRole("alertdialog", { name: "Save your changes?" })).toBeVisible();
    await expect(page.getByRole("button", { name: "KEEP EDITING" })).toBeFocused();
    await expect(page.getByRole("alertdialog").getByRole("button")).toHaveText(["SAVE", "DISCARD CHANGES", "KEEP EDITING"]);
  });

  test("Event setup keeps SAVE AND START unavailable with its reason while an Event runs", async ({ page }) => {
    await page.goto("/index.html?fixture=operator-setup-camera-in-use-by-running-event");
    const saveAndStart = page.getByRole("button", { name: "SAVE AND START" });
    await expect(saveAndStart).toHaveAttribute("aria-disabled", "true");
    await expect(saveAndStart).toHaveAccessibleDescription("End the running Event to start another.");
  });

  test("Event setup shows the brain's draft in its fields, and the Template rows their blank previews, Panorama Four turned a quarter", async ({ page }) => {
    await page.goto("/index.html?fixture=operator-setup-editing");
    await expect(page.locator("#setup-name")).toHaveValue("Harbour gala");
    await expect(page.locator("#setup-camera")).toHaveValue("camera-1");
    const previews = page.locator("#setup-templates img");
    await expect(previews).toHaveCount(4);
    await expect(page.locator('#setup-templates [data-rotated="true"] img')).toHaveCount(1);
  });
});
