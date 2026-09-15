# Curtain-led hero handoff

Date: 2026-09-10. Branch: `codex/website-real-booth-redesign`.

## Approved result

The user selected proposal 3, The Threshold, then approved removal of the timber wings beside the raised PHOTOBOOTH lightbox and asked for the proposed animation. `approved-hero.png` is the exact visual authority. The full-width horizontal timber rail below the sign remains.

The live hero now uses a generated facade, real HTML typography/actions and a user-initiated curtain reveal. It gathers the photographic cloth layer to the right over 1.8 seconds, reveals a warm generated interior, and fades the desktop headline. CLOSE THE CURTAIN or Escape reverses it. Phones keep the complete facade above the copy/actions. No real-photo hero or floating couple card remains.

This is a composited raster reveal, not a replacement for the separate interactive Three.js booth further down the page. The unseen interior is an artistic interpretation, not a measured scan. Existing sections, inquiry, anchor contracts and the earlier redesign are preserved.

## Files and assets

- `src/components/HeroCurtain.tsx` — isolated client component, native button, loading/error/retry/focus behavior and no-JS fallback.
- `src/components/HeroCurtain.module.css` — responsive composition, registered photographic crop and motion/reduced-motion styles.
- `src/components/heroCurtainState.ts` — pure, timer-free interaction state.
- `src/components/UpperExperience.tsx` and its CSS — hero integration; existing experience section retained.
- `src/components/EventPhoto.tsx` — existing static responsive-image loader reused for generated art.
- `public/images/hero/` — two WebP sources and fourteen responsive variants.
- `hero-exterior-source.png` / `hero-exterior-prompt.md` and `hero-interior-source.png` / `hero-interior-generation.md` — original generated artwork and provenance.
- `scripts/prepare-hero-assets.mjs` — deterministic Sharp encoding. Exterior source 1652 × 952; interior 1316 × 1195. No creative image edits in the script.
- `scripts/compare-hero.mjs` — normalized source-left/render-right QA boards, with full/sign/copy crops.
- `scripts/hero-failure-proxy.mjs` — local-only image failure test harness; not an application route or deployed feature.
- `@phosphor-icons/react` — direct light-weight arrow imports; no handcrafted image stand-ins.

## Verification and continuation

Authoritative current report: `website/design-qa.md`. Independent review: `review.md`. Historical QA was preserved as `previous-design-qa.md` before updating the project-root report.

From `website/`:

```powershell
npm run dev
npm run lint
npx tsc --noEmit
npm run test:hero
npm run test:booth
npm run build
```

Preview is `http://localhost:3000/fotohvn`, including the configured base path. The final production build was separately exercised on localhost:3002. Verification-only services on 3002/3003 were stopped; the existing dev server on 3000 remains running, and the preview tab is marked to stay open with its temporary viewport override cleared. Do not use a root-path-only URL or replace localhost with 127.0.0.1 in browser QA.

To repeat image failure tests, run the production app on port 3002, then run `node scripts/hero-failure-proxy.mjs`. Open localhost:3003/fotohvn, focus the hero toggle, request `/__qa/fail-image`, and inspect the retained focus/retry UI. Request `/__qa/restore-image` and activate the focused retry. Set `HERO_QA_IMAGE=interior` before starting the proxy for interior loading/cancel/error checks. Control endpoints affect only this test proxy; production files are unchanged. Stop the test services after use.

Source-reviewed but not live-emulated: native reduced-motion OS preference and JavaScript-disabled browser operation. The selected browser exposes viewport control but not those emulation capabilities. No physical-device/cross-browser certification is claimed.

## Ownership and release boundary

Changes remain uncommitted on the existing redesign branch. Root `CONTEXT.md`, `marketing/`, `research/` and prior redesign work belong to the user and were preserved. No push, external message, deployment, issue closure or unrelated dependency upgrade was performed. The previous handoff's baseline package advisories remain a pre-deployment concern.
