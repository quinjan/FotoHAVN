/**
 * Builds the `ImageData` a preview frame is delivered as. The browser's `ImageData` is the default; tests and any
 * environment without one substitute their own.
 */
export interface ImageDataFactory {
  /** A blank frame the source then fills, and reuses while the size stays the same. */
  create(width: number, height: number): ImageData;
  /** Wraps pixels the source owns, with no copy. `pixels.length` must be `width * height * 4`. */
  wrap(pixels: Uint8ClampedArray<ArrayBuffer>, width: number, height: number): ImageData;
}

export const browserImageData: ImageDataFactory = {
  create: (width, height) => new ImageData(width, height),
  wrap: (pixels, width, height) => new ImageData(pixels, width, height),
};

/** The largest frame edge a source accepts; the hosts send at most 1280 px wide, so anything bigger is a mistake. */
export const maxFrameDimension = 4096;

/** The wire sequence is the brain's 32-bit counter (`PreviewFrameHeader.ToWireSequence`). */
export const maxWireSequence = 0xffff_ffff;

export function isWireSequence(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= 0 && value <= maxWireSequence;
}

export function isFrameDimension(value: unknown): value is number {
  return typeof value === "number" && Number.isInteger(value) && value >= 1 && value <= maxFrameDimension;
}

/** Sends `frameAck` for a frame the page has finished with, through the bridge's own transport. */
export type FrameAck = (seq: number) => void;

/** The listeners of a frame source: frames go to each, and the source keeps no frame of its own. */
export class FrameSubscribers {
  private readonly listeners = new Set<(frame: ImageData) => void>();

  add(listener: (frame: ImageData) => void): () => void {
    // A wrapper per subscription, so subscribing the same function twice gives two independent subscriptions.
    const entry = (frame: ImageData): void => {
      listener(frame);
    };
    this.listeners.add(entry);
    return () => {
      this.listeners.delete(entry);
    };
  }

  publish(frame: ImageData): void {
    for (const listener of [...this.listeners]) {
      listener(frame);
    }
  }
}
