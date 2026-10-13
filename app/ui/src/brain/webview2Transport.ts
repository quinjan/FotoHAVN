import type { ClientMessage } from "../protocol/generated.ts";
import type { Transport, TransportListener } from "./types.ts";

/**
 * The WPF host's bridge. The host posts each brain message with `PostWebMessageAsJson`, so `event.data` is already
 * the decoded object, and reads ours with `WebMessageAsJson`, so we post objects, never JSON strings. The bridge is
 * open for the page's whole life; a reload runs a new page that sends a new `hello`.
 */
export function createWebView2Transport(webview: WebView2Bridge): Transport {
  let handler: ((event: WebView2MessageEvent) => void) | null = null;

  return {
    kind: "webview2",
    start(listener: TransportListener) {
      if (handler !== null) {
        return;
      }
      handler = (event) => {
        listener.onMessage(event.data);
      };
      webview.addEventListener("message", handler);
      listener.onOpen();
    },
    send(message: ClientMessage) {
      webview.postMessage(message);
      return true;
    },
    stop() {
      if (handler !== null) {
        webview.removeEventListener("message", handler);
        handler = null;
      }
    },
  };
}
