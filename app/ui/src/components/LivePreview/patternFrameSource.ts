import type { DrawableFrame, FrameSource } from "./frameSource.ts";

/**
 * A synthetic in-memory frame source: one deterministic test pattern, delivered as soon as a preview subscribes,
 * with no timers. The fixtures and tests use it; the headless host's fake camera plays the same role end to end.
 * The colors are picture content, not interface color.
 */
export function createPatternFrameSource(width = 1920, height = 1080): FrameSource {
  const frame: DrawableFrame = {
    width,
    height,
    draw(context) {
      const backdrop = context.createLinearGradient(0, 0, 0, height);
      backdrop.addColorStop(0, "rgb(206 190 168)");
      backdrop.addColorStop(1, "rgb(120 98 80)");
      context.fillStyle = backdrop;
      context.fillRect(0, 0, width, height);

      // A thirds grid, so framing and the crop guide read clearly.
      context.strokeStyle = "rgb(250 244 234 / 35%)";
      context.lineWidth = Math.max(1, Math.round(width / 640));
      for (let n = 1; n < 3; n += 1) {
        context.beginPath();
        context.moveTo((width * n) / 3, 0);
        context.lineTo((width * n) / 3, height);
        context.moveTo(0, (height * n) / 3);
        context.lineTo(width, (height * n) / 3);
        context.stroke();
      }

      // Two figures, left of center, so mirroring shows.
      context.fillStyle = "rgb(62 46 38)";
      const unit = Math.min(width, height);
      for (const [at, lift] of [
        [0.38, 0],
        [0.55, 0.04],
      ] as const) {
        const x = width * at;
        const y = height * (0.42 + lift);
        context.beginPath();
        context.arc(x, y, unit * 0.09, 0, Math.PI * 2);
        context.fill();
        context.beginPath();
        context.ellipse(x, y + unit * 0.36, unit * 0.17, unit * 0.24, 0, 0, Math.PI * 2);
        context.fill();
      }

      // A light in the top-left corner: the asymmetric mark.
      context.fillStyle = "rgb(255 250 240 / 70%)";
      context.beginPath();
      context.arc(width * 0.1, height * 0.16, unit * 0.06, 0, Math.PI * 2);
      context.fill();
    },
  };
  return {
    subscribe(onFrame) {
      onFrame(frame);
      return () => undefined;
    },
  };
}
