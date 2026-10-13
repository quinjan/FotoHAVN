import { act, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { isDrawableFrame, type DrawableFrame, type FrameSource, type PreviewFrame } from "./frameSource.ts";
import { LivePreview } from "./LivePreview.tsx";
import { createPatternFrameSource } from "./patternFrameSource.ts";

/** A source the test pushes frames into. */
function manualSource() {
  let listener: ((frame: PreviewFrame) => void) | null = null;
  const unsubscribe = vi.fn(() => {
    listener = null;
  });
  const source: FrameSource = {
    subscribe(onFrame) {
      listener = onFrame;
      return unsubscribe;
    },
  };
  return {
    source,
    unsubscribe,
    emit: (frame: PreviewFrame) => {
      act(() => {
        listener?.(frame);
      });
    },
  };
}

function drawable(width: number, height: number) {
  const frame = { width, height, draw: vi.fn<(context: CanvasRenderingContext2D) => void>() };
  return frame satisfies DrawableFrame;
}

/** The 2D context methods the preview and the pattern use, recorded. */
function fakeContext() {
  const gradient = { addColorStop: vi.fn() };
  return {
    putImageData: vi.fn(),
    drawImage: vi.fn(),
    createLinearGradient: vi.fn(() => gradient),
    fillRect: vi.fn(),
    beginPath: vi.fn(),
    moveTo: vi.fn(),
    lineTo: vi.fn(),
    stroke: vi.fn(),
    arc: vi.fn(),
    ellipse: vi.fn(),
    fill: vi.fn(),
    fillStyle: "",
    strokeStyle: "",
    lineWidth: 0,
  };
}

let context: ReturnType<typeof fakeContext>;
let frames: FrameRequestCallback[];
let getContext: { mockImplementation: (implementation: () => CanvasRenderingContext2D | null) => unknown };

function paint(): void {
  const pending = frames;
  frames = [];
  for (const callback of pending) {
    callback(0);
  }
}

beforeEach(() => {
  context = fakeContext();
  frames = [];
  getContext = vi.spyOn(HTMLCanvasElement.prototype, "getContext");
  getContext.mockImplementation(() => context as unknown as CanvasRenderingContext2D);
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => frames.push(callback));
  vi.stubGlobal("cancelAnimationFrame", (handle: number) => {
    frames.splice(handle - 1, 1);
  });
});

afterEach(() => {
  vi.unstubAllGlobals();
});

function canvas(): HTMLCanvasElement {
  const element = document.querySelector("canvas");
  if (element === null) {
    throw new Error("No canvas.");
  }
  return element;
}

describe("LivePreview", () => {
  it("says the camera is getting ready until the first frame, on Dark Walnut", () => {
    const { source } = manualSource();
    const { container } = render(<LivePreview source={source} guideRatio={4 / 3} />);
    const preview = container.firstElementChild;
    expect(preview).toHaveAttribute("data-tone", "dark");
    expect(preview).toHaveAttribute("data-state", "waiting");
    expect(screen.getByRole("status")).toHaveTextContent("Getting the camera ready.");
    expect(canvas()).not.toBeVisible();
    expect(container.querySelector("[data-fotohavn-crop-guide]")).toBeNull();
  });

  it("draws the frame on the next paint and shows the crop guide for the window's ratio", () => {
    const { source, emit } = manualSource();
    const { container } = render(<LivePreview source={source} guideRatio={450 / 600} />);
    const frame = drawable(1920, 1080);
    emit(frame);

    expect(container.firstElementChild).toHaveAttribute("data-state", "live");
    expect(screen.getByRole("status")).toHaveTextContent("");
    expect(screen.getByRole("img", { name: "Live camera preview" })).toContainElement(canvas());
    expect(frame.draw).not.toHaveBeenCalled();
    paint();
    expect(frame.draw).toHaveBeenCalledWith(context);
    expect(canvas().width).toBe(1920);
    expect(canvas().height).toBe(1080);

    const box = canvas().parentElement?.parentElement;
    expect(box?.style.getPropertyValue("--frame-ratio")).toBe(String(1920 / 1080));
    const guide = container.querySelector<HTMLElement>("[data-fotohavn-crop-guide]");
    expect(guide).toHaveAttribute("aria-hidden", "true");
    // 1920 × 1080 normalizes to 1440 × 1080, then a centered 3:4 window, 810 × 1080.
    expect(parseFloat(guide?.style.width ?? "")).toBeCloseTo((810 / 1920) * 100, 6);
    expect(parseFloat(guide?.style.height ?? "")).toBeCloseTo(100, 6);
    expect(parseFloat(guide?.style.left ?? "")).toBeCloseTo((555 / 1920) * 100, 6);
    expect(parseFloat(guide?.style.top ?? "")).toBeCloseTo(0, 6);
  });

  it("draws only the latest frame when several arrive before a paint", () => {
    const { source, emit } = manualSource();
    render(<LivePreview source={source} />);
    const first = drawable(1280, 720);
    const second = drawable(1280, 720);
    emit(first);
    emit(second);
    expect(frames).toHaveLength(1);
    paint();
    expect(first.draw).not.toHaveBeenCalled();
    expect(second.draw).toHaveBeenCalledOnce();

    const third = drawable(1280, 720);
    emit(third);
    expect(frames).toHaveLength(1);
    paint();
    expect(third.draw).toHaveBeenCalledOnce();
  });

  it("puts raw pixels and draws bitmaps", () => {
    class FakeImageData {
      constructor(
        readonly width: number,
        readonly height: number,
      ) {}
    }
    vi.stubGlobal("ImageData", FakeImageData);
    const { source, emit } = manualSource();
    render(<LivePreview source={source} />);

    const pixels = new FakeImageData(640, 480) as unknown as ImageData;
    emit(pixels);
    paint();
    expect(context.putImageData).toHaveBeenCalledWith(pixels, 0, 0);
    expect(canvas().width).toBe(640);

    const bitmap = { width: 800, height: 600, close: vi.fn() } as unknown as ImageBitmap;
    emit(bitmap);
    paint();
    expect(context.drawImage).toHaveBeenCalledWith(bitmap, 0, 0);
    expect(canvas().width).toBe(800);
    expect(canvas().height).toBe(600);
  });

  it("follows a change of frame size, even in one dimension", () => {
    const { source, emit } = manualSource();
    render(<LivePreview source={source} />);
    emit(drawable(1280, 720));
    paint();
    emit(drawable(1280, 960));
    paint();
    expect(canvas().height).toBe(960);
    expect(canvas().parentElement?.parentElement?.style.getPropertyValue("--frame-ratio")).toBe(String(1280 / 960));
  });

  it("treats a frame as a bitmap where ImageData doesn't exist", () => {
    vi.stubGlobal("ImageData", undefined);
    const { source, emit } = manualSource();
    render(<LivePreview source={source} />);
    const bitmap = { width: 800, height: 600 } as unknown as ImageBitmap;
    emit(bitmap);
    paint();
    expect(context.drawImage).toHaveBeenCalledWith(bitmap, 0, 0);
  });

  it("skips drawing without a 2D context", () => {
    getContext.mockImplementation(() => null);
    const { source, emit } = manualSource();
    render(<LivePreview source={source} />);
    const frame = drawable(1280, 720);
    emit(frame);
    expect(() => {
      paint();
    }).not.toThrow();
    expect(frame.draw).not.toHaveBeenCalled();
  });

  it("draws at once where animation frames don't exist", () => {
    vi.stubGlobal("requestAnimationFrame", undefined);
    const { source, emit } = manualSource();
    render(<LivePreview source={source} />);
    const frame = drawable(1280, 720);
    emit(frame);
    expect(frame.draw).toHaveBeenCalledOnce();
    const next = drawable(1280, 720);
    emit(next);
    expect(next.draw).toHaveBeenCalledOnce();
  });

  it("draws nothing for a paint that lands after it unmounts", () => {
    vi.stubGlobal("cancelAnimationFrame", () => undefined);
    const { source, emit } = manualSource();
    const { unmount } = render(<LivePreview source={source} />);
    const frame = drawable(1280, 720);
    emit(frame);
    unmount();
    expect(() => {
      paint();
    }).not.toThrow();
    expect(frame.draw).not.toHaveBeenCalled();
  });

  it("mirrors by CSS only when asked", () => {
    const { source } = manualSource();
    const { rerender } = render(<LivePreview source={source} />);
    expect(canvas()).not.toHaveAttribute("data-mirrored");
    rerender(<LivePreview source={source} mirrored />);
    expect(canvas()).toHaveAttribute("data-mirrored", "true");
  });

  it("has no guide without a ratio, and none for an unusable one", () => {
    const { source, emit } = manualSource();
    const { container, rerender } = render(<LivePreview source={source} />);
    emit(drawable(1280, 720));
    expect(container.querySelector("[data-fotohavn-crop-guide]")).toBeNull();
    rerender(<LivePreview source={source} guideRatio={0} />);
    expect(container.querySelector("[data-fotohavn-crop-guide]")).toBeNull();
  });

  it("stops delivery and drops a pending paint when unmounted", () => {
    const { source, unsubscribe, emit } = manualSource();
    const { unmount } = render(<LivePreview source={source} />);
    const frame = drawable(1280, 720);
    emit(frame);
    expect(frames).toHaveLength(1);
    unmount();
    expect(unsubscribe).toHaveBeenCalledOnce();
    expect(frames).toHaveLength(0);
    expect(frame.draw).not.toHaveBeenCalled();
  });

  it("waits again for a new source's first frame", () => {
    const first = manualSource();
    const second = manualSource();
    const { container, rerender } = render(<LivePreview source={first.source} />);
    first.emit(drawable(1280, 720));
    expect(container.firstElementChild).toHaveAttribute("data-state", "live");
    rerender(<LivePreview source={second.source} />);
    expect(first.unsubscribe).toHaveBeenCalledOnce();
    expect(container.firstElementChild).toHaveAttribute("data-state", "waiting");
    second.emit(drawable(1280, 720));
    expect(container.firstElementChild).toHaveAttribute("data-state", "live");
  });

  it("takes a label, a waiting message, overlays, and a layout class", () => {
    const { source } = manualSource();
    const { container } = render(
      <LivePreview source={source} label="Camera preview for this Event" waitingMessage="Checking the camera." className="stage">
        <span>3</span>
      </LivePreview>,
    );
    expect(canvas().parentElement).toHaveAttribute("aria-label", "Camera preview for this Event");
    expect(screen.getByRole("status")).toHaveTextContent("Checking the camera.");
    expect(screen.getByText("3")).toBeInTheDocument();
    expect(container.firstElementChild?.className).toContain("stage");
  });
});

describe("the pattern frame source", () => {
  it("delivers one deterministic frame at once and draws a whole picture", () => {
    const onFrame = vi.fn();
    const stop = createPatternFrameSource(1280, 720).subscribe(onFrame);
    expect(onFrame).toHaveBeenCalledOnce();
    const frame = onFrame.mock.calls[0]?.[0] as PreviewFrame;
    expect(isDrawableFrame(frame)).toBe(true);
    expect(frame.width).toBe(1280);
    expect(frame.height).toBe(720);
    if (isDrawableFrame(frame)) {
      frame.draw(context as unknown as CanvasRenderingContext2D);
    }
    expect(context.fillRect).toHaveBeenCalledWith(0, 0, 1280, 720);
    expect(context.stroke).toHaveBeenCalledTimes(2);
    expect(context.fill).toHaveBeenCalledTimes(5);
    expect(stop).not.toThrow();
  });

  it("defaults to a 1920 × 1080 frame", () => {
    const onFrame = vi.fn();
    createPatternFrameSource().subscribe(onFrame);
    expect(onFrame).toHaveBeenCalledWith(expect.objectContaining({ width: 1920, height: 1080 }));
  });

  it("is told apart from bitmaps and pixels", () => {
    expect(isDrawableFrame({ width: 1, height: 1 } as unknown as ImageBitmap)).toBe(false);
  });
});
