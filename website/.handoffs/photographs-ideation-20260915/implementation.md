# Photographs redesign implementation — 2026-09-15

Implemented in the existing online booth. No deployment or commit performed.

## Final user decisions
- Overall option 3: Camera and Arrange tabs.
- Arrange has no camera inset; actual template is both preview and selection surface.
- Capture controls option 1: round shutter bottom-center, Mirror toggle top-right inside viewfinder.
- Camera viewfinder spans the main workspace; no Photograph X of Y label or progress sidebar.
- Camera automatically requests access on entering Camera, including completed sessions. Stop ends the sequence only; no Pause/Resume controls.
- Heading Find your light.; subtitle Let this moment become a memory.
- Retake targets a selected slot and opens Camera. Candidate is reviewed in Arrange; Try again returns to live Camera. Replace opens device picker and reviews candidate in Arrange. Originals remain accepted until Use photograph.
- First capture fills remaining slots, three seconds each. Completion moves to Arrange after composition is ready. All existing filters, templates, imports, retained photographs, and PNG rendering preserved.

## Changes
OnlinePhotobooth.tsx owns tab transitions, camera lifetime, progressive controls, imports and candidate review. PrintPreview.tsx accepts file drops and displays selection/drag affordance directly in output. useComposition.ts projects candidates without changing accepted slots and keys readiness by exact render source, including same-revision candidate changes. CameraFrameGuide.tsx shows the actual normalized/template crop on desktop. Mobile viewfinder directly uses slot proportions. session.ts selects an explicit replacement target, including drops onto an unselected slot.

## Validation
Production build passed, routes /, /_not-found, /online. Scoped ESLint and TypeScript passed. 35 focused tests passed, including new file-drop targeting, candidate readiness/cancellation, and camera crop geometry coverage. Browser checked at desktop 1488x1058 and mobile320x720. Tested synthetic live stream startup, count/stop while remaining live, mirror lock, completion to Arrange, targeted retake and retry/cancel, replacement candidate acceptance, multiple imports, drag and tap Move, Filters and600x1800PNG readiness. Final production route checked with imports and console errors/warnings [].

## Limits
Countdown lifecycle and visual camera states were exercised with a synthetic MediaStream through a temporary local wrapper, not a physical-device camera acceptance matrix. Wrapper removed from app before build; retained as camera-check-fixture.tsx.txt for reproducibility. Download link and rendered PNG were verified; the in-app browser did not expose a download completion event after clicking the blob link, so filesystem delivery is not independently confirmed.

Current visual targets: selected-camera-final.png and refined-arrange.png. See website/design-qa.md for comparisons and screenshots.
