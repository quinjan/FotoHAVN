---
status: accepted
date: 2026-10-13
---

# SQLite metadata with file artifacts for Event storage

The desktop booth stores Event metadata in one SQLite database and keeps Captures and Photo Strips as files in per-Guest-Cycle folders. The brain's Event store is the only writer of Event data: `fotohavn.db`, `Events/`, and `Cache/thumbnails/`. The host owns `logs/` and `webview2-user-data/`. A file is committed before its row, and the committed row is the point of truth.

Replaces: the corresponding part of the deleted WinUI 3 app, which kept `event.json` and `guest-cycle.json` manifest files in an `Events` folder beside the executable.

## Context

- An abandoned Guest Cycle must still reach a saved Photo Strip (D18), and a crash or power loss must never leave a Capture referenced but missing.
- The Event Gallery pages through hundreds of Guest Cycles, hides deleted ones from both views at once, and filters the guest view to completed Photo Strips (D26, D27).
- A portable field-test folder must carry its own Events.
- An Active Event never survives closing or restarting the app (D17).

## Decision

**Location.** The Data folder sits beside the executable:

```text
Data/
├─ fotohavn.db                 + -wal and -shm
├─ logs/
├─ webview2-user-data/
├─ Cache/thumbnails/           derived, regenerable
└─ Events/<eventId>/GuestCycles/<guestCycleId>/
   ├─ .staging/
   ├─ capture-<sequence>.jpg
   └─ photo-strip.png
```

It must be on a local disk. Startup refuses network and cloud-synced paths it can detect and says why.

**One writer.** The Event store (`FotoHavn.Storage`) is the only writer of Event data: `fotohavn.db`, `Events/`, and `Cache/thumbnails/`. The host owns `logs/` and `webview2-user-data/`. Other adapters, such as the camera and compositor, hand the Event store bytes.

**Commit order.** Write to `.staging/<uuid>.tmp`, flush to disk, rename to the final name, then insert the row. A file is never referenced before its row exists.

**SQLite.** `Microsoft.Data.Sqlite` with plain SQL, in WAL mode with full sync, foreign keys on, and a busy timeout. An integrity check runs at startup. Schema versions use `PRAGMA user_version` and ordered SQL migration files embedded in `FotoHavn.Storage`.

**Schema.** The first migration has `events`, `event_templates`, `event_looks`, `event_phase_timers`, `guest_cycles`, `captures`, and `photo_strips`, as specified in [section 10 of the implementation plan](../implementation-plan.md#10-storage). Notable rules:

- `events.printer` is always `none` in v1 (D6).
- `countdown_seconds` is 3, 5, or 10.
- `guest_cycles.end_reason` is `completed`, `started_over`, `operator_exit`, `app_closed`, or `unrecoverable`. A Guest Cycle is recorded as `completed` when its Photo Strip is committed; the Keepsake step only displays it, so ending the Event, closing the app, or a timer during Keepsake leaves it `completed`. A cycle finished by Phase Timers is recorded as `completed`, the same as one a guest finished (D28). Since guests lost `START OVER` (D39, protocol version 12), nothing records `started_over`; the value stays valid for Guest Cycles recorded before, so the schema and its CHECK constraint are unchanged.
- `captures.state` is `kept` or `discarded`. `photo_strips.arrangement` records the Capture IDs, one per photograph window.
- Each artifact row records its byte length and SHA-256.

**Operator PIN.** Stored only as a PBKDF2-SHA256 hash (`Rfc2898DeriveBytes.Pbkdf2`) with a 16-byte random salt and at least 210,000 iterations, compared in constant time.

**Discarded Captures (D23).** A rejected Retake candidate or a replaced original becomes `discarded`, and its file is deleted when its Guest Cycle ends.

**Deletion.** Deleting an Event or a Guest Cycle sets `deleting_at` in one transaction, so it disappears at once from the Event list or both Event Gallery views. A background task then removes its folder and thumbnails, then its rows.

**Thumbnails.** Event Gallery thumbnails are derived JPEGs in `Data/Cache/thumbnails/`, named `<sourceId>-<sha8>-<width>.jpg`. The Event store creates them on first request. They are never edited, have no rows, aren't Event artifacts, and can be deleted at any time. Originals are never edited in place ([`brand/photography.md`](../../../brand/photography.md)).

**Serving.** The UI reads artifacts and thumbnails only through the read-only `media.fotohavn` virtual host ([ADR 0003](0003-react-ui-in-wpf-webview2-over-a-dotnet-brain.md)), which serves files only from inside `Data/Events` and `Data/Cache/thumbnails` and refuses path traversal. (Proposed default Q10, plan section 18; confirmed by the owner at the M0 gate.) The UI never receives file paths.

**Startup reconciler.** Before the Event list appears:

1. Delete every `.staging/` file.
2. Delete files under `Data/Events/**` that have no row. Thumbnails in `Data/Cache/` are derived and handled in step 7; `fotohavn.db`, `logs/`, and `webview2-user-data/` are never touched. The reconciler and the store never follow a junction or symbolic link: listings skip them, a delete through one is refused, and the reconciler reports a link it finds and leaves it in place.
3. For rows whose file is missing or whose hash or size differs, log the problem, set `ended_at` and `end_reason = 'unrecoverable'` on an unfinished cycle, and keep its other files. "Log and keep" is the rule: the reconciler changes nothing else about the row (a Capture stays `kept`, a Photo Strip keeps its row), so a file that reappears is adopted as it is, and the gallery shows such an item as unavailable (M9).
4. End Guest Cycles that still have no `end_reason` with `ended_at` and `end_reason = 'app_closed'` (D17).
5. Finish deleting Guest Cycles and Events marked `deleting_at`.
6. Delete files of `discarded` Captures.
7. Delete thumbnails whose source no longer exists.

**Failures.** A storage write failure (disk full, access denied, file locked) during a Guest Cycle discards the uncommitted Capture and puts the Active Event into Operator Assistance. So does a storage failure while creating the Guest Cycle record at `STEP INSIDE`. Outside a Guest Cycle, a storage failure doesn't enter Operator Assistance: Start shows Start unavailable, with `STEP INSIDE` disabled, until storage is writable again, as it does for an ineligible camera ([ADR 0004](0004-brain-owned-webcam-through-one-exclusive-mediacapture-stream.md)), and the guest gallery shows its own inline error if it can't read.

## Considered options

- **JSON manifests per Event and Guest Cycle**, as in the deleted app, were rejected. Paging, filtering the guest view, and hiding a deleted Guest Cycle from both views at once need queries and transactions.
- **Images stored as SQLite blobs** were rejected. Operators copy photographs off the booth by opening the Event's folder in File Explorer, so artifacts stay ordinary files.
- **More than one writer** (for example, the camera adapter writing its own files) was rejected. One writer keeps the commit order and reconciler rules in one place.

## Consequences

- After a crash, the reconciler leaves only committed, verified artifacts referenced, and no Active Event survives.
- Crash tests kill a child process at every step of a commit, and fault tests inject disk-full, access-denied, and locked-file errors ([ADR 0007](0007-unit-first-testing-with-real-camera-end-to-end.md)).
- Antivirus locks on the WAL or image files are handled as storage failures (risk R2).
- A lost Operator PIN can't be recovered from the database; the operator sets a new one in Event setup.
- Running from a cloud-synced or network folder is refused where it can be detected.
