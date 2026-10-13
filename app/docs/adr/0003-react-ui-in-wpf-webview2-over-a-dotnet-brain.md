---
status: accepted
date: 2026-10-13
---

# React UI in WPF WebView2 over a .NET brain

The FOTOHAVN desktop booth is one .NET 10 process. A WPF host shows a React user interface in WebView2, and a .NET brain with no Windows dependencies owns every rule, timer, and adapter. React renders the presentation the brain sends and turns touches and keys into commands; it never decides a rule, stores Event data, or reaches the camera.

Replaces: the deleted WinUI 3 desktop app (`FotoHavn.App` and `FotoHavn.Core`) and its design contract, which named WinUI as the behavioral implementation. The whole app is rebuilt; only `app/docs/research/` and the [implementation plan](../implementation-plan.md) are kept (D19).

## Context

- The guest workflow follows the website's online booth, which is already built in React ([`OnlinePhotobooth.tsx`](../../../website/src/components/onlinePhotobooth/OnlinePhotobooth.tsx)). The booth needs the same flow, but persistent, Event-bound, and timed.
- The booth also needs Windows-only capabilities: `MediaCapture` for the webcam, display enumeration for two screens, a durable Data folder, wake lock, and single instance.
- Rules must be unit-testable without a window, a camera, or a display, so most proof can run headlessly ([ADR 0007](0007-unit-first-testing-with-real-camera-end-to-end.md)).

## Decision

**Process.** `FOTOHAVN.exe` is one self-contained x64 .NET 10 process. Brain projects target `net10.0`; Windows projects target `net10.0-windows10.0.26100.0`.

**Responsibilities.**

| Part | Project | Owns | Never does |
| --- | --- | --- | --- |
| Brain | `FotoHavn.Core` | Event setup, the Active Event, the Guest Cycle, Phase Timers, Camera Binding and Guest Display Binding rules, Operator PIN rules, and role checks, through injected interfaces (clock, camera, displays, store, compositor) | Call a Windows API |
| Protocol | `FotoHavn.Protocol` | Command and presentation records, JSON options, schema export | Hold rules |
| Host | `FotoHavn.Host.Windows` (WPF + `Microsoft.Web.WebView2`) | The Operator and Guest windows, one WebView2 control per window, the bridges, the display adapter, input guards, wake lock, and single instance | Hold domain logic |
| UI | `app/ui/` (React 19, TypeScript, Vite, CSS Modules, Phosphor Light icons) | Rendering each role's presentation and sending commands | Store Event data, decide a rule, or open the camera |
| Adapters | `FotoHavn.Storage`, `FotoHavn.Camera.Windows`, `FotoHavn.Catalog` | The brain's interfaces | Other adapters never write Event data. The Event store (`FotoHavn.Storage`) is the only writer of Event data: `fotohavn.db`, `Events/`, and `Cache/thumbnails/`. The host owns `logs/` and `webview2-user-data/` ([ADR 0005](0005-sqlite-metadata-with-file-artifacts-for-event-storage.md)). |
| Headless host | `FotoHavn.Host.Headless` | Kestrel on 127.0.0.1 with a random port, a WebSocket speaking the same envelope, a fake camera, and fake displays, for development and tests | Ship in a field build |

**Protocol.**

- Commands (UI → brain) are `{ "type": "command", "id", "name", "payload" }`. The brain answers each with `commandResult`, `accepted` or `rejected` with an optional reason.
- Presentations (brain → UI) are complete snapshots, `{ "type": "presentation", "role", "revision", "presentation" }`, sent after every change. Revisions count per role, and the UI drops any snapshot older than the one it has.
- Each connection has a role, `operator` or `guest`. The host assigns it per WebView2; the headless host assigns it by a per-role startup token. The page can never declare or change its role. The brain keeps one presentation per role and rejects any command the sender's role may not send. The guest presentation never contains operator data.
- The UI opens with `hello` and its protocol version. A mismatch shows a fixed error screen; the UI and brain always ship together.
- The UI never reports activity, because Phase Timers don't depend on input (D18).
- The C# records are the source of truth. `JsonSchemaExporter` exports JSON Schema, `json-schema-to-typescript` generates the UI's types, and a test fails when either output is stale.
- The brain runs commands on one single-threaded executor.

**WebView2.**

- Both windows share one `CoreWebView2Environment` with its user-data folder under `Data/`. Development uses the Evergreen runtime; the field build carries a pinned Fixed Version runtime (spike S4).
- Virtual hosts map content, and the UI receives URLs, never file paths:
  - `https://app.fotohavn/` serves the UI bundle.
  - `https://media.fotohavn/` serves saved artifacts and derived thumbnails read-only, only from inside `Data/Events` and `Data/Cache/thumbnails`, and refuses path traversal. (Proposed default Q10, plan section 18; confirmed by the owner at the M0 gate.)
  - `https://preview.fotohavn/` serves composed previews from an in-memory cache.
- Guardrails in both windows: no default context menu, no DevTools in field builds, no browser accelerator keys, pinch zoom, swipe navigation, status bar, or autofill. Navigation is allowed only to `app.fotohavn`, and new windows are cancelled.
- On `ProcessFailed`, the host recreates that window's WebView2 and resends its role's current presentation.
- Live preview frames use a separate channel ([ADR 0004](0004-brain-owned-webcam-through-one-exclusive-mediacapture-stream.md)).

## Considered options

- **Electron and Tauri** were rejected (D1). The camera, display, storage, and composition work is Windows .NET code; either shell would add a second application runtime between that code and the UI.
- **WinUI 3** was rejected (D2). The UI moves to React, so the host only needs windows and WebView2, and keeping WinUI 3 would carry over the stack of the app being replaced. **WinForms** was rejected (D2) in favor of WPF as the host for WebView2.
- **Rules in React** were rejected (D3). The UI would need its own timers and state, and the rules could not be tested headlessly or shared by both screens.

## Consequences

- Every rule, including timers and role checks, is unit-tested in the brain with fakes, and the UI's tests cover rendering and command mapping only ([ADR 0007](0007-unit-first-testing-with-real-camera-end-to-end.md)).
- Commands that would change a running Event are rejected by the brain with reason `eventRunning`, not only hidden by the UI.
- The headless host lets agents and CI drive the real brain in Chromium without a camera or WPF.
- A UI process crash costs a brief blank frame, not the Guest Cycle.
- A protocol change requires regenerating the TypeScript types in the same change.
- The field build must carry and verify the Fixed Version WebView2 runtime.
- All of `app/` except `app/docs/research/` and the implementation plan is deleted and recreated in M1.
