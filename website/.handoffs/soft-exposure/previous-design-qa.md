# Approved curtain hero — design QA

Date: 2026-09-10. Scope: revised option 3 and its curtain reveal only. Prior full-site QA remains in `.handoffs/real-booth-redesign/`; the previous project-root report is archived in `.handoffs/hero-curtain-implementation/previous-design-qa.md`.

## Source and matched comparison

All image filenames below are under `.handoffs/hero-curtain-implementation/`.

- Source visual truth: `approved-hero.png`, 1585 × 992 pixels.
- Production implementation: `http://localhost:3002/fotohvn`. User preview remains on port 3000.
- Final closed screenshot: `desktop-final.png`.
- Viewport: 1440 × 900 CSS px, DPR 1, light palette, loaded fonts/images, closed curtain, scrollY 0.
- Capture: 1425 × 891 pixels. In-app capture resampling affects the whole frame, including native text. The source was normalized to the same pixel dimensions; aspect-ratio difference is below 0.1%.
- Combined input, source LEFT / implementation RIGHT: `comparison-desktop-final-full.png`. Focused comparisons: `comparison-desktop-final-copy.png` and `comparison-desktop-final-sign.png`.
- These paired inputs were opened and compared, not inferred from separate image views.
- Final extra states: `desktop-repaired-open.png`, `mobile-repaired-320.png`, `mobile-repaired-320-open.png`, `mobile-repaired-390.png`, `tablet-repaired-820.png`.

## Comparison and repair history

1. **Initial visual comparison: blocked.** The v1 paired desktop evidence showed a moderately oversized headline and low body/actions. The opening-state control background also spanned the copy column. P2 typography/layout findings.
2. **First repair and repeat comparison.** Reduced display scale 6.4vw → 6.05vw, h1 bottom margin 32px → 24px, actions top margin 36px → 26px; shifted the copy start 32.5% → 33%. Set the open control surface to fit-content. The v2 combined comparisons and `desktop-open-v2.png` verify these changes. Body wrapping still needed a narrower measure.
3. **Second visual repair.** Narrowed the description to 390px; `comparison-desktop-closed-final-*` confirms the selected two-line copy and aligned native actions. Initial layout/type findings resolved.
4. **Independent Standards review: blocked, P2.** Soft Brown directly over the cloth did not consistently reach 4.5:1 normal-text contrast. Repaired eyebrow/body/status using a 60% Soft Brown / 40% Ebony blend; mobile paper copy keeps Soft Brown. Initial reviewer measured repaired desktop minima of approximately 4.79:1 behind eyebrow and 5.24:1 behind body. These were source-raster measurements, not antialiased-glyph sampling.
5. **Independent Spec review: blocked, P2.** Exterior image failure replaced the focused button with an anchor. Repaired it to keep the same native button, labeled RETRY ILLUSTRATION; only the image elements remount during retry.
6. **Post-repair production evidence.** Rebuilt, captured the final closed/open desktop and responsive states, and repeated the combined full/copy/sign comparison. Actual delayed HTTP image failures and successful retries were exercised with a localhost-only QA proxy. Fresh independent reviewers assess the repaired implementation separately from the initial reviewers.
7. **Focused tablet status repair.** The fresh Standards reviewer found that the transient interior-error message crossed a darker cloth fold (P2, approximately 3.2–3.4:1 at actual glyph positions). Changed only `.status` to canonical Ebony; normal closed/open typography is unchanged. A fresh production error-state capture and independent focused review follow this repair.
8. **Final gate: passed.** `tablet-error-final.png` confirms computed status color rgb(30,26,23), unchanged placement and no overflow after the final rebuild. A new focused Standards reviewer independently measured minimum contrast 5.74:1 across the whole status rectangle and 6.05:1 at the previously failing point. The final full/copy/sign desktop comparisons were recaptured and reviewed again; no new actionable difference. Fresh Spec conformance and final Standards gates both pass.

## Required fidelity surfaces

| Surface | Assessment |
| --- | --- |
| Typography | Existing Cormorant Garamond and Manrope, two-line serif headline, italic second line, small tracked eyebrow and real DOM supporting copy preserve the approved hierarchy. Minor optical differences from generated lettering are expected. |
| Spacing/layout | Close frontal facade, centered curtain copy and booking/reveal pair match. The floating guest-picture card is gone. Phones intentionally show the complete facade above readable copy/actions; there is no separate phone source mock. |
| Colors/tokens | Ebony, warm ivory, paper and walnut palette retained. The darker secondary-ink blend is a narrow accessibility correction using existing tokens. |
| Images/assets | Raised PHOTOBOOTH lightbox has clear background on both sides; timber rail below remains. Purpose-generated exterior and interior plus a registered raster curtain replace the real event hero. No CSS/SVG booth drawing. Original generation files and responsive WebPs retained. Interior details remain an artistic approximation. |
| Copy/content | Approved headline, eyebrow, description and rental action preserved. Reveal labels accurately describe open, close, waiting and retry states. No new unconfirmed marketing claims. |

No actionable visual P0/P1/P2 difference remains in the latest parent-reviewed paired evidence. Minor capture antialiasing and generated-lettering differences are P3/expected.

## Live browser checks

- Desktop pointer open/close and actual native Space, Enter and Escape: correct state and aria-expanded, same focused button, scrollY stays 0. Rapid repeated Space reverses without timers or input lock.
- Tablet opening settles at scaleX 0.085 with a computed 1.8s transition, no page-scroll change.
- Final production 320 × 720: complete sign, readable copy and controls, open/closed states. Document scroll width = client width = 305px.
- Final 390 × 844: both widths 375px. Final 820 × 1180: both widths 805px. Differences from CSS viewport are the scrollbar, not clipping.
- Mobile menu opens and Escape closes. Hero rental link reaches #rent-fotohavn about 89px below the sticky header. No form submission or external message.
- Production console warning/error query returned an empty list. Deliberate proxy 404s belong only to the failure tests, not production errors.

### Actual loading/failure tests

`scripts/hero-failure-proxy.mjs` holds/fails selected hero image requests while forwarding everything else to the local production server. No production assets were renamed or deleted.

- Delayed exterior 404 after focusing the control: button remains focused, label becomes RETRY ILLUSTRATION, scene hides, rental/copy remain usable, scrollY 0. Evidence: `exterior-error-focus.png`.
- Restored exterior + native Enter: artwork loads, button remains focused and becomes DRAW THE CURTAIN, closed state retained, scrollY 0. Evidence: `exterior-retry-success.png`.
- Held interior on tablet: CANCEL REVEAL plus loading status; Escape cancels. Evidence: `tablet-interior-loading.png`.
- Failed interior: closed scene retained, focused TRY THE REVEAL AGAIN button, status stays inside the hero, no overflow. Evidence: `tablet-interior-error.png`.
- Restored interior + Enter: opens successfully with focus retained.

## Automated checks and limitations

- Production build, ESLint and TypeScript: passed after repairs.
- Hero reducer/assets tests: 6/6 passed. They cover opt-in loading, early requests, cancellation after late completion, rapid reversal, image failure/retry, and all 14 responsive derivatives.
- Existing 3D booth regression tests: 4/4 passed.
- Git whitespace check: passed; Windows line-ending notices are not findings.
- Reduced-motion CSS removes all hero transitions. Native OS reduced-motion preference and JavaScript-disabled browser execution were not available through the selected browser's exposed capabilities. They are source-reviewed paths, not claimed live runtime passes.
- No physical-device, cross-browser or measured booth-geometry certification. The generated interior is illustrative.
- Existing baseline dependency advisories are unchanged and remain a separate pre-deployment concern, documented in the prior redesign handoff. No deployment or dependency-upgrade scope was added.

## Final independent gate

Fresh post-repair Spec reviewer: `hero_spec_release`, passed with no actionable findings. Fresh final status/Standards verifier: `hero_status_release_gate`, passed; it closes the sole remaining finding from `hero_standards_release`. Detailed separate axes and evidence limits are recorded in `.handoffs/hero-curtain-implementation/review.md`. Initial reviewers were not reused. Runtime emulation limitations above remain explicit, not mislabeled as passes.

final result: passed
