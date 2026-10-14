import { expect, test } from "../../src/fixtures";
import { maxFrameAgeMs, openDiagnostics, waitForFreshFrames } from "../../src/camera-diagnostics";

/** The S1 floor (ADR 0004): frames delivered per second over a steady state, with at most 10% dropped. */
const fpsFloor = 15;
const maxDroppedShare = 0.1;
const windowMs = 10_000;
const sampleEveryMs = 500;
const warmUpMs = 2_000;

interface Sample {
  atMs: number;
  frameAgeMs: number | null;
  deliveredFps: number | null;
  droppedFrames: number | null;
  drawn: number;
  canvasWidth: number;
  canvasHeight: number;
}

test(
  "on the diagnostics screen, the selected camera shows a live preview; frame age stays under 500 ms; the frame rate meets the S1 floor for 10 s",
  { tag: ["@m5"] },
  async ({ app, operatorPage, hooks, camera }, testInfo) => {
    const screen = await openDiagnostics(operatorPage, hooks, camera);

    await test.step("the camera becomes ready with fresh frames and the preview goes live", async () => {
      const age = await waitForFreshFrames(hooks);
      console.log(`first fresh frame age ${age.toFixed(0)} ms`);
      await expect(screen.root).toHaveAttribute("data-camera-state", "ready");
      await expect(screen.preview).toHaveAttribute("data-state", "live");
      await expect(screen.canvas).toBeVisible();
    });

    // Counts the page's own canvas draws (the LivePreview draws each arrived frame with putImageData or drawImage).
    await operatorPage.evaluate(() => {
      const counter = { drawn: 0 };
      (window as unknown as { __fotohavnDrawn: { drawn: number } }).__fotohavnDrawn = counter;
      const proto = CanvasRenderingContext2D.prototype;
      const putImageData = proto.putImageData as (...args: unknown[]) => void;
      const drawImage = proto.drawImage as (...args: unknown[]) => void;
      proto.putImageData = function (this: CanvasRenderingContext2D, ...args: unknown[]) {
        counter.drawn++;
        return putImageData.apply(this, args);
      } as typeof proto.putImageData;
      proto.drawImage = function (this: CanvasRenderingContext2D, ...args: unknown[]) {
        counter.drawn++;
        return drawImage.apply(this, args);
      } as typeof proto.drawImage;
    });

    const samples: Sample[] = [];
    await test.step("sample every 500 ms for 10 s", async () => {
      const started = performance.now();
      while (performance.now() - started < windowMs) {
        const tick = performance.now();
        const { camera: report } = await hooks.getDiagnostics();
        const page = await operatorPage.evaluate(() => {
          const canvas = document.querySelector("canvas");
          return {
            drawn: (window as unknown as { __fotohavnDrawn: { drawn: number } }).__fotohavnDrawn.drawn,
            width: canvas?.width ?? 0,
            height: canvas?.height ?? 0,
          };
        });
        samples.push({
          atMs: tick - started,
          frameAgeMs: report.frameAgeMs,
          deliveredFps: report.deliveredFps,
          droppedFrames: report.droppedFrames,
          drawn: page.drawn,
          canvasWidth: page.width,
          canvasHeight: page.height,
        });
        expect(report.state, `camera state at ${String(Math.round(tick - started))} ms`).toBe("ready");
        const wait = sampleEveryMs - (performance.now() - tick);
        if (wait > 0) {
          await new Promise((resolve) => setTimeout(resolve, wait));
        }
      }
    });

    const steady = samples.filter((sample) => sample.atMs >= warmUpMs);
    const first = steady[0];
    const last = steady.at(-1);
    expect(first, "samples after warm-up").toBeDefined();
    expect(last, "samples after warm-up").toBeDefined();
    if (first === undefined || last === undefined) {
      return;
    }

    await test.step("frame age stays under 500 ms at every sample", () => {
      const ages = samples.map((sample) => sample.frameAgeMs);
      expect(ages.every((age) => age !== null)).toBe(true);
      const worst = Math.max(...ages.map((age) => age ?? Number.POSITIVE_INFINITY));
      console.log(`frame age: max ${worst.toFixed(0)} ms over ${String(samples.length)} samples`);
      expect(worst).toBeLessThan(maxFrameAgeMs);
    });

    const deliveredFps = steady.map((sample) => sample.deliveredFps ?? 0);
    const seconds = (last.atMs - first.atMs) / 1_000;
    const drawnFps = (last.drawn - first.drawn) / seconds;

    await test.step("the delivered frame rate meets the S1 floor after a 2 s warm-up", () => {
      const min = Math.min(...deliveredFps);
      const mean = deliveredFps.reduce((total, fps) => total + fps, 0) / deliveredFps.length;
      console.log(
        `delivered fps (booth): min ${min.toFixed(1)}, mean ${mean.toFixed(1)}; page draws ${drawnFps.toFixed(1)}/s over ${seconds.toFixed(1)} s`,
      );
      expect(min).toBeGreaterThanOrEqual(fpsFloor);
      expect(drawnFps, "frames the page drew per second").toBeGreaterThanOrEqual(fpsFloor);
    });

    await test.step("at most 10% of the arrived frames were dropped", () => {
      // Arrived = delivered + dropped; delivered is the booth's trailing rate integrated over the window.
      const mean = deliveredFps.reduce((total, fps) => total + fps, 0) / deliveredFps.length;
      const delivered = mean * seconds;
      const dropped = (last.droppedFrames ?? 0) - (first.droppedFrames ?? 0);
      const share = dropped / (dropped + delivered);
      console.log(`dropped ${String(dropped)} of ~${String(Math.round(dropped + delivered))} arrived (${(share * 100).toFixed(2)}%)`);
      expect(share).toBeLessThanOrEqual(maxDroppedShare);
    });

    await test.step("the page's own canvas holds a picture and keeps updating", async () => {
      const read = () =>
        operatorPage.evaluate(() => {
          const canvas = document.querySelector("canvas");
          const context = canvas?.getContext("2d");
          if (canvas === null || context === null || context === undefined) {
            return null;
          }
          const { data } = context.getImageData(0, 0, canvas.width, canvas.height);
          let sum = 0;
          for (let at = 0; at < data.length; at += 4) {
            sum += data[at]! + data[at + 1]! + data[at + 2]!;
          }
          return {
            width: canvas.width,
            height: canvas.height,
            meanLevel: sum / (3 * canvas.width * canvas.height),
            drawn: (window as unknown as { __fotohavnDrawn: { drawn: number } }).__fotohavnDrawn.drawn,
          };
        });
      const before = await read();
      await new Promise((resolve) => setTimeout(resolve, 500));
      const after = await read();
      console.log(
        `canvas ${String(before?.width)}×${String(before?.height)}, mean level ${before?.meanLevel.toFixed(2)}; draws ${String(before?.drawn)} → ${String(after?.drawn)}`,
      );
      expect(before).not.toBeNull();
      expect(after).not.toBeNull();
      expect(before?.width).toBeGreaterThanOrEqual(1280);
      expect(before?.height).toBeGreaterThanOrEqual(720);
      expect(after?.drawn ?? 0).toBeGreaterThan(before?.drawn ?? 0);
    });

    await test.step("the screen shows the page's own measures", async () => {
      await expect(screen.measure("fps")).toHaveText(/^\d+ fps$/);
      await expect(screen.statusLine.first()).toHaveText("Ready");
    });

    await test.step("screenshot", async () => {
      const file = testInfo.outputPath("camera-live.png");
      await operatorPage.screenshot({ path: file });
      await testInfo.attach("camera-live", { path: file, contentType: "image/png" });
      console.log(`screenshot: ${file}`);
    });

    expect((await app.close()).code).toBe(0);
  },
);
