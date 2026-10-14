import { lazy, Suspense, type ComponentType } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { CameraDiagnosticsScreen } from "../components/CameraDiagnosticsScreen/index.ts";
import { silentFrameSource } from "../components/LivePreview/index.ts";
import type { CameraDiagnosticsView } from "../protocol/generated.ts";
import { useFrameStats } from "../preview/useFrameStats.ts";
import type { CameraDiagnosticsWithHooksProps } from "./CameraDiagnosticsHooks.tsx";

export interface CameraDiagnosticsSurfaceProps {
  readonly view: CameraDiagnosticsView;
}

/**
 * The screen with E2E controls, or null outside an E2E build. Vite replaces the build-time condition, so a field build
 * drops this expression and its dynamic import: `dist/` holds no code that sends a test hook from this screen.
 */
const HookedScreen: ComponentType<CameraDiagnosticsWithHooksProps> | null =
  import.meta.env.VITE_E2E === "true"
    ? lazy(() => import("./CameraDiagnosticsHooks.tsx").then((module) => ({ default: module.CameraDiagnosticsWithHooks })))
    : null;

/**
 * The `cameraDiagnostics` surface: the brain's view, the live frames from this connection's frame source, and the
 * frame rate and age the page measures on them, read-only. An E2E build then swaps in the screen with controls (the
 * camera list, `Mirror`, and `CAPTURE`, which send the `test.*` hooks), showing the read-only screen until it loads.
 */
export function CameraDiagnosticsSurface({ view }: CameraDiagnosticsSurfaceProps) {
  const client = useBrainClient();
  const source = client.frames ?? silentFrameSource;
  const measured = useFrameStats(source, view.state === "ready");
  const readOnly = <CameraDiagnosticsScreen view={view} source={source} measured={measured} />;
  return HookedScreen === null ? (
    readOnly
  ) : (
    <Suspense fallback={readOnly}>
      <HookedScreen view={view} source={source} measured={measured} />
    </Suspense>
  );
}
