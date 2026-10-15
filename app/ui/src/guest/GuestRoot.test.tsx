import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { App } from "../app/App.tsx";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import { ProtocolBrainClient } from "../brain/protocolClient.ts";
import type { BrainClient, CommandResult } from "../brain/types.ts";
import { guestShellFixtures, guestStart } from "../fixtures/inlineFixtures.ts";
import type { CommandPayloadMap, GuestPresentation } from "../protocol/generated.ts";
import { ManualTransport } from "../test/manualTransport.ts";
import { GuestRoot } from "./GuestRoot.tsx";

const noOverlays = guestStart.presentation.overlays;

function presentationOf(name: string): GuestPresentation {
  const fixture = guestShellFixtures[name];
  if (fixture === undefined) {
    throw new Error(`No guest fixture ${name}`);
  }
  return fixture.presentation;
}

function guestApp(presentation: Partial<GuestPresentation>) {
  const transport = new ManualTransport();
  const client = new ProtocolBrainClient(transport);
  client.connect();
  render(<App client={client} />);
  act(() => {
    transport.open();
    transport.receive({ ...guestStart, presentation: { ...guestStart.presentation, ...presentation } });
  });
}

interface Sent {
  readonly name: keyof CommandPayloadMap;
  readonly payload: unknown;
}

function stubClient(answer: (name: keyof CommandPayloadMap) => CommandResult = () => ({ status: "accepted" })) {
  const sent: Sent[] = [];
  const client: BrainClient = {
    kind: "fixture",
    connect: vi.fn(),
    getState: () => ({ status: "connected", session: null, mismatch: null }),
    subscribe: () => () => undefined,
    sendCommand: (name, payload) => {
      sent.push({ name, payload });
      return Promise.resolve(answer(name));
    },
    sendTest: vi.fn(),
    dispose: vi.fn(),
  };
  return { client, sent };
}

function renderRoot(presentation: GuestPresentation, client: BrainClient) {
  const ui = (next: GuestPresentation) => (
    <BrainClientContext value={client}>
      <GuestRoot presentation={next} reconnecting={false} />
    </BrainClientContext>
  );
  const view = render(ui(presentation));
  return {
    rerender: (next: GuestPresentation) => {
      view.rerender(ui(next));
    },
  };
}

function names(sent: readonly Sent[]): string[] {
  return sent.map((command) => command.name);
}

function exitControl(): HTMLElement {
  return screen.getByRole("button", { name: "Operator exit" });
}

async function advance(ms: number): Promise<void> {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

describe("Guest surfaces", () => {
  it("renders Start: wordmark, statement, supporting line, and STEP INSIDE unavailable until the brain says so", () => {
    guestApp({});
    const main = screen.getByRole("main");
    expect(main).toHaveAttribute("data-fotohavn-surface", "start");
    expect(screen.getByText("FOTOHAVN")).toBeVisible();
    expect(screen.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();
    expect(screen.getByText("Come a little closer.")).toBeVisible();
    const stepInside = screen.getByRole("button", { name: "STEP INSIDE" });
    expect(stepInside).toHaveAttribute("data-fotohavn-action", "primary");
    expect(stepInside).toHaveAttribute("aria-disabled", "true");
    expect(screen.queryByRole("button", { name: "SEE THE GOOD COMPANY" })).not.toBeInTheDocument();
    expect(main.querySelector("[data-guest-gallery-available]")).toHaveAttribute("data-guest-gallery-available", "false");
    expect(exitControl()).toBeVisible();
  });

  it("renders Start even without its view", () => {
    guestApp({ start: null });
    expect(screen.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();
    expect(screen.getByRole("button", { name: "STEP INSIDE" })).toHaveAttribute("aria-disabled", "true");
  });

  it("renders the fatal surface", () => {
    guestApp({ surface: "fatal", start: null, fatal: { reason: "camera" } });
    expect(screen.getByRole("heading", { level: 1, name: "A little interruption." })).toBeVisible();
    expect(screen.getByText("Please ask the FOTOHAVN team for help.")).toBeVisible();
    expect(screen.queryByText("camera")).not.toBeInTheDocument();
    expect(exitControl()).toBeVisible();
  });

  it("renders only the wordmark for surfaces later milestones build, with the exit hold", () => {
    guestApp({ surface: "template", start: null });
    expect(screen.getByRole("main")).toHaveAttribute("data-fotohavn-surface", "template");
    expect(screen.getByRole("heading", { level: 1, name: "FOTOHAVN" })).toBeVisible();
    expect(exitControl()).toBeVisible();
  });

  it("shows the brain's notice", () => {
    guestApp({ overlays: { ...noOverlays, notice: { message: "One moment." } } });
    expect(screen.getAllByRole("status").map((status) => status.textContent)).toContain("One moment.");
    expect(screen.queryByRole("button", { name: "CLOSE" })).not.toBeInTheDocument();
  });
});

describe("Start", () => {
  it("sends BeginGuestCycle from STEP INSIDE when it is available, and shows nothing when accepted", async () => {
    const { client, sent } = stubClient();
    renderRoot(presentationOf("guest-shell-start-ready"), client);
    const stepInside = screen.getByRole("button", { name: "STEP INSIDE" });
    expect(stepInside).toHaveAttribute("aria-disabled", "false");
    await act(async () => {
      fireEvent.click(stepInside);
      await Promise.resolve();
    });
    expect(sent).toEqual([{ name: "BeginGuestCycle", payload: {} }]);
    expect(screen.queryByText("One moment. The booth is getting ready.")).not.toBeInTheDocument();
  });

  it("shows a quiet notice when the brain turns STEP INSIDE down, and CLOSE clears it", async () => {
    const { client } = stubClient(() => ({ status: "rejected", reason: "notAvailable" }));
    renderRoot(presentationOf("guest-shell-start-ready"), client);
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "STEP INSIDE" }));
      await Promise.resolve();
    });
    expect(screen.getByText("One moment. The booth is getting ready.")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "CLOSE" }));
    expect(screen.queryByText("One moment. The booth is getting ready.")).not.toBeInTheDocument();
  });

  it("shows the same notice when the command fails locally, and clears it on the next try that goes through", async () => {
    let next: CommandResult = { status: "failed", error: "disconnected" };
    const { client } = stubClient(() => next);
    renderRoot(presentationOf("guest-shell-start-ready"), client);
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "STEP INSIDE" }));
      await Promise.resolve();
    });
    expect(screen.getByText("One moment. The booth is getting ready.")).toBeVisible();
    next = { status: "accepted" };
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "STEP INSIDE" }));
      await Promise.resolve();
    });
    expect(screen.queryByText("One moment. The booth is getting ready.")).not.toBeInTheDocument();
  });

  it("sends nothing while STEP INSIDE is unavailable", async () => {
    const { client, sent } = stubClient();
    renderRoot(presentationOf("guest-shell-start-not-ready"), client);
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "STEP INSIDE" }));
      await Promise.resolve();
    });
    expect(sent).toEqual([]);
  });

  it("shows the gallery link only when the brain does, and sends OpenGuestGallery", async () => {
    const { client, sent } = stubClient(() => ({ status: "rejected", reason: "notAvailable" }));
    const view = renderRoot(presentationOf("guest-shell-start-ready"), client);
    expect(screen.queryByRole("button", { name: "SEE THE GOOD COMPANY" })).not.toBeInTheDocument();

    view.rerender(presentationOf("guest-shell-start-gallery"));
    const link = screen.getByRole("button", { name: "SEE THE GOOD COMPANY" });
    expect(screen.getByRole("main").querySelector("[data-guest-gallery-available]")).toHaveAttribute(
      "data-guest-gallery-available",
      "true",
    );
    await act(async () => {
      fireEvent.click(link);
      await Promise.resolve();
    });
    expect(sent).toEqual([{ name: "OpenGuestGallery", payload: {} }]);
    expect(screen.getByText("One moment. The booth is getting ready.")).toBeVisible();
  });

  it("prefers the brain's notice over the local one", async () => {
    const { client } = stubClient(() => ({ status: "rejected", reason: "notAvailable" }));
    const view = renderRoot(presentationOf("guest-shell-start-ready"), client);
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "STEP INSIDE" }));
      await Promise.resolve();
    });
    view.rerender({
      ...presentationOf("guest-shell-start-ready"),
      overlays: { ...noOverlays, notice: { message: "Hold on a moment." } },
    });
    expect(screen.getByText("Hold on a moment.")).toBeVisible();
    expect(screen.queryByText("One moment. The booth is getting ready.")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "CLOSE" })).not.toBeInTheDocument();
  });
});

describe("Start unavailable", () => {
  it("says the booth is getting ready when the camera isn't eligible, with no action but the exit hold", () => {
    const { client } = stubClient();
    renderRoot(presentationOf("guest-shell-start-unavailable-camera"), client);
    expect(screen.getByRole("main")).toHaveAttribute("data-fotohavn-surface", "startUnavailable");
    expect(screen.getByRole("heading", { level: 1, name: "The booth is getting ready." })).toBeVisible();
    expect(screen.getByText("One moment.")).toBeVisible();
    expect(screen.getByText("FOTOHAVN")).toBeVisible();
    expect(screen.getAllByRole("button").map((button) => button.getAttribute("aria-label"))).toEqual(["Operator exit"]);
    expect(screen.queryByText("camera")).not.toBeInTheDocument();
  });

  it("says the booth is almost ready for storage, and keeps the gallery link when the brain shows it", () => {
    const { client } = stubClient();
    renderRoot(presentationOf("guest-shell-start-unavailable-storage"), client);
    expect(screen.getByRole("heading", { level: 1, name: "The booth is almost ready." })).toBeVisible();
    expect(screen.getByText("Thank you for waiting.")).toBeVisible();
    expect(screen.getByRole("button", { name: "SEE THE GOOD COMPANY" })).toBeVisible();
    expect(screen.queryByRole("button", { name: "STEP INSIDE" })).not.toBeInTheDocument();
    expect(screen.queryByText("storage")).not.toBeInTheDocument();
  });

  it("recovers to Start when the brain sends it again", () => {
    const { client } = stubClient();
    const view = renderRoot(presentationOf("guest-shell-start-unavailable-camera"), client);
    view.rerender(presentationOf("guest-shell-start-ready"));
    expect(screen.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();
    expect(screen.getByRole("button", { name: "STEP INSIDE" })).toHaveAttribute("aria-disabled", "false");
  });

  it("falls back to Start when the unavailable view is missing", () => {
    const { client } = stubClient();
    renderRoot({ ...presentationOf("guest-shell-start-unavailable-camera"), startUnavailable: null }, client);
    expect(screen.getByRole("heading", { level: 1, name: "Small room. Whole other world." })).toBeVisible();
  });
});

describe("Exit hold", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("is on every Guest Screen view", () => {
    const { client } = stubClient();
    for (const presentation of [
      presentationOf("guest-shell-start-ready"),
      presentationOf("guest-shell-start-unavailable-camera"),
      { ...presentationOf("guest-shell-start-ready"), surface: "fatal" as const, start: null },
      { ...presentationOf("guest-shell-start-ready"), surface: "keepsake" as const, start: null },
    ]) {
      const view = render(
        <BrainClientContext value={client}>
          <GuestRoot presentation={presentation} reconnecting={false} />
        </BrainClientContext>,
      );
      expect(exitControl()).toBeVisible();
      view.unmount();
    }
  });

  it("sends BeginExitHold on press and CompleteExitHold after 3 seconds", async () => {
    const { client, sent } = stubClient();
    renderRoot(presentationOf("guest-shell-start-ready"), client);
    fireEvent.pointerDown(exitControl(), { button: 0, pointerId: 1 });
    expect(names(sent)).toEqual(["BeginExitHold"]);
    await advance(2900);
    expect(names(sent)).toEqual(["BeginExitHold"]);
    await advance(200);
    expect(names(sent)).toEqual(["BeginExitHold", "CompleteExitHold"]);
    fireEvent.pointerUp(exitControl());
    expect(names(sent)).toEqual(["BeginExitHold", "CompleteExitHold"]);
  });

  it("sends CancelExit when released early, and never CompleteExitHold", async () => {
    const { client, sent } = stubClient();
    renderRoot(presentationOf("guest-shell-start-ready"), client);
    fireEvent.pointerDown(exitControl(), { button: 0, pointerId: 1 });
    await advance(1200);
    fireEvent.pointerUp(exitControl());
    await advance(5000);
    expect(names(sent)).toEqual(["BeginExitHold", "CancelExit"]);
  });

  it.each(["Enter", " "])("holds with the %j key for 3 seconds, and cancels when it is released early", async (key) => {
    const { client, sent } = stubClient();
    renderRoot(presentationOf("guest-shell-start-ready"), client);
    exitControl().focus();
    fireEvent.keyDown(exitControl(), { key });
    await advance(3100);
    fireEvent.keyUp(exitControl(), { key });
    expect(names(sent)).toEqual(["BeginExitHold", "CompleteExitHold"]);

    fireEvent.keyDown(exitControl(), { key });
    await advance(500);
    fireEvent.keyUp(exitControl(), { key });
    expect(names(sent)).toEqual(["BeginExitHold", "CompleteExitHold", "BeginExitHold", "CancelExit"]);
  });

  it("returns focus to Operator exit when the PIN dialog closes, so the hold works again without tabbing", async () => {
    const { client, sent } = stubClient();
    const view = renderRoot(presentationOf("guest-shell-start-ready"), client);
    exitControl().focus();
    fireEvent.keyDown(exitControl(), { key: "Enter" });
    await advance(3100);
    expect(names(sent)).toEqual(["BeginExitHold", "CompleteExitHold"]);

    view.rerender(presentationOf("guest-shell-pin-dialog"));
    expect(screen.queryByRole("button", { name: "Operator exit" })).not.toBeInTheDocument();
    fireEvent.keyDown(screen.getByRole("dialog", { name: "Operator PIN" }), { key: "Escape" });
    expect(names(sent)).toEqual(["BeginExitHold", "CompleteExitHold", "CancelExit"]);
    view.rerender(presentationOf("guest-shell-start-ready"));

    expect(document.activeElement).toBe(exitControl());
    fireEvent.keyDown(exitControl(), { key: "Enter" });
    expect(names(sent)).toEqual(["BeginExitHold", "CompleteExitHold", "CancelExit", "BeginExitHold"]);
    fireEvent.keyUp(exitControl(), { key: "Enter" });
  });

  it("shows the progress as text while held", async () => {
    const { client } = stubClient();
    renderRoot(presentationOf("guest-shell-start-ready"), client);
    fireEvent.pointerDown(exitControl(), { button: 0, pointerId: 1 });
    await advance(1000);
    expect(screen.getByText(/Keep holding: \d seconds?/)).toBeVisible();
    fireEvent.pointerUp(exitControl());
  });
});

describe("PIN dialog", () => {
  function pinDialog() {
    return screen.getByRole("dialog", { name: "Operator PIN" });
  }

  function enter(...digits: string[]): void {
    for (const digit of digits) {
      fireEvent.click(within(pinDialog()).getByRole("button", { name: digit }));
    }
  }

  it("replaces the exit hold, makes the content behind it inert, and traps the focus", () => {
    const { client } = stubClient();
    renderRoot(presentationOf("guest-shell-pin-dialog"), client);
    expect(pinDialog()).toHaveAccessibleDescription("Enter the Event's Operator PIN to end the Event.");
    expect(screen.queryByRole("button", { name: "Operator exit" })).not.toBeInTheDocument();
    expect(screen.getByRole("main").querySelector("[inert]")).not.toBeNull();
    expect(screen.getByRole("heading", { level: 1, name: "Small room. Whole other world." }).closest("[inert]")).not.toBeNull();
    expect(pinDialog().contains(document.activeElement)).toBe(true);
    expect(within(pinDialog()).getByRole("button", { name: "END EVENT" })).toHaveAttribute("aria-disabled", "true");
    expect(within(pinDialog()).getByText("4 to 6 digits.")).toBeVisible();
    expect(within(pinDialog()).queryByText(/tries? left/)).not.toBeInTheDocument();
  });

  it("returns the exit hold and the content when the dialog closes", () => {
    const { client } = stubClient();
    const view = renderRoot(presentationOf("guest-shell-pin-dialog"), client);
    view.rerender(presentationOf("guest-shell-start-ready"));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(exitControl()).toBeVisible();
    expect(screen.getByRole("main").querySelector("[inert]")).toBeNull();
  });

  it("sends SubmitPin with the digits entered, and clears the entry", async () => {
    const { client, sent } = stubClient();
    renderRoot(presentationOf("guest-shell-pin-dialog"), client);
    enter("2", "5", "8", "0");
    await act(async () => {
      fireEvent.click(within(pinDialog()).getByRole("button", { name: "END EVENT" }));
      await Promise.resolve();
    });
    expect(sent).toEqual([{ name: "SubmitPin", payload: { digits: "2580" } }]);
    expect(within(pinDialog()).getByText("No digits entered.")).toBeInTheDocument();
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });

  it("does not submit fewer than 4 digits", async () => {
    const { client, sent } = stubClient();
    renderRoot(presentationOf("guest-shell-pin-dialog"), client);
    enter("2", "5", "8");
    await act(async () => {
      fireEvent.click(within(pinDialog()).getByRole("button", { name: "END EVENT" }));
      await Promise.resolve();
    });
    expect(sent).toEqual([]);
  });

  it("says a wrong PIN, and how many tries are left", () => {
    const { client } = stubClient();
    renderRoot(presentationOf("guest-shell-pin-dialog-wrong"), client);
    expect(within(pinDialog()).getByRole("alert")).toHaveTextContent("That PIN isn't right. Try again.");
    expect(within(pinDialog()).getByText("3 tries left.")).toBeVisible();
  });

  it("says one try is left in the singular", () => {
    const { client } = stubClient();
    const wrong = presentationOf("guest-shell-pin-dialog-wrong");
    renderRoot({ ...wrong, overlays: { ...noOverlays, pinDialog: { error: "wrongPin", lockedRemainingSeconds: null, attemptsRemaining: 1 } } }, client);
    expect(within(pinDialog()).getByText("1 try left.")).toBeVisible();
  });

  it("still says a wrong PIN when the brain sends no attempts left", () => {
    const { client } = stubClient();
    const wrong = presentationOf("guest-shell-pin-dialog-wrong");
    renderRoot({ ...wrong, overlays: { ...noOverlays, pinDialog: { error: "wrongPin", lockedRemainingSeconds: null, attemptsRemaining: null } } }, client);
    expect(within(pinDialog()).getByRole("alert")).toHaveTextContent("That PIN isn't right. Try again.");
    expect(within(pinDialog()).queryByText(/tries? left/)).not.toBeInTheDocument();
  });

  it("locks the pad with a countdown that follows the brain, and recovers", () => {
    const { client, sent } = stubClient();
    const view = renderRoot(presentationOf("guest-shell-pin-dialog-locked"), client);
    expect(within(pinDialog()).getByRole("alert")).toHaveTextContent("Too many tries. The pad is locked for now.");
    expect(within(pinDialog()).getByText("Try again in 27 seconds.")).toBeVisible();
    expect(within(pinDialog()).queryByText(/tries? left/)).not.toBeInTheDocument();
    expect(within(pinDialog()).getByRole("button", { name: "5" })).toHaveAttribute("aria-disabled", "true");

    const locked = presentationOf("guest-shell-pin-dialog-locked");
    view.rerender({
      ...locked,
      overlays: { ...noOverlays, pinDialog: { error: "wrongPin", lockedRemainingSeconds: 1, attemptsRemaining: null } },
    });
    expect(within(pinDialog()).getByText("Try again in 1 second.")).toBeVisible();

    view.rerender({
      ...locked,
      overlays: { ...noOverlays, pinDialog: { error: null, lockedRemainingSeconds: null, attemptsRemaining: 5 } },
    });
    expect(within(pinDialog()).queryByText(/Try again in/)).not.toBeInTheDocument();
    expect(within(pinDialog()).getByRole("button", { name: "5" })).toHaveAttribute("aria-disabled", "false");
    expect(sent).toEqual([]);
  });

  it("sends CancelExit from KEEP RUNNING and from Escape", () => {
    const { client, sent } = stubClient();
    renderRoot(presentationOf("guest-shell-pin-dialog"), client);
    fireEvent.click(within(pinDialog()).getByRole("button", { name: "KEEP RUNNING" }));
    fireEvent.keyDown(pinDialog(), { key: "Escape" });
    expect(names(sent)).toEqual(["CancelExit", "CancelExit"]);
  });

  it("says so when the PIN didn't go through, but not when the pad is locked", async () => {
    let next: CommandResult = { status: "failed", error: "disconnected" };
    const { client } = stubClient(() => next);
    renderRoot(presentationOf("guest-shell-pin-dialog"), client);
    enter("1", "2", "3", "4");
    await act(async () => {
      fireEvent.click(within(pinDialog()).getByRole("button", { name: "END EVENT" }));
      await Promise.resolve();
    });
    expect(within(pinDialog()).getByRole("alert")).toHaveTextContent("That didn't go through. Try again.");

    next = { status: "rejected", reason: "pinLocked" };
    enter("1", "2", "3", "4");
    await act(async () => {
      fireEvent.click(within(pinDialog()).getByRole("button", { name: "END EVENT" }));
      await Promise.resolve();
    });
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();

    next = { status: "rejected", reason: "notAvailable" };
    enter("1", "2", "3", "4");
    await act(async () => {
      fireEvent.click(within(pinDialog()).getByRole("button", { name: "END EVENT" }));
      await Promise.resolve();
    });
    expect(within(pinDialog()).getByRole("alert")).toHaveTextContent("That didn't go through. Try again.");

    next = { status: "accepted" };
    enter("1", "2", "3", "4");
    await act(async () => {
      fireEvent.click(within(pinDialog()).getByRole("button", { name: "END EVENT" }));
      await Promise.resolve();
    });
    expect(screen.queryByRole("alert")).not.toBeInTheDocument();
  });
});
