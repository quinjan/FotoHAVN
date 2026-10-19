import { act, fireEvent, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { CameraState } from "../protocol/generated.ts";
import { runningEventPanel } from "../fixtures/operatorSetupFixtures.ts";
import { renderOperator, sentCommands, stubClient } from "../test/operatorHarness.tsx";
import { RunningEventSection, runningCameraState, timeSinceStartIntervalMs } from "./RunningEventSection.tsx";

function facts(): Record<string, string> {
  return Object.fromEntries(screen.getAllByRole("term").map((term) => [term.textContent, term.nextElementSibling?.textContent ?? ""]));
}

describe("RunningEventSection", () => {
  beforeEach(() => {
    vi.useFakeTimers({ now: Date.parse("2026-10-13T10:42:00Z") });
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("shows the brain's facts with text labels", () => {
    renderOperator(<RunningEventSection panel={runningEventPanel({ name: "Studio open day", completedGuestCycles: 37, startedAt: "2026-10-13T09:30:00Z" })} />);
    expect(screen.getByRole("group", { name: "Studio open day" })).toBeVisible();
    expect(facts()).toEqual({
      "GUEST DISPLAY": "Connected",
      CAMERA: "Ready",
      "CURRENT STEP": "Start",
      "COMPLETED GUEST CYCLES": "37",
      "TIME SINCE START": "1 h 12 min",
    });
  });

  it("shows a disconnected display and a camera that isn't available", () => {
    renderOperator(
      <RunningEventSection
        panel={runningEventPanel({ guestDisplay: { name: "DELL P2419H", state: "disconnected" }, camera: { state: "notAvailable", reason: "stale" } })}
      />,
    );
    expect(facts()["GUEST DISPLAY"]).toBe("Disconnected");
    expect(facts().CAMERA).toBe("Not available");
  });

  it.each([
    ["ready", "ready"],
    ["notAvailable", "not-available"],
    ["inUseByAnotherApp", "not-available"],
    ["checking", "checking"],
    ["idle", "checking"],
  ] as const satisfies readonly (readonly [CameraState, string])[])("reads the camera state %s as %s", (state, expected) => {
    expect(runningCameraState(state)).toBe(expected);
  });

  it("says Checking while the camera hasn't delivered a fresh frame", () => {
    renderOperator(<RunningEventSection panel={runningEventPanel({ camera: { state: "checking", reason: null } })} />);
    expect(facts().CAMERA).toBe("Checking");
  });

  it.each([
    ["start", "Start"],
    ["template", "Template"],
    ["photographs", "Photographs"],
    ["filters", "Filters"],
    ["keepsake", "Keepsake"],
    ["guestGallery", "Guest gallery"],
    ["operatorAssistance", "Operator Assistance"],
  ] as const)("says the %s step in words", (step, words) => {
    renderOperator(<RunningEventSection panel={runningEventPanel({ currentStep: step })} />);
    expect(facts()["CURRENT STEP"]).toBe(words);
  });

  it("keeps the time since start current as the clock moves", () => {
    renderOperator(<RunningEventSection panel={runningEventPanel({ startedAt: "2026-10-13T10:41:30Z" })} />);
    expect(facts()["TIME SINCE START"]).toBe("Just started");
    act(() => {
      vi.advanceTimersByTime(timeSinceStartIntervalMs * 3);
    });
    expect(facts()["TIME SINCE START"]).toBe("2 min");
    act(() => {
      vi.advanceTimersByTime(60 * 60_000);
    });
    expect(facts()["TIME SINCE START"]).toBe("1 h 2 min");
  });

  it("stops reading the clock when it goes away", () => {
    const { unmount } = renderOperator(<RunningEventSection panel={runningEventPanel()} />);
    unmount();
    expect(vi.getTimerCount()).toBe(0);
  });

  it("shows Operator Assistance as the alert in text, with RETRY as the single primary action", () => {
    renderOperator(<RunningEventSection panel={runningEventPanel({ assistance: { reason: "storage" }, currentStep: "operatorAssistance" })} />);
    expect(screen.getByRole("group")).toHaveTextContent("The Data folder can't be written to. Check the disk and the folder, then RETRY.");
    expect(screen.getByRole("button", { name: "RETRY" })).toHaveAttribute("data-fotohavn-action", "primary");
  });

  it.each([
    ["storage", null, "The Data folder can't be written to. Check the disk and the folder, then RETRY."],
    ["guestDisplay", null, "The guest display isn't connected. Reconnect it, then RETRY."],
    ["camera", null, "The camera needs attention. Check that it is connected, then RETRY."],
    ["camera", "removed", "The camera was disconnected. Reconnect it, then RETRY."],
    ["camera", "stale", "The camera stopped sending pictures. Check its cable, then RETRY."],
    ["camera", "failed", "The camera stopped working. Check its cable, then RETRY."],
    ["camera", "exclusiveLost", "Another app took over the camera. Close that app, then RETRY."],
    ["camera", "inUseByAnotherApp", "Another app is using this camera. Close that app, then RETRY."],
    ["camera", "privacyDenied", "Camera access is off in Windows privacy settings. Turn it on in Windows settings, then RETRY."],
    ["camera", "noSupportedFormat", "This camera has no picture size FOTOHAVN can use. Try another USB port, then RETRY."],
    ["camera", "notFound", "FOTOHAVN can't find this camera. Reconnect it, then RETRY."],
    ["camera", "unknown", "Something went wrong with the camera. Check that it is connected, then RETRY."],
  ] as const)("says the %s cause (camera failure %s) with what to do, never by color alone, while the step reads Operator Assistance", (reason, failure, words) => {
    renderOperator(
      <RunningEventSection
        panel={runningEventPanel({
          assistance: { reason },
          currentStep: "operatorAssistance",
          camera: failure === null ? { state: "ready", reason: null } : { state: "notAvailable", reason: failure },
        })}
      />,
    );
    expect(within(screen.getByRole("group")).getByRole("status")).toHaveTextContent(words);
    expect(screen.getByRole("group")).toHaveAttribute("data-assistance", "true");
    expect(screen.getByRole("group")).toHaveAttribute("data-tone", "dark");
    expect(facts()["CURRENT STEP"]).toBe("Operator Assistance");
    expect(document.querySelectorAll('[data-fotohavn-action="primary"]')).toHaveLength(1);
  });

  it("sends OpenGallery with the Event's id, RetryAssistance, and EndEvent", async () => {
    const client = stubClient();
    renderOperator(<RunningEventSection panel={runningEventPanel({ eventId: "event-9", assistance: { reason: "camera" } })} />, client);
    for (const name of ["OPEN GALLERY", "RETRY", "END EVENT"]) {
      await act(async () => {
        fireEvent.click(screen.getByRole("button", { name }));
        await Promise.resolve();
      });
    }
    expect(sentCommands(client)).toEqual([
      ["OpenGallery", { eventId: "event-9" }],
      ["RetryAssistance", {}],
      ["EndEvent", {}],
    ]);
  });

  it("tells the operator, politely, when the gallery isn't there yet", async () => {
    renderOperator(<RunningEventSection panel={runningEventPanel()} />, stubClient({ result: { status: "rejected", reason: "notAvailable" } }));
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "OPEN GALLERY" }));
      await Promise.resolve();
    });
    expect(screen.getAllByRole("status").some((status) => status.textContent === "That isn't ready yet.")).toBe(true);
  });
});
