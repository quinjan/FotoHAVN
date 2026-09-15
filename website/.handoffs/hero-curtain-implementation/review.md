# Independent hero release review

Date: 2026-09-10. Fixed point: `107e895a488747c56031136d77b9af31d48930dd`.

The branch is uncommitted WIP. Review used `git diff 107e895a488747c56031136d77b9af31d48930dd -- website` plus direct inspection of the new untracked hero source/assets/scripts. A three-dot-to-HEAD diff would exclude the WIP. Scope is the approved hero seam, not root user changes or the previously accepted full-site redesign. The spec is `plan.md` and the user-approved corrected mock.

Standards and Spec were independently reviewed in parallel through the code-review workflow. Reviewers were read-only and fresh after completed repair gates. Parent live-browser verification is recorded separately in `website/design-qa.md`.

## Standards

Initial reviewer `hero_standards_review` found one P2: Soft Brown over the curtain failed normal-text contrast. It found no actionable smell-baseline concern; hero tests passed 6/6. The text was darkened using existing brand inks.

Fresh reviewer `hero_standards_release` independently measured desktop body contrast at least 5.24:1 and mobile Soft Brown on Off-white at 5.33:1. It found no additional code/standards concern except one remaining P2: the tablet transient error status crossed a darker fold, giving approximately 3.25–3.42:1 at substantial glyph strokes. This was fixed by using canonical Ebony only for status text.

Final fresh verifier `hero_status_release_gate`: **passed — no Standards findings.**

The status uses canonical Ebony, #1E1A17. Independently scanning the text-free exterior beneath the entire status rectangle, including conservatively rounded edges, gives a minimum contrast of **5.74:1**, exceeding DESIGN.md's 4.5:1 requirement. The previously failing background point now measures **6.05:1**, versus 3.18:1 with the previous blend. The supplied final screenshot retains readable status, closed artwork and actions. The one-property accessibility repair conforms to the approved plan.

These were source, supplied-image and raw-asset pixel reviews, not independent live-browser checks.

## Spec

Initial reviewer `hero_spec_review` found one P2: exterior failure replaced the focused button with an anchor. The repair keeps one native button throughout loading/errors/retry/recovery; only image components remount. The parent reproduced delayed HTTP404, retained focus and successful keyboard recovery in the browser.

Final fresh reviewer `hero_spec_release`: **Spec/design-plan conformance passed with 0 actionable findings.**

- No missing or partial implementation requirements, scope creep, or wrongly implemented requirements found in the hero seam.
- Stable toggle resolves the earlier focus issue. Error/recovery captures support the parent's reported verification.
- Typography: Cormorant headline, italic second line and restrained Manrope hierarchy match the approved direction.
- Spacing: desktop composition/actions align closely; complete-facade mobile reflow follows the plan.
- Color: palette consistent; darker secondary ink is an appropriate contrast calibration.
- Imagery: generated frontal facade, clear space beside the lightbox and retained lower timber rail preserved. Registered cloth reveals the compatible illustrative interior.
- Copy: headline, description and native rental/reveal actions match the approved composition.

Independently ran hero tests: 6/6 passed. This was source and combined source/rendered evidence review, without independent browser operation. Native reduced-motion OS testing remains an explicit residual gap, not a runtime pass. The subsequent status-only Ebony repair was also confirmed plan-conformant by the fresh final verifier.

Summary: Standards 0 outstanding findings; Spec 0 outstanding findings. No actionable P0/P1/P2 issue remains in the scoped hero gate.
