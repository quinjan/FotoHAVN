import { createContext, use, useSyncExternalStore } from "react";
import type { BrainClient, BrainClientState } from "./types.ts";

export const BrainClientContext = createContext<BrainClient | null>(null);

/** The page's brain client; surfaces use it to send commands. */
export function useBrainClient(): BrainClient {
  const client = use(BrainClientContext);
  if (client === null) {
    throw new Error("FOTOHAVN: useBrainClient needs a BrainClientContext provider.");
  }
  return client;
}

export function useBrainState(client: BrainClient): BrainClientState {
  return useSyncExternalStore(client.subscribe, client.getState);
}
