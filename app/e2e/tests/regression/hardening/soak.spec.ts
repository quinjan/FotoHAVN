import { appendFileSync, statSync, writeFileSync } from "node:fs";
import path from "node:path";
import { databaseFileName, fingerprint, queryAll } from "../../../src/data";
import { expect, test } from "../../../src/fixtures";
import { startGuestEvent, type GuestRun } from "../../../src/guest-cycle";
import { eventFolder } from "../../../src/operator";
import { paths } from "../../../src/paths";
import { sampleProcess, type ProcessSample } from "../../../src/process-stats";

/** The Guest Cycles the plan asks for; FOTOHAVN_SOAK_CYCLES shortens a development run, and the bounds then scale with it. */
const cycles = Number(process.env["FOTOHAVN_SOAK_CYCLES"] ?? "300");
/**
 * The brain's clock runs this many times faster, so the Phase Timers and the countdown cost fractions of a second; the camera stays
 * real-time. The brain's 10 s Capture timeout runs on the same scaled clock, so a frame must arrive within 10 s / scale of real time:
 * at 100 that is 100 ms, and one cycle in about 260 lost the race on the development laptop ("a Capture's frame was never taken", then
 * Operator Assistance). At 20 a frame has 500 ms, and a cycle takes about 5 s.
 */
const clockScale = Number(process.env["FOTOHAVN_SOAK_SCALE"] ?? "20");
const sampleEvery = 25;
/** The first sample (after this many cycles) is the baseline: it includes the one-off warm-up (JIT, caches, the first compositions). */
const warmUpCycles = 25;

// Bounds from the development laptop's 300-cycle run at a 20x clock (Dell Pro Max 16 MC16250, "Integrated Webcam" with its shutter closed),
// which grew over its cycle-25 baseline by at most: host working set 12.3 MiB, process tree 99.7 MiB, host handles 16, tree handles 18, and
// the database 8.5 KiB per cycle. The bounds are several times that, so a leak per cycle fails (300 cycles of 0.5 MiB or 1 handle each would),
// and a busier machine does not. The booth laptop's numbers go in the verification record beside these.
const MiB = 1024 * 1024;
const bounds = {
  /** Largest working-set growth of the host process over the baseline, at any later sample. */
  hostWorkingSetGrowthBytes: 100 * MiB,
  /** Largest growth of the whole process tree's working set (WebView2 included). */
  treeWorkingSetGrowthBytes: 300 * MiB,
  /** Largest handle-count growth of the host process over the baseline. */
  hostHandleGrowth: 150,
  /** Largest handle-count growth of the whole tree. */
  treeHandleGrowth: 200,
  /** The database (main file and write-ahead log) may grow this much per Guest Cycle after the baseline. */
  databaseBytesPerCycle: 32 * 1024,
} as const;

interface Sample {
  readonly cycles: number;
  readonly seconds: number;
  readonly process: ProcessSample;
  readonly databaseBytes: number;
}

function databaseBytes(dataDir: string): number {
  return ["", "-wal"].reduce((sum, suffix) => {
    try {
      return sum + statSync(path.join(dataDir, `${databaseFileName}${suffix}`)).size;
    } catch {
      return sum;
    }
  }, 0);
}

function completedCycles(dataDir: string, eventId: string): number {
  const [row] = queryAll<{ count: number }>(
    path.join(dataDir, databaseFileName),
    "SELECT COUNT(*) AS count FROM guest_cycles WHERE event_id = ? AND end_reason = 'completed'",
    eventId,
  );
  return row?.count ?? 0;
}

function describe(sample: Sample): string {
  const mib = (bytes: number): string => (bytes / MiB).toFixed(1);
  return (
    `cycle ${String(sample.cycles).padStart(3)} at ${sample.seconds.toFixed(0).padStart(5)} s: ` +
    `host WS ${mib(sample.process.host.workingSetBytes)} MiB, private ${mib(sample.process.host.privateBytes)} MiB, ${String(sample.process.host.handles)} handles, ${String(sample.process.host.threads)} threads; ` +
    `tree ${String(sample.process.tree.processes)} processes, WS ${mib(sample.process.tree.workingSetBytes)} MiB, ${String(sample.process.tree.handles)} handles; ` +
    `database ${(sample.databaseBytes / 1024).toFixed(0)} KiB`
  );
}

/** One untouched Guest Cycle: STEP INSIDE, then every Phase Timer runs out on the scaled clock, and the cycle completes. */
async function runCycle(run: GuestRun, index: number): Promise<void> {
  const controls = await run.expectStartReady();
  await controls.stepInside.click();
  await expect
    .poll(() => completedCycles(run.dataDir, run.eventId), { timeout: 90_000, intervals: [100, 150, 250], message: `Guest Cycle ${String(index)} completes` })
    .toBe(index);
}

test(
  `${String(cycles)} Guest Cycles on the real camera with the clock scaled keep memory, handles, and the database within bounds, and lose no Capture`,
  { tag: ["@m10", "@camera"] },
  async ({ app, operatorPage, camera, hooks, dataDir }) => {
    test.setTimeout(4 * 60 * 60_000);
    const pid = app.run.pid;
    if (pid === undefined) {
      throw new Error("The app has no PID.");
    }
    const curveFile = path.join(paths.e2eOutput, "soak-curve.txt");
    writeFileSync(curveFile, "");
    const record = (line: string): void => {
      console.log(line);
      appendFileSync(curveFile, `${line}\n`);
    };

    const run = await startGuestEvent(
      { app, page: operatorPage, camera },
      {
        name: "Soak",
        countdown: 3,
        templates: ["Signature Strip"],
        timers: { Template: 20, "Photographs: capture": 20, "Photographs: review": 20, Filters: 20, Keepsake: 5 },
      },
    );
    await run.expectStartReady();

    const samples: Sample[] = [];
    const startedAt = Date.now();
    const take = (completed: number): void => {
      const sample: Sample = {
        cycles: completed,
        seconds: (Date.now() - startedAt) / 1000,
        process: sampleProcess(pid),
        databaseBytes: databaseBytes(dataDir),
      };
      samples.push(sample);
      record(describe(sample));
    };

    await hooks.setClockScale(clockScale);
    try {
      take(0);
      for (let index = 1; index <= cycles; index += 1) {
        await runCycle(run, index);
        if (index % sampleEvery === 0 || index === cycles) {
          take(index);
        }
      }
    } finally {
      await hooks.setClockScale(1);
    }
    const totalSeconds = (Date.now() - startedAt) / 1000;
    record(`${String(cycles)} Guest Cycles in ${(totalSeconds / 60).toFixed(1)} min, ${(totalSeconds / cycles).toFixed(1)} s per cycle (clock scale ${String(clockScale)}).`);

    await test.step("the Event ends cleanly", async () => {
      await run.endEventFromOperator();
    });

    await test.step("every Guest Cycle completed with its four Captures and its Photo Strip, and every committed file is there with its hash", () => {
      const database = path.join(dataDir, databaseFileName);
      const rows = queryAll<{ id: string; end_reason: string | null }>(
        database,
        "SELECT id, end_reason FROM guest_cycles WHERE event_id = ? ORDER BY started_at, id",
        run.eventId,
      );
      expect(rows).toHaveLength(cycles);
      expect(new Set(rows.map((row) => row.end_reason))).toEqual(new Set(["completed"]));

      const problems: string[] = [];
      let capturesChecked = 0;
      for (const row of rows) {
        const captures = run.captures(row.id);
        const kept = captures.filter((capture) => capture.state === "kept");
        if (kept.length !== 4 || captures.map((capture) => capture.sequence).join(",") !== captures.map((_, position) => position + 1).join(",")) {
          problems.push(`cycle ${row.id}: ${String(kept.length)} kept of ${String(captures.length)} Captures, sequences ${captures.map((capture) => capture.sequence).join(",")}`);
        }
        const folder = path.join(eventFolder(dataDir, run.eventId), "GuestCycles", row.id);
        for (const capture of kept) {
          capturesChecked += 1;
          const found = fingerprint(path.join(folder, capture.file_name));
          if (found?.sha256 !== capture.sha256 || found.bytes !== capture.byte_length) {
            problems.push(`cycle ${row.id}: ${capture.file_name} is missing or differs from its row`);
          }
        }
        const strip = run.strip(row.id);
        const stripFile = strip === undefined ? undefined : fingerprint(path.join(folder, strip.file_name));
        if (strip === undefined || stripFile?.sha256 !== strip.sha256) {
          problems.push(`cycle ${row.id}: the Photo Strip is missing or differs from its row`);
        }
      }
      record(`Checked ${String(rows.length)} Guest Cycles, ${String(capturesChecked)} kept Captures, ${String(rows.length)} Photo Strips.`);
      expect(problems, problems.slice(0, 10).join("\n")).toEqual([]);
      expect(capturesChecked).toBe(cycles * 4);
    });

    await test.step("memory, handles, and the database stay within the recorded bounds after the warm-up", () => {
      const baseline = samples.find((sample) => sample.cycles >= Math.min(warmUpCycles, cycles));
      const later = samples.filter((sample) => baseline !== undefined && sample.cycles > baseline.cycles);
      expect(baseline, "a baseline sample").toBeDefined();
      if (baseline === undefined) {
        return;
      }
      const worst = (pick: (sample: Sample) => number): number => later.reduce((largest, sample) => Math.max(largest, pick(sample) - pick(baseline)), 0);
      const growth = {
        hostWorkingSetBytes: worst((sample) => sample.process.host.workingSetBytes),
        treeWorkingSetBytes: worst((sample) => sample.process.tree.workingSetBytes),
        hostHandles: worst((sample) => sample.process.host.handles),
        treeHandles: worst((sample) => sample.process.tree.handles),
      };
      const last = samples[samples.length - 1];
      const databasePerCycle = last === undefined || last.cycles === baseline.cycles ? 0 : (last.databaseBytes - baseline.databaseBytes) / (last.cycles - baseline.cycles);
      record(
        `Growth over the baseline at cycle ${String(baseline.cycles)} (worst later sample): host WS ${(growth.hostWorkingSetBytes / MiB).toFixed(1)} MiB, tree WS ${(growth.treeWorkingSetBytes / MiB).toFixed(1)} MiB, ` +
          `host handles ${String(growth.hostHandles)}, tree handles ${String(growth.treeHandles)}, database ${(databasePerCycle / 1024).toFixed(1)} KiB per cycle ` +
          `(bounds: ${String(bounds.hostWorkingSetGrowthBytes / MiB)} MiB, ${String(bounds.treeWorkingSetGrowthBytes / MiB)} MiB, ${String(bounds.hostHandleGrowth)}, ${String(bounds.treeHandleGrowth)}, ${String(bounds.databaseBytesPerCycle / 1024)} KiB).`,
      );
      test.info().annotations.push({ type: "soak curve", description: samples.map(describe).join("\n") });
      expect(growth.hostWorkingSetBytes, "host working-set growth").toBeLessThanOrEqual(bounds.hostWorkingSetGrowthBytes);
      expect(growth.treeWorkingSetBytes, "process-tree working-set growth").toBeLessThanOrEqual(bounds.treeWorkingSetGrowthBytes);
      expect(growth.hostHandles, "host handle growth").toBeLessThanOrEqual(bounds.hostHandleGrowth);
      expect(growth.treeHandles, "process-tree handle growth").toBeLessThanOrEqual(bounds.treeHandleGrowth);
      expect(databasePerCycle, "database growth per Guest Cycle").toBeLessThanOrEqual(bounds.databaseBytesPerCycle);
    });
  },
);
