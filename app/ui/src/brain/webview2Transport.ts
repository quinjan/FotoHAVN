import type { ClientMessage } from "../protocol/generated.ts";
import { WebView2FrameSource } from "../preview/webView2FrameSource.ts";
import type { Transport, TransportListener } from "./types.ts";

/**
 * The WPF host's bridge. The host posts each brain message with `PostWebMessageAsJson`, so `event.data` is already
 * the decoded object, and reads ours with `WebMessageAsJson`, so we post objects, never JSON strings. The bridge is
 * open for the page's whole life; a reload runs a new page that sends a new `hello`.
 *
 * Live preview frames arrive as shared buffers on the same object (`sharedbufferreceived`); `frames` draws them and
 * posts each `frameAck` through `postMessage`, where the host intercepts it before the brain (contract §6).
 */
export function createWebView2Transport(webview: WebView2Bridge): Transport {
  let handler: ((event: WebView2MessageEvent) => void) | null = null;
  const frames = new WebView2FrameSource(webview, (seq) => {
    webview.postMessage({ type: "frameAck", seq } satisfies ClientMessage);
  });

  return {
    kind: "webview2",
    frames,
    start(listener: TransportListener) {
      if (handler !== null) {
        return;
      }
      handler = (event) => {
        listener.onMessage(event.data);
      };
      webview.addEventListener("message", handler);
      frames.start();
      listener.onOpen();
    },
    send(message: ClientMessage) {
      webview.postMessage(message);
      return true;
    },
    stop() {
      frames.stop();
      if (handler !== null) {
        webview.removeEventListener("message", handler);
        handler = null;
      }
    },
  };
}
