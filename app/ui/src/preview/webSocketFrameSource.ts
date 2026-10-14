import type { FrameSource, PreviewFrame } from "../components/LivePreview/index.ts";
import {
  browserImageData,
  FrameSubscribers,
  maxFrameDimension,
  type FrameAck,
  type ImageDataFactory,
} from "./frameSupport.ts";

/** The binary frame header's size, in bytes (`PreviewFrameHeader.Size`). */
export const frameHeaderSize = 24;
export const frameHeaderVersion = 1;

/** "FHPF", the first four bytes of every frame message. */
const magic = [0x46, 0x48, 0x50, 0x46] as const;

export interface FrameHeader {
  readonly seq: number;
  readonly width: number;
  readonly height: number;
  /** The camera-clock timestamp in whole milliseconds. */
  readonly timestampMs: number;
}

export type FrameHeaderResult =
  | { readonly ok: true; readonly header: FrameHeader }
  | {
      readonly ok: false;
      readonly reason: "truncated" | "magic" | "version" | "size" | "length";
      /** The wire sequence when the header was trustworthy enough to read one, so the host's slot can be freed. */
      readonly seq: number | null;
    };

/**
 * Parses the 24-byte little-endian header and checks the whole message, as `PreviewFrameHeader.TryRead` does:
 * `FHPF`, version 1, `seq u32`, `width u16`, `height u16`, `timestampMs u64`, then exactly `width × height × 4` bytes.
 * The reserved field is ignored.
 */
export function parseFrameMessage(message: ArrayBuffer): FrameHeaderResult {
  if (message.byteLength < frameHeaderSize) {
    return { ok: false, reason: "truncated", seq: null };
  }
  const view = new DataView(message);
  if (magic.some((byte, index) => view.getUint8(index) !== byte)) {
    return { ok: false, reason: "magic", seq: null };
  }
  if (view.getUint16(4, true) !== frameHeaderVersion) {
    return { ok: false, reason: "version", seq: null };
  }
  const seq = view.getUint32(8, true);
  const width = view.getUint16(12, true);
  const height = view.getUint16(14, true);
  if (width === 0 || height === 0 || width > maxFrameDimension || height > maxFrameDimension) {
    return { ok: false, reason: "size", seq };
  }
  if (message.byteLength !== frameHeaderSize + width * height * 4) {
    return { ok: false, reason: "length", seq };
  }
  const timestampMs = view.getUint32(16, true) + view.getUint32(20, true) * 2 ** 32;
  return { ok: true, header: { seq, width, height, timestampMs } };
}

/**
 * The headless host's live preview frames (contract §6, "Headless WebSocket"): a binary message on the role's bridge
 * socket, which the WebSocket transport hands to `receive`. The message's own pixels back the `ImageData`, with no
 * copy, because the page owns each message. `frameAck` goes back as a text message through the same socket, once per
 * frame the header identifies; a frame the page can't draw is still acknowledged when its sequence could be read, so
 * the host's one in-flight frame never waits on it.
 */
export class WebSocketFrameSource implements FrameSource {
  private readonly subscribers = new FrameSubscribers();
  private ignoredCount = 0;

  constructor(
    private readonly acknowledge: FrameAck,
    private readonly images: ImageDataFactory = browserImageData,
  ) {}

  /** Messages that weren't drawn: a bad header, a wrong length, or a frame too large. */
  get ignored(): number {
    return this.ignoredCount;
  }

  subscribe(onFrame: (frame: PreviewFrame) => void): () => void {
    return this.subscribers.add(onFrame);
  }

  /** Handles one binary message from the server. */
  receive(message: ArrayBuffer): void {
    const result = parseFrameMessage(message);
    if (!result.ok) {
      this.ignoredCount += 1;
      if (result.seq !== null) {
        this.acknowledge(result.seq);
      }
      return;
    }
    const { seq, width, height } = result.header;
    try {
      this.subscribers.publish(this.images.wrap(new Uint8ClampedArray(message, frameHeaderSize, width * height * 4), width, height));
    } finally {
      this.acknowledge(seq);
    }
  }
}
