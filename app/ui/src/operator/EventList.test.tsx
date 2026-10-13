import { act, cleanup, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { BrainClientContext, useBrainClient } from "../brain/BrainClientContext.ts";
import type { BrainClient, CommandResult } from "../brain/types.ts";
import { operatorEventListWithRows } from "../fixtures/inlineFixtures.ts";
import { eventRows, longEventName, longEventVenue, operatorFixtures } from "../fixtures/operatorFixtures.ts";
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
    expect(wedding).toHaveTextContent("Sat 21 Nov 2026");
    expect(wedding).toHaveTextContent("The Glasshouse, Tagaytay");
    expect(wedding).toHaveTextContent("1 Guest Cycle");
    expect(wedding).toHaveTextContent("Not used yet");
    expect(wedding).toHaveAttribute("data-event-id", "event-2");

    expect(within(birthday).getByRole("heading", { level: 2 })).toHaveTextContent("Mara's 30th");
    expect(birthday).not.toHaveTextContent("RUNNING");
    expect(birthday).toHaveTextContent("Sat 3 Oct 2026");
    expect(birthday).toHaveTextContent("42 Guest Cycles");
    expect(birthday).toHaveTextContent("Last used Sat 3 Oct 2026");
    expect(birthday).not.toHaveTextContent("No venue");
    expect(screen.queryByText("No Events yet.")).not.toBeInTheDocument();
  });

  it("keeps the order the brain sent, whatever the dates say", () => {
    const sent = [eventRows(3)[2], eventRows(3)[0], eventRows(3)[1]].filter((row) => row !== undefined);
    renderList(stubClient(vi.fn()), sent);
    const names = within(screen.getByRole("list", { name: "Events" }))
      .getAllByRole("heading", { level: 2 })
      .map((heading) => heading.textContent);
    expect(names).toEqual(sent.map((row) => row.name));
  });

  it("renders a long list with one row per Event and wraps very long text in place", () => {
    const events = eventRows(30);
    renderList(stubClient(vi.fn()), events);
    expect(within(screen.getByRole("list", { name: "Events" })).getAllByRole("listitem")).toHaveLength(30);

    const long = operatorFixtures()["operator-event-list-long"]?.presentation.eventList?.events ?? [];
    cleanup();
    renderList(stubClient(vi.fn()), long);
    const [first] = within(screen.getByRole("list", { name: "Events" })).getAllByRole("listitem");
    expect(first).toHaveTextContent(longEventName);
    expect(first).toHaveTextContent(longEventVenue);
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
