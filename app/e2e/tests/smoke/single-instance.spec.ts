import { spawn, spawnSync } from "node:child_process";
import { readFileSync } from "node:fs";
import path from "node:path";
import { expect, test } from "../../src/fixtures";
import { paths } from "../../src/paths";
import { findWindow, foregroundHandle, minimizeWindow } from "../../src/win32";

const operatorTitle = "FOTOHAVN";
const hostLog = (dataDir: string): string => path.join(dataDir, "logs", "fotohavn.jsonl");

interface SecondLaunch {
  readonly code: number | null;
  readonly elapsedMs: number;
}

/** Starts a second FOTOHAVN on the same Data folder, as a person double-clicking the exe again would, and waits for it to exit. */
async function launchSecond(dataDir: string): Promise<SecondLaunch> {
  const startedAt = Date.now();
  const child = spawn(paths.exe, ["--data", dataDir], { cwd: paths.e2eBuildDir, stdio: "ignore" });
  try {
    return await new Promise<SecondLaunch>((resolve, reject) => {
      const timer = setTimeout(() => {
        reject(new Error(`The second launch (PID ${String(child.pid)}) did not exit within 20 s.`));
      }, 20_000);
      child.once("error", (error) => {
        clearTimeout(timer);
        reject(error);
      });
      child.once("exit", (code) => {
        clearTimeout(timer);
        resolve({ code, elapsedMs: Date.now() - startedAt });
      });
    });
  } finally {
    // A second launch that is still running (it hung past the timeout) would keep the Data folder's instance mutex and the exe locked.
    if (child.pid !== undefined && child.exitCode === null && child.signalCode === null) {
      spawnSync("taskkill", ["/PID", String(child.pid), "/T", "/F"], { stdio: "ignore" });
    }
  }
}

function instanceActivations(dataDir: string): string[] {
  return readFileSync(hostLog(dataDir), "utf8")
    .split(/\r?\n/)
    .filter((line) => line.includes('"event":"instanceActivated"'))
    .map((line) => (JSON.parse(line) as { outcome?: string }).outcome ?? "");
}

test(
  "a second launch on the same Data folder exits with code 3 and brings the first instance's Operator window forward",
  { tag: ["@m10", "@cursor"] },
  async ({ app, dataDir }) => {
    const pid = app.run.pid;
    if (pid === undefined) {
      throw new Error("The app has no PID.");
    }

    const operator = findWindow(pid, operatorTitle);
    expect(operator, "the Operator window").toBeDefined();
    if (operator === undefined) {
      return;
    }

    await test.step("the Operator window is minimized, as it would be behind a person's other work", async () => {
      expect(minimizeWindow(operator.handle)).toBe(true);
      expect(findWindow(pid, operatorTitle)?.minimized).toBe(true);
    });

    await test.step("the second launch exits with code 3 within seconds, without a window of its own", async () => {
      const second = await launchSecond(dataDir);
      expect(second.code).toBe(3);
      expect(second.elapsedMs, "the second launch only signals the first and leaves").toBeLessThan(10_000);
    });

    await test.step("the first instance restored its Operator window", async () => {
      await expect.poll(() => findWindow(pid, operatorTitle)?.minimized, { timeout: 10_000, message: "the Operator window is restored" }).toBe(false);
      await expect.poll(() => instanceActivations(dataDir).length, { timeout: 10_000 }).toBe(1);
    });

    await test.step("the activation outcome is foreground, or raised when Windows refused this shell the foreground (both restore the window)", async () => {
      const [outcome] = instanceActivations(dataDir);
      expect(["foreground", "raised"]).toContain(outcome);
      const isForeground = findWindow(pid, operatorTitle)?.handle === foregroundHandle();
      const description = `outcome ${outcome ?? "none"}; Operator window is the foreground window: ${String(isForeground)}`;
      test.info().annotations.push({ type: "activation", description });
      console.log(`Single instance: ${description}`);
    });

    await test.step("a third launch is refused the same way, and the first instance is untouched", async () => {
      const third = await launchSecond(dataDir);
      expect(third.code).toBe(3);
      expect((await app.hooks.getDiagnostics()).storage.state).toBe("ready");
      expect((await app.hooks.getDiagnostics()).instance.mutexName).toMatch(/^Local\\FOTOHAVN-[0-9a-f]{16}$/);
    });
  },
);
