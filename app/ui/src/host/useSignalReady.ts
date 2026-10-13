import { useEffect, useRef } from "react";
import { signalReady } from "./hostBridge.ts";

/**
 * Signals `ready` to the host once after the first mount. The ref survives
 * StrictMode's development-only effect re-run, so the host sees one message.
 */
export function useSignalReady(): void {
  const sent = useRef(false);
  useEffect(() => {
    if (sent.current) {
      return;
    }
    sent.current = true;
    signalReady();
  }, []);
}
