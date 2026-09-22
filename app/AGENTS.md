# Desktop app work

Use this guide for the complete desktop module under `app/`: source, tests, tools, prototypes, design contract, QA evidence, field-test tooling, and local scripts.

## Load

- Read `../CONTEXT-MAP.md`, `../CONTEXT.md`, and `CONTEXT.md` for booth behavior and language.
- Read `docs/adr/0001-capture-from-one-windows-video-stream.md` for Camera stream ownership, capture timing, or format-selection work.
- Read `docs/design-system/README.md` before changing WinUI visuals, interaction, accessibility, responsive behavior, or UI language.
- Read `docs/local-development-guide.md` before building, running, publishing, or using Camera hardware.
- Read `docs/ui-verification-host.md` before producing visual or UI Automation evidence.

## Boundaries

- Keep domain behavior testable through `FotoHavn.Core`; isolate Windows and WinUI integration in the app and integration seams.
- Treat `docs/design-system/` as the normative desktop UI contract and historical screenshots as evidence.
- Treat an Available Camera as unproven until the exact device produces an eligible fresh frame.
- State hardware limits explicitly: synthetic or automated Camera checks do not establish physical-device behavior.

## Verification

- Run focused tests while iterating.
- Run commands from `app/` and run `./scripts/verify-desktop.ps1` before declaring the desktop scope complete.
- For UI-affecting changes, exercise the required states through the UI verification host and inspect rendered evidence.
- For field-test candidates, use the qualified publish and portable-launch scripts named by the local development guide, then complete the booth-laptop qualification record.

Completion requires the verification command to pass and every untested hardware, accessibility, or visual state to be named.
