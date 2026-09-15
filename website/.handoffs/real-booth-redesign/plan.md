# FOTOHVN real-booth redesign

Date: 2026-09-09. Branch: `codex/website-real-booth-redesign`. Baseline: `107e895a488747c56031136d77b9af31d48930dd`.

## User request and authority

Completely redesign the website on a new branch, preserving its colors and editorial, intimate feeling. Use the actual booth and guests in the Evia event photographs, add substantially more animation, and build an interactive 3D booth. This is implementation authorization for this branch. The new brief supersedes the old prescribed page compositions and quiet-motion ceiling. The canonical palette and brand identity remain authoritative. Existing uncommitted `CONTEXT.md`, `marketing/`, and `research/` work belongs to the user.

## Audit

The current single-page site has a generated full-bleed hero, image bento, horizontal accordion, two text marquees, vertically repeated photographs, print stacks, editorial-note carousel, and inquiry. Its imagery depicts a different booth from the real one. It repeats imagery and copy over a long scroll. Its navigation and mailto inquiry are functional. No real 3D asset or viewer exists.

Keep `/fotohvn`, `#top`, `#main-content`, `#experience`, `#the-booth`, `#prints`, `#find-a-booth`, `#rent-fotohavn`, `#inquiry`; keep primary navigation labels, form field names/order, email/social links, metadata title and staging noindex. Do not invent pricing, exact dimensions, availability, testimonials, or production approval.

## Design

Reading this as an editorial photography brand for people planning celebrations, combining warm paper, candid guest photography, and an interactive view of the physical booth. Dials: DESIGN_VARIANCE 8, MOTION_INTENSITY 8, VISUAL_DENSITY 3. Use existing Next.js, native CSS Modules, GSAP, and a dynamically imported Three.js scene.

- Hero: a luminous paper composition. Oversized Cormorant heading, two clear paths, an actual close booth photograph, and a small guest print tucked against it. Character comes from scale and real photography.
- Experience: a spacious typographic statement surrounded by two offset guest photographs. Scroll reveals bring the printed images into their final arrangement. Three concise verbs explain the ritual without feature cards.
- Booth: a large warm-paper studio for a real WebGL model, with drag-to-rotate, keyboard rotation/zoom, front/side/inside presets, reset, and curtain open/close. Small controls and a compact materials description support the model. Include a visible real-photo comparison and an honest note that the model is an illustration of the booth, not a measured floor plan.
- Prints: a staged reveal of actual prints photographed on the event table and actual people holding their keepsakes. Do not fabricate sample Photo Strips from event portraits.
- Guest album: differently sized real photographs in a horizontal editorial sequence on desktop, scroll-snap with Previous/Next buttons on mobile. Motion is user-driven and respects reduced motion.
- Visit/rental: two spacious editorial paths with direct details and existing inquiry destinations. No unconfirmed permanent Evia location or outdated event promo prices.
- Inquiry: retain field names, ordering, required inputs and the email-app handoff. Use the established mailto behavior with encoded details, no false submission-success message.
- Footer: oversized FOTOHVN typography, social/contact links and current legal line.

## Physical reference

Source directory: `marketing/reels/fotohvn-evia-event/public/assets/Pictures/`. The user's `assests/pictures` spelling resolves to this existing directory. All web photographs are optimized derivatives of these originals. Preserve originals. Contact sheet: `source-contact-sheet.jpg`.

The model follows the rectangular walnut cabinet, pale fabric curtain gathered to one side, warm rectangular PHOTOBOOTH lightbox, front-left photo display and print hatch, tall front-right glazed window, and raised plinth visible in `3f970799-a9ca-4a6e-966a-3a804a2ebc8a.jpg` and `1000018870.jpg`. Unseen rear/interior geometry is interpretive. No dimension claims.

## Motion and reliability

Hero entry establishes hierarchy. Scroll-triggered photo movement recalls arranging prints. Curtain movement reveals enclosure/interior. View presets help inspect the booth. No scroll wheel interception, forced intro, autoplay video or endless decorative loop. All content server-rendered and visible without animation; no-JS gallery remains usable. Reduced motion removes automatic transforms and makes controls immediate. Lazy-load WebGL near the viewport; render on demand, cap pixel ratio, suspend offscreen, dispose all GPU/event resources, handle WebGL failure and context loss with a real-photo fallback. Touch keeps vertical page scrolling available.

## Brand-specific skill interpretations

The user's real-photograph requirement takes precedence over the design skill's image-generation default. The preserved warm palette and Cormorant are intentional existing brand choices. The brand remains light-led under both system color preferences. Existing footer contrast is retained. Three.js is an interactive representation explicitly requested by the user, not a substitute for real photography. Keep native GSAP rather than introducing a second UI animation library.

## Acceptance

Run lint, typecheck, production build, and diff checks. Use independent code/spec review after implementation. Check the plan against the rendered site, then desktop 1440, tablet 820, mobile 390 and 320. Verify actual pointer/keyboard controls, every viewpoint, curtain, gallery, menu/Escape, form validation without sending email, anchor offsets, image loads, overflow, and console. Record evidence and distinguish verified passes from tooling limits. Run Lighthouse if the environment supports a scoped audit. Preserve a fresh-agent handoff with asset mapping and model limitations.

## Technical references

- Next.js local bundled guides: server/client components, lazy loading, images, fonts.
- [Three.js WebGLRenderer](https://threejs.org/docs/#WebGLRenderer)
- [Three.js OrbitControls](https://threejs.org/docs/#OrbitControls)
