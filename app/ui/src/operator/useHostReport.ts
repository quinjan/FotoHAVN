import { useEffect, useState } from "react";
import type { BrainClient } from "../brain/types.ts";
import type { HostFrameReport } from "../components/CameraDiagnosticsScreen/index.ts";

/** How often an E2E build asks the booth for its frame figures: a few times a second, never per frame. */
export const hostReportIntervalMs = 500;

function same(a: HostFrameReport | null, b: HostFrameReport): boolean {
  return a !== null && a.deliveredFps === b.deliveredFps && a.droppedFrames === b.droppedFrames && a.frameAgeMs === b.frameAgeMs;
}

/**
 * What the booth reports about the camera's frames (`test.getDiagnostics`; only the E2E screen uses this, since a field
 * build never sends a hook). One request at a time; null until the first answer, and when the booth answers with an
 * error, such as `hooksUnavailable`.
 */
export function useHostReport(client: BrainClient): HostFrameReport | null {
  const [report, setReport] = useState<HostFrameReport | null>(null);

  useEffect(() => {
    let active = true;
    let inFlight = false;
    async function poll(): Promise<void> {
      if (inFlight) {
        return;
      }
      inFlight = true;
      const result = await client.sendTest("test.getDiagnostics", null);
      inFlight = false;
      if (!active || !result.ok) {
        return;
      }
      const { frameAgeMs, deliveredFps, droppedFrames } = result.result.camera;
      const next: HostFrameReport = { frameAgeMs, deliveredFps, droppedFrames };
      setReport((current) => (same(current, next) ? current : next));
    }
    void poll();
    const timer = setInterval(() => {
      void poll();
    }, hostReportIntervalMs);
    return () => {
      active = false;
      clearInterval(timer);
    };
  }, [client]);

  return report;
}
