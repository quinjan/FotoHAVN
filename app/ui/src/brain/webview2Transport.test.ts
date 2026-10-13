import { describe, expect, it, vi } from "vitest";
import { operatorEventListEmpty } from "../fixtures/inlineFixtures.ts";
import { PROTOCOL_VERSION } from "../protocol/generated.ts";
import { ProtocolBrainClient } from "./protocolClient.ts";
import { createWebView2Transport } from "./webview2Transport.ts";

function fakeWebView() {
  const listeners = new Set<(event: WebView2MessageEvent) => void>();
  const postMessage = vi.fn<(message: unknown) => void>();
  const addEventListener = vi.fn((_type: "message", listener: (event: WebView2MessageEvent) => void) => {
    listeners.add(listener);
  });
  const removeEventListener = vi.fn((_type: "message", listener: (event: WebView2MessageEvent) => void) => {
    listeners.delete(listener);
  });
  const webview: WebView2Bridge = { postMessage, addEventListener, removeEventListener };
  return {
    webview,
    postMessage,
    addEventListener,
    removeEventListener,
    listeners,
    post: (data: unknown) => {
      for (const listener of listeners) {
        listener({ data });
      }
    },
  };
}

describe("createWebView2Transport", () => {
  it("posts hello as an object on start and delivers the host's decoded messages", () => {
    const { webview, postMessage, post } = fakeWebView();
    const client = new ProtocolBrainClient(createWebView2Transport(webview));
    expect(client.kind).toBe("webview2");

    client.connect();
    expect(postMessage).toHaveBeenCalledExactlyOnceWith({ type: "hello", protocolVersion: PROTOCOL_VERSION });

    post(operatorEventListEmpty);
    expect(client.getState()).toMatchObject({ status: "connected", session: { role: "operator", revision: 1 } });
  });

  it("correlates a command result through the bridge", async () => {
    const { webview, postMessage, post } = fakeWebView();
    const client = new ProtocolBrainClient(createWebView2Transport(webview), { createId: () => "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b" });
    client.connect();
    post(operatorEventListEmpty);

    const result = client.sendCommand("OpenNewEvent", {});
    expect(postMessage).toHaveBeenLastCalledWith({
      type: "command",
      id: "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b",
      name: "OpenNewEvent",
      payload: {},
    });
    post({ type: "commandResult", id: "6f1c2a8e-3b4d-4e5f-9a0b-1c2d3e4f5a6b", status: "rejected", reason: "notAvailable" });
    await expect(result).resolves.toEqual({ status: "rejected", reason: "notAvailable" });
  });

  it("starts once and removes its listener on stop", () => {
    const { webview, listeners, addEventListener, removeEventListener } = fakeWebView();
    const transport = createWebView2Transport(webview);
    const listener = { onOpen: vi.fn(), onMessage: vi.fn(), onClose: vi.fn() };

    transport.start(listener);
    transport.start(listener);
    expect(addEventListener).toHaveBeenCalledOnce();
    expect(listener.onOpen).toHaveBeenCalledOnce();
    expect(listeners.size).toBe(1);

    transport.stop();
    transport.stop();
    expect(removeEventListener).toHaveBeenCalledOnce();
    expect(listeners.size).toBe(0);
  });
});
