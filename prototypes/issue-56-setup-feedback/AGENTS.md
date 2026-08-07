# Prototype Instructions

Run the local server yourself and open the preview in the browser available to this environment. Do not give the user server-start instructions when you can run it.

Before making substantial visual changes, use the Product Design plugin's `get-context` skill when the visual source is unclear or no longer matches the current goal. When the user gives durable prototype-specific design feedback, preferences, or decisions, record them in `AGENTS.md`.

When implementing from a selected generated mock, treat that image as the source of truth for layout, component anatomy, density, spacing, color, typography, visible content, and hierarchy.

## Durable design decisions

- Base the prototype on generated direction 2: a seamless left form/readiness section and right live-preview section.
- Do not place a global loading banner at the top of Event setup. Delayed-action feedback belongs beside the action that initiated it.
- Keep readiness guidance attached to its field and keep disabled-action explanations immediately above the footer actions.
- Successful setup states are silent: do not show passive checks or generic “Ready”/“Selected” copy beside controls. Show compact field-level information only while checking or when the operator must act.
- Validate the Camera immediately after selection. Show checking and failure information beneath the Camera field, and remove it once the Camera passes.
- Do not show a setup-wide readiness, error, or retry notice above the footer actions; keep progress inside the initiating button and recovery beside the affected field.
- The approved Event Setup modal uses 1 GB as its minimum free-storage requirement and always names the Event destination as `C:\Program Files\FotoHAVN\Events`. The insufficient-storage scenario keeps other required fields valid, shows available capacity plus the required recovery amount beneath Storage, and disables both save actions.
- Use the footer's top border as the sole divider after Storage; the Storage row must not draw a competing bottom border.
- Saved Event cards keep Start Event visible and pair it with compact readiness plus 48 × 48 Edit/Delete actions.
- Failed Camera checks must change the Camera status truthfully, retain setup context, and focus the first recovery action without breaking the left/right layout.
- Confirmation dialogs focus the safe action, contain keyboard focus, support Escape, isolate the background, and restore focus; successful destructive actions close the dialog and announce the destination state.

Build app UI in `src/`. Keep `.openai/hosting.json`, `worker/index.js`, `scripts/prepare-sites-build.mjs`, and `tests/sites-worker.test.mjs` intact so the same local prototype can be handed to Sites. Before a Sites handoff, run `npm run build` and `npm run test:sites`; the build must leave `dist/client/index.html`, `dist/server/index.js`, and `dist/.openai/hosting.json`.
