import { describe, expect, it, vi } from "vitest";
import { getHostBridge, postToHost, signalReady } from "./hostBridge.ts";

function windowWith(chrome: Window["chrome"]): Window {
  return { chrome } as unknown as Window;
}

describe("hostBridge", () => {
  it("returns the WebView2 bridge when present", () => {
    const webview = { postMessage: vi.fn() };
    expect(getHostBridge(windowWith({ webview }))).toBe(webview);
  });

  it("returns undefined without window.chrome or window.chrome.webview", () => {
    expect(getHostBridge(windowWith(undefined))).toBeUndefined();
    expect(getHostBridge(windowWith({}))).toBeUndefined();
  });

  it("posts the message when the bridge exists", () => {
    const postMessage = vi.fn();
    expect(postToHost({ type: "ready" }, windowWith({ webview: { postMessage } }))).toBe(true);
    expect(postMessage).toHaveBeenCalledExactlyOnceWith({ type: "ready" });
  });

  it("does nothing and does not throw without the bridge", () => {
    expect(postToHost({ type: "ready" }, windowWith(undefined))).toBe(false);
    expect(signalReady(windowWith({}))).toBe(false);
  });

  it("signalReady posts a single ready message", () => {
    const postMessage = vi.fn();
    expect(signalReady(windowWith({ webview: { postMessage } }))).toBe(true);
    expect(postMessage).toHaveBeenCalledExactlyOnceWith({ type: "ready" });
  });

  it("defaults to the global window", () => {
    expect(signalReady()).toBe(false);
    const postMessage = vi.fn();
    vi.stubGlobal("chrome", { webview: { postMessage } });
    expect(signalReady()).toBe(true);
    expect(postMessage).toHaveBeenCalledOnce();
  });
});
