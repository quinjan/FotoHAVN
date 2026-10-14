import { describe, expect, it } from "vitest";
import { fixturePreviewUrl } from "./previewImages.ts";

function decode(url: string): string {
  return decodeURIComponent(url.replace("data:image/svg+xml,", ""));
}

describe("fixturePreviewUrl", () => {
  it("is the placeholder Photo Strip of the key's Template, at its native size, as a data URL", () => {
    const portrait = decode(fixturePreviewUrl("signature-strip.naturale-v1.r1"));
    const landscape = decode(fixturePreviewUrl("panorama-four.classic-v1.r4"));

    expect(fixturePreviewUrl("signature-strip.naturale-v1.r1")).toMatch(/^data:image\/svg\+xml,/);
    expect(portrait).toContain('width="600" height="1800"');
    expect(landscape).toContain('width="1800" height="600"');
    expect(decode(fixturePreviewUrl("butter-gingham.sepia-v1.r2"))).toContain('width="600" height="1800"');
    expect(decode(fixturePreviewUrl("polka-keepsake.sepia-v1.r2"))).toContain('width="600" height="1800"');
  });

  it("varies with the revision, and shows the Signature Strip for a key it doesn't know", () => {
    expect(fixturePreviewUrl("signature-strip.naturale-v1.r1")).not.toBe(fixturePreviewUrl("signature-strip.naturale-v1.r2"));
    expect(fixturePreviewUrl("nope.naturale-v1.r1")).toBe(fixturePreviewUrl("signature-strip.naturale-v1.r1"));
    expect(fixturePreviewUrl("signature-strip")).toBe(fixturePreviewUrl("signature-strip.naturale-v1.r0"));
  });
});
