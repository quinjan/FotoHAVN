import { useRef, useState } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import type { TestHookName, TestResult } from "../brain/types.ts";
import {
  CameraDiagnosticsScreen,
  type CameraDiagnosticsControls,
  type MeasuredFrames,
} from "../components/CameraDiagnosticsScreen/index.ts";
import type { FrameSource } from "../components/LivePreview/index.ts";
import type { CameraDiagnosticsView } from "../protocol/generated.ts";
import { hookNotice } from "./format.ts";
import { useHostReport } from "./useHostReport.ts";

export interface CameraDiagnosticsWithHooksProps {
  readonly view: CameraDiagnosticsView;
  readonly source: FrameSource;
  readonly measured: MeasuredFrames;
}

/**
 * The camera diagnostics screen with its E2E controls: the camera list, `Mirror`, and `CAPTURE` send the `test.*`
 * hooks, and the booth's own frame figures come from `test.getDiagnostics`. The brain answers each hook with a new
 * presentation, which is what changes the state, the mirror, and the last capture shown.
 *
 * Only E2E builds load this module (`CameraDiagnosticsSurface` imports it behind a build-time guard), so a field
 * build contains no code that sends a test hook from this screen.
 */
export function CameraDiagnosticsWithHooks({ view, source, measured }: CameraDiagnosticsWithHooksProps) {
  const client = useBrainClient();
  const host = useHostReport(client);
  const capturing = useRef(false);
  const [busy, setBusy] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  async function run(request: Promise<TestResult<TestHookName>>): Promise<void> {
    setNotice(hookNotice(await request));
  }

  async function capture(): Promise<void> {
    if (capturing.current) {
      return;
    }
    capturing.current = true;
    setBusy(true);
    setNotice(null);
    await run(client.sendTest("test.captureDiagnostics", null));
    capturing.current = false;
    setBusy(false);
  }

  function select(deviceId: string): void {
    void run(client.sendTest("test.selectDiagnosticsCamera", { deviceId }));
  }

  function mirror(mirrored: boolean): void {
    void run(client.sendTest("test.setDiagnosticsMirror", { mirrored }));
  }

  function startCapture(): void {
    void capture();
  }

  const controls: CameraDiagnosticsControls = {
    onSelect: select,
    onMirrorChange: mirror,
    onCapture: startCapture,
    capturing: busy,
    notice,
  };

  return <CameraDiagnosticsScreen view={view} source={source} measured={measured} host={host} controls={controls} />;
}
