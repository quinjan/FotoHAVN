import { act, fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { CameraDiagnosticsView, CameraFailureKind } from "../../protocol/generated.ts";
import type { FrameSource, PreviewFrame } from "../LivePreview/index.ts";
import { CameraDiagnosticsScreen, type CameraDiagnosticsControls, type CameraDiagnosticsScreenProps } from "./CameraDiagnosticsScreen.tsx";
import { cameraReasonCopy } from "./cameraText.ts";

const integrated = { deviceId: "usb-1", name: "Integrated Webcam" };
const fake = { deviceId: "fake-camera", name: "Fake camera" };

const ready: CameraDiagnosticsView = {
  cameras: [integrated, fake],
  selectedDeviceId: integrated.deviceId,
  state: "ready",
  reason: null,
  format: { width: 1920, height: 1080, fps: 30 },
  mirrored: false,
  lastCapture: null,
  lastComposition: null,
};

function manualSource() {
  const listeners = new Set<(frame: PreviewFrame) => void>();
  const source: FrameSource = {
    subscribe(onFrame) {
      listeners.add(onFrame);
      return () => {
        listeners.delete(onFrame);
      };
    },
  };
  return {
    source,
    listeners,
    emit: () => {
      act(() => {
        for (const listener of listeners) {
          listener({ width: 1280, height: 720, draw: vi.fn() });
        }
      });
    },
  };
}

function controls(overrides: Partial<CameraDiagnosticsControls> = {}) {
  const value = { onSelect: vi.fn(), onMirrorChange: vi.fn(), onCapture: vi.fn(), ...overrides };
  return value satisfies CameraDiagnosticsControls;
}

function renderScreen(props: Partial<CameraDiagnosticsScreenProps> = {}) {
  const frames = manualSource();
  const result = render(
    <CameraDiagnosticsScreen view={ready} source={frames.source} measured={{ fps: 30, ageMs: 40 }} {...props} />,
  );
  return { ...result, ...frames };
}

const preview = () => document.querySelector("[data-state]");
const status = () => screen.getAllByRole("status").find((node) => node.textContent !== "" && node.closest("[data-status-tone]") !== null);

beforeEach(() => {
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
});

describe("CameraDiagnosticsScreen layout and copy", () => {
  it("names itself with the eyebrow and one level 1 heading", () => {
    renderScreen();
    expect(screen.getByText("CAMERA DIAGNOSTICS")).toBeVisible();
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Check the camera.");
  });

  it("shows the state, format, measured rate and age, and no capture yet", () => {
    renderScreen();
    expect(status()).toHaveTextContent("Ready");
    expect(screen.getByText("1920 × 1080 · 30 fps")).toBeVisible();
    expect(screen.getByText("30 fps", { selector: "[data-camera-measure='fps']" })).toBeVisible();
    expect(screen.getByText("Just now")).toBeVisible();
    expect(screen.getByText("None yet", { selector: "[data-camera-measure='capture']" })).toBeVisible();
    expect(screen.queryByText("Booth report")).not.toBeInTheDocument();
  });

  it("marks the root with the state and reason for tests", () => {
    const { container } = renderScreen({ view: { ...ready, state: "notAvailable", reason: "stale", format: null } });
    expect(container.firstElementChild).toHaveAttribute("data-camera-state", "notAvailable");
    expect(container.firstElementChild).toHaveAttribute("data-camera-reason", "stale");
  });

  it("shows dashes for a rate and an age it doesn't know yet", () => {
    renderScreen({ measured: { fps: null, ageMs: null }, view: { ...ready, format: null } });
    expect(screen.getAllByText("—")).toHaveLength(3);
  });

  it("describes the last capture", () => {
    renderScreen({
      view: {
        ...ready,
        lastCapture: { fileName: "capture-20261014-071910-123.jpg", width: 1920, height: 1080, byteLength: 412_345, mirrored: true },
      },
    });
    expect(screen.getByText("capture-20261014-071910-123.jpg")).toBeVisible();
    expect(screen.getByText("1920 × 1080 · 403 KB · Mirrored")).toBeVisible();
    expect(screen.queryByText("None yet", { selector: "[data-camera-measure='capture']" })).not.toBeInTheDocument();
  });

  it("shows the booth's own figures when it reports them, skipping what it doesn't know", () => {
    const first = renderScreen({ host: { deliveredFps: 29.6, droppedFrames: 2, frameAgeMs: 31.4 } });
    expect(screen.getByText("Delivered 29.6 fps · Dropped 2 · Camera frame age 31 ms")).toBeVisible();

    const props = { view: ready, source: first.source, measured: { fps: 30, ageMs: 40 } };
    first.rerender(<CameraDiagnosticsScreen {...props} host={{ deliveredFps: null, droppedFrames: 0, frameAgeMs: null }} />);
    expect(screen.getByText("Dropped 0")).toBeVisible();

    first.rerender(<CameraDiagnosticsScreen {...props} host={{ deliveredFps: null, droppedFrames: null, frameAgeMs: null }} />);
    expect(screen.getByText("—", { selector: "[data-camera-measure='host']" })).toBeVisible();
  });
});

describe("CameraDiagnosticsScreen states", () => {
  it("draws the live preview only while the camera is ready, mirrored by CSS when asked", () => {
    const { emit } = renderScreen({ view: { ...ready, mirrored: true } });
    expect(preview()).toHaveAttribute("data-state", "waiting");
    emit();
    expect(preview()).toHaveAttribute("data-state", "live");
    expect(document.querySelector("canvas")).toHaveAttribute("data-mirrored");
  });

  it("does not mirror the picture unless the view says so", () => {
    const { emit } = renderScreen();
    emit();
    expect(document.querySelector("canvas")).not.toHaveAttribute("data-mirrored");
  });

  it("while checking, says the camera is getting ready and shows no picture", () => {
    const { emit } = renderScreen({ view: { ...ready, state: "checking", format: null } });
    emit();
    expect(status()).toHaveTextContent("Checking");
    expect(status()?.closest("[data-status-tone]")).toHaveAttribute("data-status-tone", "neutral");
    expect(preview()).toHaveAttribute("data-state", "waiting");
    expect(screen.getByText("Getting the camera ready.")).toBeVisible();
  });

  it("with no camera chosen, asks for one", () => {
    renderScreen({ view: { ...ready, selectedDeviceId: null, state: "idle", format: null } });
    expect(status()).toHaveTextContent("Choose a camera.");
    expect(screen.getByText("Choose a camera to see it here.")).toBeVisible();
  });

  it("when another app holds the camera, says so in the alert tone and shows no picture", () => {
    const { emit } = renderScreen({ view: { ...ready, state: "inUseByAnotherApp", reason: "inUseByAnotherApp", format: null } });
    emit();
    expect(status()).toHaveTextContent("In use by another app");
    expect(status()?.closest("[data-status-tone]")).toHaveAttribute("data-status-tone", "alert");
    expect(preview()).toHaveAttribute("data-state", "waiting");
    expect(screen.getByText("No picture right now.")).toBeVisible();
  });

  it.each(Object.keys(cameraReasonCopy) as CameraFailureKind[])("says why a camera isn't available: %s", (reason) => {
    const { emit } = renderScreen({ view: { ...ready, state: "notAvailable", reason, format: null } });
    emit();
    expect(status()).toHaveTextContent(`Not available. ${cameraReasonCopy[reason]}`);
    expect(status()?.closest("[data-status-tone]")).toHaveAttribute("data-status-tone", "alert");
    expect(preview()).toHaveAttribute("data-state", "waiting");
    expect(screen.getByText("No picture right now.")).toBeVisible();
  });

  it("with no cameras, says so and offers no list or status line", () => {
    renderScreen({ view: { ...ready, cameras: [], selectedDeviceId: null, state: "idle", format: null } });
    expect(screen.getByText("No camera found.")).toBeVisible();
    expect(screen.getByText("Connect a camera and it will appear here.")).toBeVisible();
    expect(screen.getByText("No picture right now.")).toBeVisible();
    expect(screen.queryByText("Choose a camera.")).not.toBeInTheDocument();
    expect(screen.queryByRole("radio")).not.toBeInTheDocument();
  });

  it("when the chosen camera is the only one and it was disconnected, says why beside the empty list", () => {
    renderScreen({ view: { ...ready, cameras: [], state: "notAvailable", reason: "removed", format: null } });
    expect(screen.getByText("No camera found.")).toBeVisible();
    expect(status()).toHaveTextContent("Not available. The camera was disconnected.");
  });
});

describe("CameraDiagnosticsScreen camera list and controls", () => {
  it("lists the cameras with the selected one chosen, read-only without controls", () => {
    renderScreen();
    const radios = screen.getAllByRole("radio");
    expect(radios.map((radio) => radio.closest("label")?.textContent)).toEqual(expect.arrayContaining([expect.stringContaining("Integrated Webcam")]));
    expect(radios[0]).toBeChecked();
    for (const radio of radios) {
      expect(radio).toBeDisabled();
    }
    expect(screen.queryByRole("switch")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "CAPTURE" })).not.toBeInTheDocument();
  });

  it("chooses a camera through the controls", () => {
    const wired = controls();
    renderScreen({ controls: wired });
    fireEvent.click(screen.getByRole("radio", { name: /Fake camera/ }));
    expect(wired.onSelect).toHaveBeenCalledExactlyOnceWith("fake-camera");
  });

  it("flips the mirror through the controls", () => {
    const wired = controls();
    renderScreen({ controls: wired });
    const mirror = screen.getByRole("switch", { name: "Mirror" });
    expect(mirror).toHaveAttribute("aria-checked", "false");
    fireEvent.click(mirror);
    expect(wired.onMirrorChange).toHaveBeenCalledExactlyOnceWith(true);
  });

  it("shows the mirror the view says", () => {
    renderScreen({ controls: controls(), view: { ...ready, mirrored: true } });
    expect(screen.getByRole("switch", { name: "Mirror" })).toHaveAttribute("aria-checked", "true");
  });

  it("captures while the camera is ready, as the view's one primary action", () => {
    const wired = controls();
    renderScreen({ controls: wired });
    const capture = screen.getByRole("button", { name: "CAPTURE" });
    expect(capture).toHaveAttribute("data-fotohavn-action", "primary");
    expect(document.querySelectorAll("[data-fotohavn-action='primary']")).toHaveLength(1);
    fireEvent.click(capture);
    expect(wired.onCapture).toHaveBeenCalledOnce();
  });

  it("can't capture while a capture is in flight", () => {
    const wired = controls({ capturing: true });
    renderScreen({ controls: wired });
    const capture = screen.getByRole("button", { name: "CAPTURE" });
    expect(capture).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(capture);
    expect(wired.onCapture).not.toHaveBeenCalled();
  });

  it("can't capture while the camera isn't ready", () => {
    const wired = controls();
    renderScreen({ controls: wired, view: { ...ready, state: "checking", format: null } });
    const capture = screen.getByRole("button", { name: "CAPTURE" });
    expect(capture).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(capture);
    expect(wired.onCapture).not.toHaveBeenCalled();
  });

  it("says when a hook didn't go through, in a live region that is empty otherwise", () => {
    const { rerender, source } = renderScreen({ controls: controls({ notice: "That didn't work. Try again." }) });
    expect(screen.getByText("That didn't work. Try again.")).toBeVisible();
    rerender(<CameraDiagnosticsScreen view={ready} source={source} measured={{ fps: 30, ageMs: 40 }} controls={controls({ notice: null })} />);
    expect(screen.queryByText("That didn't work. Try again.")).not.toBeInTheDocument();
  });
});

const portraitComposition = {
  previewKey: "signature-strip.sepia-v1.r3",
  templateId: "signature-strip",
  lookId: "sepia-v1",
  width: 600,
  height: 1800,
} as const;
const landscapeComposition = {
  previewKey: "panorama-four.classic-v1.r4",
  templateId: "panorama-four",
  lookId: "classic-v1",
  width: 1800,
  height: 600,
} as const;

describe("CameraDiagnosticsScreen last composition", () => {
  it("says none yet before a composition", () => {
    renderScreen();
    expect(screen.getByText("Last composition")).toBeVisible();
    expect(screen.getByText("None yet", { selector: "[data-camera-measure='composition']" })).toBeVisible();
    expect(screen.queryByRole("img", { name: /Signature Strip/ })).not.toBeInTheDocument();
  });

  it("shows a portrait composition as a print from the WebView2 preview host, with its names and native size", () => {
    renderScreen({ view: { ...ready, lastComposition: portraitComposition } });
    const image = screen.getByRole("img", { name: "Signature Strip, Sepia" });
    expect(image).toHaveAttribute("src", "https://preview.fotohavn/signature-strip.sepia-v1.r3.png");
    expect(image).toHaveAttribute("width", "600");
    expect(image).toHaveAttribute("height", "1800");
    expect(image.parentElement).not.toHaveAttribute("data-rotated");
    expect(screen.getByText("Signature Strip · Sepia")).toBeVisible();
    expect(screen.getByText("600 × 1800")).toBeVisible();
    expect(document.querySelector("[data-composition-key]")).toHaveAttribute("data-composition-key", "signature-strip.sepia-v1.r3");
  });

  it("turns a landscape composition a quarter turn counterclockwise and keeps its native size in the caption", () => {
    renderScreen({ view: { ...ready, lastComposition: landscapeComposition } });
    const image = screen.getByRole("img", { name: "Panorama Four, Classic" });
    expect(image.parentElement).toHaveAttribute("data-rotated", "true");
    expect(image).toHaveAttribute("width", "1800");
    expect(screen.getByText("Panorama Four · Classic")).toBeVisible();
    expect(screen.getByText("1800 × 600")).toBeVisible();
  });

  it("asks the connection's host for the image URL, so the headless host's /preview/ route is used", () => {
    const previewUrl = vi.fn((key: string) => `/preview/${key}.png`);
    renderScreen({ view: { ...ready, lastComposition: portraitComposition }, previewUrl });
    expect(previewUrl).toHaveBeenCalledWith("signature-strip.sepia-v1.r3");
    expect(screen.getByRole("img", { name: "Signature Strip, Sepia" })).toHaveAttribute("src", "/preview/signature-strip.sepia-v1.r3.png");
  });

  it("names a Template or Look it doesn't know by its id", () => {
    renderScreen({ view: { ...ready, lastComposition: { ...portraitComposition, templateId: "new-strip", lookId: "new-v1" } } });
    expect(screen.getByText("new-strip · new-v1")).toBeVisible();
  });

  it("shows it read-only, with no composition controls, unless they are given", () => {
    renderScreen({ view: { ...ready, lastComposition: portraitComposition }, controls: controls() });
    expect(screen.queryByRole("button", { name: "COMPOSE" })).not.toBeInTheDocument();
    expect(screen.queryByRole("combobox", { name: "Template" })).not.toBeInTheDocument();
  });
});

describe("CameraDiagnosticsScreen extra controls", () => {
  it("places the extra controls it is given with the other controls, and only when it has controls", () => {
    const { unmount } = renderScreen({ controls: controls({ extra: <button type="button">Extra control</button> }) });
    expect(screen.getByRole("button", { name: "Extra control" })).toBeVisible();
    unmount();

    renderScreen();
    expect(screen.queryByRole("button", { name: "Extra control" })).not.toBeInTheDocument();
  });
});
