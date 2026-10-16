import { act, fireEvent, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { flashWindowMs } from "../components/CaptureFlash/CaptureFlash.tsx";
import { cameraGuideFraction, type FrameSource } from "../components/LivePreview/index.ts";
import { names, photographsOf, renderPhotographs, stubClient } from "./PhotographsTestKit.tsx";

function frames(width: number, height: number): FrameSource {
  return {
    subscribe(onFrame) {
      onFrame({ width, height, draw: () => undefined });
      return () => undefined;
    },
  };
}

function stubReducedMotion(reduce: boolean): void {
  vi.stubGlobal(
    "matchMedia",
    vi.fn((query: string) => ({
      matches: reduce && query === "(prefers-reduced-motion: reduce)",
      media: query,
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
    })),
  );
}

function live(container: HTMLElement): readonly string[] {
  return [...container.querySelectorAll("[aria-live='polite']")].map((region) => region.textContent);
}

describe("CameraTab", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows the heading, the supporting line, the progress, and the shutter ready to take photographs", () => {
    const { container } = renderPhotographs(photographsOf("guest-cycle-photographs-camera"), stubClient());

    expect(screen.getByRole("heading", { level: 1, name: "Find your light." })).toBeVisible();
    expect(screen.getByText("Let this moment become a memory.")).toBeVisible();
    expect(screen.getByRole("progressbar", { name: "Photographs" })).toHaveAttribute("aria-valuetext", "0 of 4");
    expect(screen.getByRole("button", { name: "Take photographs" })).toHaveAttribute("aria-disabled", "false");
    expect(container.querySelector("[data-photographs-tab='camera']")).toHaveAttribute("data-sequence-running", "false");
    expect(screen.queryByRole("button", { name: /Review/i })).toBeNull();
  });

  it("starts the sequence from the shutter, and stops it from the same control while it runs", () => {
    const stub = stubClient();
    const view = renderPhotographs(photographsOf("guest-cycle-photographs-camera"), stub);

    fireEvent.click(screen.getByRole("button", { name: "Take photographs" }));
    view.show(photographsOf("guest-cycle-photographs-countdown"));
    fireEvent.click(screen.getByRole("button", { name: "Stop taking photographs" }));

    expect(stub.sent).toEqual([
      { name: "TakePhotographs", payload: {} },
      { name: "StopCapture", payload: {} },
    ]);
  });

  it("keeps the shutter focusable but silent, with a reason beside it, until the brain says photographs can be taken", () => {
    const stub = stubClient();
    renderPhotographs(photographsOf("guest-ui-camera-waiting"), stub);

    const shutter = screen.getByRole("button", { name: "Take photographs" });
    expect(shutter).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(shutter);

    expect(stub.sent).toEqual([]);
    expect(screen.getByText("One moment. The camera is getting ready.")).toBeVisible();
  });

  it("offers a way to the Arrange tab only once the brain allows a review", () => {
    const stub = stubClient();
    const view = renderPhotographs(photographsOf("guest-ui-camera-stopped"), stub);

    fireEvent.click(screen.getByRole("button", { name: "SEE MY PHOTOGRAPHS" }));
    expect(names(stub.sent)).toEqual(["ShowArrangeTab"]);

    view.show(photographsOf("guest-ui-camera-stopped", { canReview: false }));
    expect(screen.queryByRole("button", { name: "SEE MY PHOTOGRAPHS" })).toBeNull();
  });

  it("draws the crop guide for the next window's ratio on the camera's own frame", () => {
    const { container } = renderPhotographs(
      photographsOf("guest-cycle-photographs-camera", { cropAspect: 0.75 }),
      stubClient({ frames: frames(1280, 720) }),
    );

    const guide = container.querySelector<HTMLElement>("[data-fotohavn-crop-guide]");
    const expected = cameraGuideFraction(1280, 720, 0.75);
    expect(guide).not.toBeNull();
    expect(expected).not.toBeNull();
    // The 1280 × 720 frame is first cropped to its centered 4:3 (960 × 720); the 3:4 window is 540 of those 960 pixels wide.
    expect(guide?.style.width).toBe("42.1875%");
    expect(guide?.style.left).toBe("28.90625%");
    expect(guide?.style.top).toBe("0%");
    expect(guide?.style.height).toBe("100%");
  });

  it("draws the landscape window's guide as the full 4:3 crop", () => {
    const { container } = renderPhotographs(
      photographsOf("guest-cycle-photographs-camera", { cropAspect: 600 / 450 }),
      stubClient({ frames: frames(1280, 720) }),
    );

    const guide = container.querySelector<HTMLElement>("[data-fotohavn-crop-guide]");
    expect(guide?.style.width).toBe("75%");
    expect(guide?.style.left).toBe("12.5%");
  });

  it("mirrors the live picture only when the Event does", () => {
    const view = renderPhotographs(photographsOf("guest-cycle-photographs-camera", { mirrored: true }), stubClient({ frames: frames(640, 480) }));
    expect(document.querySelector("canvas")).toHaveAttribute("data-mirrored", "true");

    view.show(photographsOf("guest-cycle-photographs-camera", { mirrored: false }));
    expect(document.querySelector("canvas")).not.toHaveAttribute("data-mirrored");
  });

  it("waits quietly for the camera when no frames arrive", () => {
    renderPhotographs(photographsOf("guest-cycle-photographs-camera"), stubClient());

    expect(screen.getByText("Getting the camera ready.")).toBeVisible();
    expect(document.querySelector("[data-fotohavn-crop-guide]")).toBeNull();
  });

  it("shows each countdown number the brain pushes and announces the countdown once, when it starts", () => {
    const { container, show } = renderPhotographs(photographsOf("guest-cycle-photographs-countdown"), stubClient());
    const announcements = () => live(container).filter((text) => text !== "");

    expect(screen.getByText("4")).toBeVisible();
    expect(announcements()).toContain("Next photograph in 4 seconds.");

    for (const remaining of [3, 2, 1]) {
      show(photographsOf("guest-cycle-photographs-countdown", { sequence: { ...photographsOf("guest-cycle-photographs-countdown").sequence, countdownRemaining: remaining } }));
      expect(screen.getByText(String(remaining))).toBeVisible();
    }
    expect(announcements().filter((text) => text.startsWith("Next photograph"))).toEqual(["Next photograph in 4 seconds."]);

    show(photographsOf("guest-cycle-photographs-pausing"));
    expect(screen.queryByText("1")).toBeNull();
  });

  it("announces a photograph taken once, never on first render", () => {
    const { container, show } = renderPhotographs(photographsOf("guest-cycle-photographs-countdown"), stubClient());
    expect(live(container).join("")).not.toContain("Photograph 1 of 4 taken.");

    show(photographsOf("guest-ui-camera-stopped"));
    expect(live(container)).toContain("Photograph 2 of 4 taken.");
    show(photographsOf("guest-ui-camera-stopped"));
    expect(live(container).filter((text) => text === "Photograph 2 of 4 taken.")).toHaveLength(1);
  });

  describe("the capture flash", () => {
    const flash = (container: HTMLElement) => container.querySelector("[data-state][data-motion]");

    it("plays once for each increment of flashSequence, and not for the value it mounts with", () => {
      stubReducedMotion(false);
      const sequence = photographsOf("guest-cycle-photographs-pausing").sequence;
      const { container, show } = renderPhotographs(photographsOf("guest-cycle-photographs-pausing"), stubClient());
      expect(flash(container)).toHaveAttribute("data-state", "idle");

      show(photographsOf("guest-cycle-photographs-pausing", { sequence: { ...sequence, flashSequence: sequence.flashSequence + 1 } }));
      expect(flash(container)).toHaveAttribute("data-state", "flash");
      expect(flash(container)).toHaveAttribute("data-motion", "animated");

      act(() => {
        vi.advanceTimersByTime(flashWindowMs);
      });
      expect(flash(container)).toHaveAttribute("data-state", "idle");

      show(photographsOf("guest-cycle-photographs-pausing", { sequence: { ...sequence, flashSequence: sequence.flashSequence + 2 } }));
      expect(flash(container)).toHaveAttribute("data-state", "flash");

      // A Capture that lands while a flash still shows does not start another: never more than three flashes a second.
      show(photographsOf("guest-cycle-photographs-pausing", { sequence: { ...sequence, flashSequence: sequence.flashSequence + 3 } }));
      act(() => {
        vi.advanceTimersByTime(flashWindowMs);
      });
      expect(flash(container)).toHaveAttribute("data-state", "idle");
    });

    it("is a static change under reduced motion", () => {
      stubReducedMotion(true);
      const sequence = photographsOf("guest-cycle-photographs-pausing").sequence;
      const { container, show } = renderPhotographs(photographsOf("guest-cycle-photographs-pausing"), stubClient());

      show(photographsOf("guest-cycle-photographs-pausing", { sequence: { ...sequence, flashSequence: sequence.flashSequence + 1 } }));

      expect(flash(container)).toHaveAttribute("data-state", "flash");
      expect(flash(container)).toHaveAttribute("data-motion", "static");
    });
  });

  it("lets the guest keep the original while a Retake counts down, with STOP still on the shutter", () => {
    const stub = stubClient();
    renderPhotographs(photographsOf("guest-ui-camera-retaking"), stub);

    expect(screen.getByText("4")).toBeVisible();
    expect(screen.queryByRole("button", { name: "SEE MY PHOTOGRAPHS" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "KEEP ORIGINAL" }));
    fireEvent.click(screen.getByRole("button", { name: "Stop taking photographs" }));

    expect(stub.sent).toEqual([
      { name: "KeepOriginal", payload: {} },
      { name: "StopCapture", payload: {} },
    ]);
  });

  it("marks the pause between windows", () => {
    const { container } = renderPhotographs(photographsOf("guest-cycle-photographs-pausing"), stubClient());

    expect(container.querySelector("[data-photographs-tab='camera']")).toHaveAttribute("data-sequence-pausing", "true");
  });
});
