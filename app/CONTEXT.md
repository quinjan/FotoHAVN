# Desktop booth context

This context extends the shared language in [`../CONTEXT.md`](../CONTEXT.md) for the Windows booth application. Event, Capture, Photo Strip, Print Sheet, and Customer keep their shared meanings here and are not redefined.

Visual and interaction rules for the booth are in [`DESIGN.md`](DESIGN.md). Durable implementation choices are in the ADRs listed under [Decisions](#decisions).

## Operator language

**Event Setup**:
The operator's configuration of an Event: metadata (name, date, venue, Customer or host, and notes), Camera Binding, Guest Display Binding, printer, offered Templates and Looks with a default of each, countdown, mirror, Phase Timers, guest gallery, and Operator PIN. In v1 the printer is always "No printer".
_Avoid_: Settings, config

**Operator Screen**:
The FOTOHAVN window on the Windows primary display where the operator manages Events, Event Galleries, and the Active Event. FOTOHAVN always opens here, on the Event list.
_Avoid_: Main window, admin

**Active Event**:
The single Event admitting Guest Cycles in the running FOTOHAVN process, whatever the number of displays. It never survives closing or restarting FOTOHAVN, and its Event Setup can't be edited while it runs.
_Avoid_: Session, current session, resumed Event

**Operator PIN**:
The Event's 4–6 digit code required to leave the Active Event from the Guest Screen. It keeps guests out of operator screens; it isn't account security.
_Avoid_: Password, login

**Operator Assistance**:
The paused state of an Active Event after a Camera or storage failure during a Guest Cycle, a storage failure while creating one at `STEP INSIDE`, or a guest display failure at any time. Saved Captures stay, any countdown is cancelled, and Phase Timers pause until Retry succeeds; then they resume and any running Guest Cycle returns to the step it was on. With no Guest Cycle running, the Guest Screen returns to Start.
_Avoid_: Error screen, recovery mode

## Display language

**Available Display**:
A display Windows currently reports to FOTOHAVN.
_Avoid_: Monitor list

**Guest Screen**:
The full-screen window where guests use the Active Event. It opens on the Event's bound display at Start Event and closes when the Event ends.
_Avoid_: Kiosk, second window

**Guest Display Binding**:
An Event's link to the exact Available Display chosen for its Guest Screen, matched by the display's stable identity. FOTOHAVN never silently moves the Guest Screen to another display.
_Avoid_: Default display

## Camera language

**Available Camera**:
A Camera that Windows currently reports to FOTOHAVN as a video-capture device. Availability allows selection but doesn't prove readiness for an Event.
_Avoid_: Detected device, discovered Camera

**Eligible Camera**:
The selected Available Camera after FOTOHAVN has opened it and received a fresh frame in a usable format.
_Avoid_: Supported Camera, compatible Camera, qualified Camera

**Camera Binding**:
An Event's link to the exact Camera the operator chose, matched by device ID. FOTOHAVN never silently substitutes another Camera when the bound Camera is unavailable.
_Avoid_: Camera name, default Camera

## Guest language

**Start**:
The Guest Screen's resting view between Guest Cycles, with one action into a new Guest Cycle. When the bound Camera isn't eligible or storage isn't writable, Start is unavailable: `STEP INSIDE` is disabled until a fresh frame proves the Camera again and storage is writable again, and Start recovers by itself without Operator Assistance.
_Avoid_: Home, welcome screen, Start Event

**Guest Cycle**:
One guest group's use of the booth, beginning when a guest taps `STEP INSIDE` at Start and running through Template, Photographs, and Filters to the Keepsake Preview, ending when the booth returns to Start. It is recorded as completed when its Photo Strip is committed, before the Keepsake Preview shows it. Opening the guest gallery from Start isn't a Guest Cycle. It belongs to the Active Event, keeps its Captures as they are taken, and runs under Phase Timers.
_Avoid_: Session, transaction, Online Booth Cycle

**Template**:
A built-in design that defines a Photo Strip's size, photograph windows, paper, and artwork. An Event offers one or more Templates, and a Photo Strip is composed from exactly one.
_Avoid_: Photo Strip, layout, frame

**Look**:
One of the seven photographic finishes applied to the photographs in a Photo Strip. Guests see it as a "filter".
_Avoid_: Effect, preset

**Arrangement**:
The order of kept Captures across a Template's photograph windows.
_Avoid_: Layout

**Retake**:
Replacing one arranged Capture with a new one. The original stays in its window until the guest accepts the replacement.
_Avoid_: Redo

**Phase Timer**:
The configured maximum time for a Guest Cycle phase, counted from when the phase begins and not reset by guest input. When it runs out, the Guest Cycle continues automatically.
_Avoid_: Idle timeout, inactivity timer

**Start Over**:
A guest's deliberate end of their Guest Cycle before it completes. Saved Captures stay in the Event; no Photo Strip is made.
_Avoid_: Cancel, reset

**Keepsake Preview**:
The final Guest Cycle step, labeled Keepsake, that shows the saved Photo Strip for a set time before returning to Start. It only displays the Photo Strip: the Guest Cycle is already completed, so ending the Event, closing FOTOHAVN, or a Phase Timer during the Keepsake Preview leaves it completed.
_Avoid_: Download, result screen

## Gallery language

**Event Gallery**:
An Event's Guest Cycles, Photo Strips, and Captures, seen in one of two views. The operator view has every action. The guest view is enabled per Event, is read-only, and shows only the Active Event's completed Photo Strips with their arranged Captures. Event Setup and the Guest Screen call the guest view the guest gallery.
_Avoid_: Album, history, photo board

## Rules

### Events and screens

- Only one Event runs at a time, whatever the number of displays. Other displays are only choices for where the Guest Screen goes.
- Start Event checks, in order, that no other Event is running, that the Event Setup is valid, that the bound guest display is connected, that the bound Camera opens and delivers a fresh frame, and that storage is writable. A failed check keeps the operator in Event Setup with the reason. FOTOHAVN never substitutes another display or Camera.
- While an Event runs, the operator can create, edit, and delete other Events and browse their Event Galleries, but can't edit, delete, or restart the running Event or start any Event. The running Event shows as a read-only status panel with `OPEN GALLERY`, which opens its live operator view of the Event Gallery with no hold or PIN, and `END EVENT`. The brain rejects commands that would change the running Event; the UI doesn't only hide them.
- `END EVENT` on the Operator Screen asks for confirmation but no PIN, because the Operator Screen is trusted. The exit hold and Operator PIN on the Guest Screen are the backup, and the only way out when the Guest Screen covers the Operator Screen's display or there is only one display.
- The exit hold and Operator PIN are available on every Guest Screen view, including during a Guest Cycle. The exit hold is a 3-second press-and-hold, or Enter or Space held for 3 seconds on the focused control. Five wrong PIN attempts lock the PIN dialog for 30 seconds.
- Ending the Active Event either way ends any unfinished Guest Cycle as ended by the operator; its Captures stay. A Guest Cycle on the Keepsake Preview is already completed and stays completed.
- Closing or restarting FOTOHAVN ends the Active Event. On the next launch, unfinished Guest Cycles are recorded as ended because the app closed.

### Guest Cycles

- A Guest Cycle ends in one of five ways: completed, started over, ended by the operator, app closed, or unrecoverable. Only a completed Guest Cycle has a Photo Strip, and it is recorded as completed when that Photo Strip is committed; the Keepsake Preview only displays it. An unfinished Guest Cycle is recorded as unrecoverable when one of its saved files is found missing or changed at the next launch.
- A Guest Cycle completed by Phase Timers is the same as one completed by a guest: the same Keepsake Preview, record, and Event Gallery entry. Nothing records or shows that a timer advanced it.
- If an Event offers one Template, the Template step is skipped. If it offers one Look, the Filters step is skipped and the Photo Strip uses that Look.
- Guests may go back to an earlier step. Changing the Template keeps every kept Capture: they fill the new windows in order, extras stay in a retained pool, and a larger Template can't continue until its new empty windows are filled.
- A Capture belongs to the Guest Cycle only once it is committed to storage. The Event's mirror setting applies to both the live preview and the saved Captures.
- A Retake is transactional. `TRY AGAIN` discards only the candidate, and `KEEP ORIGINAL` cancels the Retake. Every arrangement method uses the same swap, and drag is never required.
- Rejected Retake candidates and replaced originals are discarded Captures. Their files are deleted when the Guest Cycle ends.
- `START OVER` is offered on the Template, Photographs, and Filters steps, with a confirmation, and never on the Keepsake Preview, where the Guest Cycle is already completed. If a Phase Timer runs out while the confirmation is open, the confirmation closes as `KEEP GOING` and the Guest Cycle continues automatically.
- Composition failures don't enter Operator Assistance; the guest sees `TRY AGAIN` and nothing is lost.
- A Camera or storage failure during a Guest Cycle, a storage failure while creating the Guest Cycle record at `STEP INSIDE`, and a guest display failure at any time during the Active Event enter Operator Assistance. `RETRY` is offered on the Guest Screen to anyone and on the Operator Screen.
- During a Guest Cycle, `RETRY` re-proves the Camera with a fresh frame, checks storage, and checks the bound display, reopening the Guest Screen on it if needed; then the Guest Cycle returns to the step it was on, and remaining Captures continue from the next empty window. When no Guest Cycle is running, as after a guest display loss on Start or in the guest gallery or a storage failure at `STEP INSIDE`, `RETRY` checks only the bound display and storage; then the Guest Screen returns to Start, which shows Start unavailable if the Camera still isn't eligible.
- Outside a Guest Cycle, on Start or in the guest gallery, a Camera that isn't eligible doesn't enter Operator Assistance. Start shows Start unavailable, with `STEP INSIDE` disabled, until a fresh frame proves the Camera again, and recovers by itself. The guest gallery keeps working because it needs no Camera, and returning from it to Start shows Start unavailable. The Operator Screen's running-Event panel shows the Camera state in text either way.
- Outside a Guest Cycle, a storage failure doesn't enter Operator Assistance either. Start shows Start unavailable, with `STEP INSIDE` disabled, until storage is writable again, and recovers by itself. The guest gallery shows its own inline error if it can't read the Event's Photo Strips.

### Phase Timers

| Phase | Starts when | Default | Range | When it runs out |
| --- | --- | ---: | --- | --- |
| Template | The Template step appears | 30 s | 0 (off) or 20–300 s | Continue with the selected Template |
| Photographs: capture | The Camera tab appears with empty windows and no sequence running, or a sequence is stopped | 30 s | 0 or 20–300 s | Keep the original of any pending Retake, then start the capture sequence for every empty window |
| Photographs: review | All windows are filled | 45 s | 0 or 20–300 s | Discard any pending Retake candidate, cancel a pending move, and continue to Filters with the current Arrangement |
| Filters | The Filters step appears | 30 s | 0 or 20–300 s | Finish with the selected Look |
| Keepsake | The Photo Strip is saved and shown | 12 s | 5–60 s, never off | Return to Start |
| Guest gallery | The guest view of the Event Gallery opens, outside a Guest Cycle | 60 s | 20–300 s, never off | Return to Start |

- Start has no Phase Timer.
- Only `MORE TIME`, going back, entering the phase again, or stopping a capture sequence (which restarts the capture timer) restarts a Phase Timer. `MORE TIME` appears within the last 20 seconds of the Template, Photographs capture, Photographs review, Filters, and guest gallery phases, and restarts the phase's full time. The Keepsake phase has no warning and no `MORE TIME`, because the Photo Strip is already saved and returning to Start loses nothing.
- A Phase Timer pauses while a countdown or capture runs and while the Active Event is in Operator Assistance, and resumes afterwards.
- A Phase Timer never deletes a kept Capture. With every Phase Timer on, a Guest Cycle that nobody touches after `STEP INSIDE` reaches a saved Photo Strip with the default Template, automatic Captures, the default Arrangement, and the default Look.
- When the operator turns a Phase Timer off, an abandoned Guest Cycle waits at that phase until someone continues it or uses `START OVER`.

### Event Galleries

- The operator view lists every Guest Cycle with how it ended. Guest Cycles without a Photo Strip show their kept Captures. The operator can open a Guest Cycle's folder and delete a Guest Cycle after confirmation.
- During an Active Event, the operator opens the running Event's operator view live from the running-Event panel with `OPEN GALLERY`; the exit hold and Operator PIN aren't needed.
- Deleting a Guest Cycle removes it from both views immediately.
- The guest view is off by default and should be turned on only with the Customer's permission. Guests reach it from Start when it's enabled and the Active Event has at least one completed Photo Strip. A new Guest Cycle always begins from Start, never from the guest view.
- Guests can't delete, open folders, export, or see metadata, end reasons, counts, discarded Captures, or Guest Cycles that ended without a Photo Strip.

## Boundaries

- A Guest Cycle is persistent, Event-bound, and timed. An Online Booth Cycle (see [`../website/CONTEXT.md`](../website/CONTEXT.md)) follows the same steps in the browser, isn't saved, and has no Phase Timers.
- A Template is the design a Photo Strip is composed from, not the Photo Strip itself. A FOTOHAVN Brand Strip is a website brand object and is never composed by the booth.
- Template and Look are desktop terms for now. The website's online booth uses the same Template and Look IDs; promote the terms to [`../CONTEXT.md`](../CONTEXT.md) if they become shared language.
- The Keepsake Preview shows a Photo Strip; it doesn't print. FOTOHAVN v1 makes no Print Sheets.
- The guest view of the Event Gallery is not the website's guest album or a separate photo board.
- User-facing text says FOTOHAVN. "Kiosk" appears only in code and internal documents.

## Decisions

- [ADR 0003: React UI in WPF WebView2 over a .NET brain](docs/adr/0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md)
- [ADR 0004: Brain-owned webcam through one exclusive MediaCapture stream](docs/adr/0004-brain-owned-webcam-through-one-exclusive-mediacapture-stream.md)
- [ADR 0005: SQLite metadata with file artifacts for Event storage](docs/adr/0005-sqlite-metadata-with-file-artifacts-for-event-storage.md)
- [ADR 0006: Brain-side composition with an app-owned catalog](docs/adr/0006-brain-side-composition-with-an-app-owned-catalog.md)
- [ADR 0007: Unit-first testing with real-camera end-to-end](docs/adr/0007-unit-first-testing-with-real-camera-end-to-end.md)
- [ADR 0008: Operator and Guest Screens on bound displays](docs/adr/0008-operator-and-guest-screens-on-bound-displays.md)
