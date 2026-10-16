import type { Page } from "@playwright/test";
import { captureTestScreenshot } from "../../../src/evidence";
import { expect, test } from "../../../src/fixtures";
import { GuestGalleryScreen, completeCycle, guestGallery } from "../../../src/gallery";
import { lookNames, startGuestEvent, tap, templateNames } from "../../../src/guest-cycle";

interface DomSnapshot {
  readonly text: string;
  /** Every attribute but the image addresses and the styling ones, as `tag[name]=value`. */
  readonly attributes: readonly string[];
  readonly dataAttributes: readonly string[];
  readonly sources: readonly string[];
  readonly buttons: readonly string[];
  readonly images: readonly { readonly alt: string | null }[];
}

async function snapshot(page: Page): Promise<DomSnapshot> {
  return page.evaluate(() => {
    const elements = Array.from(document.querySelectorAll("*"));
    const attributes = elements.flatMap((element) =>
      Array.from(element.attributes)
        .filter((attribute) => !["src", "srcset", "class", "style"].includes(attribute.name))
        .map((attribute) => `${element.tagName.toLowerCase()}[${attribute.name}]=${attribute.value}`),
    );
    return {
      text: document.body.textContent,
      attributes,
      dataAttributes: attributes.filter((attribute) => /^[a-z0-9-]+\[data-/.test(attribute)),
      sources: Array.from(document.querySelectorAll("img")).map((image) => image.getAttribute("src") ?? ""),
      buttons: Array.from(document.querySelectorAll("button")).map((button) => (button.getAttribute("aria-label") ?? (button.textContent.trim() || button.querySelector("img")?.getAttribute("alt")) ?? "").trim()),
      images: Array.from(document.querySelectorAll("img")).map((image) => ({ alt: image.getAttribute("alt") })),
    };
  });
}

const uuid = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;

function expectNoMetadata(dom: DomSnapshot, eventName: string, ids: readonly string[]): void {
  // "Look at *you.*" is the heading, the only place the word may appear.
  const visible = `${dom.text}\n${dom.attributes.join("\n")}`.replace(/Look at\s+you\./g, "");
  const forbidden: [string, RegExp][] = [
    ["a clock time", /\b\d{1,2}:\d{2}\b/],
    ["a date", /\b\d{4}-\d{2}-\d{2}\b|\b\d{1,2}\s+(jan|feb|mar|apr|may|jun|jul|aug|sep|oct|nov|dec)[a-z]*\b/i],
    ["an AM or PM time", /\b\d{1,2}\s?(am|pm)\b/i],
    ["the Event's name", new RegExp(eventName, "i")],
    ["a Template name", new RegExp(templateNames.join("|"), "i")],
    ["a Look name", new RegExp(lookNames.join("|"), "i")],
    ["a Template or Look label", /\b(template|look)\b/i],
    ["an outcome", /completed|started over|ended by operator|app closed|unrecoverable/i],
    ["a delete control", /delete/i],
    ["a folder control", /folder/i],
  ];
  for (const [what, pattern] of forbidden) {
    expect(pattern.exec(visible)?.[0], `the guest DOM has ${what}`).toBeUndefined();
  }
  expect(dom.dataAttributes.filter((attribute) => uuid.test(attribute) || ids.some((id) => attribute.includes(id))), "ids in data-* attributes").toEqual([]);
  for (const id of ids) {
    expect(visible, `the id ${id} in text or attributes`).not.toContain(id);
  }
}

test(
  "the guest detail shows the strip and its arranged Captures only: no time, names, Template, Look, end reason, delete, or folder controls exist in the DOM",
  { tag: ["@m9", "@camera"] },
  async ({ app, operatorPage, camera, hooks }) => {
    test.setTimeout(300_000);
    const eventName = "Limit probe";
    const run = await startGuestEvent({ app, page: operatorPage, camera }, { name: eventName, countdown: 3, guestGallery: true, timers: { Keepsake: 5 } });
    const cycle = await completeCycle(run, { retake: true });

    const captures = run.captures(cycle.id);
    const kept = captures.filter((capture) => capture.state === "kept");
    const discarded = captures.filter((capture) => capture.state === "discarded");
    const ids = [cycle.id, run.eventId, ...captures.map((capture) => capture.id)];

    await test.step("the cycle holds four arranged Captures and one discarded one", () => {
      expect(kept).toHaveLength(4);
      expect(discarded).toHaveLength(1);
      expect(run.strip(cycle.id)).toBeDefined();
    });

    const guest = new GuestGalleryScreen(await run.guest());
    await guest.open();

    await test.step("the row holds the strip as a picture and a button, and nothing else", async () => {
      await expect(guest.strips).toHaveCount(1);
      const dom = await snapshot(guest.page);
      expectNoMetadata(dom, eventName, ids);
      expect(dom.images).toEqual([{ alt: "Photo Strip from this Event" }]);
      expect(dom.buttons.filter((name) => name !== "Operator exit").sort()).toEqual(["BACK TO START", "Photo Strip from this Event"]);
    });

    await test.step("the strip's detail shows the strip and its four arranged Captures, and no metadata", async () => {
      await tap(guest.strips.first());
      await expect(guest.detail).toBeVisible();
      expect(await guestGallery(hooks)).toMatchObject({ stripCount: 1 });
      expect((await guestGallery(hooks))?.detailId).not.toBeNull();

      const dom = await snapshot(guest.page);
      expect(dom.images.map((image) => image.alt).sort()).toEqual([
        "Photo Strip from this Event",
        "Photograph from this Event",
        "Photograph from this Event",
        "Photograph from this Event",
        "Photograph from this Event",
      ]);
      expectNoMetadata(dom, eventName, ids);
      for (const capture of discarded) {
        expect(dom.sources.join("\n"), "the discarded Capture").not.toContain(capture.id);
        expect(dom.sources.join("\n"), "the discarded Capture's file").not.toContain(capture.file_name);
      }
      expect(dom.buttons.filter((name) => name !== "Operator exit").sort()).toEqual(["BACK TO START", "CLOSE"]);
      await captureTestScreenshot(guest.page, test.info(), "guest-strip-detail");
    });

    await tap(guest.close);
    await expect(guest.list).toBeVisible();
    await tap(guest.backToStart);
    await expect(guest.link).toBeVisible();
    await run.endEventFromOperator();
  },
);
