# Design QA: Healthy Guest Cycle

## Comparison target

- Visual truth: issue #20, approved Variant A (`design-qa/issue-20-variant-a-start-reference.png`, `design-qa/issue-20-variant-a-countdown-reference.png`, and `design-qa/issue-20-photo-strip-reference.png`).
- Implementation evidence: `design-qa/issue-28-start-implementation.jpg`, `design-qa/issue-28-countdown-implementation.jpg`, and `design-qa/issue-28-photo-strip-implementation.jpg`.
- Source and implementation viewport: 1280 × 720 px at 1× density. This is a native WinUI app, so CSS viewport normalization does not apply.
- States compared: Start, countdown with live mirrored Camera preview, and the ten-second Photo Strip preview.

## Findings

- No actionable P0/P1/P2 visual mismatches remain.
- Typography and hierarchy: pass — the uppercase Event label, oversized guest-facing headings, supporting copy, tracked status labels, and high-contrast countdown match Variant A.
- Spacing and layout: pass — the fixed header, centered Start composition, top-right Capture progress, primary content alignment, and Photo Strip preview follow the reference rhythm at the target viewport.
- Color and controls: pass — neutral canvas, white surfaces, black primary action, subtle borders, and visible 3 px keyboard focus styling preserve the reference palette and accessibility requirements.
- Camera state: pass — the guest preview is mirrored and labeled, the active Capture is explicit in text and four progress circles, and the countdown remains centered over the live image.
- Photo Strip state: pass — the 600 × 1800 lossless strip is shown only after decode, the full Event name is visible, the progress line is present, and the booth returns automatically after ten seconds plus the completion transition.
- Acceptance-driven differences: the live preview uses the required exact 3:2 guest frame rather than the wider issue #20 illustration; the final strip uses the required exact 2:6 output ratio rather than the wider illustrative mock. These are intentional and not defects.

## Evidence

- Full-view comparisons: each source/implementation pair was opened together at 1:1 density. Header, copy hierarchy, primary actions, Capture progress, and final-strip composition were compared across the entire 1280 × 720 surface.
- Focused regions: Start CTA foreground and pointer states; countdown overlay and progress circles; Photo Strip proportions, gutters, label, timer copy, and progress line.
- Primary interaction run: created and started a local Event, completed four real Camera Captures, observed canonical strip composition, watched the ten-second preview and 450 ms transition, and confirmed automatic return to Start.
- Persistence evidence: the exercised Guest Cycle produced exactly four canonical JPEGs, `photo-strip.png`, and a versioned completed `guest-cycle.json` with all four ordered Capture names and the Photo Strip reference.
- No unhandled native app errors were observed. Browser console checks are not applicable to this WinUI app.

## Comparison history

1. The first rendered Start comparison exposed a P1 contrast defect: the nested CTA content rendered black, and the WinUI pointer-over state replaced the black fill.
2. The CTA now pins its icon and text to white and explicitly preserves the black fill for pointer-over and pressed states.
3. Post-fix Start evidence shows the expected black/white action at rest and under pointer interaction.
4. Countdown and Photo Strip comparisons found no remaining actionable P0/P1/P2 differences.

## Follow-up polish

- P3/test gap: Operator Assistance recovery is covered by presentation and orchestration tests, but was not forced during the final physical-Camera visual run.

final result: passed
