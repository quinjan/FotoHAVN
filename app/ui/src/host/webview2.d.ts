/**
 * The subset of the WebView2 `window.chrome.webview` object the UI uses.
 * It exists only when the page runs inside the WPF host's WebView2 control.
 */
interface WebView2MessageEvent {
  /** The message the host posted with `PostWebMessageAsJson`, already decoded. */
  readonly data: unknown;
}

interface WebView2Bridge {
  postMessage(message: unknown): void;
  addEventListener(type: "message", listener: (event: WebView2MessageEvent) => void): void;
  removeEventListener(type: "message", listener: (event: WebView2MessageEvent) => void): void;
}

interface Window {
  chrome?: {
    webview?: WebView2Bridge;
  };
}
