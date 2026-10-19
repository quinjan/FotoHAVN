import { expect, test } from "../../../src/fixtures";
import { EventListScreen, EventSetupScreen } from "../../../src/screens/screens";

/** The built-in Templates and the native size their blank preview is composed at (the Template's own pixels). */
const templates = [
  { name: "Signature Strip", width: 600, height: 1800, rotated: false },
  { name: "Panorama Four", width: 1800, height: 600, rotated: true },
  { name: "Butter Gingham", width: 600, height: 1800, rotated: false },
  { name: "Polka Keepsake", width: 600, height: 1800, rotated: false },
] as const;

test.describe("blank Template previews in Event setup", { tag: ["@m6", "@m7"] }, () => {
  test("every offered Template's blank preview loads at its native size, and Panorama Four is turned a quarter", async ({ operatorPage }) => {
    await new EventListScreen(operatorPage).newEvent.click();
    const setup = new EventSetupScreen(operatorPage);
    await expect(setup.heading).toBeVisible();
    await setup.goTo("Templates");

    for (const template of templates) {
      await test.step(`${template.name}: ${String(template.width)} × ${String(template.height)}${template.rotated ? ", rotated" : ""}`, async () => {
        await expect(setup.template(template.name)).toBeChecked();
        const image = setup.templatePreview(template.name);
        await expect(image).toHaveCount(1);
        await expect
          .poll(() => image.evaluate((element) => (element as HTMLImageElement).complete && (element as HTMLImageElement).naturalWidth > 0), {
            timeout: 20_000,
            message: `the blank preview of ${template.name} loads`,
          })
          .toBe(true);
        const shown = await image.evaluate((element) => {
          const img = element as HTMLImageElement;
          return {
            naturalWidth: img.naturalWidth,
            naturalHeight: img.naturalHeight,
            rotate: getComputedStyle(img).rotate,
            marker: img.closest("[data-rotated]") !== null,
            src: img.currentSrc,
          };
        });
        expect({ width: shown.naturalWidth, height: shown.naturalHeight }, shown.src).toEqual({ width: template.width, height: template.height });
        expect(shown.marker).toBe(template.rotated);
        expect(shown.rotate).toBe(template.rotated ? "-90deg" : "none");
        expect(shown.src.startsWith("https://preview.fotohavn/")).toBe(true);
      });
    }
  });
});
