# Initial header contrast repair 04

Date: 2026-08-22  
result: blocked

## Bounded production change

- Modified only `website/src/components/SiteChrome.module.css`.
- Added a `min-width: 768px` rule that gives the initial header the existing restrained `rgba(251, 248, 242, 0.97)` Off-white surface and existing hairline divider.
- The `<768px` transparent mobile treatment is unchanged.
- Header height, negative overlay geometry, bar grids/gutters, brand/navigation/action positions, labels, destinations, 44px targets, focus rules, menu panel, menu-open state, and scrolled selectors were not changed.
- The scrolled state continues to use the same established Off-white/hairline pairing, so the repair introduces no new palette or component state.

## Static contrast and geometry checks

- Ebony foreground: `rgb(30, 26, 23)` / `#1E1A17`.
- Initial 768px-and-up surface: `rgba(251, 248, 242, 0.97)`.
- Conservative compositing range across any possible hero pixel:
  - over black: `rgb(243.47, 240.56, 234.74)`, Ebony contrast `15.27:1`;
  - over white: `rgb(251.12, 248.21, 242.39)`, Ebony contrast `16.33:1`.
- The compact rent action remains Off-white on Ebony at `16.30:1`.
- These ratios exceed 4.5:1 for the 12px navigation/actions and 3:1 for the 26px/600 brand.
- Source constraints remain `height: 72px`; brand `min-width: 44px; min-height: 44px`; navigation links `min-height: 44px`; menu `64px x 44px` minimum; rent action `min-height: 48px`.

## Verification result and blocker

- Scoped check: `npx eslint src/components/SiteChrome.tsx` passed with exit 0. The only output was the pre-existing npm user-config warning for `email`.
- Full build and TypeScript checks were intentionally not run concurrently, per the repair instruction. Integrated build remains pending.
- Requested Browser verification is blocked: after the previously loaded Browser guidance was followed, the stale session reported unavailable; packaged browser and bootstrap troubleshooting were read; `agent.browsers.list()` returned an empty list; two fresh `iab` selection attempts also reported unavailable.
- Because the explicitly requested in-app Browser could not be selected, this repair has no fresh rendered initial/scrolled measurements at 768/820/1280/1440 and no fresh rendered overflow/layout comparison. No alternate browser surface was substituted.

## Fresh-verifier instructions

1. Once the in-app Browser is available, open `http://localhost:3011/` in a fresh session.
2. At 768x1024, 820x1180, 1280x800, and 1440x1000, confirm the initial header computes to the Off-white 0.97 surface and hairline, stays 72px high, remains sticky, and has no horizontal overflow or control displacement.
3. Verify brand plus every visible MENU/navigation/action foreground against the composited header surface; require at least 3:1 for the brand and 4.5:1 for small labels.
4. Scroll at each viewport and confirm the scrolled state remains Off-white/hairline with unchanged coordinates, targets, focus visibility, and navigation behavior.
5. Confirm a 390px control check retains the approved transparent mobile treatment.
6. Run the integrated lint/type/build checks after all parallel repairs land, then rerun both independent gates. This handoff does not claim either gate passes.

result: blocked
