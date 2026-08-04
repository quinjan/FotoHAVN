# Prototype Instructions

Run the local server yourself and open the preview in the browser available to this environment. Do not give the user server-start instructions when you can run it.

Before making substantial visual changes, use the Product Design plugin's `get-context` skill when the visual source is unclear or no longer matches the current goal. When the user gives durable prototype-specific design feedback, preferences, or decisions, record them in `AGENTS.md`.

When implementing from a selected generated mock, treat that image as the source of truth for layout, component anatomy, density, spacing, color, typography, visible content, and hierarchy.

Build app UI in `src/`. Keep `.openai/hosting.json`, `worker/index.js`, `scripts/prepare-sites-build.mjs`, and `tests/sites-worker.test.mjs` intact so the same local prototype can be handed to Sites. Before a Sites handoff, run `npm run build` and `npm run test:sites`; the build must leave `dist/client/index.html`, `dist/server/index.js`, and `dist/.openai/hosting.json`.

## Prototype decisions

- Treat `reference-attio-event-setup.png` as Variant A's visual source of truth.
- Preserve every Event lifecycle, Saved Events, setup, dirty-state, confirmation, deletion, and Active Event decision recorded in GitHub issue 18; visual exploration must not change those behaviors.
- Target the fixed 1280 × 720 booth display at 100% scaling.
- Use `Save & Start Event` as the black primary setup action and `Save & Close` as a white outlined secondary action.
- Camera Tuning uses an icon-only gear control with the accessible name `Camera tuning`.
- Printer setup is a required selector containing only eligible Printers; for the first field test its options are `Choose a Printer` and `DNP DS-RX1HS`, with no readiness status in setup.
- Do not show a readiness status under the selected Camera in Event setup; readiness remains part of preflight.
- Camera Tuning slider tracks and thumbs use black rather than the earlier blue accent.
- Confirmed Event deletion enters a blocking, non-dismissible progress dialog for 1.8 seconds while the Event card remains visible; only after completion is the card removed and the success dialog shown.
- The `Event deleted` success dialog has one action, `Done`, and no Cancel action.
- `Year-End Party` is the deterministic incomplete-deletion fixture. Its deletion and every retry fail after progress, open the recovery dialog, and leave a quarantined `Deletion incomplete` card.
- A quarantined Event cannot be started, edited, or freshly deleted. Its only lifecycle action is idempotent `Retry Deletion`; closing the recovery dialog leaves quarantine intact and no retry implies restoration.
- Quarantine is saved outside the simulated Event directory in minimal local recovery metadata and survives reload. The prototype-only `?resetDeletion=1` query clears it for another review run.
