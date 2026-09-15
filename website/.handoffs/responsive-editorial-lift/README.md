# Responsive Editorial Lift — complete

User approval: “implement it now” after the portrait hero recommendation. Implemented on existing `codex/website-real-booth-redesign`; uncommitted, not deployed.

Preview: http://localhost:3000/fotohvn#top. Current verification: `../../design-qa.md`. Scope/decisions: `plan.md`. Generated source, final exact prompt and asset provenance: `asset-prompt.md` and `portrait-source.png`.

Main seams: HeroExterior.tsx/CSS (native responsive picture and over-curtain copy), editorialLiftMath.ts (geometry eligibility and responsive travel), editorialLiftController.ts (shorter Explore and hero-edge paper entry), scripts/editorial-lift.test.mjs (12 passing tests), scripts/prepare-portrait-hero.mjs (compression/responsive files).

Desktop artwork, curtain-closed behavior, semantic copy/actions and separate 3D explorer remain. On phones and portrait tablets the signature animation is now enabled; reduced motion or oversized content/very short viewports retain readable flow. Artwork is a deliberately cropped portrait camera view, not a compressed full cabinet or measured model.

Screenshots in this folder document rest/midpoint/arrival and desktop preservation. Final production phone screenshot is `production-final-mobile.png`. Earlier QA is archived as `previous-design-qa.md`. No material files deleted; new artwork is a sibling asset, original remains.
