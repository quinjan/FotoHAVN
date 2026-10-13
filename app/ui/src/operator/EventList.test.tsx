import { act, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { BrainClientContext, useBrainClient } from "../brain/BrainClientContext.ts";
import type { BrainClient, CommandResult } from "../brain/types.ts";
import { operatorEventListWithRows } from "../fixtures/inlineFixtures.ts";
import { EventList } from "./EventList.tsx";

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

function renderList(client: BrainClient, events = operatorEventListWithRows.presentation.eventList?.events ?? []) {
  return render(
    <BrainClientContext value={client}>
      <EventList events={events} />
    </BrainClientContext>,
  );
}

const rows = operatorEventListWithRows.presentation.eventList?.events ?? [];

describe("EventList", () => {
  it("renders the empty state with the primary action", () => {
    renderList(stubClient(vi.fn()), []);
    expect(screen.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
    expect(screen.getByText("No Events yet.")).toBeVisible();
    expect(screen.getByRole("button", { name: "NEW EVENT" })).toHaveTextContent(/^NEW EVENT$/);
    expect(screen.getByRole("button", { name: "NEW EVENT" })).toHaveAttribute("data-fotohavn-action", "primary");
    expect(screen.getByRole("button", { name: "NEW EVENT" }).querySelector("[data-direction='right'] svg")).not.toBeNull();
    expect(screen.queryByRole("list")).not.toBeInTheDocument();
  });

  it("renders each Event row in the brain's order with its name, date, venue, and counts", () => {
    renderList(stubClient(vi.fn()), rows);
    const items = within(screen.getByRole("list", { name: "Events" })).getAllByRole("listitem");
    expect(items).toHaveLength(2);

    const [wedding, birthday] = items as [HTMLElement, HTMLElement];
    expect(within(wedding).getByRole("heading", { level: 2 })).toHaveTextContent("Santos and Reyes wedding");
    expect(wedding).toHaveTextContent("RUNNING");
    expect(wedding).toHaveTextContent("DateNov 21, 2026");
    expect(wedding).toHaveTextContent("VenueThe Glasshouse, Tagaytay");
    expect(wedding).toHaveTextContent("Completed1 Guest Cycle");
    expect(wedding).toHaveTextContent("Last usedNot used yet");
    expect(wedding).toHaveAttribute("data-event-id", "event-2");

    expect(within(birthday).getByRole("heading", { level: 2 })).toHaveTextContent("Mara's 30th");
    expect(birthday).not.toHaveTextContent("RUNNING");
    expect(birthday).toHaveTextContent("VenueNo venue");
    expect(birthday).toHaveTextContent("Completed42 Guest Cycles");
    expect(birthday).toHaveTextContent("Last usedOct 3, 2026");
    expect(screen.queryByText("No Events yet.")).not.toBeInTheDocument();
  });

  it("sends OpenNewEvent and politely shows a rejection", async () => {
    const sendCommand = vi.fn<BrainClient["sendCommand"]>().mockResolvedValue({ status: "rejected", reason: "notAvailable" });
    renderList(stubClient(sendCommand), []);

    await act(async () => {
      screen.getByRole("button", { name: "NEW EVENT" }).click();
      await Promise.resolve();
    });

    expect(sendCommand).toHaveBeenCalledExactlyOnceWith("OpenNewEvent", {});
    expect(screen.getByRole("status")).toHaveTextContent("That isn't ready yet.");
  });

  it("ignores clicks while a command is pending, then clears the notice when accepted", async () => {
    const first = deferred();
    const second = deferred();
    const sendCommand = vi
      .fn<BrainClient["sendCommand"]>()
      .mockReturnValueOnce(first.promise)
      .mockReturnValueOnce(second.promise);
    renderList(stubClient(sendCommand), []);
    const button = screen.getByRole("button", { name: "NEW EVENT" });

    act(() => {
      button.click();
    });
    expect(button).toHaveAttribute("aria-disabled", "true");
    act(() => {
      button.click();
    });
    expect(sendCommand).toHaveBeenCalledOnce();

    await act(async () => {
      first.resolve({ status: "failed", error: "timeout" });
      await first.promise;
    });
    expect(button).toHaveAttribute("aria-disabled", "false");
    expect(screen.getByRole("status")).toHaveTextContent("The booth didn't answer. Try again.");

    act(() => {
      button.click();
    });
    expect(screen.getByRole("status")).toBeEmptyDOMElement();
    await act(async () => {
      second.resolve({ status: "accepted" });
      await second.promise;
    });
    expect(screen.getByRole("status")).toBeEmptyDOMElement();
  });

  it("needs a brain client", () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    function Consumer() {
      useBrainClient();
      return null;
    }
    expect(() => render(<Consumer />)).toThrow("useBrainClient needs a BrainClientContext provider.");
  });
});
