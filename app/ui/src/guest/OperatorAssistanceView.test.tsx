import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient, CommandResult } from "../brain/types.ts";
import type { GuestCycleView, OperatorAssistanceView as Assistance } from "../protocol/generated.ts";
import { sentCommands, stubClient } from "../test/operatorHarness.tsx";
import { OperatorAssistanceView } from "./OperatorAssistanceView.tsx";

const cycle: GuestCycleView = {
  id: "cycle-1",
  step: "photographs",
  steps: [
    { step: "template", state: "done", canGoBack: true },
    { step: "photographs", state: "current", canGoBack: false },
    { step: "filters", state: "upcoming", canGoBack: false },
    { step: "keepsake", state: "upcoming", canGoBack: false },
  ],
  startOverAvailable: true,
};

function ui(client: BrainClient, view: Assistance, withCycle: GuestCycleView | null = cycle) {
  return (
    <BrainClientContext value={client}>
      <OperatorAssistanceView cycle={withCycle} view={view} />
    </BrainClientContext>
  );
}

function deferred(): { readonly promise: Promise<CommandResult>; readonly settle: (result: CommandResult) => Promise<void> } {
  let resolve: (result: CommandResult) => void = () => undefined;
  const promise = new Promise<CommandResult>((done) => {
    resolve = done;
  });
  return {
    promise,
    settle: async (result) => {
      await act(async () => {
        resolve(result);
        await promise;
      });
    },
  };
}

describe("OperatorAssistanceView", () => {
  afterEach(cleanup);

  it("says the one thing a guest needs, the same for every reason", () => {
    for (const reason of ["camera", "storage", "guestDisplay"] as const) {
      const { container, unmount } = render(ui(stubClient(), { reason, retrying: false }));
      expect(screen.getByRole("heading", { level: 1, name: "A little interruption." })).toBeVisible();
      expect(screen.getByText("Please ask the FOTOHAVN team for help.")).toBeVisible();
      expect(container.firstElementChild).toHaveAttribute("data-assistance-reason", reason);
      expect(container.textContent).not.toMatch(/camera|storage|display|folder/i);
      unmount();
    }
  });

  it("is one dark band with RETRY as the single primary action", () => {
    const { container } = render(ui(stubClient(), { reason: "camera", retrying: false }));
    expect(container.querySelectorAll('[data-tone="dark"]')).toHaveLength(1);
    const retry = screen.getByRole("button", { name: "RETRY" });
    expect(retry).toHaveAttribute("data-fotohavn-action", "primary");
    expect(retry.closest('[data-tone="dark"]')).not.toBeNull();
    expect(document.querySelectorAll("[data-fotohavn-action]")).toHaveLength(1);
    expect(screen.getByRole("status")).toBeEmptyDOMElement();
  });

  it("names the step the cycle returns to, or none when no cycle was running", () => {
    const { container, rerender } = render(ui(stubClient(), { reason: "storage", retrying: false }));
    expect(container.firstElementChild).toHaveAttribute("data-assistance-step", "photographs");
    rerender(ui(stubClient(), { reason: "guestDisplay", retrying: false }, null));
    expect(container.firstElementChild).not.toHaveAttribute("data-assistance-step");
  });

  it("sends RetryAssistance from RETRY", async () => {
    const client = stubClient();
    render(ui(client, { reason: "camera", retrying: false }));
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "RETRY" }));
      await Promise.resolve();
    });
    expect(sentCommands(client)).toEqual([["RetryAssistance", {}]]);
    expect(screen.getByRole("status")).toBeEmptyDOMElement();
  });

  it("sends only one RetryAssistance while one is on its way, and says it is trying", async () => {
    const client = stubClient();
    const pending = deferred();
    vi.mocked(client.sendCommand).mockReturnValue(pending.promise);
    render(ui(client, { reason: "camera", retrying: false }));
    const retry = screen.getByRole("button", { name: "RETRY" });

    fireEvent.click(retry);
    fireEvent.click(retry);
    expect(sentCommands(client)).toHaveLength(1);
    expect(retry).toHaveAttribute("aria-disabled", "true");
    expect(retry).toHaveAttribute("aria-busy", "true");
    expect(screen.getByRole("status")).toHaveTextContent("Trying again…");

    await pending.settle({ status: "accepted" });
    expect(retry).toHaveAttribute("aria-disabled", "false");
    expect(retry).toHaveAttribute("aria-busy", "false");
    expect(screen.getByRole("status")).toBeEmptyDOMElement();
  });

  it("shows the brain's own retrying state: unavailable, with the line, and no command from a press", () => {
    const client = stubClient();
    const { container } = render(ui(client, { reason: "storage", retrying: true }));
    const retry = screen.getByRole("button", { name: "RETRY" });
    expect(retry).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByRole("status")).toHaveTextContent("Trying again…");
    expect(container.querySelector("[data-retrying]")).not.toBeNull();
    fireEvent.click(retry);
    expect(sentCommands(client)).toEqual([]);
  });

  it("says so, quietly, when RETRY didn't go through, and clears it on the next try", async () => {
    const client = stubClient({ result: { status: "rejected", reason: "notAvailable" } });
    render(ui(client, { reason: "camera", retrying: false }));
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "RETRY" }));
      await Promise.resolve();
    });
    expect(screen.getByRole("status")).toHaveTextContent("That didn't go through. Try again.");
    expect(screen.getByRole("button", { name: "RETRY" })).toHaveAttribute("aria-disabled", "false");

    const pending = deferred();
    vi.mocked(client.sendCommand).mockReturnValue(pending.promise);
    fireEvent.click(screen.getByRole("button", { name: "RETRY" }));
    expect(screen.getByRole("status")).toHaveTextContent("Trying again…");
    await pending.settle({ status: "accepted" });
    expect(screen.getByRole("status")).toBeEmptyDOMElement();
  });

  it("treats a command that never reached the brain the same way", async () => {
    const client = stubClient({ result: { status: "failed", error: "timeout" } });
    render(ui(client, { reason: "guestDisplay", retrying: false }));
    await act(async () => {
      fireEvent.click(screen.getByRole("button", { name: "RETRY" }));
      await Promise.resolve();
    });
    expect(screen.getByRole("status")).toHaveTextContent("That didn't go through. Try again.");
  });
});
