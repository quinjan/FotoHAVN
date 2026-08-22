# Hero LCP warning repair 02

result: passed

## Scope

- Production scope honored: `website/src/components/UpperExperience.tsx` only.
- No CSS or other production file was modified by this repair.
- This is a bounded repair result, not an independent gpt-taste or responsive-design gate pass.

## Exact change

- Removed the hero image's ineffective `preload` prop.
- Added `loading="eager"`, the Next.js 16.3.2 contract requested by the live LCP diagnostic.
- Statically imported the same local `website/public/images/hero-booth.png` asset and used that import only for the LCP hero.
- Kept the lower bento reuse on the public `/images/hero-booth.png` URL and its default lazy behavior.

The URL isolation is required because Next.js 16.3.2 development diagnostics store image loading metadata by generated URL. `UpperExperience` and `MiddleExperience` both contain below-fold lazy instances of `/images/hero-booth.png`; when the hero used that same generated URL, a later lazy registration could overwrite the eager hero's metadata and emit the warning even though the hero DOM was eager. The static import preserves the exact image file while giving the hero a distinct generated static-media URL. No below-fold image was made eager.

Preserved hero contracts: `next/image`, `fill`, `sizes="100vw"`, existing alt text, CSS class, stable fill geometry, artwork, copy, and behavior.

## Checks

- `node_modules/.bin/eslint.cmd src/components/UpperExperience.tsx`: passed, exit 0.
- `npx tsc --noEmit`: passed, exit 0.
- `npm run build`: passed, exit 0; Next.js 16.3.2 compiled and statically generated `/`.
- Server-rendered HTML inspection: passed. The hero emits `loading="eager"` with the static-media `hero-booth` URL; the lower bento and middle-experience instances emit `loading="lazy"` on the separate public URL.
- In-app Browser diagnosis before the final URL isolation: a fresh same-origin load initially had zero warning/error entries and showed the hero eager and complete; the required 320px-to-1440px resize then reproduced the warning and exposed the remaining public-URL collision from `MiddleExperience`.
- Final in-app Browser console observation after URL isolation: not completed. After the shared development server restarted, the fresh tab entered Chromium's connection-error data page, and the Browser URL safety policy blocked recovery navigation to localhost. No alternate browser surface was used. The fresh independent verifier must confirm the console result.

## Files changed

- `website/src/components/UpperExperience.tsx`
- `website/.handoffs/repair-hero-lcp-warning-02.md`

## Fresh verifier instructions

1. Start the Next.js 16.3.2 development server and open a brand-new same-origin `http://localhost:3000/` in-app Browser tab.
2. Confirm the hero image with alt text `The vintage-style enclosed FOTOHVN booth in a warm, tactile setting.` has `loading="eager"`, remains a `next/image` fill image, uses `sizes="100vw"`, completes successfully, and resolves to the statically imported `hero-booth` media URL.
3. Confirm the lower `The FOTOHVN enclosed booth.` image remains lazy and resolves through the public `/images/hero-booth.png` optimizer URL.
4. Wait at least four seconds for LCP observation, resize 1440x1000 to 320x720 and back, exercise a native interaction, and wait again.
5. Inspect the complete warning/error console stream. Require no LCP warning, hydration warning, or error before passing the independent gate.
