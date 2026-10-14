import type { FrameSource, PreviewFrame } from "../components/LivePreview/index.ts";
import type { PreviewFrameMetadata } from "../protocol/generated.ts";
import {
  browserImageData,
  FrameSubscribers,
  isFrameDimension,
  isWireSequence,
  type FrameAck,
  type ImageDataFactory,
} from "./frameSupport.ts";

/** What the page needs from a `sharedbufferreceived` message's `additionalData`. */
interface ReceivedFrame {
  readonly seq: number;
  /** Null when the metadata describes a frame this page won't draw (a format or size it doesn't expect). */
  readonly size: { readonly width: number; readonly height: number } | null;
}

function parseAdditionalData(data: unknown): unknown {
  if (typeof data !== "string") {
    return data;
  }
  try {
    return JSON.parse(data);
  } catch {
    return null;
  }
}

/**
 * Reads the host's `PreviewFrameMetadata`. A frame whose sequence can't be read is null: nothing identifies it, so the
 * page can neither draw nor acknowledge it.
 */
function readFrame(data: unknown): ReceivedFrame | null {
  const parsed = parseAdditionalData(data);
  if (typeof parsed !== "object" || parsed === null) {
    return null;
  }
  const meta = parsed as Partial<Record<keyof PreviewFrameMetadata, unknown>>;
  if (meta.type !== "previewFrame" || !isWireSequence(meta.seq)) {
    return null;
  }
  const { format, width, height } = meta;
  const size = format === "rgba" && isFrameDimension(width) && isFrameDimension(height) ? { width, height } : null;
  return { seq: meta.seq, size };
}

/**
 * The WPF host's live preview frames (contract §6, "WebView2"): the host posts a reused `CoreWebView2SharedBuffer`
 * of raw RGBA pixels with `PostSharedBufferToScript`, and holds one frame in flight until the page acknowledges it.
 *
 * Each frame is copied once into an `ImageData` the source reuses, so the shared buffer can go back at once; then
 * `releaseBuffer`, then `frameAck`. A subscriber (the `LivePreview`) keeps only the newest frame and draws it on the
 * next animation frame, so a frame delivered before that paint is overwritten in place, never queued. The source
 * listens from `start()` whether or not anyone subscribes, so a buffer is always released and acknowledged.
 *
 * A frame with metadata the page doesn't expect (not `rgba`, a size that is zero or too large, a buffer too short for
 * its size) isn't drawn, but it is still released and acknowledged, so the host never waits on it.
 */
export class WebView2FrameSource implements FrameSource {
  private readonly subscribers = new FrameSubscribers();
  private handler: ((event: WebView2SharedBufferEvent) => void) | null = null;
  private image: ImageData | null = null;
  private ignoredCount = 0;

  constructor(
    private readonly webview: WebView2Bridge,
    private readonly acknowledge: FrameAck,
    private readonly images: ImageDataFactory = browserImageData,
  ) {}

  /** Frames released and acknowledged without being drawn. */
  get ignored(): number {
    return this.ignoredCount;
  }

  /** Starts listening for shared buffers; does nothing when already started. */
  start(): void {
    if (this.handler !== null) {
      return;
    }
    this.handler = (event) => {
      this.receive(event);
    };
    this.webview.addEventListener("sharedbufferreceived", this.handler);
  }

  stop(): void {
    if (this.handler !== null) {
      this.webview.removeEventListener("sharedbufferreceived", this.handler);
      this.handler = null;
    }
    this.image = null;
  }

  subscribe(onFrame: (frame: PreviewFrame) => void): () => void {
    return this.subscribers.add(onFrame);
  }

  private receive(event: WebView2SharedBufferEvent): void {
    const buffer = event.getBuffer();
    const frame = readFrame(event.additionalData);
    const size = frame?.size ?? null;
    try {
      if (size === null || buffer.byteLength < size.width * size.height * 4) {
        this.ignoredCount += 1;
      } else {
        this.subscribers.publish(this.copyInto(buffer, size.width, size.height));
      }
    } finally {
      this.webview.releaseBuffer(buffer);
      if (frame !== null) {
        this.acknowledge(frame.seq);
      }
    }
  }

  private copyInto(buffer: ArrayBuffer, width: number, height: number): ImageData {
    let image = this.image;
    if (image?.width !== width || image.height !== height) {
      image = this.images.create(width, height);
      this.image = image;
    }
    image.data.set(new Uint8ClampedArray(buffer, 0, width * height * 4));
    return image;
  }
}
