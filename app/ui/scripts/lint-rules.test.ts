// @vitest-environment node
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";
import { ESLint } from "eslint";
import stylelint from "stylelint";
import { describe, expect, it } from "vitest";

// Proves the token lint rules fire, through the real configs (stylelint.config.js and eslint.config.js).
const uiDir = resolve(dirname(fileURLToPath(import.meta.url)), "..");

async function stylelintRules(code: string, file = "src/components/Example/Example.module.css"): Promise<string[]> {
  const result = await stylelint.lint({ code, codeFilename: resolve(uiDir, file), cwd: uiDir });
  return (result.results[0]?.warnings ?? []).map((warning) => warning.rule);
}

describe("stylelint token rules", () => {
  it("accept token-only CSS", async () => {
    const css = `.a {
  color: var(--app-tone-ink);
  background-color: transparent;
  border: var(--stroke-hairline) solid currentcolor;
  transition: opacity var(--motion-duration-standard) var(--motion-easing-editorial);
  z-index: var(--app-z-dialog);
}
@media (forced-colors: active) {
  .a { outline-color: Highlight; }
}
`;
    expect(await stylelintRules(css)).toEqual([]);
  });

  it.each([
    ["a short hex color", ".a { color: #fff; }", "color-no-hex"],
    ["a long hex color", ".a { color: #1E1A17; }", "color-no-hex"],
    ["a hex color with alpha", ".a { box-shadow: 0 1px 2px #2D211B24; }", "color-no-hex"],
    ["a named color", ".a { color: black; }", "color-named"],
    ["an rgb() color", ".a { color: rgb(30 26 23); }", "function-disallowed-list"],
    ["an oklch() color", ".a { color: oklch(0.5 0.1 30); }", "function-disallowed-list"],
    ["an oklab() color", ".a { color: oklab(0.5 0.1 0.1); }", "function-disallowed-list"],
    ["a lab() color", ".a { color: lab(50% 40 59); }", "function-disallowed-list"],
    ["a lch() color", ".a { color: lch(50% 40 59); }", "function-disallowed-list"],
    ["an hwb() color", ".a { color: hwb(30 10% 10%); }", "function-disallowed-list"],
    ["a color() color", ".a { color: color(display-p3 1 0 0); }", "function-disallowed-list"],
    ["a raw cubic-bezier()", ".a { transition: opacity 1ms cubic-bezier(0.22, 1, 0.36, 1); }", "function-disallowed-list"],
    ["a raw millisecond duration", ".a { transition: opacity 200ms; }", "unit-disallowed-list"],
    ["a raw second duration", ".a { animation-duration: 0.3s; }", "unit-disallowed-list"],
    ["a raw z-index", ".a { z-index: 10; }", "declaration-property-value-allowed-list"],
  ])("reject %s", async (_name, css, rule) => {
    expect(await stylelintRules(css)).toContain(rule);
  });

  it("allow raw values in app-tokens.css, and skip the generated brand-tokens.css", async () => {
    const raw = ":root { --x: #fff; --y: 200ms; --z: cubic-bezier(0, 0, 1, 1); --w: oklch(0.5 0.1 30); --v: color(display-p3 1 0 0); }";
    expect(await stylelintRules(raw, "src/styles/app-tokens.css")).toEqual([]);
    expect(await stylelintRules(raw, "src/styles/brand-tokens.css")).toEqual([]);
    expect(await stylelintRules(raw, "src/styles/global.css")).toEqual(
      expect.arrayContaining(["color-no-hex", "unit-disallowed-list", "function-disallowed-list"]),
    );
  });
});

describe("ESLint token rules", () => {
  it("reject hex colors and raw motion values in TSX, and accept tokens", async () => {
    const eslint = new ESLint({ cwd: uiDir });
    const code = `export const ink = "#1E1A17";
export function Swatch() {
  return (
    <div>
      <p style={{ transition: "opacity 200ms" }}>One</p>
      <p style={{ transitionTimingFunction: \`cubic-bezier(0, 0, 1, 1)\` }}>Two</p>
      <p style={{ color: "var(--app-tone-ink)" }}>Three</p>
    </div>
  );
}
`;
    // An existing file, so the type-aware config's project service accepts it; lintText lints the given code instead.
    const [result] = await eslint.lintText(code, { filePath: resolve(uiDir, "src/components/shared/ActionArrow.tsx") });
    const restricted = (result?.messages ?? []).filter((message) => message.ruleId === "no-restricted-syntax");
    expect(restricted.map((message) => message.line)).toEqual([1, 5, 6]);
  }, 60_000);

  async function restrictedLines(code: string): Promise<number[]> {
    const eslint = new ESLint({ cwd: uiDir });
    const [result] = await eslint.lintText(code, { filePath: resolve(uiDir, "src/components/shared/ActionArrow.tsx") });
    return (result?.messages ?? []).filter((message) => message.ruleId === "no-restricted-syntax").map((message) => message.line);
  }

  it.each([
    ["an rgb() color", `<p style={{ color: "rgb(30 26 23)" }}>x</p>`],
    ["an rgba() color", `<p style={{ color: "rgba(30, 26, 23, 0.5)" }}>x</p>`],
    ["an hsl() color", `<p style={{ color: "hsl(30 10% 10%)" }}>x</p>`],
    ["an hsla() color", `<p style={{ color: "hsla(30, 10%, 10%, 0.5)" }}>x</p>`],
    ["an oklch() color", `<p style={{ color: "oklch(0.5 0.1 30)" }}>x</p>`],
    ["an oklab() color", `<p style={{ color: "oklab(0.5 0.1 0.1)" }}>x</p>`],
    ["a lab() color", `<p style={{ color: "lab(50% 40 59)" }}>x</p>`],
    ["a lch() color", `<p style={{ color: "lch(50% 40 59)" }}>x</p>`],
    ["an hwb() color", `<p style={{ color: "hwb(30 10% 10%)" }}>x</p>`],
    ["a color() color", `<p style={{ color: "color(display-p3 1 0 0)" }}>x</p>`],
    ["a color function in a template literal", "<p style={{ color: `rgb(${String(1)} 2 3)` }}>x</p>"],
    ["a named color", `<p style={{ color: "red" }}>x</p>`],
    ["a named color in a shorthand", `<p style={{ border: "1px solid RebeccaPurple" }}>x</p>`],
    ["a raw numeric zIndex", `<p style={{ zIndex: 10 }}>x</p>`],
    ["a raw string zIndex", `<p style={{ zIndex: "10" }}>x</p>`],
    ["a quoted zIndex key", `<p style={{ "zIndex": 10 }}>x</p>`],
    ["a zIndex in a style object outside JSX", `export const style = { zIndex: 5 };`],
  ])("reject %s in TSX", async (_name, jsx) => {
    const code = jsx.startsWith("export") ? jsx : `export function Swatch() {\n  return ${jsx};\n}\n`;
    expect(await restrictedLines(code)).toEqual([jsx.startsWith("export") ? 1 : 2]);
  }, 60_000);

  it("accept color tokens, system colors, transparent, and zIndex tokens in inline styles", async () => {
    const code = `export function Swatch() {
  return (
    <div>
      <p style={{ color: "var(--app-tone-ink)", background: "transparent", borderColor: "currentcolor" }}>One</p>
      <p style={{ outlineColor: "Highlight", fontFamily: "var(--font-body)" }}>Two</p>
      <p style={{ zIndex: "var(--app-z-dialog)" }}>Three</p>
    </div>
  );
}
`;
    expect(await restrictedLines(code)).toEqual([]);
  }, 60_000);

  it("scope the TSX rules to src, so the token CSS files remain the only home of raw values", async () => {
    const eslint = new ESLint({ cwd: uiDir });
    const code = `export const style = { zIndex: 5, color: "oklch(0.5 0.1 30)" };\n`;
    const [result] = await eslint.lintText(code, { filePath: resolve(uiDir, "tests/fixtures/components.spec.ts") });
    expect((result?.messages ?? []).filter((message) => message.ruleId === "no-restricted-syntax")).toEqual([]);
  }, 60_000);
});
