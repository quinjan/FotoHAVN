import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { templateAndLook, type GuestStrip, type OperatorGuestCycle } from "./galleryModel.ts";
import { GuestCycleList } from "./GuestCycleList.tsx";
function nth<T>(items: readonly T[], index: number): T {
  const item = items[index];
  if (item === undefined) {
    throw new Error(`No item ${String(index)}.`);
  }
  return item;
}

const image = (name: string, width = 600, height = 1800) => ({ src: `${name}.png`, alt: `${name} alt`, width, height });

function cycle(id: string, parts: Partial<OperatorGuestCycle> = {}): OperatorGuestCycle {
  return { id, time: "13 Oct, 8:42 PM", outcome: "completed", templateName: "Signature Strip", lookName: "Naturale", strip: image(id), captures: [], ...parts };
}

const bare = (captures: OperatorGuestCycle["captures"]): OperatorGuestCycle => ({ id: "c1", time: "13 Oct, 8:42 PM", outcome: "completed", strip: null, captures });

const operatorProps = { mode: "operator", onOpen: vi.fn(), onDelete: vi.fn(), onLoadMore: vi.fn() } as const;

interface FakeObserver {
  readonly callback: IntersectionObserverCallback;
  readonly options: IntersectionObserverInit | undefined;
  readonly disconnect: ReturnType<typeof vi.fn>;
  readonly observed: Element[];
}

/** Replaces IntersectionObserver with a recorder; `intersect(observer)` reports its target as in view. */
function stubObserver(): { readonly observers: FakeObserver[]; readonly intersect: (observer: FakeObserver | undefined) => void } {
  const observers: FakeObserver[] = [];
  vi.stubGlobal(
    "IntersectionObserver",
    class {
      readonly disconnect = vi.fn();
      readonly observed: Element[] = [];
      constructor(callback: IntersectionObserverCallback, options?: IntersectionObserverInit) {
        observers.push({ callback, options, disconnect: this.disconnect, observed: this.observed });
      }
      observe(element: Element) {
        this.observed.push(element);
      }
    },
  );
  return {
    observers,
    intersect: (observer) => {
      act(() => {
        observer?.callback([{ isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver);
      });
    },
  };
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("templateAndLook", () => {
  it("joins what is recorded, and is null when nothing is", () => {
    expect(templateAndLook({ templateName: "Signature Strip", lookName: "Sepia" })).toBe("Signature Strip · Sepia");
    expect(templateAndLook({ templateName: "Signature Strip" })).toBe("Signature Strip");
    expect(templateAndLook({ lookName: "Sepia" })).toBe("Sepia");
    expect(templateAndLook({})).toBeNull();
  });
});

describe("GuestCycleList, operator: states", () => {
  it("leaves out the Template and Look of a Guest Cycle that recorded none, and says No Photo Strip", () => {
    const { container } = render(<GuestCycleList {...operatorProps} cycles={[bare([image("kept", 1640, 1230)])]} hasMore={false} />);
    const item = within(screen.getByRole("list", { name: "Guest Cycles" })).getByRole("listitem");
    expect(item).toHaveTextContent("13 Oct, 8:42 PM Completed No Photo Strip");
    expect(container.querySelectorAll("img")).toHaveLength(1);
  });

  it("shows a Guest Cycle whose files are gone as an empty mat with the reason, still openable and deletable", () => {
    const onOpen = vi.fn();
    const onDelete = vi.fn();
    const { container } = render(
      <GuestCycleList {...operatorProps} onOpen={onOpen} onDelete={onDelete} cycles={[cycle("gone", { strip: null, unavailable: true, outcome: "app-closed" })]} hasMore={false} />,
    );
    const item = within(screen.getByRole("list", { name: "Guest Cycles" })).getByRole("listitem");
    expect(item).toHaveAttribute("data-unavailable", "true");
    expect(item).toHaveTextContent("App closed");
    expect(item).toHaveTextContent("These photographs can't be found.");
    expect(item).not.toHaveTextContent("No Photo Strip");
    expect(container.querySelectorAll("img")).toHaveLength(0);
    expect(item.querySelector("[data-tone='paper']")).not.toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "OPEN" }));
    fireEvent.click(screen.getByRole("button", { name: "DELETE" }));
    expect(onOpen).toHaveBeenCalledWith("gone");
    expect(onDelete).toHaveBeenCalledWith("gone");
  });

  it("takes focus itself, and only, when it is empty", () => {
    const { container, rerender } = render(<GuestCycleList {...operatorProps} cycles={[]} hasMore={false} />);
    expect(container.firstElementChild).toHaveAttribute("tabindex", "-1");
    rerender(<GuestCycleList {...operatorProps} cycles={[cycle("c1")]} hasMore={false} />);
    expect(container.firstElementChild).not.toHaveAttribute("tabindex");
  });
});

describe("GuestCycleList, operator: focus", () => {
  const three = [cycle("c3"), cycle("c2"), cycle("c1")];

  it("restores focus to the Guest Cycle that was open, or to the one now at its position", () => {
    const { unmount } = render(<GuestCycleList {...operatorProps} cycles={three} hasMore={false} restoreFocus={{ id: "c2", index: 1 }} />);
    expect(screen.getAllByRole("button", { name: "OPEN" })[1]).toHaveFocus();
    unmount();
    render(<GuestCycleList {...operatorProps} cycles={[cycle("c3"), cycle("c1")]} hasMore={false} restoreFocus={{ id: "c2", index: 1 }} />);
    expect(screen.getAllByRole("button", { name: "OPEN" })[1]).toHaveFocus();
  });

  it("restores to the last Guest Cycle when the position is past the end, and to the list when none is left", () => {
    const { unmount } = render(<GuestCycleList {...operatorProps} cycles={[cycle("c3")]} hasMore={false} restoreFocus={{ id: "gone", index: 4 }} />);
    expect(screen.getByRole("button", { name: "OPEN" })).toHaveFocus();
    unmount();
    const { container } = render(<GuestCycleList {...operatorProps} cycles={[]} hasMore={false} restoreFocus={{ id: "gone", index: 0 }} />);
    expect(container.firstElementChild).toHaveFocus();
  });

  it("moves focus to what takes a deleted Guest Cycle's place when the opener is gone with it", () => {
    const onDelete = vi.fn();
    const { rerender } = render(<GuestCycleList {...operatorProps} onDelete={onDelete} cycles={three} hasMore={false} />);
    fireEvent.click(nth(screen.getAllByRole("button", { name: "DELETE" }), 1));
    expect(onDelete).toHaveBeenCalledWith("c2");
    (document.activeElement as HTMLElement | null)?.blur();
    rerender(<GuestCycleList {...operatorProps} onDelete={onDelete} cycles={[nth(three, 0), nth(three, 2)]} hasMore={false} />);
    expect(screen.getAllByRole("button", { name: "OPEN" })[1]).toHaveFocus();
  });

  it("does not take focus back for a deletion when focus is somewhere else", () => {
    const { rerender } = render(
      <>
        <button type="button">Elsewhere</button>
        <GuestCycleList {...operatorProps} cycles={three} hasMore={false} />
      </>,
    );
    fireEvent.click(nth(screen.getAllByRole("button", { name: "DELETE" }), 0));
    screen.getByRole("button", { name: "Elsewhere" }).focus();
    rerender(
      <>
        <button type="button">Elsewhere</button>
        <GuestCycleList {...operatorProps} cycles={three.slice(1)} hasMore={false} />
      </>,
    );
    expect(screen.getByRole("button", { name: "Elsewhere" })).toHaveFocus();
  });

  it("keeps focus where it is when a Guest Cycle is deleted from the list and still listed", () => {
    const { rerender } = render(<GuestCycleList {...operatorProps} cycles={three} hasMore={false} />);
    const remove = nth(screen.getAllByRole("button", { name: "DELETE" }), 0);
    fireEvent.click(remove);
    remove.focus();
    rerender(<GuestCycleList {...operatorProps} cycles={[...three]} hasMore={false} />);
    expect(remove).toHaveFocus();
  });

  it("tabs through each Guest Cycle's OPEN and DELETE, then SHOW MORE", () => {
    render(<GuestCycleList {...operatorProps} cycles={three.slice(0, 2)} hasMore />);
    const order = [...document.querySelectorAll<HTMLElement>("button")].filter((button) => button.tabIndex >= 0).map((button) => button.textContent);
    expect(order).toEqual(["OPEN", "DELETE", "OPEN", "DELETE", "SHOW MORE"]);
  });
});

describe("GuestCycleList, operator: near-end loading", () => {
  it("watches the page's end only when it can ask, with a margin ahead of it", () => {
    const { observers } = stubObserver();
    const { rerender } = render(<GuestCycleList {...operatorProps} cycles={[cycle("c1")]} hasMore />);
    expect(observers).toHaveLength(0);
    rerender(<GuestCycleList {...operatorProps} cycles={[cycle("c1")]} hasMore onNearEnd={vi.fn()} />);
    expect(observers).toHaveLength(1);
    expect(observers[0]?.options?.root).toBeNull();
    expect(observers[0]?.options?.rootMargin).toBe("0px 0px 600px 0px");
  });

  it("asks once for each cursor, however often the end shows, and again for the next cursor", () => {
    const { observers, intersect } = stubObserver();
    const onNearEnd = vi.fn();
    const props = { ...operatorProps, hasMore: true, onNearEnd } as const;
    const { rerender } = render(<GuestCycleList {...props} cycles={[cycle("c1")]} pageKey="cursor-1" />);
    intersect(observers[0]);
    intersect(observers[0]);
    expect(onNearEnd).toHaveBeenCalledTimes(1);

    // The same cursor again (a re-render, a new handler): still asked for.
    rerender(<GuestCycleList {...props} onNearEnd={vi.fn()} cycles={[cycle("c1")]} pageKey="cursor-1" />);
    intersect(observers.at(-1));
    expect(onNearEnd).toHaveBeenCalledTimes(1);

    rerender(<GuestCycleList {...props} cycles={[cycle("c2"), cycle("c1")]} pageKey="cursor-2" />);
    intersect(observers.at(-1));
    expect(onNearEnd).toHaveBeenCalledTimes(2);
  });

  it("asks for nothing while a page loads, and not at all on the last page; the count of Guest Cycles keys the page by default", () => {
    const { observers, intersect } = stubObserver();
    const onNearEnd = vi.fn();
    const props = { ...operatorProps, onNearEnd } as const;
    const { rerender } = render(<GuestCycleList {...props} cycles={[cycle("c1")]} hasMore loading />);
    expect(observers).toHaveLength(0);
    rerender(<GuestCycleList {...props} cycles={[cycle("c1")]} hasMore={false} />);
    expect(observers).toHaveLength(0);
    rerender(<GuestCycleList {...props} cycles={[cycle("c1")]} hasMore />);
    intersect(observers[0]);
    expect(onNearEnd).toHaveBeenCalledTimes(1);
    rerender(<GuestCycleList {...props} cycles={[cycle("c2"), cycle("c1")]} hasMore />);
    intersect(observers.at(-1));
    expect(onNearEnd).toHaveBeenCalledTimes(2);
  });

  it("does not move focus when a page it asked for arrives; SHOW MORE stays the keyboard's way", () => {
    const { observers, intersect } = stubObserver();
    const { rerender } = render(<GuestCycleList {...operatorProps} onNearEnd={vi.fn()} cycles={[cycle("c1")]} hasMore />);
    intersect(observers[0]);
    rerender(<GuestCycleList {...operatorProps} onNearEnd={vi.fn()} cycles={[cycle("c2"), cycle("c1")]} hasMore={false} />);
    expect(document.body).toHaveFocus();
  });
});

describe("GuestCycleList, guest: what a guest sees", () => {
  const strips: readonly GuestStrip[] = [
    { id: "opaque-id-1", ...image("a", 600, 1800), alt: "Photo Strip from this Event" },
    { id: "opaque-id-2", ...image("b", 1800, 600), alt: "Photo Strip from this Event" },
  ];

  it("renders the heading, the images with their alternative text, and BACK TO START, and nothing else", () => {
    const { container } = render(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} />);
    const text = container.textContent;
    expect(text).toBe("Look at you.BACK TO START");
    expect(screen.getAllByRole("img", { name: "Photo Strip from this Event" })).toHaveLength(2);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["", "", "BACK TO START"]);
    for (const absent of ["DELETE", "OPEN FOLDER", "OPEN", "SHOW MORE", "Completed", "Template", "Look:", "Captures", "No Photo Strip"]) {
      expect(screen.queryByText(absent)).toBeNull();
    }
    expect(container.innerHTML).not.toContain("opaque-id");
    expect(container.querySelectorAll("[data-fotohavn-scroll-region]")).toHaveLength(1);
  });

  it("makes the row the one scrolling region, between the heading and BACK TO START, with nothing else able to scroll", () => {
    const { container } = render(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} />);
    const regions = container.querySelectorAll("[data-fotohavn-scroll-region]");
    expect(regions).toHaveLength(1);
    const section = screen.getByRole("region", { name: "Look at you." });
    expect([...section.children].map((child) => child.hasAttribute("data-fotohavn-scroll-region"))).toEqual([false, true, false]);
    expect(regions[0]).toContainElement(screen.getByRole("list", { name: "Look at you." }));
  });

  it("follows the row's scroll position: scrolling the row to its end shows the end marker that asks for more", () => {
    const { observers, intersect } = stubObserver();
    const onLoadMore = vi.fn();
    const { container } = render(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} hasMore onLoadMore={onLoadMore} />);
    const row = nth([...container.querySelectorAll<HTMLElement>("[data-fotohavn-scroll-region]")], 0);
    row.scrollLeft = 900;
    fireEvent.scroll(row);
    expect(row.scrollLeft).toBe(900);
    expect(observers[0]?.options?.rootMargin).toBe("0px 100% 0px 0px");
    intersect(observers[0]);
    expect(onLoadMore).toHaveBeenCalledTimes(1);
  });

  it("asks once for each cursor, never while loading, and again when the next cursor arrives", () => {
    const { observers, intersect } = stubObserver();
    const onLoadMore = vi.fn();
    const props = { mode: "guest", onOpen: vi.fn(), onBack: vi.fn(), hasMore: true, onLoadMore } as const;
    const { rerender } = render(<GuestCycleList {...props} strips={strips} pageKey="cursor-1" />);
    intersect(observers[0]);
    intersect(observers[0]);
    expect(onLoadMore).toHaveBeenCalledTimes(1);

    rerender(<GuestCycleList {...props} strips={strips} pageKey="cursor-1" loading />);
    expect(observers[0]?.disconnect).toHaveBeenCalled();
    const before = observers.length;
    rerender(<GuestCycleList {...props} strips={strips} pageKey="cursor-1" />);
    expect(observers.length).toBeGreaterThan(before);
    intersect(observers.at(-1));
    expect(onLoadMore).toHaveBeenCalledTimes(1);

    rerender(<GuestCycleList {...props} strips={[...strips, { id: "c", ...image("c") }]} pageKey="cursor-2" />);
    intersect(observers.at(-1));
    expect(onLoadMore).toHaveBeenCalledTimes(2);
  });

  it("says a page is loading at the end of the row, and alone while the first page loads", () => {
    const { rerender } = render(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} />);
    const status = screen.getByRole("status");
    expect(status).toHaveTextContent("");
    rerender(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} loading />);
    expect(status).toHaveTextContent("Loading more photographs.");
    expect(status).not.toHaveAttribute("data-alone");
    rerender(<GuestCycleList mode="guest" strips={[]} onOpen={vi.fn()} onBack={vi.fn()} loading />);
    expect(status).toHaveTextContent("Loading photographs.");
    expect(status).toHaveAttribute("data-alone", "true");
    expect(screen.queryByRole("list")).toBeNull();
    expect(screen.getByRole("button", { name: "BACK TO START" })).toBeInTheDocument();
  });

  it("restores focus to the strip that was opened, and leaves focus alone otherwise", () => {
    const { unmount } = render(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} restoreFocus="opaque-id-2" />);
    expect(screen.getAllByRole("img")[1]?.closest("button")).toHaveFocus();
    unmount();
    render(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} restoreFocus="deleted-meanwhile" />);
    expect(document.body).toHaveFocus();
  });

  it("tabs through each strip and then BACK TO START", () => {
    render(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} />);
    const order = screen.getAllByRole("button").map((button) => button.querySelector("img")?.getAttribute("alt") ?? button.textContent);
    expect(order).toEqual(["Photo Strip from this Event", "Photo Strip from this Event", "BACK TO START"]);
  });
});
