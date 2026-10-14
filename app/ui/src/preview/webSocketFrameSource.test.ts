import { describe, expect, it, vi } from "vitest";
import { fakeImages } from "../test/fakeImageData.ts";
import { frameHeaderSize, parseFrameMessage, WebSocketFrameSource } from "./webSocketFrameSource.ts";

interface MessageOptions {
  readonly seq?: number;
  readonly width?: number;
  readonly height?: number;
  readonly timestampMs?: number;
  readonly magic?: readonly number[];
  readonly version?: number;
  readonly reserved?: number;
  /** Extra bytes after the pixels (or fewer, when negative). */
  readonly extra?: number;
  readonly fill?: number;
}

/** A frame message built by hand, byte for byte as the contract lays it out. */
function message({
  seq = 42,
  width = 2,
  height = 2,
  timestampMs = 18_304,
  magic = [0x46, 0x48, 0x50, 0x46],
  version = 1,
  reserved = 0,
  extra = 0,
  fill = 7,
}: MessageOptions = {}): ArrayBuffer {
  const buffer = new ArrayBuffer(frameHeaderSize + width * height * 4 + extra);
  const bytes = new Uint8Array(buffer);
  const view = new DataView(buffer);
  bytes.set(magic);
  view.setUint16(4, version, true);
  view.setUint16(6, reserved, true);
  view.setUint32(8, seq, true);
  view.setUint16(12, width, true);
  view.setUint16(14, height, true);
  view.setUint32(16, timestampMs % 2 ** 32, true);
  view.setUint32(20, Math.floor(timestampMs / 2 ** 32), true);
  bytes.fill(fill, frameHeaderSize);
  return buffer;
}

describe("parseFrameMessage", () => {
  it("reads every field of a valid message", () => {
    expect(parseFrameMessage(message({ seq: 42, width: 1280, height: 720, timestampMs: 18_304 }))).toEqual({
      ok: true,
      header: { seq: 42, width: 1280, height: 720, timestampMs: 18_304 },
    });
  });

  it("reads the full unsigned ranges: a 32-bit sequence and a timestamp past 32 bits", () => {
    expect(parseFrameMessage(message({ seq: 0xffff_ffff, timestampMs: 5_000_000_000 }))).toMatchObject({
      ok: true,
      header: { seq: 0xffff_ffff, timestampMs: 5_000_000_000 },
    });
  });

  it("ignores the reserved field", () => {
    expect(parseFrameMessage(message({ reserved: 0xabcd })).ok).toBe(true);
  });

  it("is little-endian", () => {
    const raw = message({ seq: 0x0102_0304 });
    expect([...new Uint8Array(raw, 8, 4)]).toEqual([4, 3, 2, 1]);
    expect(parseFrameMessage(raw)).toMatchObject({ header: { seq: 0x0102_0304 } });
  });

  it("rejects a message shorter than the header, with no sequence", () => {
    expect(parseFrameMessage(new ArrayBuffer(0))).toEqual({ ok: false, reason: "truncated", seq: null });
    expect(parseFrameMessage(new ArrayBuffer(frameHeaderSize - 1))).toEqual({ ok: false, reason: "truncated", seq: null });
  });

  it("rejects a wrong magic, with no sequence", () => {
    expect(parseFrameMessage(message({ magic: [0x46, 0x48, 0x50, 0x47] }))).toEqual({ ok: false, reason: "magic", seq: null });
    expect(parseFrameMessage(message({ magic: [0, 0, 0, 0] }))).toEqual({ ok: false, reason: "magic", seq: null });
  });

  it("rejects another version, with no sequence", () => {
    expect(parseFrameMessage(message({ version: 2 }))).toEqual({ ok: false, reason: "version", seq: null });
    expect(parseFrameMessage(message({ version: 0 }))).toEqual({ ok: false, reason: "version", seq: null });
  });

  it.each([
    ["a zero width", { width: 0, height: 2 }],
    ["a zero height", { width: 2, height: 0 }],
    ["a width past the limit", { width: 4097, height: 1 }],
    ["a height past the limit", { width: 1, height: 4097 }],
  ])("rejects %s, keeping the sequence", (_name, size) => {
    const raw = new ArrayBuffer(frameHeaderSize);
    const view = new DataView(raw);
    new Uint8Array(raw).set([0x46, 0x48, 0x50, 0x46]);
    view.setUint16(4, 1, true);
    view.setUint32(8, 11, true);
    view.setUint16(12, size.width, true);
    view.setUint16(14, size.height, true);
    expect(parseFrameMessage(raw)).toEqual({ ok: false, reason: "size", seq: 11 });
  });

  it("rejects a message whose length isn't the header plus width × height × 4, keeping the sequence", () => {
    expect(parseFrameMessage(message({ seq: 3, extra: -1 }))).toEqual({ ok: false, reason: "length", seq: 3 });
    expect(parseFrameMessage(message({ seq: 3, extra: 1 }))).toEqual({ ok: false, reason: "length", seq: 3 });
    expect(parseFrameMessage(message({ seq: 3, extra: -16 }))).toEqual({ ok: false, reason: "length", seq: 3 });
  });
});

function setup() {
  const acknowledge = vi.fn<(seq: number) => void>();
  const images = fakeImages();
  const source = new WebSocketFrameSource(acknowledge, images);
  const frames: ImageData[] = [];
  source.subscribe((frame) => {
    frames.push(frame as ImageData);
  });
  return { acknowledge, images, source, frames };
}

describe("WebSocketFrameSource", () => {
  it("delivers a frame's pixels without copying them, and acknowledges its sequence once", () => {
    const { source, frames, images, acknowledge } = setup();
    const raw = message({ seq: 42, width: 2, height: 3, fill: 9 });

    source.receive(raw);

    expect(frames).toHaveLength(1);
    expect(frames[0]).toMatchObject({ width: 2, height: 3 });
    expect(frames[0]?.data).toHaveLength(24);
    expect(frames[0]?.data.buffer).toBe(raw);
    expect(frames[0]?.data.byteOffset).toBe(frameHeaderSize);
    expect([...(frames[0]?.data ?? [])].every((byte) => byte === 9)).toBe(true);
    expect(images.wrapped).toHaveLength(1);
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(42);
  });

  it("delivers each message as its own frame and acknowledges each in turn", () => {
    const { source, frames, acknowledge } = setup();
    source.receive(message({ seq: 1 }));
    source.receive(message({ seq: 2 }));
    expect(frames).toHaveLength(2);
    expect(frames[0]).not.toBe(frames[1]);
    expect(acknowledge.mock.calls).toEqual([[1], [2]]);
  });

  it("acknowledges a frame nobody is subscribed to", () => {
    const acknowledge = vi.fn();
    const source = new WebSocketFrameSource(acknowledge, fakeImages());
    source.receive(message({ seq: 8 }));
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(8);
    expect(source.ignored).toBe(0);
  });

  it("stops delivering to an unsubscribed listener", () => {
    const acknowledge = vi.fn();
    const source = new WebSocketFrameSource(acknowledge, fakeImages());
    const listener = vi.fn();
    const unsubscribe = source.subscribe(listener);
    unsubscribe();
    source.receive(message());
    expect(listener).not.toHaveBeenCalled();
  });

  it("acknowledges even when a subscriber throws", () => {
    const acknowledge = vi.fn();
    const source = new WebSocketFrameSource(acknowledge, fakeImages());
    source.subscribe(() => {
      throw new Error("draw failed");
    });
    expect(() => {
      source.receive(message({ seq: 6 }));
    }).toThrow("draw failed");
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(6);
  });

  it.each([
    ["truncated", new ArrayBuffer(10)],
    ["a bad magic", message({ magic: [1, 2, 3, 4] })],
    ["a bad version", message({ version: 9 })],
  ])("ignores a %s message, and can't acknowledge it", (_name, raw) => {
    const { source, frames, acknowledge } = setup();
    source.receive(raw);
    expect(frames).toHaveLength(0);
    expect(acknowledge).not.toHaveBeenCalled();
    expect(source.ignored).toBe(1);
  });

  it("ignores a message of the wrong length, but acknowledges it so the host's slot frees", () => {
    const { source, frames, acknowledge } = setup();
    source.receive(message({ seq: 12, extra: -4 }));
    expect(frames).toHaveLength(0);
    expect(acknowledge).toHaveBeenCalledExactlyOnceWith(12);
    expect(source.ignored).toBe(1);
  });
});
