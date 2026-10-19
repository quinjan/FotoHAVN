import { expect, type Page } from "@playwright/test";

/**
 * The cycle chrome's stage clips its step (`overflow: hidden`), so a page-scroll check can't see a step that outgrew its room
 * on a short screen. Every element the step draws in flow must sit
 * within the stage's box.
 */
export async function expectStageContains(page: Page): Promise<void> {
  const spills = await page.evaluate(() => {
    const stage = document.querySelector("[data-fotohavn-cycle-step]")?.children[1];
    if (stage === undefined) {
      return [];
    }
    const room = stage.getBoundingClientRect();
    const offenders: string[] = [];
    for (const element of stage.querySelectorAll<HTMLElement>("*")) {
      const style = getComputedStyle(element);
      const rect = element.getBoundingClientRect();
      if (
        rect.width < 2 ||
        rect.height < 2 ||
        style.position === "absolute" ||
        style.position === "fixed" ||
        style.display === "none" ||
        style.display === "inline"
      ) {
        continue;
      }
      if (rect.top < room.top - 1 || rect.bottom > room.bottom + 1 || rect.left < room.left - 1 || rect.right > room.right + 1) {
        offenders.push(`${element.tagName.toLowerCase()} ${element.textContent.trim().slice(0, 24)}`);
      }
    }
    return offenders;
  });
  expect(spills).toEqual([]);
}
