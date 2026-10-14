/**
 * Where the live preview's frames come from. M5 implements it twice: the WebView2 host's shared buffer
 * (`PostSharedBufferToScript`) and the headless host's binary WebSocket messages (implementation plan, Live preview
 * frames). `LivePreview` only subscribes and draws; it never reaches the camera.
 */

/** A frame the source draws itself, e.g. decoded from a shared buffer it reuses. */
export interface DrawableFrame {
  readonly width: number;
  readonly height: number;
  /** Paints the whole frame into a canvas of exactly `width` × `height` pixels. */
  draw(context: CanvasRenderingContext2D): void;
}

/** One preview frame: a decoded bitmap, raw RGBA pixels, or a frame that draws itself. */
export type PreviewFrame = ImageBitmap | ImageData | DrawableFrame;

/**
 * A stream of preview frames. `subscribe` calls `onFrame` for each new frame (possibly synchronously, with a frame
 * already at hand) and returns the function that stops delivery. The source owns each frame: `LivePreview` draws the
 * latest one on the next animation frame and keeps no other, so a source may reuse or close a frame once it delivers
 * the next.
 */
export interface FrameSource {
  subscribe(onFrame: (frame: PreviewFrame) => void): () => void;
}

/** True for a frame that draws itself. */
export function isDrawableFrame(frame: PreviewFrame): frame is DrawableFrame {
  return "draw" in frame && typeof frame.draw === "function";
}

/** A camera that has delivered nothing: for a preview with no picture to show, and a screen with no camera yet. */
export const silentFrameSource: FrameSource = { subscribe: () => () => undefined };
