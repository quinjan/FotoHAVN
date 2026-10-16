import { describe, expect, it } from "vitest";
import { templateImageSource, templateSchematicUrl, type TemplateGeometry } from "./TemplateArt.ts";

const template: TemplateGeometry = {
  width: 600,
  height: 1800,
  windows: [
    { x: 0, y: 0, width: 600, height: 900 },
    { x: 0, y: 900, width: 600, height: 900 },
  ],
  blankPreviewKey: null,
};

function decode(url: string): string {
  return decodeURIComponent(url.replace("data:image/svg+xml,", ""));
}

describe("template art", () => {
  it("draws a schematic at the Template's native size with one outline per window", () => {
    const svg = decode(templateSchematicUrl(template));

    expect(svg).toContain('width="600" height="1800" viewBox="0 0 600 1800"');
    expect(svg.match(/<rect /g)).toHaveLength(3);
    expect(svg).toContain('y="900" width="600" height="900"');
  });

  it("keeps the outline visible on a very large Template", () => {
    expect(decode(templateSchematicUrl({ ...template, width: 3000 }))).toContain('stroke-width="10"');
    expect(decode(templateSchematicUrl(template))).toContain('stroke-width="2"');
  });

  it("serves the composed blank preview when it exists and the host can serve it", () => {
    const previewUrl = (key: string) => `https://preview.fotohavn/${key}.png`;

    expect(templateImageSource({ ...template, blankPreviewKey: "signature-strip.blank" }, previewUrl)).toBe(
      "https://preview.fotohavn/signature-strip.blank.png",
    );
  });

  it("falls back to the schematic while the brain has not composed the preview, or no host serves one", () => {
    expect(templateImageSource(template, (key) => key)).toBe(templateSchematicUrl(template));
    expect(templateImageSource({ ...template, blankPreviewKey: "k" }, undefined)).toBe(templateSchematicUrl(template));
  });
});
