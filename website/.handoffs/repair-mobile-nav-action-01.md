# Mobile navigation action alignment repair 01

result: passed

## Bounded finding

Repaired only the P2 finding that the 320px mobile `FIND A BOOTH` action was not centered like `RENT FOTOHVN`.

## Files changed

- `website/src/components/SiteChrome.module.css`
- `website/.handoffs/repair-mobile-nav-action-01.md`

`website/src/components/SiteChrome.tsx` was inspected and left unchanged.

## Exact fix

The shared `.mobileFindAction, .mobileRentAction` rule now explicitly applies:

- `display: inline-flex`
- `align-items: center`
- `justify-content: center`

This gives the secondary `FIND A BOOTH` action the same display and centering contract as its primary peer. Its light outlined styling, 48px minimum target, `#find-a-booth` destination, menu-closing click handler, and shared 2px/3px-offset focus ring remain unchanged. Escape handling, outside-click closing, and focus return are untouched because no TypeScript was modified.

## Checks

- `npx eslint src/components/SiteChrome.tsx` — passed.
- `npx tsc --noEmit` — passed.
- `npm run build` — passed; Next.js 16.3.2 compiled, type-checked, and statically generated `/`.
- Scoped source inspection — passed: both mobile actions retain `width: 100%` below 768px and `min-height: 48px`, while only the intended shared centering declarations were added by this repair.

## Fresh verifier instructions

At a 320px browser viewport:

1. Open the mobile menu.
2. Confirm `FIND A BOOTH` and `RENT FOTOHVN` each render as full-width 48px-or-taller controls with horizontally and vertically centered labels.
3. Tab to `FIND A BOOTH` and confirm its visible 2px focus indicator with 3px offset is not clipped.
4. Confirm activating it still routes to `#find-a-booth` and closes the menu.
5. Recheck Escape close plus focus return and outside-click close as regression coverage.

This repair handoff does not claim that the independent gpt-taste conformance gate passes.
