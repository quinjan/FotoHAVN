import { useEffect, useRef, useState, type CSSProperties, type ReactNode } from "react";
import { cx } from "../shared/cx.ts";
import { cameraGuideFraction } from "./cameraGeometry.ts";
import { isDrawableFrame, type FrameSource, type PreviewFrame } from "./frameSource.ts";
import styles from "./LivePreview.module.css";

export interface LivePreviewProps {
  /** Where frames come from: the WebView2 shared buffer, the headless WebSocket, or a fixture's pattern. */
  readonly source: FrameSource;
  /**
   * The target photograph window's ratio (width / height), for the crop guide, e.g. 600 / 450 for Signature Strip.
   * No guide when omitted.
   */
  readonly guideRatio?: number;
  /** Mirrors the picture by CSS, as the Event's mirror setting says; the frames themselves are never flipped. */
  readonly mirrored?: boolean;
  /** The canvas's accessible name. Default "Live camera preview". */
  readonly label?: string;
  /** Shown until the first frame arrives. Default "Getting the camera ready." */
  readonly waitingMessage?: string;
  /** Overlays above the picture and guide, such as the countdown numerals. */
  readonly children?: ReactNode;
  readonly className?: string;
}

interface FrameSize {
  readonly source: FrameSource;
  readonly width: number;
  readonly height: number;
}

function drawFrame(canvas: HTMLCanvasElement, frame: PreviewFrame): void {
  if (canvas.width !== frame.width || canvas.height !== frame.height) {
    canvas.width = frame.width;
    canvas.height = frame.height;
  }
  const context = canvas.getContext("2d");
  if (context === null) {
    return;
  }
  if (isDrawableFrame(frame)) {
    frame.draw(context);
  } else if (typeof ImageData !== "undefined" && frame instanceof ImageData) {
    context.putImageData(frame, 0, 0);
  } else {
    context.drawImage(frame as ImageBitmap, 0, 0);
  }
}

function schedule(callback: () => void): () => void {
  if (typeof requestAnimationFrame !== "function") {
    callback();
    return () => undefined;
  }
  const handle = requestAnimationFrame(callback);
  return () => {
    cancelAnimationFrame(handle);
  };
}

/**
 * The live camera preview (DESIGN.md, Photographs: Camera; Event setup): a canvas on Dark Walnut that draws only
 * the latest frame from its `FrameSource`, contained at the frame's proportion, mirrored by CSS when asked, with the
 * crop guide for the target window's ratio. Until the first frame, it says the camera is getting ready.
 */
export function LivePreview({
  source,
  guideRatio,
  mirrored = false,
  label = "Live camera preview",
  waitingMessage = "Getting the camera ready.",
  children,
  className,
}: LivePreviewProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);
  const [size, setSize] = useState<FrameSize | null>(null);

  useEffect(() => {
    let latest: PreviewFrame | null = null;
    let pending = false;
    let cancel: () => void = () => undefined;
    let known: { width: number; height: number } | null = null;
    const draw = () => {
      pending = false;
      const frame = latest;
      latest = null;
      const canvas = canvasRef.current;
      if (frame !== null && canvas !== null) {
        drawFrame(canvas, frame);
      }
    };
    const unsubscribe = source.subscribe((frame) => {
      // Only the newest frame is kept; one waiting for the next paint is replaced, never queued.
      latest = frame;
      if (known?.width !== frame.width || known.height !== frame.height) {
        known = { width: frame.width, height: frame.height };
        setSize({ source, width: frame.width, height: frame.height });
      }
      if (!pending) {
        pending = true;
        cancel = schedule(draw);
      }
    });
    return () => {
      unsubscribe();
      cancel();
      latest = null;
    };
  }, [source]);

  const frame = size !== null && size.source === source ? size : null;
  const guide = frame !== null && guideRatio !== undefined ? cameraGuideFraction(frame.width, frame.height, guideRatio) : null;
  const geometry =
    frame === null ? undefined : ({ "--frame-ratio": String(frame.width / frame.height) } as CSSProperties);

  return (
    <div className={cx(styles.preview, className)} data-tone="dark" data-state={frame === null ? "waiting" : "live"}>
      <div className={styles.frame} style={geometry}>
        <div className={styles.picture} role="img" aria-label={label} hidden={frame === null}>
          <canvas ref={canvasRef} className={styles.canvas} data-mirrored={mirrored || undefined} />
        </div>
        {guide !== null && (
          <div
            className={styles.guide}
            data-fotohavn-crop-guide=""
            aria-hidden="true"
            style={{
              left: `${String(guide.left * 100)}%`,
              top: `${String(guide.top * 100)}%`,
              width: `${String(guide.width * 100)}%`,
              height: `${String(guide.height * 100)}%`,
            }}
          />
        )}
        {children !== undefined && <div className={styles.overlay}>{children}</div>}
      </div>
      <p className={styles.waiting} role="status">
        {frame === null ? waitingMessage : ""}
      </p>
    </div>
  );
}
