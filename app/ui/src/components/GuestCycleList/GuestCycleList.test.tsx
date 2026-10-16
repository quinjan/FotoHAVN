import { act, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { GuestStrip, OperatorGuestCycle } from "./galleryModel.ts";
import { GuestCycleList } from "./GuestCycleList.tsx";

const image = (name: string, width = 600, height = 1800) => ({ src: `${name}.png`, alt: `${name} alt`, width, height });

function nth<T>(items: readonly T[], index: number): T {
  const item = items[index];
  if (item === undefined) {
    throw new Error(`No item ${String(index)}.`);
  }
  return item;
}

const cycles: readonly OperatorGuestCycle[] = [
  {
    id: "c3",
    time: "9:14 PM",
    outcome: "completed",
    templateName: "Signature Strip",
    lookName: "Sepia",
    strip: image("strip-3"),
    captures: [image("capture-a", 1920, 1080)],
  },
  {
    id: "c2",
    time: "9:06 PM",
    outcome: "started-over",
    templateName: "Panorama Four",
    strip: null,
    captures: [image("capture-b", 1920, 1080), image("capture-c", 1920, 1080)],
  },
  { id: "c1", time: "8:42 PM", outcome: "app-closed", templateName: "Butter Gingham", strip: null, captures: [] },
];

const strips: readonly GuestStrip[] = [
  { id: "s2", ...image("guest-2") },
  { id: "s1", ...image("guest-1", 1800, 600) },
];

function operatorList(overrides: Partial<Parameters<typeof GuestCycleList>[0] & { mode: "operator" }> = {}) {
  const onOpen = vi.fn();
  const onDelete = vi.fn();
  const onLoadMore = vi.fn();
  const view = render(
    <GuestCycleList mode="operator" cycles={cycles} hasMore={false} onOpen={onOpen} onDelete={onDelete} onLoadMore={onLoadMore} {...overrides} />,
  );
  return { ...view, onOpen, onDelete, onLoadMore };
}

describe("GuestCycleList, operator", () => {
  it("lists Guest Cycles in the given order with a Meta caption each", () => {
    operatorList();
    const list = screen.getByRole("list", { name: "Guest Cycles" });
    const items = within(list).getAllByRole("listitem");
    expect(items).toHaveLength(3);
    expect(items[0]).toHaveTextContent("9:14 PM");
    expect(items[0]).toHaveTextContent("Completed");
    expect(items[0]).toHaveTextContent("Signature Strip · Sepia");
    expect(items[0]).not.toHaveTextContent("No Photo Strip");
    expect(items[1]).toHaveTextContent("Started over");
    expect(items[1]).toHaveTextContent("Panorama Four");
    expect(items[1]).not.toHaveTextContent("·");
    expect(items[1]).toHaveTextContent("No Photo Strip");
    expect(items[2]).toHaveTextContent("App closed");
  });

  it("shows the Photo Strip as a print, or the kept Captures, behind a pointer-only open target", () => {
    const { container, onOpen } = operatorList();
    const targets = container.querySelectorAll<HTMLButtonElement>("button[tabindex='-1']");
    expect(targets).toHaveLength(3);
    for (const target of targets) {
      expect(target).toHaveAttribute("aria-hidden", "true");
    }
    expect(targets[0]?.querySelector("img")).toHaveAttribute("src", "strip-3.png");
    expect(targets[0]?.querySelector("[data-fit='scale']")).not.toBeNull();
    expect(targets[1]?.querySelectorAll("img")).toHaveLength(2);
    expect(targets[1]?.querySelector("[data-tone='paper']")).not.toBeNull();
    expect(targets[2]?.querySelectorAll("img")).toHaveLength(0);
    fireEvent.click(nth([...targets], 1));
    expect(onOpen).toHaveBeenCalledWith("c2");
  });

  it("opens and deletes by text actions described by the caption", () => {
    const { onOpen, onDelete } = operatorList();
    const opens = screen.getAllByRole("button", { name: "OPEN" });
    const deletes = screen.getAllByRole("button", { name: "DELETE" });
    expect(opens).toHaveLength(3);
    expect(deletes).toHaveLength(3);
    expect(opens[0]).toHaveAccessibleDescription("9:14 PM Completed Signature Strip · Sepia");
    expect(deletes[2]).toHaveAccessibleDescription("8:42 PM App closed Butter Gingham No Photo Strip");
    fireEvent.click(nth(opens, 0));
    expect(onOpen).toHaveBeenCalledWith("c3");
    fireEvent.click(nth(deletes, 2));
    expect(onDelete).toHaveBeenCalledWith("c1");
  });

  it("says so when there are no photographs yet", () => {
    operatorList({ cycles: [] });
    expect(screen.getByText("No photographs yet.")).toBeInTheDocument();
    expect(screen.queryByRole("list")).toBeNull();
    expect(screen.queryByRole("button", { name: "SHOW MORE" })).toBeNull();
  });

  it("asks for the next page with SHOW MORE, unavailable with its reason while loading", () => {
    const { onLoadMore, rerender } = operatorList({ hasMore: true, label: "Photographs from this Event", className: "page" });
    expect(screen.getByRole("list", { name: "Photographs from this Event" })).not.toHaveAttribute("aria-busy");
    const more = screen.getByRole("button", { name: "SHOW MORE" });
    expect(more).toHaveAttribute("aria-disabled", "false");
    expect(screen.getByRole("status")).toHaveTextContent("");
    fireEvent.click(more);
    expect(onLoadMore).toHaveBeenCalledOnce();

    rerender(
      <GuestCycleList mode="operator" cycles={cycles} hasMore loading onOpen={vi.fn()} onDelete={vi.fn()} onLoadMore={onLoadMore} />,
    );
    expect(screen.getByRole("list")).toHaveAttribute("aria-busy", "true");
    expect(more).toHaveAttribute("aria-disabled", "true");
    expect(more).toHaveAccessibleDescription("Loading more photographs.");
    fireEvent.click(more);
    expect(onLoadMore).toHaveBeenCalledOnce();
  });

  it("keeps focus on SHOW MORE while pages remain, and moves it to the first new Guest Cycle after the last page", () => {
    const first = cycles.slice(0, 1);
    const props = { mode: "operator", onOpen: vi.fn(), onDelete: vi.fn(), onLoadMore: vi.fn() } as const;
    const { rerender } = render(<GuestCycleList {...props} cycles={first} hasMore />);
    const more = screen.getByRole("button", { name: "SHOW MORE" });
    more.focus();
    fireEvent.click(more);
    rerender(<GuestCycleList {...props} cycles={cycles.slice(0, 2)} hasMore />);
    expect(more).toHaveFocus();

    fireEvent.click(more);
    rerender(<GuestCycleList {...props} cycles={cycles} hasMore={false} />);
    expect(screen.queryByRole("button", { name: "SHOW MORE" })).toBeNull();
    expect(screen.getAllByRole("button", { name: "OPEN" })[2]).toHaveFocus();
  });

  it("leaves focus alone when a load brings nothing", () => {
    const props = { mode: "operator", onOpen: vi.fn(), onDelete: vi.fn(), onLoadMore: vi.fn() } as const;
    const { rerender } = render(<GuestCycleList {...props} cycles={cycles} hasMore />);
    fireEvent.click(screen.getByRole("button", { name: "SHOW MORE" }));
    rerender(<GuestCycleList {...props} cycles={cycles} hasMore={false} />);
    expect(document.body).toHaveFocus();
  });

  it("forgets the open action of a Guest Cycle that leaves the list", () => {
    const props = { mode: "operator", onOpen: vi.fn(), onDelete: vi.fn(), onLoadMore: vi.fn() } as const;
    const { rerender } = render(<GuestCycleList {...props} cycles={cycles} hasMore />);
    rerender(<GuestCycleList {...props} cycles={cycles.slice(1)} hasMore />);
    expect(screen.getAllByRole("button", { name: "OPEN" })).toHaveLength(2);
  });
});

describe("GuestCycleList, guest", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("is the guest gallery: the heading, a touch-scrolling row of prints, and BACK TO START", () => {
    const onOpen = vi.fn();
    const onBack = vi.fn();
    const { container } = render(<GuestCycleList mode="guest" strips={strips} onOpen={onOpen} onBack={onBack} className="view" />);

    const heading = screen.getByRole("heading", { level: 1, name: "Look at you." });
    expect(heading.querySelector("em")).toHaveTextContent("you.");
    const section = screen.getByRole("region", { name: "Look at you." });
    expect(section).toHaveAttribute("data-tone", "light");
    expect(section.className).toContain("view");

    const row = container.querySelector("[data-fotohavn-scroll-region]");
    expect(row).not.toBeNull();
    const list = screen.getByRole("list", { name: "Look at you." });
    expect(row?.contains(list)).toBe(true);
    const prints = within(list).getAllByRole("button", { name: "guest-2 alt" });
    expect(prints).toHaveLength(1);
    fireEvent.click(within(list).getByRole("button", { name: "guest-1 alt" }));
    expect(onOpen).toHaveBeenCalledWith("s1");

    fireEvent.click(screen.getByRole("button", { name: "BACK TO START" }));
    expect(onBack).toHaveBeenCalledOnce();
  });

  it("shows completed Photo Strips as resting prints, landscape ones turned, with no metadata or delete", () => {
    render(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} headingLevel={2} />);
    expect(screen.getByRole("heading", { level: 2 })).toBeInTheDocument();
    const [portrait, landscape] = screen.getAllByRole("img");
    expect(portrait?.parentElement).toHaveAttribute("data-shadow", "contact");
    expect(portrait?.parentElement).not.toHaveAttribute("data-rotated");
    expect(landscape?.parentElement).toHaveAttribute("data-rotated", "true");
    expect(screen.queryByRole("button", { name: "DELETE" })).toBeNull();
    expect(screen.queryByRole("button", { name: "OPEN FOLDER" })).toBeNull();
    expect(screen.getAllByRole("button")).toHaveLength(3);
  });

  it("asks for more strips when the end of the row scrolls into view", () => {
    const observers: {
      callback: IntersectionObserverCallback;
      options?: IntersectionObserverInit | undefined;
      disconnect: ReturnType<typeof vi.fn>;
      observed: Element[];
    }[] = [];
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
    const onLoadMore = vi.fn();
    const { container, rerender, unmount } = render(
      <GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} hasMore onLoadMore={onLoadMore} />,
    );
    const row = container.querySelector("[data-fotohavn-scroll-region]");
    expect(observers).toHaveLength(1);
    const observer = observers[0];
    expect(observer?.options?.root).toBe(row);
    expect(observer?.observed[0]).toBe(row?.querySelector("[aria-hidden='true']"));

    act(() => {
      observer?.callback([{ isIntersecting: false } as IntersectionObserverEntry], {} as IntersectionObserver);
    });
    expect(onLoadMore).not.toHaveBeenCalled();
    act(() => {
      observer?.callback([{ isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver);
    });
    expect(onLoadMore).toHaveBeenCalledOnce();

    rerender(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} hasMore={false} onLoadMore={onLoadMore} />);
    expect(observer?.disconnect).toHaveBeenCalledOnce();
    expect(observers).toHaveLength(1);
    unmount();
  });

  it("observes nothing without more strips, a loader, or IntersectionObserver", () => {
    const constructed = vi.fn();
    vi.stubGlobal(
      "IntersectionObserver",
      class {
        constructor() {
          constructed();
        }
        observe() {
          return undefined;
        }
        disconnect() {
          return undefined;
        }
      },
    );
    const { rerender } = render(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} hasMore />);
    rerender(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} onLoadMore={vi.fn()} />);
    expect(constructed).not.toHaveBeenCalled();

    vi.stubGlobal("IntersectionObserver", undefined);
    rerender(<GuestCycleList mode="guest" strips={strips} onOpen={vi.fn()} onBack={vi.fn()} hasMore onLoadMore={vi.fn()} />);
    expect(screen.getAllByRole("listitem")).toHaveLength(2);
  });
});
