# Prototype Instructions

Run the local server yourself and open the preview in the browser available to this environment. Do not give the user server-start instructions when you can run it.

Before making substantial visual changes, use the Product Design plugin's `get-context` skill when the visual source is unclear or no longer matches the current goal. When the user gives durable prototype-specific design feedback, preferences, or decisions, record them in `AGENTS.md`.

When implementing from a selected generated mock, treat that image as the source of truth for layout, component anatomy, density, spacing, color, typography, visible content, and hierarchy.

## Issue 58 decision checkpoint

- The selected direction is the compact top-right operator control shown in the first generated option.
- Operator access requires a deliberate 1.5-second hold by pointer, touch, Space, or Enter; releasing early cancels without opening anything.
- Before confirmation, the control must not expose the words `Exit Event`.
- The existing Exit Event confirmation remains the second safeguard, with safe initial focus, focus trapping, Escape dismissal, focus restoration, and button-local `Exiting Event…` progress.

Build app UI in `src/`. Keep `.openai/hosting.json`, `worker/index.js`, `scripts/prepare-sites-build.mjs`, and `tests/sites-worker.test.mjs` intact so the same local prototype can be handed to Sites. Before a Sites handoff, run `npm run build` and `npm run test:sites`; the build must leave `dist/client/index.html`, `dist/server/index.js`, and `dist/.openai/hosting.json`.
