/**
 * The subset of the WebView2 `window.chrome.webview` object the UI uses.
 * It exists only when the page runs inside the WPF host's WebView2 control.
 */
interface WebView2Bridge {
  postMessage(message: unknown): void;
}

interface Window {
  chrome?: {
    webview?: WebView2Bridge;
  };
}
