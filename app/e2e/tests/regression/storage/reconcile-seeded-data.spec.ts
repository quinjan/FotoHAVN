import path from "node:path";
import {
  databaseFileName,
  directoryExists,
  fileExists,
  fingerprint,
  listFileNames,
  queryAll,
  queryOne,
} from "../../../src/data";
import { expect, test } from "../../../src/fixtures";
import { EventRows } from "../../../src/screens/screens";
import { reconcileScenario, seedReconcileScenario } from "../../../src/seed";

// The app starts on a Data folder holding every kind of damage the startup reconciler repairs (seed.ts mirrors the
// Storage tests' SeededDataFolder.SeedReconcileScenario) and brings it to a consistent state before the Event list shows.
test.use({ appOptions: { seed: seedReconcileScenario } });

test(
  "the app repairs a seeded Data folder at startup and the Event list shows only live Events",
  { tag: ["@m4"] },
  async ({ app, operatorPage, hooks, dataDir }) => {
    const scenario = reconcileScenario(dataDir);
    const db = path.join(dataDir, databaseFileName);

    await test.step("storage opens and the reconciler reports what it repaired", async () => {
      await expect.poll(async () => (await hooks.getDiagnostics()).storage.state, { timeout: 30_000 }).toBe("ready");
      const { storage } = await hooks.getDiagnostics();
      expect(storage.kind).toBeNull();
      expect(storage.schemaVersion).toBe(1);
      expect(storage.reconcile).toEqual(scenario.expected);
    });

    await test.step("the staging file is gone", () => {
      expect(fileExists(scenario.stagingFile), "staging file").toBe(false);
    });

    await test.step("the orphan file and the orphan Event folder are gone", () => {
      expect(fileExists(scenario.orphanFile), "orphan file in a live Guest Cycle").toBe(false);
      expect(fileExists(scenario.orphanFolderFile), "file in an Event folder no row names").toBe(false);
      expect(directoryExists(scenario.orphanEventFolder), "the Event folder no row names").toBe(false);
    });

    await test.step("the unfinished Guest Cycle ended as app_closed and kept its Capture", () => {
      const cycle = queryOne<{ ended_at: string | null; end_reason: string | null; deleting_at: string | null }>(
        db,
        "SELECT ended_at, end_reason, deleting_at FROM guest_cycles WHERE id = ?",
        scenario.openCycle.id,
      );
      expect(cycle?.end_reason).toBe("app_closed");
      expect(cycle?.ended_at).toMatch(/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z$/);
      expect(cycle?.deleting_at).toBeNull();
      const capture = queryOne<{ state: string }>(db, "SELECT state FROM captures WHERE id = ?", scenario.openCapture.id);
      expect(capture?.state).toBe("kept");
      expect(fingerprint(scenario.openCapture.path)).toEqual({
        bytes: scenario.openCapture.bytes.length,
        sha256: scenario.openCapture.sha256,
      });
      // Nothing else was left unfinished.
      expect(queryAll(db, "SELECT id FROM guest_cycles WHERE end_reason IS NULL")).toEqual([]);
    });

    await test.step("the half-deleted Event's rows and folder are gone", () => {
      const { deletingEvent, deletingEventCycle } = scenario;
      expect(queryAll(db, "SELECT id FROM events WHERE id = ?", deletingEvent.id)).toEqual([]);
      for (const table of ["event_templates", "event_looks", "event_phase_timers"]) {
        expect(queryAll(db, `SELECT 1 FROM ${table} WHERE event_id = ?`, deletingEvent.id), table).toEqual([]);
      }
      expect(queryAll(db, "SELECT id FROM guest_cycles WHERE event_id = ?", deletingEvent.id)).toEqual([]);
      expect(queryAll(db, "SELECT id FROM captures WHERE guest_cycle_id = ?", deletingEventCycle.id)).toEqual([]);
      expect(queryAll(db, "SELECT 1 FROM photo_strips WHERE guest_cycle_id = ?", deletingEventCycle.id)).toEqual([]);
      expect(directoryExists(deletingEvent.folder), "the half-deleted Event's folder").toBe(false);
      expect(fileExists(scenario.deletingEventCapturePresent.path), "the Capture file it still held").toBe(false);
    });

    await test.step("the Guest Cycle marked deleting is gone from a live Event", () => {
      const { deletingCycle } = scenario;
      expect(queryAll(db, "SELECT id FROM guest_cycles WHERE id = ?", deletingCycle.id)).toEqual([]);
      expect(queryAll(db, "SELECT id FROM captures WHERE guest_cycle_id = ?", deletingCycle.id)).toEqual([]);
      expect(queryAll(db, "SELECT 1 FROM photo_strips WHERE guest_cycle_id = ?", deletingCycle.id)).toEqual([]);
      expect(directoryExists(deletingCycle.folder), "its folder").toBe(false);
    });

    await test.step("the discarded Capture's file is gone and its row stays discarded", () => {
      expect(fileExists(scenario.discardedCapture.path), "discarded Capture file").toBe(false);
      const row = queryOne<{ state: string }>(db, "SELECT state FROM captures WHERE id = ?", scenario.discardedCapture.id);
      expect(row?.state).toBe("discarded");
    });

    await test.step("the stale thumbnails are gone, and the live ones stay", () => {
      for (const file of [...scenario.staleThumbnails, ...scenario.doomedThumbnails]) {
        expect(fileExists(file), file).toBe(false);
      }
      for (const file of scenario.liveThumbnails) {
        expect(fileExists(file), file).toBe(true);
      }
      expect(listFileNames(scenario.plan.thumbnailsDir)).toEqual(scenario.liveThumbnails.map((file) => path.basename(file)).sort());
    });

    await test.step("the live data is untouched: every row still matches its file's size and hash", () => {
      const captures = queryAll<{ id: string; guest_cycle_id: string; file_name: string; byte_length: number; sha256: string }>(
        db,
        "SELECT id, guest_cycle_id, file_name, byte_length, sha256 FROM captures WHERE state = 'kept' ORDER BY id",
      );
      const planned = scenario.plan.captures.filter((capture) => capture.state === "kept" && capture.id !== scenario.openCapture.id);
      const liveKept = planned.filter((capture) =>
        scenario.plan.cycles.some((cycle) => cycle.id === capture.cycleId && !cycle.deleting && cycle.eventId !== scenario.deletingEvent.id),
      );
      expect(captures.map((row) => row.id).sort()).toEqual([...liveKept, scenario.openCapture].map((capture) => capture.id).sort());
      for (const row of captures) {
        const capture = scenario.plan.captures.find((candidate) => candidate.id === row.id);
        expect(capture, `a seeded Capture for ${row.id}`).toBeDefined();
        expect(row.byte_length).toBe(capture?.bytes.length);
        expect(row.sha256).toBe(capture?.sha256);
        expect(fingerprint(capture?.path ?? "")).toEqual({ bytes: row.byte_length, sha256: row.sha256 });
      }

      const strips = queryAll<{ guest_cycle_id: string; byte_length: number; sha256: string }>(
        db,
        "SELECT guest_cycle_id, byte_length, sha256 FROM photo_strips ORDER BY guest_cycle_id",
      );
      expect(strips.map((row) => row.guest_cycle_id).sort()).toEqual(
        scenario.plan.strips
          .filter((strip) => strip.cycleId !== scenario.deletingCycle.id && strip.cycleId !== scenario.deletingEventCycle.id)
          .map((strip) => strip.cycleId)
          .sort(),
      );
      for (const row of strips) {
        const strip = scenario.plan.strips.find((candidate) => candidate.cycleId === row.guest_cycle_id);
        expect(row.sha256).toBe(strip?.sha256);
        expect(fingerprint(strip?.path ?? "")).toEqual({ bytes: row.byte_length, sha256: row.sha256 });
      }
      expect(fingerprint(scenario.healthyStrip.path)?.sha256).toBe(scenario.healthyStrip.sha256);

      const cycles = queryAll<{ id: string; end_reason: string }>(db, "SELECT id, end_reason FROM guest_cycles ORDER BY id");
      expect(cycles.filter((cycle) => cycle.end_reason === "completed")).toHaveLength(2);
      expect(cycles).toHaveLength(3);
    });

    await test.step("the database holds exactly the live Events", () => {
      const events = queryAll<{ id: string; deleting_at: string | null }>(db, "SELECT id, deleting_at FROM events ORDER BY id");
      expect(events.map((event) => event.id).sort()).toEqual(scenario.liveEvents.map(({ event }) => event.id).sort());
      expect(events.every((event) => event.deleting_at === null)).toBe(true);
    });

    await test.step("the Event list shows only the live Events, newest date first, with their Guest Cycle counts", async () => {
      const list = new EventRows(operatorPage);
      await expect(list.rows).toHaveCount(scenario.liveEvents.length);
      const rows = await list.read();
      expect(rows.map((row) => row.name)).toEqual(scenario.liveEvents.map(({ event }) => event.name));
      expect(rows.map((row) => row.name)).not.toContain(scenario.deletingEvent.name);
      scenario.liveEvents.forEach(({ event, completedGuestCycles }, index) => {
        const noun = completedGuestCycles === 1 ? "1 Guest Cycle" : `${String(completedGuestCycles)} Guest Cycles`;
        const used = completedGuestCycles === 0 ? "Not used yet" : "Last used .+";
        expect(rows[index]?.facts).toMatch(new RegExp(`^[A-Z][a-z]{2} \\d{1,2} [A-Z][a-z]{2} 2026 \\| ${escape(event.venue ?? "")} \\| ${noun} \\| ${used}$`));
      });
    });

    await test.step("the app exits cleanly", async () => {
      expect((await app.close()).code).toBe(0);
    });
  },
);

function escape(text: string): string {
  return text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}
