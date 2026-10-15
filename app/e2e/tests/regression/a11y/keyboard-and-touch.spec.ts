import type { CDPSession, Locator, Page } from "@playwright/test";
import { expect, test } from "../../../src/fixtures";
import { brandColors, openFixture, returnToApp } from "../../../src/gallery";

// Keyboard and touch in the real host (app/DESIGN.md: Accessibility checks): Tab reaches every control in reading
// order, each focused control shows the 2 px focus outline at a 5 px offset (Ebony on light surfaces, Off-white on
// dark), and real touch input (CDP Input.dispatchTouchEvent, not the mouse) taps the 64 px Guest Screen targets and the
// 112 px Shutter. Fixtures come from the gallery; the Operator window is sized to 1280x720, the smallest Guest layout.

const focusables = ':is(a,button,input,select,textarea,[tabindex]:not([tabindex="-1"]))';
const guestTarget = 64;
const shutterTarget = 112;

interface FocusedControl {
  readonly index: number;
  readonly name: string;
  readonly focusVisible: boolean;
  readonly outlineStyle: string;
  readonly outlineWidth: string;
  readonly outlineOffset: string;
  readonly outlineColor: string;
}

/**
 * Stamps the visible, enabled, tabbable controls with their DOM reading order and returns their accessible names,
 * so the page can report which one holds focus. Radios are not stamped: a group has one tab stop.
 */
async function stampTabStops(page: Page): Promise<string[]> {
  return await page.evaluate((selector) => {
    const nameOf = (element: HTMLElement): string => {
      const labelledby = element.getAttribute("aria-labelledby");
      const labelled = labelledby === null ? "" : labelledby.split(/\s+/).map((id) => document.getElementById(id)?.textContent ?? "").join(" ");
      return (element.getAttribute("aria-label") ?? (labelled.trim() === "" ? (element.textContent ?? "") : labelled)).trim();
    };
    const tabStops = Array.from(document.querySelectorAll<HTMLElement>(selector)).filter((element) => {
      const box = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      return box.width > 0 && box.height > 0 && style.visibility !== "hidden" && !(element as HTMLButtonElement).disabled && element.tabIndex >= 0;
    });
    tabStops.forEach((element, index) => {
      element.dataset.e2eOrder = String(index);
    });
    return tabStops.map(nameOf);
  }, focusables);
}

async function focusedControl(page: Page): Promise<FocusedControl> {
  return await page.evaluate(() => {
    const element = document.activeElement as HTMLElement;
    const style = getComputedStyle(element);
    const labelledby = element.getAttribute("aria-labelledby");
    const labelled = labelledby === null ? "" : labelledby.split(/\s+/).map((id) => document.getElementById(id)?.textContent ?? "").join(" ");
    return {
      index: Number(element.dataset.e2eOrder ?? -1),
      name: (element.getAttribute("aria-label") ?? (labelled.trim() === "" ? (element.textContent ?? "") : labelled)).trim(),
      focusVisible: element.matches(":focus-visible"),
      outlineStyle: style.outlineStyle,
      outlineWidth: style.outlineWidth,
      outlineOffset: style.outlineOffset,
      outlineColor: style.outlineColor,
    };
  });
}

/**
 * Tabs through the whole fixture and checks order and the focus ring at every stop. From a blurred start the first Tab
 * lands on the first control; a dialog fixture starts with focus already on its first control (it traps focus), so
 * that run begins there and Tabs through the rest.
 */
async function expectTabOrderAndFocusRing(page: Page, ringColor: string, { startsOnFirst = false } = {}): Promise<void> {
  const expectedNames = await stampTabStops(page);
  expect(expectedNames.length, "the fixture has several tab stops").toBeGreaterThanOrEqual(3);

  const seen: FocusedControl[] = [];
  if (startsOnFirst) {
    seen.push(await focusedControl(page));
  } else {
    await page.evaluate(() => {
      (document.activeElement as HTMLElement | null)?.blur();
    });
  }
  while (seen.length < expectedNames.length) {
    await page.keyboard.press("Tab");
    seen.push(await focusedControl(page));
  }

  expect(
    seen.map((control) => control.index),
    "Tab visits the controls in DOM reading order",
  ).toEqual(expectedNames.map((_, index) => index));
  expect(
    seen.map((control) => control.name),
    "the focused controls' accessible names",
  ).toEqual(expectedNames);
  for (const control of seen) {
    expect(control, `${control.name} shows the focus ring`).toMatchObject({
      focusVisible: true,
      outlineStyle: "solid",
      outlineWidth: "2px",
      outlineOffset: "5px",
      outlineColor: ringColor,
    });
  }

  await page.keyboard.press("Shift+Tab");
  const back = await focusedControl(page);
  expect(back.index, "Shift+Tab goes to the previous control").toBe(expectedNames.length - 2);
}

interface RecordedEvent {
  readonly type: string;
  readonly pointerType: string;
  readonly target: string;
}

/** Records pointer and click events (with their pointer type) so a tap is proven to come from touch input. */
async function recordPointerEvents(page: Page): Promise<void> {
  await page.evaluate(() => {
    const events: RecordedEvent[] = [];
    (window as unknown as { __e2ePointerEvents: RecordedEvent[] }).__e2ePointerEvents = events;
    for (const type of ["pointerdown", "pointerup", "click"]) {
      document.addEventListener(
        type,
        (event) => {
          const target = event.target as Element;
          const control = target.closest("button, input, label");
          events.push({
            type,
            pointerType: (event as PointerEvent).pointerType,
            target: (control?.getAttribute("aria-label") ?? control?.getAttribute("aria-labelledby") ?? control?.textContent ?? target.tagName).trim(),
          });
        },
        true,
      );
    }
  });
}

async function recordedEvents(page: Page): Promise<RecordedEvent[]> {
  return await page.evaluate(() => (window as unknown as { __e2ePointerEvents: RecordedEvent[] }).__e2ePointerEvents.slice());
}

/** One finger: touchStart then touchEnd at the center of the control, as the host's Input pipeline receives them. */
async function tap(cdp: CDPSession, control: Locator): Promise<void> {
  const box = await control.boundingBox();
  if (box === null) {
    throw new Error("Can't tap a control that has no bounding box.");
  }
  const x = box.x + box.width / 2;
  const y = box.y + box.height / 2;
  await cdp.send("Input.dispatchTouchEvent", { type: "touchStart", touchPoints: [{ x, y, id: 0 }] });
  await cdp.send("Input.dispatchTouchEvent", { type: "touchEnd", touchPoints: [] });
}

async function expectTarget(control: Locator, minimumWidth: number, minimumHeight: number, label: string): Promise<void> {
  const box = await control.boundingBox();
  expect(box, `${label} is on screen`).not.toBeNull();
  // The fixture lane allows half a pixel for sub-pixel layout; so does this.
  expect(box?.width ?? 0, `${label} width`).toBeGreaterThanOrEqual(minimumWidth - 0.5);
  expect(box?.height ?? 0, `${label} height`).toBeGreaterThanOrEqual(minimumHeight - 0.5);
}

test.describe("keyboard and touch in the real host", { tag: ["@m3", "@cursor"] }, () => {
  test("Tab visits the controls in reading order and each shows the 2 px focus ring at 5 px", async ({ operatorPage: page, hooks }) => {
    const colors = brandColors();
    try {
      await hooks.setWindowBounds("operator", 1280, 720);

      await test.step("operator row actions on a light surface: Ebony ring (text-link-row-actions)", async () => {
        await openFixture(page, "text-link-row-actions", "operator");
        await expectTabOrderAndFocusRing(page, colors["ebony"] ?? "");
      });

      await test.step("the guest PIN pad dialog on a light surface: Ebony ring (pin-pad-empty)", async () => {
        await openFixture(page, "pin-pad-empty", "guest");
        await expectTabOrderAndFocusRing(page, colors["ebony"] ?? "", { startsOnFirst: true });
      });

      await test.step("on a dark surface: Off-white ring (primary-action-inverted)", async () => {
        await openFixture(page, "primary-action-inverted", "guest");
        const names = await stampTabStops(page);
        expect(names).toEqual(["RETRY"]);
        await page.evaluate(() => {
          (document.activeElement as HTMLElement | null)?.blur();
        });
        await page.keyboard.press("Tab");
        expect(await focusedControl(page)).toMatchObject({
          name: "RETRY",
          focusVisible: true,
          outlineStyle: "solid",
          outlineWidth: "2px",
          outlineOffset: "5px",
          outlineColor: colors["off-white"] ?? "",
        });
      });
    } finally {
      await returnToApp(page);
    }
  });

  test("touch taps the 64 px PIN pad keys, the 112 px Shutter, and a 64 px Option list row", async ({ operatorPage: page, hooks }) => {
    const cdp = await page.context().newCDPSession(page);
    try {
      await hooks.setWindowBounds("operator", 1280, 720);

      await test.step("every PIN pad key is at least 64x64 and a touch tap enters a masked digit (pin-pad-empty)", async () => {
        await openFixture(page, "pin-pad-empty", "guest");
        const keys = page.getByRole("group", { name: "PIN digits" }).getByRole("button");
        expect(await keys.count(), "ten digits and delete").toBe(11);
        for (let index = 0; index < 11; index += 1) {
          await expectTarget(keys.nth(index), guestTarget, guestTarget, `PIN pad key ${String(index + 1)}`);
        }

        await recordPointerEvents(page);
        const filled = page.locator("[data-filled]");
        await expect(filled).toHaveCount(0);
        for (const [taps, digit] of ["1", "5", "0"].entries()) {
          await tap(cdp, page.getByRole("group", { name: "PIN digits" }).getByRole("button", { name: digit, exact: true }));
          await expect(filled, `a masked dot appears after tapping ${digit}`).toHaveCount(taps + 1);
        }
        await expect(page.getByText("3 digits entered.")).toBeAttached();

        const events = await recordedEvents(page);
        for (const type of ["pointerdown", "pointerup", "click"]) {
          const ofType = events.filter((event) => event.type === type);
          expect(ofType.map((event) => event.target), `${type} targets`).toEqual(["1", "5", "0"]);
          expect(new Set(ofType.map((event) => event.pointerType)), `${type} came from touch`).toEqual(new Set(["touch"]));
        }

        await tap(cdp, page.getByRole("button", { name: "Delete last digit" }));
        await expect(filled, "the backspace key removes a digit by touch").toHaveCount(2);
      });

      await test.step("the Shutter is at least 112x112 and a touch tap clicks it (shutter)", async () => {
        await openFixture(page, "shutter", "guest");
        const shutter = page.getByRole("button", { name: "Take photographs" });
        await expectTarget(shutter, shutterTarget, shutterTarget, "Shutter");
        await recordPointerEvents(page);
        await tap(cdp, shutter);
        const events = await recordedEvents(page);
        expect(events.map((event) => `${event.type}:${event.pointerType}:${event.target}`)).toEqual([
          "pointerdown:touch:Take photographs",
          "pointerup:touch:Take photographs",
          "click:touch:Take photographs",
        ]);
      });

      await test.step("a guest Option list row is at least 64 px tall and a touch tap lands on it (option-list-template-guest)", async () => {
        await openFixture(page, "option-list-template-guest", "guest");
        const rows = page.getByRole("radio");
        expect(await rows.count()).toBeGreaterThanOrEqual(3);
        for (let index = 0; index < (await rows.count()); index += 1) {
          await expectTarget(rows.nth(index), guestTarget, guestTarget, `Option list row ${String(index + 1)}`);
        }
        await recordPointerEvents(page);
        const row = page.getByRole("radio", { name: "Panorama Four" });
        await tap(cdp, row);
        const events = await recordedEvents(page);
        expect(events.map((event) => `${event.type}:${event.pointerType}`)).toEqual(
          expect.arrayContaining(["pointerdown:touch", "pointerup:touch", "click:touch"]),
        );
        // The fixture's chosen value is fixed (its change handler does nothing), so the tap shows as focus on the tapped row.
        await expect(row).toBeFocused();
      });
    } finally {
      await cdp.detach().catch(() => undefined);
      await returnToApp(page);
    }
  });
});
