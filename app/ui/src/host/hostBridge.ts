export interface ReadyMessage {
  readonly type: "ready";
}

/** Messages the UI sends to the host. M2 replaces this with the protocol envelope. */
export type HostMessage = ReadyMessage;

/** Returns the WebView2 bridge, or undefined outside the WPF host. */
export function getHostBridge(target: Window = window): WebView2Bridge | undefined {
  return target.chrome?.webview;
}

/** Posts a message to the host. Returns false (and does nothing) outside WebView2. */
export function postToHost(message: HostMessage, target: Window = window): boolean {
  const bridge = getHostBridge(target);
  if (bridge === undefined) {
    return false;
  }
  bridge.postMessage(message);
  return true;
}

/** Tells the host the UI has mounted. */
export function signalReady(target: Window = window): boolean {
  return postToHost({ type: "ready" }, target);
}
