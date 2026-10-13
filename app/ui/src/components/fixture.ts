import type { ReactNode } from "react";

/** The tone of the surface a fixture renders on: Off-white canvas, or Dark Walnut (`data-tone`, app-tokens.css). */
export type FixtureSurface = "light" | "dark";

/** The window a fixture belongs to; `guest` applies the kiosk scale, guest targets, and the no-scroll rules. */
export type FixtureRole = "guest" | "operator";

/**
 * One named state of a component, rendered by the fixture gallery (`index.html?gallery=<id>`) and checked by the
 * fixture lane (`npm run test:fixtures`) at four resolutions with screenshots, axe, and overflow checks.
 *
 * Each `src/components/<Name>/<Name>.fixtures.tsx` exports `fixtures: readonly ComponentFixture[]`. The gallery
 * discovers those files with `import.meta.glob`; there is no central registry. See src/components/README.md.
 */
export interface ComponentFixture {
  /** Unique across all components, kebab-case `<component>-<state>`, e.g. `primary-action-disabled`. */
  readonly id: string;
  /** A short human title, e.g. "Primary action, disabled". */
  readonly title: string;
  readonly surface: FixtureSurface;
  readonly role: FixtureRole;
  /**
   * `centered` (default): the component centered within the page gutters. `fill`: the component receives the whole
   * viewport, for full-screen compositions, bands, and dialogs with their scrim.
   */
  readonly layout?: "centered" | "fill";
  readonly render: () => ReactNode;
}
