# Booth UI components

Shared React components for the Operator Screen and Guest Screen. Read [`../../../DESIGN.md`](../../../DESIGN.md) (normative for the booth UI) and the [`brand/`](../../../../brand/README.md) documents it links before building one. This file is the convention every component follows; `scripts/component-conventions.test.ts` and the fixture lane enforce the mechanical parts.

## Folder

One folder per component, named in PascalCase after the component, with exactly these five files (extra `*.module.css` files or private helpers are fine):

```text
src/components/<Name>/
├─ <Name>.tsx            the component: a named export, readonly props, a doc comment saying what it is and when to use it
├─ <Name>.module.css     its styles: tokens only (see CSS)
├─ <Name>.test.tsx       Vitest + Testing Library: states, ARIA, keyboard, timing, reduced-motion branches
├─ <Name>.fixtures.tsx   `export const fixtures: readonly ComponentFixture[]` (see Fixtures)
└─ index.ts              `export { <Name>, type <Name>Props } from "./<Name>.tsx";`
```

- Import another component through its `index.ts` (`../PrimaryAction/index.ts`); a component's own tests and fixtures import `./<Name>.tsx`.
- `shared/` holds internals, not components. One helper per job; look here before writing your own:

  | File | Use |
  | --- | --- |
  | `cx.ts` | Joins class names |
  | `guardDisabled.ts` | Makes an `aria-disabled` action swallow activation |
  | `ActionArrow.tsx` | The Phosphor Light arrow after an action label |
  | `field.tsx`, `field.module.css` | Operator input chrome: label, requirement mark, help and error messages with their ids (`useFieldIds`, `describedBy`, `FieldMessages`), and the `.control` box |
  | `ChoiceMark.tsx`, `choice.module.css` | The checkbox and radio marks shared by `Checkbox`, `Radio`, `OptionList`, and `SegmentedChoice` |
  | `roving.ts` | Arrow-key stepping and the roving tab stop for radio groups and option lists |
  | `reducedMotion.ts` | `usePrefersReducedMotion()`, for behavior that changes (not CSS) |
  | `secondsText.ts` | "1 second" / "N seconds" for announcements and messages |
  | `fixtureHelpers.tsx`, `fixtureOverlayFrame.tsx`, `fixturePhotographs.ts`, `fixtureLayout.module.css`, `formFixture.module.css` | Fixture-only: `FocusFirst`, `noop`, the overlay frame, stand-in photographs, and layouts |

  Use them; don't change them in a component change. If you need something new there, add a new file, and never write a second copy of a helper that exists.
- There is no registry. The gallery and the fixture lane find `*.fixtures.tsx` by glob, so adding your folder is enough and parallel work never touches a shared list.

## Props and markup

- Props are `readonly`. Accept `className` for layout by the parent only; never accept `style`. Pass through native attributes (`ComponentPropsWithRef<"button">` minus what you own) where the component wraps one element.
- Use real elements: `<button>` for commands, headings in order, lists, landmarks only on screens. The booth navigates by command, so links are rare.
- Copy comes from DESIGN.md's copy tables exactly, including casing and arrows. Labels are uppercase in the source text. Domain strings (Template and Look names, notices) come from the brain as props.
- Actions: use `PrimaryAction`, `SecondaryAction`, and `TextLink`; don't restyle a raw `<button>` into one. Each sets `data-fotohavn-action="primary|secondary|text"`. **One primary action per view**: a component that composes actions renders at most one `PrimaryAction`, and the fixture lane fails a fixture with two.
- Unavailable actions use `disabled`, which renders `aria-disabled="true"`, stays focusable, and ignores activation (`shared/guardDisabled.ts`). Give the reason in text near the action.
- Arrows: pass `arrow="right" | "left" | "up" | "down" | "up-right" | "down-right"` to an action when it leads somewhere (`STEP INSIDE →`, `BACK TO START ←`). Never type an arrow glyph into a label.
- Icons: Phosphor Light only (`@phosphor-icons/react`, `weight="light"`; Fill only for a small selected or recording state), sized with `font-size` on a wrapper and `size="1em"`, `aria-hidden` beside a text label, and an accessible name on an icon-only control.
- State is never color alone: pair it with text, an ARIA state (`aria-checked`, `aria-current`, `aria-pressed`), or a shape change.
- Headings: `StatementHeading` takes `lead` and `turn`; the italic turn renders as the closing `<em>`, one per heading.

## CSS

Use only `var(--…)` tokens for colors, type, space, radius, stroke, shadow, motion, and z-order. `npm run lint` (stylelint) rejects hex colors, named colors, `rgb()`/`hsl()`, raw `cubic-bezier()`, raw `ms`/`s` durations, and raw `z-index` outside the token files; ESLint rejects hex colors and raw easings or durations in TSX.

| Need | Token | Source |
| --- | --- | --- |
| Ink, secondary ink, rules, accent, focus, action fill and label | `--app-tone-*` | `app-tokens.css`; set by the surface's `data-tone` |
| Surfaces when you own one | `--semantic-surface-*`, plus `data-tone="light" \| "paper" \| "dark"` on the element | brand + app |
| Type size by role | `--app-text-<role>` (`eyebrow`, `meta`, `action`, `supporting`, `body`, `body-lg`, `subheading`, `heading-sm`, `heading`, `heading-lg`, `display`); the countdown numeral alone uses `--app-type-countdown` | app; brand sizes on the Operator Screen, the kiosk scale on the Guest Screen |
| Family, weight, tracking, line height, case by role | `--typography-<role>-font-family`, `-font-weight`, `-letter-spacing`, `-line-height`, `-text-transform` | brand |
| Minimum target | `--app-target` (48 operator, 64 guest) | app |
| Icon size | `--app-icon-action`, `--app-icon-control` | app |
| Space | `--space-<n>` (brand scale); `--app-space-fluid-<n>` for guest compositions that grow to 2560 | brand + app |
| Page gutters, grid split, top band, stage | `--app-gutter`, `--app-grid-columns`, `--app-split-print`, `--app-split-options`, `--app-region-top`, `--app-region-stage` | app |
| Radius, stroke, shadow | `--radius-*`, `--stroke-*`, `--shadow-print`, `--shadow-print-contact`; the 2 px Muted Brass rule (selected row, running-Event panel, current-step underline) is `--app-stroke-rule-accent` | brand + app |
| Motion | `--motion-duration-*`, `--motion-easing-*`, `--motion-distance-reveal-max`, `--app-motion-nudge` | brand + app |
| Z-order | `--app-z-*` | app |
| Scrim, selection | `--app-scrim`, `--app-selection` | app |

- Size for both roles at once: use the role-resolved `--app-text-*` and `--app-target` tokens, never a fixed pixel size, so the same component is right on both screens. The role comes from `data-fotohavn-role` on `<html>` (or on a gallery preview).
- Read inks from `--app-tone-*`, never from primitives, so your component inverts on a dark surface without a prop. Respect the contrast pairs in `brand/color.md`: Soft Brown never on Cream Paper (`data-tone="paper"` switches the secondary ink), Antique Metal only on dark.
- A control whose border is its only boundary (text inputs, select, PIN keys, segment, checkbox, radio, switch) draws it in `--app-tone-ink-secondary`, which is at least 3:1 against its surface (WCAG 1.4.11). `--app-tone-rule` (Hairline) is for decoration: dividers, row separators, panel edges, and disabled controls.
- Errors are never color alone: ink text with a Phosphor warning icon, a heavier border, `aria-invalid`, and `aria-describedby` (see `shared/field.tsx`).
- Component-local custom properties are fine when derived from tokens (`--disc-size: calc(var(--app-icon-control) * 3.5)`). Don't edit `app-tokens.css`, `global.css`, or `brand-tokens.css` in a component change; propose a new app token in your report instead.
- Hover styles go inside `@media (hover: hover)`; give `:active` the same feedback for touch.
- **Motion transforms go inside `@media (prefers-reduced-motion: no-preference)`.** `global.css` already makes every transition and animation 1 ms under reduced motion; transforms (translate, scale) must not apply at all there. Content never disappears with the motion.
- Forced colors: keep a border on any control whose fill carries its shape (actions do), never set `forced-color-adjust: none`, and add `@media (forced-colors: active)` rules for states that would otherwise vanish (the selected row, the checked radio).
- No page scrolling on the Guest Screen. A guest component fits 1280 × 720 without overflow. The guest gallery's strip row is the only scrolling region; mark it `data-fotohavn-scroll-region` and give it `touch-action: pan-x`.

## Fixtures

`<Name>.fixtures.tsx` exports the component's named states:

```tsx
import type { ComponentFixture } from "../fixture.ts";
import { noop } from "../shared/fixtureHelpers.tsx";
import { Thing } from "./Thing.tsx";

export const fixtures: readonly ComponentFixture[] = [
  { id: "thing-guest", title: "Thing, guest", surface: "light", role: "guest", render: () => <Thing onSelect={noop} /> },
];
```

| Field | Rule |
| --- | --- |
| `id` | Unique across all components, kebab-case `<component>-<state>` (`option-list-selected`). It is the URL and the screenshot folder. |
| `title` | Short and human: "Option list, selected row". |
| `surface` | `light` (Off-white canvas) or `dark` (Dark Walnut). The gallery sets `data-tone` on the wrapper. |
| `role` | `guest` or `operator`. The gallery sets `data-fotohavn-role` on `<html>`, so the kiosk scale, targets, and no-scroll rules apply exactly as on that screen. |
| `layout` | Optional. `centered` (default) centers the component within the page gutters; `fill` gives it the whole viewport (screens, bands, dialogs with their scrim). |
| `render` | Returns the JSX. Fixtures send no commands (`noop`); use `FocusFirst` to show the focus ring. |

Cover every state a reviewer must see: default, each variant, selected or checked, disabled with its reason, keyboard focus, error, empty, longest realistic copy, both roles if the component appears on both screens, and dark if it can sit on a dark band. Keep fixtures deterministic: no timers running on mount, no random data, no network.

## Gallery and fixture lane

| URL (dev server or E2E build) | Shows |
| --- | --- |
| `index.html?gallery` | Every fixture, grouped by component, with links |
| `index.html?gallery=<id>` | One fixture alone in the full viewport, for screenshots |
| `index.html?gallery=manifest` | The manifest JSON the fixture lane reads |

The gallery exists only in development and E2E builds; `vite.config.ts` fails a field build that bundles it. `npm run test:fixtures` renders every fixture at 1280 × 720, 1366 × 768, 1920 × 1080, and 2560 × 1440 and checks no overflow (and, for guest fixtures, no scrolling region), the role's minimum target size on every control, at most one primary action, and axe with no violations (a fixture needn't hold an `<h1>`). It repeats each fixture at 1280 × 720 under reduced motion (no transition or animation over 1 ms) and forced colors (system colors everywhere, bordered actions). Screenshots land in `../output/fixtures/components/<id>/`: `<w>x<h>.png`, `reduced-motion-1280x720.png`, and `forced-colors-1280x720.png`. Look at them against DESIGN.md before you finish.

## Done means

From `app/ui`: `npm run lint`, `npm run typecheck`, `npm run test:coverage` (80% lines, branches, functions, and statements; fixture files are excluded), and `npm run test:fixtures` all pass, and you have looked at your screenshots at all four resolutions.
