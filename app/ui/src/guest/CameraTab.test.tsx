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

function start(): HTMLElement {
  return screen.getByRole("button", { name: "Tap to start taking photographs" });
}

describe("CameraTab", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("is all live preview: a hidden heading, the count, and the whole preview one button ready to take photographs", () => {
    const { container } = renderPhotographs(photographsOf("guest-cycle-photographs-camera"), stubClient());

    const heading = screen.getByRole("heading", { level: 1, name: "Find your light." });
    expect(heading.className).toContain("visuallyHidden");
    expect(screen.queryByText("Let this moment become a memory.")).toBeNull();
    expect(screen.getByRole("progressbar", { name: "Photographs" })).toHaveAttribute("aria-valuetext", "0 of 4");
    expect(start()).toHaveAttribute("aria-disabled", "false");
    expect(start()).toHaveAttribute("data-state", "ready");
    expect(start()).toHaveTextContent("TAP TO START");
    // Label in name (WCAG 2.5.3): the accessible name begins with the words on screen.
    expect(start().getAttribute("aria-label")?.toUpperCase().startsWith("TAP TO START")).toBe(true);
    expect(container.querySelector("[data-photographs-tab='camera']")).toHaveAttribute("data-sequence-running", "false");
    expect(screen.queryByRole("button", { name: "SEE MY PHOTOGRAPHS" })).toBeNull();
    expect(screen.queryByRole("button", { name: /stop/i })).toBeNull();
    expect(screen.getAllByRole("button")).toHaveLength(1);
  });

  it("starts the sequence with a tap anywhere on the preview, and then ignores taps: there is no stop", () => {
    const stub = stubClient();
    const view = renderPhotographs(photographsOf("guest-cycle-photographs-camera"), stub);

    fireEvent.click(start());
    view.show(photographsOf("guest-cycle-photographs-countdown"));
    expect(start()).toHaveAttribute("aria-disabled", "true");
    expect(start()).toHaveAttribute("data-state", "running");
    expect(start()).not.toHaveTextContent("TAP TO START");
    fireEvent.click(start());

    expect(stub.sent).toEqual([{ name: "TakePhotographs", payload: {} }]);
  });

  it("is a real button, so the keyboard reaches it and Enter or Space press it", () => {
    renderPhotographs(photographsOf("guest-cycle-photographs-camera"), stubClient());
    expect(start().tagName).toBe("BUTTON");
    expect(start()).toHaveAttribute("type", "button");
    start().focus();
    expect(start()).toHaveFocus();
  });

  it("reads as unavailable, with the reason as its description, until the brain says photographs can be taken", () => {
    const stub = stubClient();
    renderPhotographs(photographsOf("guest-ui-camera-waiting"), stub);

    expect(start()).toHaveAttribute("aria-disabled", "true");
    expect(start()).toHaveAttribute("data-state", "waiting");
    expect(start()).toHaveAccessibleDescription("One moment. The camera is getting ready.");
    expect(start()).not.toHaveTextContent("TAP TO START");
    fireEvent.click(start());

    expect(stub.sent).toEqual([]);
    expect(screen.getByText("One moment. The camera is getting ready.")).toBeVisible();
  });

  it("offers a way to the Arrange tab only once the brain allows a review, without starting the sequence", () => {
    const stub = stubClient();
    const view = renderPhotographs(photographsOf("guest-ui-camera-partial"), stub);

    fireEvent.click(screen.getByRole("button", { name: "SEE MY PHOTOGRAPHS" }));
    expect(names(stub.sent)).toEqual(["ShowArrangeTab"]);

    view.show(photographsOf("guest-ui-camera-partial", { canReview: false }));
    expect(screen.queryByRole("button", { name: "SEE MY PHOTOGRAPHS" })).toBeNull();
  });

  it("lays the capture area over the camera's frame so the window's guide lands exactly on it", () => {
    const { container } = renderPhotographs(
      photographsOf("guest-cycle-photographs-camera", { cropAspect: 0.75 }),
      stubClient({ frames: frames(1280, 720) }),
    );

    const preview = container.querySelector<HTMLElement>("[data-framing='capture']");
    const area = container.querySelector<HTMLElement>("[data-fotohavn-crop-guide]");
    const frame = area?.previousElementSibling as HTMLElement | null | undefined;
    expect(preview?.style.getPropertyValue("--capture-ratio")).toBe("0.75");
    expect(area).not.toBeNull();
    // The 1280 × 720 frame is first cropped to its centered 4:3 (960 × 720); the 3:4 window is 540 of those 960 pixels wide.
    expect(cameraGuideFraction(1280, 720, 0.75)?.width).toBe(0.421875);
    expect(frame?.style.getPropertyValue("--guide-fraction")).toBe("0.421875");
    expect(frame?.style.getPropertyValue("--frame-ratio")).toBe(String(1280 / 720));
  });

  it("scales the frame for a 4:3 window by the full 4:3 crop", () => {
    const { container } = renderPhotographs(
      photographsOf("guest-cycle-photographs-camera", { cropAspect: 600 / 450 }),
      stubClient({ frames: frames(1280, 720) }),
    );

    const area = container.querySelector<HTMLElement>("[data-fotohavn-crop-guide]");
    const frame = area?.previousElementSibling as HTMLElement | null | undefined;
    expect(frame?.style.getPropertyValue("--guide-fraction")).toBe("0.75");
  });

  it("mirrors the live picture only when the Event does", () => {
    const view = renderPhotographs(photographsOf("guest-cycle-photographs-camera", { mirrored: true }), stubClient({ frames: frames(640, 480) }));
    expect(document.querySelector("canvas")).toHaveAttribute("data-mirrored", "true");

    view.show(photographsOf("guest-cycle-photographs-camera", { mirrored: false }));
    expect(document.querySelector("canvas")).not.toHaveAttribute("data-mirrored");
  });

  it("waits quietly for the camera when no frames arrive, with the capture area already drawn", () => {
    renderPhotographs(photographsOf("guest-cycle-photographs-camera"), stubClient());

    expect(screen.getByText("Getting the camera ready.")).toBeVisible();
    expect(document.querySelector("[data-fotohavn-crop-guide]")).not.toBeNull();
    expect(document.querySelector("[data-framing='capture']")).toHaveAttribute("data-state", "waiting");
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

    show(photographsOf("guest-ui-camera-partial"));
    expect(live(container)).toContain("Photograph 2 of 4 taken.");
    show(photographsOf("guest-ui-camera-partial"));
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

  it("lets the guest keep the original while a Retake counts down, and taps on the preview do nothing then", () => {
    const stub = stubClient();
    renderPhotographs(photographsOf("guest-ui-camera-retaking"), stub);

    expect(screen.getByText("4")).toBeVisible();
    expect(screen.queryByRole("button", { name: "SEE MY PHOTOGRAPHS" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "KEEP ORIGINAL" }));
    fireEvent.click(start());

    expect(stub.sent).toEqual([{ name: "KeepOriginal", payload: {} }]);
  });

  it("starts a Retake with the same tap, KEEP ORIGINAL beside it", () => {
    const stub = stubClient();
    renderPhotographs(photographsOf("guest-ui-camera-retaking-ready"), stub);

    expect(screen.getByRole("button", { name: "KEEP ORIGINAL" })).toBeVisible();
    expect(start()).toHaveTextContent("TAP TO START");
    fireEvent.click(start());

    expect(stub.sent).toEqual([{ name: "TakePhotographs", payload: {} }]);
  });

  it("marks the pause between windows", () => {
    const { container } = renderPhotographs(photographsOf("guest-cycle-photographs-pausing"), stubClient());

    expect(container.querySelector("[data-photographs-tab='camera']")).toHaveAttribute("data-sequence-pausing", "true");
  });
});
