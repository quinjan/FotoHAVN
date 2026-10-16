import { act, cleanup, fireEvent, render, screen, within } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { BrainClientContext } from "../brain/BrainClientContext.ts";
import type { BrainClient } from "../brain/types.ts";
import type { FiltersView, GuestCycleView } from "../protocol/generated.ts";
import { sentCommands, stubClient } from "../test/operatorHarness.tsx";
import { FiltersStep } from "./FiltersStep.tsx";

const cycle: GuestCycleView = {
  id: "cycle-1",
  step: "filters",
  steps: [
    { step: "template", state: "done", canGoBack: true },
    { step: "photographs", state: "done", canGoBack: true },
    { step: "filters", state: "current", canGoBack: false },
    { step: "keepsake", state: "upcoming", canGoBack: false },
  ],
  startOverAvailable: true,
};

const looks = [
  { id: "naturale-v1", name: "Naturale", description: "Original colors. No filter.", thumbnailKey: "signature-strip.naturale-v1.thumb" },
  { id: "sepia-v1", name: "Sepia", description: "Soft brown tones with faded contrast.", thumbnailKey: "signature-strip.sepia-v1.thumb" },
  { id: "classic-v1", name: "Classic", description: "Warm colors with a golden tint.", thumbnailKey: null },
];

function view(parts: Partial<FiltersView> = {}): FiltersView {
  return {
    looks,
    selectedLookId: "naturale-v1",
    stripPreviewKey: "signature-strip.naturale-v1.r1",
    composing: false,
    compositionFailed: false,
    canFinish: true,
    ...parts,
  };
}

function ui(client: BrainClient, filters: FiltersView) {
  return (
    <BrainClientContext value={client}>
      <FiltersStep cycle={cycle} view={filters} />
    </BrainClientContext>
  );
}

/** The browser reports an image's size when it loads; jsdom never loads one, so a test says when, and how big. */
function loadPreview(width = 600, height = 1800): void {
  const pending = [...document.querySelectorAll<HTMLImageElement>("img[alt='']")].filter(
    (image) => image.closest("fieldset") === null && image.closest("[data-fit]") === null,
  );
  const target = pending.at(-1);
  if (target === undefined) {
    throw new Error("no preview is loading");
  }
  Object.defineProperty(target, "naturalWidth", { value: width, configurable: true });
  Object.defineProperty(target, "naturalHeight", { value: height, configurable: true });
  fireEvent.load(target);
}

/** jsdom has no AnimationEvent, so React listens for the prefixed name; a real browser sends `animationend`. */
function endAnimation(element: Element): void {
  fireEvent(element, new Event("webkitAnimationEnd", { bubbles: true }));
}

function stubReducedMotion(matches: boolean): void {
  vi.stubGlobal(
    "matchMedia",
    vi.fn(() => ({ matches, addEventListener: vi.fn(), removeEventListener: vi.fn() })),
  );
}

function strip(): HTMLElement {
  return screen.getByRole("img", { name: "Preview of your Photo Strip" });
}

describe("FiltersStep", () => {
  afterEach(() => {
    cleanup();
    vi.unstubAllGlobals();
  });

  it("shows the heading, the supporting line, and the Looks as one radio group with the selected Look chosen", () => {
    render(ui(stubClient(), view()));
    expect(screen.getByRole("heading", { level: 1, name: "Find the feeling." })).toBeVisible();
    expect(screen.getByText("Choose a filter for your photographs.")).toBeVisible();
    const group = screen.getByRole("group", { name: "Filters" });
    const radios = within(group).getAllByRole("radio");
    expect(radios.map((radio) => radio.getAttribute("value"))).toEqual(["naturale-v1", "sepia-v1", "classic-v1"]);
    expect(radios.map((radio) => (radio as HTMLInputElement).checked)).toEqual([true, false, false]);
    expect(screen.getByRole("radio", { name: "Sepia" })).toHaveAccessibleDescription("Soft brown tones with faded contrast.");
  });

  it("serves each Look's thumbnail from its preview key and leaves a quiet box where there is none yet", () => {
    const { container } = render(ui(stubClient(), view()));
    const thumbnails = [...container.querySelectorAll("fieldset img")].map((image) => image.getAttribute("src"));
    expect(thumbnails).toEqual([
      "https://preview.fotohavn/signature-strip.naturale-v1.thumb.png",
      "https://preview.fotohavn/signature-strip.sepia-v1.thumb.png",
    ]);
    expect(container.querySelectorAll("fieldset li")).toHaveLength(3);
  });

  it("sends SelectLook with the Look the guest chooses, and keeps the brain's choice on screen until it changes it", () => {
    const client = stubClient();
    render(ui(client, view()));
    fireEvent.click(screen.getByRole("radio", { name: "Sepia" }));
    expect(sentCommands(client)).toEqual([["SelectLook", { lookId: "sepia-v1" }]]);
    expect(screen.getByRole("radio", { name: "Naturale" })).toBeChecked();
  });

  it("sends FinishStrip from FINISH MY STRIP, the view's one primary action", () => {
    const client = stubClient();
    render(ui(client, view()));
    const finish = screen.getByRole("button", { name: "FINISH MY STRIP" });
    expect(finish).toHaveAttribute("data-fotohavn-action", "primary");
    expect(document.querySelectorAll('[data-fotohavn-action="primary"]')).toHaveLength(1);
    fireEvent.click(finish);
    expect(sentCommands(client)).toEqual([["FinishStrip", {}]]);
  });

  it("makes FINISH MY STRIP unavailable, and silent, while the brain says it can't finish", () => {
    const client = stubClient();
    render(ui(client, view({ canFinish: false })));
    const finish = screen.getByRole("button", { name: "FINISH MY STRIP" });
    expect(finish).toHaveAttribute("aria-disabled", "true");
    fireEvent.click(finish);
    expect(sentCommands(client)).toEqual([]);
  });

  it("shows the strip preview from its key once the picture has loaded, as a print at the picture's own proportion", () => {
    render(ui(stubClient(), view()));
    expect(screen.queryByRole("img", { name: "Preview of your Photo Strip" })).toBeNull();
    loadPreview(600, 1800);
    expect(strip()).toHaveAttribute("src", "https://preview.fotohavn/signature-strip.naturale-v1.r1.png");
    expect(strip()).toHaveAttribute("width", "600");
    expect(strip()).toHaveAttribute("height", "1800");
    expect(strip().closest("[data-rotated]")).toBeNull();
  });

  it("turns a landscape picture a quarter turn, as Panorama Four is everywhere on the Guest Screen", () => {
    render(ui(stubClient(), view({ stripPreviewKey: "panorama-four.naturale-v1.r1" })));
    loadPreview(1800, 600);
    expect(strip().closest("[data-rotated]")).not.toBeNull();
  });

  it("falls back to the Signature Strip's proportion when the picture reports no size", () => {
    render(ui(stubClient(), view()));
    loadPreview(0, 0);
    expect(strip()).toHaveAttribute("width", "600");
    expect(strip()).toHaveAttribute("height", "1800");
  });

  it("shows no strip, and no error, where the transport has no host to serve previews", () => {
    const client: BrainClient = { ...stubClient(), previewUrl: undefined };
    const { container } = render(ui(client, view()));
    expect(container.querySelectorAll("img")).toHaveLength(0);
    expect(screen.getByRole("heading", { level: 1 })).toBeVisible();
  });

  it("waits quietly before the first composition: no strip, a polite line, FINISH unavailable", () => {
    render(ui(stubClient(), view({ stripPreviewKey: null, composing: true, canFinish: false })));
    expect(screen.getByRole("status")).toHaveTextContent("Preparing your strip.");
    expect(screen.queryByRole("img", { name: "Preview of your Photo Strip" })).toBeNull();
    expect(screen.getByRole("button", { name: "FINISH MY STRIP" })).toHaveAttribute("aria-disabled", "true");
  });

  it("keeps the strip on screen while composing, and announces the line through a live region that was already there", () => {
    const client = stubClient();
    const { rerender } = render(ui(client, view()));
    loadPreview();
    const live = screen.getByRole("status");
    expect(live).toBeEmptyDOMElement();

    rerender(ui(client, view({ selectedLookId: "sepia-v1", composing: true, canFinish: false })));
    expect(screen.getByRole("status")).toBe(live);
    expect(live).toHaveTextContent("Preparing your strip.");
    expect(strip()).toBeVisible();
    expect(document.querySelector("[data-composing]")).not.toBeNull();
  });

  it("Q25: a failed composition keeps the previous strip, says so inline, and offers TRY AGAIN in place of FINISH", () => {
    const client = stubClient();
    const { rerender } = render(ui(client, view()));
    loadPreview();
    rerender(ui(client, view({ selectedLookId: "sepia-v1", compositionFailed: true, canFinish: false })));

    expect(screen.getByText("Your strip couldn't be prepared. Try again.")).toBeVisible();
    expect(strip()).toBeVisible();
    expect(strip()).toHaveAttribute("src", "https://preview.fotohavn/signature-strip.naturale-v1.r1.png");
    expect(screen.queryByRole("button", { name: "FINISH MY STRIP" })).toBeNull();
    const retry = screen.getByRole("button", { name: "TRY AGAIN" });
    expect(retry).toHaveAttribute("data-fotohavn-action", "primary");
    expect(document.querySelectorAll('[data-fotohavn-action="primary"]')).toHaveLength(1);
    expect(document.querySelector("[data-status-tone='alert']")).not.toBeNull();

    fireEvent.click(retry);
    expect(sentCommands(client)).toEqual([["RetryComposition", {}]]);
  });

  it("crossfades to a new picture over the old one, then drops the old one when the fade ends", () => {
    const client = stubClient();
    const { rerender, container } = render(ui(client, view()));
    loadPreview();
    expect(container.querySelectorAll("[data-entering]")).toHaveLength(0);

    rerender(ui(client, view({ selectedLookId: "sepia-v1", stripPreviewKey: "signature-strip.sepia-v1.r2" })));
    // The old picture stays until the new one has loaded.
    expect(strip()).toHaveAttribute("src", "https://preview.fotohavn/signature-strip.naturale-v1.r1.png");
    loadPreview();
    expect(screen.getAllByRole("img", { name: "Preview of your Photo Strip" })).toHaveLength(1);
    const entering = container.querySelector<HTMLElement>("[data-entering]");
    expect(entering).not.toBeNull();
    if (entering === null) {
      return;
    }
    expect(within(entering).getByRole("img")).toHaveAttribute("src", "https://preview.fotohavn/signature-strip.sepia-v1.r2.png");

    // An animation on something inside the layer doesn't end the crossfade.
    endAnimation(within(entering).getByRole("img"));
    expect(container.querySelector("[data-entering]")).not.toBeNull();
    endAnimation(entering);
    expect(container.querySelector("[data-entering]")).toBeNull();
    expect(strip()).toHaveAttribute("src", "https://preview.fotohavn/signature-strip.sepia-v1.r2.png");
  });

  it("replaces the picture at once under reduced motion", () => {
    stubReducedMotion(true);
    const client = stubClient();
    const { rerender, container } = render(ui(client, view()));
    loadPreview();
    rerender(ui(client, view({ selectedLookId: "sepia-v1", stripPreviewKey: "signature-strip.sepia-v1.r2" })));
    loadPreview();
    expect(container.querySelectorAll("[data-entering]")).toHaveLength(0);
    expect(strip()).toHaveAttribute("src", "https://preview.fotohavn/signature-strip.sepia-v1.r2.png");
  });

  it("keeps the picture it has when the key goes away, and doesn't load one it already shows", () => {
    const client = stubClient();
    const { rerender } = render(ui(client, view()));
    loadPreview();
    rerender(ui(client, view({ composing: true })));
    expect(() => {
      loadPreview();
    }).toThrow("no preview is loading");
    rerender(ui(client, view({ stripPreviewKey: null })));
    expect(strip()).toHaveAttribute("src", "https://preview.fotohavn/signature-strip.naturale-v1.r1.png");
  });

  it("puts many Looks in two columns and keeps their descriptions for assistive technology only", () => {
    const many = ["a", "b", "c", "d", "e", "f", "g"].map((id) => ({ id, name: `Look ${id}`, description: `About ${id}.`, thumbnailKey: null }));
    const { container } = render(ui(stubClient(), view({ looks: many, selectedLookId: "a" })));
    const list = container.querySelector<HTMLElement>("[data-dense]");
    expect(list).not.toBeNull();
    expect(list?.style.getPropertyValue("--look-rows")).toBe("4");
    expect(screen.getByRole("radio", { name: "Look c" })).toHaveAccessibleDescription("About c.");
  });

  it("keeps a short list in one column with its descriptions showing", () => {
    const { container } = render(ui(stubClient(), view()));
    expect(container.querySelector("[data-dense]")).toBeNull();
    expect(screen.getByText("Original colors. No filter.")).toBeVisible();
  });

  it("answers Look changes and the finish while a command is still on its way, without waiting for the brain", () => {
    const client = stubClient();
    vi.mocked(client.sendCommand).mockReturnValue(new Promise(() => undefined));
    render(ui(client, view()));
    act(() => {
      fireEvent.click(screen.getByRole("radio", { name: "Classic" }));
      fireEvent.click(screen.getByRole("button", { name: "FINISH MY STRIP" }));
    });
    expect(sentCommands(client).map(([name]) => name)).toEqual(["SelectLook", "FinishStrip"]);
  });
});
