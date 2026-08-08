# Batch 3 UI verification review

Date: 2026-08-08  
Source commit: `7a0d19cd24b973e3ac2255203aca6273b85f5dab`  
Application SHA-256: `50ee2707cc69a2bc10a14c728e648517451572fdbc71268fefa8533e6eb04388`

## Environment

The verification host accepted the checked-in pinned environment without
`--allow-environment-drift`:

- Windows build 26200, x64 process and OS
- .NET SDK 10.0.302
- `en-PH` culture and `en-US` UI culture
- 120 DPI, Light theme, ClearType

The host keeps FotoHAVN topmost during capture so screen-copy evidence cannot
silently capture another application. UI-verification instances use a
process-scoped App SDK key so a terminated registration cannot redirect a later
fixture to an invalid window.

## Execution

All 48 Batch 3 fixtures were executed through the shared host with
`--completed-through-batch 3`. Each fixture produced `actual.png`, `diff.png`,
and `result.json`. The local run is under
`artifacts/ui-verification/issue-75-pinned-final` and `run.json` reports:

- 48 total results
- 0 exact matches
- 48 `review-required` results
- 224 UI Automation findings
- changed-pixel ratios from 32.17% to 94.02% (69.06% average)

## Review findings

The capture itself is valid and unobstructed, but the Batch 3 acceptance gate
does **not** pass yet. The remaining differences are systematic rather than
environment drift:

1. All semantic surface roots expose WinUI `Group` or `Pane`, while the approved
   annotations require `Window` (48 findings).
2. Setup-field and confirmation reading-order checks do not resolve the approved
   Camera/status, Printer, Storage, safe-action, and confirming-action landmarks
   consistently (at least 88 findings).
3. Page-heading and safe-action focus expectations remain unmet in 21 fixtures.
4. Required polite/assertive live-region events are missing in Saved Events,
   Event setup, and confirmation states.
5. The host currently treats internal 12-pixel ScrollViewer buttons as product
   targets, producing target-size findings that need either product remediation
   or a host rule scoped to framework chrome.
6. Pixel output differs materially from the approved renderer targets. Manual
   inspection confirmed that the real FotoHAVN window was captured, compact and
   Stress content remains scrollable, the setup header/footer remain fixed, and
   Stress Event Card actions no longer overlap. The current WinUI compositions
   nevertheless differ in spacing, card/dialog proportions, icon treatment,
   identity anatomy, and action-rail layout.

## Decision

Do not mark PR #83 ready or replace the approved targets from this run. Resolve
the semantic/focus/announcement findings and either align the WinUI pixels with
the approved targets or obtain an explicit design approval for a new baseline.
Rerun all 48 fixtures afterward; the acceptance gate is complete only when no
result contains an unexplained visual or semantic regression.
