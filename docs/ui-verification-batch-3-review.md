# Batch 3 UI verification review

Date: 2026-08-08

PR: [#83](https://github.com/quinjan/FotoHAVN/pull/83)

The authoritative source commit is recorded by the `Batch 3 UI verification`
workflow and its uploaded `run.json`. This review was
completed against the final local pre-commit evidence before that exact-commit
workflow run.

## Pinned environment

The verification host accepted the environment without
`--allow-environment-drift`:

- Windows build 26200, x64 process and OS
- .NET SDK 10.0.302
- `en-PH` culture and `en-US` UI culture
- 120 DPI, Light theme, ClearType

The repository runner is named `FotoHAVN-Pinned-UI-QUINJ3875` and has the
labels `fotohavn-ui-verification`, `windows-26200`, `dpi-120`, and
`dotnet-10.0.302`. It runs interactively because WinUI screenshot capture
requires a signed-in desktop. The workflow never runs for pull-request or fork
events. Its bootstrap push trigger is owner-only and restricted to this PR's
branch; after the workflow reaches the default branch, ordinary evidence runs
use `workflow_dispatch`.

The host keeps FotoHAVN topmost during capture so screen-copy evidence cannot
silently capture another application. UI-verification instances use a
process-scoped App SDK key so a terminated registration cannot redirect a later
fixture to an invalid window.

## Final execution

All 48 Batch 3 fixtures were executed through the shared host with
`--completed-through-batch 3`. Each fixture produced `actual.png`, `diff.png`,
and `result.json`. The final local evidence is under
`artifacts/ui-verification/issue-75-pinned-final-v5`; its summary reports:

- 48 total results
- 48 complete target/actual/diff evidence sets
- 0 environment-drift results
- 0 UI Automation findings
- 0 exact pixel matches and 48 `review-required` results
- changed-pixel ratios from 6.04% to 66.94% (32.76% average)
- application fingerprint
  `e7ec9dcd52380047a20d494431bf920dbe296db9987a90cda93b72d09ceee887`

`review-required` is intentional: the host uses an exact-pixel comparator and
does not automatically approve visual differences. Review disposition remains
a human gate rather than a tolerance hidden in the capture tool.

## Approved decisions represented in the evidence

- The Event Card's whole-card **Start Event** action remains visually hidden at
  rest, matching the target, while remaining available to accessibility and
  interaction semantics.
- Event Camera preview is 16:9 and carries a 3:2 capture guide.
- Compact Event identity uses the correct `2F91 · C4E8` suffix.
- Setup reading order is Event name, Camera, preview, Printer, then Storage;
  Compact and Stress stack this order and scroll vertically.
- Busy confirmations disable both actions, cannot be cancelled, and put focus
  on the heading/status.
- Stress confirmations use 16-pixel outer margins with full-width stacked
  actions; Compact confirmation actions and Event Card columns reflow at the
  approved breakpoints.

## Visual review disposition

All 48 target/actual/diff sets were reviewed in eight contact sheets. No
fixture has unexplained clipping, overlap, missing product action, broken
responsive behavior, or semantic regression. The remaining visible
differences are explained and accepted as follows:

1. Native WinUI and the browser target renderer differ in text and icon
   antialiasing, control templates, disabled-state rendering, focus visuals,
   and a small amount of intrinsic spacing.
2. Production deletion confirmations retain the stronger safety copy that all
   Guest Cycles are removed and cannot be recovered. The shorter renderer copy
   does not supersede that domain-safety requirement.
3. Save and Discard confirmations retain the Event Setup surface behind their
   modal scrim because Setup is the real invoker. The renderer's Saved Events
   placeholder is not the production navigation context.
4. A deletion-incomplete Event Card hides Edit/Delete and exposes **Retry
   deletion**. That quarantine behavior is required by the production deletion
   contract even though the visual target still shows the ordinary card
   actions.
5. The production Camera preview uses the approved deterministic image with the
   native WinUI crop, live/mirrored badge, and 3:2 guide treatment.

## Acceptance decision

The local Batch 3 visual and semantic review passes. PR #83 may be marked ready
only after the `Batch 3 UI verification` workflow succeeds on the exact
final branch commit and its uploaded artifact independently confirms all 48
evidence sets, the pinned environment, and zero semantic findings. The stable
workflow page is
[Batch 3 UI verification](https://github.com/quinjan/FotoHAVN/actions/workflows/ui-verification-batch-3.yml).
