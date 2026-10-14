import { act, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient, TestResult } from "../brain/types.ts";
import type { FrameSource, PreviewFrame } from "../components/LivePreview/index.ts";
import { silentFrameSource } from "../components/LivePreview/index.ts";
import type { CameraDiagnosticsView, DiagnosticsResult } from "../protocol/generated.ts";
import { CameraDiagnosticsWithHooks } from "./CameraDiagnosticsHooks.tsx";
import { CameraDiagnosticsSurface } from "./CameraDiagnosticsSurface.tsx";
import { hostReportIntervalMs } from "./useHostReport.ts";

const ready: CameraDiagnosticsView = {
  cameras: [
    { deviceId: "usb-1", name: "Integrated Webcam" },
    { deviceId: "fake-camera", name: "Fake camera" },
  ],
  selectedDeviceId: "usb-1",
  state: "ready",
  reason: null,
  format: { width: 1920, height: 1080, fps: 30 },
  mirrored: false,
  lastCapture: null,
  lastComposition: null,
};

type Hook = (name: string, args: unknown) => Promise<TestResult<never>>;

function okResult(camera: Partial<DiagnosticsResult["camera"]> = {}): Promise<unknown> {
  return Promise.resolve({
    ok: true,
    result: { camera: { state: "ready", deviceId: "usb-1", format: null, frameAgeMs: 33, deliveredFps: 29, droppedFrames: 1, previewTarget: "operator", ...camera } },
  });
}

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
      for (const listener of listeners) {
        listener({ width: 1280, height: 720, draw: vi.fn() });
      }
    },
  };
}

function stubClient(sendTest: ReturnType<typeof vi.fn>, frames?: FrameSource): BrainClient {
  return {
    kind: "fixture",
    ...(frames === undefined ? {} : { frames }),
    connect: vi.fn(),
    getState: () => ({ status: "connected", session: null, mismatch: null }),
    subscribe: () => () => undefined,
    sendCommand: vi.fn(),
    sendTest: sendTest as unknown as BrainClient["sendTest"],
    dispose: vi.fn(),
  };
}

function renderSurface(client: BrainClient, hooks: boolean, view = ready) {
  return render(
    <BrainClientContext value={client}>
      {hooks ? (
        <CameraDiagnosticsWithHooks view={view} source={client.frames ?? silentFrameSource} measured={{ fps: null, ageMs: null }} />
      ) : (
        <CameraDiagnosticsSurface view={view} />
      )}
    </BrainClientContext>,
  );
}

function hooksCalled(sendTest: ReturnType<typeof vi.fn>): string[] {
  return sendTest.mock.calls.map((call) => String(call[0]));
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.spyOn(HTMLCanvasElement.prototype, "getContext").mockReturnValue(null);
});

afterEach(() => {
  vi.unstubAllEnvs();
  vi.useRealTimers();
});

describe("CameraDiagnosticsSurface in a field build", () => {
  it("shows the view read-only and never sends a test hook", async () => {
    const sendTest = vi.fn<Hook>();
    renderSurface(stubClient(sendTest), false);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(5 * hostReportIntervalMs);
    });
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Check the camera.");
    expect(screen.queryByRole("button", { name: "CAPTURE" })).not.toBeInTheDocument();
    expect(screen.queryByRole("switch")).not.toBeInTheDocument();
    expect(screen.queryByText("Booth report")).not.toBeInTheDocument();
    expect(sendTest).not.toHaveBeenCalled();
  });

  it("shows no controls and sends no hook by default, whatever the connection", () => {
    const sendTest = vi.fn<Hook>();
    render(
      <BrainClientContext value={stubClient(sendTest)}>
        <CameraDiagnosticsSurface view={ready} />
      </BrainClientContext>,
    );
    expect(screen.queryByRole("button", { name: "CAPTURE" })).not.toBeInTheDocument();
    expect(sendTest).not.toHaveBeenCalled();
  });

  it("swaps in the screen with controls in an E2E build, showing the read-only screen until it loads", async () => {
    vi.stubEnv("VITE_E2E", "true");
    vi.resetModules();
    // A fresh module graph evaluates the build-time guard again, now as an E2E build.
    const { BrainClientContext: Context } = await import("../brain/BrainClientContext.ts");
    const { CameraDiagnosticsSurface: E2ESurface } = await import("./CameraDiagnosticsSurface.tsx");
    const sendTest = vi.fn<Hook>((name) => (name === "test.getDiagnostics" ? okResult() : Promise.resolve({ ok: true, result: {} })) as never);
    vi.useRealTimers();
    render(
      <Context value={stubClient(sendTest)}>
        <E2ESurface view={ready} />
      </Context>,
    );
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("Check the camera.");
    expect(await screen.findByRole("button", { name: "CAPTURE" })).toBeVisible();
    expect(sendTest).toHaveBeenCalledWith("test.getDiagnostics", null);
  });
});

describe("CameraDiagnosticsSurface frames", () => {
  it("draws the connection's frames and measures their rate and age on a timer", () => {
    const { source, emit, listeners } = manualSource();
    const { unmount } = renderSurface(stubClient(vi.fn<Hook>(), source), false);
    expect(listeners.size).toBeGreaterThan(0);

    for (let n = 0; n < 10; n += 1) {
      act(() => {
        emit();
        vi.advanceTimersByTime(50);
      });
    }
    act(() => {
      vi.advanceTimersByTime(250);
    });
    expect(screen.getByText("20 fps", { selector: "[data-camera-measure='fps']" })).toBeVisible();
    expect(document.querySelector("[data-state]")).toHaveAttribute("data-state", "live");

    unmount();
    expect(listeners.size).toBe(0);
  });

  it("measures nothing while the camera isn't ready", () => {
    const { source, emit, listeners } = manualSource();
    renderSurface(stubClient(vi.fn<Hook>(), source), false, { ...ready, state: "notAvailable", reason: "stale", format: null });
    expect(listeners.size).toBe(0);
    act(() => {
      emit();
      vi.advanceTimersByTime(1_000);
    });
    expect(screen.getAllByText("—", { selector: "[data-camera-measure]" })).toHaveLength(2);
    expect(document.querySelector("[data-state]")).toHaveAttribute("data-state", "waiting");
  });

  it("copes with a connection that carries no frames", () => {
    renderSurface(stubClient(vi.fn<Hook>()), false);
    act(() => {
      vi.advanceTimersByTime(1_000);
    });
    expect(document.querySelector("[data-state]")).toHaveAttribute("data-state", "waiting");
    expect(screen.getByText("—", { selector: "[data-camera-measure='fps']" })).toBeVisible();
  });
});

describe("CameraDiagnosticsSurface in an E2E build", () => {
  it("picks a camera with test.selectDiagnosticsCamera", async () => {
    const sendTest = vi.fn<Hook>((name) => (name === "test.getDiagnostics" ? okResult() : Promise.resolve({ ok: true, result: {} })) as never);
    renderSurface(stubClient(sendTest), true);
    await act(async () => {
      fireEvent.click(screen.getByRole("radio", { name: /Fake camera/ }));
      await Promise.resolve();
    });
    expect(sendTest).toHaveBeenCalledWith("test.selectDiagnosticsCamera", { deviceId: "fake-camera" });
  });

  it("flips the mirror with test.setDiagnosticsMirror", async () => {
    const sendTest = vi.fn<Hook>((name) => (name === "test.getDiagnostics" ? okResult() : Promise.resolve({ ok: true, result: {} })) as never);
    renderSurface(stubClient(sendTest), true);
    await act(async () => {
      fireEvent.click(screen.getByRole("switch", { name: "Mirror" }));
      await Promise.resolve();
    });
    expect(sendTest).toHaveBeenCalledWith("test.setDiagnosticsMirror", { mirrored: true });
  });

  it("captures once with test.captureDiagnostics, unavailable while it runs, and says nothing on success", async () => {
    let finish: (result: unknown) => void = () => undefined;
    const pending = new Promise((resolve) => {
      finish = resolve;
    });
    const sendTest = vi.fn<Hook>((name) => (name === "test.captureDiagnostics" ? pending : okResult()) as never);
    renderSurface(stubClient(sendTest), true);

    act(() => {
      fireEvent.click(screen.getByRole("button", { name: "CAPTURE" }));
      fireEvent.click(screen.getByRole("button", { name: "CAPTURE" }));
    });
    expect(hooksCalled(sendTest).filter((name) => name === "test.captureDiagnostics")).toHaveLength(1);
    expect(screen.getByRole("button", { name: "CAPTURE" })).toHaveAttribute("aria-disabled", "true");

    await act(async () => {
      finish({ ok: true, result: { fileName: "capture.jpg", width: 1, height: 1, byteLength: 1, mirrored: false } });
      await pending;
    });
    expect(screen.getByRole("button", { name: "CAPTURE" })).toHaveAttribute("aria-disabled", "false");
    expect(screen.queryByText(/didn't/)).not.toBeInTheDocument();
  });

  it("says when a hook didn't go through, then clears the line on the next try that does", async () => {
    let answer: unknown = { ok: false, error: "hookFailed" };
    const sendTest = vi.fn<Hook>((name) => (name === "test.captureDiagnostics" ? Promise.resolve(answer) : okResult()) as never);
    renderSurface(stubClient(sendTest), true);

    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "CAPTURE" }));
      await Promise.resolve();
    });
    expect(screen.getByText("That didn't work. Try again.")).toBeVisible();

    answer = { ok: false, error: "timeout" };
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "CAPTURE" }));
      await Promise.resolve();
    });
    expect(screen.getByText("The booth didn't answer. Try again.")).toBeVisible();

    answer = { ok: true, result: {} };
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "CAPTURE" }));
      await Promise.resolve();
    });
    expect(screen.queryByText(/Try again/)).not.toBeInTheDocument();
  });
});

describe("CameraDiagnosticsSurface booth report", () => {
  it("asks for the booth's frame figures at once and a few times a second, and shows them", async () => {
    const sendTest = vi.fn<Hook>(() => okResult() as never);
    renderSurface(stubClient(sendTest), true);
    await act(async () => {
      await Promise.resolve();
    });
    expect(sendTest).toHaveBeenCalledExactlyOnceWith("test.getDiagnostics", null);
    expect(screen.getByText("Delivered 29 fps · Dropped 1 · Camera frame age 33 ms")).toBeVisible();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(hostReportIntervalMs * 3);
    });
    expect(sendTest).toHaveBeenCalledTimes(4);
    expect(hostReportIntervalMs).toBeGreaterThanOrEqual(250);
  });

  it("never has two requests at once", async () => {
    let release: () => void = () => undefined;
    const sendTest = vi.fn<Hook>(
      () =>
        new Promise((resolve) => {
          release = () => {
            resolve({ ok: true, result: { camera: { frameAgeMs: 1, deliveredFps: 1, droppedFrames: 0 } } } as never);
          };
        }),
    );
    renderSurface(stubClient(sendTest), true);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(hostReportIntervalMs * 4);
    });
    expect(sendTest).toHaveBeenCalledOnce();

    await act(async () => {
      release();
      await vi.advanceTimersByTimeAsync(hostReportIntervalMs);
    });
    expect(sendTest).toHaveBeenCalledTimes(2);
  });

  it("shows nothing when the booth answers an error, such as hooksUnavailable", async () => {
    const sendTest = vi.fn<Hook>(() => Promise.resolve({ ok: false, error: "hooksUnavailable" }) as never);
    renderSurface(stubClient(sendTest), true);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(hostReportIntervalMs);
    });
    expect(screen.queryByText("Booth report")).not.toBeInTheDocument();
  });

  it("keeps the same report when the booth's figures haven't changed, and takes new ones when they have", async () => {
    let camera: Partial<DiagnosticsResult["camera"]> = {};
    const sendTest = vi.fn<Hook>(() => okResult(camera) as never);
    renderSurface(stubClient(sendTest), true);
    await act(async () => {
      await vi.advanceTimersByTimeAsync(hostReportIntervalMs);
    });
    expect(screen.getByText(/Dropped 1/)).toBeVisible();

    camera = { droppedFrames: 4 };
    await act(async () => {
      await vi.advanceTimersByTimeAsync(hostReportIntervalMs);
    });
    expect(screen.getByText(/Dropped 4/)).toBeVisible();
  });

  it("ignores an answer that arrives after the screen is gone, and stops asking", async () => {
    let release: () => void = () => undefined;
    const sendTest = vi.fn<Hook>(
      () =>
        new Promise((resolve) => {
          release = () => {
            resolve({ ok: true, result: { camera: { frameAgeMs: 1, deliveredFps: 1, droppedFrames: 0 } } } as never);
          };
        }),
    );
    const { unmount } = renderSurface(stubClient(sendTest), true);
    unmount();
    await act(async () => {
      release();
      await vi.advanceTimersByTimeAsync(hostReportIntervalMs * 3);
    });
    expect(sendTest).toHaveBeenCalledOnce();
  });
});
