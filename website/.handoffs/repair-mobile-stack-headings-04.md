# Mobile stack-heading repair 04

result: passed

## Scope

Fixed only the pass-2 P2 mobile stack-heading overflow in `website/src/components/MiddleExperience.module.css`.

## CSS change

- Replaced the mobile `.stackCopy h3` `10ch` / `clamp(2.6rem, 13vw, 3.5rem)` seam with `max-width: 100%`, `font-size: clamp(1.95rem, 9.75vw, 2.75rem)`, and `line-height: 0.98`.
- Removed `.stackCopy h3` from the later `max-width: 360px` fixed `2.55rem` override so the responsive clamp remains authoritative at 320px.
- Copy, DOM structure, padding, media geometry, transforms, overflow, desktop/tablet stack rules, and reduced-motion rules are unchanged.

## In-app Browser verification

Verified the shared `http://localhost:3011/` implementation in a fresh Codex in-app Browser tab after reload. Values below are text-range right edges against the 24px-inset content right edge:

| Requested viewport | Client width | Font size | STEP INSIDE | BE YOURSELVES | KEEP THE PHOTOGRAPH | Content right |
|---:|---:|---:|---:|---:|---:|---:|
| 320×720 | 305px | 31.2px | 213.99px | 226.16px | 253.96px | 256.8px |
| 375×812 | 360px | 36.582px | 242.59px | 303.34px | 289.45px | 312px |
| 390×844 | 375px | 38.064px | 250.51px | 313.73px | 299.27px | 327.2px |

At all three widths:

- every heading element and text-range rectangle stayed within the 24px-inset copy measure and card;
- every paragraph stayed within the same content measure;
- heading-to-paragraph and copy-to-media overlap checks were false;
- descendant/card horizontal overflow checks were false;
- document `scrollWidth - clientWidth` was `0`;
- all headings, paragraphs, and stack-image layout boxes were visible with positive dimensions.

## Other checks

- `git diff --check -- website/src/components/MiddleExperience.module.css` — passed; Git emitted only the repository's LF-to-CRLF working-copy warning.
- CSS source inspection — the repair exists only inside `@media (max-width: 767px)`; desktop/tablet stack size, sticky geometry, and static 768–1023px geometry are untouched.
- Scoped ESLint — not applicable because the only production change is CSS.
- Full TypeScript and production build — intentionally not run concurrently; integrated build remains pending for the orchestrator.

This repair result does not assert that either independent gate passes.
