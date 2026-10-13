import { useLayoutEffect } from "react";
import { BrainClientContext, useBrainState } from "../brain/BrainClientContext.ts";
import type { BrainClient, BrainClientState } from "../brain/types.ts";
import { GuestRoot } from "../guest/GuestRoot.tsx";
import { OperatorRoot } from "../operator/OperatorRoot.tsx";
import { ConnectingScreen, NotConnectedScreen, ProtocolMismatchScreen } from "../screens/StatusScreens.tsx";

/**
 * Mirrors the connection on the root element for end-to-end tests: `data-fotohavn-status` always, and
 * `data-fotohavn-role` and `data-fotohavn-revision` after each accepted snapshot (contract §9, UI).
 */
export function applyRootDataset(root: HTMLElement, state: BrainClientState): void {
  root.dataset.fotohavnStatus = state.status;
  if (state.session === null) {
    delete root.dataset.fotohavnRole;
    delete root.dataset.fotohavnRevision;
  } else {
    root.dataset.fotohavnRole = state.session.role;
    root.dataset.fotohavnRevision = String(state.session.revision);
  }
}

function useRootDataset(state: BrainClientState): void {
  useLayoutEffect(() => {
    applyRootDataset(document.documentElement, state);
  }, [state]);
}

function Surfaces({ state, fixtureNames }: { readonly state: BrainClientState; readonly fixtureNames: readonly string[] }) {
  const { status, session } = state;
  if (status === "notConnected") {
    return <NotConnectedScreen fixtureNames={fixtureNames} />;
  }
  if (status === "protocolMismatch") {
    return <ProtocolMismatchScreen mismatch={state.mismatch} />;
  }
  if (session === null) {
    return <ConnectingScreen />;
  }
  const reconnecting = status === "reconnecting";
  return session.role === "operator" ? (
    <OperatorRoot presentation={session.presentation} reconnecting={reconnecting} />
  ) : (
    <GuestRoot presentation={session.presentation} reconnecting={reconnecting} />
  );
}

/** The root: routes by connection status, then by the role the brain assigned in `welcome`. */
export function App({
  client,
  fixtureNames = [],
}: {
  readonly client: BrainClient;
  /** Links shown on the not-connected screen in development and E2E builds. */
  readonly fixtureNames?: readonly string[];
}) {
  const state = useBrainState(client);
  useRootDataset(state);
  return (
    <BrainClientContext value={client}>
      <Surfaces state={state} fixtureNames={fixtureNames} />
    </BrainClientContext>
  );
}
