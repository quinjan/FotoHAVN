import { describe, expect, it, vi } from "vitest";
import { operatorEventListEmpty } from "../fixtures/inlineFixtures.ts";
import { PROTOCOL_VERSION } from "../protocol/generated.ts";
import { fakeWebView } from "../test/fakeWebView.ts";
import { stubImageData } from "../test/fakeImageData.ts";
import { ProtocolBrainClient } from "./protocolClient.ts";
import { createWebView2Transport } from "./webview2Transport.ts";

describe("createWebView2Transport", () => {
  it("serves previews at the preview.fotohavn virtual host", () => {
    const { webview } = fakeWebView();
    expect(createWebView2Transport(webview).previewUrl?.("signature-strip.naturale-v1.r7")).toBe(
      "https://preview.fotohavn/signature-strip.naturale-v1.r7.png",
    );
  });
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

  it("starts once and removes its listeners on stop", () => {
    const { webview, listeners, bufferListeners, addEventListener, removeEventListener } = fakeWebView();
    const transport = createWebView2Transport(webview);
    const listener = { onOpen: vi.fn(), onMessage: vi.fn(), onClose: vi.fn() };

    transport.start(listener);
    transport.start(listener);
    // The message listener and the frame source's shared-buffer listener.
    expect(addEventListener).toHaveBeenCalledTimes(2);
    expect(listener.onOpen).toHaveBeenCalledOnce();
    expect(listeners.size).toBe(1);
    expect(bufferListeners.size).toBe(1);

    transport.stop();
    transport.stop();
    expect(removeEventListener).toHaveBeenCalledTimes(2);
    expect(listeners.size).toBe(0);
    expect(bufferListeners.size).toBe(0);
  });

  it("carries the live preview: a shared buffer is drawn, released, and acknowledged through postMessage", () => {
    stubImageData();
    const { webview, postMessage, releaseBuffer, postBuffer } = fakeWebView();
    const transport = createWebView2Transport(webview);
    transport.start({ onOpen: vi.fn(), onMessage: vi.fn(), onClose: vi.fn() });
    const frames = vi.fn();
    transport.frames?.subscribe(frames);

    const buffer = new ArrayBuffer(2 * 2 * 4);
    postBuffer(buffer, { type: "previewFrame", seq: 7, width: 2, height: 2, format: "rgba", timestampMs: 100 });

    expect(frames).toHaveBeenCalledOnce();
    expect(releaseBuffer).toHaveBeenCalledExactlyOnceWith(buffer);
    expect(postMessage).toHaveBeenLastCalledWith({ type: "frameAck", seq: 7 });
  });
});
