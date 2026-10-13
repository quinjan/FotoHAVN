import { useRef, useState } from "react";
import { useBrainClient } from "../brain/BrainClientContext.ts";
import { StorageErrorScreen } from "../components/StorageErrorScreen/index.ts";
import type { StorageErrorView } from "../protocol/generated.ts";
import { commandNotice } from "./format.ts";

const stillFailing = "The Data folder still needs attention.";

/** The `storageError` surface: `TRY AGAIN` sends `RetryStorage`, and the brain answers with a new snapshot on success. */
export function StorageErrorSurface({ view }: { readonly view: StorageErrorView }) {
  const client = useBrainClient();
  const inFlight = useRef(false);
  const [retrying, setRetrying] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);

  async function retry(): Promise<void> {
    if (inFlight.current) {
      return;
    }
    inFlight.current = true;
    setRetrying(true);
    setNotice(null);
    const result = await client.sendCommand("RetryStorage", {});
    inFlight.current = false;
    setRetrying(false);
    // An accepted retry that leaves this screen showing means the folder still can't be used.
    setNotice(commandNotice(result) ?? stillFailing);
  }

  return (
    <StorageErrorScreen
      kind={view.kind}
      dataPath={view.dataPath}
      retrying={retrying}
      notice={notice}
      onRetry={() => {
        void retry();
      }}
    />
  );
}
