import { vi } from "vitest";

/** A fake `window.chrome.webview`: records posted messages, buffer releases, and listeners; the test drives both events. */
export function fakeWebView() {
  const messageListeners = new Set<(event: WebView2MessageEvent) => void>();
  const bufferListeners = new Set<(event: WebView2SharedBufferEvent) => void>();
  const postMessage = vi.fn<(message: unknown) => void>();
  const releaseBuffer = vi.fn<(buffer: ArrayBuffer) => void>();
  const addEventListener = vi.fn((type: string, listener: unknown) => {
    (type === "message" ? messageListeners : bufferListeners).add(listener as never);
  });
  const removeEventListener = vi.fn((type: string, listener: unknown) => {
    (type === "message" ? messageListeners : bufferListeners).delete(listener as never);
  });
  const webview: WebView2Bridge = { postMessage, addEventListener, removeEventListener, releaseBuffer };
  return {
    webview,
    postMessage,
    releaseBuffer,
    addEventListener,
    removeEventListener,
    listeners: messageListeners,
    bufferListeners,
    /** The host posts a brain message with `PostWebMessageAsJson`. */
    post: (data: unknown) => {
      for (const listener of messageListeners) {
        listener({ data });
      }
    },
    /** The host posts a shared buffer with `PostSharedBufferToScript`. */
    postBuffer: (buffer: ArrayBuffer, additionalData: unknown) => {
      for (const listener of bufferListeners) {
        listener({ additionalData, getBuffer: () => buffer });
      }
    },
  };
}
