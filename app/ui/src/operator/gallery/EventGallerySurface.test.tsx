import { act, cleanup, fireEvent, screen, waitFor, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { operatorGalleryFixtures } from "../../fixtures/operatorGalleryFixtures.ts";
import { runningEventPanel } from "../../fixtures/operatorSetupFixtures.ts";
import type { EventGalleryView, RunningEventPanel } from "../../protocol/generated.ts";
import { renderOperator, sentCommands, stubClient } from "../../test/operatorHarness.tsx";
import { EventGallerySurface } from "./EventGallerySurface.tsx";
import { forgetOpened } from "./galleryFocus.ts";
function nth<T>(items: readonly T[], index: number): T {
  const item = items[index];
  if (item === undefined) {
    throw new Error(`No item ${String(index)}.`);
  }
  return item;
}

function baseView(name = "operator-ui-gallery-mixed"): EventGalleryView {
  const view = operatorGalleryFixtures[name]?.presentation.eventGallery;
  if (view === null || view === undefined) {
    throw new Error(`no gallery in ${name}`);
  }
  return view;
}

function surface(view: EventGalleryView, running: RunningEventPanel | null = null) {
  return <EventGallerySurface view={view} running={running} />;
}

const rejected = { status: "rejected", reason: "notAvailable" } as const;

interface FakeObserver {
  readonly callback: IntersectionObserverCallback;
}

function stubObserver(): { readonly observers: FakeObserver[]; readonly intersect: () => void } {
  const observers: FakeObserver[] = [];
  vi.stubGlobal(
    "IntersectionObserver",
    class {
      constructor(callback: IntersectionObserverCallback) {
        observers.push({ callback });
      }
      observe() {
        return undefined;
      }
      disconnect() {
        return undefined;
      }
    },
  );
  return {
    observers,
    intersect: () => {
      act(() => {
        observers.at(-1)?.callback([{ isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver);
      });
    },
  };
}

afterEach(() => {
  cleanup();
  forgetOpened();
  vi.unstubAllGlobals();
});

describe("the operator Event Gallery surface", () => {
  it("shows the Event's name above 'Event Gallery.', Guest Cycles with their time and ending, and BACK TO EVENTS", () => {
    renderOperator(surface(baseView()));
    expect(screen.getByRole("heading", { level: 1, name: "Event Gallery." })).toBeVisible();
    expect(screen.getByText("Event 5: Santos and Reyes celebration")).toBeVisible();
    const items = within(screen.getByRole("list", { name: "Guest Cycles" })).getAllByRole("listitem");
    expect(items).toHaveLength(7);
    expect(items[0]).toHaveTextContent("Completed");
    expect(items[0]).toHaveTextContent("Signature Strip · Naturale");
    expect(items[1]).toHaveTextContent("Started over");
    expect(items[1]).toHaveTextContent("No Photo Strip");
    expect(items[4]).toHaveTextContent("These photographs can't be found.");
    expect(items[5]).toHaveTextContent("Unrecoverable");
    expect(screen.getByRole("button", { name: "BACK TO EVENTS" })).toBeVisible();
  });

  it("sends each control's command with its Guest Cycle or cursor", async () => {
    const { client } = renderOperator(surface(baseView()));
    fireEvent.click(screen.getByRole("button", { name: "BACK TO EVENTS" }));
    fireEvent.click(nth(screen.getAllByRole("button", { name: "OPEN" }), 2));
    fireEvent.click(nth(screen.getAllByRole("button", { name: "DELETE" }), 1));
    fireEvent.click(screen.getByRole("button", { name: "SHOW MORE" }));
    await waitFor(() => {
      expect(sentCommands(client)).toHaveLength(4);
    });
    expect(sentCommands(client)).toEqual([
      ["CloseGallery", {}],
      ["OpenGuestCycle", { guestCycleId: "cycle-7" }],
      ["DeleteGuestCycle", { guestCycleId: "cycle-8" }],
      ["LoadMoreGuestCycles", { cursor: "cursor-1" }],
    ]);
  });

  it("marks the running Event's gallery as live, and no other", () => {
    const { rerender } = renderOperator(surface({ ...baseView(), running: true }));
    expect(screen.getByText("RUNNING")).toBeVisible();
    expect(screen.getByText("New photographs appear here as guests finish.")).toBeVisible();
    rerender(surface(baseView()));
    expect(screen.queryByText("RUNNING")).toBeNull();
  });

  it("shows Operator Assistance in the running Event's gallery, with a way back to the panel", async () => {
    const panel = runningEventPanel({ assistance: { reason: "camera" } });
    const { client } = renderOperator(surface({ ...baseView(), running: true }, panel));
    expect(screen.getByText("The camera needs attention.")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "GO TO THE RUNNING EVENT" }));
    await waitFor(() => {
      expect(sentCommands(client)).toEqual([["CloseGallery", {}]]);
    });
  });

  it("says there are no photographs yet, without paging controls", () => {
    renderOperator(surface({ ...baseView(), items: [], nextCursor: null, empty: true }));
    expect(screen.getByText("No photographs yet.")).toBeVisible();
    expect(screen.queryByRole("button", { name: "SHOW MORE" })).toBeNull();
    expect(screen.queryByRole("button", { name: "TRY AGAIN" })).toBeNull();
  });

  it("says the first page is loading, and neither 'No photographs yet.' nor a list", () => {
    renderOperator(surface({ ...baseView(), items: [], nextCursor: null, loadingMore: true }));
    expect(screen.getByText("Loading photographs.")).toBeVisible();
    expect(screen.queryByText("No photographs yet.")).toBeNull();
    expect(screen.queryByRole("list")).toBeNull();
  });

  it("disables SHOW MORE with the reason while a later page loads", () => {
    renderOperator(surface({ ...baseView(), loadingMore: true }));
    const more = screen.getByRole("button", { name: "SHOW MORE" });
    expect(more).toHaveAttribute("aria-disabled", "true");
    expect(more).toHaveAccessibleDescription("Loading more photographs.");
    expect(screen.getByRole("list", { name: "Guest Cycles" })).toHaveAttribute("aria-busy", "true");
  });

  it("offers TRY AGAIN when a later page failed: the Guest Cycles stay, SHOW MORE goes, and it asks for that cursor", async () => {
    const { client } = renderOperator(surface({ ...baseView(), loadFailed: true }));
    expect(screen.getByText("Couldn't load these photographs.")).toBeVisible();
    expect(screen.getAllByRole("listitem")).toHaveLength(7);
    expect(screen.queryByRole("button", { name: "SHOW MORE" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "TRY AGAIN" }));
    await waitFor(() => {
      expect(sentCommands(client)).toEqual([["LoadMoreGuestCycles", { cursor: "cursor-1" }]]);
    });
  });

  it("offers TRY AGAIN when the first page failed: no list, no empty line, and it opens the Event's gallery again", async () => {
    const { client } = renderOperator(surface({ ...baseView(), items: [], nextCursor: null, loadFailed: true }));
    expect(screen.getByText("Couldn't load these photographs.")).toBeVisible();
    expect(screen.queryByText("No photographs yet.")).toBeNull();
    expect(screen.queryByRole("list")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "TRY AGAIN" }));
    await waitFor(() => {
      expect(sentCommands(client)).toEqual([["OpenGallery", { eventId: "event-5" }]]);
    });
  });

  it("shows no failure line, and no way to ask again, when nothing failed", () => {
    renderOperator(surface(baseView()));
    expect(screen.queryByText("Couldn't load these photographs.")).toBeNull();
    expect(screen.queryByRole("button", { name: "TRY AGAIN" })).toBeNull();
  });

  it("lists a large Event in full and has nothing more to ask for after the last page", () => {
    renderOperator(surface(baseView("operator-ui-gallery-large")));
    expect(screen.getByRole("list", { name: "Guest Cycles" }).children).toHaveLength(320);
    expect(screen.queryByRole("button", { name: "SHOW MORE" })).toBeNull();
  });
});

describe("the operator Event Gallery surface: near-end loading", () => {
  it("asks for the next page once for each cursor when the end comes near, and quietly when the brain declines", async () => {
    const { intersect } = stubObserver();
    const client = stubClient({ result: rejected });
    const { rerender } = renderOperator(surface(baseView()), client);
    intersect();
    intersect();
    await waitFor(() => {
      expect(sentCommands(client)).toEqual([["LoadMoreGuestCycles", { cursor: "cursor-1" }]]);
    });
    expect(screen.queryByText("That isn't ready yet.")).toBeNull();

    rerender(surface({ ...baseView(), nextCursor: "cursor-2" }));
    intersect();
    await waitFor(() => {
      expect(sentCommands(client)).toHaveLength(2);
    });
    expect(sentCommands(client)[1]).toEqual(["LoadMoreGuestCycles", { cursor: "cursor-2" }]);
  });

  it("says so when SHOW MORE, pressed by hand, is declined", async () => {
    renderOperator(surface(baseView()), stubClient({ result: rejected }));
    fireEvent.click(screen.getByRole("button", { name: "SHOW MORE" }));
    expect(await screen.findByText("That isn't ready yet.")).toBeVisible();
  });

  it("asks for nothing while a page loads, after a failure, or on the last page", () => {
    const { observers } = stubObserver();
    const { rerender } = renderOperator(surface({ ...baseView(), loadingMore: true }));
    rerender(surface({ ...baseView(), loadFailed: true }));
    rerender(surface({ ...baseView(), nextCursor: null }));
    expect(observers).toHaveLength(0);
  });
});

describe("the operator Event Gallery surface: focus", () => {
  it("puts focus back on the Guest Cycle that was open when the list comes back from its detail", () => {
    const first = renderOperator(surface(baseView()));
    fireEvent.click(nth(screen.getAllByRole("button", { name: "OPEN" }), 3));
    first.unmount();
    renderOperator(surface(baseView()));
    expect(screen.getAllByRole("button", { name: "OPEN" })[3]).toHaveFocus();
  });

  it("remembers it for one return only, and only for the same Event", () => {
    const first = renderOperator(surface(baseView()));
    fireEvent.click(nth(screen.getAllByRole("button", { name: "OPEN" }), 3));
    first.unmount();
    const second = renderOperator(surface(baseView()));
    second.unmount();
    renderOperator(surface(baseView()));
    expect(document.body).toHaveFocus();
    cleanup();

    const another = renderOperator(surface(baseView()));
    fireEvent.click(nth(screen.getAllByRole("button", { name: "OPEN" }), 3));
    another.unmount();
    renderOperator(surface({ ...baseView(), eventId: "event-9" }));
    expect(document.body).toHaveFocus();
  });
});
