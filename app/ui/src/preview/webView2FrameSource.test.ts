import { describe, expect, it, vi } from "vitest";
import { fakeImages } from "../test/fakeImageData.ts";
import { fakeWebView } from "../test/fakeWebView.ts";
import { WebView2FrameSource } from "./webView2FrameSource.ts";

function metadata(seq: number, width = 2, height = 2, extra: Record<string, unknown> = {}) {
  return { type: "previewFrame", seq, width, height, format: "rgba", timestampMs: 1_000 + seq, ...extra };
}

function pixels(width: number, height: number, fill: number): ArrayBuffer {
  const buffer = new ArrayBuffer(width * height * 4);
  new Uint8Array(buffer).fill(fill);
  return buffer;
}

function setup() {
  const host = fakeWebView();
  const acknowledge = vi.fn<(seq: number) => void>();
  const images = fakeImages();
  const source = new WebView2FrameSource(host.webview, acknowledge, images);
  const frames: ImageData[] = [];
  source.subscribe((frame) => {
    frames.push(frame as ImageData);
  });
  source.start();
  return { ...host, acknowledge, images, source, frames };
}

describe("WebView2FrameSource listening", () => {
  it("listens for shared buffers from start() until stop(), once", () => {
    const { bufferListeners, addEventListener, removeEventListener, releaseBuffer, source } = setup();
    source.start();
    expect(addEventListener).toHaveBeenCalledExactlyOnceWith("sharedbufferreceived", expect.any(Function));
    expect(bufferListeners.size).toBe(1);

    source.stop();
    source.stop();
    expect(removeEventListener).toHaveBeenCalledExactlyOnceWith("sharedbufferreceived", expect.any(Function));
    expect(bufferListeners.size).toBe(0);
    expect(releaseBuffer).not.toHaveBeenCalled();
  });

  it("can start again after a stop", () => {
    const { bufferListeners, source } = setup();
    source.stop();
    source.start();
    expect(bufferListeners.size).toBe(1);
  });
});

describe("WebView2FrameSource frames", () => {
  it("draws the frame's pixels, then releases the buffer, then acknowledges it, once per frame", () => {
    const { postBuffer, releaseBuffer, acknowledge, frames, images } = setup();
    const buffer = pixels(2, 2, 200);

    postBuffer(buffer, metadata(42));

    expect(frames).toHaveLength(1);
    expect(frames[0]).toMatchObject({ width: 2, height: 2 });
    expect([...(frames[0]?.data ?? [])]).toEqual(new Array<number>(16).fill(200));
    expect(images.created).toHaveLength(1);
    expect(releaseBuffer).toHaveBeenCalledExactlyOnceWith(buffer);
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(42);
    expect(releaseBuffer.mock.invocationCallOrder[0]).toBeLessThan(acknowledge.mock.invocationCallOrder[0] ?? 0);
  });

  it("copies the pixels, so the host reusing the buffer after the ack can't change the frame", () => {
    const { postBuffer, frames } = setup();
    const buffer = pixels(2, 2, 10);
    postBuffer(buffer, metadata(1));
    new Uint8Array(buffer).fill(99);
    expect(frames[0]?.data[0]).toBe(10);
  });

  it("reuses one ImageData while the size stays the same, so a preview that draws only the latest sees the newest pixels", () => {
    const { postBuffer, frames, images } = setup();
    postBuffer(pixels(2, 2, 1), metadata(1));
    postBuffer(pixels(2, 2, 2), metadata(2));
    postBuffer(pixels(2, 2, 3), metadata(3));

    expect(images.created).toHaveLength(1);
    expect(frames[0]).toBe(frames[1]);
    expect(frames[2]?.data[0]).toBe(3);
  });

  it("makes a new ImageData when the size changes", () => {
    const { postBuffer, frames, images } = setup();
    postBuffer(pixels(2, 2, 1), metadata(1));
    postBuffer(pixels(4, 2, 1), metadata(2, 4, 2));
    postBuffer(pixels(4, 2, 1), metadata(3, 4, 2));
    postBuffer(pixels(2, 4, 1), metadata(4, 2, 4));

    expect(images.created.map(({ width, height }) => [width, height])).toEqual([
      [2, 2],
      [4, 2],
      [2, 4],
    ]);
    expect(frames).toHaveLength(4);
  });

  it("reads only width × height × 4 bytes of a buffer sized for a larger frame", () => {
    const { postBuffer, frames, acknowledge } = setup();
    const buffer = pixels(4, 4, 5);
    postBuffer(buffer, metadata(8, 2, 2));
    expect(frames[0]?.data).toHaveLength(16);
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(8);
  });

  it("accepts additionalData as a JSON string", () => {
    const { postBuffer, frames, acknowledge } = setup();
    postBuffer(pixels(2, 2, 1), JSON.stringify(metadata(9)));
    expect(frames).toHaveLength(1);
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(9);
  });

  it("accepts the largest wire sequence", () => {
    const { postBuffer, acknowledge } = setup();
    postBuffer(pixels(2, 2, 1), metadata(0xffff_ffff));
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(0xffff_ffff);
  });

  it("stops delivering to a subscriber that unsubscribed, and still releases and acknowledges", () => {
    const host = fakeWebView();
    const acknowledge = vi.fn();
    const source = new WebView2FrameSource(host.webview, acknowledge, fakeImages());
    const kept = vi.fn();
    const dropped = vi.fn();
    source.subscribe(kept);
    const unsubscribe = source.subscribe(dropped);
    source.start();

    unsubscribe();
    host.postBuffer(pixels(2, 2, 1), metadata(1));

    expect(kept).toHaveBeenCalledOnce();
    expect(dropped).not.toHaveBeenCalled();
    expect(host.releaseBuffer).toHaveBeenCalledOnce();
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(1);
  });

  it("releases and acknowledges a frame nobody is subscribed to", () => {
    const host = fakeWebView();
    const acknowledge = vi.fn();
    const source = new WebView2FrameSource(host.webview, acknowledge, fakeImages());
    source.start();
    host.postBuffer(pixels(2, 2, 1), metadata(3));
    expect(host.releaseBuffer).toHaveBeenCalledOnce();
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(3);
    expect(source.ignored).toBe(0);
  });

  it("releases and acknowledges even when a subscriber throws", () => {
    const host = fakeWebView();
    const acknowledge = vi.fn();
    const source = new WebView2FrameSource(host.webview, acknowledge, fakeImages());
    source.subscribe(() => {
      throw new Error("draw failed");
    });
    source.start();

    expect(() => {
      host.postBuffer(pixels(2, 2, 1), metadata(4));
    }).toThrow("draw failed");
    expect(host.releaseBuffer).toHaveBeenCalledOnce();
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(4);
  });
});

describe("WebView2FrameSource frames it won't draw", () => {
  it.each([
    ["a format other than rgba", metadata(5, 2, 2, { format: "bgra" })],
    ["a zero width", metadata(5, 0, 2)],
    ["a zero height", metadata(5, 2, 0)],
    ["a width past the limit", metadata(5, 4097, 1)],
    ["a fractional height", metadata(5, 2, 1.5)],
    ["a text width", metadata(5, "2" as unknown as number, 2)],
    ["no size", { type: "previewFrame", seq: 5, format: "rgba" }],
  ])("releases and acknowledges a frame with %s, drawing nothing", (_name, data) => {
    const { postBuffer, releaseBuffer, acknowledge, frames, source } = setup();
    const buffer = pixels(2, 2, 1);
    postBuffer(buffer, data);
    expect(frames).toHaveLength(0);
    expect(releaseBuffer).toHaveBeenCalledExactlyOnceWith(buffer);
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(5);
    expect(source.ignored).toBe(1);
  });

  it("ignores a buffer too short for its stated size, but frees the host's slot", () => {
    const { postBuffer, releaseBuffer, acknowledge, frames, source } = setup();
    postBuffer(pixels(2, 2, 1), metadata(6, 4, 4));
    expect(frames).toHaveLength(0);
    expect(releaseBuffer).toHaveBeenCalledOnce();
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(6);
    expect(source.ignored).toBe(1);
  });

  it.each([
    ["no metadata", undefined],
    ["null metadata", null],
    ["a number", 5],
    ["text that isn't JSON", "{nope"],
    ["JSON text that isn't an object", "7"],
    ["another message type", { type: "somethingElse", seq: 1, width: 2, height: 2, format: "rgba" }],
    ["no sequence", { type: "previewFrame", width: 2, height: 2, format: "rgba" }],
    ["a negative sequence", metadata(-1)],
    ["a fractional sequence", metadata(1.5)],
    ["a sequence past 32 bits", metadata(0x1_0000_0000)],
    ["a text sequence", metadata("7" as unknown as number)],
  ])("releases but can't acknowledge a frame with %s", (_name, data) => {
    const { postBuffer, releaseBuffer, acknowledge, frames, source } = setup();
    const buffer = pixels(2, 2, 1);
    postBuffer(buffer, data);
    expect(frames).toHaveLength(0);
    expect(releaseBuffer).toHaveBeenCalledExactlyOnceWith(buffer);
    expect(acknowledge).not.toHaveBeenCalled();
    expect(source.ignored).toBe(1);
  });
});
