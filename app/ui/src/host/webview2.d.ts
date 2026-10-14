/**
 * The subset of the WebView2 `window.chrome.webview` object the UI uses.
 * It exists only when the page runs inside the WPF host's WebView2 control.
 */
interface WebView2MessageEvent {
  /** The message the host posted with `PostWebMessageAsJson`, already decoded. */
  readonly data: unknown;
}

/** A shared buffer the host posted with `PostSharedBufferToScript`; `getBuffer()` is valid until `releaseBuffer`. */
interface WebView2SharedBufferEvent {
  /** The `additionalDataAsJson` the host sent with the buffer, already decoded (a JSON string is tolerated). */
  readonly additionalData?: unknown;
  getBuffer(): ArrayBuffer;
}

interface WebView2Bridge {
  postMessage(message: unknown): void;
  addEventListener(type: "message", listener: (event: WebView2MessageEvent) => void): void;
  addEventListener(type: "sharedbufferreceived", listener: (event: WebView2SharedBufferEvent) => void): void;
  removeEventListener(type: "message", listener: (event: WebView2MessageEvent) => void): void;
  removeEventListener(type: "sharedbufferreceived", listener: (event: WebView2SharedBufferEvent) => void): void;
  /** Gives a shared buffer back to the host; the page must not touch it afterwards. */
  releaseBuffer(buffer: ArrayBuffer): void;
}

interface Window {
  chrome?: {
    webview?: WebView2Bridge;
  };
}
