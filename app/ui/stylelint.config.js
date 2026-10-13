/**
 * Stylelint for the UI's CSS (`npm run lint` runs it after ESLint). Beyond stylelint-config-recommended, it enforces
 * app/DESIGN.md's token rule: component and screen CSS reads colors, easings, durations, and z-order from tokens.
 * scripts/lint-rules.test.ts proves each rule fires.
 */
const useTokens = "use a var(--…) from brand-tokens.css or app-tokens.css (app/DESIGN.md, Tokens).";

/** @type {import("stylelint").Config} */
export default {
  extends: ["stylelint-config-recommended"],
  ignoreFiles: [
    // Generated from brand/tokens/tokens.json by app/scripts/generate-tokens.mjs; scripts/brand-tokens.test.ts checks it.
    "src/styles/brand-tokens.css",
    "dist/**",
    "dist-e2e/**",
    "coverage/**",
    "node_modules/**",
  ],
  rules: {
    "color-no-hex": [true, { message: `Hex colors belong only in the token files; ${useTokens}` }],
    "color-named": ["never", { message: `Named colors belong only in the token files; ${useTokens}` }],
    "function-disallowed-list": [
      ["cubic-bezier", "rgb", "rgba", "hsl", "hsla"],
      { message: `Raw cubic-bezier() and color functions belong only in the token files; ${useTokens}` },
    ],
    "unit-disallowed-list": [["ms", "s"], { message: `Raw durations belong only in the token files; ${useTokens}` }],
    "declaration-property-value-allowed-list": [
      { "z-index": ["/^var\\(--app-z-[a-z-]+\\)$/"] },
      { message: "Use a z-order token, var(--app-z-…), from app-tokens.css; never a raw z-index." },
    ],
    // CSS Modules syntax.
    "selector-pseudo-class-no-unknown": [true, { ignorePseudoClasses: ["global", "local"] }],
    "property-no-unknown": [true, { ignoreProperties: ["composes"] }],
  },
  overrides: [
    {
      // The app-only token file holds raw values; that is its job (app/DESIGN.md, App tokens).
      files: ["src/styles/app-tokens.css"],
      rules: {
        "color-no-hex": null,
        "color-named": null,
        "function-disallowed-list": null,
        "unit-disallowed-list": null,
      },
    },
  ],
};
