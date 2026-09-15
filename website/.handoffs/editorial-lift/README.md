# Editorial Lift handoff

The user chose Editorial Lift instead of Soft Exposure. Implemented and locally verified on `codex/website-real-booth-redesign`; no commit, push or deployment. Preview: http://localhost:3000/FOTOHAVN#top.

Start with `plan.md`, `review.md` and `../../design-qa.md`. Selected storyboard and paired evidence are here. Final production smoke images use `production-desktop-`; responsive evidence covers 320, 390 and 820.

## Implementation

- `HeroExterior.tsx` and CSS replace the obsolete hero curtain interaction with one closed exterior and a real Explore anchor.
- `EditorialLift.tsx`, CSS, `editorialLiftController.ts` and `editorialLiftMath.ts` implement the native-scroll overlap, interruptible anchor arrival and responsive/reduced-flow fallback.
- `UpperExperience.tsx` supplies the existing destination once, with staged headline continuation. Later sections and separate 3D remain unchanged by this selection.
- `scripts/editorial-lift.test.mjs` replaces obsolete curtain-state tests. `scripts/compare-editorial-lift.mjs` recreates paired evidence.

Obsolete curtain component/state/test code and incomplete dissolve code were removed. Historical interior artwork and proposal/QA folders remain in place, unused by the hero. Root `CONTEXT.md`, marketing/research and earlier uncommitted redesign changes were preserved. Publishing and further redesign require a new request.
