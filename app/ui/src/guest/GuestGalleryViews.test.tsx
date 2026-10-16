import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient } from "../brain/types.ts";
import { guestGalleryFixtures } from "../fixtures/guestGalleryFixtures.ts";
import type { GuestGalleryView as GuestGalleryData, GuestOverlays, GuestStripDetailView as GuestStripDetailData } from "../protocol/generated.ts";
import { sentCommands, stubClient } from "../test/operatorHarness.tsx";
import { forgetOpenedStrip } from "./galleryFocus.ts";
import { GuestGalleryView } from "./GuestGalleryView.tsx";
import { GuestStripDetailView } from "./GuestStripDetailView.tsx";

const noOverlays: GuestOverlays = { exitHold: null, pinDialog: null, phaseTimerWarning: null, startOverConfirmation: null, notice: null };
const warning = (remainingSeconds: number, phase: "guestGallery" | "template" = "guestGallery"): Partial<GuestOverlays> => ({ phaseTimerWarning: { phase, remainingSeconds } });

function galleryOf(name: string): { readonly view: GuestGalleryData; readonly overlays: GuestOverlays } {
  const presentation = guestGalleryFixtures[name]?.presentation;
  if (presentation?.guestGallery === null || presentation?.guestGallery === undefined) {
    throw new Error(`no gallery in ${name}`);
  }
  return { view: presentation.guestGallery, overlays: presentation.overlays };
}

function detailOf(name: string): { readonly view: GuestStripDetailData; readonly overlays: GuestOverlays } {
  const presentation = guestGalleryFixtures[name]?.presentation;
  if (presentation?.guestStripDetail === null || presentation?.guestStripDetail === undefined) {
    throw new Error(`no detail in ${name}`);
  }
  return { view: presentation.guestStripDetail, overlays: presentation.overlays };
}

function withClient(client: BrainClient, ui: React.ReactNode) {
  return <BrainClientContext value={client}>{ui}</BrainClientContext>;
}

function stubObserver() {
  const observers: { readonly callback: IntersectionObserverCallback }[] = [];
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
  return () => {
    act(() => {
      observers.at(-1)?.callback([{ isIntersecting: true } as IntersectionObserverEntry], {} as IntersectionObserver);
    });
  };
}

afterEach(() => {
  cleanup();
  forgetOpenedStrip();
  vi.unstubAllGlobals();
});

describe("the guest gallery view", () => {
  it("shows 'Look at you.', each Photo Strip with its alternative text, and BACK TO START", () => {
    const { view, overlays } = galleryOf("guest-ui-gallery-four-strips");
    render(withClient(stubClient(), <GuestGalleryView view={view} overlays={overlays} />));
    expect(screen.getByRole("heading", { level: 1, name: "Look at you." })).toBeVisible();
    expect(screen.getAllByRole("img", { name: "Photo Strip from this Event" })).toHaveLength(4);
    expect(screen.getByRole("button", { name: "BACK TO START" })).toBeVisible();
  });

  it("sends OpenGuestStrip with the strip's id, and BackToStart", async () => {
    const { view, overlays } = galleryOf("guest-ui-gallery-four-strips");
    const client = stubClient();
    render(withClient(client, <GuestGalleryView view={view} overlays={overlays} />));
    fireEvent.click(screen.getAllByRole("img", { name: "Photo Strip from this Event" })[1]?.closest("button") as HTMLElement);
    fireEvent.click(screen.getByRole("button", { name: "BACK TO START" }));
    await waitFor(() => {
      expect(sentCommands(client)).toHaveLength(2);
    });
    expect(sentCommands(client)).toEqual([
      ["OpenGuestStrip", { guestCycleId: view.strips[1]?.guestCycleId }],
      ["BackToStart", {}],
    ]);
  });

  it("asks for the next page with its cursor once, when the row nears its end, and again for the next cursor", async () => {
    const intersect = stubObserver();
    const { view, overlays } = galleryOf("guest-ui-gallery-many-strips");
    const client = stubClient();
    const { rerender } = render(withClient(client, <GuestGalleryView view={view} overlays={overlays} />));
    intersect();
    intersect();
    await waitFor(() => {
      expect(sentCommands(client)).toEqual([["LoadMoreGuestStrips", { cursor: "cursor-1" }]]);
    });
    rerender(withClient(client, <GuestGalleryView view={{ ...view, nextCursor: "cursor-2" }} overlays={overlays} />));
    intersect();
    await waitFor(() => {
      expect(sentCommands(client)).toHaveLength(2);
    });
    expect(sentCommands(client)[1]).toEqual(["LoadMoreGuestStrips", { cursor: "cursor-2" }]);
  });

  it("asks for nothing on the last page or while a page loads", () => {
    const intersect = stubObserver();
    const { view, overlays } = galleryOf("guest-ui-gallery-loading-more");
    const client = stubClient();
    const { rerender } = render(withClient(client, <GuestGalleryView view={view} overlays={overlays} />));
    rerender(withClient(client, <GuestGalleryView view={{ ...view, loadingMore: false, nextCursor: null }} overlays={overlays} />));
    intersect();
    expect(sentCommands(client)).toEqual([]);
  });

  it("says a page is loading, alone while the first page loads", () => {
    const first = galleryOf("guest-ui-gallery-loading-first");
    render(withClient(stubClient(), <GuestGalleryView view={first.view} overlays={first.overlays} />));
    expect(screen.getByText("Loading photographs.")).toBeVisible();
    expect(screen.queryByRole("img")).toBeNull();
    cleanup();
    const more = galleryOf("guest-ui-gallery-loading-more");
    render(withClient(stubClient(), <GuestGalleryView view={more.view} overlays={more.overlays} />));
    expect(screen.getByText("Loading more photographs.")).toBeVisible();
  });

  it("puts focus back on the strip that was opened when the row comes back from its detail", () => {
    const { view, overlays } = galleryOf("guest-ui-gallery-four-strips");
    const first = render(withClient(stubClient(), <GuestGalleryView view={view} overlays={overlays} />));
    fireEvent.click(screen.getAllByRole("img")[2]?.closest("button") as HTMLElement);
    first.unmount();
    render(withClient(stubClient(), <GuestGalleryView view={view} overlays={overlays} />));
    expect(screen.getAllByRole("img")[2]?.closest("button")).toHaveFocus();
  });

  it("tabs through each strip, then BACK TO START", () => {
    const { view, overlays } = galleryOf("guest-ui-gallery-four-strips");
    render(withClient(stubClient(), <GuestGalleryView view={view} overlays={overlays} />));
    expect(screen.getAllByRole("button").map((button) => button.querySelector("img")?.getAttribute("alt") ?? button.textContent)).toEqual([
      "Photo Strip from this Event",
      "Photo Strip from this Event",
      "Photo Strip from this Event",
      "Photo Strip from this Event",
      "BACK TO START",
    ]);
  });
});

describe("the guest strip detail view", () => {
  it("shows the strip and its arranged Captures with their alternative text, CLOSE, and BACK TO START", () => {
    const { view, overlays } = detailOf("guest-ui-strip-detail");
    render(withClient(stubClient(), <GuestStripDetailView view={view} overlays={overlays} />));
    expect(screen.getAllByRole("img", { name: "Photo Strip from this Event" })).toHaveLength(1);
    expect(screen.getAllByRole("img", { name: "Photograph from this Event" })).toHaveLength(4);
    expect(screen.getAllByRole("button").map((button) => button.textContent)).toEqual(["CLOSE", "BACK TO START"]);
  });

  it("starts focus on CLOSE, and sends CloseGuestStrip and BackToStart", async () => {
    const { view, overlays } = detailOf("guest-ui-strip-detail");
    const client = stubClient();
    render(withClient(client, <GuestStripDetailView view={view} overlays={overlays} />));
    expect(screen.getByRole("button", { name: "CLOSE" })).toHaveFocus();
    fireEvent.click(screen.getByRole("button", { name: "CLOSE" }));
    fireEvent.click(screen.getByRole("button", { name: "BACK TO START" }));
    await waitFor(() => {
      expect(sentCommands(client)).toHaveLength(2);
    });
    expect(sentCommands(client)).toEqual([["CloseGuestStrip", {}], ["BackToStart", {}]]);
  });

  it("turns a landscape strip a quarter counterclockwise and keeps a portrait one upright", () => {
    const landscape = detailOf("guest-ui-strip-detail-landscape");
    render(withClient(stubClient(), <GuestStripDetailView view={landscape.view} overlays={landscape.overlays} />));
    expect(screen.getByAltText("Photo Strip from this Event").parentElement).toHaveAttribute("data-rotated", "true");
    cleanup();
    const portrait = detailOf("guest-ui-strip-detail");
    render(withClient(stubClient(), <GuestStripDetailView view={portrait.view} overlays={portrait.overlays} />));
    expect(screen.getByAltText("Photo Strip from this Event").parentElement).not.toHaveAttribute("data-rotated");
  });
});

describe("the Phase Timer warning on the gallery screens", () => {
  type Mount = (overlays: GuestOverlays, client: BrainClient) => React.ReactNode;
  const screens: readonly (readonly [string, Mount])[] = [
    [
      "the row",
      (overlays, client) => withClient(client, <GuestGalleryView view={galleryOf("guest-ui-gallery-four-strips").view} overlays={overlays} />),
    ],
    [
      "the detail",
      (overlays, client) => withClient(client, <GuestStripDetailView view={detailOf("guest-ui-strip-detail").view} overlays={overlays} />),
    ],
  ];

  it.each(screens)("shows the seconds left on %s, with MORE TIME sending MoreTime, and nothing when there is no warning", async (_name, mount) => {
    const client = stubClient();
    const { rerender } = render(mount(noOverlays, client));
    expect(screen.queryByRole("button", { name: "MORE TIME" })).toBeNull();

    rerender(mount({ ...noOverlays, ...warning(18) }, client));
    expect(screen.getByText("SECONDS LEFT")).toBeVisible();
    expect(screen.getByText("18")).toBeVisible();
    fireEvent.click(screen.getByRole("button", { name: "MORE TIME" }));
    await waitFor(() => {
      expect(sentCommands(client)).toEqual([["MoreTime", {}]]);
    });

    rerender(mount({ ...noOverlays, ...warning(17) }, client));
    expect(screen.getByText("17")).toBeVisible();
    rerender(mount(noOverlays, client));
    expect(screen.queryByRole("button", { name: "MORE TIME" })).toBeNull();
  });

  it("shows the last second as one second", () => {
    const { view, overlays } = galleryOf("guest-ui-gallery-four-strips");
    render(withClient(stubClient(), <GuestGalleryView view={view} overlays={{ ...overlays, ...warning(1) }} />));
    expect(screen.getByText("SECOND LEFT")).toBeVisible();
  });

  it("ignores another phase's warning: this screen has only the gallery's", () => {
    const { view, overlays } = galleryOf("guest-ui-gallery-four-strips");
    render(withClient(stubClient(), <GuestGalleryView view={view} overlays={{ ...overlays, ...warning(10, "template") }} />));
    expect(screen.queryByRole("button", { name: "MORE TIME" })).toBeNull();
  });

  it("arrives once: a warning already under way when the screen mounts shows at rest, a new one arrives", () => {
    const { view, overlays } = galleryOf("guest-ui-gallery-four-strips");
    const client = stubClient();
    const ui = (extra: Partial<GuestOverlays>) => withClient(client, <GuestGalleryView view={view} overlays={{ ...overlays, ...extra }} />);
    const mounted = render(ui(warning(12)));
    expect(document.querySelector("[data-phase='guest-gallery']")).toHaveAttribute("data-motion", "static");
    mounted.rerender(ui({}));
    mounted.rerender(ui(warning(20)));
    expect(document.querySelector("[data-phase='guest-gallery']")).not.toHaveAttribute("data-motion", "static");
  });

  it("keeps the warning inside the screen's frame, over nothing the guest needs to press", () => {
    const { view, overlays } = galleryOf("guest-ui-gallery-four-strips");
    render(withClient(stubClient(), <GuestGalleryView view={view} overlays={{ ...overlays, ...warning(15) }} />));
    const frame = document.querySelector("[data-phase='guest-gallery']")?.parentElement;
    expect(frame).toContainElement(screen.getByRole("button", { name: "BACK TO START" }));
  });
});

describe("what a guest can see in the gallery views", () => {
  const allowed = [/\d+ seconds? left\. Choose MORE TIME to stay a little longer\./g, /Look at you\./g, /BACK TO START/g, /CLOSE/g, /Loading (more )?photographs\./g, /\d+ SECONDS? LEFT/g, /MORE TIME/g];

  it.each(Object.keys(guestGalleryFixtures))("%s carries no text beyond the heading, the actions, the loading line, and the warning, and no id", (name) => {
    const presentation = guestGalleryFixtures[name]?.presentation;
    if (presentation === undefined) {
      throw new Error(`no fixture ${name}`);
    }
    const ui =
      presentation.guestGallery !== null ? (
        <GuestGalleryView view={presentation.guestGallery} overlays={presentation.overlays} />
      ) : presentation.guestStripDetail !== null ? (
        <GuestStripDetailView view={presentation.guestStripDetail} overlays={presentation.overlays} />
      ) : null;
    const { container } = render(withClient(stubClient(), ui));
    let text = container.textContent;
    for (const phrase of allowed) {
      text = text.replace(phrase, "");
    }
    expect(text.replace(/\s+/g, "").replace(/\d+/g, "")).toBe("");
    expect(container.querySelectorAll("dl, dt, dd, time, [data-guest-cycle], [data-gallery-event]")).toHaveLength(0);
    for (const control of screen.queryAllByRole("button")) {
      expect(control.textContent).not.toMatch(/DELETE|FOLDER|OPEN/);
    }
    const ids = [...(presentation.guestGallery?.strips.map((strip) => strip.guestCycleId) ?? [])];
    for (const id of ids) {
      expect(container.innerHTML).not.toContain(id);
    }
  });
});
