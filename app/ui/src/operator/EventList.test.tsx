import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { BrainClientContext, useBrainClient } from "../brain/BrainClientContext.ts";
import type { BrainClient, CommandResult } from "../brain/types.ts";
import { operatorEventListWithRows } from "../fixtures/inlineFixtures.ts";
import { eventRows, longEventName, longEventVenue, operatorFixtures } from "../fixtures/operatorFixtures.ts";
import { runningEventPanel } from "../fixtures/operatorSetupFixtures.ts";
import { renderOperator, sentCommands, stubClient } from "../test/operatorHarness.tsx";
import { EventList } from "./EventList.tsx";

function deferred() {
  let resolve: (result: CommandResult) => void = () => undefined;
  const promise = new Promise<CommandResult>((next) => {
    resolve = next;
  });
  return { promise, resolve };
}

const rows = operatorEventListWithRows.presentation.eventList?.events ?? [];

function renderList(events = rows, running = null as ReturnType<typeof runningEventPanel> | null, client = stubClient()) {
  return renderOperator(<EventList events={events} running={running} />, client);
}

const rowOf = (name: RegExp | string) =>
  within(screen.getByRole("list", { name: "Events" })).getByRole("heading", { level: 2, name }).closest("li") as HTMLElement;

describe("EventList", () => {
  it("renders the empty state with the primary action", () => {
    renderList([]);
    expect(screen.getByRole("heading", { level: 1, name: "Events" })).toBeVisible();
    expect(screen.getByText("No Events yet.")).toBeVisible();
    expect(screen.getByRole("button", { name: "NEW EVENT" })).toHaveTextContent(/^NEW EVENT$/);
    expect(screen.getByRole("button", { name: "NEW EVENT" })).toHaveAttribute("data-fotohavn-action", "primary");
    expect(screen.getByRole("button", { name: "NEW EVENT" }).querySelector("[data-direction='right'] svg")).not.toBeNull();
    expect(screen.queryByRole("list")).not.toBeInTheDocument();
  });

  it("renders each Event row in the brain's order with its name, date, venue, and counts", () => {
    renderList();
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
    renderList(sent);
    const names = within(screen.getByRole("list", { name: "Events" }))
      .getAllByRole("heading", { level: 2 })
      .map((heading) => heading.textContent);
    expect(names).toEqual(sent.map((row) => row.name));
  });

  it("renders a long list with one row per Event and wraps very long text in place", () => {
    renderList(eventRows(30));
    expect(within(screen.getByRole("list", { name: "Events" })).getAllByRole("listitem")).toHaveLength(30);

    const long = operatorFixtures()["operator-event-list-long"]?.presentation.eventList?.events ?? [];
    cleanup();
    renderList(long);
    const [first] = within(screen.getByRole("list", { name: "Events" })).getAllByRole("listitem");
    expect(first).toHaveTextContent(longEventName);
    expect(first).toHaveTextContent(longEventVenue);
  });

  describe("row actions", () => {
    it("offers every action on each row while no Event runs, with the name in each accessible name", () => {
      const idle = eventRows(2, false);
      renderList(idle);
      const row = within(rowOf(idle[0]?.name ?? ""));
      expect(row.getAllByRole("button").map((button) => button.textContent)).toEqual(["START", "GALLERY", "EDIT", "OPEN FOLDER", "DELETE"]);
      const name = idle[0]?.name ?? "";
      for (const label of [`Start ${name}`, `Open the gallery of ${name}`, `Edit ${name}`, `Open the folder of ${name}`, `Delete ${name}`]) {
        expect(row.getByRole("button", { name: label })).toBeVisible();
      }
      expect(row.getByRole("button", { name: `Start ${name}` })).toHaveAttribute("aria-disabled", "false");
      expect(screen.queryByText("End the running Event to start another.")).toBeNull();
      for (const button of row.getAllByRole("button")) {
        expect(button).toHaveAttribute("data-fotohavn-action", "text");
      }
    });

    it("gives the running row only Gallery and Open folder, and the other rows a disabled Start with its reason", () => {
      const running = eventRows(3);
      renderList(running, runningEventPanel({ name: running[0]?.name ?? "" }));
      const runningRow = within(rowOf(new RegExp(`^${running[0]?.name ?? ""}$`)));
      expect(runningRow.getAllByRole("button").map((button) => button.textContent)).toEqual(["GALLERY", "OPEN FOLDER"]);
      expect(runningRow.getByText("RUNNING")).toBeVisible();

      const other = rowOf(new RegExp(`^${running[1]?.name ?? ""}$`));
      expect(within(other).getAllByRole("button").map((button) => button.textContent)).toEqual(["START", "GALLERY", "EDIT", "OPEN FOLDER", "DELETE"]);
      const start = within(other).getByRole("button", { name: `Start ${running[1]?.name ?? ""}` });
      expect(start).toHaveAttribute("aria-disabled", "true");
      expect(within(other).getByText("End the running Event to start another.")).toBeVisible();
      expect(start).toHaveAccessibleDescription("End the running Event to start another.");
    });

    it("hides Edit and Delete where the brain doesn't allow them, and Gallery and Open folder likewise", () => {
      const [row] = eventRows(1, false);
      if (row === undefined) {
        throw new Error("no row");
      }
      renderList([{ ...row, actions: { canStart: true, canEdit: false, canDelete: false, canOpenFolder: false, canOpenGallery: false } }]);
      expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["NEW EVENT", "START"]);
    });

    it.each([
      ["Start", "StartEvent"],
      ["Open the gallery of", "OpenGallery"],
      ["Edit", "EditEvent"],
      ["Open the folder of", "OpenEventFolder"],
      ["Delete", "DeleteEvent"],
    ] as const)("%s sends %s with the row's Event id", async (label, command) => {
      const idle = eventRows(2, false);
      const client = stubClient();
      renderList(idle, null, client);
      await act(async () => {
        within(rowOf(idle[1]?.name ?? "")).getByRole("button", { name: new RegExp(`^${label}`) }).click();
        await Promise.resolve();
      });
      expect(sentCommands(client)).toEqual([[command, { eventId: idle[1]?.id }]]);
    });

    it("politely says so when the brain declines a row action, such as the gallery before it exists", async () => {
      const idle = eventRows(1, false);
      renderList(idle, null, stubClient({ result: { status: "rejected", reason: "notAvailable" } }));
      await act(async () => {
        screen.getByRole("button", { name: /^Open the gallery of/ }).click();
        await Promise.resolve();
      });
      expect(screen.getAllByRole("status").some((status) => status.textContent === "That isn't ready yet.")).toBe(true);
    });

    it("sends one command per row action at a time", async () => {
      const first = deferred();
      const sendCommand = vi.fn<BrainClient["sendCommand"]>().mockReturnValue(first.promise);
      const idle = eventRows(1, false);
      const client = { ...stubClient(), sendCommand };
      renderList(idle, null, client);
      const start = screen.getByRole("button", { name: /^Start/ });
      act(() => {
        start.click();
        start.click();
      });
      expect(sendCommand).toHaveBeenCalledOnce();
      await act(async () => {
        first.resolve({ status: "accepted" });
        await first.promise;
      });
      act(() => {
        start.click();
      });
      expect(sendCommand).toHaveBeenCalledTimes(2);
    });
  });

  describe("NEW EVENT and the running-Event panel", () => {
    it("sends OpenNewEvent and politely shows a rejection", async () => {
      const client = stubClient({ result: { status: "rejected", reason: "notAvailable" } });
      renderList([], null, client);

      await act(async () => {
        screen.getByRole("button", { name: "NEW EVENT" }).click();
        await Promise.resolve();
      });

      expect(client.sendCommand).toHaveBeenCalledExactlyOnceWith("OpenNewEvent", {});
      expect(screen.getAllByRole("status").some((status) => status.textContent === "That isn't ready yet.")).toBe(true);
    });

    it("ignores clicks while a command is pending, then clears the notice when accepted", async () => {
      const first = deferred();
      const second = deferred();
      const sendCommand = vi
        .fn<BrainClient["sendCommand"]>()
        .mockReturnValueOnce(first.promise)
        .mockReturnValueOnce(second.promise);
      renderList([], null, { ...stubClient(), sendCommand });
      const button = screen.getByRole("button", { name: "NEW EVENT" });
      const notice = () => screen.getAllByRole("status").map((status) => status.textContent).join("");

      act(() => {
        button.click();
        button.click();
      });
      expect(sendCommand).toHaveBeenCalledOnce();

      await act(async () => {
        first.resolve({ status: "failed", error: "timeout" });
        await first.promise;
      });
      expect(notice()).toBe("The booth didn't answer. Try again.");

      act(() => {
        button.click();
      });
      await act(async () => {
        second.resolve({ status: "accepted" });
        await second.promise;
      });
      expect(notice()).toBe("");
    });

    it("lets the operator close a notice", async () => {
      renderList([], null, stubClient({ result: { status: "rejected", reason: "notAvailable" } }));
      await act(async () => {
        screen.getByRole("button", { name: "NEW EVENT" }).click();
        await Promise.resolve();
      });
      fireEvent.click(screen.getByRole("button", { name: "CLOSE" }));
      expect(screen.queryByRole("button", { name: "CLOSE" })).toBeNull();
    });

    it("pins the running Event's panel above the rows, with NEW EVENT still the one primary action", () => {
      const running = eventRows(2);
      renderList(running, runningEventPanel({ name: running[0]?.name ?? "" }));
      const panel = screen.getByRole("group", { name: running[0]?.name ?? "" });
      const list = screen.getByRole("list", { name: "Events" });
      expect(panel.compareDocumentPosition(list) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      expect(within(panel).getByRole("button", { name: "OPEN GALLERY" })).toBeVisible();
      expect(within(panel).getByRole("button", { name: "END EVENT" })).toBeVisible();
      expect(within(panel).queryByRole("button", { name: "RETRY" })).toBeNull();
      expect(document.querySelectorAll('[data-fotohavn-action="primary"]')).toHaveLength(1);
      expect(screen.getByRole("button", { name: "NEW EVENT" })).toHaveAttribute("data-fotohavn-action", "primary");
    });

    it("shows no panel when no Event runs", () => {
      renderList(eventRows(2, false));
      expect(screen.queryByRole("group")).toBeNull();
    });

    it("during Operator Assistance gives the primary action to RETRY and makes NEW EVENT secondary", () => {
      renderList(eventRows(2), runningEventPanel({ assistance: { reason: "camera" }, currentStep: "operatorAssistance" }));
      expect(document.querySelectorAll('[data-fotohavn-action="primary"]')).toHaveLength(1);
      expect(screen.getByRole("button", { name: "RETRY" })).toHaveAttribute("data-fotohavn-action", "primary");
      expect(screen.getByRole("button", { name: "NEW EVENT" })).toHaveAttribute("data-fotohavn-action", "secondary");
      expect(screen.getByRole("group")).toHaveTextContent("The camera needs attention.");
    });
  });

  it("needs a brain client", () => {
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    function Consumer() {
      useBrainClient();
      return null;
    }
    expect(() => render(<Consumer />)).toThrow("useBrainClient needs a BrainClientContext provider.");
    expect(BrainClientContext).toBeDefined();
  });
});
