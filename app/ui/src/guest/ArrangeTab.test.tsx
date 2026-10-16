import { act, fireEvent, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { PhotographsView } from "../protocol/generated.ts";
import { cycleOf, names, photographsOf, renderPhotographs, stubClient, type SentCommand } from "./PhotographsTestKit.tsx";

/** The brain's Move starts on the first photograph; these tests move the second, which has a neighbour on each side. */
function movingSecond(): PhotographsView {
  return photographsOf("guest-cycle-photographs-arrange-moving", { selection: { index: 1, mode: "moving" } });
}

function strip(): HTMLElement {
  return screen.getByRole("group", { name: "Your Photo Strip" });
}

/** Lays the strip's windows out one above the other, 100 px apart, so pointer positions map to windows. */
function layOut(): void {
  [...strip().children].forEach((cell, index) => {
    Object.defineProperty(cell, "getBoundingClientRect", {
      configurable: true,
      value: () => ({ left: 0, top: index * 100, right: 100, bottom: index * 100 + 90, width: 100, height: 90, x: 0, y: index * 100 }),
    });
  });
}

function windowButton(position: number, total = 4): HTMLElement {
  return screen.getByRole("button", { name: `Photograph ${String(position)} of ${String(total)}` });
}

function press(target: HTMLElement, x: number, y: number, pointerId = 1): void {
  fireEvent.pointerDown(target, { pointerId, pointerType: "touch", clientX: x, clientY: y, button: 0 });
}

function move(target: HTMLElement, x: number, y: number, pointerId = 1): void {
  fireEvent.pointerMove(target, { pointerId, pointerType: "touch", clientX: x, clientY: y });
}

function release(target: HTMLElement, x: number, y: number, pointerId = 1): void {
  fireEvent.pointerUp(target, { pointerId, pointerType: "touch", clientX: x, clientY: y });
}

function dragged(sent: readonly SentCommand[]): readonly SentCommand[] {
  return sent.filter((command) => command.name === "DragPlace");
}

describe("ArrangeTab", () => {
  beforeEach(() => {
    Object.defineProperty(HTMLElement.prototype, "setPointerCapture", { configurable: true, value: vi.fn() });
  });

  afterEach(() => {
    Reflect.deleteProperty(HTMLElement.prototype, "setPointerCapture");
  });

  it("shows the hint, the strip with every photograph in its window, and the way on", () => {
    const view = photographsOf("guest-cycle-photographs-arrange");
    const { container } = renderPhotographs(view, stubClient());

    expect(screen.getByRole("heading", { level: 1, name: "Arrange your moment." })).toBeVisible();
    expect(screen.getByText("Drag to rearrange · Tap a photograph to edit")).toBeVisible();
    expect(within(strip()).getAllByRole("button")).toHaveLength(4);
    expect(container.querySelectorAll("[data-arrange-strip] img[alt='']").length).toBeGreaterThanOrEqual(5);
    expect(container.querySelector("[data-photographs-tab='arrange']")).toHaveAttribute("data-arrangement-revision", String(view.revision));
    expect(screen.getByRole("button", { name: "CONTINUE TO FILTERS" })).toHaveAttribute("aria-disabled", "false");
  });

  it("fills each window with its photograph, cover-cropped, and leaves empty windows named and quiet", () => {
    const { container } = renderPhotographs(photographsOf("guest-ui-arrange-partial"), stubClient());

    const photo = windowButton(1).querySelector("img");
    expect(photo?.getAttribute("src")).toMatch(/^data:image\/svg\+xml/);
    expect(within(strip()).getAllByRole("button")).toHaveLength(3);
    expect(screen.getByText("Position 4 is empty.")).toBeInTheDocument();
    expect(container.querySelector("[data-empty]")).not.toBeNull();
    expect(screen.getByText("Take a photograph for each empty position to continue.")).toBeVisible();
    expect(screen.getByRole("button", { name: "CONTINUE TO FILTERS" })).toHaveAttribute("aria-disabled", "true");
  });

  it("places each window by the Template's geometry, as percentages of the print", () => {
    renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), stubClient());

    expect(windowButton(2)).toHaveStyle({ left: "0%", top: "25%", width: "100%", height: "25%" });
  });

  it("turns a landscape Template's strip a quarter and keeps its position numbers upright", () => {
    const { container } = renderPhotographs(photographsOf("guest-cycle-photographs-arrange-landscape"), stubClient());

    expect(container.querySelector("[data-arrange-strip] > div")).toHaveAttribute("data-rotated", "true");
    expect(windowButton(2)).toHaveStyle({ left: "25%", width: "25%", height: "100%" });
  });

  it("continues to Filters, and asks to take more photographs while a window is empty", () => {
    const stub = stubClient();
    const view = renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), stub);
    fireEvent.click(screen.getByRole("button", { name: "CONTINUE TO FILTERS" }));
    expect(stub.sent).toEqual([{ name: "GoToStep", payload: { step: "filters" } }]);

    view.show(photographsOf("guest-ui-arrange-partial"));
    fireEvent.click(screen.getByRole("button", { name: "CONTINUE TO FILTERS" }));
    fireEvent.click(screen.getByRole("button", { name: "TAKE MORE PHOTOGRAPHS" }));
    expect(names(stub.sent)).toEqual(["GoToStep", "ShowCameraTab"]);
  });

  it("goes quiet while the strip is prepared with Filters skipped: no actions, windows inert, one polite line", () => {
    const stub = stubClient();
    const composing = photographsOf("guest-cycle-photographs-arrange-one-look-composing");
    const { container, show } = renderPhotographs(composing, stub, cycleOf("guest-cycle-photographs-arrange-one-look"));

    expect(screen.getByText("Preparing your strip.")).toBeVisible();
    expect(screen.getByRole("button", { name: "FINISH MY STRIP" })).toHaveAttribute("aria-disabled", "true");
    expect(screen.queryByRole("button", { name: "RETAKE" })).toBeNull();
    expect(screen.queryByRole("button", { name: "MOVE" })).toBeNull();
    expect(screen.queryByText("Take a photograph for each empty position to continue.")).toBeNull();
    expect(within(strip()).getAllByRole("button").every((window) => window.getAttribute("aria-disabled") === "true")).toBe(true);
    expect(container.querySelector("[aria-live='polite']")?.textContent).toBe("");

    show({ ...composing, canReview: true, canContinue: true });
    expect(screen.queryByText("Preparing your strip.")).toBeNull();
    expect(within(strip()).getAllByRole("button").some((window) => window.getAttribute("aria-disabled") === "true")).toBe(false);
  });

  it("does not offer more photographs when no window is empty", () => {
    renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), stubClient());

    expect(screen.queryByRole("button", { name: "TAKE MORE PHOTOGRAPHS" })).toBeNull();
  });

  it("leaves Photographs as FINISH MY STRIP when the Event offers one Look and Filters is skipped", () => {
    const stub = stubClient();
    renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), stub, cycleOf("guest-cycle-photographs-arrange-one-look"));

    expect(screen.queryByRole("button", { name: "CONTINUE TO FILTERS" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "FINISH MY STRIP" }));

    expect(stub.sent).toEqual([{ name: "GoToStep", payload: { step: "filters" } }]);
  });

  describe("selecting", () => {
    it("selects the photograph the guest taps, and nothing happens on an empty window", () => {
      const stub = stubClient();
      renderPhotographs(photographsOf("guest-ui-arrange-partial"), stub);

      fireEvent.click(windowButton(3));

      expect(stub.sent).toEqual([{ name: "SelectPosition", payload: { index: 2 } }]);
    });

    it("shows what the selection allows: RETAKE and MOVE, each asking for the selected window", () => {
      const stub = stubClient();
      renderPhotographs(photographsOf("guest-cycle-photographs-arrange-selected"), stub);

      expect(screen.getByText("Photograph 2 is selected.")).toBeVisible();
      expect(windowButton(2)).toHaveAttribute("aria-pressed", "true");
      expect(windowButton(1)).toHaveAttribute("aria-pressed", "false");
      fireEvent.click(screen.getByRole("button", { name: "RETAKE" }));
      fireEvent.click(screen.getByRole("button", { name: "MOVE" }));

      expect(stub.sent).toEqual([
        { name: "BeginRetake", payload: { index: 1 } },
        { name: "BeginMove", payload: { index: 1 } },
      ]);
    });

    it("offers no actions while nothing is selected", () => {
      renderPhotographs(photographsOf("guest-cycle-photographs-arrange", { selection: { index: null, mode: "none" } }), stubClient());

      expect(screen.queryByRole("button", { name: "RETAKE" })).toBeNull();
      expect(screen.queryByRole("button", { name: "MOVE" })).toBeNull();
    });

    it("moves focus between the windows with the arrow keys", () => {
      renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), stubClient());
      windowButton(1).focus();

      fireEvent.keyDown(windowButton(1), { key: "ArrowDown" });
      expect(windowButton(2)).toHaveFocus();
      fireEvent.keyDown(windowButton(2), { key: "ArrowRight" });
      expect(windowButton(3)).toHaveFocus();
      fireEvent.keyDown(windowButton(3), { key: "ArrowUp" });
      expect(windowButton(2)).toHaveFocus();
      fireEvent.keyDown(windowButton(2), { key: "ArrowLeft" });
      expect(windowButton(1)).toHaveFocus();
      fireEvent.keyDown(windowButton(1), { key: "ArrowLeft" });
      expect(windowButton(1)).toHaveFocus();
      fireEvent.keyDown(windowButton(1), { key: "Enter" });
      expect(windowButton(1)).toHaveFocus();
    });

    it("ignores an arrow key from outside the windows", () => {
      renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), stubClient());
      document.body.focus();

      fireEvent.keyDown(windowButton(2), { key: "ArrowDown" });

      expect(document.body).toHaveFocus();
    });
  });

  describe("moving", () => {
    it("asks where the photograph goes, turns every window into a destination, and sends the one tapped", () => {
      const stub = stubClient();
      renderPhotographs(movingSecond(), stub);

      expect(screen.getByText("Choose where photograph 2 goes.")).toBeVisible();
      expect(screen.getByRole("button", { name: "Photograph 2, moving" })).toBeInTheDocument();
      fireEvent.click(screen.getByRole("button", { name: "Swap with position 4" }));

      expect(stub.sent).toEqual([{ name: "MoveTo", payload: { index: 3 } }]);
    });

    it("can move into an empty window", () => {
      const stub = stubClient();
      renderPhotographs(
        photographsOf("guest-ui-arrange-partial", { selection: { index: 0, mode: "moving" } }),
        stub,
      );

      fireEvent.click(screen.getByRole("button", { name: "Move to position 4" }));

      expect(stub.sent).toEqual([{ name: "MoveTo", payload: { index: 3 } }]);
    });

    it("cancels a Move, and offers nothing else beside it", () => {
      const stub = stubClient();
      renderPhotographs(photographsOf("guest-cycle-photographs-arrange-moving"), stub);

      fireEvent.click(screen.getByRole("button", { name: "CANCEL MOVE" }));

      expect(stub.sent).toEqual([{ name: "CancelMove", payload: {} }]);
      expect(screen.queryByRole("button", { name: "RETAKE" })).toBeNull();
    });

    it("never starts a drag while a Move waits", () => {
      const stub = stubClient();
      renderPhotographs(movingSecond(), stub);
      layOut();

      press(screen.getByRole("button", { name: "Swap with position 1" }), 50, 45);
      move(screen.getByRole("button", { name: "Swap with position 1" }), 50, 245);
      release(screen.getByRole("button", { name: "Swap with position 1" }), 50, 245);

      expect(dragged(stub.sent)).toEqual([]);
    });
  });

  describe("dragging", () => {
    it("sends DragPlace with the arrangement revision when a photograph is released on another window", () => {
      const stub = stubClient();
      const view = photographsOf("guest-cycle-photographs-arrange");
      renderPhotographs(view, stub);
      layOut();

      press(windowButton(1), 50, 45);
      move(windowButton(1), 50, 245);
      release(windowButton(1), 50, 245);

      expect(stub.sent).toEqual([
        { name: "DragPlace", payload: { sourceIndex: 0, captureId: view.windows[0]?.captureId, revision: view.revision, targetIndex: 2 } },
      ]);
    });

    it("sends the revision the brain last showed, so a stale release is the brain's to ignore", () => {
      const stub = stubClient();
      const arranged = photographsOf("guest-cycle-photographs-arrange");
      const view = renderPhotographs(arranged, stub);
      layOut();
      view.show({ ...arranged, revision: 9 });

      press(windowButton(4), 50, 345);
      move(windowButton(4), 50, 45);
      release(windowButton(4), 50, 45);

      expect(stub.sent).toEqual([
        { name: "DragPlace", payload: { sourceIndex: 3, captureId: arranged.windows[3]?.captureId, revision: 9, targetIndex: 0 } },
      ]);
    });

    it("marks the source and the window it is over while the drag is under way", () => {
      renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), stubClient());
      layOut();

      press(windowButton(1), 50, 45);
      move(windowButton(1), 50, 145);

      expect(windowButton(1)).toHaveAttribute("data-drag-source");
      expect(windowButton(2)).toHaveAttribute("data-drag-over");
      move(windowButton(1), 50, 45);
      expect(windowButton(2)).not.toHaveAttribute("data-drag-over");
      release(windowButton(1), 50, 45);
      expect(windowButton(1)).not.toHaveAttribute("data-drag-source");
    });

    it("moves a photograph into an empty window the same way", () => {
      const stub = stubClient();
      renderPhotographs(photographsOf("guest-ui-arrange-partial"), stub);
      layOut();

      press(windowButton(1), 50, 45);
      move(windowButton(1), 50, 345);
      release(windowButton(1), 50, 345);

      expect(dragged(stub.sent)).toEqual([
        { name: "DragPlace", payload: { sourceIndex: 0, captureId: "capture-1", revision: 6, targetIndex: 3 } },
      ]);
    });

    it("sends nothing for a release on the same window, on no window, or after a cancelled touch", () => {
      const stub = stubClient();
      renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), stub);
      layOut();

      press(windowButton(1), 50, 45);
      move(windowButton(1), 90, 80);
      release(windowButton(1), 50, 45);
      press(windowButton(1), 50, 45);
      move(windowButton(1), 500, 500);
      release(windowButton(1), 500, 500);
      press(windowButton(1), 50, 45);
      move(windowButton(1), 50, 245);
      fireEvent.pointerCancel(windowButton(1), { pointerId: 1 });
      release(windowButton(1), 50, 245);

      expect(stub.sent).toEqual([]);
    });

    it("treats a short press as a tap, and a drag as only a drag", () => {
      const stub = stubClient();
      renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), stub);
      layOut();

      press(windowButton(2), 50, 145);
      move(windowButton(2), 52, 146);
      release(windowButton(2), 52, 146);
      fireEvent.click(windowButton(2));
      expect(names(stub.sent)).toEqual(["SelectPosition"]);

      press(windowButton(1), 50, 45);
      move(windowButton(1), 50, 245);
      release(windowButton(1), 50, 245);
      fireEvent.click(windowButton(1));
      expect(names(stub.sent)).toEqual(["SelectPosition", "DragPlace"]);
    });

    it("ignores other pointers and the secondary mouse button", () => {
      const stub = stubClient();
      renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), stub);
      layOut();

      fireEvent.pointerDown(windowButton(1), { pointerId: 1, pointerType: "mouse", button: 2, clientX: 50, clientY: 45 });
      move(windowButton(1), 50, 245);
      release(windowButton(1), 50, 245);
      press(windowButton(1), 50, 45, 1);
      move(windowButton(1), 50, 245, 2);
      release(windowButton(1), 50, 245, 2);

      expect(stub.sent).toEqual([]);
    });

    it("does not drag while a sequence runs or a candidate waits", () => {
      const stub = stubClient();
      const view = renderPhotographs(photographsOf("guest-cycle-photographs-retaking"), stub);
      layOut();
      press(windowButton(1), 50, 45);
      move(windowButton(1), 50, 245);
      release(windowButton(1), 50, 245);
      fireEvent.click(windowButton(2));

      view.show(photographsOf("guest-cycle-photographs-arrange", { sequence: { ...photographsOf("guest-cycle-photographs-arrange").sequence, running: true } }));
      layOut();
      press(windowButton(1), 50, 45);
      move(windowButton(1), 50, 245);
      release(windowButton(1), 50, 245);
      fireEvent.click(windowButton(2));

      expect(stub.sent).toEqual([]);
    });
  });

  describe("drag and Move are the same arrangement", () => {
    it("name the same source and the same destination for the same intent", () => {
      const dragStub = stubClient();
      const drag = renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), dragStub);
      layOut();
      press(windowButton(1), 50, 45);
      move(windowButton(1), 50, 245);
      release(windowButton(1), 50, 245);
      drag.unmount();

      const moveStub = stubClient();
      const moving = renderPhotographs(photographsOf("guest-cycle-photographs-arrange"), moveStub);
      fireEvent.click(windowButton(1));
      moving.show(photographsOf("guest-cycle-photographs-arrange", { selection: { index: 0, mode: "selected" } }));
      fireEvent.click(screen.getByRole("button", { name: "MOVE" }));
      moving.show(photographsOf("guest-cycle-photographs-arrange", { selection: { index: 0, mode: "moving" } }));
      fireEvent.click(screen.getByRole("button", { name: "Swap with position 3" }));

      const [placed] = dragged(dragStub.sent);
      const begin = moveStub.sent.find((command) => command.name === "BeginMove");
      const end = moveStub.sent.find((command) => command.name === "MoveTo");
      expect(placed?.payload).toMatchObject({ sourceIndex: 0, targetIndex: 2 });
      expect({ sourceIndex: (begin?.payload as { index: number }).index, targetIndex: (end?.payload as { index: number }).index }).toEqual({
        sourceIndex: 0,
        targetIndex: 2,
      });
    });
  });

  describe("a Retake's new photograph", () => {
    it("waits beside the original with ACCEPT, TRY AGAIN, and KEEP ORIGINAL", () => {
      const stub = stubClient();
      renderPhotographs(photographsOf("guest-cycle-photographs-retaking"), stub);

      expect(screen.getByText("Keep the new photograph for position 3, or try again.")).toBeVisible();
      expect(screen.getByText("Original")).toBeVisible();
      expect(screen.getByText("New")).toBeVisible();
      fireEvent.click(screen.getByRole("button", { name: "ACCEPT" }));
      fireEvent.click(screen.getByRole("button", { name: "TRY AGAIN" }));
      fireEvent.click(screen.getByRole("button", { name: "KEEP ORIGINAL" }));

      expect(names(stub.sent)).toEqual(["AcceptRetake", "TryAgain", "KeepOriginal"]);
      expect(screen.queryByRole("button", { name: "CONTINUE TO FILTERS" })).toBeNull();
    });

    it("leaves the strip as it was and unreachable until the guest decides", () => {
      const stub = stubClient();
      renderPhotographs(photographsOf("guest-cycle-photographs-retaking"), stub);

      expect(windowButton(3)).toHaveAttribute("aria-disabled", "true");
      fireEvent.click(windowButton(1));

      expect(stub.sent).toEqual([]);
    });

    it("compares at the window's own proportion", () => {
      const { container } = renderPhotographs(photographsOf("guest-ui-arrange-retaking-panorama"), stubClient());

      expect(container.querySelector("[data-retake-index='1']")?.querySelector("div")).toHaveStyle({ "--window-ratio": "0.75" });
    });

    it("only offers KEEP ORIGINAL while the Retake waits for its photograph", () => {
      const stub = stubClient();
      renderPhotographs(photographsOf("guest-cycle-photographs-arrange", { selection: { index: 2, mode: "retaking" } }), stub);

      fireEvent.click(screen.getByRole("button", { name: "KEEP ORIGINAL" }));

      expect(stub.sent).toEqual([{ name: "KeepOriginal", payload: {} }]);
      expect(screen.queryByRole("button", { name: "ACCEPT" })).toBeNull();
    });

    it("copes with a candidate for a window the Template does not have, and with no original image", () => {
      const view = photographsOf("guest-cycle-photographs-retaking");
      renderPhotographs(
        {
          ...view,
          retakeCandidate: { index: 9, imageUrl: "data:,x" },
          windows: view.windows.map((window) => ({ ...window, imageUrl: null })),
        },
        stubClient(),
      );

      expect(screen.getByRole("button", { name: "ACCEPT" })).toBeVisible();
    });
  });

  describe("the retained pool", () => {
    it("shows only a count, in the voice, and nothing when none are kept", () => {
      const view = renderPhotographs(photographsOf("guest-ui-arrange-retained"), stubClient());
      expect(screen.getByText("1 more photograph is kept for this Event.")).toBeVisible();

      view.show(photographsOf("guest-ui-arrange-retained", { retainedCount: 3 }));
      expect(screen.getByText("3 more photographs are kept for this Event.")).toBeVisible();

      view.show(photographsOf("guest-ui-arrange-retained", { retainedCount: 0 }));
      expect(screen.queryByText(/kept for this Event/)).toBeNull();
    });
  });

  describe("live regions", () => {
    const announcements = (container: HTMLElement) =>
      [...container.querySelectorAll("[aria-live='polite']")].map((region) => region.textContent).filter((text) => text !== "");

    it("announces that the strip is being prepared once, when the brain starts", () => {
      const arranged = photographsOf("guest-cycle-photographs-arrange-one-look");
      const { container, show } = renderPhotographs(arranged, stubClient(), cycleOf("guest-cycle-photographs-arrange-one-look"));
      expect(announcements(container)).toEqual([]);

      show(photographsOf("guest-cycle-photographs-arrange-one-look-composing"));
      expect(announcements(container)).toEqual(["Preparing your strip."]);
    });

    it("announces a new photograph once, when the candidate arrives", () => {
      const { container, show } = renderPhotographs(
        photographsOf("guest-cycle-photographs-arrange", { selection: { index: 2, mode: "retaking" } }),
        stubClient(),
      );
      expect(announcements(container)).toEqual([]);

      show(photographsOf("guest-cycle-photographs-retaking"));
      expect(announcements(container)).toEqual(["Your new photograph is ready. Accept it, try again, or keep the original."]);
      show(photographsOf("guest-cycle-photographs-retaking", { retainedCount: 2 }));
      expect(announcements(container)).toHaveLength(1);
    });

    it("announces where a photograph was placed, when the arrangement changes under a selection", () => {
      const { container, show } = renderPhotographs(photographsOf("guest-cycle-photographs-arrange-moving"), stubClient());
      expect(announcements(container)).toEqual([]);

      show(photographsOf("guest-cycle-photographs-arrange-selected", { revision: 7, selection: { index: 3, mode: "selected" } }));

      expect(announcements(container)).toEqual(["Photograph placed in position 4."]);
    });

    it("stays quiet when the revision changes with nothing selected", () => {
      const unselected = photographsOf("guest-cycle-photographs-arrange", { selection: { index: null, mode: "none" } });
      const { container, show } = renderPhotographs(unselected, stubClient());

      show({ ...unselected, revision: 7 });

      expect(announcements(container)).toEqual([]);
    });

    it("lets a layout effect run for the first value without announcing it", () => {
      const { container } = renderPhotographs(photographsOf("guest-cycle-photographs-retaking"), stubClient());
      act(() => undefined);

      expect(announcements(container)).toEqual([]);
    });
  });
});
