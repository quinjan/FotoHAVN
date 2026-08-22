# Mobile accordion repair 01

result: passed

## Scope

Repaired only the gpt-taste verifier's P1 finding for overlapping trigger-label and panel copy in the expanded 320px vertical accordion.

## Files changed

- `website/src/components/UpperExperience.module.css`
- `website/.handoffs/repair-mobile-accordion-01.md`

`website/src/components/UpperExperience.tsx` required no change. The native full-card button, `aria-expanded`, `aria-controls`, pointer selection, keyboard focus movement, Enter/Space native button activation, exact copy/assets, desktop horizontal accordion, and reduced-motion behavior remain unchanged.

## Exact fix

- At the mobile breakpoint, `.accordionTrigger` now aligns its label at the top of the full-card button rather than sharing the bottom region with the expanded panel.
- `.accordionPanel` remains anchored 24px from the bottom and no longer carries the ineffective 64px top padding.
- The mobile-only `.accordionVeil` now darkens both the top label edge and bottom description edge while leaving the image's center comparatively open.
- The trigger label and expanded description therefore occupy separate reserved top and bottom regions without reducing the native button's full-card hit area.

## Checks

- `git diff --check -- website/src/components/UpperExperience.module.css` — passed.
- `npm run lint -- src/components/UpperExperience.tsx` — passed.
- `npx tsc --noEmit` — passed.
- `npm run build` — passed with Next.js 16.3.2; the static `/` route was generated.
- Codex in-app Browser at 320×720 — clicked `ENCLOSED`, `TOGETHER`, and `PRINTED`. Each selected item measured 256.8×420px; each label and description remained inside the item, with no intersection or clipping. The measured vertical gap between label and panel copy was 279.6px in every state. ArrowDown moved focus from `ENCLOSED` to `TOGETHER`.

## Fresh verifier instructions

1. Render the accordion at 320×720 and inspect all three states: `ENCLOSED`, `TOGETHER`, and `PRINTED`.
2. Select every state once by pointer and once through keyboard focus plus Enter/Space.
3. Confirm the trigger label remains in the top reserved region and the expanded description remains in the bottom reserved region, with neither collision nor clipping.
4. Confirm each native button still covers the full card, exposes the correct `aria-expanded` / `aria-controls` relationship, and shows its focus ring.
5. Recheck desktop at 1024px or wider to confirm the horizontal accordion and vertical labels are unchanged.

This repair handoff does not assert that the independent gpt-taste conformance gate passes.
