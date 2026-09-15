# Experience implementation result

Status: scoped implementation complete; integrated type/build, independent plan conformance, and responsive QA remain with root.

## Implemented

- Replaced the old customer-photo manifesto in `src/components/UpperExperience.tsx` and its stylesheet with the selected Option 2 copy, an airy headline with italic `possibility.`, a full-width 5/2/5 photographic band, three supporting offers, and the online invitation.
- Uses only `/images/experience-online/curtain.webp`, `lens.webp`, and `keepsake.webp` inside Experience. Root's prepared width siblings are present. `EventPhoto` receives breakpoint-specific image sizes; no image pipeline or asset files were edited by this agent.
- Preserved the existing `EditorialLift` and hero, `#experience`, focusable `#experience-heading`, and `data-lift-follow` hook. The section, captions, and invitation remain in intrinsic flow. Neither the existing controller nor its measured-height behavior was modified.
- Below 768px, each photograph is immediately followed by its caption in a vertical editorial sequence with 24px gutters. The three photographs use landscape, wider detail, and square crops respectively. Tablet keeps the unequal photographic band, with a two-line headline and a stacked invitation/action.
- Added scoped `ExperienceReveal.tsx`: one-time, 600ms opacity/16px reveals on offers and invitation. Content is visible before enhancement; late hydration does not hide already visible text. Observers and animations are cleaned up, and reduced motion bypasses or settles the reveal.
- Added `ONLINE BOOTH` to the shared desktop and mobile navigation, using `withSiteBasePath("/online")`. Navigation spacing tightens at 1024–1199px, and labels do not wrap.
- Added `MAKE A DIGITAL STRIP` after the existing Prints copy and guest-album action. The original guest-album action and physical-print narrative remain. New links use Phosphor's SSR ArrowRight icon and restrained pointer/focus feedback, with travel removed under reduced motion.

## Source ownership

Edited only the assigned existing files:

- `src/components/UpperExperience.tsx`
- `src/components/UpperExperience.module.css`
- `src/components/SiteChrome.tsx`
- `src/components/SiteChrome.module.css`
- `src/components/MiddleExperience.tsx`
- `src/components/MiddleExperience.module.css`

Added the scoped motion component `src/components/ExperienceReveal.tsx` and this handoff. Existing unrelated changes in these files were retained except the explicitly superseded Experience interior/styles. No global CSS, hero/lift source, online-route source, manifests, generated assets, or browser session was modified by this agent. No commit was created.

## Verification evidence and remaining work

- Read `website/AGENTS.md`, local Next.js server/client component and navigation guidance, and the local `basePath` reference before writing code. Native anchors use the explicit site helper; no duplicate base-path prefix from `next/link` is involved.
- Scoped ESLint passed with exit 0 for `UpperExperience.tsx`, `ExperienceReveal.tsx`, `SiteChrome.tsx`, and `MiddleExperience.tsx`.
- Scoped `git diff --check` passed with exit 0; Git only emitted the repository's LF-to-CRLF normalization notices.
- Read the root-captured `experience-initial-browser.png` at 843×1032. The three images, all story offers, and the online invitation are visibly present. Root reported document overflow 0 at that initial viewport. This spot-check is not the independent conformance or complete responsive gate.
- Root still owns the full type/build checks, fresh plan-conformance review, 320px/desktop responsive reviews, anchor/lift runtime checks, and all actual online-route functionality tests. Inspect the new desktop navigation around 1024px and headline/invitation wrapping at large widths as part of those reviews.

## Useful seams

- The `stories` data in `UpperExperience.tsx` is the single source for asset names, alt text, offers, and responsive image sizes.
- `.privacy`, `.look`, and `.keepsake` place photographs and their captions independently in the shared desktop grid while preserving their figure/caption reading order on mobile.
- `.introduction`, `.stories`, and `.invitation` share a 1600px maximum width; the photos reach the section edges and text aligns to the site's gutter token.
- The only DOM query in `ExperienceReveal` is scoped to its own root and `[data-experience-reveal]`; it does not share selectors or ScrollTriggers with other website sections.
