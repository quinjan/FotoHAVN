import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, readdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { DatabaseSync, type SQLInputValue } from "node:sqlite";
import { crc32, deflateSync } from "node:zlib";
import { databaseFileName } from "./data";
import { paths } from "./paths";

/**
 * Seeds a Data folder before the app starts, so a test can launch it on data the Event store already holds. This is
 * the one place tests write `fotohavn.db` (everything else reads it through data.ts), and it mirrors the Storage tests'
 * `SeededDataFolder`: the migration scripts run from disk, in order, each followed by `PRAGMA user_version = <version>`
 * (what `MigrationRunner` does), then rows and files laid out the way the Event store writes them.
 */

const migrationsDir = path.join(paths.appDir, "src", "FotoHavn.Storage", "Migrations");
const migrationName = /^(\d{4})_[a-z0-9_]+\.sql$/;

export interface Migration {
  version: number;
  file: string;
}

/** The migration scripts in version order, as the embedded catalog lists them. */
export function migrations(): Migration[] {
  const found = readdirSync(migrationsDir)
    .map((name) => ({ name, match: migrationName.exec(name) }))
    .filter((entry) => entry.match !== null)
    .map((entry) => ({ version: Number(entry.match?.[1]), file: path.join(migrationsDir, entry.name) }))
    .sort((a, b) => a.version - b.version);
  found.forEach((migration, index) => {
    if (migration.version !== index + 1) {
      throw new Error(`Migration versions must run 1, 2, 3 without gaps; found ${migration.version} at position ${index + 1}.`);
    }
  });
  return found;
}

/** The newest migration number, which is the `PRAGMA user_version` a freshly migrated database holds. */
export function latestMigrationVersion(): number {
  return migrations().at(-1)?.version ?? 0;
}

// --- Small valid images, so file sizes and hashes are real. ---

// A 1x1 baseline JPEG, encoded by a real encoder.
const baseJpeg = Buffer.from(
  "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAABAAEDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD74ooor5w9U//Z",
  "base64",
);

/** A valid 1x1 JPEG that carries `label` in a comment segment, so each Capture has its own bytes. */
export function tinyJpeg(label: string): Buffer {
  const text = Buffer.from(label, "utf8");
  const comment = Buffer.concat([Buffer.from([0xff, 0xfe, (text.length + 2) >> 8, (text.length + 2) & 0xff]), text]);
  return Buffer.concat([baseJpeg.subarray(0, 2), comment, baseJpeg.subarray(2)]);
}

function pngChunk(type: string, data: Buffer): Buffer {
  const body = Buffer.concat([Buffer.from(type, "ascii"), data]);
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);
  const checksum = Buffer.alloc(4);
  checksum.writeUInt32BE(crc32(body));
  return Buffer.concat([length, body, checksum]);
}

/** A valid 1x1 PNG that carries `label` in a text chunk, so each Photo Strip has its own bytes. */
export function tinyPng(label: string): Buffer {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(1, 0);
  header.writeUInt32BE(1, 4);
  header.set([8, 2, 0, 0, 0], 8); // 8-bit RGB, no interlace
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    pngChunk("IHDR", header),
    pngChunk("tEXt", Buffer.from(`Comment\0${label}`, "latin1")),
    pngChunk("IDAT", deflateSync(Buffer.from([0, 0xe8, 0xdc, 0xc6]))),
    pngChunk("IEND", Buffer.alloc(0)),
  ]);
}

export function sha256Hex(bytes: Uint8Array): string {
  return createHash("sha256").update(bytes).digest("hex");
}

// --- Formats the Event store writes (StorageTimestamp, StorageIds, ThumbnailName). ---

/** `yyyy-MM-ddTHH:mm:ss.fffffffZ`, UTC and fixed width. */
export function storageTimestamp(value: Date): string {
  return value.toISOString().replace("Z", "0000Z");
}

/** A deterministic lowercase GUID, so a test can name what a seed wrote without reading it back. */
export function seedId(kind: number, index: number): string {
  const hex = (value: number, width: number): string => value.toString(16).padStart(width, "0");
  return `5eed${hex(kind, 4)}-0000-4000-8000-${hex(index, 12)}`;
}

export function thumbnailName(sourceId: string, sha256: string, width: number): string {
  return `${sourceId}-${sha256.slice(0, 8).toLowerCase()}-${String(width)}.jpg`;
}

// --- The plan: rows and files, written together. ---

export interface PlannedEvent {
  id: string;
  name: string;
  date: string;
  venue: string | null;
  createdAt: string;
  deleting: boolean;
  folder: string;
}

export interface PlannedCycle {
  id: string;
  eventId: string;
  startedAt: string;
  endReason: "completed" | "started_over" | "operator_exit" | "app_closed" | "unrecoverable" | null;
  deleting: boolean;
  folder: string;
}

export interface PlannedCapture {
  id: string;
  cycleId: string;
  sequence: number;
  state: "kept" | "discarded";
  fileName: string;
  path: string;
  bytes: Buffer;
  sha256: string;
  /** False seeds the row without its file (a half-deleted folder). */
  hasFile: boolean;
}

export interface PlannedStrip {
  cycleId: string;
  fileName: string;
  path: string;
  bytes: Buffer;
  sha256: string;
  hasFile: boolean;
}

export interface PlannedFile {
  path: string;
  bytes: Buffer;
}

/** Collects what a Data folder holds, then {@link DataFolderPlan.write} creates it. */
export class DataFolderPlan {
  readonly events: PlannedEvent[] = [];
  readonly cycles: PlannedCycle[] = [];
  readonly captures: PlannedCapture[] = [];
  readonly strips: PlannedStrip[] = [];
  readonly files: PlannedFile[] = [];
  private cycleCounter = 0;

  constructor(readonly dataDir: string) {}

  get eventsDir(): string {
    return path.join(this.dataDir, "Events");
  }

  get thumbnailsDir(): string {
    return path.join(this.dataDir, "Cache", "thumbnails");
  }

  addEvent(event: { id: string; name: string; date: string; venue?: string; createdAt: string; deleting?: boolean }): PlannedEvent {
    const planned: PlannedEvent = {
      id: event.id,
      name: event.name,
      date: event.date,
      venue: event.venue ?? null,
      createdAt: event.createdAt,
      deleting: event.deleting ?? false,
      folder: path.join(this.eventsDir, event.id),
    };
    this.events.push(planned);
    return planned;
  }

  /** Adds a Guest Cycle; a null `endReason` leaves it unfinished. */
  addCycle(
    event: PlannedEvent,
    id: string,
    endReason: PlannedCycle["endReason"],
    options: { deleting?: boolean } = {},
  ): PlannedCycle {
    this.cycleCounter += 1;
    const startedAt = new Date(Date.UTC(2026, 9, 24, 10, this.cycleCounter)).toISOString();
    const cycle: PlannedCycle = {
      id,
      eventId: event.id,
      startedAt,
      endReason,
      deleting: options.deleting ?? false,
      folder: path.join(event.folder, "GuestCycles", id),
    };
    this.cycles.push(cycle);
    return cycle;
  }

  addCapture(
    cycle: PlannedCycle,
    id: string,
    sequence: number,
    options: { state?: "kept" | "discarded"; hasFile?: boolean } = {},
  ): PlannedCapture {
    const bytes = tinyJpeg(`capture ${id}`);
    const fileName = `capture-${String(sequence)}.jpg`;
    const capture: PlannedCapture = {
      id,
      cycleId: cycle.id,
      sequence,
      state: options.state ?? "kept",
      fileName,
      path: path.join(cycle.folder, fileName),
      bytes,
      sha256: sha256Hex(bytes),
      hasFile: options.hasFile ?? true,
    };
    this.captures.push(capture);
    return capture;
  }

  addPhotoStrip(cycle: PlannedCycle, options: { hasFile?: boolean } = {}): PlannedStrip {
    const bytes = tinyPng(`photo strip ${cycle.id}`);
    const strip: PlannedStrip = {
      cycleId: cycle.id,
      fileName: "photo-strip.png",
      path: path.join(cycle.folder, "photo-strip.png"),
      bytes,
      sha256: sha256Hex(bytes),
      hasFile: options.hasFile ?? true,
    };
    this.strips.push(strip);
    return strip;
  }

  /** Adds a file no row refers to (a staging leftover, an orphan, a stale thumbnail) and returns its path. */
  addFile(file: string, content = "stray"): string {
    this.files.push({ path: file, bytes: Buffer.from(content, "utf8") });
    return file;
  }

  /** Adds a thumbnail named for its source and the source file's hash. */
  addThumbnail(sourceId: string, sha256: string, width: number): string {
    return this.addFile(path.join(this.thumbnailsDir, thumbnailName(sourceId, sha256, width)), "thumbnail");
  }

  /** Creates `fotohavn.db` (migrated) with every row, and every file. */
  write(): void {
    mkdirSync(this.dataDir, { recursive: true });
    const db = new DatabaseSync(path.join(this.dataDir, databaseFileName));
    try {
      db.exec("PRAGMA foreign_keys = ON;");
      for (const migration of migrations()) {
        db.exec("BEGIN;");
        db.exec(readFileSync(migration.file, "utf8"));
        db.exec(`PRAGMA user_version = ${String(migration.version)};`);
        db.exec("COMMIT;");
      }
      this.writeRows(db);
    } finally {
      db.close();
    }
    for (const capture of this.captures.filter((c) => c.hasFile)) {
      writeBytes(capture.path, capture.bytes);
    }
    for (const strip of this.strips.filter((s) => s.hasFile)) {
      writeBytes(strip.path, strip.bytes);
    }
    for (const file of this.files) {
      writeBytes(file.path, file.bytes);
    }
  }

  private writeRows(db: DatabaseSync): void {
    const run = (sql: string, ...params: SQLInputValue[]): void => {
      db.prepare(sql).run(...params);
    };
    for (const event of this.events) {
      run(
        "INSERT INTO events (id, name, event_date, venue, default_template_id, default_look_id, created_at, updated_at, deleting_at) VALUES (?, ?, ?, ?, 'signature-strip', 'naturale-v1', ?, ?, ?)",
        event.id,
        event.name,
        event.date,
        event.venue,
        event.createdAt,
        event.createdAt,
        event.deleting ? storageTimestamp(new Date(Date.UTC(2026, 9, 2, 9))) : null,
      );
      run("INSERT INTO event_templates (event_id, template_id) VALUES (?, 'signature-strip')", event.id);
      run("INSERT INTO event_looks (event_id, look_id) VALUES (?, 'naturale-v1')", event.id);
      run("INSERT INTO event_phase_timers (event_id, phase, seconds) VALUES (?, 'template', 30)", event.id);
    }
    for (const cycle of this.cycles) {
      const started = new Date(cycle.startedAt);
      run(
        "INSERT INTO guest_cycles (id, event_id, started_at, ended_at, end_reason, deleting_at) VALUES (?, ?, ?, ?, ?, ?)",
        cycle.id,
        cycle.eventId,
        storageTimestamp(started),
        cycle.endReason === null ? null : storageTimestamp(new Date(started.getTime() + 60_000)),
        cycle.endReason,
        cycle.deleting ? storageTimestamp(new Date(started.getTime() + 120_000)) : null,
      );
    }
    for (const capture of this.captures) {
      run(
        "INSERT INTO captures (id, guest_cycle_id, sequence, file_name, byte_length, sha256, width, height, mirrored, taken_at, state) VALUES (?, ?, ?, ?, ?, ?, 1920, 1080, 1, ?, ?)",
        capture.id,
        capture.cycleId,
        capture.sequence,
        capture.fileName,
        capture.bytes.length,
        capture.sha256,
        storageTimestamp(new Date(Date.UTC(2026, 9, 24, 10, 5))),
        capture.state,
      );
    }
    for (const strip of this.strips) {
      const arrangement = this.captures.filter((c) => c.cycleId === strip.cycleId && c.state === "kept").map((c) => c.id);
      run(
        "INSERT INTO photo_strips (guest_cycle_id, template_id, look_id, arrangement, file_name, byte_length, sha256, width, height, committed_at) VALUES (?, 'signature-strip', 'naturale-v1', ?, ?, ?, ?, 600, 1800, ?)",
        strip.cycleId,
        JSON.stringify(arrangement),
        strip.fileName,
        strip.bytes.length,
        strip.sha256,
        storageTimestamp(new Date(Date.UTC(2026, 9, 24, 10, 6))),
      );
    }
  }
}

function writeBytes(file: string, bytes: Uint8Array): void {
  mkdirSync(path.dirname(file), { recursive: true });
  writeFileSync(file, bytes);
}

// --- The reconcile scenario (mirrors `SeededDataFolder.SeedReconcileScenario()` in FotoHavn.Storage.Tests). ---

/** What startup should repair, in the order of `ReconcileDiagnostics`. */
export interface ExpectedReconcile {
  stagingFilesDeleted: number;
  orphanFilesDeleted: number;
  artifactProblems: number;
  cyclesMarkedUnrecoverable: number;
  cyclesClosedAsAppClosed: number;
  guestCyclesDeleted: number;
  eventsDeleted: number;
  discardedFilesDeleted: number;
  orphanThumbnailsDeleted: number;
  problems: number;
}

export interface ReconcileScenario {
  plan: DataFolderPlan;
  /** The Events the Event list shows, in the order it shows them (event date, newest first). */
  liveEvents: { event: PlannedEvent; completedGuestCycles: number }[];
  /** A live Event with a completed Guest Cycle whose Captures and Photo Strip are healthy. */
  healthyEvent: PlannedEvent;
  healthyCycle: PlannedCycle;
  healthyCaptures: PlannedCapture[];
  healthyStrip: PlannedStrip;
  discardedCapture: PlannedCapture;
  /** A Guest Cycle with a kept Capture that was still unfinished when the app stopped. */
  openCycle: PlannedCycle;
  openCapture: PlannedCapture;
  stagingFile: string;
  orphanFile: string;
  /** A file in an Event folder no row names. */
  orphanFolderFile: string;
  orphanEventFolder: string;
  deletingCycle: PlannedCycle;
  deletingEvent: PlannedEvent;
  deletingEventCycle: PlannedCycle;
  deletingEventCapturePresent: PlannedCapture;
  deletingEventCaptureMissing: PlannedCapture;
  liveThumbnails: string[];
  staleThumbnails: string[];
  /** Thumbnails of what step 5 deletes; they go with it and are not counted by step 7. */
  doomedThumbnails: string[];
  expected: ExpectedReconcile;
}

const eventKind = 1;
const cycleKind = 2;
const captureKind = 3;
const folderKind = 4;

/**
 * One Data folder with every kind of damage the startup reconciler repairs (staging files, orphans, an unfinished Guest
 * Cycle, a half-deleted Event and Guest Cycle, a discarded Capture, stale thumbnails) next to three live Events, two of
 * them with a completed Guest Cycle. Deterministic, so a test can name every row and file without reading the folder.
 */
export function reconcileScenario(dataDir: string): ReconcileScenario {
  const plan = new DataFolderPlan(dataDir);
  const created = (day: number): string => storageTimestamp(new Date(Date.UTC(2026, 9, day, 9)));

  // The newest by date comes first: no Guest Cycles at all.
  const quietEvent = plan.addEvent({ id: seedId(eventKind, 1), name: "Fairmont Makati launch", date: "2026-12-05", venue: "Fairmont Makati, Salon 2", createdAt: created(2) });

  // The healthy Event: a completed Guest Cycle with a Photo Strip, plus the damage that lives beside it.
  const healthyEvent = plan.addEvent({ id: seedId(eventKind, 2), name: "Santos and Reyes wedding", date: "2026-11-21", venue: "The Glasshouse, Tagaytay", createdAt: created(1) });
  const healthyCycle = plan.addCycle(healthyEvent, seedId(cycleKind, 1), "completed");
  const first = plan.addCapture(healthyCycle, seedId(captureKind, 1), 1);
  const second = plan.addCapture(healthyCycle, seedId(captureKind, 2), 2);
  const discardedCapture = plan.addCapture(healthyCycle, seedId(captureKind, 3), 3, { state: "discarded" });
  const healthyStrip = plan.addPhotoStrip(healthyCycle);
  const orphanFile = plan.addFile(path.join(healthyCycle.folder, "capture-9.jpg"));

  const openCycle = plan.addCycle(healthyEvent, seedId(cycleKind, 2), null);
  const openCapture = plan.addCapture(openCycle, seedId(captureKind, 4), 1);
  const stagingFile = plan.addFile(path.join(openCycle.folder, ".staging", "leftover.tmp"));

  const deletingCycle = plan.addCycle(healthyEvent, seedId(cycleKind, 3), "completed", { deleting: true });
  const deletingCapture = plan.addCapture(deletingCycle, seedId(captureKind, 5), 1);
  const deletingStrip = plan.addPhotoStrip(deletingCycle);

  // A second healthy Event, older.
  const gardenEvent = plan.addEvent({ id: seedId(eventKind, 3), name: "Casa Rosa garden party", date: "2026-10-17", venue: "Casa Rosa Garden, Alfonso", createdAt: created(3) });
  const gardenCycle = plan.addCycle(gardenEvent, seedId(cycleKind, 4), "completed");
  const gardenCapture = plan.addCapture(gardenCycle, seedId(captureKind, 6), 1);
  const gardenStrip = plan.addPhotoStrip(gardenCycle);

  // A half-deleted Event: marked deleting, its folder only partly gone (one Capture file left, the rest missing).
  const deletingEvent = plan.addEvent({ id: seedId(eventKind, 4), name: "Half-deleted Event", date: "2026-11-28", createdAt: created(4), deleting: true });
  const deletingEventCycle = plan.addCycle(deletingEvent, seedId(cycleKind, 5), "completed");
  const deletingEventCapturePresent = plan.addCapture(deletingEventCycle, seedId(captureKind, 7), 1);
  const deletingEventCaptureMissing = plan.addCapture(deletingEventCycle, seedId(captureKind, 8), 2, { hasFile: false });
  plan.addPhotoStrip(deletingEventCycle, { hasFile: false });

  // An Event folder no row names.
  const orphanEventFolder = path.join(plan.eventsDir, seedId(folderKind, 1));
  const orphanFolderFile = plan.addFile(path.join(orphanEventFolder, "GuestCycles", seedId(folderKind, 2), "capture-1.jpg"));

  const liveThumbnails = [
    plan.addThumbnail(first.id, first.sha256, 256),
    plan.addThumbnail(healthyCycle.id, healthyStrip.sha256, 512),
    plan.addThumbnail(gardenCapture.id, gardenCapture.sha256, 256),
  ];
  const staleThumbnails = [
    plan.addThumbnail(seedId(captureKind, 99), sha256Hex(Buffer.from([1])), 256),
    plan.addThumbnail(second.id, sha256Hex(Buffer.from([9, 9])), 256),
    plan.addThumbnail(discardedCapture.id, discardedCapture.sha256, 256),
    plan.addFile(path.join(plan.thumbnailsDir, "notes.txt")),
  ];
  const doomedThumbnails = [
    plan.addThumbnail(deletingCapture.id, deletingCapture.sha256, 256),
    plan.addThumbnail(deletingCycle.id, deletingStrip.sha256, 256),
    plan.addThumbnail(deletingEventCapturePresent.id, deletingEventCapturePresent.sha256, 256),
  ];

  return {
    plan,
    liveEvents: [
      { event: quietEvent, completedGuestCycles: 0 },
      { event: healthyEvent, completedGuestCycles: 1 },
      { event: gardenEvent, completedGuestCycles: 1 },
    ],
    healthyEvent,
    healthyCycle,
    healthyCaptures: [first, second],
    healthyStrip,
    discardedCapture,
    openCycle,
    openCapture,
    stagingFile,
    orphanFile,
    orphanFolderFile,
    orphanEventFolder,
    deletingCycle,
    deletingEvent,
    deletingEventCycle,
    deletingEventCapturePresent,
    deletingEventCaptureMissing,
    liveThumbnails,
    staleThumbnails,
    doomedThumbnails,
    expected: {
      stagingFilesDeleted: 1,
      orphanFilesDeleted: 2,
      artifactProblems: 0,
      cyclesMarkedUnrecoverable: 0,
      cyclesClosedAsAppClosed: 1,
      guestCyclesDeleted: 2,
      eventsDeleted: 1,
      discardedFilesDeleted: 1,
      orphanThumbnailsDeleted: 4,
      problems: 0,
    },
  };
}

/** Seeds the {@link reconcileScenario} into `dataDir`; pass it as `appOptions.seed`. */
export function seedReconcileScenario(dataDir: string): void {
  reconcileScenario(dataDir).plan.write();
}

/** The Events `seedOperatorEvents` writes, newest date first (the Event list's order). */
export const operatorSeed = {
  /** Two completed Guest Cycles: two Photo Strips and five kept photographs (one more was discarded). */
  seaside: { id: seedId(0xe1, 1), name: "Seaside wedding", venue: "Pier Hall", guestCycles: 2, photoStrips: 2, photographs: 5 },
  harbour: { id: seedId(0xe1, 2), name: "Harbour gala", venue: "Harbour Hall" },
  studio: { id: seedId(0xe1, 3), name: "Studio open day" },
} as const;

/** Seeds three Events for the Event list scenarios (delete, open folder, visual record); pass it as `appOptions.seed`. */
export function seedOperatorEvents(dataDir: string): void {
  const plan = new DataFolderPlan(dataDir);
  const seaside = plan.addEvent({
    id: operatorSeed.seaside.id,
    name: operatorSeed.seaside.name,
    date: "2026-10-03",
    venue: operatorSeed.seaside.venue,
    createdAt: storageTimestamp(new Date(Date.UTC(2026, 8, 1))),
  });
  const first = plan.addCycle(seaside, seedId(0xe2, 1), "completed");
  plan.addCapture(first, seedId(0xe3, 1), 1);
  plan.addCapture(first, seedId(0xe3, 2), 2);
  plan.addCapture(first, seedId(0xe3, 3), 3);
  plan.addCapture(first, seedId(0xe3, 4), 4, { state: "discarded" });
  plan.addPhotoStrip(first);
  const second = plan.addCycle(seaside, seedId(0xe2, 2), "completed");
  plan.addCapture(second, seedId(0xe3, 5), 1);
  plan.addCapture(second, seedId(0xe3, 6), 2);
  plan.addPhotoStrip(second);
  plan.addEvent({
    id: operatorSeed.harbour.id,
    name: operatorSeed.harbour.name,
    date: "2026-09-12",
    venue: operatorSeed.harbour.venue,
    createdAt: storageTimestamp(new Date(Date.UTC(2026, 7, 20))),
  });
  plan.addEvent({
    id: operatorSeed.studio.id,
    name: operatorSeed.studio.name,
    date: "2026-08-22",
    createdAt: storageTimestamp(new Date(Date.UTC(2026, 7, 1))),
  });
  plan.write();
}
