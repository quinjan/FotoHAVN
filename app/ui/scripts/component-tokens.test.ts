// @vitest-environment node
import { readFile } from "node:fs/promises";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";

// Static checks on the CSS that jsdom can't compute: app tokens, and control-boundary contrast (WCAG 1.4.11, 3:1).
const srcDir = resolve(dirname(fileURLToPath(import.meta.url)), "../src");
const read = (path: string): Promise<string> => readFile(resolve(srcDir, path), "utf8");

function brandColor(brandTokens: string, name: string): string {
  const match = new RegExp(`--color-${name}:\\s*(#[0-9A-Fa-f]{6})`).exec(brandTokens);
  if (match?.[1] === undefined) {
    throw new Error(`--color-${name} is not in brand-tokens.css`);
  }
  return match[1];
}

function luminance(hex: string): number {
  const [r = 0, g = 0, b = 0] = [1, 3, 5].map((i) => {
    const channel = Number.parseInt(hex.slice(i, i + 2), 16) / 255;
    return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(a: string, b: string): number {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x) as [number, number];
  return (light + 0.05) / (dark + 0.05);
}

describe("app tokens", () => {
  it("declares the accent rule stroke and the countdown size", async () => {
    const tokens = await read("styles/app-tokens.css");
    expect(tokens).toMatch(/--app-stroke-rule-accent:\s*2px;/);
    expect(tokens).toContain(
      "--app-type-countdown: clamp(160px, calc(160px + (100vw - 1280px) * 160 / 1280), 320px);",
    );
  });

  it("sizes the countdown numeral with the countdown token", async () => {
    const css = await read("components/CountdownNumeral/CountdownNumeral.module.css");
    expect(css).toContain("font-size: var(--app-type-countdown);");
  });

  it("reads the Select placeholder in the secondary ink and the list in the primary ink", async () => {
    const css = await read("components/Select/Select.module.css");
    expect(css).toMatch(/\.select:has\(option\[data-placeholder\]:checked\) \{\s*color: var\(--app-tone-ink-secondary\);/);
    expect(css).toMatch(/\.select option \{\s*color: var\(--app-tone-ink\);/);
  });

  it.each([
    "RunningEventPanel/RunningEventPanel.module.css",
    "OptionList/OptionList.module.css",
    "StepProgress/StepProgress.module.css",
  ])("%s draws its accent rule from the token", async (file) => {
    const css = await read(`components/${file}`);
    expect(css).toContain("var(--app-stroke-rule-accent)");
    expect(css).not.toContain("calc(var(--stroke-hairline) * 2)");
  });
});

describe("control boundaries", () => {
  it("draw a resting input border in the secondary ink", async () => {
    const css = await read("components/shared/field.module.css");
    const control = /\.control \{[^}]*\}/.exec(css)?.[0] ?? "";
    expect(control).toContain("border: var(--stroke-hairline) solid var(--app-tone-ink-secondary);");
  });

  it("have a 3:1 border on Off-white and Warm Ivory", async () => {
    const brand = await read("styles/brand-tokens.css");
    const border = brandColor(brand, "soft-brown");
    expect(contrast(border, brandColor(brand, "off-white"))).toBeGreaterThanOrEqual(3);
    expect(contrast(border, brandColor(brand, "warm-ivory"))).toBeGreaterThanOrEqual(3);
  });

  it("keep the decorative Hairline below 3:1, so only controls change", async () => {
    const brand = await read("styles/brand-tokens.css");
    expect(contrast(brandColor(brand, "hairline"), brandColor(brand, "off-white"))).toBeLessThan(3);
  });
});
