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
  /**
   * How the picture sits in the box. `contain` (default): the whole camera frame, letterboxed and centred, with the guide
   * drawn on it (Event setup, diagnostics). `capture`: the capture area (`guideRatio`) at the box's full height (or width,
   * if that is smaller), centred, with the picture scaled so the guide lands exactly on it and running on past it as far as
   * the camera reaches, the surround dimmed (the Camera tab). Without a `guideRatio` it falls back to `contain`.
   */
  readonly framing?: "contain" | "capture";
  /** Overlays above the picture and guide, such as the countdown numerals; with `capture`, laid over the capture area. */
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
 * crop guide for the target window's ratio. Until the first frame, it says the camera is getting ready. With
 * `framing="capture"` the capture area itself fills the box's height instead, and the picture around it is dimmed.
 */
export function LivePreview({
  source,
  guideRatio,
  mirrored = false,
  label = "Live camera preview",
  waitingMessage = "Getting the camera ready.",
  framing = "contain",
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
  const capture = framing === "capture" && guideRatio !== undefined && Number.isFinite(guideRatio) && guideRatio > 0;
  const geometry =
    frame === null
      ? undefined
      : ({
          "--frame-ratio": String(frame.width / frame.height),
          ...(capture && guide !== null ? { "--guide-fraction": String(guide.width) } : {}),
        } as CSSProperties);
  const waiting = (
    <p className={styles.waiting} role="status">
      {frame === null ? waitingMessage : ""}
    </p>
  );
  const picture = (
    <div className={styles.picture} role="img" aria-label={label} hidden={frame === null}>
      <canvas ref={canvasRef} className={styles.canvas} data-mirrored={mirrored || undefined} />
    </div>
  );

  if (capture) {
    return (
      <div
        className={cx(styles.preview, className)}
        data-tone="dark"
        data-state={frame === null ? "waiting" : "live"}
        data-framing="capture"
        style={{ "--capture-ratio": String(guideRatio) } as CSSProperties}
      >
        <div className={styles.frame} style={geometry}>
          {picture}
        </div>
        <div className={styles.area} data-fotohavn-crop-guide="" aria-hidden="true" />
        {children !== undefined && <div className={styles.areaOverlay}>{children}</div>}
        {waiting}
      </div>
    );
  }

  return (
    <div className={cx(styles.preview, className)} data-tone="dark" data-state={frame === null ? "waiting" : "live"}>
      <div className={styles.frame} style={geometry}>
        {picture}
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
      {waiting}
    </div>
  );
}
