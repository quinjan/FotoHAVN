# Responsive Editorial Lift — design QA

Date: 2026-09-10. Scope: bringing the selected desktop hero composition and transition to phones and tablets. User explicitly requested implementation after the responsive review. Desktop and later sections remain unchanged in design. Previous report is archived at `.handoffs/responsive-editorial-lift/previous-design-qa.md`.

final result: passed

## Implemented direction

- Native picture source selects a new portrait composition on phones and portrait tablets. Live headline, body and actions sit over the closed cream curtain; no separate mobile photo-above-copy layout.
- The same raised PHOTOBOOTH sign has no wood wings beside it. The portrait view crops the outer cabinet deliberately instead of stretching the whole wide booth. Original desktop exterior is unchanged.
- Native-scroll opaque paper lift now runs on standard mobile and tablet viewports. Phone travel is 32% of viewport (180–300px); portrait tablet is 35% (280–420px). Desktop travel stays unchanged.
- The incoming sheet starts at the actual hero edge, not the bottom of a taller mobile Experience section. Explore follows the animation and focuses the destination.
- Natural flow remains for reduced motion and exceptionally short/enlarged-content layouts where pinning would conceal controls. It is no longer the blanket mobile/tablet behavior.

## Evidence and visual assessment

Evidence folder: `.handoffs/responsive-editorial-lift/`. Initial same-viewport captures are in `../responsive-hero-audit/`; selected desktop storyboard remains in `../editorial-lift/`. Main agent inspected the generated source and actual rest/midpoint/arrival screenshots. No new independent reviewer pass is claimed this turn.

| Viewport (CSS pixels) | Observed result | Evidence |
| --- | --- | --- |
| 320 × 720 | Portrait hero 576px tall; integrated copy/actions, legible full sign, scroll mode. Scroll/client widths 305px. Explore focuses heading at scrollY 230, section top about 69px. | mobile-320-rest.png, mobile-320-arrived.png |
| 390 × 844 | Final portrait hero 702px tall; no overflow, selected portrait-640w asset. Native midpoint at 135px scroll, focused keyboard arrival at 270px; Back restores #top and scrollY 0. | mobile-390-rest.png, mobile-390-mid.png, mobile-390-arrived.png |
| 820 × 1180 | Portrait hero 1107px tall, portrait-960w asset, scroll mode and zero overflow. Midpoint at scrollY 207/progress .501; native continuation passes arrival. | tablet-820-rest.png, tablet-820-mid.png, tablet-820-arrived.png |
| 1180 × 820 | Wide original exterior, desktop-like overlay and scroll mode, zero overflow. | tablet-landscape.png |
| 1440 × 900 | Original desktop composition retained; native midpoint at scrollY 270/progress .545. | desktop-preserved.png, desktop-mid.png |
| 844 × 390 | Short landscape safety fallback: ordinary flow allows reaching copy and actions below the fold, zero overflow. | phone-landscape-fallback.png |

Typography: Cormorant Garamond / Manrope remain. Mobile title is a readable two-line composition at tested widths; body remains 16px. Colors: existing ivory/walnut/ebony retained, with dark-walnut supporting copy over portrait cloth. Layout: no detached copy panel, no added guest card, no opening curtain. Images: accurate referenced portrait illustration, not measured geometry. Copy: existing content and real anchor labels preserved. Separate 3D explorer and downstream content were not redesigned.

## Iterations and final checks

1. Created portrait background using built-in ImageGen from the existing approved exterior, inspected it, then saved it in the repository. `asset-prompt.md` records the exact prompt and source path.
2. Enabled phone/tablet overlap, shorter travel and hero-edge paper entry. Changed mobile aspect limit from 190vw to 180vw so the full lightbox fits at tested narrow widths.
3. A development warning identified the new picture parent as statically positioned. Added an explicit absolute/inset picture frame; verified its computed position and unchanged final layout.
4. Final production rebuild passed after stopping the agent-owned preview that briefly locked the standalone build directory on Windows. This was a local build-file lock, not an application error.
5. Final production 390px smoke: ready/loaded portrait image, scroll mode, no overflow, expected absolute picture positioning. Evidence: `production-final-mobile.png`. Console contains only the previously captured pre-repair dev warning; no new warning or error was emitted on the final production load.

## Tests and evidence limits

- Production build and TypeScript: passed after final source change.
- Full-project lint and standalone typecheck passed during implementation; final focused lint also passed for the changed TypeScript/scripts.
- Hero tests: 12/12 passed, including all responsive image candidates/aspect ratios, mobile/tablet travel and arrival, native progress/reverse, reduced flow, wheel/touch/Escape interruption, modified links, cleanup, offscreen mode changes and late fonts.
- Existing booth regressions: 4/4 passed. This is not a new complete 3D visual review.
- Browser exercised wheel-driven progress, keyboard Tab/Enter, heading focus, Back and viewport/orientation layouts. Touch cancellation is fixture-tested, not a physical-device touch test.
- Native OS reduced-motion preference, real mobile browser chrome changes, screen reader and browser text-zoom runs were not available/performed. Reduced/no-JS paths are source/fixture-reviewed, not mislabeled as live passes.
- No new network-failure injection. No form submission, external message, commit, push, deployment or dependency update. Existing advisories and unrelated user changes remain untouched.

Runtime asset: `public/images/hero/exterior-portrait.webp` (118,128 bytes) plus seven responsive derivatives. Rebuild with `node scripts/prepare-portrait-hero.mjs`. Preview: http://localhost:3000/FOTOHAVN#top.
