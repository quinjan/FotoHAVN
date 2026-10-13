import { act, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient, CommandResult } from "../brain/types.ts";
import { StorageErrorSurface } from "./StorageErrorSurface.tsx";

function stubClient(sendCommand: BrainClient["sendCommand"]): BrainClient {
  return {
    kind: "fixture",
    connect: vi.fn(),
    getState: () => ({ status: "connected", session: null, mismatch: null }),
    subscribe: () => () => undefined,
    sendCommand,
    sendTest: vi.fn(),
    dispose: vi.fn(),
  };
}

function deferred() {
  let resolve: (result: CommandResult) => void = () => undefined;
  const promise = new Promise<CommandResult>((next) => {
    resolve = next;
  });
  return { promise, resolve };
}

function renderSurface(client: BrainClient) {
  return render(
    <BrainClientContext value={client}>
      <StorageErrorSurface view={{ kind: "cloudSynced", dataPath: "C:\\Users\\Booth\\OneDrive\\FOTOHAVN\\Data" }} />
    </BrainClientContext>,
  );
}

const tryAgain = () => screen.getByRole("button", { name: "TRY AGAIN" });

describe("StorageErrorSurface", () => {
  it("renders the kind's explanation and the path from the view", async () => {
    renderSurface(stubClient(vi.fn()));
    expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent("The Data folder needs attention.");
    expect(await screen.findByText(/inside a cloud-synced folder/)).toBeVisible();
    expect(screen.getByText("C:\\Users\\Booth\\OneDrive\\FOTOHAVN\\Data")).toBeVisible();
  });

  it("sends RetryStorage once, stays unavailable while in flight, and says it is trying again", async () => {
    const pending = deferred();
    const sendCommand = vi.fn<BrainClient["sendCommand"]>().mockReturnValue(pending.promise);
    renderSurface(stubClient(sendCommand));

    act(() => {
      tryAgain().click();
      tryAgain().click();
    });
    expect(tryAgain()).toHaveAttribute("aria-disabled", "true");
    expect(screen.getByText("Trying again…")).toBeVisible();
    act(() => {
      tryAgain().click();
    });
    expect(sendCommand).toHaveBeenCalledExactlyOnceWith("RetryStorage", {});

    await act(async () => {
      pending.resolve({ status: "accepted" });
      await pending.promise;
    });
    expect(tryAgain()).toHaveAttribute("aria-disabled", "false");
    expect(screen.queryByText("Trying again…")).not.toBeInTheDocument();
  });

  it("says the folder still needs attention when an accepted retry leaves the screen showing", async () => {
    renderSurface(stubClient(vi.fn<BrainClient["sendCommand"]>().mockResolvedValue({ status: "accepted" })));
    await act(async () => {
      tryAgain().click();
      await Promise.resolve();
    });
    expect(screen.getByText("The Data folder still needs attention.")).toBeVisible();
  });

  it("shows a rejected retry as an alert line, and clears it on the next try", async () => {
    const second = deferred();
    const sendCommand = vi
      .fn<BrainClient["sendCommand"]>()
      .mockResolvedValueOnce({ status: "rejected", reason: "notAvailable" })
      .mockReturnValueOnce(second.promise);
    renderSurface(stubClient(sendCommand));

    await act(async () => {
      tryAgain().click();
      await Promise.resolve();
    });
    const notice = screen.getByText("That isn't ready yet.");
    expect(notice.closest("[data-status-tone]")).toHaveAttribute("data-status-tone", "alert");

    act(() => {
      tryAgain().click();
    });
    expect(screen.queryByText("That isn't ready yet.")).not.toBeInTheDocument();
    expect(screen.getByText("Trying again…")).toBeVisible();
    await act(async () => {
      second.resolve({ status: "failed", error: "timeout" });
      await second.promise;
    });
    expect(screen.getByText("The booth didn't answer. Try again.")).toBeVisible();
    expect(sendCommand).toHaveBeenCalledTimes(2);
  });
});
